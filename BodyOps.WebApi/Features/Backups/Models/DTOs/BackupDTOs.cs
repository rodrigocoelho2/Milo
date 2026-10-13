using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Days.Models.DTOs;

namespace BodyOps.WebApi.Features.Backups.Models.DTOs;

/// <summary>
/// A whole account as a JSON file: settings, plan, logged days and weigh-ins.
/// </summary>
public sealed class BackupDTO
{
    /// <summary>The value of <see cref="Format"/> in every BodyOps backup.</summary>
    public const string FormatName = "bodyops-backup";

    /// <summary>Gets or sets the file format marker.</summary>
    public string Format { get; set; } = FormatName;

    /// <summary>Gets or sets the format version.</summary>
    public int Version { get; set; } = 1;

    /// <summary>Gets or sets when the file was made (UTC).</summary>
    public DateTime ExportedAt { get; set; }

    /// <summary>Gets or sets the settings.</summary>
    public BackupSettingsDTO Settings { get; set; } = new();

    /// <summary>Gets or sets the weekly plan, one entry per weekday that has something.</summary>
    public List<BackupPlanDayDTO> Plan { get; set; } = [];

    /// <summary>Gets or sets the logged days, oldest first.</summary>
    public List<DayBackupDTO> Days { get; set; } = [];

    /// <summary>Gets or sets the weigh-ins, oldest first.</summary>
    public List<BackupWeightDTO> Weights { get; set; } = [];
}

/// <summary>
/// The settings in a backup.
/// </summary>
public sealed class BackupSettingsDTO
{
    /// <summary>Gets or sets the weight goal.</summary>
    public decimal? WeightGoalKg { get; set; }
}

/// <summary>
/// One weekday of the plan in a backup. The ids only link the logged days to the plan inside the file.
/// </summary>
public sealed class BackupPlanDayDTO
{
    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets the meals.</summary>
    public List<BackupMealDTO> Meals { get; set; } = [];

    /// <summary>Gets or sets the exercises.</summary>
    public List<BackupExerciseDTO> Exercises { get; set; } = [];
}

/// <summary>
/// A planned meal in a backup.
/// </summary>
public sealed class BackupMealDTO
{
    /// <summary>Gets or sets the id within the file.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the foods.</summary>
    public List<BackupFoodDTO> Foods { get; set; } = [];
}

/// <summary>
/// A food in a backup.
/// </summary>
public sealed class BackupFoodDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the unit.</summary>
    public FoodUnit Unit { get; set; } = FoodUnit.Grams;
}

/// <summary>
/// A planned exercise in a backup.
/// </summary>
public sealed class BackupExerciseDTO
{
    /// <summary>Gets or sets the id within the file.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType Type { get; set; } = ExerciseType.Strength;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan text.</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets the notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles picked by hand, or null to detect them.</summary>
    public List<Muscle>? Muscles { get; set; }
}

/// <summary>
/// A weigh-in in a backup.
/// </summary>
public sealed class BackupWeightDTO
{
    /// <summary>Gets or sets the date.</summary>
    public DateOnly Date { get; set; }

    /// <summary>Gets or sets the weight.</summary>
    public decimal WeightKg { get; set; }
}

/// <summary>
/// What an import brought in.
/// </summary>
public sealed class BackupImportResultDTO
{
    /// <summary>Gets or sets which kind of file it was ("bodyops" or "corpoops").</summary>
    public string Source { get; set; } = "bodyops";

    /// <summary>Gets or sets the planned meals.</summary>
    public int Meals { get; set; }

    /// <summary>Gets or sets the planned exercises.</summary>
    public int Exercises { get; set; }

    /// <summary>Gets or sets the logged days.</summary>
    public int Days { get; set; }

    /// <summary>Gets or sets the weigh-ins.</summary>
    public int Weights { get; set; }
}
