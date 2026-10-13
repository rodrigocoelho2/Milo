namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// Sign-in / sign-up request.
/// </summary>
public sealed class CredentialsDTO
{
    /// <summary>Gets or sets the username.</summary>
    public string? Username { get; set; }

    /// <summary>Gets or sets the password.</summary>
    public string? Password { get; set; }

    /// <summary>Gets or sets a value indicating whether to stay signed in after the browser closes.</summary>
    public bool Remember { get; set; } = true;
}
