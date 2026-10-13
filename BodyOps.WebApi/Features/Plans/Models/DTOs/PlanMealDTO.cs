using BodyOps.WebApi.Features.Plans.Models.Entities;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// A planned meal with its foods.
/// </summary>
public sealed class PlanMealDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PlanMealDTO()
    {
    }

    /// <summary>
    /// Maps a meal and its foods.
    /// </summary>
    /// <param name="entity">The meal.</param>
    /// <param name="foods">Its foods, in order.</param>
    public PlanMealDTO(PlanMeal entity, IEnumerable<PlanFood> foods)
    {
        Id = entity.PlanMealID;
        Name = entity.Name;
        Foods = foods.Select(static food => new PlanFoodDTO(food)).ToList();
    }

    /// <summary>Gets or sets the meal identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the foods, in order.</summary>
    public List<PlanFoodDTO> Foods { get; set; } = [];
}
