namespace BodyOps.Library.Enums;

/// <summary>
/// Whether a day's session happened. Stored as TINYINT in dbo.Day.
/// </summary>
public enum TrainingState
{
    /// <summary>Not marked yet.</summary>
    Pending = 0,

    /// <summary>Trained.</summary>
    Done = 1,

    /// <summary>Skipped.</summary>
    Missed = 2,
}
