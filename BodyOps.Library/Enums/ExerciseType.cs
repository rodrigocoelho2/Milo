namespace BodyOps.Library.Enums;

/// <summary>
/// The kind of a planned exercise. Stored as TINYINT in dbo.PlanExercise / dbo.DayExercise.
/// </summary>
public enum ExerciseType
{
    /// <summary>Sets of reps with a load (logged per set).</summary>
    Strength = 1,

    /// <summary>Time and distance (cycling, running…).</summary>
    Cardio = 2,
}
