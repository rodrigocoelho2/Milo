using System.Text.RegularExpressions;
using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Accounts.Models;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;
using BodyOps.WebApi.Features.Accounts.Repositories;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Accounts.Services;

/// <summary>
/// Sign-up, sign-in, password change and account deletion.
/// </summary>
/// <param name="userRepository">Account data access.</param>
/// <param name="userSession">The login cookie of the current request.</param>
/// <param name="globalizationHelper">Translations.</param>
/// <param name="options">The BodyOps options.</param>
internal sealed partial class AccountService(
    IUserRepository userRepository,
    IUserSession userSession,
    IGlobalizationHelper globalizationHelper,
    IOptions<BodyOpsOptions> options) : IAccountService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IUserSession _userSession = userSession;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;
    private readonly IOptions<BodyOpsOptions> _options = options;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MeDTO>> MeAsync(long userId)
    {
        var user = await _userRepository.GetAsync(userId);
        return user is null
            ? ServiceApiResponse<MeDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)))
            : ServiceApiResponse<MeDTO>.Success(new MeDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MeDTO>> RegisterAsync(CredentialsDTO request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;

        if (!UsernamePattern().IsMatch(username))
        {
            return await _globalizationHelper.ValidationProblemAsync<MeDTO>("username", nameof(TranslationKeys.InvalidUsername));
        }

        if (await PasswordProblemAsync<MeDTO>("password", password) is { } passwordProblem)
        {
            return passwordProblem;
        }

        var result = await _userRepository.CreateAsync(username, PasswordHasher.Hash(password), PasswordHasher.NewSecurityStamp(), _options.Value.AllowRegistration);
        switch (result.Outcome)
        {
            case UserCreateOutcome.RegistrationClosed:
                return ServiceApiResponse<MeDTO>.Forbidden(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.RegistrationClosed)));
            case UserCreateOutcome.UsernameTaken:
                return ServiceApiResponse<MeDTO>.Conflict(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.UsernameTaken)));
        }

        var user = result.User!;
        await _userRepository.TouchLoginAsync(user.UserID);
        await _userSession.SignInAsync(user, request.Remember);
        return ServiceApiResponse<MeDTO>.Created(new MeDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MeDTO>> LoginAsync(CredentialsDTO request)
    {
        var username = request.Username?.Trim() ?? string.Empty;
        var password = request.Password ?? string.Empty;
        var user = username.Length is > 0 and <= 64 ? await _userRepository.GetByNameAsync(username) : null;

        if (user is null)
        {
            PasswordHasher.VerifyAgainstDummy(password);
            return ServiceApiResponse<MeDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        if (password.Length > PasswordHasher.MaxPasswordLength || !PasswordHasher.Verify(password, user.PasswordHash))
        {
            return ServiceApiResponse<MeDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        await _userRepository.TouchLoginAsync(user.UserID);
        await _userSession.SignInAsync(user, request.Remember);
        return ServiceApiResponse<MeDTO>.Success(new MeDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> LogoutAsync()
    {
        await _userSession.SignOutAsync();
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> ChangePasswordAsync(long userId, PasswordChangeDTO request)
    {
        var user = await _userRepository.GetAsync(userId);
        if (user is null)
        {
            return ServiceApiResponse<string>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        // An account made with Discord has no password yet: its first one needs no current password.
        if (!string.IsNullOrEmpty(user.PasswordHash) && !PasswordHasher.Verify(request.CurrentPassword ?? string.Empty, user.PasswordHash))
        {
            return await _globalizationHelper.ValidationProblemAsync<string>("currentPassword", nameof(TranslationKeys.WrongCurrentPassword));
        }

        if (await PasswordProblemAsync<string>("newPassword", request.NewPassword ?? string.Empty) is { } passwordProblem)
        {
            return passwordProblem;
        }

        var updated = await _userRepository.SetPasswordAsync(user.UserID, PasswordHasher.Hash(request.NewPassword!), PasswordHasher.NewSecurityStamp());

        // The new stamp signs out every other device; give this one a fresh cookie so it stays in.
        await _userSession.SignInAsync(updated!, remember: true);
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAccountAsync(long userId, AccountDeleteDTO request)
    {
        var user = await _userRepository.GetAsync(userId);
        if (user is null)
        {
            return ServiceApiResponse<string>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        // An account made with Discord and no password yet confirms with the session alone.
        if (!string.IsNullOrEmpty(user.PasswordHash) && !PasswordHasher.Verify(request.Password ?? string.Empty, user.PasswordHash))
        {
            return await _globalizationHelper.ValidationProblemAsync<string>("password", nameof(TranslationKeys.WrongPassword));
        }

        await _userRepository.DeleteAsync(user.UserID);
        await _userSession.SignOutAsync();
        return ServiceApiResponse<string>.Success();
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MeDTO>> DiscordSignInAsync(string discordId)
    {
        var user = UserRules.IsDiscordId(discordId) ? await _userRepository.GetByDiscordAsync(discordId) : null;
        if (user is null)
        {
            return ServiceApiResponse<MeDTO>.NotFound(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordNotLinked)));
        }

        await _userRepository.TouchLoginAsync(user.UserID);
        await _userSession.SignInAsync(user, remember: true);
        return ServiceApiResponse<MeDTO>.Success(new MeDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<MeDTO>> DiscordRegisterAsync(string? username, string discordId, string? discordName)
    {
        username = username?.Trim() ?? string.Empty;
        if (!UsernamePattern().IsMatch(username))
        {
            return await _globalizationHelper.ValidationProblemAsync<MeDTO>("username", nameof(TranslationKeys.InvalidUsername));
        }

        if (!UserRules.IsDiscordId(discordId))
        {
            return ServiceApiResponse<MeDTO>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordExpired)));
        }

        if (await _userRepository.GetByDiscordAsync(discordId) is not null)
        {
            return ServiceApiResponse<MeDTO>.Conflict(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordTaken)));
        }

        // The first account (the admin) is made with a password, from the sign-up form.
        if (await _userRepository.CountAsync() == 0)
        {
            return ServiceApiResponse<MeDTO>.Forbidden(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.RegistrationClosed)));
        }

        // No password: the empty hash never verifies, so only Discord opens the account until one is set.
        var result = await _userRepository.CreateAsync(username, string.Empty, PasswordHasher.NewSecurityStamp(), _options.Value.AllowRegistration);
        switch (result.Outcome)
        {
            case UserCreateOutcome.RegistrationClosed:
                return ServiceApiResponse<MeDTO>.Forbidden(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.RegistrationClosed)));
            case UserCreateOutcome.UsernameTaken:
                return ServiceApiResponse<MeDTO>.Conflict(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.UsernameTaken)));
        }

        var user = result.User!;
        if (!await _userRepository.SetDiscordAsync(user.UserID, discordId, UserRules.DiscordName(discordName)))
        {
            // Linked somewhere else a moment ago: don't leave an account nobody can open.
            await _userRepository.DeleteAsync(user.UserID);
            return ServiceApiResponse<MeDTO>.Conflict(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordTaken)));
        }

        user = (await _userRepository.GetAsync(user.UserID))!;
        await _userRepository.TouchLoginAsync(user.UserID);
        await _userSession.SignInAsync(user, remember: true);
        return ServiceApiResponse<MeDTO>.Created(new MeDTO(user));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DiscordLinkAsync(long userId, string discordId, string? discordName)
    {
        if (await _userRepository.GetAsync(userId) is null || !UserRules.IsDiscordId(discordId))
        {
            return ServiceApiResponse<string>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        return await _userRepository.SetDiscordAsync(userId, discordId, UserRules.DiscordName(discordName))
            ? ServiceApiResponse<string>.Success()
            : ServiceApiResponse<string>.Conflict(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.DiscordTaken)));
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DiscordUnlinkAsync(long userId)
    {
        var user = await _userRepository.GetAsync(userId);
        if (user is null)
        {
            return ServiceApiResponse<string>.Unauthorized(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.WrongCredentials)));
        }

        if (string.IsNullOrEmpty(user.PasswordHash))
        {
            return await _globalizationHelper.ValidationProblemAsync<string>("discord", nameof(TranslationKeys.DiscordNeedsPassword));
        }

        await _userRepository.SetDiscordAsync(userId, null, null);
        return ServiceApiResponse<string>.Success();
    }

    [GeneratedRegex(@"^[A-Za-z0-9_.\-]{3,24}$")]
    private static partial Regex UsernamePattern();

    private async Task<ServiceApiResponse<T>?> PasswordProblemAsync<T>(string field, string password) => password.Length switch
    {
        < PasswordHasher.MinPasswordLength => await _globalizationHelper.ValidationProblemAsync<T>(field, nameof(TranslationKeys.PasswordTooShort), PasswordHasher.MinPasswordLength),
        > PasswordHasher.MaxPasswordLength => await _globalizationHelper.ValidationProblemAsync<T>(field, nameof(TranslationKeys.PasswordTooLong), PasswordHasher.MaxPasswordLength),
        _ => null,
    };
}
