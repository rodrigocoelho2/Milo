namespace BodyOps.WebApi.Features.Settings.Models.DTOs;

/// <summary>
/// The account's settings.
/// </summary>
public sealed class SettingsDTO
{
    /// <summary>Gets or sets the body weight goal (null = none).</summary>
    public decimal? WeightGoalKg { get; set; }

    /// <summary>
    /// Gets or sets the rest between sets, in seconds (15–900). Reads always return a value (90 by default);
    /// a save without it keeps the current one.
    /// </summary>
    public int? RestSeconds { get; set; }
}
