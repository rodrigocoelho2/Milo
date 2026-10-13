using BodyOps.WebApi.Features.Accounts.Models;
using BodyOps.WebApi.Features.Accounts.Models.Entities;

namespace BodyOps.WebApi.Features.Accounts.Repositories;

/// <summary>
/// Data access for accounts (dbo.User).
/// </summary>
internal interface IUserRepository
{
    /// <summary>
    /// Counts the accounts (dbo.UserCount).
    /// </summary>
    /// <returns>The number of accounts.</returns>
    Task<int> CountAsync();

    /// <summary>
    /// Gets one account (dbo.UserGet).
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <returns>The account, or null.</returns>
    Task<UserAccount?> GetAsync(long userId);

    /// <summary>
    /// Finds an account by username, ignoring case (dbo.UserGetByName).
    /// </summary>
    /// <param name="username">The username.</param>
    /// <returns>The account, or null.</returns>
    Task<UserAccount?> GetByNameAsync(string username);

    /// <summary>
    /// Lists every account with how much it has planned and logged (dbo.UserList).
    /// </summary>
    /// <returns>The accounts, oldest first.</returns>
    Task<List<UserAccountListItem>> ListAsync();

    /// <summary>
    /// Creates an account (dbo.UserCreate). The first account becomes the admin.
    /// </summary>
    /// <param name="username">The username.</param>
    /// <param name="passwordHash">The password hash.</param>
    /// <param name="securityStamp">The security stamp.</param>
    /// <param name="allowRegistration">Whether sign-up is open.</param>
    /// <returns>The outcome and the new account.</returns>
    Task<UserCreateResult> CreateAsync(string username, string passwordHash, string securityStamp, bool allowRegistration);

    /// <summary>
    /// Sets a new password and security stamp (dbo.UserSetPassword).
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <param name="passwordHash">The new password hash.</param>
    /// <param name="securityStamp">The new security stamp.</param>
    /// <returns>The updated account, or null when it does not exist.</returns>
    Task<UserAccount?> SetPasswordAsync(long userId, string passwordHash, string securityStamp);

    /// <summary>
    /// Records a sign-in (dbo.UserTouchLogin).
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <returns>A task that completes when saved.</returns>
    Task TouchLoginAsync(long userId);

    /// <summary>
    /// Deletes an account and all its data (dbo.UserDelete).
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <returns>True when an account was deleted.</returns>
    Task<bool> DeleteAsync(long userId);

    /// <summary>
    /// Removes the account's plan, logged days, weigh-ins and settings, keeping the account (dbo.UserDataClear).
    /// </summary>
    /// <param name="userId">The account identifier.</param>
    /// <returns>A task that completes when removed.</returns>
    Task ClearDataAsync(long userId);
}
