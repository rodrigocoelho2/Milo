namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// Change-password request.
/// </summary>
public sealed class PasswordChangeDTO
{
    /// <summary>Gets or sets the current password.</summary>
    public string? CurrentPassword { get; set; }

    /// <summary>Gets or sets the new password.</summary>
    public string? NewPassword { get; set; }
}
