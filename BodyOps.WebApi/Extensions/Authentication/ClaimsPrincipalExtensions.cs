using System.Globalization;
using System.Security.Claims;
using BodyOps.WebApi.Features.Accounts.Models.Entities;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// Reads and builds the claims of the login cookie.
/// </summary>
internal static class ClaimsPrincipalExtensions
{
    /// <summary>The claim holding the account's security stamp; a new stamp invalidates every cookie.</summary>
    public const string StampClaim = "stamp";

    /// <summary>The claim present (value "1") on the admin's cookie.</summary>
    public const string AdminClaim = "admin";

    /// <summary>
    /// Builds the principal stored in the login cookie.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <returns>The principal.</returns>
    public static ClaimsPrincipal CreatePrincipal(UserAccount user)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, user.UserID.ToString(CultureInfo.InvariantCulture)),
            new(ClaimTypes.Name, user.Username),
            new(StampClaim, user.SecurityStamp),
        };
        if (user.IsAdmin)
        {
            claims.Add(new Claim(AdminClaim, "1"));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme));
    }

    /// <summary>
    /// Gets the signed-in account's identifier.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The UserID, or null when not signed in.</returns>
    public static long? UserID(this ClaimsPrincipal principal) =>
        long.TryParse(principal.FindFirstValue(ClaimTypes.NameIdentifier), NumberStyles.None, CultureInfo.InvariantCulture, out var id) ? id : null;

    /// <summary>
    /// Gets the signed-in account's identifier, for endpoints that already require authorization.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The UserID.</returns>
    public static long RequiredUserID(this ClaimsPrincipal principal) =>
        principal.UserID() ?? throw new InvalidOperationException("No signed-in user.");

    /// <summary>
    /// Gets the signed-in account's username.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>The username.</returns>
    public static string Username(this ClaimsPrincipal principal) => principal.Identity?.Name ?? "User";

    /// <summary>
    /// Tells whether the signed-in account is the admin.
    /// </summary>
    /// <param name="principal">The principal.</param>
    /// <returns>True for the admin.</returns>
    public static bool IsAdmin(this ClaimsPrincipal principal) => principal.HasClaim(AdminClaim, "1");
}
