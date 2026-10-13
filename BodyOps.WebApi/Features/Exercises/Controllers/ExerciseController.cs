using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Exercises.Models.DTOs;
using BodyOps.WebApi.Features.Exercises.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Exercises.Controllers;

/// <summary>
/// Progression of the signed-in account's exercises.
/// </summary>
/// <param name="exerciseService">Exercise business logic.</param>
[Authorize]
[Route("api/exercises")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class ExerciseController(IExerciseService exerciseService) : InternalControllerBase
{
    private readonly IExerciseService _exerciseService = exerciseService;

    /// <summary>
    /// Lists every strength exercise in the plan or the log.
    /// </summary>
    /// <returns>The exercises, by name.</returns>
    [HttpGet]
    [ProducesResponseType<List<ExerciseSummaryDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ExerciseList", OperationId = "ExerciseList")]
    public async Task<ActionResult<List<ExerciseSummaryDTO>>> ExerciseListAsync()
    {
        var apiResponse = await _exerciseService.ListAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Lists the personal records, newest first.
    /// </summary>
    /// <param name="limit">How many (1–200, default 30).</param>
    /// <returns>The records.</returns>
    [HttpGet("records")]
    [ProducesResponseType<List<RecordDTO>>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "ExerciseRecords", OperationId = "ExerciseRecords")]
    public async Task<ActionResult<List<RecordDTO>>> ExerciseRecordsAsync([FromQuery] int limit = 30)
    {
        var apiResponse = await _exerciseService.RecordsAsync(User.RequiredUserID(), limit);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Gets every logged session of one exercise.
    /// </summary>
    /// <param name="name">The exercise name.</param>
    /// <returns>The sessions, oldest first.</returns>
    [HttpGet("history")]
    [ProducesResponseType<ExerciseHistoryDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "ExerciseHistory", OperationId = "ExerciseHistory")]
    public async Task<ActionResult<ExerciseHistoryDTO>> ExerciseHistoryAsync([FromQuery] string? name)
    {
        var apiResponse = await _exerciseService.HistoryAsync(User.RequiredUserID(), name);
        return apiResponse.ToActionResult();
    }
}
