using System.Net;
using System.Net.Mime;
using System.Text.Json;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Watch.Models.DTOs;
using BodyOps.WebApi.Features.Watch.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Watch.Controllers;

/// <summary>
/// Apple Watch and Apple Health: the personal key for the iOS Shortcut, and what the Shortcut sends.
/// </summary>
/// <param name="watchService">Watch business logic.</param>
[Authorize]
[Route("api/watch")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class WatchController(IWatchService watchService) : InternalControllerBase
{
    private readonly IWatchService _watchService = watchService;

    /// <summary>
    /// The key (hint only) and the workouts of the last four weeks.
    /// </summary>
    /// <returns>The state.</returns>
    [HttpGet]
    [ProducesResponseType<WatchDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "WatchGet", OperationId = "WatchGet")]
    public async Task<ActionResult<WatchDTO>> WatchGetAsync()
    {
        var apiResponse = await _watchService.GetAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Makes a new key (the old one stops working). The key is in this answer only.
    /// </summary>
    /// <returns>The key and the state.</returns>
    [HttpPost("key")]
    [ProducesResponseType<WatchKeyDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "WatchKeyCreate", OperationId = "WatchKeyCreate")]
    public async Task<ActionResult<WatchKeyDTO>> WatchKeyCreateAsync()
    {
        var apiResponse = await _watchService.CreateKeyAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Revokes the key.
    /// </summary>
    /// <returns>The state.</returns>
    [HttpDelete("key")]
    [ProducesResponseType<WatchDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "WatchKeyRevoke", OperationId = "WatchKeyRevoke")]
    public async Task<ActionResult<WatchDTO>> WatchKeyRevokeAsync()
    {
        var apiResponse = await _watchService.RevokeKeyAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// What the Shortcut sends: a workout and/or a weight. Authenticated with the key ("Authorization: Bearer milo_hk_…",
    /// the X-Milo-Key header, or "key" in the body), not the sign-in cookie.
    /// </summary>
    /// <param name="body">The JSON body.</param>
    /// <returns>What was imported, in words the Shortcut can show.</returns>
    [AllowAnonymous]
    [NoAppHeader]
    [EnableRateLimiting(RateLimitPolicies.Watch)]
    [RequestSizeLimit(256 * 1024)]
    [HttpPost("ingest")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<WatchIngestResultDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "WatchIngest", OperationId = "WatchIngest")]
    public async Task<ActionResult<WatchIngestResultDTO>> WatchIngestAsync([FromBody] JsonElement body)
    {
        var authorization = Request.Headers.Authorization.ToString();
        var key = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? authorization[7..].Trim()
            : Request.Headers.TryGetValue("X-Milo-Key", out var header) ? header.ToString().Trim()
            : null;
        var apiResponse = await _watchService.IngestAsync(string.IsNullOrEmpty(key) ? null : key, body);
        return apiResponse.ToActionResult();
    }
}
