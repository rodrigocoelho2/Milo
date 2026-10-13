using BodyOps.WebApi.Features.Settings.Models.Entities;

namespace BodyOps.WebApi.Features.Settings.Repositories;

/// <summary>
/// Data access for the account settings (dbo.UserSetting).
/// </summary>
internal interface ISettingRepository
{
    /// <summary>
    /// Gets the settings (dbo.UserSettingGet).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The settings, or null before the first save.</returns>
    Task<UserSetting?> GetAsync(long userId);

    /// <summary>
    /// Saves the settings (dbo.UserSettingSave).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="weightGoalKg">The weight goal.</param>
    /// <returns>The saved settings.</returns>
    Task<UserSetting> SaveAsync(long userId, decimal? weightGoalKg);
}
