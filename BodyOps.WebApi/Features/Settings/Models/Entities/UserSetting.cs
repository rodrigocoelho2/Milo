namespace BodyOps.WebApi.Features.Settings.Models.Entities;

/// <summary>
/// One row of dbo.UserSetting. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class UserSetting
{
    /// <summary>Gets or sets the account.</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the body weight goal.</summary>
    public decimal? WeightGoalKg { get; set; }

    /// <summary>Gets or sets the rest between sets in seconds, or null for the default.</summary>
    public int? RestSeconds { get; set; }

    /// <summary>Gets or sets when it was last saved (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
