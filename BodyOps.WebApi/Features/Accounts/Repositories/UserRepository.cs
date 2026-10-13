using System.Data;
using Dapper;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Accounts.Models;
using BodyOps.WebApi.Features.Accounts.Models.Entities;

namespace BodyOps.WebApi.Features.Accounts.Repositories;

/// <summary>
/// Accounts through the dbo.User* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class UserRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IUserRepository
{
    /// <inheritdoc />
    public Task<int> CountAsync() => ExecuteScalarAsync<int>("[dbo].[UserCount]");

    /// <inheritdoc />
    public Task<UserAccount?> GetAsync(long userId) =>
        QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserGet]", new { UserID = userId });

    /// <inheritdoc />
    public Task<UserAccount?> GetByNameAsync(string username) =>
        QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserGetByName]", new { Username = username });

    /// <inheritdoc />
    public Task<List<UserAccountListItem>> ListAsync() => QueryAsync<UserAccountListItem>("[dbo].[UserList]");

    /// <inheritdoc />
    public async Task<UserCreateResult> CreateAsync(string username, string passwordHash, string securityStamp, bool allowRegistration)
    {
        var parameters = new DynamicParameters(new
        {
            Username = username,
            PasswordHash = passwordHash,
            SecurityStamp = securityStamp,
            AllowRegistration = allowRegistration,
        });
        parameters.Add("Outcome", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var user = await QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserCreate]", parameters);
        return new UserCreateResult((UserCreateOutcome)parameters.Get<int>("Outcome"), user);
    }

    /// <inheritdoc />
    public Task<UserAccount?> SetPasswordAsync(long userId, string passwordHash, string securityStamp) =>
        QuerySingleOrDefaultAsync<UserAccount>("[dbo].[UserSetPassword]", new { UserID = userId, PasswordHash = passwordHash, SecurityStamp = securityStamp });

    /// <inheritdoc />
    public Task TouchLoginAsync(long userId) => ExecuteAsync("[dbo].[UserTouchLogin]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long userId)
    {
        var parameters = new DynamicParameters(new { UserID = userId });
        parameters.Add("Deleted", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await ExecuteAsync("[dbo].[UserDelete]", parameters);
        return parameters.Get<bool>("Deleted");
    }

    /// <inheritdoc />
    public Task ClearDataAsync(long userId) => ExecuteAsync("[dbo].[UserDataClear]", new { UserID = userId });
}
