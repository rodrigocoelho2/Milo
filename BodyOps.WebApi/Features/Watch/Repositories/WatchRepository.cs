using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Watch.Models.Entities;

namespace BodyOps.WebApi.Features.Watch.Repositories;

/// <summary>
/// Shortcut keys and Watch workouts through the dbo.Watch* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class WatchRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IWatchRepository
{
    /// <inheritdoc />
    public Task<WatchKeyRow?> GetKeyAsync(long userId) => QuerySingleOrDefaultAsync<WatchKeyRow>("[dbo].[WatchKeyGet]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<WatchKeyRow> SaveKeyAsync(long userId, byte[] keyHash, string keyHint) =>
        (await QuerySingleOrDefaultAsync<WatchKeyRow>("[dbo].[WatchKeySave]", new { UserID = userId, KeyHash = keyHash, KeyHint = keyHint }))!;

    /// <inheritdoc />
    public Task DeleteKeyAsync(long userId) => ExecuteAsync("[dbo].[WatchKeyDelete]", new { UserID = userId });

    /// <inheritdoc />
    public Task<WatchKeyRow?> UseKeyAsync(byte[] keyHash) => QuerySingleOrDefaultAsync<WatchKeyRow>("[dbo].[WatchKeyUse]", new { KeyHash = keyHash });

    /// <inheritdoc />
    public Task MarkKeyAsync(long userId, string error) =>
        ExecuteAsync("[dbo].[WatchKeyMark]", new { UserID = userId, LastError = error.Length <= 200 ? error : error[..200] });

    /// <inheritdoc />
    public Task<List<WatchWorkoutRow>> ListWorkoutsAsync(long userId, DateOnly from, DateOnly to) =>
        QueryAsync<WatchWorkoutRow>("[dbo].[WatchWorkoutList]", new { UserID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public async Task<WatchWorkoutRow> SaveWorkoutAsync(long userId, WatchWorkoutRow workout) =>
        (await QuerySingleOrDefaultAsync<WatchWorkoutRow>("[dbo].[WatchWorkoutSave]", new
        {
            UserID = userId,
            workout.DayDate,
            workout.StartedAt,
            workout.EndedAt,
            Kind = workout.Kind.Length <= 60 ? workout.Kind : workout.Kind[..60],
            workout.DurationSeconds,
            workout.ActiveKcal,
            workout.AvgBpm,
            workout.MaxBpm,
            workout.DistanceKm,
            HeartRates = workout.HeartRates.Length <= 1000 ? workout.HeartRates : workout.HeartRates[..1000],
            workout.PlanExerciseID,
        }))!;
}
