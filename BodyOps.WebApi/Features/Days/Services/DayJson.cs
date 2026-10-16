using System.Globalization;
using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Days.Services;

/// <summary>
/// The JSON document dbo.DayImport reads (property names are the procedure's JSON paths).
/// </summary>
internal static class DayJson
{
    /// <summary>
    /// Serializes days for dbo.DayImport. Names are cleaned on the way.
    /// </summary>
    /// <param name="days">The days.</param>
    /// <returns>The JSON array.</returns>
    public static string Serialize(IEnumerable<DayBackupDTO> days) => JsonSerializer.Serialize(days.Select(static day => new
    {
        date = day.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        trainingState = (int)day.TrainingState,
        meals = day.Meals.Select(static meal => new
        {
            planMealId = meal.PlanMealId,
            name = Truncate(NameText.Clean(meal.Name), PlanService.MaxMealName),
            foods = Truncate(meal.Foods ?? string.Empty, 2000),
            eaten = meal.Eaten,
            time = MealTimeText.TryParse(meal.Time, out var time) ? MealTimeText.Format(time) : null,
        }).ToList(),
        exercises = day.Exercises.Select(static exercise => new
        {
            planExerciseId = exercise.PlanExerciseId,
            type = (int)exercise.Type,
            name = Truncate(NameText.Clean(exercise.Name), PlanService.MaxExerciseName),
            detail = Truncate(NameText.Clean(exercise.Detail), PlanService.MaxDetail),
            notes = Truncate((exercise.Notes ?? string.Empty).Trim(), PlanService.MaxNotes),
            muscles = MuscleDetector.Format(exercise.Muscles),
            loadKg = exercise.LoadKg,
            cardioMinutes = exercise.CardioMinutes,
            cardioKm = exercise.CardioKm,
            cardioKind = exercise.Type == ExerciseType.Cardio && exercise.CardioKind is { } kind && Enum.IsDefined(kind) ? (int?)kind : null,
            sets = exercise.Sets.Take(ExerciseDetailParser.MaxSets).ToList(),
            setLoads = (exercise.SetLoads ?? []).Take(ExerciseDetailParser.MaxSets)
                .Select(static load => load is >= 0 and <= DayService.MaxLoadKg ? load : null).ToList(),
        }).ToList(),
    }).ToList());

    private static string Truncate(string value, int length) => value.Length <= length ? value : value[..length];
}
