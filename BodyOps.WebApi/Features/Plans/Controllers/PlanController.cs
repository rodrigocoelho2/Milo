using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Plans.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Plans.Controllers;

/// <summary>
/// The signed-in account's weekly plan: meals and exercises for each weekday.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="planService">Plan business logic.</param>
[Authorize]
[Route("api/plan")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PlanController(
    ILogger<PlanController> logger,
    IPlanService planService) : InternalControllerBase
{
    private readonly ILogger<PlanController> _logger = logger;
    private readonly IPlanService _planService = planService;

    /// <summary>
    /// Gets the weekly plan.
    /// </summary>
    /// <returns>Seven days, Monday first.</returns>
    [HttpGet]
    [ProducesResponseType<PlanDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PlanGet", OperationId = "PlanGet")]
    public async Task<ActionResult<PlanDTO>> PlanGetAsync()
    {
        var apiResponse = await _planService.GetAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Replaces the meals of a weekday.
    /// </summary>
    /// <param name="weekday">1 = Monday … 7 = Sunday.</param>
    /// <param name="request">The meals, in order.</param>
    /// <returns>The weekday's plan.</returns>
    [HttpPut("{weekday:int}/meals")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PlanDayDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PlanSaveMeals", OperationId = "PlanSaveMeals")]
    public async Task<ActionResult<PlanDayDTO>> PlanSaveMealsAsync(int weekday, [FromBody] List<PlanMealSaveDTO> request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PlanSaveMeals {UserID} {Weekday} {Count}", User.UserID(), weekday, request?.Count);

        var apiResponse = await _planService.SaveMealsAsync(User.RequiredUserID(), weekday, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Replaces the exercises of a weekday.
    /// </summary>
    /// <param name="weekday">1 = Monday … 7 = Sunday.</param>
    /// <param name="request">The exercises, in order.</param>
    /// <returns>The weekday's plan.</returns>
    [HttpPut("{weekday:int}/exercises")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PlanDayDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PlanSaveExercises", OperationId = "PlanSaveExercises")]
    public async Task<ActionResult<PlanDayDTO>> PlanSaveExercisesAsync(int weekday, [FromBody] List<PlanExerciseSaveDTO> request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PlanSaveExercises {UserID} {Weekday} {Count}", User.UserID(), weekday, request?.Count);

        var apiResponse = await _planService.SaveExercisesAsync(User.RequiredUserID(), weekday, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Copies a weekday's meals or exercises onto other weekdays, replacing theirs.
    /// </summary>
    /// <param name="weekday">The weekday to copy (1 = Monday … 7 = Sunday).</param>
    /// <param name="request">What to copy and where.</param>
    /// <returns>The whole plan.</returns>
    [HttpPost("{weekday:int}/copy")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PlanDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PlanCopy", OperationId = "PlanCopy")]
    public async Task<ActionResult<PlanDTO>> PlanCopyAsync(int weekday, [FromBody] PlanCopyDTO request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("PlanCopy {UserID} {Weekday} {Part}", User.UserID(), weekday, request.Part);

        var apiResponse = await _planService.CopyAsync(User.RequiredUserID(), weekday, request);
        return apiResponse.ToActionResult();
    }
}
