using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Days.Repositories;
using BodyOps.WebApi.Features.Days.Services;
using BodyOps.WebApi.Features.Exercises.Models.DTOs;
using BodyOps.WebApi.Features.Plans.Repositories;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Exercises.Services;

/// <summary>
/// Progression from the logged strength sessions.
/// </summary>
/// <param name="dayRepository">Day data access.</param>
/// <param name="planRepository">Plan data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class ExerciseService(
    IDayRepository dayRepository,
    IPlanRepository planRepository,
    IGlobalizationHelper globalizationHelper) : IExerciseService
{
    private readonly IDayRepository _dayRepository = dayRepository;
    private readonly IPlanRepository _planRepository = planRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<ExerciseSummaryDTO>>> ListAsync(long userId)
    {
        var plan = await _planRepository.GetAsync(userId);
        var log = await _dayRepository.ListExerciseLogAsync(userId);

        var byKey = new Dictionary<string, ExerciseSummaryDTO>();
        foreach (var exercise in plan.Exercises.Where(static exercise => exercise.ExerciseType == ExerciseType.Strength))
        {
            byKey.TryAdd(NameText.Key(exercise.Name), new ExerciseSummaryDTO { Name = exercise.Name });
        }

        foreach (var group in log.GroupBy(static row => row.NameKey))
        {
            var latest = group.MaxBy(static row => row.DayDate)!;
            if (!byKey.TryGetValue(group.Key, out var summary))
            {
                summary = new ExerciseSummaryDTO { Name = latest.Name };
                byKey[group.Key] = summary;
            }

            summary.Sessions = group.Count();
            summary.BestKg = group.Max(static row => row.LoadKg);
            summary.LastDate = DateOnly.FromDateTime(latest.DayDate);
        }

        return ServiceApiResponse<List<ExerciseSummaryDTO>>.Success(byKey.Values.OrderBy(static summary => summary.Name, StringComparer.OrdinalIgnoreCase).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<ExerciseHistoryDTO>> HistoryAsync(long userId, string? name)
    {
        var key = NameText.Key(name);
        if (key.Length is 0 or > PlanService.MaxExerciseName)
        {
            return await _globalizationHelper.ValidationProblemAsync<ExerciseHistoryDTO>("name", nameof(TranslationKeys.InvalidName), PlanService.MaxExerciseName);
        }

        var rows = (await _dayRepository.ListExerciseLogAsync(userId)).Where(row => row.NameKey == key).OrderBy(static row => row.DayDate).ToList();
        var sessions = rows.Select(static row =>
        {
            var reps = WeekBuilder.ParseReps(row.Reps);
            return new ExerciseSessionDTO
            {
                Date = DateOnly.FromDateTime(row.DayDate),
                LoadKg = row.LoadKg,
                Reps = reps,
                VolumeKg = row.LoadKg is { } load && reps.Count > 0 ? load * reps.Sum() : null,
            };
        }).ToList();

        return ServiceApiResponse<ExerciseHistoryDTO>.Success(new ExerciseHistoryDTO
        {
            Name = rows.Count > 0 ? rows[^1].Name : NameText.Clean(name),
            BestKg = rows.Max(static row => row.LoadKg),
            Sessions = sessions,
        });
    }
}
