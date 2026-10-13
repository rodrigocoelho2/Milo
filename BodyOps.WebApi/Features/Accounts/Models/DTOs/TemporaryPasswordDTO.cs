namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// The password set by an admin reset; shown once to the admin.
/// </summary>
public sealed class TemporaryPasswordDTO
{
    /// <summary>Gets or sets the temporary password.</summary>
    public string TemporaryPassword { get; set; } = string.Empty;
}
