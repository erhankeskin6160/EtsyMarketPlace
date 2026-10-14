namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Application.Shipping;
using Microsoft.Extensions.Configuration;

public sealed class EtsyLogisticsApiClient : IEtsyLogisticsApiClient
{
    private readonly HttpClient _httpClient;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IEtsyOAuthService _oauthService;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IConfiguration _config;

    public EtsyLogisticsApiClient(
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

    public async Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/shipping-profiles";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Kargo profilleri Etsy'den alınamadı (HTTP {(int)res.StatusCode}): {body}");
        }
        return body;
    }

    public async Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/readiness-state-definitions";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken ct)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/listings/active?limit=100";

        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

        using var res = await _httpClient.SendAsync(req, ct);
        if (!res.IsSuccessStatusCode) return null;
        return await res.Content.ReadAsStringAsync(ct);
    }

    public async Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken ct)
    {
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(shopId, ct);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var url = "https://api.etsy.com/v3/application/seller-taxonomy/nodes";
        using var req = new HttpRequestMessage(HttpMethod.Get, url);
        req.Headers.Add("x-api-key", apiKeyHeader);

        using var res = await _httpClient.SendAsync(req, ct);
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"Etsy taxonomy nodes API çağrısı başarısız (HTTP {(int)res.StatusCode}): {body}");
        }
        return body;
    }

    public async Task<IReadOnlyList<EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(
        string shopId,
        int lookbackDays = 45,
        bool? wasShipped = null,
        CancellationToken ct = default)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        long minCreated = DateTimeOffset.UtcNow.AddDays(-Math.Max(1, lookbackDays)).ToUnixTimeSeconds();
        const int pageSize = 100;
        var receipts = new List<EtsyFulfillmentReceipt>();
        var offset = 0;

        while (true)
        {
            var url = $"https://api.etsy.com/v3/application/shops/{shopId}/receipts?limit={pageSize}&offset={offset}&min_created={minCreated}";
            if (wasShipped.HasValue)
            {
                url += $"&was_shipped={(wasShipped.Value ? "true" : "false")}";
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Add("x-api-key", apiKeyHeader);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);

            using var res = await _httpClient.SendAsync(req, ct);
            var body = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode)
            {
                throw new InvalidOperationException($"Etsy siparişleri alınamadı (HTTP {(int)res.StatusCode}): {body}");
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
            {
                break;
            }

            var count = 0;
            foreach (var item in results.EnumerateArray())
            {
                count++;
                var parsed = ParseFulfillmentReceipt(item);
                if (parsed != null)
                {
                    receipts.Add(parsed);
                }
            }

            offset += count;
            int totalCount = doc.RootElement.TryGetProperty("count", out var countEl) && countEl.TryGetInt32(out var c) ? c : 0;
            if (count < pageSize || (totalCount > 0 && offset >= totalCount))
            {
                break;
            }
        }

        return receipts;
    }

    public async Task<bool> SubmitTrackingAsync(
        string shopId,
        long receiptId,
        string trackingCode,
        string carrierName,
        bool sendBcc = false,
        CancellationToken ct = default)
    {
        var (accessToken, apiKeyHeader) = await PrepareAuthAsync(shopId, ct);
        var url = $"https://api.etsy.com/v3/application/shops/{shopId}/receipts/{receiptId}/tracking";

        var payload = new
        {
            tracking_code = trackingCode.Trim(),
            carrier_name = carrierName.Trim(),
            send_bcc = sendBcc
        };

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Headers.Add("x-api-key", apiKeyHeader);
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        req.Content = new StringContent(JsonSerializer.Serialize(payload), System.Text.Encoding.UTF8, "application/json");

        using var res = await _httpClient.SendAsync(req, ct);
        return res.IsSuccessStatusCode;
    }

    private static EtsyFulfillmentReceipt ParseFulfillmentReceipt(JsonElement receipt)
    {
        long receiptId = receipt.TryGetProperty("receipt_id", out var rIdProp) && rIdProp.TryGetInt64(out var rId) ? rId : 0;
        var (total, currency) = ReadMoney(receipt, "grandtotal");

        var lines = new List<EtsyFulfillmentLine>();
        if (receipt.TryGetProperty("transactions", out var txArray) && txArray.ValueKind == JsonValueKind.Array)
        {
            foreach (var tx in txArray.EnumerateArray())
            {
                long listingId = tx.TryGetProperty("listing_id", out var lIdProp) && lIdProp.TryGetInt64(out var lId) ? lId : 0;
                string title = tx.TryGetProperty("title", out var titleProp) ? titleProp.GetString() ?? string.Empty : string.Empty;
                int quantity = tx.TryGetProperty("quantity", out var qProp) && qProp.TryGetInt32(out var q) ? Math.Max(1, q) : 1;
                var (price, _) = ReadMoney(tx, "price");
                lines.Add(new EtsyFulfillmentLine(listingId, title, quantity, price));
            }
        }

        long createdTimestamp = 0;
        if (receipt.TryGetProperty("created_timestamp", out var ctProp) && ctProp.TryGetInt64(out var cts))
        {
            createdTimestamp = cts;
        }
        else if (receipt.TryGetProperty("create_timestamp", out var ctProp2) && ctProp2.TryGetInt64(out var cts2))
        {
            createdTimestamp = cts2;
        }

        var trackingCodes = new List<string>();
        if (receipt.TryGetProperty("shipments", out var shipments) && shipments.ValueKind == JsonValueKind.Array)
        {
            foreach (var shipment in shipments.EnumerateArray())
            {
                if (shipment.TryGetProperty("tracking_code", out var codeProp))
                {
                    string? code = codeProp.GetString();
                    if (!string.IsNullOrWhiteSpace(code)) trackingCodes.Add(code.Trim());
                }
            }
        }

        var addr = EtsyAddressParser.Parse(receipt);
        string countryCode = !string.IsNullOrWhiteSpace(addr.CountryCode)
            ? addr.CountryCode
            : (receipt.TryGetProperty("country_iso", out var isoProp) ? isoProp.GetString() ?? "US" : "US");

        string buyerName = !string.IsNullOrWhiteSpace(addr.BuyerName)
            ? addr.BuyerName
            : (receipt.TryGetProperty("name", out var nProp) ? nProp.GetString() ?? string.Empty : string.Empty);

        string buyerEmail = !string.IsNullOrWhiteSpace(addr.BuyerEmail)
            ? addr.BuyerEmail
            : (receipt.TryGetProperty("buyer_email", out var eProp) ? eProp.GetString() ?? string.Empty : string.Empty);

        string phone = !string.IsNullOrWhiteSpace(addr.Phone)
            ? addr.Phone
            : (receipt.TryGetProperty("phone", out var pProp) ? pProp.GetString() ?? string.Empty : string.Empty);

        string ioss = receipt.TryGetProperty("ioss_number", out var iossProp) ? iossProp.GetString() ?? string.Empty : string.Empty;
        bool wasShipped = receipt.TryGetProperty("was_shipped", out var wsProp) && wsProp.ValueKind == JsonValueKind.True;

        return new EtsyFulfillmentReceipt(
            receiptId,
            buyerName,
            buyerEmail,
            phone,
            addr.StreetAddress,
            addr.SecondAddress,
            addr.City,
            addr.State,
            addr.PostalCode,
            countryCode,
            EtsyFulfillmentQueuePolicy.CountryDisplayName(countryCode),
            ioss,
            total,
            currency,
            createdTimestamp > 0 ? DateTimeOffset.FromUnixTimeSeconds(createdTimestamp) : DateTimeOffset.UtcNow,
            wasShipped,
            trackingCodes,
            lines);
    }

    private static (decimal Amount, string Currency) ReadMoney(JsonElement parent, string propertyName)
    {
        if (!parent.TryGetProperty(propertyName, out var element)) return (0m, "USD");
        if (element.ValueKind == JsonValueKind.Number && element.TryGetDecimal(out var num))
        {
            return (num, "USD");
        }
        if (element.ValueKind == JsonValueKind.Object)
        {
            decimal amt = 0m;
            decimal div = 100m;
            if (element.TryGetProperty("amount", out var aProp) && aProp.TryGetDecimal(out var a)) amt = a;
            if (element.TryGetProperty("divisor", out var dProp) && dProp.TryGetDecimal(out var d) && d > 0) div = d;
            string cur = element.TryGetProperty("currency_code", out var cProp) ? cProp.GetString() ?? "USD" : "USD";
            return (amt / div, cur);
        }
        return (0m, "USD");
    }
}
