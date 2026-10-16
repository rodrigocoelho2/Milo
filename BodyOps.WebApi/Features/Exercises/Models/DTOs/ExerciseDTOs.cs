using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;

namespace BodyOps.WebApi.Features.Exercises.Models.DTOs;

/// <summary>
/// An exercise that is in the plan or has been logged, for the progression picker.
/// </summary>
public sealed class ExerciseSummaryDTO
{
    /// <summary>Gets or sets the name (as most recently written).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets how many sessions were logged with a load or sets.</summary>
    public int Sessions { get; set; }

    /// <summary>Gets or sets the heaviest load logged.</summary>
    public decimal? BestKg { get; set; }

    /// <summary>Gets or sets the most recent session.</summary>
    public DateOnly? LastDate { get; set; }

    /// <summary>Gets or sets when <see cref="BestKg"/> was first lifted.</summary>
    public DateOnly? BestDate { get; set; }

    /// <summary>Gets or sets the loads of the last sessions that had one (at most 12), oldest first.</summary>
    public List<ExercisePointDTO> Trend { get; set; } = [];

    /// <summary>Gets or sets the best estimated one-rep max.</summary>
    public decimal? BestE1rmKg { get; set; }

    /// <summary>Gets or sets the set behind <see cref="BestE1rmKg"/>: its load.</summary>
    public decimal? BestE1rmLoadKg { get; set; }

    /// <summary>Gets or sets the set behind <see cref="BestE1rmKg"/>: its reps.</summary>
    public int? BestE1rmReps { get; set; }

    /// <summary>Gets or sets when <see cref="BestE1rmKg"/> was first reached.</summary>
    public DateOnly? BestE1rmDate { get; set; }
}

/// <summary>
/// Every session of one exercise, for the progression chart.
/// </summary>
public sealed class ExerciseHistoryDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the heaviest load logged.</summary>
    public decimal? BestKg { get; set; }

    /// <summary>Gets or sets the best estimated one-rep max.</summary>
    public decimal? BestE1rmKg { get; set; }

    /// <summary>Gets or sets the sessions, oldest first.</summary>
    public List<ExerciseSessionDTO> Sessions { get; set; } = [];

    /// <summary>Gets or sets the heaviest load for at least 3, 5, 8, 10, 12 and 15 reps, with the day each was first done.</summary>
    public List<RepBestDTO> RepRecords { get; set; } = [];
}

/// <summary>
/// One logged session of an exercise.
/// </summary>
public sealed class ExerciseSessionDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the load.</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set done.</summary>
    public List<int> Reps { get; set; } = [];

    /// <summary>Gets or sets the load of each set done, same length as <see cref="Reps"/>.</summary>
    public List<decimal?> Loads { get; set; } = [];

    /// <summary>Gets or sets reps × load (null without either).</summary>
    public decimal? VolumeKg { get; set; }

    /// <summary>Gets or sets the best estimated one-rep max of the session's sets.</summary>
    public decimal? E1rmKg { get; set; }
}

/// <summary>
/// One session's load, for a trend line.
/// </summary>
public sealed class ExercisePointDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the load.</summary>
    public decimal LoadKg { get; set; }

    /// <summary>Gets or sets the session's best estimated one-rep max (null when no set qualifies).</summary>
    public decimal? E1rmKg { get; set; }
}

/// <summary>
/// A personal record: a session that beat the heaviest load, a rep-count best or the estimated 1RM of that exercise.
/// One per exercise and session, the strongest kind first (load, then reps, then estimated 1RM).
/// </summary>
public sealed class RecordDTO
{
    /// <summary>Gets or sets what the record beat.</summary>
    public RecordKind Kind { get; set; } = RecordKind.Load;

    /// <summary>Gets or sets the rep count of a <see cref="RecordKind.Reps"/> record.</summary>
    public int? Reps { get; set; }

    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the exercise name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the new best (the load, or the estimated 1RM for <see cref="RecordKind.E1rm"/>).</summary>
    public decimal LoadKg { get; set; }

    /// <summary>Gets or sets the best load before it.</summary>
    public decimal PreviousKg { get; set; }
}
