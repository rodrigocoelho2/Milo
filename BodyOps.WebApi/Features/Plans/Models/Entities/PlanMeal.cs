namespace BodyOps.WebApi.Features.Plans.Models.Entities;

/// <summary>
/// One row of dbo.PlanMeal. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class PlanMeal
{
    /// <summary>Gets or sets the meal identifier.</summary>
    public long PlanMealID { get; set; }

    /// <summary>Gets or sets the weekday (1 = Monday … 7 = Sunday).</summary>
    public int Weekday { get; set; }

    /// <summary>Gets or sets the name ("Breakfast").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the position within the day.</summary>
    public int SortOrder { get; set; }

    /// <summary>Gets or sets when the meal is usually eaten, or null.</summary>
    public TimeSpan? MealTime { get; set; }
}
