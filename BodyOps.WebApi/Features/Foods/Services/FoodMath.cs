using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using BodyOps.WebApi.Features.Foods.Models.Entities;

namespace BodyOps.WebApi.Features.Foods.Services;

/// <summary>
/// Calories and macros of food amounts. Pure functions, unit-tested; the page has the same rules (BO.macrosFor).
/// </summary>
internal static partial class FoodMath
{
    /// <summary>The largest calorie value a food can have.</summary>
    public const decimal MaxKcal = 10000;

    /// <summary>The largest gram value (protein, carbs, fat) a food can have.</summary>
    public const decimal MaxGrams = 1000;

    /// <summary>
    /// The values of an amount of a food. Grams of a per-piece food, or pieces of a per-100 g food, convert through
    /// the piece weight; without one they can't be worked out.
    /// </summary>
    /// <param name="food">The food (null = not in the account's foods).</param>
    /// <param name="amount">The amount.</param>
    /// <param name="unit">Grams or pieces.</param>
    /// <returns>The values, or null when they can't be worked out.</returns>
    public static Macros? For(Food? food, decimal amount, FoodUnit unit)
    {
        if (food is null || (food.Kcal is null && food.ProteinG is null))
        {
            return null;
        }

        decimal? factor = (food.Basis, unit) switch
        {
            (FoodBasis.Per100g, FoodUnit.Grams) => amount / 100m,
            (FoodBasis.Per100g, FoodUnit.Pieces) => food.PieceGrams is { } grams ? amount * grams / 100m : null,
            (FoodBasis.PerPiece, FoodUnit.Pieces) => amount,
            (FoodBasis.PerPiece, FoodUnit.Grams) => food.PieceGrams is { } grams and > 0 ? amount / grams : null,
            _ => null,
        };

        if (factor is not { } f)
        {
            return null;
        }

        return new Macros(Scale(food.Kcal, f), Scale(food.ProteinG, f), Scale(food.CarbsG, f), Scale(food.FatG, f));
    }

    /// <summary>
    /// Adds up values; unknown items are counted, not guessed.
    /// </summary>
    /// <param name="items">The values of each food (null = unknown).</param>
    /// <returns>The totals.</returns>
    public static MacrosDTO Sum(IEnumerable<Macros?> items)
    {
        var total = new MacrosDTO();
        foreach (var item in items)
        {
            if (item is not { } m)
            {
                total.Unknown++;
                continue;
            }

            total.Kcal = Add(total.Kcal, m.Kcal);
            total.ProteinG = Add(total.ProteinG, m.ProteinG);
            total.CarbsG = Add(total.CarbsG, m.CarbsG);
            total.FatG = Add(total.FatG, m.FatG);
        }

        total.Kcal = Round(total.Kcal);
        total.ProteinG = Round(total.ProteinG);
        total.CarbsG = Round(total.CarbsG);
        total.FatG = Round(total.FatG);
        return total;
    }

    /// <summary>
    /// Adds up totals (meals into a day).
    /// </summary>
    /// <param name="totals">The totals.</param>
    /// <returns>Their sum.</returns>
    public static MacrosDTO Add(IEnumerable<MacrosDTO> totals)
    {
        var sum = new MacrosDTO();
        foreach (var total in totals)
        {
            sum.Kcal = Add(sum.Kcal, total.Kcal);
            sum.ProteinG = Add(sum.ProteinG, total.ProteinG);
            sum.CarbsG = Add(sum.CarbsG, total.CarbsG);
            sum.FatG = Add(sum.FatG, total.FatG);
            sum.Unknown += total.Unknown;
        }

        return sum;
    }

    /// <summary>
    /// Whether totals know anything (at least calories or protein).
    /// </summary>
    /// <param name="total">The totals.</param>
    /// <returns>True when there is something to show.</returns>
    public static bool Known(MacrosDTO? total) => total is not null && (total.Kcal is not null || total.ProteinG is not null);

    /// <summary>
    /// Reads back a meal's foods as written on a logged day ("Oats 80 g · Banana 1 pc", see WeekBuilder.FoodsLine).
    /// </summary>
    /// <param name="line">The foods line.</param>
    /// <returns>Each food's name, amount and unit; parts that don't read are left out.</returns>
    public static List<(string Name, decimal Amount, FoodUnit Unit)> ParseFoodsLine(string? line)
    {
        var foods = new List<(string, decimal, FoodUnit)>();
        if (string.IsNullOrWhiteSpace(line))
        {
            return foods;
        }

        foreach (var part in line.Split(" · ", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var match = FoodPart().Match(part);
            if (match.Success && decimal.TryParse(match.Groups[2].Value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
            {
                foods.Add((match.Groups[1].Value, amount, match.Groups[3].Value == "pc" ? FoodUnit.Pieces : FoodUnit.Grams));
            }
        }

        return foods;
    }

    /// <summary>
    /// A search key: lower case, without accents, spaces collapsed ("Pão  de Centeio" → "pao de centeio").
    /// </summary>
    /// <param name="text">The text.</param>
    /// <returns>The key.</returns>
    public static string SearchKey(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return string.Empty;
        }

        var builder = new StringBuilder(text.Length);
        foreach (var c in text.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                builder.Append(char.ToLowerInvariant(c));
            }
        }

        return Spaces().Replace(builder.ToString().Normalize(NormalizationForm.FormC), " ").Trim();
    }

    private static decimal? Scale(decimal? value, decimal factor) => value is { } v ? v * factor : null;

    private static decimal? Add(decimal? total, decimal? value) => value is { } v ? (total ?? 0) + v : total;

    private static decimal? Round(decimal? value) => value is { } v ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : null;

    [GeneratedRegex(@"^(.+?)\s+(\d+(?:\.\d+)?)\s+(g|pc)$")]
    private static partial Regex FoodPart();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}

/// <summary>
/// Calories and macros of one food amount (each null when the label doesn't give it).
/// </summary>
/// <param name="Kcal">Calories.</param>
/// <param name="ProteinG">Protein (g).</param>
/// <param name="CarbsG">Carbohydrates (g).</param>
/// <param name="FatG">Fat (g).</param>
internal readonly record struct Macros(decimal? Kcal, decimal? ProteinG, decimal? CarbsG, decimal? FatG);
