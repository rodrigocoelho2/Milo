namespace BodyOps.Library.Constants;

/// <summary>
/// Names of the HTTP headers the page and the WebApi agree on.
/// </summary>
public static class ApiHeaders
{
    /// <summary>
    /// Sent by the page on every request that changes something (CSRF guard for the cookie login).
    /// </summary>
    public const string App = "X-BodyOps";
}
