using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Push.Models.DTOs;
using BodyOps.WebApi.Features.Push.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Push.Controllers;

/// <summary>
/// Push reminders: the account's devices, turning push on and off for this browser, and a test.
/// </summary>
/// <param name="pushService">Push business logic.</param>
[Authorize]
[Route("api/push")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class PushController(IPushService pushService) : InternalControllerBase
{
    private readonly IPushService _pushService = pushService;

    /// <summary>
    /// Whether push is on, the key to subscribe with and the account's devices.
    /// </summary>
    /// <returns>The push state.</returns>
    [HttpGet]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushGet", OperationId = "PushGet")]
    public async Task<ActionResult<PushDTO>> PushGetAsync()
    {
        var apiResponse = await _pushService.GetAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Turns push on for this browser.
    /// </summary>
    /// <param name="request">The browser's subscription.</param>
    /// <returns>The push state.</returns>
    [HttpPost("subscribe")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [SwaggerOperation(Summary = "PushSubscribe", OperationId = "PushSubscribe")]
    public async Task<ActionResult<PushDTO>> PushSubscribeAsync([FromBody] PushSubscribeDTO request)
    {
        var apiResponse = await _pushService.SubscribeAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Turns push off for a device.
    /// </summary>
    /// <param name="request">The device (id or endpoint).</param>
    /// <returns>The push state.</returns>
    [HttpPost("unsubscribe")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<PushDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "PushUnsubscribe", OperationId = "PushUnsubscribe")]
    public async Task<ActionResult<PushDTO>> PushUnsubscribeAsync([FromBody] PushUnsubscribeDTO request)
    {
        var apiResponse = await _pushService.UnsubscribeAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Sends a test notification to the account's devices.
    /// </summary>
    /// <returns>How many devices it reached.</returns>
    [HttpPost("test")]
    [ProducesResponseType<PushTestResultDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.ServiceUnavailable)]
    [SwaggerOperation(Summary = "PushTest", OperationId = "PushTest")]
    public async Task<ActionResult<PushTestResultDTO>> PushTestAsync()
    {
        var apiResponse = await _pushService.TestAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }
}
