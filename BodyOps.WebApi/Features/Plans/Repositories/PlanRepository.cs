using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Plans.Models.Entities;
using Dapper;

namespace BodyOps.WebApi.Features.Plans.Repositories;

/// <summary>
/// The weekly plan through the dbo.Plan* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class PlanRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IPlanRepository
{
    /// <inheritdoc />
    public Task<PlanData> GetAsync(long userId, int? weekday = null) =>
        QueryMultipleAsync("[dbo].[PlanGet]", new { UserID = userId, Weekday = weekday }, static async grid =>
            new PlanData(
                (await grid.ReadAsync<PlanMeal>()).AsList(),
                (await grid.ReadAsync<PlanFood>()).AsList(),
                (await grid.ReadAsync<PlanExercise>()).AsList())
            {
                Names = (await grid.ReadAsync<PlanDayName>()).AsList(),
            });

    /// <inheritdoc />
    public Task<PlanData> SaveMealsAsync(long userId, int weekday, string meals, DateOnly syncFrom) =>
        QueryMultipleAsync("[dbo].[PlanMealsSave]", new { UserID = userId, Weekday = weekday, Json = meals, SyncFrom = Date(syncFrom) }, static async grid =>
            new PlanData(
                (await grid.ReadAsync<PlanMeal>()).AsList(),
                (await grid.ReadAsync<PlanFood>()).AsList(),
                []));

    /// <inheritdoc />
    public Task<List<PlanExercise>> SaveExercisesAsync(long userId, int weekday, string exercises, DateOnly syncFrom) =>
        QueryAsync<PlanExercise>("[dbo].[PlanExercisesSave]", new { UserID = userId, Weekday = weekday, Json = exercises, SyncFrom = Date(syncFrom) });

    /// <inheritdoc />
    public Task CopyDayAsync(long userId, int fromWeekday, IReadOnlyCollection<int> toWeekdays, PlanPart part, DateOnly syncFrom) =>
        ExecuteAsync("[dbo].[PlanCopyDay]", new
        {
            UserID = userId,
            FromWeekday = fromWeekday,
            ToWeekdays = JsonSerializer.Serialize(toWeekdays),
            Part = (int)part,
            SyncFrom = Date(syncFrom),
        });

    /// <inheritdoc />
    public Task SaveDayNameAsync(long userId, int weekday, string name) =>
        ExecuteAsync("[dbo].[PlanDayNameSave]", new { UserID = userId, Weekday = weekday, Name = name });
}
