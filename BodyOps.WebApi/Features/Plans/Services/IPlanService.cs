using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Business logic of the weekly plan.
/// </summary>
internal interface IPlanService
{
    /// <summary>
    /// Gets the whole plan.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>Seven days, Monday first.</returns>
    Task<ServiceApiResponse<PlanDTO>> GetAsync(long userId);

    /// <summary>
    /// Replaces a weekday's meals.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="weekday">1 = Monday … 7 = Sunday.</param>
    /// <param name="meals">The meals, in order.</param>
    /// <returns>The weekday's plan, or validation problems.</returns>
    Task<ServiceApiResponse<PlanDayDTO>> SaveMealsAsync(long userId, int weekday, List<PlanMealSaveDTO>? meals);

    /// <summary>
    /// Replaces a weekday's exercises.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="weekday">1 = Monday … 7 = Sunday.</param>
    /// <param name="exercises">The exercises, in order.</param>
    /// <returns>The weekday's plan, or validation problems.</returns>
    Task<ServiceApiResponse<PlanDayDTO>> SaveExercisesAsync(long userId, int weekday, List<PlanExerciseSaveDTO>? exercises);

    /// <summary>
    /// Copies a weekday's meals or exercises onto other weekdays.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="weekday">The weekday to copy.</param>
    /// <param name="request">What to copy and where.</param>
    /// <returns>The whole plan, or validation problems.</returns>
    Task<ServiceApiResponse<PlanDTO>> CopyAsync(long userId, int weekday, PlanCopyDTO request);

    /// <summary>
    /// Names a weekday's session ("Upper body").
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="weekday">1 = Monday … 7 = Sunday.</param>
    /// <param name="request">The name; empty removes it.</param>
    /// <returns>The weekday's plan, or validation problems.</returns>
    Task<ServiceApiResponse<PlanDayDTO>> SaveNameAsync(long userId, int weekday, PlanDayNameDTO request);
}
