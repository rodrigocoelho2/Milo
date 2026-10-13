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
    /// <summary>
    /// Builds a Monday-to-Sunday week.
    /// </summary>
    /// <param name="monday">The Monday.</param>
    /// <param name="today">Today.</param>
    /// <param name="plan">The current plan.</param>
    /// <param name="logged">The logged days of that week.</param>
    /// <param name="history">Every logged strength exercise (for last session and records).</param>
    /// <param name="weights">The weigh-ins (may include other dates), for the day score.</param>
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
    /// names none) and each cardio exercise counts one. A day marked trained counts as all done.
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

        if (day.TrainingState == TrainingState.Done)
        {
            done = planned;
        }

        return (done, planned);
    }

    /// <summary>
    /// The day score, 0–100. Training weighs 50, meals 35 and the weigh-in 15; parts the day doesn't have (no
    /// exercises on a rest day, no meals planned) are left out and the rest scaled up. A day marked as skipped
    /// scores its training as zero.
    /// </summary>
    /// <param name="day">The day, with <see cref="DayDTO.SetsDone"/> and <see cref="DayDTO.SetsPlanned"/> filled in.</param>
    /// <returns>The score.</returns>
    public static int DayScore(DayDTO day)
    {
        double total = 0, weight = 0;
        if (day.SetsPlanned > 0)
        {
            weight += 50;
            total += day.TrainingState == TrainingState.Missed ? 0 : 50.0 * day.SetsDone / day.SetsPlanned;
        }

        if (day.Meals.Count > 0)
        {
            weight += 35;
            total += 35.0 * day.Meals.Count(static meal => meal.Eaten) / day.Meals.Count;
        }

        weight += 15;
        total += day.WeightKg is null ? 0 : 15;
        return (int)Math.Round(100 * total / weight, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Whether a day takes part in the streak: it has something planned or a weigh-in.
    /// </summary>
    /// <param name="day">The day.</param>
    /// <returns>True when it counts or breaks the streak.</returns>
    public static bool Counts(DayDTO day) => day.SetsPlanned > 0 || day.Meals.Count > 0 || day.WeightKg is not null;

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
                    exercise.LoadKg, sets[exercise.DayExerciseID].ToList(), exercise.CardioMinutes, exercise.CardioKm, history))
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
                null, [], null, null, history))
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
        IReadOnlyList<ExerciseLogRow> history)
    {
        var targets = type == ExerciseType.Strength ? ExerciseDetailParser.Targets(detail) : [];
        var length = Math.Max(targets.Count, setRows.Count == 0 ? 0 : setRows.Max(static set => set.SetNumber));
        var sets = Enumerable.Range(1, length)
            .Select(number => setRows.FirstOrDefault(set => set.SetNumber == number) is { } set ? (int?)set.Reps : null)
            .ToList();

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
            CardioMinutes = cardioMinutes,
            CardioKm = cardioKm,
        };

        var reps = sets.Sum(static set => set ?? 0);
        if (loadKg is not null && reps > 0)
        {
            dto.VolumeKg = loadKg.Value * reps;
        }

        if (type == ExerciseType.Strength)
        {
            var key = NameText.Key(name);
            var earlier = history.Where(row => row.NameKey == key && DateOnly.FromDateTime(row.DayDate) < date).ToList();
            if (earlier.Count > 0)
            {
                var last = earlier.MaxBy(static row => row.DayDate)!;
                dto.Last = new LastSessionDTO { Date = DateOnly.FromDateTime(last.DayDate), LoadKg = last.LoadKg, Reps = ParseReps(last.Reps) };
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
