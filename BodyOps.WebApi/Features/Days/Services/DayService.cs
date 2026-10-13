using System.Globalization;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Weights.Repositories;

namespace BodyOps.WebApi.Features.Days.Services;

/// <summary>
/// Reads and writes the logged days. The first write on a date copies that weekday's plan into the day,
/// so later plan edits never rewrite past weeks.
/// </summary>
/// <param name="dayRepository">Day data access.</param>
/// <param name="planRepository">Plan data access.</param>
/// <param name="weightRepository">Weigh-ins (for the day score).</param>
/// <param name="clock">Today in the configured time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class DayService(
    IDayRepository dayRepository,
    IPlanRepository planRepository,
    IWeightRepository weightRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : IDayService
{
    /// <summary>The day score a day needs to keep the streak going.</summary>
    public const int StreakThreshold = 60;

    /// <summary>How far back the streak looks.</summary>
    public const int StreakWindowDays = 400;

    /// <summary>Heaviest load accepted.</summary>
    public const decimal MaxLoadKg = 1000m;

    /// <summary>Most reps in one set.</summary>
    public const int MaxReps = 999;

    /// <summary>Largest cardio minutes or kilometres.</summary>
    public const decimal MaxCardio = 10_000m;

    /// <summary>Earliest date that can be logged.</summary>
    public static readonly DateOnly MinDate = new(2000, 1, 1);

    private readonly IDayRepository _dayRepository = dayRepository;
    private readonly IPlanRepository _planRepository = planRepository;
    private readonly IWeightRepository _weightRepository = weightRepository;
    private readonly IAppClock _clock = clock;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> GetWeekAsync(long userId, DateOnly? date)
    {
        var day = date ?? _clock.Today;
        if (await DateProblemAsync<WeekDTO>(day) is { } problem)
        {
            return problem;
        }

        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, day));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<ConsistencyDayDTO>>> GetConsistencyAsync(long userId, int weeks)
    {
        weeks = Math.Clamp(weeks, 1, 53);
        var today = _clock.Today;
        var from = WeekMath.Monday(today).AddDays(-7 * (weeks - 1));

        var plan = await _planRepository.GetAsync(userId);
        var logged = await _dayRepository.ListRangeAsync(userId, from, today);
        var days = new List<ConsistencyDayDTO>();
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            days.Add(new ConsistencyDayDTO { Date = date, Score = WeekBuilder.Score(WeekBuilder.Day(date, plan, logged, [])) });
        }

        return ServiceApiResponse<List<ConsistencyDayDTO>>.Success(days);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> SetMealEatenAsync(long userId, DateOnly date, long planMealId, MealEatenDTO request)
    {
        if (await DateProblemAsync<WeekDTO>(date) is { } problem)
        {
            return problem;
        }

        if (!await _dayRepository.SetMealEatenAsync(userId, date, planMealId, request.Eaten))
        {
            return ServiceApiResponse<WeekDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.UnknownPlanItem)));
        }

        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, date));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> SetAllMealsEatenAsync(long userId, DateOnly date, MealEatenDTO request)
    {
        if (await DateProblemAsync<WeekDTO>(date) is { } problem)
        {
            return problem;
        }

        await _dayRepository.SetAllMealsEatenAsync(userId, date, request.Eaten);
        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, date));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> SetTrainingStateAsync(long userId, DateOnly date, TrainingStateDTO request)
    {
        if (await DateProblemAsync<WeekDTO>(date) is { } problem)
        {
            return problem;
        }

        var state = Enum.IsDefined(request.State) ? request.State : TrainingState.Pending;
        await _dayRepository.SetTrainingStateAsync(userId, date, state);
        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, date));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> LogExerciseAsync(long userId, DateOnly date, long planExerciseId, ExerciseLogDTO request)
    {
        if (await DateProblemAsync<WeekDTO>(date) is { } problem)
        {
            return problem;
        }

        if (request.LoadKg is < 0 or > MaxLoadKg)
        {
            return await _globalizationHelper.ValidationProblemAsync<WeekDTO>("loadKg", nameof(TranslationKeys.InvalidLoad), MaxLoadKg);
        }

        var sets = request.Sets ?? [];
        if (sets.Count > ExerciseDetailParser.MaxSets)
        {
            return await _globalizationHelper.ValidationProblemAsync<WeekDTO>("sets", nameof(TranslationKeys.TooManySets), ExerciseDetailParser.MaxSets);
        }

        if (sets.Any(static reps => reps is < 0 or > MaxReps))
        {
            return await _globalizationHelper.ValidationProblemAsync<WeekDTO>("sets", nameof(TranslationKeys.InvalidReps), MaxReps);
        }

        if (request.CardioMinutes is < 0 or > MaxCardio || request.CardioKm is < 0 or > MaxCardio)
        {
            return await _globalizationHelper.ValidationProblemAsync<WeekDTO>("cardio", nameof(TranslationKeys.InvalidCardio), MaxCardio);
        }

        var found = await _dayRepository.LogExerciseAsync(userId, date, planExerciseId,
            request.LoadKg is { } load ? Math.Round(load, 2) : null,
            sets,
            request.CardioMinutes is { } minutes ? Math.Round(minutes, 1) : null,
            request.CardioKm is { } km ? Math.Round(km, 2) : null);

        if (!found)
        {
            return ServiceApiResponse<WeekDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.UnknownPlanItem)));
        }

        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, date));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<DayBackupDTO>>> ResetWeekAsync(long userId, DateOnly date)
    {
        if (await DateProblemAsync<List<DayBackupDTO>>(date) is { } problem)
        {
            return problem;
        }

        var monday = WeekMath.Monday(date);
        var removed = DayBackupMapper.ToBackup(await _dayRepository.ListRangeAsync(userId, monday, monday.AddDays(6)));
        await _dayRepository.DeleteRangeAsync(userId, monday, monday.AddDays(6));
        return ServiceApiResponse<List<DayBackupDTO>>.Success(removed);
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeekDTO>> RestoreWeekAsync(long userId, DateOnly date, List<DayBackupDTO>? days)
    {
        if (await DateProblemAsync<WeekDTO>(date) is { } problem)
        {
            return problem;
        }

        var monday = WeekMath.Monday(date);
        var inWeek = (days ?? []).Where(day => day.Date >= monday && day.Date <= monday.AddDays(6)).ToList();
        if (inWeek.Count > 0)
        {
            await _dayRepository.ImportAsync(userId, DayJson.Serialize(inWeek));
        }

        return ServiceApiResponse<WeekDTO>.Success(await BuildWeekAsync(userId, monday));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<StreakDTO>> GetStreakAsync(long userId)
    {
        var today = _clock.Today;
        var from = today.AddDays(-StreakWindowDays);
        var plan = await _planRepository.GetAsync(userId);
        var logged = await _dayRepository.ListRangeAsync(userId, from, today);
        var weights = await _weightRepository.ListAsync(userId);
        var days = new List<DayDTO>(StreakWindowDays + 1);
        for (var date = from; date <= today; date = date.AddDays(1))
        {
            days.Add(WeekBuilder.Day(date, plan, logged, [], weights));
        }

        return ServiceApiResponse<StreakDTO>.Success(Streak(days));
    }

    /// <summary>
    /// Works out the streak from consecutive days, oldest first, the last one being today.
    /// </summary>
    /// <param name="days">The days up to today.</param>
    /// <returns>The streak.</returns>
    internal static StreakDTO Streak(IReadOnlyList<DayDTO> days)
    {
        var streak = new StreakDTO { Threshold = StreakThreshold, TodayScore = days.Count > 0 ? days[^1].Score : 0 };

        // Every day before today: a good day extends the run, a poor one ends it.
        var run = 0;
        for (var i = 0; i < days.Count - 1; i++)
        {
            if (!WeekBuilder.Counts(days[i]))
            {
                continue;
            }

            run = days[i].Score >= StreakThreshold ? run + 1 : 0;
            streak.Best = Math.Max(streak.Best, run);
        }

        streak.ThroughYesterday = run;

        // Today only adds to the run; it can still be finished, so it never breaks it.
        var todayCounts = days.Count > 0 && WeekBuilder.Counts(days[^1]) && days[^1].Score >= StreakThreshold;
        streak.Current = run + (todayCounts ? 1 : 0);
        streak.Best = Math.Max(streak.Best, streak.Current);
        return streak;
    }

    private async Task<WeekDTO> BuildWeekAsync(long userId, DateOnly date)
    {
        var monday = WeekMath.Monday(date);
        var plan = await _planRepository.GetAsync(userId);
        var logged = await _dayRepository.ListRangeAsync(userId, monday, monday.AddDays(6));
        var history = await _dayRepository.ListExerciseLogAsync(userId);
        var weights = await _weightRepository.ListAsync(userId);
        return WeekBuilder.Week(monday, _clock.Today, plan, logged, history, weights);
    }

    private async Task<ServiceApiResponse<T>?> DateProblemAsync<T>(DateOnly date)
    {
        var max = _clock.Today.AddDays(366);
        return date < MinDate || date > max
            ? await _globalizationHelper.ValidationProblemAsync<T>("date", nameof(TranslationKeys.InvalidDate),
                MinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), max.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : null;
    }
}
