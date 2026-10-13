namespace BodyOps.WebApi.Features.Settings.Models.DTOs;

/// <summary>
/// The account's settings.
/// </summary>
public sealed class SettingsDTO
{
    /// <summary>Gets or sets the body weight goal (null = none).</summary>
    public decimal? WeightGoalKg { get; set; }
}
