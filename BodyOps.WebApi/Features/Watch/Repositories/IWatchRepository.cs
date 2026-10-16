using BodyOps.WebApi.Features.Watch.Models.Entities;

namespace BodyOps.WebApi.Features.Watch.Repositories;

/// <summary>
/// The Shortcut keys (dbo.WatchKey) and the Watch workouts (dbo.WatchWorkout).
/// </summary>
internal interface IWatchRepository
{
    /// <summary>Gets the account's key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>The key without its hash, or null.</returns>
    Task<WatchKeyRow?> GetKeyAsync(long userId);

    /// <summary>Sets the account's key, replacing the old one.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="keyHash">The SHA-256 of the key.</param>
    /// <param name="keyHint">Its start and end.</param>
    /// <returns>The key row.</returns>
    Task<WatchKeyRow> SaveKeyAsync(long userId, byte[] keyHash, string keyHint);

    /// <summary>Revokes the account's key.</summary>
    /// <param name="userId">The account.</param>
    /// <returns>A task.</returns>
    Task DeleteKeyAsync(long userId);

    /// <summary>Finds the account of a key and records the use.</summary>
    /// <param name="keyHash">The SHA-256 of the key.</param>
    /// <returns>The key row, or null for an unknown key.</returns>
    Task<WatchKeyRow?> UseKeyAsync(byte[] keyHash);

    /// <summary>Records how the last sync went.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="error">The error, or empty.</param>
    /// <returns>A task.</returns>
    Task MarkKeyAsync(long userId, string error);

    /// <summary>Lists the workouts between two days.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="from">The first day.</param>
    /// <param name="to">The last day.</param>
    /// <returns>The workouts, oldest first.</returns>
    Task<List<WatchWorkoutRow>> ListWorkoutsAsync(long userId, DateOnly from, DateOnly to);

    /// <summary>Adds a workout, or updates the one with the same start.</summary>
    /// <param name="userId">The account.</param>
    /// <param name="workout">The workout (its id is ignored; a null PlanExerciseID keeps the stored one).</param>
    /// <returns>The saved workout.</returns>
    Task<WatchWorkoutRow> SaveWorkoutAsync(long userId, WatchWorkoutRow workout);
}
