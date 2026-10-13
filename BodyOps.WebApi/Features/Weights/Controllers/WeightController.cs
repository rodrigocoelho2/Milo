using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Weights.Controllers;

/// <summary>
/// The signed-in account's weigh-ins.
/// </summary>
/// <param name="weightService">Weigh-in business logic.</param>
[Authorize]
[Route("api/weights")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class WeightController(IWeightService weightService) : InternalControllerBase
{
    private readonly IWeightService _weightService = weightService;

    /// <summary>
    /// Gets every weigh-in and the goal.
    /// </summary>
    /// <returns>The weigh-ins, oldest first.</returns>
    [HttpGet]
    [ProducesResponseType<WeightsDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "WeightList", OperationId = "WeightList")]
    public async Task<ActionResult<WeightsDTO>> WeightListAsync()
    {
        var apiResponse = await _weightService.ListAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Records the weigh-in of a date, replacing one already there.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd), today or earlier.</param>
    /// <param name="request">The weight.</param>
    /// <returns>Every weigh-in.</returns>
    [HttpPut("{date}")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WeightsDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "WeightSave", OperationId = "WeightSave")]
    public async Task<ActionResult<WeightsDTO>> WeightSaveAsync(DateOnly date, [FromBody] WeightSaveDTO request)
    {
        var apiResponse = await _weightService.SaveAsync(User.RequiredUserID(), date, request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Removes the weigh-in of a date.
    /// </summary>
    /// <param name="date">The date (yyyy-MM-dd).</param>
    /// <returns>Every weigh-in left.</returns>
    [HttpDelete("{date}")]
    [ProducesResponseType<WeightsDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "WeightDelete", OperationId = "WeightDelete")]
    public async Task<ActionResult<WeightsDTO>> WeightDeleteAsync(DateOnly date)
    {
        var apiResponse = await _weightService.DeleteAsync(User.RequiredUserID(), date);
        return apiResponse.ToActionResult();
    }
}
