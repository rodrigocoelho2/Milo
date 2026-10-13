using BodyOps.WebApi.Features.Accounts.Models.Entities;

namespace BodyOps.WebApi.Features.Accounts.Models;

/// <summary>
/// The @Outcome of dbo.UserCreate.
/// </summary>
public enum UserCreateOutcome
{
    /// <summary>The account was created.</summary>
    Created = 0,

    /// <summary>Another account already uses the username.</summary>
    UsernameTaken = 1,

    /// <summary>Sign-up is closed and this is not the first account.</summary>
    RegistrationClosed = 2,
}

/// <summary>
/// The result of creating an account.
/// </summary>
/// <param name="Outcome">What happened.</param>
/// <param name="User">The new account (only when created).</param>
public sealed record UserCreateResult(UserCreateOutcome Outcome, UserAccount? User);
