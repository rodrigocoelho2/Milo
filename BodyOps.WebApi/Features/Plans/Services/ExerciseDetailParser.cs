using System.Globalization;
using System.Text.RegularExpressions;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Reads the planned sets out of the "Sets × reps" text of an exercise: "3x12" is three sets of 12,
/// "3×12, 2×10" is five sets, "4 sets" is four sets with no rep target (0), "3x8-12" is three sets with a rep range
/// (target 12, the top of the range; at least 8).
/// </summary>
internal static partial class ExerciseDetailParser
{
    /// <summary>The most sets one exercise can have.</summary>
    public const int MaxSets = 20;

    /// <summary>
    /// The rep target of each planned set.
    /// </summary>
    /// <param name="detail">The plan text.</param>
    /// <returns>One entry per set (0 when the plan gives no reps); empty when the text names no sets.</returns>
    public static List<int> Targets(string? detail) => Sets(detail).Select(static set => set.Max).ToList();

    /// <summary>
    /// The lowest reps of each planned set: the bottom of a range ("3x8-12" → 8, 8, 8), else the same as
    /// <see cref="Targets"/>.
    /// </summary>
    /// <param name="detail">The plan text.</param>
    /// <returns>One entry per set.</returns>
    public static List<int> Mins(string? detail) => Sets(detail).Select(static set => set.Min).ToList();

    /// <summary>
    /// Whether the plan gives a rep range ("3x8-12") for at least one set, which turns on automatic progression.
    /// </summary>
    /// <param name="detail">The plan text.</param>
    /// <returns>True for a range.</returns>
    public static bool IsRange(string? detail) => Sets(detail).Any(static set => set.Min < set.Max);

    /// <summary>
    /// The planned sets with their rep range (a fixed number has Min = Max).
    /// </summary>
    /// <param name="detail">The plan text.</param>
    /// <returns>One entry per set; empty when the text names no sets.</returns>
    public static List<(int Min, int Max)> Sets(string? detail)
    {
        var sets = new List<(int Min, int Max)>();
        if (string.IsNullOrWhiteSpace(detail))
        {
            return sets;
        }

        var text = detail.Replace('×', 'x').Replace('X', 'x').Replace('–', '-').Replace('—', '-');
        foreach (Match match in SetsByReps().Matches(text))
        {
            var count = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var low = Math.Min(int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture), 999);
            var high = match.Groups[3].Success ? Math.Min(int.Parse(match.Groups[3].Value, CultureInfo.InvariantCulture), 999) : low;
            if (high < low)
            {
                (low, high) = (high, low);
            }

            for (var i = 0; i < count && sets.Count < MaxSets; i++)
            {
                sets.Add((low, high));
            }
        }

        if (sets.Count == 0 && SetsOnly().Match(text) is { Success: true } setsOnly)
        {
            var count = Math.Min(int.Parse(setsOnly.Groups[1].Value, CultureInfo.InvariantCulture), MaxSets);
            sets.AddRange(Enumerable.Repeat((0, 0), count));
        }

        return sets;
    }

    [GeneratedRegex(@"(\d{1,2})\s*x\s*(\d{1,3})(?:\s*-\s*(\d{1,3})(?!\d)(?!\s*kg))?", RegexOptions.IgnoreCase)]
    private static partial Regex SetsByReps();

    [GeneratedRegex(@"(\d{1,2})\s*(?:sets?|s[ée]ries?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SetsOnly();
}
