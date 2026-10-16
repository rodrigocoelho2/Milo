using BodyOps.WebApi.Features.Push.Models.Entities;

namespace BodyOps.WebApi.Features.Push.Repositories;

/// <summary>
/// The devices with push on (dbo.PushSubscription) and the reminders already sent (dbo.PushSent).
/// </summary>
internal interface IPushRepository
{
    /// <summary>
    /// Lists devices.
    /// </summary>
    /// <param name="userId">One account's, or null for every account's.</param>
    /// <returns>The devices.</returns>
    Task<List<PushSubscriptionRow>> ListAsync(long? userId);

    /// <summary>
    /// Adds or updates this browser for an account.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <param name="p256dh">The public key.</param>
    /// <param name="auth">The secret.</param>
    /// <param name="device">The label.</param>
    /// <returns>The device.</returns>
    Task<PushSubscriptionRow> SaveAsync(long userId, string endpoint, string p256dh, string auth, string device);

    /// <summary>
    /// Removes a device by id (one of the account's) or by endpoint.
    /// </summary>
    /// <param name="userId">The account (with <paramref name="id"/>).</param>
    /// <param name="id">The device id.</param>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>False when nothing matched.</returns>
    Task<bool> DeleteAsync(long? userId, long? id, string? endpoint);

    /// <summary>
    /// Records how the last push to a device went.
    /// </summary>
    /// <param name="id">The device.</param>
    /// <param name="error">The error, or empty when delivered.</param>
    /// <returns>A task.</returns>
    Task MarkAsync(long id, string error);

    /// <summary>
    /// Claims a reminder so it is sent once.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="key">The reminder key ("meal:2026-10-16:12").</param>
    /// <returns>True the first time.</returns>
    Task<bool> ClaimAsync(long userId, string key);
}
