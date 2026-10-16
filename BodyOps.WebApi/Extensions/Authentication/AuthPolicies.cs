namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// Authorization policy names.
/// </summary>
internal static class AuthPolicies
{
    /// <summary>Only the server admin (Manage users, Download all assets).</summary>
    public const string Admin = "admin";
}

/// <summary>
/// Rate limiting policy names.
/// </summary>
internal static class RateLimitPolicies
{
    /// <summary>Sign-in, sign-up and password attempts: a few per minute per address, to slow down guessing.</summary>
    public const string Auth = "auth";

    /// <summary>What the Apple Health Shortcut sends: plenty for a phone, a wall for guessing keys.</summary>
    public const string Watch = "watch";
}
