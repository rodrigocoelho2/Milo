namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// The three result sets of dbo.PlanGet.
/// </summary>
/// <param name="Meals">The meals.</param>
/// <param name="Foods">The foods of those meals.</param>
/// <param name="Exercises">The exercises.</param>
public sealed record PlanData(List<PlanMeal> Meals, List<PlanFood> Foods, List<PlanExercise> Exercises)
{
    /// <summary>Gets an empty plan.</summary>
    public static PlanData Empty => new([], [], []);
}
