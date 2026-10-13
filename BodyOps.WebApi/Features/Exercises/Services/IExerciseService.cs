using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Exercises.Models.DTOs;

namespace BodyOps.WebApi.Features.Exercises.Services;

/// <summary>
/// Progression of each exercise across weeks.
/// </summary>
internal interface IExerciseService
{
    /// <summary>
    /// Lists every exercise in the plan or the log.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The exercises, by name.</returns>
    Task<ServiceApiResponse<List<ExerciseSummaryDTO>>> ListAsync(long userId);

    /// <summary>
    /// Gets every session of one exercise (matched by name, ignoring case and extra spaces).
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="name">The exercise name.</param>
    /// <returns>The sessions (empty when none were logged).</returns>
    Task<ServiceApiResponse<ExerciseHistoryDTO>> HistoryAsync(long userId, string? name);

    /// <summary>
    /// Lists the personal records: sessions that beat every earlier load of their exercise.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="limit">How many, newest first (1–200).</param>
    /// <returns>The records, newest first.</returns>
    Task<ServiceApiResponse<List<RecordDTO>>> RecordsAsync(long userId, int limit);
}
