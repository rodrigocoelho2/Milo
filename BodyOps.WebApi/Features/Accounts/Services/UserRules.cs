namespace BodyOps.WebApi.Features.Accounts.Services;

/// <summary>
/// Small rules about Discord accounts that don't need the database.
/// </summary>
internal static class UserRules
{
    /// <summary>The longest Discord name kept.</summary>
    public const int DiscordNameLength = 64;

    /// <summary>Whether a text is a Discord account id (a snowflake: digits only).</summary>
    /// <param name="value">The text.</param>
    /// <returns>True for a Discord id.</returns>
    public static bool IsDiscordId(string? value) =>
        value is { Length: >= 5 and <= 32 } && value.All(char.IsAsciiDigit);

    /// <summary>The Discord name to keep: trimmed, shortened, or null when there's none.</summary>
    /// <param name="value">The name Discord gave.</param>
    /// <returns>The name, or null.</returns>
    public static string? DiscordName(string? value)
    {
        var name = value?.Trim();
        return string.IsNullOrEmpty(name) ? null : name.Length <= DiscordNameLength ? name : name[..DiscordNameLength];
    }

    /// <summary>
    /// A user name to suggest for a Discord account: its handle (or else its display name) with only the characters
    /// allowed, or empty when too little is left.
    /// </summary>
    /// <param name="handle">The Discord user name.</param>
    /// <param name="name">The Discord display name.</param>
    /// <param name="maxLength">The longest user name allowed.</param>
    /// <returns>The suggestion, or empty.</returns>
    public static string SuggestUserName(string? handle, string? name, int maxLength)
    {
        foreach (var candidate in new[] { handle, name })
        {
            var kept = new string((candidate ?? string.Empty).Where(static c => char.IsAsciiLetterOrDigit(c) || c is '.' or '_' or '-').ToArray());
            if (kept.Length >= 3)
            {
                return kept.Length <= maxLength ? kept : kept[..maxLength];
            }
        }

        return string.Empty;
    }
}
