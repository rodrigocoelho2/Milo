namespace BodyOps.WebApi.Features.Watch.Models.Entities;

/// <summary>
/// One row of dbo.WatchKey (without the hash). Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class WatchKeyRow
{
    /// <summary>Gets or sets the account.</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the start and end of the key ("milo_hk_7Q2m…3f9a").</summary>
    public string KeyHint { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was made (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when the Shortcut last used it (UTC).</summary>
    public DateTime? LastUsedAt { get; set; }

    /// <summary>Gets or sets what was wrong with the last sync, or empty.</summary>
    public string LastError { get; set; } = string.Empty;
}

/// <summary>
/// One row of dbo.WatchWorkout. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class WatchWorkoutRow
{
    /// <summary>Gets or sets the identifier.</summary>
    public long WatchWorkoutID { get; set; }

    /// <summary>Gets or sets the account.</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the day it counts for (the day it started).</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets when it started (app time zone).</summary>
    public DateTime StartedAt { get; set; }

    /// <summary>Gets or sets when it ended (app time zone).</summary>
    public DateTime EndedAt { get; set; }

    /// <summary>Gets or sets the Watch's workout type as sent.</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets how long it lasted.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Gets or sets the active calories.</summary>
    public decimal? ActiveKcal { get; set; }

    /// <summary>Gets or sets the average heart rate.</summary>
    public short? AvgBpm { get; set; }

    /// <summary>Gets or sets the maximum heart rate.</summary>
    public short? MaxBpm { get; set; }

    /// <summary>Gets or sets the distance.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>Gets or sets the heart-rate curve, comma-separated.</summary>
    public string HeartRates { get; set; } = string.Empty;

    /// <summary>Gets or sets the planned cardio it filled in.</summary>
    public long? PlanExerciseID { get; set; }

    /// <summary>Gets or sets when it arrived (UTC).</summary>
    public DateTime CreatedAt { get; set; }
}
