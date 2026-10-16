using System.Data;
using System.Text.Json;
using BodyOps.WebApi.Extensions.Data;
using BodyOps.WebApi.Features.Foods.Models.Entities;
using Dapper;

namespace BodyOps.WebApi.Features.Foods.Repositories;

/// <summary>
/// Foods through the dbo.Food* stored procedures.
/// </summary>
/// <param name="connectionFactory">Opens database connections.</param>
internal sealed class FoodRepository(ISqlConnectionFactory connectionFactory) : RepositoryBase(connectionFactory), IFoodRepository
{
    /// <inheritdoc />
    public Task<List<Food>> ListAsync(long userId) =>
        QueryAsync<Food>("[dbo].[FoodList]", new { UserID = userId });

    /// <inheritdoc />
    public async Task<(int Result, Food? Food)> SaveAsync(long userId, long? foodId, Food food)
    {
        var parameters = new DynamicParameters(new
        {
            UserID = userId,
            FoodID = foodId,
            food.Name,
            food.NameKey,
            Basis = (byte)food.Basis,
            food.PieceGrams,
            food.PieceName,
            food.Kcal,
            food.ProteinG,
            food.CarbsG,
            food.FatG,
            Source = (byte)food.Source,
            food.SourceRef,
        });
        parameters.Add("Result", dbType: DbType.Int32, direction: ParameterDirection.Output);

        var saved = await QuerySingleOrDefaultAsync<Food>("[dbo].[FoodSave]", parameters);
        return (parameters.Get<int>("Result"), saved);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteAsync(long userId, long foodId)
    {
        var parameters = new DynamicParameters(new { UserID = userId, FoodID = foodId });
        parameters.Add("Found", dbType: DbType.Boolean, direction: ParameterDirection.Output);

        await ExecuteAsync("[dbo].[FoodDelete]", parameters);
        return parameters.Get<bool>("Found");
    }

    /// <inheritdoc />
    public Task ImportAsync(long userId, IEnumerable<Food> foods) =>
        ExecuteAsync("[dbo].[FoodImport]", new
        {
            UserID = userId,
            Json = JsonSerializer.Serialize(foods.Select(static food => new
            {
                name = food.Name,
                nameKey = food.NameKey,
                basis = (int)food.Basis,
                pieceGrams = food.PieceGrams,
                pieceName = food.PieceName,
                kcal = food.Kcal,
                proteinG = food.ProteinG,
                carbsG = food.CarbsG,
                fatG = food.FatG,
                source = (int)food.Source,
                sourceRef = food.SourceRef,
            })),
        });
}
