using System.Globalization;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Meal times as the API sends them: "HH:mm" (24 hours), or null for none.
/// </summary>
internal static class MealTimeText
{
    /// <summary>
    /// Reads a time typed as "8:00", "08:00" or "08:00:00".
    /// </summary>
    /// <param name="text">The text; null or blank means no time.</param>
    /// <param name="time">The time of day, or null.</param>
    /// <returns>False when the text is not a time of day.</returns>
    public static bool TryParse(string? text, out TimeSpan? time)
    {
        time = null;
        if (string.IsNullOrWhiteSpace(text))
        {
            return true;
        }

        if (TimeOnly.TryParseExact(text.Trim(), ["H:mm", "HH:mm", "HH:mm:ss"], CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            time = new TimeSpan(parsed.Hour, parsed.Minute, 0);
            return true;
        }

        return false;
    }

    /// <summary>
    /// Writes a time as "HH:mm".
    /// </summary>
    /// <param name="time">The time of day, or null.</param>
    /// <returns>The text, or null.</returns>
    public static string? Format(TimeSpan? time) =>
        time is { } value ? $"{value.Hours:00}:{value.Minutes:00}" : null;
}
