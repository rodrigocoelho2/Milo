namespace BodyOps.Library.Enums;

/// <summary>
/// The kind of a cardio exercise. Stored as TINYINT in dbo.PlanExercise / dbo.DayExercise (NULL = work it out from
/// the name). Some kinds are logged by time only; the others by time and distance.
/// </summary>
public enum CardioKind
{
    /// <summary>Stationary or road bike: time and distance.</summary>
    Bike = 1,

    /// <summary>Treadmill: time only.</summary>
    Treadmill = 2,

    /// <summary>Running outdoors: time and distance.</summary>
    Run = 3,

    /// <summary>Walking: time and distance.</summary>
    Walk = 4,

    /// <summary>Elliptical / cross trainer: time only.</summary>
    Elliptical = 5,

    /// <summary>Rowing machine: time and distance.</summary>
    Rowing = 6,

    /// <summary>Stair climber: time only.</summary>
    Stairs = 7,

    /// <summary>Swimming: time and distance.</summary>
    Swim = 8,

    /// <summary>Anything else: time, and distance if it has one.</summary>
    Other = 9,
}
