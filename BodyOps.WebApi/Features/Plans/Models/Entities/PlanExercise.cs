using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// One row of dbo.PlanExercise. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class PlanExercise
{
    /// <summary>Gets or sets the exercise identifier.</summary>
    public long PlanExerciseID { get; set; }

    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType ExerciseType { get; set; }

    /// <summary>Gets or sets the name ("Pec deck").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan as typed ("3x12", "20 km").</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets free notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles picked by hand (comma-separated numbers), or null to detect them from the name.</summary>
    public string? Muscles { get; set; }

    /// <summary>Gets or sets the kind of cardio picked by hand, or null to work it out from the name (cardio only).</summary>
    public CardioKind? CardioKind { get; set; }

    /// <summary>Gets or sets the position within the day.</summary>
    public int SortOrder { get; set; }
}
