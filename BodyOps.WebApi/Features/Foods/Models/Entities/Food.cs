using BodyOps.Library.Enums;

namespace BodyOps.WebApi.Features.Foods.Models.Entities;

/// <summary>
/// One row of dbo.Food. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class Food
{
    /// <summary>Gets or sets the food identifier.</summary>
    public long FoodID { get; set; }

    /// <summary>Gets or sets the name ("Oats").</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the name as matched against the plan (cleaned, lower case).</summary>
    public string NameKey { get; set; } = string.Empty;

    /// <summary>Gets or sets what the values are for.</summary>
    public FoodBasis Basis { get; set; } = FoodBasis.Per100g;

    /// <summary>Gets or sets the weight of one piece or serving, when known.</summary>
    public decimal? PieceGrams { get; set; }

    /// <summary>Gets or sets what one piece is called ("slice", "scoop").</summary>
    public string? PieceName { get; set; }

    /// <summary>Gets or sets the calories.</summary>
    public decimal? Kcal { get; set; }

    /// <summary>Gets or sets the protein in grams.</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gets or sets the carbohydrates in grams.</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gets or sets the fat in grams.</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gets or sets where the values came from.</summary>
    public FoodSource Source { get; set; } = FoodSource.Own;

    /// <summary>Gets or sets the id in the source (the Milo list id, or the barcode).</summary>
    public string? SourceRef { get; set; }

    /// <summary>Gets or sets when it last changed (UTC).</summary>
    public DateTime UpdatedAt { get; set; }
}
