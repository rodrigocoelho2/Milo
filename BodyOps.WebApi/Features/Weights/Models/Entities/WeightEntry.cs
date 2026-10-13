namespace BodyOps.WebApi.Features.Weights.Models.Entities;

/// <summary>
/// One row of dbo.WeightEntry. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class WeightEntry
{
    /// <summary>Gets or sets the date.</summary>
    public DateTime EntryDate { get; set; }

    /// <summary>Gets or sets the weight.</summary>
    public decimal WeightKg { get; set; }
}
