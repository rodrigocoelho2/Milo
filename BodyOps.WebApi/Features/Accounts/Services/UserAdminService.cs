using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;
using BodyOps.WebApi.Features.Accounts.Repositories;

namespace BodyOps.WebApi.Features.Accounts.Services;

/// <summary>
/// Manage users: list, reset password, delete.
/// </summary>
/// <param name="userRepository">Account data access.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class UserAdminService(IUserRepository userRepository, IGlobalizationHelper globalizationHelper) : IUserAdminService
{
    private readonly IUserRepository _userRepository = userRepository;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<UserListItemDTO>>> ListAsync()
    {
        var users = await _userRepository.ListAsync();
        return ServiceApiResponse<List<UserListItemDTO>>.Success(users.Select(static user => new UserListItemDTO(user)).ToList());
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<TemporaryPasswordDTO>> ResetPasswordAsync(long adminUserId, long userId)
    {
        if (userId == adminUserId)
        {
            return await _globalizationHelper.ValidationProblemAsync<TemporaryPasswordDTO>("userId", nameof(TranslationKeys.ResetOwnPassword));
        }

        var temporary = PasswordHasher.GenerateTemporary();
        var updated = await _userRepository.SetPasswordAsync(userId, PasswordHasher.Hash(temporary), PasswordHasher.NewSecurityStamp());
        return updated is null
            ? await _globalizationHelper.NotFoundAsync<TemporaryPasswordDTO>(userId)
            : ServiceApiResponse<TemporaryPasswordDTO>.Success(new TemporaryPasswordDTO { TemporaryPassword = temporary });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<string>> DeleteAsync(long adminUserId, long userId)
    {
        if (userId == adminUserId)
        {
            return await _globalizationHelper.ValidationProblemAsync<string>("userId", nameof(TranslationKeys.DeleteOwnAccount));
        }

        return await _userRepository.DeleteAsync(userId)
            ? ServiceApiResponse<string>.Success()
            : await _globalizationHelper.NotFoundAsync<string>(userId);
    }
}
