namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using Microsoft.Extensions.Configuration;

public sealed class EtsyListingApiClient : IEtsyListingApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IConfiguration _config;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    public EtsyListingApiClient(
        HttpClient httpClient,
        IEtsyTokenStore tokenStore,
        IEtsyOAuthService oauthService,
        IShopSettingsRepository settingsRepo,
        IConfiguration config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _tokenStore = tokenStore ?? throw new ArgumentNullException(nameof(tokenStore));
        _oauthService = oauthService ?? throw new ArgumentNullException(nameof(oauthService));
        _settingsRepo = settingsRepo ?? throw new ArgumentNullException(nameof(settingsRepo));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    private async Task<(string AccessToken, string ApiKeyHeader)> PrepareAuthAsync(string shopId, CancellationToken ct)
    {
        var token = await _tokenStore.GetAsync(shopId, ct)
            ?? throw new InvalidOperationException($"Mağaza #{shopId} için aktif OAuth token bulunamadı.");

        if (token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1))
        {
            token = await _oauthService.RefreshTokenAsync(shopId, token.RefreshToken, ct);
            await _tokenStore.SaveAsync(shopId, token, ct);
        }

        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, ct);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);

        if (string.IsNullOrWhiteSpace(keystring))
            throw new InvalidOperationException("Etsy Keystring (Client ID) bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin.");

        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();
        return (token.AccessToken, apiKeyHeader);
    }

    public async Task<(long ListingId, string Url)> CreateDraftListingAsync(
        string shopId,
        CreateDraftListingRawRequest raw,
        CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var client = _httpClient;
        var postUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings";

        var formDict = new List<KeyValuePair<string, string>>
        {
            new("quantity", Math.Max(1, raw.Quantity).ToString(CultureInfo.InvariantCulture)),
            new("title", raw.Title),
            new("description", raw.Description),
            new("price", raw.Price.ToString("0.00", CultureInfo.InvariantCulture)),
            new("who_made", raw.WhoMade),
            new("when_made", raw.WhenMade),
            new("taxonomy_id", raw.TaxonomyId > 0 ? raw.TaxonomyId.ToString(CultureInfo.InvariantCulture) : "1042"),
            new("type", raw.Type),
            new("state", raw.State)
        };

        if (raw.ShippingProfileId.HasValue && raw.ShippingProfileId.Value > 0)
        {
            formDict.Add(new("shipping_profile_id", raw.ShippingProfileId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        if (raw.ReadinessStateId.HasValue && raw.ReadinessStateId.Value > 0)
        {
            formDict.Add(new("readiness_state_id", raw.ReadinessStateId.Value.ToString(CultureInfo.InvariantCulture)));
        }

        foreach (var tag in raw.Tags)
        {
            formDict.Add(new("tags[]", tag));
        }

        foreach (var mat in raw.Materials)
        {
            formDict.Add(new("materials[]", mat));
        }

        using var req = new HttpRequestMessage(HttpMethod.Post, postUrl)
        {
            Content = new FormUrlEncodedContent(formDict)
        };
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await client.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);

        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Etsy ilan oluşturma başarısız (HTTP {(int)res.StatusCode}): {body}");
        }

        long createdListingId = 0;
        string listingUrl = "";
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.TryGetProperty("listing_id", out var lidProp))
            {
                createdListingId = lidProp.GetInt64();
            }
            else if (root.TryGetProperty("results", out var resProp) && resProp.GetArrayLength() > 0)
            {
                createdListingId = resProp[0].GetProperty("listing_id").GetInt64();
            }

            if (root.TryGetProperty("url", out var urlProp))
            {
                listingUrl = urlProp.GetString() ?? "";
            }
        }
        catch { }

        if (createdListingId <= 0)
        {
            throw new InvalidOperationException($"İlan oluşturuldu ancak listing_id okunamadı: {body}");
        }

        if (string.IsNullOrWhiteSpace(listingUrl))
        {
            listingUrl = $"https://www.etsy.com/your/shops/me/listing-editor/edit/{createdListingId}";
        }

        return (createdListingId, listingUrl);
    }

    public async Task<bool> UploadListingImageAsync(
        string shopId,
        long listingId,
        byte[] imageBytes,
        string fileName,
        int rank,
        CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var client = _httpClient;
        var imgUploadUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/{listingId}/images";

        using var imgContent = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(imageBytes);
        fileContent.Headers.ContentType = new MediaTypeHeaderValue("image/jpeg");
        imgContent.Add(fileContent, "image", fileName);
        imgContent.Add(new StringContent(rank.ToString(CultureInfo.InvariantCulture)), "rank");

        using var req = new HttpRequestMessage(HttpMethod.Post, imgUploadUrl)
        {
            Content = imgContent
        };
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await client.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
    }

    public async Task<(bool Success, string? ErrorMessage)> UpdateListingInventoryAsync(
        string shopId,
        long listingId,
        EtsyInventoryPayload payload,
        CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var client = _httpClient;
        var invUrl = $"https://api.etsy.com/v3/application/listings/{listingId}/inventory";

        var jsonString = JsonSerializer.Serialize(payload, JsonOptions);

        using var req = new HttpRequestMessage(HttpMethod.Put, invUrl)
        {
            Content = new StringContent(jsonString, Encoding.UTF8, "application/json")
        };
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await client.SendAsync(req, ct);
        if (res.IsSuccessStatusCode)
        {
            return (true, null);
        }

        var errorBody = await res.Content.ReadAsStringAsync(ct);
        return (false, $"Etsy Envanter Reddi (HTTP {(int)res.StatusCode}): {errorBody}");
    }

    public async Task<bool> SetListingStateAsync(
        string shopId,
        long listingId,
        string state,
        CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var client = _httpClient;
        var patchUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/{listingId}";

        var form = new List<KeyValuePair<string, string>>
        {
            new("state", state)
        };

        using var req = new HttpRequestMessage(HttpMethod.Patch, patchUrl)
        {
            Content = new FormUrlEncodedContent(form)
        };
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await client.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
    }

    public async Task<long?> GetDefaultShippingProfileIdAsync(string shopId, CancellationToken ct)
    {
        try
        {
            var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
            var client = _httpClient;
            var profUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/shipping-profiles";

            using var req = new HttpRequestMessage(HttpMethod.Get, profUrl);
            req.Headers.Add("x-api-key", apiKeyHeader);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("results", out var rArray) && rArray.GetArrayLength() > 0)
                {
                    var first = rArray[0];
                    if (first.TryGetProperty("shipping_profile_id", out var spIdProp))
                    {
                        return spIdProp.GetInt64();
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public async Task<long?> GetDefaultReadinessStateIdAsync(string shopId, CancellationToken ct)
    {
        try
        {
            var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
            var client = _httpClient;

            // 1. Tanımlı readiness state'leri dene
            var readyDefUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/readiness-state-definitions";
            using var req = new HttpRequestMessage(HttpMethod.Get, readyDefUrl);
            req.Headers.Add("x-api-key", apiKeyHeader);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await client.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                var body = await res.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("results", out var rdArray) && rdArray.GetArrayLength() > 0)
                {
                    var firstRd = rdArray[0];
                    if (firstRd.TryGetProperty("readiness_state_id", out var rsIdProp))
                    {
                        return rsIdProp.GetInt64();
                    }
                }
            }

            // 2. Fallback: Aktif ilanlardan birinin readiness state'ini oku
            var activeListingsUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/active?limit=30";
            using var actReq = new HttpRequestMessage(HttpMethod.Get, activeListingsUrl);
            actReq.Headers.Add("x-api-key", apiKeyHeader);
            actReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var actRes = await client.SendAsync(actReq, ct);
            if (actRes.IsSuccessStatusCode)
            {
                var actBody = await actRes.Content.ReadAsStringAsync(ct);
                using var actDoc = JsonDocument.Parse(actBody);
                if (actDoc.RootElement.TryGetProperty("results", out var aListings) && aListings.GetArrayLength() > 0)
                {
                    foreach (var al in aListings.EnumerateArray())
                    {
                        if (al.TryGetProperty("readiness_state_id", out var rsProp) && rsProp.GetInt64() > 0)
                        {
                            return rsProp.GetInt64();
                        }
                    }
                }
            }
        }
        catch { }

        return null;
    }

    public async Task<string> GetShopListingsRawJsonAsync(string shopId, int limit, CancellationToken cancellationToken = default)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, cancellationToken);
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/listings?state=active&limit={clampedLimit}&sort_on=updated&sort_order=desc&includes=Images";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKeyHeader);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var fallbackUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/active?limit={clampedLimit}&sort_on=updated&sort_order=desc";
            using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
            fbReq.Headers.Add("x-api-key", apiKeyHeader);
            fbReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
            using var fbResp = await _httpClient.SendAsync(fbReq, cancellationToken);
            if (!fbResp.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                throw new HttpRequestException($"Etsy API hatası (HTTP {(int)response.StatusCode}): {err}");
            }
            return await fbResp.Content.ReadAsStringAsync(cancellationToken);
        }

        return await response.Content.ReadAsStringAsync(cancellationToken);
    }

    public async Task<string?> GetListingImagesRawJsonAsync(long listingId, string shopId, CancellationToken cancellationToken = default)
    {
        try
        {
            var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, cancellationToken);
            var imgReqUrl = $"https://api.etsy.com/v3/application/listings/{listingId}/images";
            using var imgReq = new HttpRequestMessage(HttpMethod.Get, imgReqUrl);
            imgReq.Headers.Add("x-api-key", apiKeyHeader);
            imgReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var imgResp = await _httpClient.SendAsync(imgReq, cancellationToken);
            if (imgResp.IsSuccessStatusCode)
            {
                return await imgResp.Content.ReadAsStringAsync(cancellationToken);
            }
        }
        catch { }

        return null;
    }

    public async Task<(bool Success, string Body, string? ErrorMessage)> UpdateListingRawAsync(
        string shopId,
        string listingId,
        IReadOnlyDictionary<string, string> formFields,
        CancellationToken cancellationToken = default)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, cancellationToken);
        var patchUrl = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/{listingId}";

        using var patchReq = new HttpRequestMessage(new HttpMethod("PATCH"), patchUrl)
        {
            Content = new FormUrlEncodedContent(formFields)
        };
        patchReq.Headers.Add("x-api-key", apiKeyHeader);
        patchReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var patchRes = await _httpClient.SendAsync(patchReq, cancellationToken);
        var patchBody = await patchRes.Content.ReadAsStringAsync(cancellationToken);

        if (!patchRes.IsSuccessStatusCode)
        {
            return (false, patchBody, $"Etsy güncelleme başarısız (HTTP {(int)patchRes.StatusCode}): {patchBody}");
        }

        return (true, patchBody, null);
    }
}
