using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Plans.Models.Entities;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// A food of a planned meal.
/// </summary>
public sealed class PlanFoodDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PlanFoodDTO()
    {
    }

    /// <summary>
    /// Maps a food.
    /// </summary>
    /// <param name="entity">The food.</param>
    public PlanFoodDTO(PlanFood entity)
    {
        Id = entity.PlanFoodID;
        Name = entity.Name;
        Amount = entity.Amount;
        Unit = entity.Unit;
    }

    /// <summary>Gets or sets the food identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the unit ("grams" or "pieces").</summary>
    public FoodUnit Unit { get; set; } = FoodUnit.Grams;
}
