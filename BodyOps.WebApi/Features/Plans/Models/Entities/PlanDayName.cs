namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// One row of dbo.PlanDay. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class PlanDayName
{
    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets the name of the session ("Upper body").</summary>
    public string Name { get; set; } = string.Empty;
}
