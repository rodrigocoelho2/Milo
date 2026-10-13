namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// The name of a weekday's session ("Upper body"). Empty removes it.
/// </summary>
public sealed class PlanDayNameDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }
}
