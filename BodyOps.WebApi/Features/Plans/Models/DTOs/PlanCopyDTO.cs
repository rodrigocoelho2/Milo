using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// Copy one day's meals or exercises onto other days (replacing what they had).
/// </summary>
public sealed class PlanCopyDTO
{
    /// <summary>Gets or sets what to copy.</summary>
    public PlanPart Part { get; set; } = PlanPart.Exercises;

    /// <summary>Gets or sets the weekdays to copy to (1 = Monday … 7 = Sunday).</summary>
    public List<int>? Targets { get; set; }
}
