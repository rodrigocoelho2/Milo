namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// Someone new back from Discord, before they pick a user name.
/// </summary>
public sealed class DiscordPendingDTO
{
    /// <summary>Gets or sets the Discord account's name.</summary>
    public string Name { get; set; } = string.Empty;

    /// <summary>Gets or sets the user name suggested from it (may be empty).</summary>
    public string Suggestion { get; set; } = string.Empty;
}

/// <summary>
/// The user name picked for a new account made with Discord.
/// </summary>
public sealed class DiscordRegisterDTO
{
    /// <summary>Gets or sets the user name.</summary>
    public string? Username { get; set; }
}
