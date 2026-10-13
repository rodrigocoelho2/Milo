using System.Data;
using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Days.Models.Entities;
using Dapper;

namespace BodyOps.WebApi.Features.Days.Repositories;

/// <summary>
/// Logged days through the dbo.Day* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class DayRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IDayRepository
{
    /// <inheritdoc />
    public Task<DayRange> ListRangeAsync(long userId, DateOnly from, DateOnly to) =>
        QueryMultipleAsync("[dbo].[DayListRange]", new { UserID = userId, From = Date(from), To = Date(to) }, static async grid =>
            new DayRange(
                (await grid.ReadAsync<DayRow>()).AsList(),
                (await grid.ReadAsync<DayMealRow>()).AsList(),
                (await grid.ReadAsync<DayExerciseRow>()).AsList(),
                (await grid.ReadAsync<DaySetRow>()).AsList()));

    /// <inheritdoc />
    public Task<List<ExerciseLogRow>> ListExerciseLogAsync(long userId) =>
        QueryAsync<ExerciseLogRow>("[dbo].[ExerciseLogList]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<bool> SetMealEatenAsync(long userId, DateOnly date, long planMealId, bool eaten)
    {
        var parameters = new DynamicParameters(new { UserID = userId, DayDate = Date(date), PlanMealID = planMealId, Eaten = eaten });
        parameters.Add("Found", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await ExecuteAsync("[dbo].[DayMealSetEaten]", parameters);
        return parameters.Get<bool>("Found");
    }

    /// <inheritdoc />
    public Task SetAllMealsEatenAsync(long userId, DateOnly date, bool eaten) =>
        ExecuteAsync("[dbo].[DayMealSetAllEaten]", new { UserID = userId, DayDate = Date(date), Eaten = eaten });

    /// <inheritdoc />
    public Task SetTrainingStateAsync(long userId, DateOnly date, TrainingState state) =>
        ExecuteAsync("[dbo].[DaySetTrainingState]", new { UserID = userId, DayDate = Date(date), TrainingState = (byte)state });

    /// <inheritdoc />
    public async Task<bool> LogExerciseAsync(long userId, DateOnly date, long planExerciseId, decimal? loadKg, IReadOnlyList<int?> sets, decimal? cardioMinutes, decimal? cardioKm)
    {
        var parameters = new DynamicParameters(new
        {
            UserID = userId,
            DayDate = Date(date),
            PlanExerciseID = planExerciseId,
            LoadKg = loadKg,
            CardioMinutes = cardioMinutes,
            CardioKm = cardioKm,
            SetsJson = JsonSerializer.Serialize(sets),
        });
        parameters.Add("Found", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await ExecuteAsync("[dbo].[DayExerciseLog]", parameters);
        return parameters.Get<bool>("Found");
    }

    /// <inheritdoc />
    public Task DeleteRangeAsync(long userId, DateOnly from, DateOnly to) =>
        ExecuteAsync("[dbo].[DayDeleteRange]", new { UserID = userId, From = Date(from), To = Date(to) });

    /// <inheritdoc />
    public Task ImportAsync(long userId, string days) =>
        ExecuteAsync("[dbo].[DayImport]", new { UserID = userId, Json = days });
}
