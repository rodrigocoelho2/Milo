using BodyOps.WebApi.Features.Accounts.Models.Entities;

namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// The signed-in account.
/// </summary>
public sealed class MeDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public MeDTO()
    {
    }

    /// <summary>
    /// Maps an account.
    /// </summary>
    /// <param name="entity">The account.</param>
    public MeDTO(UserAccount entity)
    {
        Id = entity.UserID;
        UniqueId = entity.UserUniqueID;
        Username = entity.Username;
        IsAdmin = entity.IsAdmin;
        DiscordLinked = !string.IsNullOrEmpty(entity.DiscordID);
        DiscordName = DiscordLinked ? entity.DiscordName : null;
        HasPassword = !string.IsNullOrEmpty(entity.PasswordHash);
    }

    /// <summary>Gets or sets the account identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the account's stable public identifier.</summary>
    public Guid UniqueId { get; set; }

    /// <summary>Gets or sets the username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is the server admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets a value indicating whether a Discord account is linked (it can sign in to this one).</summary>
    public bool DiscordLinked { get; set; }

    /// <summary>Gets or sets the linked Discord account's name.</summary>
    public string? DiscordName { get; set; }

    /// <summary>Gets or sets a value indicating whether the account has a password (one made with Discord has none until one is set).</summary>
    public bool HasPassword { get; set; }
}
