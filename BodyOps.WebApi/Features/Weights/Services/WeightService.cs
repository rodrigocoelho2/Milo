using System.Globalization;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Extensions.Time;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Settings.Repositories;
using BodyOps.WebApi.Features.Weights.Models.DTOs;
using BodyOps.WebApi.Features.Weights.Repositories;

namespace BodyOps.WebApi.Features.Weights.Services;

/// <summary>
/// Records and lists weigh-ins.
/// </summary>
/// <param name="weightRepository">Weigh-in data access.</param>
/// <param name="settingRepository">Settings data access (the goal).</param>
/// <param name="clock">Today in the configured time zone.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class WeightService(
    IWeightRepository weightRepository,
    ISettingRepository settingRepository,
    IAppClock clock,
    IGlobalizationHelper globalizationHelper) : IWeightService
{
    /// <summary>Lightest accepted weight.</summary>
    public const decimal MinWeightKg = 20m;

    /// <summary>Heaviest accepted weight.</summary>
    public const decimal MaxWeightKg = 400m;

    private readonly IWeightRepository _weightRepository = weightRepository;
    private readonly ISettingRepository _settingRepository = settingRepository;
    private readonly IAppClock _clock = clock;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeightsDTO>> ListAsync(long userId) =>
        ServiceApiResponse<WeightsDTO>.Success(await BuildAsync(userId));

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeightsDTO>> SaveAsync(long userId, DateOnly date, WeightSaveDTO request)
    {
        if (await DateProblemAsync(date) is { } dateProblem)
        {
            return dateProblem;
        }

        if (request.WeightKg is < MinWeightKg or > MaxWeightKg)
        {
            return await _globalizationHelper.ValidationProblemAsync<WeightsDTO>("weightKg", nameof(TranslationKeys.InvalidWeight), MinWeightKg, MaxWeightKg);
        }

        await _weightRepository.SaveAsync(userId, date, Math.Round(request.WeightKg, 1));
        return ServiceApiResponse<WeightsDTO>.Success(await BuildAsync(userId));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<WeightsDTO>> DeleteAsync(long userId, DateOnly date) =>
        await _weightRepository.DeleteAsync(userId, date)
            ? ServiceApiResponse<WeightsDTO>.Success(await BuildAsync(userId))
            : await _globalizationHelper.NotFoundAsync<WeightsDTO>(date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

    private async Task<WeightsDTO> BuildAsync(long userId)
    {
        var entries = await _weightRepository.ListAsync(userId);
        var setting = await _settingRepository.GetAsync(userId);
        return new WeightsDTO
        {
            Entries = entries.Select(static entry => new WeightEntryDTO { Date = DateOnly.FromDateTime(entry.EntryDate), WeightKg = entry.WeightKg }).ToList(),
            GoalKg = setting?.WeightGoalKg,
        };
    }

    private async Task<ServiceApiResponse<WeightsDTO>?> DateProblemAsync(DateOnly date)
    {
        var today = _clock.Today;
        return date < DayService.MinDate || date > today
            ? await _globalizationHelper.ValidationProblemAsync<WeightsDTO>("date", nameof(TranslationKeys.InvalidDate),
                DayService.MinDate.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), today.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture))
            : null;
    }
}
