using System.Globalization;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Services;
using BodyOps.WebApi.Features.Weights.Models.Entities;

namespace BodyOps.WebApi.Features.Days.Services;

/// <summary>
/// Turns the stored rows into what the Today and Week screens show. Pure functions, so the rules are unit-tested:
/// a logged day shows its own copy of the plan; any other day shows the current plan with nothing done.
/// </summary>
internal static class WeekBuilder
{
    /// <summary>How much training weighs in the day score (meals weigh the rest).</summary>
    public const double TrainingWeight = 60;

    /// <summary>How much the meals weigh in the day score.</summary>
    public const double MealsWeight = 40;

    /// <summary>
    /// Builds a Monday-to-Sunday week.
    /// </summary>
    /// <param name="monday">The Monday.</param>
    /// <param name="today">Today.</param>
    /// <param name="plan">The current plan.</param>
    /// <param name="logged">The logged days of that week.</param>
    /// <param name="history">Every logged strength exercise (for last session and records).</param>
    /// <param name="weights">The weigh-ins (may include other dates), shown on each day.</param>
    /// <returns>The week.</returns>
    public static WeekDTO Week(DateOnly monday, DateOnly today, PlanData plan, DayRange logged, IReadOnlyList<ExerciseLogRow> history, IReadOnlyList<WeightEntry>? weights = null)
    {
        var days = Enumerable.Range(0, 7).Select(offset => Day(monday.AddDays(offset), plan, logged, history, weights)).ToList();
        return new WeekDTO
        {
            From = monday,
            To = monday.AddDays(6),
            Today = today,
            Days = days,
            Totals = Totals(days),
        };
    }

    /// <summary>
    /// Builds one day.
    /// </summary>
    /// <param name="date">The date.</param>
    /// <param name="plan">The current plan.</param>
    /// <param name="logged">Logged days (may include other dates).</param>
    /// <param name="history">Every logged strength exercise.</param>
    /// <param name="weights">The weigh-ins (may include other dates).</param>
    /// <returns>The day.</returns>
    public static DayDTO Day(DateOnly date, PlanData plan, DayRange logged, IReadOnlyList<ExerciseLogRow> history, IReadOnlyList<WeightEntry>? weights = null)
    {
        var dto = Items(date, plan, logged, history);
        dto.Name = plan.Names.FirstOrDefault(name => name.Weekday == dto.Weekday)?.Name ?? string.Empty;
        dto.WeightKg = weights?.FirstOrDefault(entry => DateOnly.FromDateTime(entry.EntryDate) == date)?.WeightKg;
        (dto.SetsDone, dto.SetsPlanned) = SetProgress(dto);
        dto.Score = DayScore(dto);
        return dto;
    }

    /// <summary>
    /// How much of the planned training was done: each strength exercise counts its planned sets (one when the plan
    /// names none) and each cardio exercise counts one. A day marked trained with nothing logged (ticked off without
    /// logging sets) counts as all done; logging marks the day trained too, so then the logged sets count.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>Sets done and sets planned.</returns>
    public static (int Done, int Planned) SetProgress(DayDTO day)
    {
        int done = 0, planned = 0;
        foreach (var exercise in day.Exercises)
        {
            if (exercise.Type == ExerciseType.Cardio)
            {
                planned++;
                done += exercise.CardioMinutes > 0 || exercise.CardioKm > 0 ? 1 : 0;
                continue;
            }

            var target = Math.Max(1, exercise.Targets.Count);
            var logged = exercise.Sets.Count(static set => set is not null);
            planned += target;
            done += Math.Min(target, logged);
        }

        if (day.TrainingState == TrainingState.Done && done == 0)
        {
            done = planned;
        }

        return (done, planned);
    }

