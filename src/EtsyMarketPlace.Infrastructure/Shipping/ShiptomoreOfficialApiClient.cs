namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Ship to More resmî REST API istemcisi (openapi v1.1.0).
/// Kimlik doğrulama: HTTP Basic — Client ID kullanıcı adı, Client Secret şifre.
/// Secret, <see cref="IShippingSecretProtector"/> ile şifreli saklanır (Aras tokeni ile aynı mekanizma).
/// </summary>
public sealed class ShiptomoreOfficialApiClient : IShiptomoreOfficialApi
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _http;
    private readonly Func<ShiptomoreSettings> _loadSettings;

    public ShiptomoreOfficialApiClient(HttpClient? httpClient = null, Func<ShiptomoreSettings>? loadSettings = null)
    {
        _http = httpClient ?? new HttpClient { Timeout = TimeSpan.FromSeconds(60) };
        _loadSettings = loadSettings ?? (() => ShiptomoreSettingsStore.Load());
    }

    public string BaseUrl { get; set; } = "https://dev.shiptomore.com";

    public bool HasCredentials
    {
        get
        {
            var s = SafeLoad();
            return s != null
                && !string.IsNullOrWhiteSpace(s.ClientId)
                && !string.IsNullOrWhiteSpace(DecryptSecret(s));
        }
    }

    public async Task<IReadOnlyList<string>> GetProvidersAsync(CancellationToken ct = default)
    {
        using var doc = await SendForJsonAsync(HttpMethod.Get, "/v1/providers", null, ct).ConfigureAwait(false);
        var list = new List<string>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                string value = item.ValueKind == JsonValueKind.String
                    ? item.GetString() ?? string.Empty
                    : (item.TryGetProperty("slug", out var slug) ? slug.GetString() ?? string.Empty : item.ToString());
                if (!string.IsNullOrWhiteSpace(value))
                {
                    list.Add(value);
                }
            }
        }

        return list;
    }

    public async Task<IReadOnlyList<ShiptomoreHsCode>> SearchHsCodesAsync(string query, int limit = 50, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query) || query.Trim().Length < 2)
        {
            return Array.Empty<ShiptomoreHsCode>();
        }

        string url = $"/v1/hs-codes?q={Uri.EscapeDataString(query.Trim())}&limit={Math.Clamp(limit, 1, 100)}";
        using var doc = await SendForJsonAsync(HttpMethod.Get, url, null, ct).ConfigureAwait(false);
        var codes = new List<ShiptomoreHsCode>();
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in doc.RootElement.EnumerateArray())
            {
                codes.Add(new ShiptomoreHsCode
                {
                    Code = item.TryGetProperty("hs_code", out var c) ? c.GetString() ?? string.Empty : string.Empty,
                    Description = item.TryGetProperty("description", out var d) ? d.GetString() ?? string.Empty : string.Empty,
                    UsTariffRate = item.TryGetProperty("us_tariff_rate", out var u) && u.ValueKind == JsonValueKind.Number
                        ? u.GetDouble()
                        : null
                });
            }
        }

        return codes;
    }

    public async Task<IReadOnlyList<ShiptomorePriceOption>> CalculatePricesAsync(ShiptomorePriceRequest request, CancellationToken ct = default)
    {
        using var doc = await SendForJsonAsync(HttpMethod.Post, "/v1/prices/calculate", request, ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<ShiptomorePriceResponse>(doc.RootElement.GetRawText(), JsonOpts);
        return response?.Data ?? new List<ShiptomorePriceOption>();
    }

    public async Task<ShiptomoreShipmentResponse> CreateShipmentAsync(ShiptomoreShipmentRequest request, CancellationToken ct = default)
    {
        using var doc = await SendForJsonAsync(HttpMethod.Post, "/v1/shipments", request, ct).ConfigureAwait(false);
        var response = JsonSerializer.Deserialize<ShiptomoreShipmentResponse>(doc.RootElement.GetRawText(), JsonOpts);

        if (response == null || string.IsNullOrWhiteSpace(response.Id))
        {
            throw new ShiptomoreApiException("Ship to More geçerli bir gönderi kimliği döndürmedi.", 200, Array.Empty<string>());
        }

        return response;
    }

    public async Task<ShiptomoreShipmentDetail> GetShipmentAsync(string slug, bool includeLabels = false, CancellationToken ct = default)
    {
        string url = $"/v1/shipments/{Uri.EscapeDataString(slug)}?include_labels={(includeLabels ? "true" : "false")}";
        using var doc = await SendForJsonAsync(HttpMethod.Get, url, null, ct).ConfigureAwait(false);
        return JsonSerializer.Deserialize<ShiptomoreShipmentDetail>(doc.RootElement.GetRawText(), JsonOpts)
               ?? throw new ShiptomoreApiException("Gönderi detayı okunamadı.", 200, Array.Empty<string>());
    }

    public async Task<byte[]> DownloadLabelAsync(string slug, string labelFormat = "a4", CancellationToken ct = default)
    {
        string format = string.Equals(labelFormat, "thermal", StringComparison.OrdinalIgnoreCase) ? "thermal" : "a4";
        string url = $"/v1/shipments/{Uri.EscapeDataString(slug)}/labels?label_format={format}";

        using var doc = await SendForJsonAsync(HttpMethod.Get, url, null, ct).ConfigureAwait(false);
        var label = JsonSerializer.Deserialize<ShiptomoreLabelData>(doc.RootElement.GetRawText(), JsonOpts);

        if (label == null || string.IsNullOrWhiteSpace(label.Data))
        {
            throw new ShiptomoreApiException("Etiket verisi boş döndü.", 200, Array.Empty<string>());
        }

        try
        {
            return Convert.FromBase64String(label.Data);
        }
        catch (FormatException ex)
        {
            throw new ShiptomoreApiException("Etiket Base64 çözülemedi: " + ex.Message, 200, Array.Empty<string>());
        }
    }

    public async Task CancelShipmentAsync(string slug, CancellationToken ct = default)
    {
        using var request = new HttpRequestMessage(HttpMethod.Delete, BaseUrl + $"/v1/shipments/{Uri.EscapeDataString(slug)}");
        ApplyAuth(request);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Trace("DELETE /v1/shipments/{slug}", $"{(int)response.StatusCode} | {body}");

        if (!response.IsSuccessStatusCode)
        {
            throw BuildException(response.StatusCode, body);
        }
    }

    // ------------------------------------------------------------------ yardımcılar

    private async Task<JsonDocument> SendForJsonAsync(HttpMethod method, string relativeUrl, object? payload, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(method, BaseUrl + relativeUrl);
        ApplyAuth(request);

        string requestJson = string.Empty;
        if (payload != null)
        {
            requestJson = JsonSerializer.Serialize(payload, JsonOpts);
            request.Content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        }

        Trace($"{method} {relativeUrl}", requestJson);

        using var response = await _http.SendAsync(request, ct).ConfigureAwait(false);
        string body = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        Trace($"{method} {relativeUrl} → yanıt", $"{(int)response.StatusCode} | {body}");

        if (!response.IsSuccessStatusCode)
        {
            throw BuildException(response.StatusCode, body);
        }

        return JsonDocument.Parse(string.IsNullOrWhiteSpace(body) ? "{}" : body);
    }

    private void ApplyAuth(HttpRequestMessage request)
    {
        var settings = SafeLoad();
        string clientId = settings?.ClientId?.Trim() ?? string.Empty;
        string secret = DecryptSecret(settings);

        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(secret))
        {
            throw new ShiptomoreApiException(
                "Ship to More API kimlik bilgileri tanımlı değil. Client ID ve Client Secret girilmeli.",
                401,
                new[] { "Ayarlar → Ship to More bölümünden API anahtarlarını girin." });
        }

        string raw = $"{clientId}:{secret}";
        request.Headers.Authorization = new AuthenticationHeaderValue(
            "Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes(raw)));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
    }

    private ShiptomoreSettings? SafeLoad()
    {
        try
        {
            return _loadSettings();
        }
        catch
        {
            return null;
        }
    }

    private static string DecryptSecret(ShiptomoreSettings? settings)
    {
        if (settings == null)
        {
            return string.Empty;
        }

        // Depo, korumali dosyayi okurken duz metni ClientSecret'a koyar (bellekte).
        if (!string.IsNullOrWhiteSpace(settings.ClientSecret))
        {
            return settings.ClientSecret!;
        }

        if (string.IsNullOrWhiteSpace(settings.EncryptedClientSecret))
        {
            return string.Empty;
        }

        var protector = ShippingSecretProtector.Current;
        return protector == null
            ? settings.EncryptedClientSecret
            : protector.Unprotect(settings.EncryptedClientSecret);
    }

    /// <summary>FastAPI hata gövdesini (detail[].msg) okunabilir mesaja çevirir.</summary>
    private static ShiptomoreApiException BuildException(HttpStatusCode status, string body)
    {
        var details = new List<string>();
        string summary = $"Ship to More API hatası ({(int)status}).";

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("detail", out var detail))
            {
                if (detail.ValueKind == JsonValueKind.Array)
                {
                    foreach (var item in detail.EnumerateArray())
                    {
                        string loc = item.TryGetProperty("loc", out var l) && l.ValueKind == JsonValueKind.Array
                            ? string.Join(".", l.EnumerateArray().Select(x => x.ToString()))
                            : string.Empty;
                        string msg = item.TryGetProperty("msg", out var m) ? m.GetString() ?? string.Empty : string.Empty;
                        details.Add(string.IsNullOrWhiteSpace(loc) ? msg : $"{loc}: {msg}");
                    }
                }
                else if (detail.ValueKind == JsonValueKind.String)
                {
                    details.Add(detail.GetString() ?? string.Empty);
                }
            }
        }
        catch
        {
            // gövde JSON değilse özet mesajla devam edilir
        }

        if (details.Count > 0)
        {
            summary += " " + string.Join(" | ", details);
        }
        else if (!string.IsNullOrWhiteSpace(body))
        {
            summary += " " + (body.Length > 400 ? body[..400] : body);
        }

        return new ShiptomoreApiException(summary, (int)status, details, body);
    }

    /// <summary>Aras entegrasyonundaki izleme deseninin aynısı — teşhis için istek/yanıt kaydı.</summary>
    internal static void Trace(string step, string content)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms",
                "captures");
            Directory.CreateDirectory(folder);
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [Shiptomore] [{step}]\n{content}\n{new string('-', 60)}\n";
            File.AppendAllText(Path.Combine(folder, "shiptomore_api_trace.log"), line);
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }
}
