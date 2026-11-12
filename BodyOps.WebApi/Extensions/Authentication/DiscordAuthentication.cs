using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.OAuth;
using BodyOps.WebApi.Features;

namespace BodyOps.WebApi.Extensions.Authentication;

/// <summary>
/// "Sign in with Discord": Discord's OAuth2 (scope identify only) through ASP.NET Core's own OAuth handler. Discord's
/// answer lands in a short-lived cookie of its own; <c>/api/auth/discord/done</c> reads it and signs in to the account
/// that Discord account is linked to (or links it, from Settings). A Discord account that isn't linked yet can create a
/// new account (when new accounts are allowed): the cookie stays while the person picks a user name.
/// </summary>
internal static class DiscordAuthentication
{
    /// <summary>The OAuth scheme.</summary>
    public const string Scheme = "Discord";

    /// <summary>The cookie that holds Discord's answer for a moment.</summary>
    public const string ExternalScheme = "DiscordExternal";

    /// <summary>Where Discord sends the browser back (add it in the Discord app's OAuth2 redirects).</summary>
    public const string CallbackPath = "/signin-discord";

    /// <summary>The claim with the Discord account's id.</summary>
    public const string IdClaim = "discord:id";

    /// <summary>The claim with the Discord account's name.</summary>
    public const string NameClaim = "discord:name";

    /// <summary>The claim with the Discord account's user name (the handle, suggested as the user name).</summary>
    public const string UserNameClaim = "discord:username";

    /// <summary>The key, in the sign-in's properties, of what it's for ("signin" or "link").</summary>
    public const string ModeKey = "ludex:discord-mode";

    /// <summary>Adds the Discord scheme and its cookie.</summary>
    /// <param name="builder">The authentication builder.</param>
    /// <param name="options">The BodyOps options (client id and secret).</param>
    /// <returns>The builder.</returns>
    public static AuthenticationBuilder AddDiscord(this AuthenticationBuilder builder, BodyOpsOptions options)
    {
        builder.AddCookie(ExternalScheme, cookie =>
        {
            cookie.Cookie.Name = "ludex.discord";
            cookie.Cookie.HttpOnly = true;
            cookie.Cookie.SameSite = SameSiteMode.Lax;
            cookie.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;
            cookie.ExpireTimeSpan = TimeSpan.FromMinutes(5);
        });

        builder.AddOAuth(Scheme, oauth =>
        {
            oauth.SignInScheme = ExternalScheme;
            oauth.ClientId = options.DiscordClientId.Trim();
            oauth.ClientSecret = options.DiscordClientSecret.Trim();
            oauth.CallbackPath = CallbackPath;
            oauth.AuthorizationEndpoint = "https://discord.com/oauth2/authorize";
            oauth.TokenEndpoint = "https://discord.com/api/oauth2/token";
            oauth.UserInformationEndpoint = "https://discord.com/api/users/@me";
            oauth.UsePkce = true;
            oauth.SaveTokens = false;
            oauth.Scope.Clear();
            oauth.Scope.Add("identify");

            // The NAS is often plain HTTP on the LAN: the correlation cookie must work without HTTPS, and Lax is enough
            // for Discord's redirect back (a top-level GET).
            oauth.CorrelationCookie.SameSite = SameSiteMode.Lax;
            oauth.CorrelationCookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;

            oauth.Events.OnCreatingTicket = async context =>
            {
                using var request = new HttpRequestMessage(HttpMethod.Get, context.Options.UserInformationEndpoint);
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", context.AccessToken);
                request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
                using var response = await context.Backchannel.SendAsync(request, context.HttpContext.RequestAborted);
                response.EnsureSuccessStatusCode();

                using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync(context.HttpContext.RequestAborted));
                var root = json.RootElement;
                var id = root.TryGetProperty("id", out var idValue) ? idValue.GetString() : null;
                if (string.IsNullOrEmpty(id))
                {
                    context.Fail("Discord didn't say who you are.");
                    return;
                }

                var name = Text(root, "global_name") ?? Text(root, "username") ?? id;
                context.Identity?.AddClaim(new Claim(IdClaim, id));
                context.Identity?.AddClaim(new Claim(NameClaim, name));
                context.Identity?.AddClaim(new Claim(UserNameClaim, Text(root, "username") ?? string.Empty));
            };

            // Cancelled on Discord, or anything else that went wrong: back to the sign-in page with a note, never an error page.
            oauth.Events.OnRemoteFailure = context =>
            {
                var link = context.Properties?.Items.TryGetValue(ModeKey, out var mode) == true && mode == "link";
                context.Response.Redirect(link ? "/settings?discord=failed" : "/signin?discord=failed");
                context.HandleResponse();
                return Task.CompletedTask;
            };
        });

        return builder;
    }

    /// <summary>The Discord account Discord just confirmed, from the short-lived cookie (which stays).</summary>
    /// <param name="context">The request.</param>
    /// <returns>The account and what the sign-in was for; null when there's none.</returns>
    public static async Task<DiscordAccount?> PeekAsync(HttpContext context)
    {
        var result = await context.AuthenticateAsync(ExternalScheme);
        if (!result.Succeeded || result.Principal is null)
        {
            return null;
        }

        var id = result.Principal.FindFirstValue(IdClaim);
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var mode = result.Properties?.Items.TryGetValue(ModeKey, out var value) == true && value is not null ? value : "signin";
        return new DiscordAccount(id, result.Principal.FindFirstValue(NameClaim) ?? id, result.Principal.FindFirstValue(UserNameClaim) ?? string.Empty, mode);
    }

    /// <summary>Drops the short-lived cookie once Discord's answer has been used.</summary>
    /// <param name="context">The request.</param>
    /// <returns>A task.</returns>
    public static Task ForgetAsync(HttpContext context) => context.SignOutAsync(ExternalScheme);


    private static string? Text(JsonElement root, string name) =>
        root.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()
            : null;
}

/// <summary>A Discord account Discord just confirmed.</summary>
/// <param name="Id">The Discord account's id.</param>
/// <param name="Name">Its display name.</param>
/// <param name="Handle">Its user name.</param>
/// <param name="Mode">What the sign-in was for ("signin" or "link").</param>
internal sealed record DiscordAccount(string Id, string Name, string Handle, string Mode);
