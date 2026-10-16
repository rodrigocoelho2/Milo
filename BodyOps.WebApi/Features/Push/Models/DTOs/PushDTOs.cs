using System.Security.Cryptography;
using System.Text;
using BodyOps.WebApi.Features.Push.Models.Entities;

namespace BodyOps.WebApi.Features.Push.Models.DTOs;

/// <summary>
/// Push on this server for the signed-in account: whether it is on, the key browsers subscribe with, and the devices.
/// </summary>
public sealed class PushDTO
{
    /// <summary>Gets or sets a value indicating whether the server sends push reminders.</summary>
    public bool Enabled { get; set; }

    /// <summary>Gets or sets the VAPID public key (base64url) browsers subscribe with.</summary>
    public string PublicKey { get; set; } = string.Empty;

    /// <summary>Gets or sets the account's devices, newest first.</summary>
    public List<PushDeviceDTO> Devices { get; set; } = [];
}

/// <summary>
/// One device with push turned on.
/// </summary>
public sealed class PushDeviceDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public PushDeviceDTO()
    {
    }

    /// <summary>
    /// Maps a device.
    /// </summary>
    /// <param name="row">The device.</param>
    public PushDeviceDTO(PushSubscriptionRow row)
    {
        Id = row.PushSubscriptionID;
        Key = KeyOf(row.Endpoint);
        Device = row.Device;
        CreatedAt = row.CreatedAt;
        LastSentAt = row.LastSentAt;
        LastError = row.LastError;
    }

    /// <summary>Gets or sets the identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the first 12 hex characters of the endpoint's SHA-256, so a page knows its own device.</summary>
    public string Key { get; set; } = string.Empty;

    /// <summary>Gets or sets the label.</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was added (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when a push last reached it (UTC).</summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>Gets or sets the last delivery error, or empty.</summary>
    public string LastError { get; set; } = string.Empty;

    /// <summary>
    /// The device key of an endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint.</param>
    /// <returns>12 hex characters.</returns>
    public static string KeyOf(string endpoint) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(endpoint)))[..12];
}

/// <summary>
/// A browser's push subscription (PushSubscription.toJSON() of the page).
/// </summary>
public sealed class PushSubscribeDTO
{
    /// <summary>Gets or sets the endpoint.</summary>
    public string? Endpoint { get; set; }

    /// <summary>Gets or sets the public key (keys.p256dh).</summary>
    public string? P256dh { get; set; }

    /// <summary>Gets or sets the authentication secret (keys.auth).</summary>
    public string? Auth { get; set; }

    /// <summary>Gets or sets a label for the device list.</summary>
    public string? Device { get; set; }
}

/// <summary>
/// A device to turn push off for: by id (from the list) or by this browser's endpoint.
/// </summary>
public sealed class PushUnsubscribeDTO
{
    /// <summary>Gets or sets the device id.</summary>
    public long? Id { get; set; }

    /// <summary>Gets or sets the endpoint.</summary>
    public string? Endpoint { get; set; }
}

/// <summary>
/// How many devices a test push reached.
/// </summary>
public sealed class PushTestResultDTO
{
    /// <summary>Gets or sets the devices reached.</summary>
    public int Reached { get; set; }

    /// <summary>Gets or sets the devices that failed.</summary>
    public int Failed { get; set; }
}

/// <summary>
/// A button on a notification (Android, Windows, macOS Chrome; iOS shows none).
/// </summary>
/// <param name="Action">What the service worker does ("ate", "other", "weigh").</param>
/// <param name="Title">The button text.</param>
public sealed record PushAction(string Action, string Title);

/// <summary>
/// One notification: what the service worker shows, and where a tap goes.
/// </summary>
/// <param name="Title">The title.</param>
/// <param name="Body">The text.</param>
/// <param name="Url">Where a tap opens (a path of the app).</param>
/// <param name="Tag">Same tag = the newer replaces the older.</param>
/// <param name="Actions">The buttons.</param>
/// <param name="Data">What the buttons need (a date, a meal).</param>
public sealed record PushMessage(string Title, string Body, string Url, string Tag, IReadOnlyList<PushAction>? Actions = null, IReadOnlyDictionary<string, string>? Data = null);
