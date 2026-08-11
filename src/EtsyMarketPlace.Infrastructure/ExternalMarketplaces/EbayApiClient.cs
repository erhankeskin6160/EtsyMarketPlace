namespace EtsyMarketPlace.Infrastructure.ExternalMarketplaces;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

public sealed class EbayApiClient(HttpClient? httpClient = null) : IEbayApiClient
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);
    private readonly HttpClient httpClient = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(18) };

    public async Task<string> TestConnectionAsync(
        EbayApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (!settings.HasCredentials)
        {
            throw new InvalidOperationException("eBay Client ID ve Client Secret girilmelidir.");
        }

        var testSettings = new EbayApiSettings
        {
            ClientId = settings.ClientId,
            ClientSecret = settings.ClientSecret,
            MarketplaceId = string.IsNullOrWhiteSpace(settings.MarketplaceId) ? "EBAY_US" : settings.MarketplaceId,
            Limit = Math.Clamp(settings.Limit <= 0 ? 5 : settings.Limit, 1, 5),
        };
        var products = await SearchAsync(testSettings, "handmade gift", cancellationToken);
        return $"Baglanti basarili. {products.Count} eBay urunu okundu.";
    }

    public async Task<IReadOnlyList<EbayApiProduct>> SearchAsync(
        EbayApiSettings settings,
        string query,
        CancellationToken cancellationToken = default)
    {
        if (!settings.HasCredentials)
        {
            return [];
        }

        var token = await GetApplicationTokenAsync(settings, cancellationToken);
        var limit = Math.Clamp(settings.Limit, 1, 50);
        var url = $"https://api.ebay.com/buy/browse/v1/item_summary/search?q={Uri.EscapeDataString(query)}&limit={limit}";
        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        if (!string.IsNullOrWhiteSpace(settings.MarketplaceId))
        {
            request.Headers.Add("X-EBAY-C-MARKETPLACE-ID", settings.MarketplaceId.Trim());
        }

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"eBay API aramasi basarisiz. HTTP {(int)response.StatusCode}: {body}");
        }

        return ParseProducts(body);
    }

    private async Task<string> GetApplicationTokenAsync(EbayApiSettings settings, CancellationToken cancellationToken)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "https://api.ebay.com/identity/v1/oauth2/token");
        var rawCredentials = $"{settings.ClientId.Trim()}:{settings.ClientSecret.Trim()}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes(rawCredentials)));
        request.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["scope"] = "https://api.ebay.com/oauth/api_scope",
        });

        using var response = await httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"eBay token alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        return document.RootElement.TryGetProperty("access_token", out var token)
            ? token.GetString() ?? ""
            : throw new InvalidOperationException("eBay token yanitinda access_token yok.");
    }

    private static IReadOnlyList<EbayApiProduct> ParseProducts(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("itemSummaries", out var items) ||
            items.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var products = new List<EbayApiProduct>();
        foreach (var item in items.EnumerateArray())
        {
            var title = ReadString(item, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                continue;
            }

            var price = ReadPrice(item, "price", out var currency);
            var url = ReadString(item, "itemWebUrl");
            var imageUrl = item.TryGetProperty("image", out var image)
                ? ReadString(image, "imageUrl")
                : "";
            var seller = item.TryGetProperty("seller", out var sellerElement)
                ? ReadString(sellerElement, "username")
                : "eBay seller";
            var category = ReadCategory(item);
            var tags = BuildTags(title, category);
            var demand = Math.Clamp(tags.Count * 15 + title.Length / 2, 15, 260);
            var shopSignal = Math.Clamp(seller.Length * 75 + tags.Count * 110, 80, 7000);

            products.Add(new EbayApiProduct(
                title,
                url,
                imageUrl,
                seller,
                category,
                price,
                currency,
                demand,
                shopSignal,
                tags));
        }

        return products;
    }

    private static decimal ReadPrice(JsonElement item, string propertyName, out string currency)
    {
        currency = "";
        if (!item.TryGetProperty(propertyName, out var price) ||
            price.ValueKind != JsonValueKind.Object)
        {
            return 0;
        }

        currency = ReadString(price, "currency");
        var value = ReadString(price, "value");
        return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed)
            ? parsed
            : 0;
    }

    private static string ReadCategory(JsonElement item)
    {
        if (item.TryGetProperty("categories", out var categories) &&
            categories.ValueKind == JsonValueKind.Array)
        {
            var names = categories
                .EnumerateArray()
                .Select(category => ReadString(category, "categoryName"))
                .Where(name => !string.IsNullOrWhiteSpace(name))
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToList();
            if (names.Count > 0)
            {
                return string.Join(" > ", names);
            }
        }

        return "";
    }

    private static IReadOnlyList<string> BuildTags(string title, string category)
    {
        return $"{title} {category}"
            .Split([' ', ',', '|', '-', '/', '>', '(', ')', '[', ']'], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length is >= 3 and <= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();
    }

    private static string ReadString(JsonElement element, string propertyName) =>
        element.TryGetProperty(propertyName, out var value) && value.ValueKind != JsonValueKind.Null
            ? value.GetString() ?? ""
            : "";
}
