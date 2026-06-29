namespace SimilarProductsWinForms.Services;

using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json.Serialization;
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

    public async Task<MarketListingResult> GetPublicListingAsync(
        EtsyApiSettings settings,
        long listingId,
        string primaryKeyword = "",
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        if (listingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(listingId), "Gecerli bir Etsy listing ID gerekli.");
        }

        var query = ToQueryString(new Dictionary<string, string>
        {
            ["includes"] = "Shop,Images",
        });

        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/{listingId}?{query}", useAccessToken: false);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Listing linkinden urun alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        var wrapped = body.Contains("\"results\"", StringComparison.OrdinalIgnoreCase)
            ? body
            : $"{{\"results\":[{body}]}}";
        var listing = ParseMarketListings(wrapped, primaryKeyword).FirstOrDefault()
            ?? throw new InvalidOperationException("Etsy listing verisi okunamadi.");

        if (listing.ImageUrls.Count == 0)
        {
            listing.ImageUrls = await GetListingImagesAsync(settings, listingId, cancellationToken);
        }

        await EnrichShopDataAsync(settings, [listing], primaryKeyword, cancellationToken);
        return listing;
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

    public async Task<OwnShopProfile> GetOwnShopProfileAsync(
        EtsyApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (shopId, shopName) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        return new OwnShopProfile(shopId, shopName, BuildShopUrl(shopName));
    }

    public async Task<MarketListingResult> GetOwnShopListingAsync(
        EtsyApiSettings settings,
        long listingId,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (_, shopName) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/listings/{listingId}?includes=Images,Shop", useAccessToken: true);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Listing yeniden alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        var wrapped = body.Contains("\"results\"", StringComparison.OrdinalIgnoreCase)
            ? body
            : $"{{\"results\":[{body}]}}";
        var listing = ParseMarketListings(wrapped, "").FirstOrDefault()
            ?? throw new InvalidOperationException("Listing yaniti okunamadi.");
        listing.ShopName = shopName;
        listing.ShopUrl = BuildShopUrl(shopName);
        return listing;
    }

    public async Task UpdateOwnShopListingTextAsync(
        EtsyApiSettings settings,
        long listingId,
        ListingTextUpdate update,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        if (listingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(listingId), "Gecerli bir listing kimligi gerekli.");
        }

        var (shopId, _) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        var tags = NormalizeListingTags(update.Tags);
        var materials = NormalizeListingMaterials(update.Materials);
        var form = new List<KeyValuePair<string, string>>
        {
            new("title", update.Title.Trim()),
            new("description", update.Description.Trim()),
        };

        foreach (var tag in tags)
        {
            form.Add(new("tags[]", tag));
        }

        foreach (var material in materials)
        {
            form.Add(new("materials[]", material));
        }

        using var request = CreateRequest(settings, HttpMethod.Patch, $"{BaseUrl}/shops/{shopId}/listings/{listingId}", useAccessToken: true);
        request.Content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Listing guncellenemedi. HTTP {(int)response.StatusCode}: {body}");
        }
    }

    public async Task UploadOwnShopListingImageAsync(
        EtsyApiSettings settings,
        long listingId,
        string imagePath,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        if (listingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(listingId), "Gecerli bir listing kimligi gerekli.");
        }

        if (!File.Exists(imagePath))
        {
            throw new FileNotFoundException("Yuklenecek gorsel bulunamadi.", imagePath);
        }

        var (shopId, _) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        await using var stream = File.OpenRead(imagePath);
        using var content = new MultipartFormDataContent();
        using var imageContent = new StreamContent(stream);
        imageContent.Headers.ContentType = new MediaTypeHeaderValue(GetImageContentType(imagePath));
        content.Add(imageContent, "image", Path.GetFileName(imagePath));

        using var request = CreateRequest(settings, HttpMethod.Post, $"{BaseUrl}/shops/{shopId}/listings/{listingId}/images", useAccessToken: true);
        request.Content = content;
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Listing gorseli yuklenemedi. HTTP {(int)response.StatusCode}: {body}");
        }
    }

    public async Task<List<EtsyShippingProfileOption>> GetOwnShopShippingProfilesAsync(
        EtsyApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (shopId, _) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}/shipping-profiles", useAccessToken: true);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Shipping profilleri alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        var profiles = new List<EtsyShippingProfileOption>();
        foreach (var item in results.EnumerateArray())
        {
            var id = GetLong(item, "shipping_profile_id");
            if (id <= 0)
            {
                continue;
            }

            var title = GetString(item, "title");
            if (string.IsNullOrWhiteSpace(title))
            {
                title = GetString(item, "name");
            }

            profiles.Add(new EtsyShippingProfileOption(id, string.IsNullOrWhiteSpace(title) ? $"Shipping profile #{id}" : title));
        }

        return profiles.OrderBy(profile => profile.Title, StringComparer.CurrentCultureIgnoreCase).ToList();
    }

    public async Task<List<EtsyReadinessStateOption>> GetOwnShopReadinessStateOptionsAsync(
        EtsyApiSettings settings,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (shopId, _) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/shops/{shopId}/listings/active?limit=100", useAccessToken: true);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Hazirlik durumu secenekleri alinamadi. HTTP {(int)response.StatusCode}: {body}");
        }

        using var document = JsonDocument.Parse(body);
        if (!document.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return [];
        }

        return results
            .EnumerateArray()
            .Select(item => GetLong(item, "readiness_state_id"))
            .Where(id => id > 0)
            .Distinct()
            .OrderBy(id => id)
            .Select(id => new EtsyReadinessStateOption(id, $"Hazirlik durumu #{id}"))
            .ToList();
    }

    public async Task<CreatedDraftListing> CreateOwnShopDraftListingAsync(
        EtsyApiSettings settings,
        DraftListingCreateRequest draft,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        var (shopId, _) = await GetOwnShopIdentityAsync(settings, cancellationToken);
        var tags = NormalizeListingTags(draft.Tags);
        var materials = NormalizeListingMaterials(draft.Materials);
        var form = new List<KeyValuePair<string, string>>
        {
            new("quantity", Math.Max(1, draft.Quantity).ToString(CultureInfo.InvariantCulture)),
            new("title", draft.Title.Trim()),
            new("description", draft.Description.Trim()),
            new("price", draft.Price.ToString("0.00", CultureInfo.InvariantCulture)),
            new("who_made", string.IsNullOrWhiteSpace(draft.WhoMade) ? "i_did" : draft.WhoMade.Trim()),
            new("when_made", string.IsNullOrWhiteSpace(draft.WhenMade) ? "made_to_order" : draft.WhenMade.Trim()),
            new("taxonomy_id", draft.TaxonomyId.ToString(CultureInfo.InvariantCulture)),
            new("type", draft.IsDigital ? "download" : "physical"),
        };

        if (!draft.IsDigital && draft.ShippingProfileId > 0)
        {
            form.Add(new("shipping_profile_id", draft.ShippingProfileId.ToString(CultureInfo.InvariantCulture)));
            if (draft.ReadinessStateId > 0)
            {
                form.Add(new("readiness_state_id", draft.ReadinessStateId.ToString(CultureInfo.InvariantCulture)));
            }
        }

        foreach (var tag in tags)
        {
            form.Add(new("tags[]", tag));
        }

        foreach (var material in materials)
        {
            form.Add(new("materials[]", material));
        }

        using var request = CreateRequest(settings, HttpMethod.Post, $"{BaseUrl}/shops/{shopId}/listings", useAccessToken: true);
        request.Content = new FormUrlEncodedContent(form);
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(BuildDraftListingErrorMessage((int)response.StatusCode, body));
        }

        using var document = JsonDocument.Parse(body);
        var listing = FirstResultOrRoot(document.RootElement);
        var listingId = GetLong(listing, "listing_id");
        if (listingId <= 0)
        {
            throw new InvalidOperationException($"Taslak listing olusturuldu ama listing_id okunamadi: {body}");
        }

        var url = GetString(listing, "url");
        return new CreatedDraftListing(
            listingId,
            string.IsNullOrWhiteSpace(url) ? $"https://www.etsy.com/listing/{listingId}" : url);
    }

    public async Task UpdateOwnShopListingInventoryAsync(
        EtsyApiSettings settings,
        long listingId,
        DraftListingInventoryUpdate inventory,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        await EnsureAccessTokenAsync(settings, cancellationToken);

        if (listingId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(listingId), "Gecerli bir listing kimligi gerekli.");
        }

        if (inventory.Variations.Count == 0)
        {
            return;
        }

        var products = BuildInventoryProducts(listingId, inventory);
        if (products.Count == 0)
        {
            return;
        }

        var payload = new
        {
            products,
            price_on_property = Array.Empty<long>(),
            quantity_on_property = Array.Empty<long>(),
            sku_on_property = Array.Empty<long>(),
        };

        using var request = CreateRequest(settings, HttpMethod.Put, $"{BaseUrl}/listings/{listingId}/inventory", useAccessToken: true);
        request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var body = await response.Content.ReadAsStringAsync(cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Varyasyonlar Etsy inventory sistemine yazilamadi. HTTP {(int)response.StatusCode}: {body}");
        }
    }

    private static List<object> BuildInventoryProducts(long listingId, DraftListingInventoryUpdate inventory)
    {
        var groups = inventory.Variations
            .Where(group => group.Values.Count > 0)
            .Take(2)
            .ToList();
        if (groups.Count == 0)
        {
            return [];
        }

        var combinations = BuildVariationCombinations(groups)
            .Take(70)
            .ToList();

        var products = new List<object>();
        for (var index = 0; index < combinations.Count; index++)
        {
            var combination = combinations[index];
            var offering = new Dictionary<string, object>
            {
                ["price"] = inventory.Price.ToString("0.00", CultureInfo.InvariantCulture),
                ["quantity"] = Math.Max(1, inventory.Quantity),
                ["is_enabled"] = true,
            };
            if (inventory.ReadinessStateId is > 0)
            {
                offering["readiness_state_id"] = inventory.ReadinessStateId.Value;
            }

            products.Add(new
            {
                sku = $"AUTO-{listingId}-{index + 1}",
                property_values = combination.Select(item => new
                {
                    property_id = item.Group.PropertyId,
                    property_name = item.Group.Name,
                    values = new[] { item.Value },
                }).ToList(),
                offerings = new[] { offering },
            });
        }

        return products;
    }

    private static IEnumerable<List<(DraftListingVariationGroup Group, string Value)>> BuildVariationCombinations(
        IReadOnlyList<DraftListingVariationGroup> groups)
    {
        if (groups.Count == 1)
        {
            foreach (var value in groups[0].Values)
            {
                yield return [(groups[0], value)];
            }

            yield break;
        }

        foreach (var first in groups[0].Values)
        {
            foreach (var second in groups[1].Values)
            {
                yield return [(groups[0], first), (groups[1], second)];
            }
        }
    }

    private static string BuildDraftListingErrorMessage(int statusCode, string body)
    {
        if (body.Contains("readiness_state_id", StringComparison.OrdinalIgnoreCase))
        {
            return $"Taslak listing olusturulamadi. Fiziksel urunlerde Etsy magazaya bagli gecerli hazirlik durumu (readiness_state_id) ister. Urun Kesif ekranindaki Hazirlik durumu alanindan magazana ait bir deger sec veya mevcut listinglerinden gecen ID'yi gir. HTTP {statusCode}: {body}";
        }

        if (body.Contains("shipping_profile_id", StringComparison.OrdinalIgnoreCase))
        {
            return $"Taslak listing olusturulamadi. Fiziksel urun icin gecerli Shipping profile ID gerekli. Etsy Magaza ayarlarindan teslimat profili ID'sini girip tekrar deneyin. HTTP {statusCode}: {body}";
        }

        return $"Taslak listing olusturulamadi. HTTP {statusCode}: {body}";
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

    public async Task<Dictionary<long, string>> GetSellerTaxonomyNamesAsync(
        EtsyApiSettings settings,
        IEnumerable<long> taxonomyIds,
        CancellationToken cancellationToken = default)
    {
        EnsureApiCredentials(settings);
        var requestedIds = taxonomyIds.Where(id => id > 0).Distinct().ToHashSet();
        var names = new Dictionary<long, string>();
        if (requestedIds.Count == 0)
        {
            return names;
        }

        try
        {
            using var request = CreateRequest(settings, HttpMethod.Get, $"{BaseUrl}/seller-taxonomy/nodes", useAccessToken: false);
            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            if (!response.IsSuccessStatusCode || string.IsNullOrWhiteSpace(body))
            {
                return names;
            }

            using var document = JsonDocument.Parse(body);
            var roots = GetArray(document.RootElement, "results", "nodes");
            if (roots.HasValue)
            {
                foreach (var node in roots.Value.EnumerateArray())
                {
                    CollectTaxonomyNames(node, [], requestedIds, names);
                }
            }
            else
            {
                CollectTaxonomyNames(document.RootElement, [], requestedIds, names);
            }
        }
        catch
        {
            // Kategori adi ek bilgi; okunamazsa arama akisini durdurma.
        }

        return names;
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

    private static List<string> NormalizeListingTags(IEnumerable<string> tags) =>
        tags
            .Select(tag => tag.Trim())
            .Where(tag => tag.Length > 0)
            .Select(tag => tag.Length <= 20 ? tag : tag[..20].TrimEnd())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();

    internal static List<string> NormalizeListingMaterialsForEtsy(IEnumerable<string> materials) =>
        materials
            .Select(SanitizeListingMaterial)
            .Where(material => material.Length > 0)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();

    private static List<string> NormalizeListingMaterials(IEnumerable<string> materials) =>
        NormalizeListingMaterialsForEtsy(materials);

    private static string SanitizeListingMaterial(string material)
    {
        var normalized = (material ?? "")
            .Normalize(NormalizationForm.FormD)
            .Replace('&', ' ')
            .Replace('/', ' ')
            .Replace('\\', ' ')
            .Replace('|', ' ')
            .Replace(',', ' ')
            .Replace(';', ' ')
            .Replace(':', ' ')
            .Replace('.', ' ')
            .Replace('*', ' ')
            .Replace('•', ' ')
            .Replace('–', ' ')
            .Replace('—', ' ');

        var builder = new StringBuilder(normalized.Length);
        foreach (var character in normalized)
        {
            var category = CharUnicodeInfo.GetUnicodeCategory(character);
            if (category == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            if (char.IsLetterOrDigit(character) || character == ' ' || character == '-')
            {
                builder.Append(character);
            }
        }

        var collapsed = string.Join(
            ' ',
            builder
                .ToString()
                .Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Trim('-', ' ');

        return collapsed.Length <= 45
            ? collapsed
            : collapsed[..45].TrimEnd('-', ' ');
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

    private static string ReadTaxonomyName(JsonElement root)
    {
        var path = GetStringArray(root, "path")
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .ToList();
        if (path.Count > 0)
        {
            return string.Join(" > ", path);
        }

        var name = GetFirstString(root, "name", "display_name");
        if (!string.IsNullOrWhiteSpace(name))
        {
            return name;
        }

        var fullPath = GetArray(root, "full_path");
        if (fullPath.HasValue)
        {
            var names = fullPath.Value.EnumerateArray()
                .Select(item => GetFirstString(item, "name", "display_name"))
                .Where(part => !string.IsNullOrWhiteSpace(part))
                .ToList();
            if (names.Count > 0)
            {
                return string.Join(" > ", names);
            }
        }

        return "";
    }

    private static void CollectTaxonomyNames(
        JsonElement node,
        IReadOnlyList<string> parentPath,
        HashSet<long> requestedIds,
        Dictionary<long, string> names)
    {
        var nodeId = GetLong(node, "id");
        if (nodeId <= 0)
        {
            nodeId = GetLong(node, "taxonomy_id");
        }

        var nodeName = GetFirstString(node, "name", "display_name");
        var currentPath = parentPath.ToList();
        if (!string.IsNullOrWhiteSpace(nodeName))
        {
            currentPath.Add(nodeName);
        }

        if (nodeId > 0 && requestedIds.Contains(nodeId) && currentPath.Count > 0)
        {
            names[nodeId] = string.Join(" > ", currentPath);
        }

        var children = GetArray(node, "children", "Children", "child_nodes");
        if (!children.HasValue)
        {
            return;
        }

        foreach (var child in children.Value.EnumerateArray())
        {
            CollectTaxonomyNames(child, currentPath, requestedIds, names);
        }
    }

    private static string BuildShopUrl(string shopName)
    {
        return string.IsNullOrWhiteSpace(shopName)
            ? "https://www.etsy.com"
            : $"https://www.etsy.com/shop/{Uri.EscapeDataString(shopName.Trim())}";
    }

    private static string GetImageContentType(string imagePath)
    {
        var extension = Path.GetExtension(imagePath).ToLowerInvariant();
        return extension switch
        {
            ".jpg" or ".jpeg" => "image/jpeg",
            ".gif" => "image/gif",
            ".webp" => "image/webp",
            _ => "image/png",
        };
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

internal sealed record ListingTextUpdate(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Materials);

internal sealed record DraftListingCreateRequest(
    string Title,
    string Description,
    decimal Price,
    int Quantity,
    long TaxonomyId,
    long ShippingProfileId,
    bool IsDigital,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Materials,
    long ReadinessStateId = 0,
    string WhoMade = "i_did",
    string WhenMade = "made_to_order");

internal sealed record CreatedDraftListing(long ListingId, string Url);

internal sealed record DraftListingInventoryUpdate(
    decimal Price,
    int Quantity,
    long? ReadinessStateId,
    IReadOnlyList<DraftListingVariationGroup> Variations);

internal sealed record DraftListingVariationGroup(
    string Name,
    long PropertyId,
    IReadOnlyList<string> Values);

internal sealed record EtsyShippingProfileOption(long ShippingProfileId, string Title)
{
    public string DisplayName => $"{Title} ({ShippingProfileId})";
}

internal sealed record EtsyReadinessStateOption(long ReadinessStateId, string Title)
{
    public string DisplayName => $"{Title} ({ReadinessStateId})";
}
