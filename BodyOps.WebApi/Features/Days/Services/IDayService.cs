using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Days.Models.DTOs;

namespace BodyOps.WebApi.Features.Days.Services;

/// <summary>
/// Business logic of the logged days: ticking meals, logging sets, marking sessions, and reading weeks.
/// </summary>
internal interface IDayService
{
    /// <summary>
    /// Gets the week a date falls in.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">Any date of the week, or null for this week.</param>
    /// <returns>The week.</returns>
    Task<ServiceApiResponse<WeekDTO>> GetWeekAsync(long userId, DateOnly? date);

    /// <summary>
    /// Gets how complete each day was over the last weeks.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="weeks">How many weeks back, this one included (1–53).</param>
    /// <returns>One entry per day from the first Monday to today.</returns>
    Task<ServiceApiResponse<List<ConsistencyDayDTO>>> GetConsistencyAsync(long userId, int weeks);

    /// <summary>
    /// Gets the streak of days scoring at least <see cref="DayService.StreakThreshold"/>.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The streak.</returns>
    Task<ServiceApiResponse<StreakDTO>> GetStreakAsync(long userId);

    /// <summary>
    /// Ticks or unticks a meal.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <param name="planMealId">The planned meal.</param>
    /// <param name="request">Eaten or not.</param>
    /// <returns>The updated week.</returns>
    Task<ServiceApiResponse<WeekDTO>> SetMealEatenAsync(long userId, DateOnly date, long planMealId, MealEatenDTO request);

    /// <summary>
    /// Ticks or unticks every meal of a day.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <param name="request">Eaten or not.</param>
    /// <returns>The updated week.</returns>
    Task<ServiceApiResponse<WeekDTO>> SetAllMealsEatenAsync(long userId, DateOnly date, MealEatenDTO request);

    /// <summary>
    /// Marks a day's session.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <param name="request">The state.</param>
    /// <returns>The updated week.</returns>
    Task<ServiceApiResponse<WeekDTO>> SetTrainingStateAsync(long userId, DateOnly date, TrainingStateDTO request);

    /// <summary>
    /// Saves what was done for an exercise.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <param name="planExerciseId">The planned exercise.</param>
    /// <param name="request">Load and sets, or minutes and kilometres.</param>
    /// <returns>The updated week.</returns>
    Task<ServiceApiResponse<WeekDTO>> LogExerciseAsync(long userId, DateOnly date, long planExerciseId, ExerciseLogDTO request);

    /// <summary>
    /// Forgets everything logged in a week.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">Any date of the week.</param>
    /// <returns>The removed days, so the page can offer Undo.</returns>
    Task<ServiceApiResponse<List<DayBackupDTO>>> ResetWeekAsync(long userId, DateOnly date);

    /// <summary>
    /// Puts back days removed by <see cref="ResetWeekAsync"/>.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">Any date of the week.</param>
    /// <param name="days">The removed days.</param>
    /// <returns>The restored week.</returns>
    Task<ServiceApiResponse<WeekDTO>> RestoreWeekAsync(long userId, DateOnly date, List<DayBackupDTO>? days);
}
