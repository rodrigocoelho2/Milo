using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Foods.Models.Entities;

namespace BodyOps.WebApi.Features.Foods.Models.DTOs;

/// <summary>
/// One of the account's foods.
/// </summary>
public sealed class FoodDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public FoodDTO()
    {
    }

    /// <summary>
    /// Maps a food.
    /// </summary>
    /// <param name="food">The food.</param>
    public FoodDTO(Food food)
    {
        Id = food.FoodID;
        Name = food.Name;
        Basis = food.Basis;
        PieceGrams = food.PieceGrams;
        PieceName = food.PieceName;
        Kcal = food.Kcal;
        ProteinG = food.ProteinG;
        CarbsG = food.CarbsG;
        FatG = food.FatG;
        Source = food.Source;
        SourceRef = food.SourceRef;
    }

    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets what the values are for (per 100 g or per piece).</summary>
    public FoodBasis Basis { get; set; } = FoodBasis.Per100g;

    /// <summary>Gets or sets the weight of one piece, when known.</summary>
    public decimal? PieceGrams { get; set; }

    /// <summary>Gets or sets what one piece is called.</summary>
    public string? PieceName { get; set; }

    /// <summary>Gets or sets the calories.</summary>
    public decimal? Kcal { get; set; }

    /// <summary>Gets or sets the protein (g).</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gets or sets the carbohydrates (g).</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gets or sets the fat (g).</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gets or sets where the values came from.</summary>
    public FoodSource Source { get; set; } = FoodSource.Own;

    /// <summary>Gets or sets the id in the source (Milo list id or barcode).</summary>
    public string? SourceRef { get; set; }
}

/// <summary>
/// A food to add or change. Calories and protein are what Milo needs; carbs and fat are optional.
/// </summary>
public sealed class FoodSaveDTO
{
    /// <summary>Gets or sets the name.</summary>
    public string? Name { get; set; }

    /// <summary>Gets or sets what the values are for.</summary>
    public FoodBasis Basis { get; set; } = FoodBasis.Per100g;

    /// <summary>Gets or sets the weight of one piece (optional).</summary>
    public decimal? PieceGrams { get; set; }

    /// <summary>Gets or sets what one piece is called (optional).</summary>
    public string? PieceName { get; set; }

    /// <summary>Gets or sets the calories.</summary>
    public decimal? Kcal { get; set; }

    /// <summary>Gets or sets the protein (g).</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gets or sets the carbohydrates (g).</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gets or sets the fat (g).</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gets or sets where the values came from.</summary>
    public FoodSource Source { get; set; } = FoodSource.Own;

    /// <summary>Gets or sets the id in the source.</summary>
    public string? SourceRef { get; set; }
}

/// <summary>
/// A food found in Milo's list or on Open Food Facts, not yet one of the account's foods.
/// </summary>
public sealed class FoodHitDTO
{
    /// <summary>Gets or sets where it was found.</summary>
    public FoodSource Source { get; set; }

    /// <summary>Gets or sets the id in the source (Milo list id or barcode).</summary>
    public string SourceRef { get; set; } = string.Empty;

    /// <summary>Gets or sets the name to show (and to save the food under).</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets a second line: the other language's name, or the brand and pack size.</summary>
    public string? Detail { get; set; }

    /// <summary>Gets or sets calories per 100 g.</summary>
    public decimal? Kcal { get; set; }

    /// <summary>Gets or sets protein per 100 g.</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gets or sets carbohydrates per 100 g.</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gets or sets fat per 100 g.</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gets or sets the weight of one piece or serving, when known.</summary>
    public decimal? PieceGrams { get; set; }

    /// <summary>Gets or sets what one piece is called.</summary>
    public string? PieceName { get; set; }
}

/// <summary>
/// What a search found: the account's own foods first, then Milo's list.
/// </summary>
public sealed class FoodSearchDTO
{
    /// <summary>Gets or sets the account's foods that match.</summary>
    public List<FoodDTO> Mine { get; set; } = [];

    /// <summary>Gets or sets the foods of Milo's list that match (and aren't already the account's).</summary>
    public List<FoodHitDTO> Milo { get; set; } = [];
}

/// <summary>
/// Calories and macros of a meal, a day or a food amount. Each total adds up what is known; <see cref="Unknown"/>
/// counts the foods without values.
/// </summary>
public sealed class MacrosDTO
{
    /// <summary>Gets or sets the calories.</summary>
    public decimal? Kcal { get; set; }

    /// <summary>Gets or sets the protein (g).</summary>
    public decimal? ProteinG { get; set; }

    /// <summary>Gets or sets the carbohydrates (g).</summary>
    public decimal? CarbsG { get; set; }

    /// <summary>Gets or sets the fat (g).</summary>
    public decimal? FatG { get; set; }

    /// <summary>Gets or sets how many foods had no values (or no way to convert the amount).</summary>
    public int Unknown { get; set; }
}
