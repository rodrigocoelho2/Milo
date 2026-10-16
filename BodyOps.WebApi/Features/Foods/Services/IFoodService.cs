using BodyOps.Library.Services;
using BodyOps.WebApi.Features.Foods.Models.DTOs;

namespace BodyOps.WebApi.Features.Foods.Services;

/// <summary>
/// The account's foods, Milo's list and packaged foods.
/// </summary>
internal interface IFoodService
{
    /// <summary>
    /// Lists the account's foods.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <returns>The foods, by name.</returns>
    Task<ServiceApiResponse<List<FoodDTO>>> ListAsync(long userId);

    /// <summary>
    /// Adds a food, or changes the one with the same name (or <paramref name="foodId"/>).
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="foodId">The food to change, or null.</param>
    /// <param name="request">The food.</param>
    /// <returns>The saved food.</returns>
    Task<ServiceApiResponse<FoodDTO>> SaveAsync(long userId, long? foodId, FoodSaveDTO request);

    /// <summary>
    /// Removes a food.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="foodId">The food.</param>
    /// <returns>No content, or not found.</returns>
    Task<ServiceApiResponse<FoodDTO>> DeleteAsync(long userId, long foodId);

    /// <summary>
    /// Searches the account's foods and Milo's list.
    /// </summary>
    /// <param name="userId">The account.</param>
    /// <param name="query">What was typed.</param>
    /// <returns>The matches.</returns>
    Task<ServiceApiResponse<FoodSearchDTO>> SearchAsync(long userId, string? query);

    /// <summary>
    /// Searches packaged foods on Open Food Facts.
    /// </summary>
    /// <param name="query">The product name.</param>
    /// <returns>The products.</returns>
    Task<ServiceApiResponse<List<FoodHitDTO>>> PackagedAsync(string? query);

    /// <summary>
    /// Looks up a barcode on Open Food Facts.
    /// </summary>
    /// <param name="barcode">The barcode.</param>
    /// <returns>The product.</returns>
    Task<ServiceApiResponse<FoodHitDTO>> BarcodeAsync(string? barcode);
}
