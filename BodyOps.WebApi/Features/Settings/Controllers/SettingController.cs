using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Settings.Controllers;

/// <summary>
/// The signed-in account's settings.
/// </summary>
/// <param name="settingService">Settings business logic.</param>
[Authorize]
[Route("api/settings")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class SettingController(ISettingService settingService) : InternalControllerBase
{
    private readonly ISettingService _settingService = settingService;

    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <returns>The settings.</returns>
    [HttpGet]
    [ProducesResponseType<SettingsDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "SettingGet", OperationId = "SettingGet")]
    public async Task<ActionResult<SettingsDTO>> SettingGetAsync()
    {
        var apiResponse = await _settingService.GetAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="request">The settings.</param>
    /// <returns>The saved settings.</returns>
    [HttpPut]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<SettingsDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [SwaggerOperation(Summary = "SettingSave", OperationId = "SettingSave")]
    public async Task<ActionResult<SettingsDTO>> SettingSaveAsync([FromBody] SettingsDTO request)
    {
        var apiResponse = await _settingService.SaveAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }
}
