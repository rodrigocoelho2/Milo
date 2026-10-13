using System.Net;
using System.Net.Mime;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Health.Models.DTOs;
using BodyOps.WebApi.Features.Health.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Health.Controllers;

/// <summary>
/// Health check. Unprotected on purpose: the page calls it to find out whether it runs on the server and whether sign-up is open.
/// </summary>
/// <param name="healthService">Health business logic.</param>
[AllowAnonymous]
[Route("api/health")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
internal sealed class HealthController(IHealthService healthService) : InternalControllerBase
{
    private readonly IHealthService _healthService = healthService;

    /// <summary>
    /// Gets the server status.
    /// </summary>
    /// <returns>The status.</returns>
    [HttpGet]
    [ProducesResponseType<HealthDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "HealthGet", OperationId = "HealthGet")]
    public async Task<ActionResult<HealthDTO>> HealthGetAsync()
    {
        var apiResponse = await _healthService.GetAsync();
        return apiResponse.ToActionResult();
    }
}
