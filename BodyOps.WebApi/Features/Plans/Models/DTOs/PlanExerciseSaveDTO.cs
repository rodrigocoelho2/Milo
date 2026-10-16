using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// An exercise as sent by the plan editor. Without an id (or with one that is not on that day) it is a new exercise.
/// </summary>
public sealed class PlanExerciseSaveDTO
{
    /// <summary>Gets or sets the exercise identifier, or null for a new exercise.</summary>
    public long? Id { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType Type { get; set; } = ExerciseType.Strength;

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the plan as typed ("3x12").</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets free notes.</summary>
    public string? Notes { get; set; }

    /// <summary>Gets or sets the muscles picked by hand, or null to detect them from the name.</summary>
    public List<Muscle>? Muscles { get; set; }

    /// <summary>Gets or sets the kind of cardio picked by hand, or null to work it out from the name (cardio only).</summary>
    public CardioKind? CardioKind { get; set; }
}
