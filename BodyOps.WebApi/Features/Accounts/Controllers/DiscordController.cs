using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;
using BodyOps.WebApi.Features.Accounts.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Accounts.Controllers;

/// <summary>
/// "Sign in with Discord": off to Discord and back, then sign in to the linked account, link it to the signed-in
/// account (from Settings), or pick a user name for a new account. Only on when DISCORD_CLIENT_ID and
/// DISCORD_CLIENT_SECRET are set.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="accountService">Account business logic.</param>
/// <param name="options">The BodyOps options.</param>
[Authorize]
[Route("api/auth/discord")]
[ApiController]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
internal sealed class DiscordController(
    ILogger<DiscordController> logger,
    IAccountService accountService,
    IOptions<BodyOpsOptions> options) : InternalControllerBase
{
    private readonly ILogger<DiscordController> _logger = logger;
    private readonly IAccountService _accountService = accountService;
    private readonly IOptions<BodyOpsOptions> _options = options;

    /// <summary>
    /// Sends the browser to Discord to sign in.
    /// </summary>
    /// <returns>A redirect to Discord.</returns>
    [AllowAnonymous]
    [HttpGet]
    [SwaggerOperation(Summary = "DiscordSignIn", OperationId = "DiscordSignIn")]
    public IActionResult DiscordSignIn()
    {
        if (!_options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/discord/done" };
        properties.Items[DiscordAuthentication.ModeKey] = "signin";
        return Challenge(properties, DiscordAuthentication.Scheme);
    }

    /// <summary>
    /// Sends the browser to Discord to link a Discord account to the signed-in account. A plain link (the app's header
    /// can't ride along on a page change): a forged one only links the visitor's own Discord to their own account.
    /// </summary>
    /// <returns>A redirect to Discord.</returns>
    [HttpGet("link")]
    [SwaggerOperation(Summary = "DiscordLink", OperationId = "DiscordLink")]
    public IActionResult DiscordLink()
    {
        if (!_options.Value.DiscordEnabled)
        {
            return NotFound();
        }

        var properties = new AuthenticationProperties { RedirectUri = "/api/auth/discord/done" };
        properties.Items[DiscordAuthentication.ModeKey] = "link";
        return Challenge(properties, DiscordAuthentication.Scheme);
    }

    /// <summary>
    /// Back from Discord: signs in, links (from Settings), or sends someone new to pick a user name.
    /// </summary>
    /// <returns>A redirect into the app.</returns>
    [AllowAnonymous]
    [HttpGet("done")]
    [SwaggerOperation(Summary = "DiscordDone", OperationId = "DiscordDone")]
    public async Task<IActionResult> DiscordDoneAsync()
    {
        var account = _options.Value.DiscordEnabled ? await DiscordAuthentication.PeekAsync(HttpContext) : null;
        if (account is null)
        {
            return Redirect("/signin?discord=failed");
        }

        if (account.Mode == "link")
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            if (User.UserID() is not { } me)
            {
                return Redirect("/signin");
            }

            var linked = await _accountService.DiscordLinkAsync(me, account.Id, account.Name);
            return Redirect(linked.IsSuccess ? "/settings?discord=linked" : "/settings?discord=taken");
        }

        var signedIn = await _accountService.DiscordSignInAsync(account.Id);
        if (signedIn.IsSuccess)
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
            return Redirect("/today");
        }

        // Someone new: pick a user name (Discord's answer stays in its cookie for a few minutes).
        if (_options.Value.AllowRegistration)
        {
            return Redirect("/signup/discord");
        }

        if (_logger.IsEnabled(LogLevel.Warning))
            _logger.LogWarning("Discord sign-in with an unlinked Discord account");

        await DiscordAuthentication.ForgetAsync(HttpContext);
        return Redirect("/signin?discord=not-linked");
    }

    /// <summary>
    /// The Discord account waiting for a user name (for the "Almost there" form).
    /// </summary>
    /// <returns>Its name and a suggested user name, or 404.</returns>
    [AllowAnonymous]
    [HttpGet("pending")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType<DiscordPendingDTO>((int)HttpStatusCode.OK)]
    [ProducesResponseType((int)HttpStatusCode.NotFound)]
    [SwaggerOperation(Summary = "DiscordPending", OperationId = "DiscordPending")]
    public async Task<ActionResult<DiscordPendingDTO>> DiscordPendingAsync()
    {
        var account = _options.Value.DiscordEnabled ? await DiscordAuthentication.PeekAsync(HttpContext) : null;
        if (account is null || account.Mode == "link")
        {
            return NotFound();
        }

        return new DiscordPendingDTO { Name = account.Name, Suggestion = UserRules.SuggestUserName(account.Handle, account.Name, 24) };
    }

    /// <summary>
    /// Creates the account for the Discord account waiting, with the user name picked, and signs it in.
    /// </summary>
    /// <param name="request">The user name.</param>
    /// <returns>The new account.</returns>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    [Consumes(MediaTypeNames.Application.Json)]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType<MeDTO>((int)HttpStatusCode.Created)]
    [SwaggerOperation(Summary = "DiscordRegister", OperationId = "DiscordRegister")]
    public async Task<ActionResult<MeDTO>> DiscordRegisterAsync([FromBody] DiscordRegisterDTO request)
    {
        var account = _options.Value.DiscordEnabled ? await DiscordAuthentication.PeekAsync(HttpContext) : null;
        if (account is null || account.Mode == "link")
        {
            return NotFound();
        }

        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("DiscordRegister {Username}", request.Username);

        var apiResponse = await _accountService.DiscordRegisterAsync(request.Username, account.Id, account.Name);
        if (apiResponse.IsSuccess)
        {
            await DiscordAuthentication.ForgetAsync(HttpContext);
        }

        return apiResponse.ToActionResult("/api/auth/me");
    }

    /// <summary>
    /// Unlinks Discord from the signed-in account (refused while it is the only way in).
    /// </summary>
    /// <returns>No content.</returns>
    [HttpPost("unlink")]
    [Produces(MediaTypeNames.Application.Json)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(Summary = "DiscordUnlink", OperationId = "DiscordUnlink")]
    public async Task<ActionResult<string>> DiscordUnlinkAsync() => (await _accountService.DiscordUnlinkAsync(User.RequiredUserID())).ToActionResult();
}
