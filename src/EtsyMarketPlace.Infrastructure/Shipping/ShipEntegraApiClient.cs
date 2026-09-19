namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.Globalization;
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

    public ShipEntegraApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
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

    private static void ValidateStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ShipEntegraTokenExpiredException(
                $"ShipEntegra yetkilendirme hatası ({(int)response.StatusCode} {response.ReasonPhrase}). Oturum süreniz dolmuş olabilir.");
        }

        if (!response.IsSuccessStatusCode)
        {
            throw new HttpRequestException(
                $"ShipEntegra API isteği başarısız oldu: {(int)response.StatusCode} {response.ReasonPhrase}");
        }
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
