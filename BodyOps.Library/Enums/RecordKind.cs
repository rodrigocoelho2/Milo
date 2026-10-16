namespace BodyOps.Library.Enums;

/// <summary>
/// What a personal record beat.
/// </summary>
public enum RecordKind
{
    /// <summary>The heaviest load ever lifted on that exercise.</summary>
    Load = 1,

    /// <summary>The heaviest load for at least a number of reps (3, 5, 8, 10, 12 or 15).</summary>
    Reps = 2,

    /// <summary>The best estimated one-rep max.</summary>
    E1rm = 3,
}
