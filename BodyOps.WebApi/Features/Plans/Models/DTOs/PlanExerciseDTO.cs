using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Exercises.Services;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// A planned exercise, with the muscles it trains and its planned sets.
/// </summary>
public sealed class PlanExerciseDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PlanExerciseDTO()
    {
    }

    /// <summary>
    /// Maps an exercise.
    /// </summary>
    /// <param name="entity">The exercise.</param>
    public PlanExerciseDTO(PlanExercise entity)
    {
        Id = entity.PlanExerciseID;
        Type = entity.ExerciseType;
        Name = entity.Name;
        Detail = entity.Detail;
        Notes = entity.Notes;
        MusclesAuto = entity.Muscles is null;
        Muscles = MuscleDetector.Detect(entity.Name, entity.ExerciseType, entity.Muscles).Keys.ToList();
        Targets = entity.ExerciseType == ExerciseType.Strength ? ExerciseDetailParser.Targets(entity.Detail) : [];
        if (entity.ExerciseType == ExerciseType.Strength)
        {
            TargetsMin = ExerciseDetailParser.Mins(entity.Detail);
            ProgressStepKg = entity.ProgressStepKg;
            StepKg = Lifting.Step(ExerciseDetailParser.IsRange(entity.Detail), entity.ProgressStepKg);
        }

        if (entity.ExerciseType == ExerciseType.Cardio)
        {
            CardioKind = CardioKinds.Resolve(entity.Name, entity.CardioKind);
            CardioKindAuto = entity.CardioKind is null;
            CardioDistance = CardioKinds.UsesDistance(CardioKind.Value);
        }
    }

    /// <summary>Gets or sets the exercise identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets strength or cardio.</summary>
    public ExerciseType Type { get; set; } = ExerciseType.Strength;

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the plan as typed ("3x12").</summary>
    public string Detail { get; set; } = string.Empty;

    /// <summary>Gets or sets free notes.</summary>
    public string Notes { get; set; } = string.Empty;

    /// <summary>Gets or sets the muscles it trains.</summary>
    public List<Muscle> Muscles { get; set; } = [];

    /// <summary>Gets or sets a value indicating whether <see cref="Muscles"/> were detected from the name (false = picked by hand).</summary>
    public bool MusclesAuto { get; set; } = true;

    /// <summary>Gets or sets the rep target of each planned set (0 = no target); empty when the plan names no sets.</summary>
    public List<int> Targets { get; set; } = [];

    /// <summary>Gets or sets the lowest reps of each planned set (the bottom of a range; same as <see cref="Targets"/> for fixed reps).</summary>
    public List<int> TargetsMin { get; set; } = [];

    /// <summary>Gets or sets the step set on the exercise (null = the default).</summary>
    public decimal? ProgressStepKg { get; set; }

    /// <summary>Gets or sets the weight added once every set reaches the top of the range; null when the plan has no range.</summary>
    public decimal? StepKg { get; set; }

    /// <summary>Gets or sets the kind of cardio (null for strength).</summary>
    public CardioKind? CardioKind { get; set; }

    /// <summary>Gets or sets a value indicating whether <see cref="CardioKind"/> was worked out from the name (false = picked).</summary>
    public bool CardioKindAuto { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether this cardio is logged with a distance as well as the time.</summary>
    public bool CardioDistance { get; set; }
}
