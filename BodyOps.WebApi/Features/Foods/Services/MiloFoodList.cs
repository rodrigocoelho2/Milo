using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Foods.Models.DTOs;

namespace BodyOps.WebApi.Features.Foods.Services;

/// <summary>
/// Searches Milo's built-in list by English or Portuguese name: every word typed must start a word of the name.
/// </summary>
internal static class MiloFoodList
{
    private static readonly (MiloFood Food, string[] En, string[] Pt)[] Index = MiloFoodData.All
        .Select(static food => (food, Words(food.En), Words(food.Pt)))
        .ToArray();

    /// <summary>
    /// Finds foods.
    /// </summary>
    /// <param name="query">What was typed ("frango", "chicken br").</param>
    /// <param name="limit">The most to return.</param>
    /// <returns>The matches, best first: names starting with the query, then the rest, shortest name first.</returns>
    public static List<MiloFood> Search(string? query, int limit = 12)
    {
        var words = Words(query);
        if (words.Length == 0)
        {
            return [];
        }

        return Index
            .Select(entry => (entry.Food, Pt: Matches(entry.Pt, words), En: Matches(entry.En, words), entry))
            .Where(static hit => hit.Pt || hit.En)
            .OrderByDescending(hit => (hit.Pt ? hit.entry.Pt : hit.entry.En)[0].StartsWith(words[0], StringComparison.Ordinal))
            .ThenBy(static hit => hit.Pt ? hit.Food.Pt.Length : hit.Food.En.Length)
            .Take(limit)
            .Select(static hit => hit.Food)
            .ToList();
    }

    /// <summary>
    /// Finds a food by its list id.
    /// </summary>
    /// <param name="id">The id ("u08120").</param>
    /// <returns>The food, or null.</returns>
    public static MiloFood? Find(string? id) => MiloFoodData.All.FirstOrDefault(food => food.Id == id);

    /// <summary>
    /// A list food as a search result, named in the language the query matched.
    /// </summary>
    /// <param name="food">The food.</param>
    /// <param name="query">What was typed.</param>
    /// <returns>The result.</returns>
    public static FoodHitDTO Hit(MiloFood food, string? query)
    {
        var portuguese = Matches(Words(food.Pt), Words(query)) && !Matches(Words(food.En), Words(query));
        return new FoodHitDTO
        {
            Source = FoodSource.Milo,
            SourceRef = food.Id,
            Name = portuguese ? food.Pt : food.En,
            Detail = portuguese ? food.En : food.Pt,
            Kcal = food.Kcal,
            ProteinG = food.ProteinG,
            CarbsG = food.CarbsG,
            FatG = food.FatG,
            PieceGrams = food.PieceGrams,
            PieceName = food.PieceName,
        };
    }

    private static bool Matches(string[] name, string[] words) =>
        words.All(word => name.Any(part => part.StartsWith(word, StringComparison.Ordinal)));

    private static string[] Words(string? text) =>
        FoodMath.SearchKey(text).Split([' ', ',', '/', '(', ')', '–', '-'], StringSplitOptions.RemoveEmptyEntries);
}
