namespace BodyOps.WebApi.Features.Weights.Models.DTOs;

/// <summary>
/// Every weigh-in with the goal.
/// </summary>
public sealed class WeightsDTO
{
    /// <summary>Gets or sets the weigh-ins, oldest first.</summary>
    public List<WeightEntryDTO> Entries { get; set; } = [];

    /// <summary>Gets or sets the goal (null = none).</summary>
    public decimal? GoalKg { get; set; }
}

/// <summary>
/// One weigh-in.
/// </summary>
public sealed class WeightEntryDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the weight.</summary>
    public decimal WeightKg { get; set; }
}

/// <summary>
/// Record a weigh-in.
/// </summary>
public sealed class WeightSaveDTO
{
    /// <summary>Gets or sets the weight.</summary>
    public decimal WeightKg { get; set; }
}
