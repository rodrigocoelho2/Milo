using BodyOps.WebApi.Features.Weights.Models.Entities;

namespace BodyOps.WebApi.Features.Weights.Repositories;

/// <summary>
/// Data access for weigh-ins (dbo.WeightEntry).
/// </summary>
internal interface IWeightRepository
{
    /// <summary>
    /// Every weigh-in (dbo.WeightList).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>Oldest first.</returns>
    Task<List<WeightEntry>> ListAsync(long userId);

    /// <summary>
    /// Records a weigh-in, replacing that date's (dbo.WeightSave).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <param name="weightKg">The weight.</param>
    /// <returns>A task that completes when saved.</returns>
    Task SaveAsync(long userId, DateOnly date, decimal weightKg);

    /// <summary>
    /// Removes a weigh-in (dbo.WeightDelete).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="date">The date.</param>
    /// <returns>True when one was removed.</returns>
    Task<bool> DeleteAsync(long userId, DateOnly date);

    /// <summary>
    /// Adds weigh-ins from a backup (dbo.WeightImport).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="entries">The weigh-ins.</param>
    /// <returns>A task that completes when saved.</returns>
    Task ImportAsync(long userId, IEnumerable<(DateOnly Date, decimal WeightKg)> entries);
}
