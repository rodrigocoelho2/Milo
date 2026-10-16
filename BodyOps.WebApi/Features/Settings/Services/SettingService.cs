using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Models.Entities;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.WebApi.Features.Settings.Services;

/// <summary>
/// Reads and saves the account settings.
/// </summary>
/// <param name="settingRepository">Settings data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class SettingService(ISettingRepository settingRepository, IGlobalizationHelper globalizationHelper) : ISettingService
{
    /// <summary>The rest between sets when none is set.</summary>
    public const int DefaultRestSeconds = 90;

    /// <summary>Shortest rest accepted.</summary>
    public const int MinRestSeconds = 15;

    /// <summary>Longest rest accepted.</summary>
    public const int MaxRestSeconds = 900;

    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <summary>The workout reminder time when none is set.</summary>
    public static readonly TimeSpan DefaultWorkoutTime = new(18, 0, 0);

    /// <summary>The weigh-in reminder time when none is set.</summary>
    public static readonly TimeSpan DefaultWeighInTime = new(8, 0, 0);

    /// <summary>The streak reminder time when none is set.</summary>
    public static readonly TimeSpan DefaultStreakTime = new(21, 0, 0);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> GetAsync(long userId) =>
        ServiceApiResponse<SettingsDTO>.Success(ToDTO(await _settingRepository.GetAsync(userId)));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> SaveAsync(long userId, SettingsDTO request)
    {
        if (request.WeightGoalKg is < WeightService.MinWeightKg or > WeightService.MaxWeightKg)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("weightGoalKg", nameof(TranslationKeys.InvalidWeight), WeightService.MinWeightKg, WeightService.MaxWeightKg);
        }

        if (request.RestSeconds is < MinRestSeconds or > MaxRestSeconds)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("restSeconds", nameof(TranslationKeys.InvalidRest), MinRestSeconds, MaxRestSeconds);
        }

        if (request.WeighEveryDays is < 1 or > WeighInSchedule.MaxDays)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("weighEveryDays", nameof(TranslationKeys.InvalidWeighEvery), 1, WeighInSchedule.MaxDays);
        }

        var reminders = request.Reminders;
        TimeSpan? workout = null, weighIn = null, streak = null;
        foreach (var (field, text) in new[] { ("workoutTime", reminders?.WorkoutTime), ("weighInTime", reminders?.WeighInTime), ("streakTime", reminders?.StreakTime) })
        {
            if (text is null)
            {
                continue;
            }

            if (!MealTimeText.TryParse(text, out var time) || time is null)
            {
                return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>($"reminders.{field}", nameof(TranslationKeys.InvalidTime));
            }

            (workout, weighIn, streak) = field switch
            {
                "workoutTime" => (time, weighIn, streak),
                "weighInTime" => (workout, time, streak),
                _ => (workout, weighIn, time),
            };
        }

        var current = await _settingRepository.GetAsync(userId) ?? new UserSetting { UserID = userId };
        var setting = new UserSetting
        {
            UserID = userId,
            WeightGoalKg = request.WeightGoalKg is { } goal ? Math.Round(goal, 1) : null,
            RestSeconds = request.RestSeconds ?? current.RestSeconds,
            WeighEveryDays = request.WeighEveryDays ?? current.WeighEveryDays,
            RemindMeals = reminders?.Meals ?? current.RemindMeals,
            RemindWorkout = reminders?.Workout ?? current.RemindWorkout,
            WorkoutTime = workout ?? current.WorkoutTime,
            RemindWeighIn = reminders?.WeighIn ?? current.RemindWeighIn,
            WeighInTime = weighIn ?? current.WeighInTime,
            RemindStreak = reminders?.Streak ?? current.RemindStreak,
            StreakTime = streak ?? current.StreakTime,
        };

        return ServiceApiResponse<SettingsDTO>.Success(ToDTO(await _settingRepository.SaveAsync(userId, setting)));
    }

    /// <summary>
    /// The settings with the defaults filled in.
    /// </summary>
    /// <param name="setting">The stored settings, or null.</param>
    /// <returns>The DTO.</returns>
    public static SettingsDTO ToDTO(UserSetting? setting) => new()
    {
        WeightGoalKg = setting?.WeightGoalKg,
        RestSeconds = setting?.RestSeconds ?? DefaultRestSeconds,
        WeighEveryDays = setting?.WeighEveryDays ?? 1,
        Reminders = new ReminderSettingsDTO
        {
            Meals = setting?.RemindMeals ?? true,
            Workout = setting?.RemindWorkout ?? true,
            WorkoutTime = MealTimeText.Format(setting?.WorkoutTime ?? DefaultWorkoutTime),
            WeighIn = setting?.RemindWeighIn ?? true,
            WeighInTime = MealTimeText.Format(setting?.WeighInTime ?? DefaultWeighInTime),
            Streak = setting?.RemindStreak ?? true,
            StreakTime = MealTimeText.Format(setting?.StreakTime ?? DefaultStreakTime),
        },
    };
}
