using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Days.Models.DTOs;
using BodyOps.WebApi.Features.Days.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Days.Controllers;

/// <summary>
/// The signed-in account's logged days: weeks, ticked meals, logged sets and sessions. Every change answers with the updated week.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="dayService">Day business logic.</param>
[Authorize]
[Route("api/days")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class DayController(
    ILogger<DayController> logger,
    IDayService dayService) : InternalControllerBase
{
    private readonly ILogger<DayController> _logger = logger;
    private readonly IDayService _dayService = dayService;

    /// <summary>
    /// Gets the week a date falls in.
    /// </summary>
    /// <param name="date">Any date of the week (yyyy-MM-dd); this week when left out.</param>
    /// <returns>The week.</returns>
    [HttpGet("week")]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayWeekGet", OperationId = "DayWeekGet")]
    public async Task<ActionResult<WeekDTO>> DayWeekGetAsync([FromQuery] DateOnly? date)
    {
        var apiResponse = await _dayService.GetWeekAsync(User.RequiredUserID(), date);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Gets how complete each day was over the last weeks (the consistency grid).
    /// </summary>
    /// <param name="weeks">How many weeks, this one included (1–53, default 21).</param>
    /// <returns>One entry per day up to today.</returns>
    [HttpGet("consistency")]
    [ProducesResponseType<List<ConsistencyDayDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayConsistency", OperationId = "DayConsistency")]
    public async Task<ActionResult<List<ConsistencyDayDTO>>> DayConsistencyAsync([FromQuery] int weeks = 21)
    {
        var apiResponse = await _dayService.GetConsistencyAsync(User.RequiredUserID(), weeks);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Ticks or unticks one meal.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd).</param>
    /// <param name="planMealId">The planned meal.</param>
    /// <param name="request">Eaten or not.</param>
    /// <returns>The updated week.</returns>
    [HttpPut("{date}/meals/{planMealId:long}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "DayMealEaten", OperationId = "DayMealEaten")]
    public async Task<ActionResult<WeekDTO>> DayMealEatenAsync(DateOnly date, long planMealId, [FromBody] MealEatenDTO request)
    {
        var apiResponse = await _dayService.SetMealEatenAsync(User.RequiredUserID(), date, planMealId, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Ticks or unticks every meal of a day.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd).</param>
    /// <param name="request">Eaten or not.</param>
    /// <returns>The updated week.</returns>
    [HttpPut("{date}/meals")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayMealsEaten", OperationId = "DayMealsEaten")]
    public async Task<ActionResult<WeekDTO>> DayMealsEatenAsync(DateOnly date, [FromBody] MealEatenDTO request)
    {
        var apiResponse = await _dayService.SetAllMealsEatenAsync(User.RequiredUserID(), date, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Marks a day's session as not marked, trained or skipped.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd).</param>
    /// <param name="request">The state.</param>
    /// <returns>The updated week.</returns>
    [HttpPut("{date}/training")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayTraining", OperationId = "DayTraining")]
    public async Task<ActionResult<WeekDTO>> DayTrainingAsync(DateOnly date, [FromBody] TrainingStateDTO request)
    {
        var apiResponse = await _dayService.SetTrainingStateAsync(User.RequiredUserID(), date, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Saves the load and sets (or minutes and kilometres) of one exercise.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd).</param>
    /// <param name="planExerciseId">The planned exercise.</param>
    /// <param name="request">What was done.</param>
    /// <returns>The updated week.</returns>
    [HttpPut("{date}/exercises/{planExerciseId:long}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "DayExerciseLog", OperationId = "DayExerciseLog")]
    public async Task<ActionResult<WeekDTO>> DayExerciseLogAsync(DateOnly date, long planExerciseId, [FromBody] ExerciseLogDTO request)
    {
        var apiResponse = await _dayService.LogExerciseAsync(User.RequiredUserID(), date, planExerciseId, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Forgets everything logged in the week of a date (Reset week).
    /// </summary>
    /// <param name="date">Any date of the week (yyyy-MM-dd).</param>
    /// <returns>The removed days, to send back to restore for Undo.</returns>
    [HttpDelete("week/{date}")]
    [ProducesResponseType<List<DayBackupDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayWeekReset", OperationId = "DayWeekReset")]
    public async Task<ActionResult<List<DayBackupDTO>>> DayWeekResetAsync(DateOnly date)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("DayWeekReset {UserID} {Date}", User.UserID(), date);

        var apiResponse = await _dayService.ResetWeekAsync(User.RequiredUserID(), date);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Puts back the days removed by Reset week (Undo).
    /// </summary>
    /// <param name="date">Any date of the week (yyyy-MM-dd).</param>
    /// <param name="request">The removed days.</param>
    /// <returns>The restored week.</returns>
    [HttpPost("week/{date}/restore")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeekDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "DayWeekRestore", OperationId = "DayWeekRestore")]
    public async Task<ActionResult<WeekDTO>> DayWeekRestoreAsync(DateOnly date, [FromBody] List<DayBackupDTO> request)
    {
        var apiResponse = await _dayService.RestoreWeekAsync(User.RequiredUserID(), date, request);
        return apiResponse.ToActionResult();
    }
}
