using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Days.Models.DTOs;

/// <summary>
/// One week (Monday to Sunday) as the Today and Week screens show it.
/// </summary>
public sealed class WeekDTO
{
    /// <summary>Gets or sets the Monday.</summary>
    public DateOnly From { get; set; }

    /// <summary>Gets or sets the Sunday.</summary>
    public DateOnly To { get; set; }

    /// <summary>Gets or sets today's date on the server's clock.</summary>
    public DateOnly Today { get; set; }

    /// <summary>Gets or sets the seven days.</summary>
    public List<DayDTO> Days { get; set; } = [];

    /// <summary>Gets or sets the week's totals.</summary>
    public WeekTotalsDTO Totals { get; set; } = new();
}

/// <summary>
/// One day: its meals and exercises (from the log when something was logged, otherwise from the plan).
/// </summary>
public sealed class DayDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets a value indicating whether something was logged that day (its plan is then frozen once the week is over).</summary>
    public bool Logged { get; set; }

    /// <summary>Gets or sets whether the session happened.</summary>
    public TrainingState TrainingState { get; set; }

    /// <summary>Gets or sets the name of the day's session in the plan ("Upper body"), or empty.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the weigh-in of that day, if any.</summary>
    public decimal? WeightKg { get; set; }

    /// <summary>Gets or sets the planned sets (strength sets, one per cardio exercise).</summary>
    public int SetsPlanned { get; set; }

    /// <summary>Gets or sets how many of the planned sets were done.</summary>
    public int SetsDone { get; set; }

    /// <summary>Gets or sets the day score, 0–100: training and meals (see <c>WeekBuilder.DayScore</c>).</summary>
    public int Score { get; set; }

    /// <summary>Gets or sets the meals.</summary>
    public List<DayMealDTO> Meals { get; set; } = [];

    /// <summary>Gets or sets the exercises.</summary>
    public List<DayExerciseDTO> Exercises { get; set; } = [];
}

