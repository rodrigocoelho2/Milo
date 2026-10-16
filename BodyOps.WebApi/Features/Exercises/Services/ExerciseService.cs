using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Days.Models.DTOs;
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
                .Select(static row => new ExercisePointDTO { Date = DateOnly.FromDateTime(row.DayDate), LoadKg = row.LoadKg!.Value, E1rmKg = SessionE1rm(row) })
                .ToList();

            // The best estimated 1RM and the set behind it (the earliest day it was reached).
            foreach (var row in group.OrderBy(static row => row.DayDate))
            {
                var reps = WeekBuilder.ParseReps(row.Reps);
                var loads = WeekBuilder.SessionLoads(row);
                for (var i = 0; i < reps.Count; i++)
                {
                    if (Lifting.E1rm(loads[i], reps[i]) is { } estimate && (summary.BestE1rmKg is null || estimate > summary.BestE1rmKg))
                    {
                        summary.BestE1rmKg = estimate;
                        summary.BestE1rmLoadKg = loads[i];
                        summary.BestE1rmReps = reps[i];
                        summary.BestE1rmDate = DateOnly.FromDateTime(row.DayDate);
                    }
                }
            }
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
            var loads = WeekBuilder.SessionLoads(row);
            var volume = reps.Select((count, i) => count * (loads[i] ?? 0)).Sum();
            return new ExerciseSessionDTO
            {
                Date = DateOnly.FromDateTime(row.DayDate),
                LoadKg = row.LoadKg,
                Reps = reps,
                Loads = loads,
                VolumeKg = volume > 0 ? volume : null,
                E1rmKg = SessionE1rm(row),
            };
        }).ToList();

        return ServiceApiResponse<ExerciseHistoryDTO>.Success(new ExerciseHistoryDTO
        {
            Name = rows.Count > 0 ? rows[^1].Name : NameText.Clean(name),
            BestKg = rows.Max(static row => row.LoadKg),
            BestE1rmKg = sessions.Max(static session => session.E1rmKg),
            Sessions = sessions,
            RepRecords = RepRecords(rows),
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
    /// Finds the sessions that beat an earlier best of the same exercise: the heaviest load, the heaviest load for a rep
    /// count, or the estimated 1RM. One record per session, the strongest kind first. The first session sets the bars,
    /// and a rep count done for the first time is not a record.
    /// </summary>
    /// <param name="log">The exercise log, any order.</param>
    /// <returns>The records, oldest first.</returns>
    internal static List<RecordDTO> Records(IEnumerable<ExerciseLogRow> log)
    {
        var records = new List<RecordDTO>();
        foreach (var group in log.GroupBy(static row => row.NameKey))
        {
            decimal? bestLoad = null, bestE1rm = null;
            var repBests = new Dictionary<int, decimal>();
            foreach (var row in group.OrderBy(static row => row.DayDate))
            {
                var date = DateOnly.FromDateTime(row.DayDate);
                var sets = WeekBuilder.ParseReps(row.Reps).Zip(WeekBuilder.SessionLoads(row), static (reps, load) => (reps, load)).ToList();
                var sessionBests = Lifting.RepBests(sets);
                var e1rm = SessionE1rm(row);

                if (row.LoadKg is { } load && bestLoad is { } previousLoad && load > previousLoad)
                {
                    records.Add(new RecordDTO { Kind = RecordKind.Load, Date = date, Name = row.Name, LoadKg = load, PreviousKg = previousLoad });
                }
                else if (Lifting.RepRecord(repBests, sessionBests) is { } repRecord)
                {
                    records.Add(new RecordDTO { Kind = RecordKind.Reps, Reps = repRecord.Reps, Date = date, Name = row.Name, LoadKg = repRecord.LoadKg, PreviousKg = repRecord.PreviousKg });
                }
                else if (e1rm is { } estimate && bestE1rm is { } previousE1rm && estimate > previousE1rm)
                {
                    records.Add(new RecordDTO { Kind = RecordKind.E1rm, Date = date, Name = row.Name, LoadKg = estimate, PreviousKg = previousE1rm });
                }

                if (row.LoadKg is { } rowLoad)
                {
                    bestLoad = bestLoad is null ? rowLoad : Math.Max(bestLoad.Value, rowLoad);
                }

                if (e1rm is { } rowE1rm)
                {
                    bestE1rm = bestE1rm is null ? rowE1rm : Math.Max(bestE1rm.Value, rowE1rm);
                }

                foreach (var (reps, best) in sessionBests)
                {
                    if (!repBests.TryGetValue(reps, out var current) || best > current)
                    {
                        repBests[reps] = best;
                    }
                }
            }
        }

        return records.OrderBy(static record => record.Date).ToList();
    }

    /// <summary>
    /// The heaviest load for each rep count, with the first day it was done.
    /// </summary>
    /// <param name="rows">One exercise's sessions, oldest first.</param>
    /// <returns>One entry per rep count reached, lowest count first.</returns>
    internal static List<RepBestDTO> RepRecords(IEnumerable<ExerciseLogRow> rows)
    {
        var bests = new Dictionary<int, RepBestDTO>();
        foreach (var row in rows.OrderBy(static row => row.DayDate))
        {
            var sets = WeekBuilder.ParseReps(row.Reps).Zip(WeekBuilder.SessionLoads(row), static (reps, load) => (reps, load));
            foreach (var (reps, load) in Lifting.RepBests(sets))
            {
                if (!bests.TryGetValue(reps, out var best) || load > best.LoadKg)
                {
                    bests[reps] = new RepBestDTO { Reps = reps, LoadKg = load, Date = DateOnly.FromDateTime(row.DayDate) };
                }
            }
        }

        return bests.Values.OrderBy(static best => best.Reps).ToList();
    }

    private static decimal? SessionE1rm(ExerciseLogRow row) =>
        Lifting.BestE1rm(WeekBuilder.ParseReps(row.Reps).Select(static reps => (int?)reps).ToList(), WeekBuilder.SessionLoads(row));
}
