using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Plans.Models.Entities;

namespace BodyOps.WebApi.Features.Plans.Repositories;

/// <summary>
/// Data access for the weekly plan (dbo.PlanMeal, dbo.PlanFood, dbo.PlanExercise).
/// </summary>
internal interface IPlanRepository
{
    /// <summary>
    /// Gets the plan (dbo.PlanGet).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="weekday">One weekday, or null for the whole week.</param>
    /// <returns>The meals, foods and exercises.</returns>
    Task<PlanData> GetAsync(long userId, int? weekday = null);

    /// <summary>
    /// Replaces a weekday's meals (dbo.PlanMealsSave).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="weekday">The weekday.</param>
    /// <param name="meals">The meals in order, as JSON (see the procedure).</param>
    /// <param name="syncFrom">The first date whose logged days follow the plan (this week's Monday).</param>
    /// <returns>The weekday's meals (in the order sent) and foods; no exercises.</returns>
    Task<PlanData> SaveMealsAsync(long userId, int weekday, string meals, DateOnly syncFrom);

    /// <summary>
    /// Replaces a weekday's exercises (dbo.PlanExercisesSave).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="weekday">The weekday.</param>
    /// <param name="exercises">The exercises in order, as JSON (see the procedure).</param>
    /// <param name="syncFrom">The first date whose logged days follow the plan.</param>
    /// <returns>The weekday's exercises, in the order sent.</returns>
    Task<List<PlanExercise>> SaveExercisesAsync(long userId, int weekday, string exercises, DateOnly syncFrom);

    /// <summary>
    /// Copies a weekday's meals or exercises onto other weekdays (dbo.PlanCopyDay).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="fromWeekday">The weekday to copy.</param>
    /// <param name="toWeekdays">The weekdays to replace.</param>
    /// <param name="part">Meals or exercises.</param>
    /// <param name="syncFrom">The first date whose logged days follow the plan.</param>
    /// <returns>A task that completes when copied.</returns>
    Task CopyDayAsync(long userId, int fromWeekday, IReadOnlyCollection<int> toWeekdays, PlanPart part, DateOnly syncFrom);
}
