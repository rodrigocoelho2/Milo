using System.Globalization;
using System.Text.Json;
using System.Text.RegularExpressions;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Backups.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Backups.Services;

/// <summary>
/// Reads a backup made by CorpoOps, the single-file app BodyOps replaces: weekdays keyed "Seg" … "Dom", meals and
/// exercises with string ids, weekly logs with a free-text "real" field ("3x10", "32 min · 5,2 km"), and plan snapshots
/// of past weeks. The names are kept as they are in the file.
/// </summary>
internal static partial class LegacyBackupConverter
{
    private static readonly string[] Weekdays = ["Seg", "Ter", "Qua", "Qui", "Sex", "Sáb", "Dom"];

    private static readonly Dictionary<string, Muscle> Muscles = new(StringComparer.OrdinalIgnoreCase)
    {
        ["peito"] = Muscle.Chest,
        ["ombros"] = Muscle.Shoulders,
        ["biceps"] = Muscle.Biceps,
        ["triceps"] = Muscle.Triceps,
        ["antebracos"] = Muscle.Forearms,
        ["abdominais"] = Muscle.Abs,
        ["dorsais"] = Muscle.Lats,
        ["trapezio"] = Muscle.Traps,
        ["lombar"] = Muscle.LowerBack,
        ["gluteos"] = Muscle.Glutes,
        ["quadriceps"] = Muscle.Quads,
        ["isquiotibiais"] = Muscle.Hamstrings,
        ["gemeos"] = Muscle.Calves,
    };

    /// <summary>
    /// Tells whether a JSON document looks like a CorpoOps backup.
    /// </summary>
    /// <param name="root">The document.</param>
    /// <returns>True when it has "diet" and "workout" objects.</returns>
    public static bool IsLegacy(JsonElement root) =>
        root.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("diet", out var diet) && diet.ValueKind == JsonValueKind.Object
        && root.TryGetProperty("workout", out var workout) && workout.ValueKind == JsonValueKind.Object;

