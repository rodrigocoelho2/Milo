namespace BodyOps.WebApi.Features.Accounts.Models.Entities;

/// <summary>
/// One row of dbo.User. Property names are the column names returned by the stored procedures — do not rename.
/// </summary>
public sealed class UserAccount
{
    /// <summary>Gets or sets the account identifier (identity).</summary>
    public long UserID { get; set; }

    /// <summary>Gets or sets the account's stable public identifier.</summary>
    public Guid UserUniqueID { get; set; }

    /// <summary>Gets or sets the username (unique, ignoring case).</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets the password hash (see PasswordHasher).</summary>
    public string PasswordHash { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is the server admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets the security stamp; changing it signs the account out everywhere.</summary>
    public string SecurityStamp { get; set; } = string.Empty;

    /// <summary>Gets or sets when the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the last sign-in (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }
}
