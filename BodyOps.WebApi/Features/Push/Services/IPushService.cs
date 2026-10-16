using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Push.Models.DTOs;

namespace BodyOps.WebApi.Features.Push.Services;

/// <summary>
/// Push reminders for the signed-in account: its devices, turning them on and off, and a test.
/// </summary>
internal interface IPushService
{
    /// <summary>
    /// Whether push is on, the key to subscribe with and the account's devices.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The push state.</returns>
    Task<ServiceApiResponse<PushDTO>> GetAsync(long userId);

    /// <summary>
    /// Turns push on for this browser.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="request">The browser's subscription.</param>
    /// <returns>The push state.</returns>
    Task<ServiceApiResponse<PushDTO>> SubscribeAsync(long userId, PushSubscribeDTO request);

    /// <summary>
    /// Turns push off for a device (one of the list, or this browser).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="request">The device.</param>
    /// <returns>The push state.</returns>
    Task<ServiceApiResponse<PushDTO>> UnsubscribeAsync(long userId, PushUnsubscribeDTO request);

    /// <summary>
    /// Sends a test notification to every device of the account.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>How many devices it reached.</returns>
    Task<ServiceApiResponse<PushTestResultDTO>> TestAsync(long userId);
}
