namespace SimilarProductsWinForms.Services;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text.Json;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Infrastructure.Http;
using SimilarProductsWinForms.Models;

internal sealed class EtsyApiClient
{
    private const string BaseUrl = "https://api.etsy.com/v3/application";
    private const string TokenUrl = "https://api.etsy.com/v3/public/oauth/token";
    private static readonly HttpClient SharedHttpClient = new(
        new ApiResilienceHandler(new HttpClientHandler()));
    private readonly HttpClient _httpClient = SharedHttpClient;
    private readonly Dictionary<long, ShopSnapshot> _shopCache = [];

    public Uri CreateAuthorizationUri(EtsyApiSettings settings, string scopes)
    {
        if (!settings.HasApiCredentials)
        {
            throw new InvalidOperationException("Keystring ve shared secret girilmeden OAuth linki olusturulamaz.");
        }

        settings.LastCodeVerifier = PkceHelper.CreateCodeVerifier();
        settings.LastState = PkceHelper.CreateState();
        var codeChallenge = PkceHelper.CreateCodeChallenge(settings.LastCodeVerifier);

        var query = new Dictionary<string, string>
        {
            ["response_type"] = "code",
            ["redirect_uri"] = settings.RedirectUri.Trim(),
            ["scope"] = scopes,
            ["client_id"] = settings.Keystring.Trim(),
            ["state"] = settings.LastState,
            ["code_challenge"] = codeChallenge,
            ["code_challenge_method"] = "S256",
        };

        return new Uri("https://www.etsy.com/oauth/connect?" + ToQueryString(query));
    }

