using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;

namespace BodyOps.WebApi.Features.Accounts.Services;

/// <summary>
/// Business logic for the signed-in user's own account.
/// </summary>
internal interface IAccountService
{
    /// <summary>
    /// Gets the signed-in account.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <returns>The account.</returns>
    Task<ServiceApiResponse<MeDTO>> MeAsync(long userId);

    /// <summary>
    /// Creates an account and signs it in. The first account is always allowed and becomes the admin.
    /// </summary>
    /// <param name="request">The username and password.</param>
    /// <returns>The new account (201), or why not.</returns>
    Task<ServiceApiResponse<MeDTO>> RegisterAsync(CredentialsDTO request);

    /// <summary>
    /// Checks the credentials and signs in.
    /// </summary>
    /// <param name="request">The username and password.</param>
    /// <returns>The account, or 401.</returns>
    Task<ServiceApiResponse<MeDTO>> LoginAsync(CredentialsDTO request);

    /// <summary>
    /// Signs out.
    /// </summary>
    /// <returns>No content.</returns>
    Task<ServiceApiResponse<string>> LogoutAsync();

    /// <summary>
    /// Changes the password. Every other device is signed out; this one gets a fresh cookie.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="request">The current and the new password.</param>
    /// <returns>No content, or why not.</returns>
    Task<ServiceApiResponse<string>> ChangePasswordAsync(long userId, PasswordChangeDTO request);

    /// <summary>
    /// Deletes the signed-in account with all its data, then signs out.
    /// </summary>
    /// <param name="userId">The signed-in account.</param>
    /// <param name="request">The password, as confirmation.</param>
    /// <returns>No content, or why not.</returns>
    Task<ServiceApiResponse<string>> DeleteAccountAsync(long userId, AccountDeleteDTO request);

    /// <summary>
    /// Signs in the account a (checked) Discord account is linked to.
    /// </summary>
    /// <param name="discordId">The Discord account's id.</param>
    /// <returns>The account, or not found.</returns>
    Task<ServiceApiResponse<MeDTO>> DiscordSignInAsync(string discordId);

    /// <summary>
    /// Creates an account that signs in with a (checked) Discord account, without a password, and signs it in.
    /// </summary>
    /// <param name="username">The user name picked.</param>
    /// <param name="discordId">The Discord account's id.</param>
    /// <param name="discordName">The Discord account's name.</param>
    /// <returns>The new account, or the problem.</returns>
    Task<ServiceApiResponse<MeDTO>> DiscordRegisterAsync(string? username, string discordId, string? discordName);

    /// <summary>
    /// Links a (checked) Discord account to an account.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="discordId">The Discord account's id.</param>
    /// <param name="discordName">The Discord account's name.</param>
    /// <returns>Done, or conflict when it is linked to someone else.</returns>
    Task<ServiceApiResponse<string>> DiscordLinkAsync(long userId, string discordId, string? discordName);

    /// <summary>
    /// Unlinks Discord (refused while it is the only way into the account).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>Done, or the problem.</returns>
    Task<ServiceApiResponse<string>> DiscordUnlinkAsync(long userId);
}
