using System.Data;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Push.Models.Entities;
using Dapper;

namespace BodyOps.WebApi.Features.Push.Repositories;

/// <summary>
/// Push devices and sent reminders through the dbo.Push* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class PushRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IPushRepository
{
    /// <inheritdoc />
    public Task<List<PushSubscriptionRow>> ListAsync(long? userId) =>
        QueryAsync<PushSubscriptionRow>("[dbo].[PushSubscriptionList]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<PushSubscriptionRow> SaveAsync(long userId, string endpoint, string p256dh, string auth, string device) =>
        (await QuerySingleOrDefaultAsync<PushSubscriptionRow>("[dbo].[PushSubscriptionSave]", new { UserID = userId, Endpoint = endpoint, P256dh = p256dh, Auth = auth, Device = device }))!;

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long? userId, long? id, string? endpoint)
    {
        var parameters = new DynamicParameters(new { UserID = userId, PushSubscriptionID = id, Endpoint = endpoint });
        parameters.Add("Found", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        await ExecuteAsync("[dbo].[PushSubscriptionDelete]", parameters);
        return parameters.Get<bool>("Found");
    }

    /// <inheritdoc />
    public Task MarkAsync(long id, string error) =>
        ExecuteAsync("[dbo].[PushSubscriptionMark]", new { PushSubscriptionID = id, LastError = error.Length <= 200 ? error : error[..200] });

    /// <inheritdoc />
    public async Task<bool> ClaimAsync(long userId, string key)
    {
        var parameters = new DynamicParameters(new { UserID = userId, SentKey = key.Length <= 80 ? key : key[..80] });
        parameters.Add("Claimed", dbType: DbType.Boolean, direction: ParameterDirection.Output);
        await ExecuteAsync("[dbo].[PushSentClaim]", parameters);
        return parameters.Get<bool>("Claimed");
    }
}
