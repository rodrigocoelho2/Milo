using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Weights.Models.DTOs;

namespace BodyOps.WebApi.Features.Weights.Services;

/// <summary>
/// Business logic of the weigh-ins.
/// </summary>
internal interface IWeightService
{
    /// <summary>
    /// Gets every weigh-in and the goal.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The weigh-ins.</returns>
    Task<ServiceApiResponse<WeightsDTO>> ListAsync(long userId);

    /// <summary>
    /// Records the weigh-in of a date (replacing one already there).
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <param name="request">The weight.</param>
    /// <returns>Every weigh-in, or validation problems.</returns>
    Task<ServiceApiResponse<WeightsDTO>> SaveAsync(long userId, DateOnly date, WeightSaveDTO request);

    /// <summary>
    /// Removes the weigh-in of a date.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="date">The date.</param>
    /// <returns>Every weigh-in left, or 404.</returns>
    Task<ServiceApiResponse<WeightsDTO>> DeleteAsync(long userId, DateOnly date);
}
