using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Exercises.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Exercises.Services;

/// <summary>
/// Progression from the logged strength sessions.
/// </summary>
/// <param name="dayRepository">Day data access.</param>
/// <param name="planRepository">Plan data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class ExerciseService(
    IDayRepository dayRepository,
    IPlanRepository planRepository,
    IGlobalizationHelper globalizationHelper) : IExerciseService
{
    private readonly IDayRepository _dayRepository = dayRepository;
    private readonly IPlanRepository _planRepository = planRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <summary>How many sessions a trend line shows.</summary>
    public const int TrendLength = 12;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<ExerciseSummaryDTO>>> ListAsync(long userId)
    {
        var plan = await _planRepository.GetAsync(userId);
        var log = await _dayRepository.ListExerciseLogAsync(userId);

        var byKey = new Dictionary<string, ExerciseSummaryDTO>();
        foreach (var exercise in plan.Exercises.Where(static exercise => exercise.ExerciseType == ExerciseType.Strength))
        {
            byKey.TryAdd(NameText.Key(exercise.Name), new ExerciseSummaryDTO { Name = exercise.Name });
        }

        foreach (var group in log.GroupBy(static row => row.NameKey))
        {
            var latest = group.MaxBy(static row => row.DayDate)!;
            if (!byKey.TryGetValue(group.Key, out var summary))
            {
                summary = new ExerciseSummaryDTO { Name = latest.Name };
                byKey[group.Key] = summary;
            }

            summary.Sessions = group.Count();
            summary.BestKg = group.Max(static row => row.LoadKg);
            summary.LastDate = DateOnly.FromDateTime(latest.DayDate);
            if (summary.BestKg is { } best)
            {
                summary.BestDate = DateOnly.FromDateTime(group.Where(row => row.LoadKg == best).Min(static row => row.DayDate));
            }

            summary.Trend = group
                .Where(static row => row.LoadKg is not null)
                .OrderBy(static row => row.DayDate)
                .TakeLast(TrendLength)
                .Select(static row => new ExercisePointDTO { Date = DateOnly.FromDateTime(row.DayDate), LoadKg = row.LoadKg!.Value })
                .ToList();
        }

        return ServiceApiResponse<List<ExerciseSummaryDTO>>.Success(byKey.Values.OrderBy(static summary => summary.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ExerciseHistoryDTO>> HistoryAsync(long userId, string? name)
    {
        var key = NameText.Key(name);
        if (key.Length is 0 or > PlanService.MaxExerciseName)
        {
            return await _globalizationHelper.ValidationProblemAsync<ExerciseHistoryDTO>("name", nameof(TranslationKeys.InvalidName), PlanService.MaxExerciseName);
        }

        var rows = (await _dayRepository.ListExerciseLogAsync(userId)).Where(row => row.NameKey == key).OrderBy(static row => row.DayDate).ToList();
        var sessions = rows.Select(static row =>
        {
            var reps = WeekBuilder.ParseReps(row.Reps);
            var stored = WeekBuilder.ParseLoads(row.Loads);
            var loads = reps.Select((_, i) => (i < stored.Count ? stored[i] : null) ?? row.LoadKg).ToList();
            var volume = reps.Select((count, i) => count * (loads[i] ?? 0)).Sum();
            return new ExerciseSessionDTO
            {
                Date = DateOnly.FromDateTime(row.DayDate),
                LoadKg = row.LoadKg,
                Reps = reps,
                Loads = loads,
                VolumeKg = volume > 0 ? volume : null,
            };
        }).ToList();

        return ServiceApiResponse<ExerciseHistoryDTO>.Success(new ExerciseHistoryDTO
        {
            Name = rows.Count > 0 ? rows[^1].Name : NameText.Clean(name),
            BestKg = rows.Max(static row => row.LoadKg),
            Sessions = sessions,
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<RecordDTO>>> RecordsAsync(long userId, int limit)
    {
        limit = Math.Clamp(limit, 1, 200);
        var records = Records(await _dayRepository.ListExerciseLogAsync(userId));
        return ServiceApiResponse<List<RecordDTO>>.Success(records.OrderByDescending(static record => record.Date).ThenBy(static record => record.Name).Take(limit).ToList());
    }

    /// <summary>
    /// Finds the sessions whose load beat every earlier load of the same exercise (the first session sets the bar, it is not a record).
    /// </summary>
    /// <param name="log">The exercise log, any order.</param>
    /// <returns>The records, oldest first.</returns>
    internal static List<RecordDTO> Records(IEnumerable<ExerciseLogRow> log)
    {
        var records = new List<RecordDTO>();
        foreach (var group in log.Where(static row => row.LoadKg is not null).GroupBy(static row => row.NameKey))
        {
            decimal? best = null;
            foreach (var row in group.OrderBy(static row => row.DayDate))
            {
                var load = row.LoadKg!.Value;
                if (best is { } previous && load > previous)
                {
                    records.Add(new RecordDTO { Date = DateOnly.FromDateTime(row.DayDate), Name = row.Name, LoadKg = load, PreviousKg = previous });
                }

                best = best is null ? load : Math.Max(best.Value, load);
            }
        }

        return records.OrderBy(static record => record.Date).ToList();
    }
}
