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

    /// <summary>Gets or sets how often the account weighs in, in days (null = every day).</summary>
    public int? WeighEveryDays { get; set; }

    /// <summary>Gets or sets whether meal reminders are on (null = on).</summary>
    public bool? RemindMeals { get; set; }

    /// <summary>Gets or sets whether the workout reminder is on (null = on).</summary>
    public bool? RemindWorkout { get; set; }

    /// <summary>Gets or sets when the workout reminder comes (null = 18:00).</summary>
    public TimeSpan? WorkoutTime { get; set; }

    /// <summary>Gets or sets whether the weigh-in reminder is on (null = on).</summary>
    public bool? RemindWeighIn { get; set; }

    /// <summary>Gets or sets when the weigh-in reminder comes on the due day (null = 08:00).</summary>
    public TimeSpan? WeighInTime { get; set; }

    /// <summary>Gets or sets whether the streak-at-risk reminder is on (null = on).</summary>
    public bool? RemindStreak { get; set; }

    /// <summary>Gets or sets when the streak-at-risk reminder comes (null = 21:00).</summary>
    public TimeSpan? StreakTime { get; set; }

    /// <summary>Gets or sets when it was last saved (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
