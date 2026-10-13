using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.Entities;

namespace BodyOps.WebApi.Features.Days.Repositories;

/// <summary>
/// Data access for logged days (dbo.Day, dbo.DayMeal, dbo.DayExercise, dbo.DaySet).
/// </summary>
internal interface IDayRepository
{
    /// <summary>
    /// The logged days of a date range (dbo.DayListRange).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First date.</param>
    /// <param name="to">Last date (inclusive).</param>
    /// <returns>The days and their rows.</returns>
    Task<DayRange> ListRangeAsync(long userId, DateOnly from, DateOnly to);

    /// <summary>
    /// Every strength exercise logged with a load or sets (dbo.ExerciseLogList).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The rows, oldest first.</returns>
    Task<List<ExerciseLogRow>> ListExerciseLogAsync(long userId);

    /// <summary>
    /// Ticks or unticks a meal (dbo.DayMealSetEaten).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <param name="planMealId">The planned meal.</param>
    /// <param name="eaten">Eaten or not.</param>
    /// <returns>False when that day has no such meal.</returns>
    Task<bool> SetMealEatenAsync(long userId, DateOnly date, long planMealId, bool eaten);

    /// <summary>
    /// Ticks or unticks every meal of a day (dbo.DayMealSetAllEaten).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <param name="eaten">Eaten or not.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetAllMealsEatenAsync(long userId, DateOnly date, bool eaten);

    /// <summary>
    /// Marks a day's session (dbo.DaySetTrainingState).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <param name="state">The state.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SetTrainingStateAsync(long userId, DateOnly date, TrainingState state);

    /// <summary>
    /// Saves what was done for an exercise (dbo.DayExerciseLog).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <param name="planExerciseId">The planned exercise.</param>
    /// <param name="loadKg">The load.</param>
    /// <param name="sets">The reps of each set (null = not done).</param>
    /// <param name="cardioMinutes">Cardio minutes.</param>
    /// <param name="cardioKm">Cardio kilometres.</param>
    /// <returns>False when that day has no such exercise.</returns>
    Task<bool> LogExerciseAsync(long userId, DateOnly date, long planExerciseId, decimal? loadKg, IReadOnlyList<int?> sets, decimal? cardioMinutes, decimal? cardioKm);

    /// <summary>
    /// Forgets the logged days of a date range (dbo.DayDeleteRange).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">First date.</param>
    /// <param name="to">Last date (inclusive).</param>
    /// <returns>A task that completes when deleted.</returns>
    Task DeleteRangeAsync(long userId, DateOnly from, DateOnly to);

    /// <summary>
    /// Writes logged days, replacing those dates (dbo.DayImport).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="days">The days as JSON (see <see cref="Services.DayJson"/>).</param>
    /// <returns>A task that completes when written.</returns>
    Task ImportAsync(long userId, string days);
}
