using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Days.Models.Entities;

/// <summary>
/// One row of dbo.Day. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class DayRow
{
    /// <summary>Gets or sets the day identifier.</summary>
    public long DayID { get; set; }

    /// <summary>Gets or sets the date (time part is always midnight).</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets whether the session happened.</summary>
    public TrainingState TrainingState { get; set; }
}

/// <summary>
/// One row of dbo.DayMeal.
/// </summary>
public sealed class DayMealRow
{
    /// <summary>Gets or sets the day.</summary>
    public long DayID { get; set; }

    /// <summary>Gets or sets the row identifier.</summary>
    public long DayMealID { get; set; }

    /// <summary>Gets or sets the planned meal it was copied from (negative for imported history).</summary>
    public long PlanMealID { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the foods on one line.</summary>
    public string Foods { get; set; } = string.Empty;

    /// <summary>Gets or sets the position within the day.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets a value indicating whether the meal was eaten.</summary>
    public bool Eaten { get; set; }

    /// <summary>Gets or sets when the meal is planned, or null.</summary>
    public TimeSpan? MealTime { get; set; }

    /// <summary>Gets or sets what was eaten instead (an off-plan meal), or null.</summary>
    public string? SwapName { get; set; }

    /// <summary>Gets or sets the calories of what was eaten instead.</summary>
    public decimal? SwapKcal { get; set; }

    /// <summary>Gets or sets the protein of what was eaten instead.</summary>
    public decimal? SwapProteinG { get; set; }
}

/// <summary>
/// One row of dbo.DayExercise.
/// </summary>
public sealed class DayExerciseRow
{
    /// <summary>Gets or sets the day.</summary>
    public long DayID { get; set; }

    /// <summary>Gets or sets the row identifier.</summary>
    public long DayExerciseID { get; set; }

    /// <summary>Gets or sets the planned exercise it was copied from (negative for imported history).</summary>
    public long PlanExerciseID { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType ExerciseType { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the name in lower case (links the exercise across days).</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan text ("3x12").</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles picked by hand, or null to detect them.</summary>
    public string? Muscles { get; set; }

    /// <summary>Gets or sets the position within the day.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets the load lifted.</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the cardio minutes.</summary>
    public decimal? CardioMinutes { get; set; }

    /// <summary>Gets or sets the cardio distance.</summary>
    public decimal? CardioKm { get; set; }

    /// <summary>Gets or sets the kind of cardio picked in the plan, or null to work it out from the name.</summary>
    public CardioKind? CardioKind { get; set; }
}

/// <summary>
/// One row of dbo.DaySet.
/// </summary>
public sealed class DaySetRow
{
    /// <summary>Gets or sets the exercise row.</summary>
    public long DayExerciseID { get; set; }

    /// <summary>Gets or sets the set number (1-based).</summary>
    public int SetNumber { get; set; }

    /// <summary>Gets or sets the reps done.</summary>
    public int Reps { get; set; }

    /// <summary>Gets or sets the load of this set, or null for the exercise's load (sets logged before per-set loads).</summary>
    public decimal? LoadKg { get; set; }
}

/// <summary>
/// The four result sets of dbo.DayListRange.
/// </summary>
/// <param name="Days">The logged days.</param>
/// <param name="Meals">Their meals.</param>
/// <param name="Exercises">Their exercises.</param>
/// <param name="Sets">The sets of those exercises.</param>
public sealed record DayRange(List<DayRow> Days, List<DayMealRow> Meals, List<DayExerciseRow> Exercises, List<DaySetRow> Sets)
{
    /// <summary>Gets an empty range.</summary>
    public static DayRange Empty => new([], [], [], []);
}

/// <summary>
/// One row of dbo.ExerciseLogList: a strength exercise logged on a day.
/// </summary>
public sealed class ExerciseLogRow
{
    /// <summary>Gets or sets the date.</summary>
    public DateTime DayDate { get; set; }

    /// <summary>Gets or sets the name in lower case.</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the name as written that day.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the load lifted (the heaviest set).</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set done, "12,11,10" (null when none were logged).</summary>
    public string? Reps { get; set; }

    /// <summary>Gets or sets the load of each set done, "60,65,70" (an empty item = the exercise's load).</summary>
    public string? Loads { get; set; }
}
