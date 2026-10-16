using System.Globalization;
using System.Net;
using System.Text.Json;
using BodyOps.Library.Enums;
using BodyOps.WebApi.Features.Foods.Models.DTOs;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;

namespace BodyOps.WebApi.Features.Foods.Services;

/// <summary>
/// The outcome of a packaged-food lookup.
/// </summary>
internal enum PackagedStatus
{
    /// <summary>Answered (possibly with nothing found).</summary>
    Ok,

    /// <summary>The barcode isn't on Open Food Facts.</summary>
    NotFound,

    /// <summary>Open Food Facts didn't answer.</summary>
    Unavailable,

    /// <summary>Too many lookups in the last minute: Open Food Facts asks apps to keep it low.</summary>
    SlowDown,
}

/// <summary>
/// Packaged foods by name or barcode (Open Food Facts; faked in tests).
/// </summary>
internal interface IPackagedFoodSource
{
    /// <summary>
    /// Searches by name.
    /// </summary>
    /// <param name="query">The name.</param>
    /// <returns>The status and the products with values.</returns>
    Task<(PackagedStatus Status, List<FoodHitDTO> Hits)> SearchAsync(string query);

    /// <summary>
    /// Looks up a barcode.
    /// </summary>
    /// <param name="barcode">8–14 digits.</param>
    /// <returns>The status and the product.</returns>
    Task<(PackagedStatus Status, FoodHitDTO? Hit)> BarcodeAsync(string barcode);
}

/// <summary>
/// Open Food Facts (https://world.openfoodfacts.org), the open product database (ODbL). The server asks it, so the
/// page never talks to another site. Kept well under its limits (10 searches and 15 product reads a minute per
/// address) and cached for a day, so the same product is only fetched once.
/// </summary>
/// <param name="cache">The memory cache.</param>
/// <param name="options">The app options (contact for the User-Agent).</param>
/// <param name="logger">The logger.</param>
internal sealed class OpenFoodFactsSource(IMemoryCache cache, IOptions<BodyOpsOptions> options, ILogger<OpenFoodFactsSource> logger) : IPackagedFoodSource
{
    private const string Fields = "code,product_name,product_name_pt,product_name_en,brands,quantity,serving_quantity,nutriments";
    private static readonly HttpClient Http = new(new SocketsHttpHandler { PooledConnectionLifetime = TimeSpan.FromMinutes(10) })
    {
        BaseAddress = new Uri("https://world.openfoodfacts.org/"),
        Timeout = TimeSpan.FromSeconds(12),
    };

    private readonly IMemoryCache _cache = cache;
    private readonly ILogger<OpenFoodFactsSource> _logger = logger;
    private readonly string _userAgent = $"Milo/1.0 ({options.Value.OpenFoodFactsContact})";
    private readonly RateGate _searchGate = new(8);
    private readonly RateGate _productGate = new(12);

    /// <inheritdoc />
    public async Task<(PackagedStatus Status, List<FoodHitDTO> Hits)> SearchAsync(string query)
    {
        var key = "off:q:" + FoodMath.SearchKey(query);
        if (_cache.TryGetValue(key, out List<FoodHitDTO>? cached) && cached is not null)
        {
            return (PackagedStatus.Ok, cached);
        }

        if (!_searchGate.TryEnter())
        {
            return (PackagedStatus.SlowDown, []);
        }

        var url = $"cgi/search.pl?search_terms={Uri.EscapeDataString(query)}&search_simple=1&action=process&json=1&page_size=15&fields={Fields}";
        using var document = await GetAsync(url);
        if (document is null)
        {
            return (PackagedStatus.Unavailable, []);
        }

        var hits = document.RootElement.TryGetProperty("products", out var products) && products.ValueKind == JsonValueKind.Array
            ? products.EnumerateArray().Select(Read).OfType<FoodHitDTO>().Take(10).ToList()
            : [];
        _cache.Set(key, hits, TimeSpan.FromDays(1));
        return (PackagedStatus.Ok, hits);
    }

