using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Settings.Models.DTOs;

namespace BodyOps.WebApi.Features.Settings.Services;

/// <summary>
/// Business logic of the account settings.
/// </summary>
internal interface ISettingService
{
    /// <summary>
    /// Gets the settings.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The settings (defaults before the first save).</returns>
    Task<ServiceApiResponse<SettingsDTO>> GetAsync(long userId);

    /// <summary>
    /// Saves the settings.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="request">The settings.</param>
    /// <returns>The saved settings, or validation problems.</returns>
    Task<ServiceApiResponse<SettingsDTO>> SaveAsync(long userId, SettingsDTO request);
}
