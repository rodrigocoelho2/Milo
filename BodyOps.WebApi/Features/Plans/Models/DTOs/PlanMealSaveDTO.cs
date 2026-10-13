using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Models.DTOs;

/// <summary>
/// A meal as sent by the plan editor. Without an id (or with one that is not on that day) it is a new meal.
/// </summary>
public sealed class PlanMealSaveDTO
{
    /// <summary>Gets or sets the meal identifier, or null for a new meal.</summary>
    public long? Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets when the meal is usually eaten ("08:00"), or null for no time.</summary>
    public string? Time { get; set; }

    /// <summary>Gets or sets the foods, in order.</summary>
    public List<PlanFoodSaveDTO>? Foods { get; set; }
}

/// <summary>
/// A food as sent by the plan editor.
/// </summary>
public sealed class PlanFoodSaveDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets the amount.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the unit.</summary>
    public FoodUnit Unit { get; set; } = FoodUnit.Grams;
}
