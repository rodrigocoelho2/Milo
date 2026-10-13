using System.Text.RegularExpressions;

namespace BodyOps.WebApi.Features.Plans.Services;

/// <summary>
/// Tidies names typed by people: trimmed, with runs of spaces collapsed. The database derives an exercise's NameKey
/// (which links the same exercise across days) as LOWER(Name), so names must always pass through <see cref="Clean"/>.
/// </summary>
internal static partial class NameText
{
    /// <summary>
    /// Trims and collapses whitespace.
    /// </summary>
    /// <param name="value">The typed text (may be null).</param>
    /// <returns>The tidy text; empty for null.</returns>
    public static string Clean(string? value) => value is null ? string.Empty : Spaces().Replace(value.Trim(), " ");

    /// <summary>
    /// The key that identifies an exercise across days ("Pec deck" and " pec  DECK" are the same exercise).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The cleaned name in lower case.</returns>
    public static string Key(string? name) => Clean(name).ToLowerInvariant();

    [GeneratedRegex(@"\s+")]
    private static partial Regex Spaces();
}