/// <summary>
/// A meal of a day.
/// </summary>
public sealed class DayMealDTO
{
    /// <summary>Gets or sets the planned meal (the key used to tick it).</summary>
    public long PlanMealId { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the foods on one line ("Whey 25 g · Banana 1 pc").</summary>
    public string Foods { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether it was eaten.</summary>
    public bool Eaten { get; set; }

    /// <summary>Gets or sets when it is planned ("08:00"), or null.</summary>
    public string? Time { get; set; }
}

/// <summary>
/// An exercise of a day, with what was done and how it compares with earlier sessions.
/// </summary>
public sealed class DayExerciseDTO
{
    /// <summary>Gets or sets the planned exercise (the key used to log it).</summary>
    public long PlanExerciseId { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType Type { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan text ("3x12").</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles it trains.</summary>
    public List<Muscle> Muscles { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether <see cref="Muscles"/> were detected from the name (false = picked by hand).</summary>
    public bool MusclesAuto { get; set; } = true;

    /// <summary>Gets or sets the rep target of each planned set (0 = none).</summary>
    public List<int> Targets { get; set; } = [];

    /// <summary>Gets or sets the load lifted: the heaviest set (what records compare).</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set (null = not done); as long as the plan or the log, whichever is longer.</summary>
    public List<int?> Sets { get; set; } = [];

    /// <summary>Gets or sets the load of each set, same length as <see cref="Sets"/> (null = not done or no load).</summary>
    public List<decimal?> SetLoads { get; set; } = [];

    /// <summary>Gets or sets the cardio minutes.</summary>
    public decimal? CardioMinutes { get; set; }

    /// <summary>Gets or sets the cardio distance.</summary>
    public decimal? CardioKm { get; set; }

    /// <summary>Gets or sets the kind of cardio (null for strength).</summary>
    public CardioKind? CardioKind { get; set; }

    /// <summary>Gets or sets a value indicating whether this cardio is logged with a distance as well as the time.</summary>
    public bool CardioDistance { get; set; }

    /// <summary>Gets or sets reps × load over the sets done (null without a load or sets).</summary>
    public decimal? VolumeKg { get; set; }

    /// <summary>Gets or sets the most recent earlier session of the same exercise.</summary>
    public LastSessionDTO? Last { get; set; }

    /// <summary>Gets or sets the heaviest load in earlier sessions.</summary>
    public decimal? BestBeforeKg { get; set; }

    /// <summary>Gets or sets a value indicating whether this load beats every earlier session (a personal record).</summary>
    public bool IsPr { get; set; }
}

/// <summary>
/// An earlier session of an exercise.
/// </summary>
public sealed class LastSessionDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the load (the heaviest set).</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set done.</summary>
    public List<int> Reps { get; set; } = [];

    /// <summary>Gets or sets the load of each set done, same length as <see cref="Reps"/>.</summary>
    public List<decimal?> Loads { get; set; } = [];
}

/// <summary>
/// A week's totals.
/// </summary>
public sealed class WeekTotalsDTO
{
    /// <summary>Gets or sets the meals eaten.</summary>
    public int MealsEaten { get; set; }

    /// <summary>Gets or sets the meals planned.</summary>
    public int MealsPlanned { get; set; }

    /// <summary>Gets or sets the days marked as trained.</summary>
    public int TrainedDays { get; set; }

    /// <summary>Gets or sets the days marked as skipped.</summary>
    public int MissedDays { get; set; }

    /// <summary>Gets or sets the days with at least one exercise planned.</summary>
    public int TrainingDaysPlanned { get; set; }

    /// <summary>Gets or sets the sets logged.</summary>
    public int SetsLogged { get; set; }

    /// <summary>Gets or sets reps × load over the week.</summary>
    public decimal VolumeKg { get; set; }

    /// <summary>Gets or sets the sets per muscle on trained days, most first.</summary>
    public List<MuscleSetsDTO> Muscles { get; set; } = [];

    /// <summary>Gets or sets how many exercises counted as one set because neither the plan nor the log said how many.</summary>
    public int UnknownSetCount { get; set; }
}

/// <summary>
/// How many sets a muscle got.
/// </summary>
public sealed class MuscleSetsDTO
{
    /// <summary>Gets or sets the muscle.</summary>
    public Muscle Muscle { get; set; }

    /// <summary>Gets or sets the sets (secondary movers count partly, so this can be fractional).</summary>
    public double Sets { get; set; }
}

/// <summary>
/// How complete a day was, for the consistency grid.
/// </summary>
public sealed class ConsistencyDayDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets 0–1 (the share of meals eaten and the session done or skipped), or null when nothing was logged.</summary>
    public double? Score { get; set; }
}

/// <summary>
/// Tick or untick a meal (or every meal of a day).
/// </summary>
public sealed class MealEatenDTO
{
    /// <summary>Gets or sets a value indicating whether it was eaten.</summary>
    public bool Eaten { get; set; }
}

/// <summary>
/// Mark a day's session.
/// </summary>
public sealed class TrainingStateDTO
{
    /// <summary>Gets or sets the state.</summary>
    public TrainingState State { get; set; }
}

/// <summary>
/// What was done for one exercise.
/// </summary>
public sealed class ExerciseLogDTO
{
    /// <summary>Gets or sets the load (strength) for sets without one of their own in <see cref="SetLoads"/>.</summary>
    public decimal? LoadKg { get; set; }

    /// <summary>Gets or sets the reps of each set; null = set not done.</summary>
    public List<int?>? Sets { get; set; }

    /// <summary>Gets or sets the load of each set (progressive loading); null = the <see cref="LoadKg"/>.</summary>
    public List<decimal?>? SetLoads { get; set; }

    /// <summary>Gets or sets the minutes (cardio).</summary>
    public decimal? CardioMinutes { get; set; }

    /// <summary>Gets or sets the kilometres (cardio).</summary>
    public decimal? CardioKm { get; set; }
}

/// <summary>
/// The run of good days: days scoring at least <see cref="Threshold"/> in a row. Days with nothing planned neither
/// count nor break it.
/// </summary>
public sealed class StreakDTO
{
    /// <summary>Gets or sets the score a day needs to count.</summary>
    public int Threshold { get; set; }

    /// <summary>Gets or sets the run up to and including yesterday (today can still be added).</summary>
    public int ThroughYesterday { get; set; }

    /// <summary>Gets or sets today's score.</summary>
    public int TodayScore { get; set; }

    /// <summary>Gets or sets the current run, today included when it already counts.</summary>
    public int Current { get; set; }

    /// <summary>Gets or sets the longest run in the last year.</summary>
    public int Best { get; set; }
}