    /// <summary>
    /// The day score, 0–100. Training weighs 60 and meals 40; a part the day doesn't have (no exercises on a rest
    /// day, no meals planned) is left out and the other scaled up. A day marked as skipped scores its training as
    /// zero. The weigh-in is on the timeline but not in the score: people weigh in every week or two, not daily.
    /// </summary>
    /// <param name="day">The day, with <see cref="DayDTO.SetsDone"/> and <see cref="DayDTO.SetsPlanned"/> filled in.</param>
    /// <returns>The score.</returns>
    public static int DayScore(DayDTO day)
    {
        double total = 0, weight = 0;
        if (day.SetsPlanned > 0)
        {
            weight += TrainingWeight;
            total += day.TrainingState == TrainingState.Missed ? 0 : TrainingWeight * day.SetsDone / day.SetsPlanned;
        }

        if (day.Meals.Count > 0)
        {
            weight += MealsWeight;
            total += MealsWeight * day.Meals.Count(static meal => meal.Eaten) / day.Meals.Count;
        }

        return weight == 0 ? 0 : (int)Math.Round(100 * total / weight, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether a day takes part in the streak: it has training or meals planned (a weigh-in alone doesn't count).
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>True when it counts or breaks the streak.</returns>
    public static bool Counts(DayDTO day) => day.SetsPlanned > 0 || day.Meals.Count > 0;

    private static DayDTO Items(DateOnly date, PlanData plan, DayRange logged, IReadOnlyList<ExerciseLogRow> history)
    {
        var weekday = WeekMath.Weekday(date);
        var row = logged.Days.FirstOrDefault(day => DateOnly.FromDateTime(day.DayDate) == date);
        var dto = new DayDTO { Date = date, Weekday = weekday, Logged = row is not null, TrainingState = row?.TrainingState ?? TrainingState.Pending };

        if (row is not null)
        {
            dto.Meals = logged.Meals
                .Where(meal => meal.DayID == row.DayID)
                .OrderBy(static meal => meal.SortOrder)
                .Select(static meal => new DayMealDTO { PlanMealId = meal.PlanMealID, Name = meal.Name, Foods = meal.Foods, Eaten = meal.Eaten, Time = MealTimeText.Format(meal.MealTime) })
                .ToList();

            var sets = logged.Sets.ToLookup(static set => set.DayExerciseID);
            dto.Exercises = logged.Exercises
                .Where(exercise => exercise.DayID == row.DayID)
                .OrderBy(static exercise => exercise.SortOrder)
                .Select(exercise => Exercise(date, exercise.PlanExerciseID, exercise.ExerciseType, exercise.Name, exercise.Detail, exercise.Notes, exercise.Muscles,
                    exercise.LoadKg, sets[exercise.DayExerciseID].ToList(), exercise.CardioMinutes, exercise.CardioKm, exercise.CardioKind, history))
                .ToList();
            return dto;
        }

        var foods = plan.Foods.ToLookup(static food => food.PlanMealID);
        dto.Meals = plan.Meals
            .Where(meal => meal.Weekday == weekday)
            .OrderBy(static meal => meal.SortOrder)
            .Select(meal => new DayMealDTO { PlanMealId = meal.PlanMealID, Name = meal.Name, Foods = FoodsLine(foods[meal.PlanMealID]), Time = MealTimeText.Format(meal.MealTime) })
            .ToList();
        dto.Exercises = plan.Exercises
            .Where(exercise => exercise.Weekday == weekday)
            .OrderBy(static exercise => exercise.SortOrder)
            .Select(exercise => Exercise(date, exercise.PlanExerciseID, exercise.ExerciseType, exercise.Name, exercise.Detail, exercise.Notes, exercise.Muscles,
                null, [], null, null, exercise.CardioKind, history))
            .ToList();
        return dto;
    }

    /// <summary>
    /// The foods of a meal on one line, written the same way as the database view dbo.PlanMealSummary.
    /// </summary>
    /// <param name="foods">The foods.</param>
    /// <returns>"Whey 25 g · Banana 1 pc".</returns>
    public static string FoodsLine(IEnumerable<PlanFood> foods) =>
        string.Join(" · ", foods.OrderBy(static food => food.SortOrder).Select(static food =>
            $"{food.Name} {food.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {(food.Unit == FoodUnit.Pieces ? "pc" : "g")}"));

    /// <summary>
    /// Reads a stored reps list ("12,11,10").
    /// </summary>
    /// <param name="reps">The stored list, or null.</param>
    /// <returns>The reps.</returns>
    public static List<int> ParseReps(string? reps) =>
        string.IsNullOrWhiteSpace(reps)
            ? []
            : reps.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
                .Select(static value => int.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var number) ? number : 0)
                .ToList();

    /// <summary>
    /// Reads a stored loads list ("60,65,70"; an empty item = no load of its own).
    /// </summary>
    /// <param name="loads">The stored list, or null.</param>
    /// <returns>The loads.</returns>
    public static List<decimal?> ParseLoads(string? loads) =>
        string.IsNullOrEmpty(loads)
            ? []
            : loads.Split(',')
                .Select(static value => decimal.TryParse(value.Trim(), NumberStyles.Number, CultureInfo.InvariantCulture, out var number) ? number : (decimal?)null)
                .ToList();

    /// <summary>
    /// The 0–1 completeness of a day: the share of meals eaten and, when marked, whether the session happened.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>The score, or null when nothing was logged or there is nothing to score.</returns>
    public static double? Score(DayDTO day)
    {
        if (!day.Logged)
        {
            return null;
        }

        var parts = new List<double>(2);
        if (day.Meals.Count > 0)
        {
            parts.Add((double)day.Meals.Count(static meal => meal.Eaten) / day.Meals.Count);
        }

        if (day.TrainingState == TrainingState.Done)
        {
            parts.Add(1);
        }
        else if (day.TrainingState == TrainingState.Missed)
        {
            parts.Add(0);
        }

        return parts.Count == 0 ? null : parts.Average();
    }

    private static DayExerciseDTO Exercise(
        DateOnly date,
        long planExerciseId,
        ExerciseType type,
        string name,
        string detail,
        string notes,
        string? muscles,
        decimal? loadKg,
        List<DaySetRow> setRows,
        decimal? cardioMinutes,
        decimal? cardioKm,
        CardioKind? cardioKind,
        IReadOnlyList<ExerciseLogRow> history)
    {
        var targets = type == ExerciseType.Strength ? ExerciseDetailParser.Targets(detail) : [];
        var length = Math.Max(targets.Count, setRows.Count == 0 ? 0 : setRows.Max(static set => set.SetNumber));
        var rows = Enumerable.Range(1, length).Select(number => setRows.FirstOrDefault(set => set.SetNumber == number)).ToList();
        var sets = rows.Select(static set => set is null ? null : (int?)set.Reps).ToList();
        // Sets logged before per-set loads carry the exercise's load.
        var setLoads = rows.Select(set => set is null ? null : set.LoadKg ?? loadKg).ToList();

        var dto = new DayExerciseDTO
        {
            PlanExerciseId = planExerciseId,
            Type = type,
            Name = name,
            Detail = detail,
            Notes = notes,
            Muscles = MuscleDetector.Detect(name, type, muscles).Keys.ToList(),
            MusclesAuto = muscles is null,
            Targets = targets,
            LoadKg = loadKg,
            Sets = sets,
            SetLoads = setLoads,
            CardioMinutes = cardioMinutes,
            CardioKm = cardioKm,
        };

        if (type == ExerciseType.Cardio)
        {
            dto.CardioKind = CardioKinds.Resolve(name, cardioKind);
            dto.CardioDistance = CardioKinds.UsesDistance(dto.CardioKind.Value);
        }

        var volume = rows.Sum(set => set is null ? 0 : set.Reps * (set.LoadKg ?? loadKg ?? 0));
        if (volume > 0)
        {
            dto.VolumeKg = volume;
        }

        if (type == ExerciseType.Strength)
        {
            var key = NameText.Key(name);
            var earlier = history.Where(row => row.NameKey == key && DateOnly.FromDateTime(row.DayDate) < date).ToList();
            if (earlier.Count > 0)
            {
                var last = earlier.MaxBy(static row => row.DayDate)!;
                var lastReps = ParseReps(last.Reps);
                var lastLoads = ParseLoads(last.Loads);
                dto.Last = new LastSessionDTO
                {
                    Date = DateOnly.FromDateTime(last.DayDate),
                    LoadKg = last.LoadKg,
                    Reps = lastReps,
                    Loads = lastReps.Select((_, i) => (i < lastLoads.Count ? lastLoads[i] : null) ?? last.LoadKg).ToList(),
                };
                dto.BestBeforeKg = earlier.Max(static row => row.LoadKg);
                dto.IsPr = loadKg is not null && dto.BestBeforeKg is not null && loadKg > dto.BestBeforeKg;
            }
        }

        return dto;
    }

    private static WeekTotalsDTO Totals(List<DayDTO> days)
    {
        var totals = new WeekTotalsDTO();
        var muscles = new Dictionary<Muscle, double>();

        foreach (var day in days)
        {
            totals.MealsPlanned += day.Meals.Count;
            totals.MealsEaten += day.Meals.Count(static meal => meal.Eaten);
            totals.TrainedDays += day.TrainingState == TrainingState.Done ? 1 : 0;
            totals.MissedDays += day.TrainingState == TrainingState.Missed ? 1 : 0;
            totals.TrainingDaysPlanned += day.Exercises.Count > 0 ? 1 : 0;

            foreach (var exercise in day.Exercises)
            {
                var done = exercise.Sets.Count(static set => set is not null);
                totals.SetsLogged += done;
                totals.VolumeKg += exercise.VolumeKg ?? 0;

                if (day.TrainingState != TrainingState.Done || exercise.Muscles.Count == 0)
                {
                    continue;
                }

                var setCount = done > 0 ? done : exercise.Targets.Count;
                if (setCount == 0)
                {
                    setCount = 1;
                    totals.UnknownSetCount++;
                }

                // Detected muscles carry how much each one works; muscles picked by hand count fully.
                var weights = exercise.MusclesAuto ? MuscleDetector.Detect(exercise.Name, exercise.Type, null) : [];
                foreach (var muscle in exercise.Muscles)
                {
                    var weight = weights.TryGetValue(muscle, out var detected) ? detected : 1;
                    muscles[muscle] = muscles.GetValueOrDefault(muscle) + setCount * weight;
                }
            }
        }

        totals.Muscles = muscles
            .OrderByDescending(static pair => pair.Value)
            .Select(static pair => new MuscleSetsDTO { Muscle = pair.Key, Sets = Math.Round(pair.Value, 1) })
            .ToList();
        return totals;
    }
}
