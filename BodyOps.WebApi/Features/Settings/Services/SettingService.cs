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
    /// <summary>The rest between sets when none is set.</summary>
    public const int DefaultRestSeconds = 90;

    /// <summary>Shortest rest accepted.</summary>
    public const int MinRestSeconds = 15;

    /// <summary>Longest rest accepted.</summary>
    public const int MaxRestSeconds = 900;

    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> GetAsync(long userId)
    {
        var setting = await _settingRepository.GetAsync(userId);
        return ServiceApiResponse<SettingsDTO>.Success(ToDTO(setting?.WeightGoalKg, setting?.RestSeconds));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<SettingsDTO>> SaveAsync(long userId, SettingsDTO request)
    {
        if (request.WeightGoalKg is < WeightService.MinWeightKg or > WeightService.MaxWeightKg)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("weightGoalKg", nameof(TranslationKeys.InvalidWeight), WeightService.MinWeightKg, WeightService.MaxWeightKg);
        }

        if (request.RestSeconds is < MinRestSeconds or > MaxRestSeconds)
        {
            return await _globalizationHelper.ValidationProblemAsync<SettingsDTO>("restSeconds", nameof(TranslationKeys.InvalidRest), MinRestSeconds, MaxRestSeconds);
        }

        var rest = request.RestSeconds ?? (await _settingRepository.GetAsync(userId))?.RestSeconds;
        var saved = await _settingRepository.SaveAsync(userId, request.WeightGoalKg is { } goal ? Math.Round(goal, 1) : null, rest);
        return ServiceApiResponse<SettingsDTO>.Success(ToDTO(saved.WeightGoalKg, saved.RestSeconds));
    }

    private static SettingsDTO ToDTO(decimal? weightGoalKg, int? restSeconds) =>
        new() { WeightGoalKg = weightGoalKg, RestSeconds = restSeconds ?? DefaultRestSeconds };
}
