using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// One row of dbo.PlanFood. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class PlanFood
{
    /// <summary>Gets or sets the food identifier.</summary>
    public long PlanFoodID { get; set; }

    /// <summary>Gets or sets the meal it belongs to.</summary>
    public long PlanMealID { get; set; }

    /// <summary>Gets or sets the name ("Whey").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the amount, in <see cref="Unit"/>.</summary>
    public decimal Amount { get; set; }

    /// <summary>Gets or sets the unit (grams or pieces).</summary>
    public FoodUnit Unit { get; set; }

    /// <summary>Gets or sets the position within the meal.</summary>
    public int SortOrder { get; set; }
}
