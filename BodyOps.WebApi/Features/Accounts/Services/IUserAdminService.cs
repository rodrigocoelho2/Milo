using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;

namespace BodyOps.WebApi.Features.Accounts.Services;

/// <summary>
/// Business logic for Manage users (admin only).
/// </summary>
internal interface IUserAdminService
{
    /// <summary>
    /// Lists every account with its number of marked entries.
    /// </summary>
    /// <returns>The accounts, oldest first.</returns>
    Task<ServiceApiResponse<List<UserListItemDTO>>> ListAsync();

    /// <summary>
    /// Gives another account a temporary password (signing it out everywhere).
    /// </summary>
    /// <param name="adminUserId">The signed-in admin.</param>
    /// <param name="userId">The account to reset.</param>
    /// <returns>The temporary password, or why not.</returns>
    Task<ServiceApiResponse<TemporaryPasswordDTO>> ResetPasswordAsync(long adminUserId, long userId);

    /// <summary>
    /// Deletes another account with its plan, logged days and weigh-ins.
    /// </summary>
    /// <param name="adminUserId">The signed-in admin.</param>
    /// <param name="userId">The account to delete.</param>
    /// <returns>No content, or why not.</returns>
    Task<ServiceApiResponse<string>> DeleteAsync(long adminUserId, long userId);
}
