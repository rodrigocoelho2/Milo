using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Settings.Models.DTOs;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Weights.Services;

namespace BodyOps.WebApi.Features.Settings.Services;

/// <summary>
/// Reads and saves the account settings.
/// </summary>
/// <param name="settingRepository">Settings data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class SettingService(ISettingRepository settingRepository, IGlobalizationHelper globalizationHelper) : ISettingService
{
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> GetAsync(long userId)
    {
        var setting = await _settingRepository.GetAsync(userId);
        return ServiceApiResponse<SettingsDTO>.Success(new SettingsDTO { WeightGoalKg = setting?.WeightGoalKg });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> SaveAsync(long userId, SettingsDTO request)
    {
        if (request.WeightGoalKg is < WeightService.MinWeightKg or > WeightService.MaxWeightKg)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("weightGoalKg", nameof(TranslationKeys.InvalidWeight), WeightService.MinWeightKg, WeightService.MaxWeightKg);
        }

        var saved = await _settingRepository.SaveAsync(userId, request.WeightGoalKg is { } goal ? Math.Round(goal, 1) : null);
        return ServiceApiResponse<SettingsDTO>.Success(new SettingsDTO { WeightGoalKg = saved.WeightGoalKg });
    }
}
