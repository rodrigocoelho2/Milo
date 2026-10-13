using System.Data;
using System.Globalization;
using System.Text.Json;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Weights.Models.Entities;
using Dapper;

namespace BodyOps.WebApi.Features.Weights.Repositories;

/// <summary>
/// Weigh-ins through the dbo.Weight* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class WeightRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IWeightRepository
{
    /// <inheritdoc />
    public Task<List<WeightEntry>> ListAsync(long userId) =>
        QueryAsync<WeightEntry>("[dbo].[WeightList]", new { UserID = userId });

    /// <inheritdoc />
    public Task SaveAsync(long userId, DateOnly date, decimal weightKg) =>
        ExecuteAsync("[dbo].[WeightSave]", new { UserID = userId, EntryDate = Date(date), WeightKg = weightKg });

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long userId, DateOnly date)
    {
        var parameters = new DynamicParameters(new { UserID = userId, EntryDate = Date(date) });
        parameters.Add("Deleted", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await ExecuteAsync("[dbo].[WeightDelete]", parameters);
        return parameters.Get<bool>("Deleted");
    }

    /// <inheritdoc />
    public Task ImportAsync(long userId, IEnumerable<(DateOnly Date, decimal WeightKg)> entries) =>
        ExecuteAsync("[dbo].[WeightImport]", new
        {
            UserID = userId,
            Json = JsonSerializer.Serialize(entries.Select(static entry => new
            {
                date = entry.Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
                weightKg = entry.WeightKg,
            })),
        });
}