    /// <inheritdoc />
    public async Task<(PackagedStatus Status, FoodHitDTO? Hit)> BarcodeAsync(string barcode)
    {
        var key = "off:b:" + barcode;
        if (_cache.TryGetValue(key, out FoodHitDTO? cached))
        {
            return (cached is null ? PackagedStatus.NotFound : PackagedStatus.Ok, cached);
        }

        if (!_productGate.TryEnter())
        {
            return (PackagedStatus.SlowDown, null);
        }

        using var document = await GetAsync($"api/v2/product/{barcode}?fields={Fields}", notFoundIsAnswer: true);
        if (document is null)
        {
            return (PackagedStatus.Unavailable, null);
        }

        var hit = document.RootElement.TryGetProperty("product", out var product) && product.ValueKind == JsonValueKind.Object ? Read(product) : null;
        _cache.Set(key, hit, TimeSpan.FromDays(1));
        return (hit is null ? PackagedStatus.NotFound : PackagedStatus.Ok, hit);
    }

    /// <summary>
    /// Reads one Open Food Facts product: values per 100 g (kcal from kJ when only energy is given) and the serving size.
    /// </summary>
    /// <param name="product">The product JSON.</param>
    /// <returns>The product, or null without a name or without calories and protein.</returns>
    internal static FoodHitDTO? Read(JsonElement product)
    {
        var name = Text(product, "product_name_pt") ?? Text(product, "product_name") ?? Text(product, "product_name_en");
        if (name is null || !product.TryGetProperty("nutriments", out var n) || n.ValueKind != JsonValueKind.Object)
        {
            return null;
        }

        var kcal = Number(n, "energy-kcal_100g") ?? (Number(n, "energy_100g") is { } kj ? kj / 4.184m : null);
        var protein = Number(n, "proteins_100g");
        if (kcal is null && protein is null)
        {
            return null;
        }

        var serving = Number(product, "serving_quantity");
        var detail = string.Join(" · ", new[] { Text(product, "brands")?.Split(',')[0].Trim(), Text(product, "quantity") }.Where(static part => !string.IsNullOrWhiteSpace(part)));
        return new FoodHitDTO
        {
            Source = FoodSource.OpenFoodFacts,
            SourceRef = Text(product, "code") ?? string.Empty,
            Name = name.Length > 80 ? name[..80] : name,
            Detail = detail.Length == 0 ? null : detail,
            Kcal = Clamp(kcal, FoodMath.MaxKcal),
            ProteinG = Clamp(protein, FoodMath.MaxGrams),
            CarbsG = Clamp(Number(n, "carbohydrates_100g"), FoodMath.MaxGrams),
            FatG = Clamp(Number(n, "fat_100g"), FoodMath.MaxGrams),
            PieceGrams = serving is > 0 and < 5000 ? Math.Round(serving.Value, 1) : null,
            PieceName = serving is > 0 and < 5000 ? "serving" : null,
        };
    }

    private async Task<JsonDocument?> GetAsync(string url, bool notFoundIsAnswer = false)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.TryAddWithoutValidation("User-Agent", _userAgent);
            using var response = await Http.SendAsync(request);
            if (!response.IsSuccessStatusCode && !(notFoundIsAnswer && response.StatusCode == HttpStatusCode.NotFound))
            {
                _logger.LogWarning("Open Food Facts answered {Status} for {Url}", (int)response.StatusCode, url);
                return null;
            }

            await using var stream = await response.Content.ReadAsStreamAsync();
            return await JsonDocument.ParseAsync(stream);
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or JsonException)
        {
            _logger.LogWarning(exception, "Open Food Facts could not be reached");
            return null;
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(value.GetString())
            ? value.GetString()!.Trim()
            : null;

    private static decimal? Number(JsonElement element, string name)
    {
        if (!element.TryGetProperty(name, out var value))
        {
            return null;
        }

        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
        {
            return number;
        }

        return value.ValueKind == JsonValueKind.String && decimal.TryParse(value.GetString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static decimal? Clamp(decimal? value, decimal max) => value is { } v && v >= 0 ? Math.Round(Math.Min(v, max), 1) : null;

    /// <summary>
    /// At most N calls in any rolling minute.
    /// </summary>
    /// <param name="perMinute">The limit.</param>
    private sealed class RateGate(int perMinute)
    {
        private readonly Queue<DateTime> _calls = new();
        private readonly Lock _lock = new();

        public bool TryEnter()
        {
            lock (_lock)
            {
                var now = DateTime.UtcNow;
                while (_calls.Count > 0 && now - _calls.Peek() > TimeSpan.FromMinutes(1))
                {
                    _calls.Dequeue();
                }

                if (_calls.Count >= perMinute)
                {
                    return false;
                }

                _calls.Enqueue(now);
                return true;
            }
        }
    }
}
