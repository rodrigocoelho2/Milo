using System.Text.Json;
using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Backups.Models.DTOs;

namespace BodyOps.WebApi.Features.Backups.Services;

/// <summary>
/// Export and import of a whole account.
/// </summary>
internal interface IBackupService
{
    /// <summary>
    /// Exports everything the account has.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The backup.</returns>
    Task<ServiceApiResponse<BackupDTO>> ExportAsync(long userId);

    /// <summary>
    /// Replaces everything the account has with a backup (BodyOps or CorpoOps).
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="document">The backup file's JSON.</param>
    /// <returns>What was imported, or why the file was refused.</returns>
    Task<ServiceApiResponse<BackupImportResultDTO>> ImportAsync(long userId, JsonElement document);
}
