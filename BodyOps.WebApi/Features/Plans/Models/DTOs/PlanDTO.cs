namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// The whole weekly plan: always seven days, Monday first.
/// </summary>
public sealed class PlanDTO
{
    /// <summary>Gets or sets the days.</summary>
    public List<PlanDayDTO> Days { get; set; } = [];
}
