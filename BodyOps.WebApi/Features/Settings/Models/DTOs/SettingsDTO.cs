namespace BodyOps.WebApi.Features.Settings.Models.DTOs;

/// <summary>
/// The account's settings. Reads always return every value (defaults filled in); a save without a value keeps the
/// current one, except the weight goal, where null removes it.
/// </summary>
public sealed class SettingsDTO
{
    /// <summary>Gets or sets the body weight goal (null = none).</summary>
    public decimal? WeightGoalKg { get; set; }

    /// <summary>Gets or sets the rest between sets, in seconds (15–900; 90 by default).</summary>
    public int? RestSeconds { get; set; }

    /// <summary>Gets or sets how often the account weighs in, in days (1–60; 1 = every day, the default).</summary>
    public int? WeighEveryDays { get; set; }

    /// <summary>Gets or sets which push reminders the account wants, and when.</summary>
    public ReminderSettingsDTO? Reminders { get; set; }
}

/// <summary>
/// The push reminders the account wants. Times are "HH:mm" in the server's time zone.
/// </summary>
public sealed class ReminderSettingsDTO
{
    /// <summary>Gets or sets whether each meal is reminded at its planned time.</summary>
    public bool? Meals { get; set; }

    /// <summary>Gets or sets whether training days get a workout reminder.</summary>
    public bool? Workout { get; set; }

    /// <summary>Gets or sets when the workout reminder comes ("18:00").</summary>
    public string? WorkoutTime { get; set; }

    /// <summary>Gets or sets whether the weigh-in day gets a reminder.</summary>
    public bool? WeighIn { get; set; }

    /// <summary>Gets or sets when the weigh-in reminder comes ("08:00").</summary>
    public string? WeighInTime { get; set; }

    /// <summary>Gets or sets whether a day still under the streak bar gets a reminder.</summary>
    public bool? Streak { get; set; }

    /// <summary>Gets or sets when the streak reminder comes ("21:00").</summary>
    public string? StreakTime { get; set; }
}
