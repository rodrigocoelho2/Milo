namespace BodyOps.WebApi.Features.Push.Models.Entities;

/// <summary>
/// One row of dbo.PushSubscription. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class PushSubscriptionRow
{
    /// <summary>Gets or sets the device identifier.</summary>
    public long PushSubscriptionID { get; set; }

    /// <summary>Gets or sets the account it belongs to.</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the browser's push service endpoint.</summary>
    public string Endpoint { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's public key (base64url).</summary>
    public string P256dh { get; set; } = string.Empty;

    /// <summary>Gets or sets the browser's authentication secret (base64url).</summary>
    public string Auth { get; set; } = string.Empty;

    /// <summary>Gets or sets a label ("iPhone · Milo app").</summary>
    public string Device { get; set; } = string.Empty;

    /// <summary>Gets or sets when it was added (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets when a push last reached it (UTC).</summary>
    public DateTime? LastSentAt { get; set; }

    /// <summary>Gets or sets the last delivery error, or empty.</summary>
    public string LastError { get; set; } = string.Empty;
}
