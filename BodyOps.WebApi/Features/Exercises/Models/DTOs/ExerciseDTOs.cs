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

    /// <summary>Gets or sets the sessions, oldest first.</summary>
    public List<ExerciseSessionDTO> Sessions { get; set; } = [];
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

    /// <summary>Gets or sets reps × load (null without either).</summary>
    public decimal? VolumeKg { get; set; }
}
