namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// Delete-my-account request.
/// </summary>
public sealed class AccountDeleteDTO
{
    /// <summary>Gets or sets the password, as confirmation.</summary>
    public string? Password { get; set; }
}
