using System.Globalization;
using System.Text.RegularExpressions;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Reads the planned sets out of the "Sets × reps" text of an exercise: "3x12" is three sets of 12,
/// "3×12, 2×10" is five sets, "4 sets" is four sets with no rep target (0).
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
    public static List<int> Targets(string? detail)
    {
        var targets = new List<int>();
        if (string.IsNullOrWhiteSpace(detail))
        {
            return targets;
        }

        var text = detail.Replace('×', 'x').Replace('X', 'x');
        foreach (Match match in SetsByReps().Matches(text))
        {
            var sets = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
            var reps = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);
            for (var i = 0; i < sets && targets.Count < MaxSets; i++)
            {
                targets.Add(Math.Min(reps, 999));
            }
        }

        if (targets.Count == 0 && SetsOnly().Match(text) is { Success: true } setsOnly)
        {
            var sets = Math.Min(int.Parse(setsOnly.Groups[1].Value, CultureInfo.InvariantCulture), MaxSets);
            targets.AddRange(Enumerable.Repeat(0, sets));
        }

        return targets;
    }

    [GeneratedRegex(@"(\d{1,2})\s*x\s*(\d{1,3})")]
    private static partial Regex SetsByReps();

    [GeneratedRegex(@"(\d{1,2})\s*(?:sets?|s[ée]ries?)\b", RegexOptions.IgnoreCase)]
    private static partial Regex SetsOnly();
}
