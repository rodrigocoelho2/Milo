using BodyOps.Library.Enums;
using BodyOps.Library.Services;
using BodyOps.WebApi.Extensions.Globalization;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using BodyOps.WebApi.Features.Foods.Models.Entities;
using BodyOps.WebApi.Features.Foods.Repositories;
using BodyOps.WebApi.Features.Plans.Services;

namespace BodyOps.WebApi.Features.Foods.Services;

/// <summary>
/// The account's foods (matched to the plan by name), Milo's built-in list and Open Food Facts.
/// </summary>
/// <param name="foodRepository">Food data access.</param>
/// <param name="packaged">Open Food Facts.</param>
/// <param name="globalizationHelper">Translations.</param>
internal sealed class FoodService(
    IFoodRepository foodRepository,
    IPackagedFoodSource packaged,
    IGlobalizationHelper globalizationHelper) : IFoodService
{
    /// <summary>The longest food name (same as a plan food's).</summary>
    public const int MaxName = 80;

    /// <summary>The heaviest piece.</summary>
    public const decimal MaxPieceGrams = 5000;

    private readonly IFoodRepository _foodRepository = foodRepository;
    private readonly IPackagedFoodSource _packaged = packaged;
    private readonly IGlobalizationHelper _globalizationHelper = globalizationHelper;

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<FoodDTO>>> ListAsync(long userId) =>
        ServiceApiResponse<List<FoodDTO>>.Success((await _foodRepository.ListAsync(userId)).Select(static food => new FoodDTO(food)).ToList());

    /// <inheritdoc />
    public async Task<ServiceApiResponse<FoodDTO>> SaveAsync(long userId, long? foodId, FoodSaveDTO request)
    {
        var name = NameText.Clean(request.Name);
        if (name.Length is 0 or > MaxName)
        {
            return await _globalizationHelper.ValidationProblemAsync<FoodDTO>("name", nameof(TranslationKeys.InvalidName), MaxName);
        }

        foreach (var (field, value, max) in new[] { ("kcal", request.Kcal, FoodMath.MaxKcal), ("proteinG", request.ProteinG, FoodMath.MaxGrams), ("carbsG", request.CarbsG, FoodMath.MaxGrams), ("fatG", request.FatG, FoodMath.MaxGrams) })
        {
            if (value is < 0 || value > max)
            {
                return await _globalizationHelper.ValidationProblemAsync<FoodDTO>(field, nameof(TranslationKeys.InvalidFoodValue), max);
            }
        }

        if (request.PieceGrams is <= 0 or > MaxPieceGrams)
        {
            return await _globalizationHelper.ValidationProblemAsync<FoodDTO>("pieceGrams", nameof(TranslationKeys.InvalidFoodValue), MaxPieceGrams);
        }

        var food = new Food
        {
            Name = name,
            NameKey = Key(name),
            Basis = Enum.IsDefined(request.Basis) ? request.Basis : FoodBasis.Per100g,
            PieceGrams = request.PieceGrams is { } grams ? Math.Round(grams, 1) : null,
            PieceName = Truncate(NameText.Clean(request.PieceName), 20),
            Kcal = Round(request.Kcal),
            ProteinG = Round(request.ProteinG),
            CarbsG = Round(request.CarbsG),
            FatG = Round(request.FatG),
            Source = Enum.IsDefined(request.Source) ? request.Source : FoodSource.Own,
            SourceRef = Truncate(request.SourceRef?.Trim(), 40),
        };

        var (result, saved) = await _foodRepository.SaveAsync(userId, foodId, food);
        return result switch
        {
            1 when saved is not null => ServiceApiResponse<FoodDTO>.Success(new FoodDTO(saved)),
            2 => await _globalizationHelper.ValidationProblemAsync<FoodDTO>("name", nameof(TranslationKeys.FoodNameTaken), name),
            _ => await _globalizationHelper.NotFoundAsync<FoodDTO>(foodId ?? 0),
        };
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<FoodDTO>> DeleteAsync(long userId, long foodId) =>
        await _foodRepository.DeleteAsync(userId, foodId)
            ? ServiceApiResponse<FoodDTO>.Success()
            : await _globalizationHelper.NotFoundAsync<FoodDTO>(foodId);

    /// <inheritdoc />
    public async Task<ServiceApiResponse<FoodSearchDTO>> SearchAsync(long userId, string? query)
    {
        var foods = await _foodRepository.ListAsync(userId);
        var words = FoodMath.SearchKey(query).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var mine = words.Length == 0
            ? foods
            : foods.Where(food => words.All(word => food.NameKey.Split(' ').Any(part => part.StartsWith(word, StringComparison.Ordinal)))).ToList();
        var keys = foods.Select(static food => food.NameKey).ToHashSet();
        var refs = foods.Where(static food => food.SourceRef is not null).Select(static food => food.SourceRef!).ToHashSet();
        var milo = MiloFoodList.Search(query)
            .Select(food => MiloFoodList.Hit(food, query))
            .Where(hit => !refs.Contains(hit.SourceRef) && !keys.Contains(Key(hit.Name)))
            .ToList();

        return ServiceApiResponse<FoodSearchDTO>.Success(new FoodSearchDTO
        {
            Mine = mine.Take(12).Select(static food => new FoodDTO(food)).ToList(),
            Milo = milo,
        });
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<List<FoodHitDTO>>> PackagedAsync(string? query)
    {
        var text = NameText.Clean(query);
        if (text.Length is < 2 or > MaxName)
        {
            return ServiceApiResponse<List<FoodHitDTO>>.Success([]);
        }

        var (status, hits) = await _packaged.SearchAsync(text);
        return status switch
        {
            PackagedStatus.Ok => ServiceApiResponse<List<FoodHitDTO>>.Success(hits),
            PackagedStatus.SlowDown => ServiceApiResponse<List<FoodHitDTO>>.Unavailable(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.PackagedSlowDown))),
            _ => ServiceApiResponse<List<FoodHitDTO>>.Unavailable(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.PackagedUnavailable))),
        };
    }

    /// <inheritdoc />
    public async Task<ServiceApiResponse<FoodHitDTO>> BarcodeAsync(string? barcode)
    {
        var code = (barcode ?? string.Empty).Trim();
        if (code.Length is < 8 or > 14 || !code.All(char.IsAsciiDigit))
        {
            return await _globalizationHelper.ValidationProblemAsync<FoodHitDTO>("barcode", nameof(TranslationKeys.InvalidBarcode));
        }

        var (status, hit) = await _packaged.BarcodeAsync(code);
        return status switch
        {
            PackagedStatus.Ok when hit is not null => ServiceApiResponse<FoodHitDTO>.Success(hit),
            PackagedStatus.NotFound or PackagedStatus.Ok => await _globalizationHelper.NotFoundAsync<FoodHitDTO>(code),
            PackagedStatus.SlowDown => ServiceApiResponse<FoodHitDTO>.Unavailable(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.PackagedSlowDown))),
            _ => ServiceApiResponse<FoodHitDTO>.Unavailable(await _globalizationHelper.TranslateAsync(nameof(TranslationKeys.PackagedUnavailable))),
        };
    }

    /// <summary>
    /// The key a food is matched by: the name without accents, in lower case ("Pão" and "pao" are the same food).
    /// </summary>
    /// <param name="name">The name.</param>
    /// <returns>The key.</returns>
    public static string Key(string? name) => FoodMath.SearchKey(name);

    private static decimal? Round(decimal? value) => value is { } v ? Math.Round(v, 1, MidpointRounding.AwayFromZero) : null;

    private static string? Truncate(string? value, int length) =>
        string.IsNullOrEmpty(value) ? null : value.Length > length ? value[..length] : value;
}
