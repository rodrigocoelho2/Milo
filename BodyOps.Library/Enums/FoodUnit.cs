namespace BodyOps.Library.Enums;

/// <summary>
/// How a food amount is measured. Stored as TINYINT in dbo.PlanFood.
/// </summary>
public enum FoodUnit
{
    /// <summary>Grams ("g").</summary>
    Grams = 1,

    /// <summary>Pieces ("pc"): eggs, bananas…</summary>
    Pieces = 2,
}