    public async Task ExchangeAuthorizationCodeAsync(EtsyApiSettings settings, string authorizationCode, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.LastCodeVerifier))
        {
            throw new InvalidOperationException("Once OAuth linki uretin. Code verifier bos.");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "authorization_code",
            ["client_id"] = settings.Keystring.Trim(),
            ["redirect_uri"] = settings.RedirectUri.Trim(),
            ["code"] = authorizationCode.Trim(),
            ["code_verifier"] = settings.LastCodeVerifier,
        };

        using var response = await _httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        ApplyTokenResponse(settings, body);
    }

    public async Task RefreshAccessTokenAsync(EtsyApiSettings settings, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(settings.RefreshToken))
        {
            throw new InvalidOperationException("Refresh token yok. Once OAuth ile giris yapin.");
        }

        var form = new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["client_id"] = settings.Keystring.Trim(),
            ["refresh_token"] = settings.RefreshToken.Trim(),
        };

        using var response = await _httpClient.PostAsync(TokenUrl, new FormUrlEncodedContent(form), cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Token yenilenemedi. HTTP {(int)response.StatusCode}: {body}");
        }

        ApplyTokenResponse(settings, body);
    }

    public async Task<List<EtsyListingDto>> FindActiveListingsAsync(EtsyApiSettings settings, string keywords, int limit = 10, CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);

        var query = ToQueryString(new Dictionary<string, string>
        {
            ["keywords"] = keywords,
            ["limit"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/active?{query}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Listing arama basarisiz. HTTP {(int)response.StatusCode}: {body}");
        }

        return ParseListings(body);
    }

    public async Task<List<MarketListingResult>> FindMarketListingsAsync(
        EtsyApiSettings settings,
        string keywords,
        int limit = 30,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);

        var query = ToQueryString(new Dictionary<string, string>
        {
            ["keywords"] = keywords.Trim(),
            ["limit"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["sort_on"] = "score",
            ["sort_order"] = "desc",
            ["includes"] = "Shop,Images",
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/active?{query}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Pazar aramasi basarisiz. HTTP {(int)response.StatusCode}: {body}");
        }

        var listings = ParseMarketListings(body, keywords);
        await EnrichShopDataAsync(settings, listings, keywords, cancellationToken);
        return listings.OrderByDescending(listing => listing.MarketScore).ToList();
    }

    public async Task<KeywordMarketApiSample> GetKeywordMarketSampleAsync(
        EtsyApiSettings settings,
        string keywords,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);

        var query = ToQueryString(new Dictionary<string, string>
        {
            ["keywords"] = keywords.Trim(),
            ["limit"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["sort_on"] = "score",
            ["sort_order"] = "desc",
            ["includes"] = "Shop,Images",
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/active?{query}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Anahtar kelime orneklemi alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var totalResults = GetInt(document.RootElement, "count");
        var listings = ParseMarketListings(body, keywords);
        return new KeywordMarketApiSample(Math.Max(totalResults, listings.Count), listings);
    }

    public async Task<List<string>> GetListingImagesAsync(
        EtsyApiSettings settings,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/{listingId}/images", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return [];
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return ReadImageUrls(results);
    }

    public async Task<CompetitorShopAnalysis> GetCompetitorShopAnalysisAsync(
        EtsyApiSettings settings,
        long shopId,
        int limit = 50,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        if (shopId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(shopId), "Gecerli bir magaza kimligi gerekli.");
        }

        var profile = await GetCompetitorShopProfileAsync(settings, shopId, cancellationToken);
        var query = ToQueryString(new Dictionary<string, string>
        {
            ["limit"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["sort_on"] = "created",
            ["sort_order"] = "desc",
            ["includes"] = "Images",
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}/listings/active?{query}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Magaza urunleri alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        var listings = ParseMarketListings(body, "");
        foreach (var listing in listings)
        {
            listing.ShopName = profile.ShopName;
            listing.ShopUrl = profile.ShopUrl;
            listing.ShopSales = profile.TotalSales;
            listing.ReviewCount = profile.ReviewCount;
            listing.ReviewAverage = profile.ReviewAverage;
        }

        return CompetitorShopAnalyzer.Analyze(profile, listings);
    }

    private async Task<CompetitorShopProfile> GetCompetitorShopProfileAsync(
        EtsyApiSettings settings,
        long shopId,
        CancellationToken cancellationToken)
    {
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Magaza bilgisi alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        var shop = document.RootElement;
        if (shop.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
        {
            shop = results[0];
        }

        var shopName = GetString(shop, "shop_name");
        return new CompetitorShopProfile
        {
            ShopId = shopId,
            ShopName = string.IsNullOrWhiteSpace(shopName) ? $"Shop {shopId}" : shopName,
            ShopUrl = BuildShopUrl(shopName),
            Title = GetString(shop, "title"),
            TotalSales = GetInt(shop, "transaction_sold_count"),
            ReviewCount = GetInt(shop, "review_count"),
            ReviewAverage = GetDecimal(shop, "review_average"),
            ActiveListingCount = GetInt(shop, "listing_active_count"),
        };
    }

    public async Task<OwnShopPerformanceSource> GetOwnShopPerformanceSourceAsync(
        EtsyApiSettings settings,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var userId = ReadUserIdFromAccessToken(settings.AccessToken);
        using var shopRequest = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/users/{userId}/shops", useAccessToken: true);
        using var shopResponse = await _httpClient.SendAsync(shopRequest, cancellationToken);
        var shopBody = await shopResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!shopResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Bagli magaza bilgisi alinamadi. HTTP {(int)shopResponse.StatusCode}: {shopBody}");
        }

        using var shopDocument = JsonDocument.Parse(shopBody);
        var shop = FirstResultOrRoot(shopDocument.RootElement);
        var shopId = GetLong(shop, "shop_id");
        var shopName = GetString(shop, "shop_name");
        if (shopId <= 0)
        {
            throw new InvalidOperationException("OAuth kullanicisina ait Etsy magazasi bulunamadi.");
        }

        var receipts = await GetOwnShopReceiptsAsync(settings, shopId, periodStart, periodEnd, cancellationToken);
        return new OwnShopPerformanceSource(
            new OwnShopProfile(shopId, shopName, BuildShopUrl(shopName)),
            receipts);
    }

    public async Task<List<MarketListingResult>> GetOwnShopActiveListingsAsync(
        EtsyApiSettings settings,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (shopId, shopName) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        var query = ToQueryString(new Dictionary<string, string>
        {
            ["limit"] = Math.Clamp(limit, 1, 100).ToString(CultureInfo.InvariantCulture),
            ["sort_on"] = "updated",
            ["sort_order"] = "desc",
            ["includes"] = "Images",
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}/listings/active?{query}", useAccessToken: true);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kendi magaza listingleri alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        var listings = ParseMarketListings(body, "");
        foreach (var listing in listings)
        {
            listing.ShopName = shopName;
            listing.ShopUrl = BuildShopUrl(shopName);
        }

        return listings;
    }

    private async Task<(long ShopId, string ShopName)> GetOwnShopIdentityAsync(
        EtsyApiSettings settings,
        CancellationToken cancellationToken)
    {
        var userId = ReadUserIdFromAccessToken(settings.AccessToken);
        using var shopRequest = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/users/{userId}/shops", useAccessToken: true);
        using var shopResponse = await _httpClient.SendAsync(shopRequest, cancellationToken);
        var shopBody = await shopResponse.Content.ReadAsStringAsync(cancellationToken);
        if (!shopResponse.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Bagli magaza bilgisi alinamadi. HTTP {(int)shopResponse.StatusCode}: {shopBody}");
        }

        using var shopDocument = JsonDocument.Parse(shopBody);
        var shop = FirstResultOrRoot(shopDocument.RootElement);
        var shopId = GetLong(shop, "shop_id");
        var shopName = GetString(shop, "shop_name");
        if (shopId <= 0)
        {
            throw new InvalidOperationException("OAuth kullanicisina ait Etsy magazasi bulunamadi.");
        }

        return (shopId, shopName);
    }

    private async Task<List<OwnShopReceipt>> GetOwnShopReceiptsAsync(
        EtsyApiSettings settings,
        long shopId,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken)
    {
        const int pageSize = 100;
        var offset = 0;
        var receipts = new List<OwnShopReceipt>();

        while (true)
        {
            var query = ToQueryString(new Dictionary<string, string>
            {
                ["min_created"] = periodStart.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ["max_created"] = periodEnd.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture),
                ["limit"] = pageSize.ToString(CultureInfo.InvariantCulture),
                ["offset"] = offset.ToString(CultureInfo.InvariantCulture),
            });
            using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}/receipts?{query}", useAccessToken: true);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Magaza siparisleri alinamadi. HTTP {(int)response.StatusCode}: {body}");
            }

            using var document = JsonDocument.Parse(body);
            var results = GetArray(document.RootElement, "results");
            if (!results.HasValue || results.Value.GetArrayLength() == 0)
            {
                break;
            }

            receipts.AddRange(results.Value.EnumerateArray().Select(ParseOwnShopReceipt));
            offset += results.Value.GetArrayLength();
            var count = GetInt(document.RootElement, "count");
            if (results.Value.GetArrayLength() < pageSize || (count > 0 && offset >= count))
            {
                break;
            }
        }

        return receipts;
    }

    private static OwnShopReceipt ParseOwnShopReceipt(JsonElement receipt)
    {
        var (grandTotal, currency) = ReadMoney(receipt, "grandtotal");
        var transactions = GetArray(receipt, "transactions", "Transactions")?
            .EnumerateArray()
            .Select(transaction =>
            {
                var (amount, transactionCurrency) = ReadMoney(transaction, "price");
                return new OwnShopTransaction(
                    GetLong(transaction, "listing_id"),
                    GetString(transaction, "title"),
                    Math.Max(1, GetInt(transaction, "quantity")),
                    amount,
                    string.IsNullOrWhiteSpace(transactionCurrency) ? currency : transactionCurrency);
            })
            .ToList() ?? [];
        var created = GetLong(receipt, "create_timestamp");

        return new OwnShopReceipt(
            GetLong(receipt, "receipt_id"),
            created > 0 ? DateTimeOffset.FromUnixTimeSeconds(created) : DateTimeOffset.MinValue,
            GetBool(receipt, "is_paid"),
            GetBool(receipt, "is_canceled"),
            grandTotal,
            currency,
            transactions);
    }

    private async Task EnsureAccessTokenAsync(EtsyApiSettings settings, CancellationToken cancellationToken)
    {
        if (settings.HasAccessToken)
        {
            return;
        }

        if (!string.IsNullOrWhiteSpace(settings.RefreshToken))
        {
            await RefreshAccessTokenAsync(settings, cancellationToken);
            return;
        }

        throw new InvalidOperationException("Kendi magaza verileri icin OAuth baglantisi gerekli. API Ayarlari ekranindan shops_r, listings_r ve transactions_r izinleriyle baglanin.");
    }

    public async Task<string> TestConnectionAsync(EtsyApiSettings settings, CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/openapi-ping", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"API test basarisiz. HTTP {(int)response.StatusCode}: {body}");
        }

        return string.IsNullOrWhiteSpace(body) ? "API baglantisi basarili." : body;
    }

    private static HttpRequestMessage CreateRequest(EtsyApiSettings settings, HttpMethod method, string url, bool useAccessToken)
    {
        var request = new HttpRequestMessage(method, url);
        request.Headers.Add("x-api-key", settings.ApiKeyHeader);
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (useAccessToken)
        {
            request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", settings.AccessToken.Trim());
        }

        return request;
    }

    private static void EnsureApiCredentials(EtsyApiSettings settings)
    {
        if (!settings.HasApiCredentials)
        {
            throw new InvalidOperationException("Etsy API keystring ve shared secret gerekli.");
        }
    }

    private static void ApplyTokenResponse(EtsyApiSettings settings, string json)
    {
        using var document = JsonDocument.Parse(json);
        var root = document.RootElement;
        settings.AccessToken = root.GetProperty("access_token").GetString() ?? "";
        settings.RefreshToken = root.TryGetProperty("refresh_token", out var refresh)
            ? refresh.GetString() ?? settings.RefreshToken
            : settings.RefreshToken;

        var expiresIn = root.TryGetProperty("expires_in", out var expires)
            ? expires.GetInt32()
            : 3600;
        settings.AccessTokenExpiresAtUtc = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
    }

    private static List<EtsyListingDto> ParseListings(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var listings = new List<EtsyListingDto>();
        foreach (var item in results.EnumerateArray())
        {
            var listingId = GetLong(item, "listing_id");
            var shopId = GetLong(item, "shop_id");
            var shopName = GetString(item, "shop_name");
            var url = GetString(item, "url");
            listings.Add(new EtsyListingDto
            {
                ListingId = listingId,
                Title = GetString(item, "title"),
                ShopName = string.IsNullOrWhiteSpace(shopName) ? $"Shop {shopId}" : shopName,
                ShopUrl = BuildShopUrl(shopName),
                ListingUrl = string.IsNullOrWhiteSpace(url) ? $"https://www.etsy.com/listing/{listingId}" : url,
                PriceDisplay = ReadPrice(item),
                QuantityDisplay = GetIntDisplay(item, "quantity"),
            });
        }

        return listings;
    }

    private static List<MarketListingResult> ParseMarketListings(string json, string primaryKeyword)
    {
        using var document = JsonDocument.Parse(json);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var listings = new List<MarketListingResult>();
        foreach (var item in results.EnumerateArray())
        {
            var listingId = GetLong(item, "listing_id");
            var shopId = GetLong(item, "shop_id");
            var title = GetString(item, "title");
            var description = GetString(item, "description");
            var tags = GetStringArray(item, "tags");
            var favorites = GetInt(item, "num_favorers");
            var views = GetInt(item, "views");
            var quantity = GetInt(item, "quantity");
            var (price, currency) = ReadPriceValue(item);

            var shop = GetObject(item, "Shop", "shop");
            var shopName = shop.HasValue ? GetString(shop.Value, "shop_name") : GetString(item, "shop_name");
            var shopSales = shop.HasValue ? GetInt(shop.Value, "transaction_sold_count") : 0;
            var reviewCount = shop.HasValue ? GetInt(shop.Value, "review_count") : 0;
            var reviewAverage = shop.HasValue ? GetDecimal(shop.Value, "review_average") : 0;
            var images = GetArray(item, "Images", "images");
            var imageUrls = images.HasValue ? ReadImageUrls(images.Value) : [];
            var imageUrl = imageUrls.FirstOrDefault() ?? "";

            var seo = SeoScoreCalculator.Calculate(title, description, tags, primaryKeyword).Score;
            var marketScore = CalculateMarketScore(title, primaryKeyword, favorites, views, shopSales, reviewAverage, seo);
            var listingUrl = GetString(item, "url");

            listings.Add(new MarketListingResult
            {
                ListingId = listingId,
                ShopId = shopId,
                TaxonomyId = GetLong(item, "taxonomy_id"),
                Title = title,
                Description = description,
                ListingUrl = string.IsNullOrWhiteSpace(listingUrl) ? $"https://www.etsy.com/listing/{listingId}" : listingUrl,
                ImageUrl = imageUrl,
                ShopName = string.IsNullOrWhiteSpace(shopName) ? $"Shop {shopId}" : shopName,
                ShopUrl = BuildShopUrl(shopName),
                Price = price,
                CurrencyCode = string.IsNullOrWhiteSpace(currency) ? "USD" : currency,
                Quantity = quantity,
                Favorites = favorites,
                Views = views,
                ShopSales = shopSales,
                ReviewCount = reviewCount,
                ReviewAverage = reviewAverage,
                Tags = tags,
                ImageUrls = imageUrls,
                SeoScore = seo,
                MarketScore = marketScore,
            });
        }

        return listings.OrderByDescending(listing => listing.MarketScore).ToList();
    }

    private async Task EnrichShopDataAsync(
        EtsyApiSettings settings,
        List<MarketListingResult> listings,
        string keyword,
        CancellationToken cancellationToken)
    {
        foreach (var group in listings.Where(item => item.ShopId > 0).GroupBy(item => item.ShopId))
        {
            var snapshot = await GetShopSnapshotAsync(settings, group.Key, cancellationToken);
            if (snapshot is not null)
            {
                foreach (var item in group)
                {
                    item.ShopName = snapshot.ShopName;
                    item.ShopUrl = BuildShopUrl(snapshot.ShopName);
                    item.ShopSales = snapshot.Sales;
                    item.ReviewCount = snapshot.ReviewCount;
                    item.ReviewAverage = snapshot.ReviewAverage;
                    item.MarketScore = CalculateMarketScore(item.Title, keyword, item.Favorites, item.Views, item.ShopSales, item.ReviewAverage, item.SeoScore);
                }
            }

            await Task.Delay(210, cancellationToken);
        }
    }

    private async Task<ShopSnapshot?> GetShopSnapshotAsync(
        EtsyApiSettings settings,
        long shopId,
        CancellationToken cancellationToken)
    {
        if (_shopCache.TryGetValue(shopId, out var cached))
        {
            return cached;
        }

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        using var document = JsonDocument.Parse(body);
        var shop = document.RootElement;
        if (shop.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array && results.GetArrayLength() > 0)
        {
            shop = results[0];
        }

        var shopName = GetString(shop, "shop_name");
        if (string.IsNullOrWhiteSpace(shopName))
        {
            return null;
        }

        var snapshot = new ShopSnapshot(
            shopName,
            GetInt(shop, "transaction_sold_count"),
            GetInt(shop, "review_count"),
            GetDecimal(shop, "review_average"));
        _shopCache[shopId] = snapshot;
        return snapshot;
    }

    private static List<string> ReadImageUrls(JsonElement images)
    {
        var urls = new List<string>();
        foreach (var image in images.EnumerateArray())
        {
            var url = GetFirstString(image, "url_fullxfull", "url_570xN", "url_170x135", "url_75x75");
            if (!string.IsNullOrWhiteSpace(url))
            {
                urls.Add(url);
            }
        }

        return urls.Distinct(StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static int CalculateMarketScore(
        string title,
        string keyword,
        int favorites,
        int views,
        int shopSales,
        decimal reviewAverage,
        int seoScore)
    {
        var keywordScore = !string.IsNullOrWhiteSpace(keyword) && title.Contains(keyword, StringComparison.CurrentCultureIgnoreCase) ? 15m : 5m;
        var favoriteScore = Math.Min(20m, (decimal)Math.Log10(Math.Max(1, favorites) + 1) * 8m);
        var viewScore = Math.Min(15m, (decimal)Math.Log10(Math.Max(1, views) + 1) * 5m);
        var salesScore = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, shopSales) + 1) * 7m);
        var reviewScore = Math.Min(10m, reviewAverage * 2m);
        var result = keywordScore + favoriteScore + viewScore + salesScore + reviewScore + seoScore * 0.15m;
        return (int)Math.Round(Math.Clamp(result, 0m, 100m));
    }

    private static (decimal Amount, string Currency) ReadPriceValue(JsonElement item)
    {
        if (!item.TryGetProperty("price", out var price))
        {
            return (0m, "USD");
        }

        if (price.ValueKind == JsonValueKind.Object)
        {
            var amount = GetLong(price, "amount");
            var divisor = GetInt(price, "divisor");
            return (divisor > 0 ? amount / (decimal)divisor : 0m, GetString(price, "currency_code"));
        }

        return price.ValueKind == JsonValueKind.String && decimal.TryParse(price.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed)
            ? (parsed, "USD")
            : (0m, "USD");
    }

    private static (decimal Amount, string Currency) ReadMoney(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var money) || money.ValueKind != JsonValueKind.Object)
        {
            return (0m, "USD");
        }

        var amount = GetLong(money, "amount");
        var divisor = GetInt(money, "divisor");
        return (divisor > 0 ? amount / (decimal)divisor : 0m, GetString(money, "currency_code"));
    }

    private static JsonElement FirstResultOrRoot(JsonElement root)
    {
        var results = GetArray(root, "results");
        return results.HasValue && results.Value.GetArrayLength() > 0 ? results.Value[0] : root;
    }

    private static long ReadUserIdFromAccessToken(string accessToken)
    {
        var separator = accessToken.IndexOf('.');
        var value = separator > 0 ? accessToken[..separator] : "";
        if (!long.TryParse(value, NumberStyles.None, CultureInfo.InvariantCulture, out var userId) || userId <= 0)
        {
            throw new InvalidOperationException("OAuth access token icinden Etsy kullanici kimligi okunamadi. API Ayarlari ekranindan yeniden baglanin.");
        }

        return userId;
    }

    private static List<string> GetStringArray(JsonElement item, string propertyName)
    {
        if (!item.TryGetProperty(propertyName, out var value) || value.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return value.EnumerateArray()
            .Where(element => element.ValueKind == JsonValueKind.String)
            .Select(element => element.GetString() ?? "")
            .Where(text => !string.IsNullOrWhiteSpace(text))
            .ToList();
    }

    private static JsonElement? GetObject(JsonElement item, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Object)
            {
                return value;
            }
        }

        return null;
    }

    private static JsonElement? GetArray(JsonElement item, params string[] propertyNames)
    {
        foreach (var name in propertyNames)
        {
            if (item.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.Array)
            {
                return value;
            }
        }

        return null;
    }

    private static string ReadPrice(JsonElement item)
    {
        if (item.TryGetProperty("price", out var price))
        {
            if (price.ValueKind == JsonValueKind.Object)
            {
                var amount = GetInt(price, "amount");
                var divisor = GetInt(price, "divisor");
                var currency = GetString(price, "currency_code");
                if (amount > 0 && divisor > 0)
                {
                    return $"{currency} {(amount / (decimal)divisor):0.##}";
                }
            }

            if (price.ValueKind == JsonValueKind.String)
            {
                return price.GetString() ?? "";
            }
        }

        return "Fiyat API sonucunda yok";
    }

    private static string GetIntDisplay(JsonElement item, string propertyName)
    {
        var value = GetInt(item, propertyName);
        return value > 0 ? value.ToString(CultureInfo.InvariantCulture) : "API sonucunda yok";
    }

    private static string GetString(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString() ?? ""
            : "";

    private static string GetFirstString(JsonElement item, params string[] propertyNames)
    {
        foreach (var propertyName in propertyNames)
        {
            var value = GetString(item, propertyName);
            if (!string.IsNullOrWhiteSpace(value))
            {
                return value;
            }
        }

        return "";
    }

    private static string BuildShopUrl(string shopName)
    {
        return string.IsNullOrWhiteSpace(shopName)
            ? "https://www.etsy.com"
            : $"https://www.etsy.com/shop/{Uri.EscapeDataString(shopName.Trim())}";
    }

    private static int GetInt(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.TryGetInt32(out var result)
            ? result
            : 0;

    private static decimal GetDecimal(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.TryGetDecimal(out var result)
            ? result
            : 0m;

    private static bool GetBool(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) &&
        (value.ValueKind == JsonValueKind.True ||
         (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number) && number != 0));

    private static long GetLong(JsonElement item, string propertyName) =>
        item.TryGetProperty(propertyName, out var value) && value.TryGetInt64(out var result)
            ? result
            : 0L;

    private static string ToQueryString(Dictionary<string, string> values) =>
        string.Join("&", values.Select(pair => $"{Uri.EscapeDataString(pair.Key)}={Uri.EscapeDataString(pair.Value)}"));

    private sealed record ShopSnapshot(string ShopName, int Sales, int ReviewCount, decimal ReviewAverage);
}
