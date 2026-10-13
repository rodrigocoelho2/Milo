using System.Globalization;
using System.Net;
using System.Net.Mime;
using System.Text.Json;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Backups.Models.DTOs;
using BodyOps.WebApi.Features.Backups.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Backups.Controllers;

/// <summary>
/// Export and import of the signed-in account as a JSON file. Import also reads CorpoOps backups.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="backupService">Backup business logic.</param>
/// <param name="timeProvider">The time source (file name).</param>
[Authorize]
[Route("api/backup")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class BackupController(
    ILogger<BackupController> logger,
    IBackupService backupService,
    TimeProvider timeProvider) : InternalControllerBase
{
    private readonly ILogger<BackupController> _logger = logger;
    private readonly IBackupService _backupService = backupService;
    private readonly TimeProvider _timeProvider = timeProvider;

    /// <summary>
    /// Downloads everything the account has (plan, logged days, weigh-ins, settings).
    /// </summary>
    /// <returns>The backup, as an attachment.</returns>
    [HttpGet]
    [ProducesResponseType<BackupDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "BackupExport", OperationId = "BackupExport")]
    public async Task<ActionResult<BackupDTO>> BackupExportAsync()
    {
        var apiResponse = await _backupService.ExportAsync(User.RequiredUserID());
        if (apiResponse.IsSuccess)
        {
            var name = string.Create(CultureInfo.InvariantCulture, $"bodyops-backup-{_timeProvider.GetUtcNow():yyyy-MM-dd}.json");
            Response.Headers.ContentDisposition = $"attachment; filename=\"{name}\"";
        }

        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Replaces everything the account has with a BodyOps or CorpoOps backup.
    /// </summary>
    /// <param name="request">The backup file's content.</param>
    /// <returns>What was imported.</returns>
    [HttpPost]
    [Consumes(MediaTypeNames.Application.Json)]
    [RequestSizeLimit(32 * 1024 * 1024)]
    [ProducesResponseType<BackupImportResultDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.BadRequest)]
    [ProducesResponseType((int)HttpStatusCode.RequestEntityTooLarge)]
    [SwaggerOperation(Summary = "BackupImport", OperationId = "BackupImport")]
    public async Task<ActionResult<BackupImportResultDTO>> BackupImportAsync([FromBody] JsonElement request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("BackupImport {UserID}", User.UserID());

        var apiResponse = await _backupService.ImportAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }
}