    /// <summary>
    /// Converts a CorpoOps backup.
    /// </summary>
    /// <param name="root">The document.</param>
    /// <returns>The same data as a BodyOps backup.</returns>
    public static BackupDTO Convert(JsonElement root)
    {
        var mealIds = new Dictionary<string, long>(StringComparer.Ordinal);
        var exerciseIds = new Dictionary<string, long>(StringComparer.Ordinal);
        long Id(Dictionary<string, long> ids, string key)
        {
            if (!ids.TryGetValue(key, out var id))
            {
                id = ids.Count + 1;
                ids[key] = id;
            }

            return id;
        }

        var backup = new BackupDTO { ExportedAt = DateTime.UtcNow };
        var diet = root.GetProperty("diet");
        var workout = root.GetProperty("workout");

        // Plan
        for (var i = 0; i < 7; i++)
        {
            var day = new BackupPlanDayDTO { Weekday = i + 1 };
            foreach (var meal in Meals(diet, Weekdays[i]))
            {
                day.Meals.Add(new BackupMealDTO
                {
                    Id = Id(mealIds, meal.Id),
                    Name = meal.Name,
                    Foods = meal.Foods.Select(static food => new BackupFoodDTO { Name = food.Name, Amount = food.Amount, Unit = food.Unit }).ToList(),
                });
            }

            foreach (var exercise in Exercises(workout, Weekdays[i]))
            {
                day.Exercises.Add(new BackupExerciseDTO
                {
                    Id = Id(exerciseIds, exercise.Id),
                    Type = exercise.Type,
                    Name = exercise.Name,
                    Detail = exercise.Detail,
                    Notes = exercise.Notes,
                    Muscles = exercise.Muscles,
                });
            }

            backup.Plan.Add(day);
        }

        // Logged days (each week shows the plan it had, from its snapshot when there is one)
        var snapshots = root.TryGetProperty("planSnapshots", out var snapshotElement) && snapshotElement.ValueKind == JsonValueKind.Object ? snapshotElement : default;
        if (root.TryGetProperty("logs", out var logs) && logs.ValueKind == JsonValueKind.Object)
        {
            foreach (var week in logs.EnumerateObject().OrderBy(static week => week.Name, StringComparer.Ordinal))
            {
                if (week.Value.ValueKind != JsonValueKind.Object
                    || !DateOnly.TryParseExact(week.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var monday))
                {
                    continue;
                }

                var weekDiet = diet;
                var weekWorkout = workout;
                if (snapshots.ValueKind == JsonValueKind.Object && snapshots.TryGetProperty(week.Name, out var snapshot) && snapshot.ValueKind == JsonValueKind.Object)
                {
                    if (snapshot.TryGetProperty("diet", out var snapshotDiet) && snapshotDiet.ValueKind == JsonValueKind.Object)
                    {
                        weekDiet = snapshotDiet;
                    }

                    if (snapshot.TryGetProperty("workout", out var snapshotWorkout) && snapshotWorkout.ValueKind == JsonValueKind.Object)
                    {
                        weekWorkout = snapshotWorkout;
                    }
                }

                for (var i = 0; i < 7; i++)
                {
                    if (!week.Value.TryGetProperty(Weekdays[i], out var log) || log.ValueKind != JsonValueKind.Object)
                    {
                        continue;
                    }

                    if (Day(monday.AddDays(i), log, Meals(weekDiet, Weekdays[i]), Exercises(weekWorkout, Weekdays[i]), key => Id(mealIds, key), key => Id(exerciseIds, key)) is { } day)
                    {
                        backup.Days.Add(day);
                    }
                }
            }
        }

        // Weigh-ins and goal
        if (root.TryGetProperty("weightLog", out var weights) && weights.ValueKind == JsonValueKind.Object)
        {
            foreach (var entry in weights.EnumerateObject())
            {
                if (DateOnly.TryParseExact(entry.Name, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
                    && Number(entry.Value) is { } kg)
                {
                    backup.Weights.Add(new BackupWeightDTO { Date = date, WeightKg = kg });
                }
            }

            backup.Weights.Sort(static (a, b) => a.Date.CompareTo(b.Date));
        }

        if (root.TryGetProperty("settings", out var settings) && settings.ValueKind == JsonValueKind.Object
            && settings.TryGetProperty("weightTarget", out var target))
        {
            backup.Settings.WeightGoalKg = Number(target);
        }

        return backup;
    }

    /// <summary>
    /// Reads the sets out of CorpoOps' "real" text: "3x10" is three sets of 10, "3×12, 3×10" six sets,
    /// "12, 11, 10" three sets.
    /// </summary>
    /// <param name="real">The text.</param>
    /// <returns>The reps of each set.</returns>
    public static List<int?> ParseSets(string? real)
    {
        var sets = new List<int?>();
        if (string.IsNullOrWhiteSpace(real))
        {
            return sets;
        }

        var text = real.Replace('×', 'x').Replace('X', 'x');
        var grouped = ExerciseDetailParser.Targets(text);
        if (grouped.Count > 0 && text.Contains('x'))
        {
            sets.AddRange(grouped.Select(static reps => (int?)reps));
            return sets;
        }

        foreach (Match match in PlainNumber().Matches(text))
        {
            if (sets.Count < ExerciseDetailParser.MaxSets && int.TryParse(match.Value, NumberStyles.None, CultureInfo.InvariantCulture, out var reps) && reps <= 999)
            {
                sets.Add(reps);
            }
        }

        return sets;
    }

    /// <summary>
    /// Reads minutes and kilometres out of CorpoOps' cardio "real" text ("32 min · 5,2 km").
    /// </summary>
    /// <param name="real">The text.</param>
    /// <returns>The minutes and kilometres found.</returns>
    public static (decimal? Minutes, decimal? Km) ParseCardio(string? real)
    {
        if (string.IsNullOrWhiteSpace(real))
        {
            return (null, null);
        }

        var minutes = Minutes().Match(real);
        var km = Kilometres().Match(real);
        return (minutes.Success ? Decimal(minutes.Groups[1].Value) : null, km.Success ? Decimal(km.Groups[1].Value) : null);
    }

    private static DayBackupDTO? Day(
        DateOnly date,
        JsonElement log,
        List<LegacyMeal> meals,
        List<LegacyExercise> exercises,
        Func<string, long> mealId,
        Func<string, long> exerciseId)
    {
        var eaten = log.TryGetProperty("mealsDone", out var mealsDone) && mealsDone.ValueKind == JsonValueKind.Object ? mealsDone : default;
        var exLog = log.TryGetProperty("exLog", out var exLogElement) && exLogElement.ValueKind == JsonValueKind.Object ? exLogElement : default;
        var state = Text(log, "trainState") switch
        {
            "done" => TrainingState.Done,
            "missed" => TrainingState.Missed,
            _ => TrainingState.Pending,
        };

        var day = new DayBackupDTO { Date = date, TrainingState = state };
        foreach (var meal in meals)
        {
            day.Meals.Add(new DayMealBackupDTO
            {
                PlanMealId = mealId(meal.Id),
                Name = meal.Name,
                Foods = string.Join(" · ", meal.Foods.Select(static food =>
                    $"{food.Name} {food.Amount.ToString("0.##", CultureInfo.InvariantCulture)} {(food.Unit == FoodUnit.Pieces ? "pc" : "g")}")),
                Eaten = eaten.ValueKind == JsonValueKind.Object && eaten.TryGetProperty(meal.Id, out var flag) && flag.ValueKind == JsonValueKind.True,
            });
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exercise in exercises)
        {
            seen.Add(exercise.Id);
            var entry = exLog.ValueKind == JsonValueKind.Object && exLog.TryGetProperty(exercise.Id, out var found) && found.ValueKind == JsonValueKind.Object ? found : default;
            day.Exercises.Add(Logged(exerciseId(exercise.Id), exercise.Type, exercise.Name, exercise.Detail, exercise.Notes, exercise.Muscles, entry));
        }

        // Logged exercises whose plan entry was deleted before the snapshot existed.
        if (exLog.ValueKind == JsonValueKind.Object)
        {
            foreach (var orphan in exLog.EnumerateObject().Where(property => !seen.Contains(property.Name) && property.Value.ValueKind == JsonValueKind.Object))
            {
                var name = NameText.Clean(Text(orphan.Value, "name"));
                if (name.Length == 0)
                {
                    continue;
                }

                var type = Text(orphan.Value, "type") == "cardio" ? ExerciseType.Cardio : ExerciseType.Strength;
                day.Exercises.Add(Logged(exerciseId(orphan.Name), type, name, string.Empty, string.Empty, null, orphan.Value));
            }
        }

        var anything = day.TrainingState != TrainingState.Pending
            || day.Meals.Any(static meal => meal.Eaten)
            || day.Exercises.Any(static exercise => exercise.LoadKg is not null || exercise.Sets.Count > 0 || exercise.CardioMinutes is not null || exercise.CardioKm is not null);
        return anything ? day : null;
    }

    private static DayExerciseBackupDTO Logged(long id, ExerciseType type, string name, string detail, string notes, List<Muscle>? muscles, JsonElement entry)
    {
        var exercise = new DayExerciseBackupDTO { PlanExerciseId = id, Type = type, Name = name, Detail = detail, Notes = notes, Muscles = muscles };
        if (entry.ValueKind != JsonValueKind.Object)
        {
            return exercise;
        }

        var real = Text(entry, "real");
        if (type == ExerciseType.Cardio)
        {
            (exercise.CardioMinutes, exercise.CardioKm) = ParseCardio(real);
        }
        else
        {
            exercise.LoadKg = entry.TryGetProperty("kg", out var kg) ? Number(kg) : null;
            exercise.Sets = ParseSets(real);
        }

        return exercise;
    }

    private static List<LegacyMeal> Meals(JsonElement diet, string weekday)
    {
        var meals = new List<LegacyMeal>();
        if (!diet.TryGetProperty(weekday, out var list) || list.ValueKind != JsonValueKind.Array)
        {
            return meals;
        }

        foreach (var meal in list.EnumerateArray().Where(static meal => meal.ValueKind == JsonValueKind.Object))
        {
            var id = Text(meal, "id");
            var name = NameText.Clean(Text(meal, "name"));
            if (id.Length == 0)
            {
                continue;
            }

            var foods = new List<LegacyFood>();
            if (meal.TryGetProperty("foods", out var foodList) && foodList.ValueKind == JsonValueKind.Array)
            {
                foreach (var food in foodList.EnumerateArray().Where(static food => food.ValueKind == JsonValueKind.Object))
                {
                    var foodName = NameText.Clean(Text(food, "name"));
                    if (foodName.Length == 0)
                    {
                        continue;
                    }

                    var amount = (food.TryGetProperty("amount", out var amountElement) ? Number(amountElement) : null)
                        ?? (food.TryGetProperty("grams", out var grams) ? Number(grams) : null)
                        ?? 0;
                    foods.Add(new LegacyFood(foodName, Math.Clamp(amount, 0, PlanService.MaxAmount), Text(food, "unit") == "un" ? FoodUnit.Pieces : FoodUnit.Grams));
                }
            }

            meals.Add(new LegacyMeal(id, name.Length == 0 ? "Meal" : name, foods));
        }

        return meals;
    }

    private static List<LegacyExercise> Exercises(JsonElement workout, string weekday)
    {
        var exercises = new List<LegacyExercise>();
        if (!workout.TryGetProperty(weekday, out var day) || day.ValueKind != JsonValueKind.Object
            || !day.TryGetProperty("entries", out var entries) || entries.ValueKind != JsonValueKind.Array)
        {
            return exercises;
        }

        foreach (var entry in entries.EnumerateArray().Where(static entry => entry.ValueKind == JsonValueKind.Object))
        {
            var id = Text(entry, "id");
            var name = NameText.Clean(Text(entry, "name"));
            if (id.Length == 0 || name.Length == 0)
            {
                continue;
            }

            List<Muscle>? muscles = null;
            if (entry.TryGetProperty("muscles", out var muscleList) && muscleList.ValueKind == JsonValueKind.Array)
            {
                muscles = muscleList.EnumerateArray()
                    .Select(static muscle => muscle.ValueKind == JsonValueKind.String && Muscles.TryGetValue(muscle.GetString()!, out var value) ? (Muscle?)value : null)
                    .OfType<Muscle>()
                    .Distinct()
                    .ToList();
            }

            exercises.Add(new LegacyExercise(
                id,
                Text(entry, "type") == "cardio" ? ExerciseType.Cardio : ExerciseType.Strength,
                name,
                NameText.Clean(Text(entry, "detail")),
                Text(entry, "notes").Trim(),
                muscles));
        }

        return exercises;
    }

    private static string Text(JsonElement element, string property) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(property, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? string.Empty
            : string.Empty;

    private static decimal? Number(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Number when element.TryGetDecimal(out var number) => number,
        JsonValueKind.String => Decimal(element.GetString()),
        _ => null,
    };

    private static decimal? Decimal(string? text) =>
        decimal.TryParse((text ?? string.Empty).Trim().Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var value) ? value : null;

    [GeneratedRegex(@"\d{1,3}")]
    private static partial Regex PlainNumber();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*min", RegexOptions.IgnoreCase)]
    private static partial Regex Minutes();

    [GeneratedRegex(@"(\d+(?:[.,]\d+)?)\s*km", RegexOptions.IgnoreCase)]
    private static partial Regex Kilometres();

    private sealed record LegacyFood(string Name, decimal Amount, FoodUnit Unit);

    private sealed record LegacyMeal(string Id, string Name, List<LegacyFood> Foods);

    private sealed record LegacyExercise(string Id, ExerciseType Type, string Name, string Detail, string Notes, List<Muscle>? Muscles);
}
