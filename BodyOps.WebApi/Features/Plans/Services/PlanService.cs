using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Repositories;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Reads and edits the weekly plan. Every edit also updates the logged days of the current and later weeks,
/// while earlier weeks keep the plan they had.
/// </summary>
/// <param name="planRepository">Plan data access.</param>
/// <param name="clock">Today in the configured time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class PlanService(
    IPlanRepository planRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : IPlanService
{
    /// <summary>Longest meal or food name.</summary>
    public const int MaxMealName = 80;

    /// <summary>Longest day name ("Upper body").</summary>
    public const int MaxDayName = 40;

    /// <summary>Longest exercise name.</summary>
    public const int MaxExerciseName = 100;

    /// <summary>Longest "sets × reps" text.</summary>
    public const int MaxDetail = 60;

    /// <summary>Longest notes.</summary>
    public const int MaxNotes = 200;

    /// <summary>Most meals per day.</summary>
    public const int MaxMeals = 12;

    /// <summary>Most foods per meal.</summary>
    public const int MaxFoods = 25;

    /// <summary>Most exercises per day.</summary>
    public const int MaxExercises = 30;

    /// <summary>Largest food amount.</summary>
    public const decimal MaxAmount = 100_000m;

    private readonly IPlanRepository _planRepository = planRepository;
    private readonly IAppClock _clock = clock;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDTO>> GetAsync(long userId)
    {
        var plan = await _planRepository.GetAsync(userId);
        return ServiceApiResponse<PlanDTO>.Success(ToDTO(plan));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDayDTO>> SaveMealsAsync(long userId, int weekday, List<PlanMealSaveDTO>? meals)
    {
        if (await WeekdayProblemAsync<PlanDayDTO>(weekday) is { } weekdayProblem)
        {
            return weekdayProblem;
        }

        meals ??= [];
        if (meals.Count > MaxMeals)
        {
            return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>("meals", nameof(TranslationKeys.TooManyItems), MaxMeals);
        }

        var items = new List<object>(meals.Count);
        for (var i = 0; i < meals.Count; i++)
        {
            var meal = meals[i];
            var name = NameText.Clean(meal.Name);
            if (name.Length is 0 or > MaxMealName)
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"meals[{i}].name", nameof(TranslationKeys.InvalidName), MaxMealName);
            }

            if (!MealTimeText.TryParse(meal.Time, out var time))
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"meals[{i}].time", nameof(TranslationKeys.InvalidTime));
            }

            var foods = meal.Foods ?? [];
            if (foods.Count > MaxFoods)
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"meals[{i}].foods", nameof(TranslationKeys.TooManyItems), MaxFoods);
            }

            var cleanFoods = new List<(string Name, decimal Amount, FoodUnit Unit)>(foods.Count);
            for (var j = 0; j < foods.Count; j++)
            {
                var food = foods[j];
                var foodName = NameText.Clean(food.Name);
                if (foodName.Length is 0 or > MaxMealName)
                {
                    return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"meals[{i}].foods[{j}].name", nameof(TranslationKeys.InvalidName), MaxMealName);
                }

                if (food.Amount is < 0 or > MaxAmount)
                {
                    return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"meals[{i}].foods[{j}].amount", nameof(TranslationKeys.InvalidAmount), MaxAmount);
                }

                cleanFoods.Add((foodName, Math.Round(food.Amount, 2), Enum.IsDefined(food.Unit) ? food.Unit : FoodUnit.Grams));
            }

            items.Add(PlanJson.Meal(meal.Id, name, time, cleanFoods));
        }

        await _planRepository.SaveMealsAsync(userId, weekday, PlanJson.Serialize(items), WeekMath.Monday(_clock.Today));
        return ServiceApiResponse<PlanDayDTO>.Success(ToDayDTO(weekday, await _planRepository.GetAsync(userId, weekday)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDayDTO>> SaveExercisesAsync(long userId, int weekday, List<PlanExerciseSaveDTO>? exercises)
    {
        if (await WeekdayProblemAsync<PlanDayDTO>(weekday) is { } weekdayProblem)
        {
            return weekdayProblem;
        }

        exercises ??= [];
        if (exercises.Count > MaxExercises)
        {
            return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>("exercises", nameof(TranslationKeys.TooManyItems), MaxExercises);
        }

        var items = new List<object>(exercises.Count);
        for (var i = 0; i < exercises.Count; i++)
        {
            var exercise = exercises[i];
            var name = NameText.Clean(exercise.Name);
            if (name.Length is 0 or > MaxExerciseName)
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"exercises[{i}].name", nameof(TranslationKeys.InvalidName), MaxExerciseName);
            }

            var detail = NameText.Clean(exercise.Detail);
            if (detail.Length > MaxDetail)
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"exercises[{i}].detail", nameof(TranslationKeys.TextTooLong), MaxDetail);
            }

            var notes = (exercise.Notes ?? string.Empty).Trim();
            if (notes.Length > MaxNotes)
            {
                return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>($"exercises[{i}].notes", nameof(TranslationKeys.TextTooLong), MaxNotes);
            }

            var type = Enum.IsDefined(exercise.Type) ? exercise.Type : ExerciseType.Strength;
            items.Add(PlanJson.Exercise(exercise.Id, type, name, detail, notes, MuscleDetector.Format(exercise.Muscles), exercise.CardioKind));
        }

        await _planRepository.SaveExercisesAsync(userId, weekday, PlanJson.Serialize(items), WeekMath.Monday(_clock.Today));
        return ServiceApiResponse<PlanDayDTO>.Success(ToDayDTO(weekday, await _planRepository.GetAsync(userId, weekday)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDTO>> CopyAsync(long userId, int weekday, PlanCopyDTO request)
    {
        if (await WeekdayProblemAsync<PlanDTO>(weekday) is { } weekdayProblem)
        {
            return weekdayProblem;
        }

        var targets = (request.Targets ?? []).Distinct().ToList();
        if (targets.Any(static target => target is < 1 or > 7))
        {
            return await _globalizationHelper.ValidationProblemAsync<PlanDTO>("targets", nameof(TranslationKeys.InvalidWeekday));
        }

        targets.Remove(weekday);
        if (targets.Count == 0)
        {
            return await _globalizationHelper.ValidationProblemAsync<PlanDTO>("targets", nameof(TranslationKeys.CopyToSameDay));
        }

        var part = Enum.IsDefined(request.Part) ? request.Part : PlanPart.Exercises;
        await _planRepository.CopyDayAsync(userId, weekday, targets, part, WeekMath.Monday(_clock.Today));
        return ServiceApiResponse<PlanDTO>.Success(ToDTO(await _planRepository.GetAsync(userId)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<PlanDayDTO>> SaveNameAsync(long userId, int weekday, PlanDayNameDTO request)
    {
        if (await WeekdayProblemAsync<PlanDayDTO>(weekday) is { } weekdayProblem)
        {
            return weekdayProblem;
        }

        var name = NameText.Clean(request.Name);
        if (name.Length > MaxDayName)
        {
            return await _globalizationHelper.ValidationProblemAsync<PlanDayDTO>("name", nameof(TranslationKeys.InvalidName), MaxDayName);
        }

        await _planRepository.SaveDayNameAsync(userId, weekday, name);
        return ServiceApiResponse<PlanDayDTO>.Success(ToDayDTO(weekday, await _planRepository.GetAsync(userId, weekday)));
    }

    /// <summary>
    /// Builds the seven-day plan from the stored rows.
    /// </summary>
    /// <param name="plan">The rows.</param>
    /// <returns>The plan, Monday first.</returns>
    internal static PlanDTO ToDTO(PlanData plan) => new()
    {
        Days = Enumerable.Range(1, 7).Select(weekday => ToDayDTO(weekday, plan)).ToList(),
    };

    /// <summary>
    /// Builds one weekday's plan from the stored rows.
    /// </summary>
    /// <param name="weekday">The weekday.</param>
    /// <param name="plan">The rows (may hold other weekdays too).</param>
    /// <returns>The weekday's plan.</returns>
    internal static PlanDayDTO ToDayDTO(int weekday, PlanData plan)
    {
        var foods = plan.Foods.ToLookup(static food => food.PlanMealID);
        var day = new PlanDayDTO
        {
            Weekday = weekday,
            Name = plan.Names.FirstOrDefault(name => name.Weekday == weekday)?.Name ?? string.Empty,
            Meals = plan.Meals
                .Where(meal => meal.Weekday == weekday)
                .OrderBy(static meal => meal.SortOrder)
                .Select(meal => new PlanMealDTO(meal, foods[meal.PlanMealID].OrderBy(static food => food.SortOrder)))
                .ToList(),
            Exercises = plan.Exercises
                .Where(exercise => exercise.Weekday == weekday)
                .OrderBy(static exercise => exercise.SortOrder)
                .Select(static exercise => new PlanExerciseDTO(exercise))
                .ToList(),
        };

        // Planned sets per muscle: each exercise counts its planned sets (one when the plan doesn't say) times how much the muscle works.
        var muscles = new Dictionary<Muscle, double>();
        foreach (var exercise in plan.Exercises.Where(exercise => exercise.Weekday == weekday))
        {
            var sets = Math.Max(1, ExerciseDetailParser.Targets(exercise.Detail).Count);
            foreach (var (muscle, weight) in MuscleDetector.Detect(exercise.Name, exercise.ExerciseType, exercise.Muscles))
            {
                muscles[muscle] = muscles.GetValueOrDefault(muscle) + sets * weight;
            }
        }

        day.Muscles = muscles
            .OrderByDescending(static pair => pair.Value)
            .Select(static pair => new MuscleSetsDTO { Muscle = pair.Key, Sets = Math.Round(pair.Value, 1) })
            .ToList();
        return day;
    }

    private async Task<ServiceApiResponse<T>?> WeekdayProblemAsync<T>(int weekday) =>
        weekday is < 1 or > 7 ? await _globalizationHelper.ValidationProblemAsync<T>("weekday", nameof(TranslationKeys.InvalidWeekday)) : null;
}
