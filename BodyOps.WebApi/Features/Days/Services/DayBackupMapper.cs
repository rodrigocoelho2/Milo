using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Models.Entities;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Days.Services;

/// <summary>
/// Logged days as backup records (export, and the undo of Reset week).
/// </summary>
internal static class DayBackupMapper
{
    /// <summary>
    /// Converts stored rows to backup records.
    /// </summary>
    /// <param name="range">The rows.</param>
    /// <returns>One record per logged day, oldest first.</returns>
    public static List<DayBackupDTO> ToBackup(DayRange range)
    {
        var meals = range.Meals.ToLookup(static meal => meal.DayID);
        var exercises = range.Exercises.ToLookup(static exercise => exercise.DayID);
        var sets = range.Sets.ToLookup(static set => set.DayExerciseID);

        return range.Days.OrderBy(static day => day.DayDate).Select(day => new DayBackupDTO
        {
            Date = DateOnly.FromDateTime(day.DayDate),
            TrainingState = day.TrainingState,
            Meals = meals[day.DayID].OrderBy(static meal => meal.SortOrder).Select(static meal => new DayMealBackupDTO
            {
                PlanMealId = meal.PlanMealID,
                Name = meal.Name,
                Foods = meal.Foods,
                Eaten = meal.Eaten,
                Time = MealTimeText.Format(meal.MealTime),
                SwapName = meal.SwapName,
                SwapKcal = meal.SwapKcal,
                SwapProteinG = meal.SwapProteinG,
            }).ToList(),
            Exercises = exercises[day.DayID].OrderBy(static exercise => exercise.SortOrder).Select(exercise =>
            {
                var done = sets[exercise.DayExerciseID].ToList();
                var length = done.Count == 0 ? 0 : done.Max(static set => set.SetNumber);
                return new DayExerciseBackupDTO
                {
                    PlanExerciseId = exercise.PlanExerciseID,
                    Type = exercise.ExerciseType,
                    Name = exercise.Name,
                    Detail = exercise.Detail,
                    Notes = exercise.Notes,
                    Muscles = exercise.Muscles is null ? null : MuscleDetector.Parse(exercise.Muscles),
                    LoadKg = exercise.LoadKg,
                    CardioMinutes = exercise.CardioMinutes,
                    CardioKm = exercise.CardioKm,
                    CardioKind = exercise.CardioKind,
                    Sets = Enumerable.Range(1, length)
                        .Select(number => done.FirstOrDefault(set => set.SetNumber == number) is { } set ? (int?)set.Reps : null)
                        .ToList(),
                    SetLoads = Enumerable.Range(1, length)
                        .Select(number => done.FirstOrDefault(set => set.SetNumber == number)?.LoadKg)
                        .ToList(),
                };
            }).ToList(),
        }).ToList();
    }
}
