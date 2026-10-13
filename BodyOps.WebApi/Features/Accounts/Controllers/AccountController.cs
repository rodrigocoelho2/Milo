using System.Net;
using System.Net.Mime;
using BodyOps.Library.Validation;
using BodyOps.WebApi.Extensions.Authentication;
using BodyOps.WebApi.Extensions.Mvc;
using BodyOps.WebApi.Extensions.Services;
using BodyOps.WebApi.Features.Accounts.Models.DTOs;
using BodyOps.WebApi.Features.Accounts.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Swashbuckle.AspNetCore.Annotations;

namespace BodyOps.WebApi.Features.Accounts.Controllers;

/// <summary>
/// The signed-in user's own account: sign-up, sign-in, sign-out, password and deletion.
/// </summary>
/// <param name="logger">Logger.</param>
/// <param name="accountService">Account business logic.</param>
[Authorize]
[Route("api/auth")]
[ApiController]
[Produces(MediaTypeNames.Application.Json)]
[ProducesErrorResponseType(typeof(ApiValidationProblemDetails))]
[ProducesResponseType((int)HttpStatusCode.BadRequest)]
[ProducesResponseType((int)HttpStatusCode.Unauthorized)]
internal sealed class AccountController(
    ILogger<AccountController> logger,
    IAccountService accountService) : InternalControllerBase
{
    private readonly ILogger<AccountController> _logger = logger;
    private readonly IAccountService _accountService = accountService;

    /// <summary>
    /// Gets the signed-in account.
    /// </summary>
    /// <returns>The account.</returns>
    [HttpGet("me")]
    [ProducesResponseType<MeDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AccountMe", OperationId = "AccountMe")]
    public async Task<ActionResult<MeDTO>> AccountMeAsync()
    {
        var apiResponse = await _accountService.MeAsync(User.RequiredUserID());
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Creates an account and signs it in. The first account on the server becomes the admin.
    /// </summary>
    /// <param name="request">The username and password.</param>
    /// <returns>The new account.</returns>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("register")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<MeDTO>((int)HttpStatusCode.Created)]
    [ProducesResponseType((int)HttpStatusCode.Forbidden)]
    [ProducesResponseType((int)HttpStatusCode.Conflict)]
    [SwaggerOperation(Summary = "AccountRegister", OperationId = "AccountRegister")]
    public async Task<ActionResult<MeDTO>> AccountRegisterAsync([FromBody] CredentialsDTO request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AccountRegister {Username}", request.Username);

        var apiResponse = await _accountService.RegisterAsync(request);
        return apiResponse.ToActionResult("/api/auth/me");
    }

    /// <summary>
    /// Signs in.
    /// </summary>
    /// <param name="request">The username and password.</param>
    /// <returns>The account.</returns>
    [AllowAnonymous]
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("login")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType<MeDTO>((int)HttpStatusCode.OK)]
    [SwaggerOperation(Summary = "AccountLogin", OperationId = "AccountLogin")]
    public async Task<ActionResult<MeDTO>> AccountLoginAsync([FromBody] CredentialsDTO request)
    {
        var apiResponse = await _accountService.LoginAsync(request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Signs out.
    /// </summary>
    /// <returns>No content.</returns>
    [AllowAnonymous]
    [HttpPost("logout")]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(Summary = "AccountLogout", OperationId = "AccountLogout")]
    public async Task<ActionResult<string>> AccountLogoutAsync()
    {
        var apiResponse = await _accountService.LogoutAsync();
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Changes the password; every other device is signed out.
    /// </summary>
    /// <param name="request">The current and the new password.</param>
    /// <returns>No content.</returns>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("password")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(Summary = "AccountPassword", OperationId = "AccountPassword")]
    public async Task<ActionResult<string>> AccountPasswordAsync([FromBody] PasswordChangeDTO request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AccountPassword {UserID}", User.UserID());

        var apiResponse = await _accountService.ChangePasswordAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }

    /// <summary>
    /// Deletes the signed-in account with its plan, logged days and weigh-ins.
    /// </summary>
    /// <param name="request">The password, as confirmation.</param>
    /// <returns>No content.</returns>
    [EnableRateLimiting(RateLimitPolicies.Auth)]
    [HttpPost("delete-account")]
    [Consumes(MediaTypeNames.Application.Json)]
    [ProducesResponseType((int)HttpStatusCode.NoContent)]
    [SwaggerOperation(Summary = "AccountDelete", OperationId = "AccountDelete")]
    public async Task<ActionResult<string>> AccountDeleteAsync([FromBody] AccountDeleteDTO request)
    {
        if (_logger.IsEnabled(LogLevel.Information))
            _logger.LogInformation("AccountDelete {UserID}", User.UserID());

        var apiResponse = await _accountService.DeleteAccountAsync(User.RequiredUserID(), request);
        return apiResponse.ToActionResult();
    }
}
