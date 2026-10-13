using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Json;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Accounts.Repositories;
using BodyOps.WebApi.Features.Backups.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Settings.Services;
using BodyOps.WebApi.Features.Weights.Repositories;
using BodyOps.WebApi.Features.Weights.Services;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Backups.Services;

/// <summary>
/// Writes an account to a JSON file and reads it back. Importing replaces the plan, the logged days, the weigh-ins
/// and the settings; the ids in the file only link the days to the plan and are given new values.
/// </summary>
/// <param name="userRepository">Account data access.</param>
/// <param name="planRepository">Plan data access.</param>
/// <param name="dayRepository">Day data access.</param>
/// <param name="weightRepository">Weigh-in data access.</param>
/// <param name="settingRepository">Settings data access.</param>
/// <param name="clock">Today in the configured time zone.</param>
/// <param name="timeProvider">The time source (export timestamp).</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="options">The BodyOps options.</param>
internal sealed class BackupService(
    IUserRepository userRepository,
    IPlanRepository planRepository,
    IDayRepository dayRepository,
    IWeightRepository weightRepository,
    ISettingRepository settingRepository,
    IAppClock clock,
    TimeProvider timeProvider,
    IGlobalizationHelper globalizationHelper,
    IOptions<BodyOpsOptions> options) : IBackupService
{
    private static readonly JsonSerializerOptions ReadOptions = CreateReadOptions();

    private readonly IUserRepository _userRepository = userRepository;
    private readonly IPlanRepository _planRepository = planRepository;
    private readonly IDayRepository _dayRepository = dayRepository;
    private readonly IWeightRepository _weightRepository = weightRepository;
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IAppClock _clock = clock;
    private readonly TimeProvider _timeProvider = timeProvider;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IOptions<BodyOpsOptions> _options = options;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BackupDTO>> ExportAsync(long userId)
    {
        var plan = await _planRepository.GetAsync(userId);
        var days = await _dayRepository.ListRangeAsync(userId, DayService.MinDate, _clock.Today.AddDays(366));
        var weights = await _weightRepository.ListAsync(userId);
        var setting = await _settingRepository.GetAsync(userId);

        var foods = plan.Foods.ToLookup(static food => food.PlanMealID);
        var backup = new BackupDTO
        {
            ExportedAt = _timeProvider.GetUtcNow().UtcDateTime,
            Settings = new BackupSettingsDTO { WeightGoalKg = setting?.WeightGoalKg, RestSeconds = setting?.RestSeconds },
            Plan = Enumerable.Range(1, 7).Select(weekday => new BackupPlanDayDTO
            {
                Weekday = weekday,
                Name = plan.Names.FirstOrDefault(name => name.Weekday == weekday)?.Name ?? string.Empty,
                Meals = plan.Meals.Where(meal => meal.Weekday == weekday).OrderBy(static meal => meal.SortOrder).Select(meal => new BackupMealDTO
                {
                    Id = meal.PlanMealID,
                    Name = meal.Name,
                    Time = MealTimeText.Format(meal.MealTime),
                    Foods = foods[meal.PlanMealID].OrderBy(static food => food.SortOrder)
                        .Select(static food => new BackupFoodDTO { Name = food.Name, Amount = food.Amount, Unit = food.Unit }).ToList(),
                }).ToList(),
                Exercises = plan.Exercises.Where(exercise => exercise.Weekday == weekday).OrderBy(static exercise => exercise.SortOrder).Select(static exercise => new BackupExerciseDTO
                {
                    Id = exercise.PlanExerciseID,
                    Type = exercise.ExerciseType,
                    Name = exercise.Name,
                    Detail = exercise.Detail,
                    Notes = exercise.Notes,
                    Muscles = exercise.Muscles is null ? null : MuscleDetector.Parse(exercise.Muscles),
                }).ToList(),
            }).ToList(),
            Days = DayBackupMapper.ToBackup(days),
            Weights = weights.Select(static entry => new BackupWeightDTO { Date = DateOnly.FromDateTime(entry.EntryDate), WeightKg = entry.WeightKg }).ToList(),
        };

        return ServiceApiResponse<BackupDTO>.Success(backup);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<BackupImportResultDTO>> ImportAsync(long userId, JsonElement document)
    {
        var maxBytes = _options.Value.MaxBackupBytes;
        if (document.ValueKind == JsonValueKind.Object && document.GetRawText().Length > maxBytes)
        {
            return ServiceApiResponse<BackupImportResultDTO>.PayloadTooLarge(
                await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.BackupTooLarge), maxBytes / (1024 * 1024)));
        }

        BackupDTO? backup = null;
        var source = "bodyops";
        try
        {
            if (document.ValueKind == JsonValueKind.Object
                && document.TryGetProperty("format", out var format) && format.ValueKind == JsonValueKind.String
                && format.GetString() == BackupDTO.FormatName)
            {
                backup = document.Deserialize<BackupDTO>(ReadOptions);
            }
            else if (LegacyBackupConverter.IsLegacy(document))
            {
                backup = LegacyBackupConverter.Convert(document);
                source = "corpoops";
            }
        }
        catch (Exception exception) when (exception is JsonException or InvalidOperationException or FormatException or KeyNotFoundException)
        {
            backup = null;
        }

        if (backup is null)
        {
            return await _globalizationHelper.ValidationProblemAsync<BackupImportResultDTO>("file", nameof(TranslationKeys.InvalidBackup));
        }

        return ServiceApiResponse<BackupImportResultDTO>.Success(await WriteAsync(userId, backup, source));
    }

    private static JsonSerializerOptions CreateReadOptions()
    {
        var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        JsonSetup.Apply(options);
        return options;
    }

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];

    private async Task<BackupImportResultDTO> WriteAsync(long userId, BackupDTO backup, string source)
    {
        var syncFrom = WeekMath.Monday(_clock.Today);
        var mealMap = new Dictionary<long, long>();
        var exerciseMap = new Dictionary<long, long>();
        var result = new BackupImportResultDTO { Source = source };

        await _userRepository.ClearDataAsync(userId);

        foreach (var day in backup.Plan.Where(static day => day.Weekday is >= 1 and <= 7).GroupBy(static day => day.Weekday).Select(static group => group.First()))
        {
            var dayName = Truncate(NameText.Clean(day.Name), PlanService.MaxDayName);
            if (dayName.Length > 0)
            {
                await _planRepository.SaveDayNameAsync(userId, day.Weekday, dayName);
            }

            var meals = day.Meals
                .Select(static meal => (meal.Id, Name: Truncate(NameText.Clean(meal.Name), PlanService.MaxMealName), Time: MealTimeText.TryParse(meal.Time, out var time) ? time : null, meal.Foods))
                .Where(static meal => meal.Name.Length > 0)
                .Take(PlanService.MaxMeals)
                .ToList();
            if (meals.Count > 0)
            {
                var json = PlanJson.Serialize(meals.Select(static meal => PlanJson.Meal(null, meal.Name, meal.Time, (meal.Foods ?? [])
                    .Select(static food => (Name: Truncate(NameText.Clean(food.Name), PlanService.MaxMealName), Amount: Math.Clamp(Math.Round(food.Amount, 2), 0, PlanService.MaxAmount), Unit: Enum.IsDefined(food.Unit) ? food.Unit : FoodUnit.Grams))
                    .Where(static food => food.Name.Length > 0)
                    .Take(PlanService.MaxFoods))));
                var saved = await _planRepository.SaveMealsAsync(userId, day.Weekday, json, syncFrom);
                var savedMeals = saved.Meals.OrderBy(static meal => meal.SortOrder).ToList();
                for (var i = 0; i < meals.Count && i < savedMeals.Count; i++)
                {
                    mealMap.TryAdd(meals[i].Id, savedMeals[i].PlanMealID);
                }

                result.Meals += savedMeals.Count;
            }

            var exercises = day.Exercises
                .Select(static exercise => (exercise.Id, Name: Truncate(NameText.Clean(exercise.Name), PlanService.MaxExerciseName), Exercise: exercise))
                .Where(static exercise => exercise.Name.Length > 0)
                .Take(PlanService.MaxExercises)
                .ToList();
            if (exercises.Count > 0)
            {
                var json = PlanJson.Serialize(exercises.Select(static item => PlanJson.Exercise(
                    null,
                    Enum.IsDefined(item.Exercise.Type) ? item.Exercise.Type : ExerciseType.Strength,
                    item.Name,
                    Truncate(NameText.Clean(item.Exercise.Detail), PlanService.MaxDetail),
                    Truncate((item.Exercise.Notes ?? string.Empty).Trim(), PlanService.MaxNotes),
                    MuscleDetector.Format(item.Exercise.Muscles))));
                var saved = (await _planRepository.SaveExercisesAsync(userId, day.Weekday, json, syncFrom)).OrderBy(static exercise => exercise.SortOrder).ToList();
                for (var i = 0; i < exercises.Count && i < saved.Count; i++)
                {
                    exerciseMap.TryAdd(exercises[i].Id, saved[i].PlanExerciseID);
                }

                result.Exercises += saved.Count;
            }
        }

        // Days: point their items at the new plan ids; items whose planned meal or exercise is gone get unique negative ids.
        var orphan = 0L;
        var days = backup.Days
            .Where(static day => day.Date >= DayService.MinDate)
            .GroupBy(static day => day.Date)
            .Select(static group => group.First())
            .OrderBy(static day => day.Date)
            .ToList();
        foreach (var day in days)
        {
            var usedMeals = new HashSet<long>();
            foreach (var meal in day.Meals)
            {
                meal.PlanMealId = mealMap.TryGetValue(meal.PlanMealId, out var mapped) && usedMeals.Add(mapped) ? mapped : --orphan;
            }

            var usedExercises = new HashSet<long>();
            foreach (var exercise in day.Exercises)
            {
                exercise.PlanExerciseId = exerciseMap.TryGetValue(exercise.PlanExerciseId, out var mapped) && usedExercises.Add(mapped) ? mapped : --orphan;
                exercise.Sets = exercise.Sets.Select(static reps => reps is >= 0 and <= DayService.MaxReps ? reps : null).ToList();
                exercise.LoadKg = exercise.LoadKg is >= 0 and <= DayService.MaxLoadKg ? exercise.LoadKg : null;
            }

            day.Meals.RemoveAll(static meal => NameText.Clean(meal.Name).Length == 0);
            day.Exercises.RemoveAll(static exercise => NameText.Clean(exercise.Name).Length == 0);
        }

        foreach (var chunk in days.Chunk(200))
        {
            await _dayRepository.ImportAsync(userId, DayJson.Serialize(chunk));
        }

        result.Days = days.Count;

        var weights = backup.Weights
            .Where(static entry => entry.WeightKg is >= WeightService.MinWeightKg and <= WeightService.MaxWeightKg && entry.Date >= DayService.MinDate)
            .GroupBy(static entry => entry.Date)
            .Select(static group => (group.Key, Math.Round(group.Last().WeightKg, 1)))
            .ToList();
        if (weights.Count > 0)
        {
            await _weightRepository.ImportAsync(userId, weights);
        }

        result.Weights = weights.Count;

        var goal = backup.Settings?.WeightGoalKg is >= WeightService.MinWeightKg and <= WeightService.MaxWeightKg ? backup.Settings.WeightGoalKg : null;
        var rest = backup.Settings?.RestSeconds is >= SettingService.MinRestSeconds and <= SettingService.MaxRestSeconds ? backup.Settings.RestSeconds : null;
        await _settingRepository.SaveAsync(userId, goal is { } value ? Math.Round(value, 1) : null, rest);
        return result;
    }
}
