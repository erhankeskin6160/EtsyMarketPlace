using EtsyMarketPlace.Infrastructure.Http;
namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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
/// ShipEntegra (api.shipentegra.com) resmi REST API istemcisi.
/// </summary>
public sealed class ShipEntegraApiClient : IShipEntegraApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseEndpoint = "https://api.shipentegra.com/v1/tools/calculate/all";
    private const string BaseUrlV1 = "https://api.shipentegra.com/v1";

    public ShipEntegraApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient.Instance;
    }

    /// <summary>
    /// Canlı ShipEntegra API'sinden fiyat hesaplama akışını yürütür ve teklifleri çeker.
    /// </summary>
    public async Task<List<ShipEntegraQuoteOffer>> FetchLiveQuotesAsync(
        ShipEntegraQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        if (string.IsNullOrWhiteSpace(cleanToken))
        {
            throw new ShipEntegraTokenExpiredException("ShipEntegra oturum tokeni boş veya tanımsız. Lütfen geçerli bir token girin.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        // ShipEntegra API'si "kgDesi" parametresini string olarak bekler (Örn: "0.6" veya "1.2")
        double billable = request.BillableWeightKg;
        string kgDesiStr = billable.ToString("0.#", CultureInfo.InvariantCulture);

        var payload = new
        {
            country = request.ReceiverCountry.ToUpperInvariant(),
            kgDesi = kgDesiStr
        };

        string jsonBody = JsonSerializer.Serialize(payload);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        // Yetki / Token Kontrolü
        if (root.TryGetProperty("code", out var codeElem))
        {
            int code = codeElem.GetInt32();
            if (code is 401 or 40100 or 403)
            {
                throw new ShipEntegraTokenExpiredException("ShipEntegra oturum tokeninizin süresi doldu (401 Unauthorized).");
            }
        }

        if (root.TryGetProperty("status", out var statusElem) &&
            !statusElem.GetString()!.Equals("success", StringComparison.OrdinalIgnoreCase))
        {
            string msg = root.TryGetProperty("message", out var m) ? m.GetString() ?? "" : "Bilinmeyen API yanıtı";
            throw new InvalidOperationException($"ShipEntegra API uyarısı: {msg}");
        }

        var offers = new List<ShipEntegraQuoteOffer>();
        if (!root.TryGetProperty("data", out var dataElem))
        {
            return offers;
        }

        string bestCarrier = dataElem.TryGetProperty("bestCarrier", out var bc) ? bc.GetString() ?? "" : "";

        if (dataElem.TryGetProperty("prices", out var pricesElem) && pricesElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var p in pricesElem.EnumerateArray())
            {
                string serviceName = p.TryGetProperty("serviceName", out var sn) ? sn.GetString() ?? "" : "";
                string clearName = p.TryGetProperty("clearServiceName", out var cn) ? cn.GetString() ?? "" : serviceName;
                string serviceType = p.TryGetProperty("serviceType", out var st) ? st.GetString() ?? "ECO" : "ECO";
                string currency = p.TryGetProperty("currency", out var curr) ? curr.GetString() ?? "USD" : "USD";
                string addDesc = p.TryGetProperty("additionalDescription", out var desc) ? desc.GetString() ?? "" : "";
                string tooltip = p.TryGetProperty("tooltip", out var tt) ? tt.GetString() ?? "" : "";

                decimal cargoPrice = p.TryGetProperty("cargoPrice", out var cp) ? cp.GetDecimal() : 0m;
                decimal fuelCost = p.TryGetProperty("fuelCost", out var fc) ? fc.GetDecimal() : 0m;
                decimal totalPrice = p.TryGetProperty("totalPrice", out var tp) ? tp.GetDecimal() : (cargoPrice + fuelCost);

                bool isBest = !string.IsNullOrWhiteSpace(bestCarrier) &&
                              serviceName.Equals(bestCarrier, StringComparison.OrdinalIgnoreCase);

                offers.Add(new ShipEntegraQuoteOffer
                {
                    ServiceName = serviceName,
                    ClearServiceName = clearName,
                    ServiceType = serviceType,
                    CargoPrice = cargoPrice,
                    FuelCost = fuelCost,
                    TotalPrice = totalPrice,
                    Currency = currency,
                    AdditionalDescription = addDesc,
                    Tooltip = tooltip,
                    IsBestCarrier = isBest,
                    IsLivePrice = true
                });
            }
        }

        return offers;
    }

    /// <summary>Sipariş oluşturur (panel sözleşmesi). Dönen modelde sipariş ve kalem kimlikleri bulunur.</summary>
    public async Task<ShipEntegraOrderResult> CreateOrderAsync(
        ShipEntegraCreateOrderRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrlV1 + "/orders");
        ApplyHeaders(httpRequest, cleanToken);

        string json = JsonSerializer.Serialize(request);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        LogShipEntegraTrace("CreateOrder-Request", json);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogShipEntegraTrace("CreateOrder-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");
        ValidateStatus(response, responseContent);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"ShipEntegra sipariş oluşturma HTTP ({(int)response.StatusCode}). Yanıt: {responseContent}");
        }

        return ParseOrderResult(responseContent);
    }

    /// <summary>Siparişin kalem kimliklerini getirir (/orders/{id}/items).</summary>
    public async Task<List<long>> GetOrderItemsAsync(
        long orderId,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, BaseUrlV1 + $"/orders/{orderId}/items");
        ApplyHeaders(httpRequest, cleanToken);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogShipEntegraTrace("GetOrderItems-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");

        var ids = new List<long>();
        try
        {
            using var doc = JsonDocument.Parse(responseContent);
            var root = doc.RootElement;
            JsonElement list = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElem))
            {
                list = dataElem;
            }

            if (list.ValueKind == JsonValueKind.Array)
            {
                foreach (var item in list.EnumerateArray())
                {
                    long id = TryReadLong(item, "id");
                    if (id == 0)
                    {
                        id = TryReadLong(item, "itemId");
                    }

                    if (id != 0)
                    {
                        ids.Add(id);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ayrıştırılamayan yanıt boş liste döner; çağıran taraf anlamlı hata üretir.
        }

        return ids;
    }

    /// <summary>Etiket oluşturur ve ham yanıt baytlarını döner (PDF veya JSON olabilir).</summary>
    public async Task<byte[]?> CreateLabelAsync(
        ShipEntegraCreateLabelRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrlV1 + "/logistics/labels/shipentegra");
        ApplyHeaders(httpRequest, cleanToken);

        string json = JsonSerializer.Serialize(request);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");
        LogShipEntegraTrace("CreateLabel-Request", json);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        string labelBodyPreview = bytes.Length > 0 && bytes[0] == 0x25
            ? $"{bytes.Length} bayt (dosya)"
            : Encoding.UTF8.GetString(bytes, 0, Math.Min(bytes.Length, 400));
        LogShipEntegraTrace("CreateLabel-Response", $"Status: {(int)response.StatusCode} | {labelBodyPreview}");
        ValidateStatus(response, labelBodyPreview);

        return bytes;
    }

    private static ShipEntegraOrderResult ParseOrderResult(string json)
    {
        var result = new ShipEntegraOrderResult { RawJson = json };
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            JsonElement data = root;
            if (root.ValueKind == JsonValueKind.Object && root.TryGetProperty("data", out var dataElem) && dataElem.ValueKind == JsonValueKind.Object)
            {
                data = dataElem;
            }

            result.OrderId = TryReadLong(data, "id");
            if (result.OrderId == 0)
            {
                result.OrderId = TryReadLong(data, "orderId");
            }

            if (data.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
            {
                foreach (var it in items.EnumerateArray())
                {
                    long id = TryReadLong(it, "id");
                    if (id == 0)
                    {
                        id = TryReadLong(it, "itemId");
                    }

                    if (id != 0)
                    {
                        result.ItemIds.Add(id);
                    }
                }
            }
        }
        catch (Exception)
        {
            // Ham yanıt zaten result.RawJson içinde; çağıran taraf değerlendirir.
        }

        return result;
    }

    private static long TryReadLong(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var prop))
        {
            return 0;
        }

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetInt64(out long value))
        {
            return value;
        }

        if (prop.ValueKind == JsonValueKind.String && long.TryParse(prop.GetString(), out long parsed))
        {
            return parsed;
        }

        return 0;
    }

    private static void LogShipEntegraTrace(string step, string content)
    {
        try
        {
            string folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            string logPath = Path.Combine(folder, "shipentegra_api_trace.log");
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{step}]\n{content}\n----------------------------------------\n";
            File.AppendAllText(logPath, line);
        }
        catch (Exception)
        {
            // Teşhis yazımı akışı etkilemez.
        }
    }

    private static void ApplyHeaders(HttpRequestMessage req, string token)
    {
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        req.Headers.TryAddWithoutValidation("Accept-Language", "tr");
        req.Headers.TryAddWithoutValidation("Origin", "https://app.shipentegra.com");
        req.Headers.TryAddWithoutValidation("Referer", "https://app.shipentegra.com/");
        req.Headers.TryAddWithoutValidation("X-Shipentegra-Client-Os", "WEB");
    }

    private static void ValidateStatus(HttpResponseMessage response, string? responseBody = null)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ShipEntegraTokenExpiredException(
                $"ShipEntegra yetkilendirme hatası ({(int)response.StatusCode} {response.ReasonPhrase}). Oturum süreniz dolmuş olabilir.");
        }

        if (!response.IsSuccessStatusCode)
        {
            string detail = string.IsNullOrWhiteSpace(responseBody)
                ? string.Empty
                : " | Yanıt: " + (responseBody!.Length > 400 ? responseBody.Substring(0, 400) : responseBody);
            throw new HttpRequestException(
                $"ShipEntegra API isteği başarısız oldu: {(int)response.StatusCode} {response.ReasonPhrase}{detail}");
        }
    }

    /// <summary>
    /// E-posta/şifre ile doğrudan API oturumu açar; v4.public token çiftini döndürür.
    /// Otomatik token yenilemenin tarayıcısız yoludur (panel ile aynı uç: /v1/auth/login).
    /// </summary>
    public async Task<ShipEntegraAuthTokens?> LoginAsync(
        string email,
        string password,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(password))
        {
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrlV1 + "/auth/login");
        httpRequest.Headers.TryAddWithoutValidation("Origin", "https://app.shipentegra.com");
        httpRequest.Headers.TryAddWithoutValidation("Referer", "https://app.shipentegra.com/");

        var payload = new
        {
            email = email,
            password = password,
            pushId = " ",
            keepMeOpen = false
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        string body = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            return null;
        }

        var (access, refresh) = ShipEntegraTokenExtractor.ExtractFromText(body);
        if (string.IsNullOrWhiteSpace(access))
        {
            return null;
        }

        return new ShipEntegraAuthTokens
        {
            AccessToken = access!,
            RefreshToken = refresh ?? string.Empty
        };
    }

    public static string CleanToken(string? rawToken)
    {
        if (string.IsNullOrWhiteSpace(rawToken)) return string.Empty;
        string t = rawToken.Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..].Trim();
        }
        return t;
    }
}
