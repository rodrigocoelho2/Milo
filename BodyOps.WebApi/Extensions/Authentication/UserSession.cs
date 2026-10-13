using BodyOps.WebApi.Features.Accounts.Models.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// Signs the current request in and out (the login cookie).
/// </summary>
internal interface IUserSession
{
    /// <summary>
    /// Issues the login cookie for an account.
    /// </summary>
    /// <param name="user">The account.</param>
    /// <param name="remember">Keep the cookie after the browser closes.</param>
    /// <returns>A task that completes when the cookie is set.</returns>
    Task SignInAsync(UserAccount user, bool remember);

    /// <summary>
    /// Removes the login cookie.
    /// </summary>
    /// <returns>A task that completes when the cookie is removed.</returns>
    Task SignOutAsync();
}

/// <summary>
/// Cookie sign-in for the current HTTP request.
/// </summary>
/// <param name="httpContextAccessor">The current request.</param>
internal sealed class UserSession(IHttpContextAccessor httpContextAccessor) : IUserSession
{
    private readonly IHttpContextAccessor _httpContextAccessor = httpContextAccessor;

    private HttpContext Http => _httpContextAccessor.HttpContext ?? throw new InvalidOperationException("No current HTTP request.");

    /// <inheritdoc />
    public Task SignInAsync(UserAccount user, bool remember) =>
        Http.SignInAsync(CookieAuthenticationDefaults.AuthenticationScheme, ClaimsPrincipalExtensions.CreatePrincipal(user),
            new AuthenticationProperties { IsPersistent = remember });

    /// <inheritdoc />
    public Task SignOutAsync() => Http.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
}
