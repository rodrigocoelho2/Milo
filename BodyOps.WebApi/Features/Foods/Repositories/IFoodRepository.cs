using BodyOps.WebApi.Features.Foods.Models.Entities;

namespace BodyOps.WebApi.Features.Foods.Repositories;

/// <summary>
/// The account's foods (dbo.Food).
/// </summary>
internal interface IFoodRepository
{
    /// <summary>
    /// Lists the foods.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The foods, by name.</returns>
    Task<List<Food>> ListAsync(long userId);

    /// <summary>
    /// Adds or changes a food (without an id, a food with the same name is changed).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="foodId">The food to change, or null.</param>
    /// <param name="food">The values (Name and NameKey filled in).</param>
    /// <returns>1 and the food when saved; 0 when <paramref name="foodId"/> doesn't exist; 2 when the name is taken.</returns>
    Task<(int Result, Food? Food)> SaveAsync(long userId, long? foodId, Food food);

    /// <summary>
    /// Removes a food.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="foodId">The food.</param>
    /// <returns>False when the account has no such food.</returns>
    Task<bool> DeleteAsync(long userId, long foodId);

    /// <summary>
    /// Adds the foods of a backup (names the account already has are skipped).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="foods">The foods.</param>
    /// <returns>A task that completes when they are written.</returns>
    Task ImportAsync(long userId, IEnumerable<Food> foods);
}
