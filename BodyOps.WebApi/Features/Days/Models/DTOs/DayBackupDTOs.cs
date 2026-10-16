using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Days.Models.DTOs;

/// <summary>
/// A logged day as stored in a backup, or handed back by Reset week so it can be undone.
/// Property names match the JSON paths read by dbo.DayImport.
/// </summary>
public sealed class DayBackupDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets whether the session happened.</summary>
    public TrainingState TrainingState { get; set; }

    /// <summary>Gets or sets the meals.</summary>
    public List<DayMealBackupDTO> Meals { get; set; } = [];

    /// <summary>Gets or sets the exercises.</summary>
    public List<DayExerciseBackupDTO> Exercises { get; set; } = [];
}

/// <summary>
/// A meal of a logged day in a backup.
/// </summary>
public sealed class DayMealBackupDTO
{
    /// <summary>Gets or sets the planned meal it came from (its id in the backup's plan).</summary>
    public long PlanMealId { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the foods on one line.</summary>
    public string Foods { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether it was eaten.</summary>
    public bool Eaten { get; set; }

    /// <summary>Gets or sets when it was planned ("08:00"), or null.</summary>
    public string? Time { get; set; }
}

/// <summary>
/// An exercise of a logged day in a backup.
/// </summary>
public sealed class DayExerciseBackupDTO
{
    /// <summary>Gets or sets the planned exercise it came from (its id in the backup's plan).</summary>
    public long PlanExerciseId { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType Type { get; set; } = ExerciseType.Strength;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan text.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles picked by hand, or null to detect them.</summary>
    public List<Muscle>? Muscles { get; set; }

    /// <summary>Gets or sets the load.</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set (null = not done).</summary>
    public List<int?> Sets { get; set; } = [];

    /// <summary>Gets or sets the load of each set, same length as <see cref="Sets"/> (null = the <see cref="LoadKg"/>).</summary>
    public List<decimal?> SetLoads { get; set; } = [];

    /// <summary>Gets or sets the cardio minutes.</summary>
    public decimal? CardioMinutes { get; set; }

    /// <summary>Gets or sets the cardio kilometres.</summary>
    public decimal? CardioKm { get; set; }

    /// <summary>Gets or sets the kind of cardio picked in the plan, or null to work it out from the name.</summary>
    public CardioKind? CardioKind { get; set; }
}
