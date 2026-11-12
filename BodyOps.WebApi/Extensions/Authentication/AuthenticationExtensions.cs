using System.Security.Claims;
using System.Threading.RateLimiting;
using BodyOps.WebApi.Features;
using BodyOps.WebApi.Features.Accounts.Repositories;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.DataProtection;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// Cookie authentication, the admin policy and the sign-in rate limit.
/// </summary>
internal static class AuthenticationExtensions
{
    /// <summary>
    /// Registers authentication, authorization and rate limiting.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="settings">The BodyOps options (data folder already resolved).</param>
    /// <returns>The service collection.</returns>
    public static IServiceCollection AddBodyOpsAuthentication(this IServiceCollection services, BodyOpsOptions settings)
    {
        // Login cookies are encrypted with these keys; keeping them in the data volume means
        // people stay signed in across container restarts and image updates.
        services.AddDataProtection()
            .SetApplicationName("BodyOps")
            .PersistKeysToFileSystem(new DirectoryInfo(Path.Combine(settings.DataDirectory, "keys")));

        var authentication = services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
            .AddCookie(options =>
            {
                options.Cookie.Name = "bodyops.auth";
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;
                options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;   // works on plain http inside the LAN
                options.ExpireTimeSpan = TimeSpan.FromDays(60);
                options.SlidingExpiration = true;

                // This is an API for a single page: answer with status codes instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = static context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
                options.Events.OnValidatePrincipal = ValidatePrincipalAsync;
            });

        if (settings.DiscordEnabled)
        {
            authentication.AddDiscord(settings);
        }

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Admin, static policy => policy.RequireAuthenticatedUser().RequireClaim(ClaimsPrincipalExtensions.AdminClaim, "1"));

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.AddPolicy(RateLimitPolicies.Auth, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions
                {
                    PermitLimit = Math.Max(1, settings.AuthAttemptsPerMinute),
                    Window = TimeSpan.FromMinutes(1),
                    QueueLimit = 0,
                }));
            options.AddPolicy(RateLimitPolicies.Watch, http => RateLimitPartition.GetFixedWindowLimiter(
                http.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                _ => new FixedWindowRateLimiterOptions { PermitLimit = 30, Window = TimeSpan.FromMinutes(1), QueueLimit = 0 }));
        });

        services.AddHttpContextAccessor();
        services.AddSingleton<IUserSession, UserSession>();
        return services;
    }

    /// <summary>
    /// Runs on every authenticated request: a deleted account, or one whose password was changed or reset
    /// elsewhere, no longer matches its cookie and is signed out.
    /// </summary>
    /// <param name="context">The cookie validation context.</param>
    /// <returns>A task that completes when validated.</returns>
    private static async Task ValidatePrincipalAsync(CookieValidatePrincipalContext context)
    {
        var principal = context.Principal;
        var userId = principal?.UserID();
        var userRepository = context.HttpContext.RequestServices.GetRequiredService<IUserRepository>();
        var user = userId is null ? null : await userRepository.GetAsync(userId.Value);

        if (principal is null || user is null || user.SecurityStamp != principal.FindFirstValue(ClaimsPrincipalExtensions.StampClaim))
        {
            context.RejectPrincipal();
            await context.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return;
        }

        // Admin rights can move to another account when the admin deletes theirs.
        if (user.IsAdmin != principal.IsAdmin() || user.Username != principal.Identity?.Name)
        {
            context.ReplacePrincipal(ClaimsPrincipalExtensions.CreatePrincipal(user));
            context.ShouldRenew = true;
        }
    }
}
