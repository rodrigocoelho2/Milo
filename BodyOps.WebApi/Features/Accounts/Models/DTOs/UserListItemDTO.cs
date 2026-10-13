using BodyOps.WebApi.Features.Accounts.Models.Entities;

namespace BodyOps.WebApi.Features.Accounts.Models.DTOs;

/// <summary>
/// One account in Settings › Users.
/// </summary>
public sealed class UserListItemDTO
{
    /// <summary>
    /// Initializes an empty DTO (serialization).
    /// </summary>
    public UserListItemDTO()
    {
    }

    /// <summary>
    /// Maps a list row.
    /// </summary>
    /// <param name="entity">The row.</param>
    public UserListItemDTO(UserAccountListItem entity)
    {
        Id = entity.UserID;
        Username = entity.Username;
        IsAdmin = entity.IsAdmin;
        CreatedAt = entity.CreatedAt;
        LastLoginAt = entity.LastLoginAt;
        PlannedExercises = entity.PlannedExercises;
        PlannedMeals = entity.PlannedMeals;
        LastLoggedDay = entity.LastLoggedDay is { } day ? DateOnly.FromDateTime(day) : null;
    }

    /// <summary>Gets or sets the account identifier.</summary>
    public long Id { get; set; }

    /// <summary>Gets or sets the username.</summary>
    public string Username { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the account is the server admin.</summary>
    public bool IsAdmin { get; set; }

    /// <summary>Gets or sets when the account was created (UTC).</summary>
    public DateTime CreatedAt { get; set; }

    /// <summary>Gets or sets the last sign-in (UTC).</summary>
    public DateTime? LastLoginAt { get; set; }

    /// <summary>Gets or sets how many exercises the weekly plan has.</summary>
    public int PlannedExercises { get; set; }

    /// <summary>Gets or sets how many meals the weekly plan has.</summary>
    public int PlannedMeals { get; set; }

    /// <summary>Gets or sets the most recent day with something logged.</summary>
    public DateOnly? LastLoggedDay { get; set; }
}
