namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// The result sets of dbo.PlanGet.
/// </summary>
/// <param name="Meals">The meals.</param>
/// <param name="Foods">The foods of those meals.</param>
/// <param name="Exercises">The exercises.</param>
public sealed record PlanData(List<PlanMeal> Meals, List<PlanFood> Foods, List<PlanExercise> Exercises)
{
    /// <summary>Gets the names of the weekdays' sessions (weekdays without a name have no row).</summary>
    public List<PlanDayName> Names { get; init; } = [];

    /// <summary>Gets an empty plan.</summary>
    public static PlanData Empty => new([], [], []);
}
