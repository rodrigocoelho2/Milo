using System.Globalization;
using BodyOps.WebApi.Features.Watch.Models.Entities;
using BodyOps.WebApi.Features.Watch.Services;

namespace BodyOps.WebApi.Features.Watch.Models.DTOs;

/// <summary>
/// Apple Watch and Apple Health for the signed-in account: the Shortcut key and the recent workouts.
/// </summary>
public sealed class WatchDTO
{
    /// <summary>Gets or sets a value indicating whether the account has a key.</summary>
    public bool Connected { get; set; }

    /// <summary>Gets or sets the start and end of the key.</summary>
    public string? KeyHint { get; set; }

    /// <summary>Gets or sets when the key was made (UTC).</summary>
    public DateTime? KeyCreatedAt { get; set; }

    /// <summary>Gets or sets when the Shortcut last sent something (UTC).</summary>
    public DateTime? LastSyncAt { get; set; }

    /// <summary>Gets or sets what was wrong with the last sync, or empty.</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>Gets or sets the address the Shortcut sends to.</summary>
    public string IngestPath { get; set; } = "/api/watch/ingest";

    /// <summary>Gets or sets the workouts of the last weeks, oldest first.</summary>
    public List<WatchWorkoutDTO> Workouts { get; set; } = [];
}

/// <summary>
/// A new key: shown once, in full.
/// </summary>
public sealed class WatchKeyDTO
{
    /// <summary>Gets or sets the key (only now: Milo keeps its hash).</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the state with the new key.</summary>
    public WatchDTO Watch { get; set; } = new();
}

/// <summary>
/// One Watch workout.
/// </summary>
public sealed class WatchWorkoutDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public WatchWorkoutDTO()
    {
    }

    /// <summary>
    /// Maps a row.
    /// </summary>
    /// <param name="row">The row.</param>
    public WatchWorkoutDTO(WatchWorkoutRow row)
    {
        Id = row.WatchWorkoutID;
        Date = DateOnly.FromDateTime(row.DayDate);
        StartedAt = row.StartedAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        EndedAt = row.EndedAt.ToString("yyyy-MM-dd'T'HH:mm", CultureInfo.InvariantCulture);
        Kind = row.Kind;
        Category = WatchPayload.Category(row.Kind) switch
        {
            WatchCategory.Strength => "strength",
            WatchCategory.Cardio => "cardio",
            _ => "other",
        };
        DurationSeconds = row.DurationSeconds;
        ActiveKcal = row.ActiveKcal;
        AvgBpm = row.AvgBpm;
        MaxBpm = row.MaxBpm;
        DistanceKm = row.DistanceKm;
        HeartRates = [.. row.HeartRates.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(static value => int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var bpm) ? bpm : 0).Where(static bpm => bpm > 0)];
        PlanExerciseId = row.PlanExerciseID;
    }

    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the day it counts for.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets when it started ("2026-10-16T18:14", app time zone).</summary>
    public string StartedAt { get; set; } = string.Empty;

    /// <summary>Gets or sets when it ended.</summary>
    public string EndedAt { get; set; } = string.Empty;

    /// <summary>Gets or sets the Watch's workout type ("Traditional Strength Training").</summary>
    public string Kind { get; set; } = string.Empty;

    /// <summary>Gets or sets "strength", "cardio" or "other".</summary>
    public string Category { get; set; } = "other";

    /// <summary>Gets or sets how long it lasted.</summary>
    public int DurationSeconds { get; set; }

    /// <summary>Gets or sets the active calories.</summary>
    public decimal? ActiveKcal { get; set; }

    /// <summary>Gets or sets the average heart rate.</summary>
    public int? AvgBpm { get; set; }

    /// <summary>Gets or sets the maximum heart rate.</summary>
    public int? MaxBpm { get; set; }

    /// <summary>Gets or sets the distance.</summary>
    public decimal? DistanceKm { get; set; }

    /// <summary>Gets or sets the heart-rate curve (up to 60 points).</summary>
    public List<int> HeartRates { get; set; } = [];

    /// <summary>Gets or sets the planned cardio it filled in.</summary>
    public long? PlanExerciseId { get; set; }
}

/// <summary>
/// What an import did, in words the Shortcut can show.
/// </summary>
public sealed class WatchIngestResultDTO
{
    /// <summary>Gets or sets the summary ("Milo got Traditional Strength Training: 58 min, 412 kcal, 128 bpm avg.").</summary>
    public string Message { get; set; } = string.Empty;

    /// <summary>Gets or sets the (first) workout saved, or null.</summary>
    public WatchWorkoutDTO? Workout { get; set; }

    /// <summary>Gets or sets every workout saved.</summary>
    public List<WatchWorkoutDTO> Workouts { get; set; } = [];

    /// <summary>Gets or sets the planned cardio filled in ("Treadmill, 22 min"), or null.</summary>
    public string? FilledCardio { get; set; }

    /// <summary>Gets or sets the weight saved, or null.</summary>
    public decimal? WeightKg { get; set; }
}
