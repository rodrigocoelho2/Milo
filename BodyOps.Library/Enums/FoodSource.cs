namespace BodyOps.Library.Enums;

/// <summary>
/// Where a food's values came from.
/// </summary>
public enum FoodSource
{
    /// <summary>Typed by the user (from the label).</summary>
    Own = 1,

    /// <summary>Milo's built-in list of basic foods (USDA values).</summary>
    Milo = 2,

    /// <summary>Open Food Facts, by name or barcode.</summary>
    OpenFoodFacts = 3,
}
