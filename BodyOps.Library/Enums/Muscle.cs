namespace BodyOps.Library.Enums;

/// <summary>
/// The muscle groups drawn on the body map. Stored as TINYINT in dbo.PlanExerciseMuscle.
/// </summary>
public enum Muscle
{
    /// <summary>Chest.</summary>
    Chest = 1,

    /// <summary>Shoulders (deltoids).</summary>
    Shoulders = 2,

    /// <summary>Biceps.</summary>
    Biceps = 3,

    /// <summary>Triceps.</summary>
    Triceps = 4,

    /// <summary>Forearms.</summary>
    Forearms = 5,

    /// <summary>Abs and obliques.</summary>
    Abs = 6,

    /// <summary>Lats (latissimus dorsi).</summary>
    Lats = 7,

    /// <summary>Traps.</summary>
    Traps = 8,

    /// <summary>Lower back.</summary>
    LowerBack = 9,

    /// <summary>Glutes.</summary>
    Glutes = 10,

    /// <summary>Quads.</summary>
    Quads = 11,

    /// <summary>Hamstrings.</summary>
    Hamstrings = 12,

    /// <summary>Calves.</summary>
    Calves = 13,
}
