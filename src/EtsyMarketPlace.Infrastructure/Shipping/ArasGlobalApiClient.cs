namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
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
/// Aras Global (api.arasglobalcargo.com) resmi REST API istemcisi.
/// </summary>
public sealed class ArasGlobalApiClient : IArasGlobalApiClient
{
    private readonly HttpClient _httpClient;
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private const string BaseUrl = "https://api.arasglobalcargo.com";
    private const string StartCalcEndpoint = "/ShipmentPricing/StartCargoProviderBasePriceCalculation";
    private const string GetPricesEndpoint = "/ShipmentPricing/GetShipmentBasePriceList";

    public ArasGlobalApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
    }

    /// <summary>
    /// Canlı Aras Global API'sinden fiyat hesaplama akışını başlatır ve teklifleri çeker.
    /// </summary>
    public async Task<List<ArasGlobalQuoteOffer>> FetchLiveQuotesAsync(
        ArasGlobalQuoteRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        if (string.IsNullOrWhiteSpace(cleanToken))
        {
            throw new ArasGlobalTokenExpiredException("Aras Global oturum tokeni boş veya tanımsız. Lütfen geçerli bir token girin.");
        }

        // 1. Adım: Fiyat hesaplamayı başlat ve ReferenceCode al
        string referenceCode = await StartCalculationAsync(request, cleanToken, cancellationToken);
        if (string.IsNullOrWhiteSpace(referenceCode))
        {
            throw new InvalidOperationException("Aras Global hesaplama başlatılamadı veya referans kodu alınamadı.");
        }

        // 2. Adım: Teklifler sonuçlanana kadar GetShipmentBasePriceList'i sorgula (Maks 5 deneme)
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(800, cancellationToken); // Kısa bekleme

            var (isConcluded, offers) = await QueryBasePricesAsync(referenceCode, cleanToken, cancellationToken);
            if (offers.Count > 0 && (isConcluded || attempt >= 4))
            {
                return offers;
            }
        }

        return [];
    }

    private async Task<string> StartCalculationAsync(
        ArasGlobalQuoteRequest req,
        string token,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + StartCalcEndpoint);
        ApplyHeaders(httpRequest, token);

        var payload = new
        {
            ShipmentDimensions = new[]
            {
                new
                {
                    Weight = req.WeightKg,
                    Width = req.WidthCm,
                    Length = req.LengthCm,
                    Height = req.HeightCm,
                    ShipmentItems = new[]
                    {
                        new
                        {
                            Quantity = 1,
                            HsCode = string.Empty,
                            UnitPrice = req.ItemUnitPrice
                        }
                    }
                }
            },
            CompanyId = string.Empty,
            Currency = req.Currency,
            DiscountRates = Array.Empty<object>(),
            InternationalShipmentCategory = 0,
            IsIndividualCustomer = req.IsIndividualCustomer,
            PackageType = req.PackageType,
            ReceiverCity = req.ReceiverCity,
            ReceiverCountry = req.ReceiverCountry,
            ReceiverPostalCode = req.ReceiverPostalCode,
            ReceiverState = req.ReceiverState,
            ReceiverTown = req.ReceiverTown,
            SenderCountry = req.SenderCountry,
            TotalPrice = req.ItemUnitPrice
        };

        string jsonBody = JsonSerializer.Serialize(payload);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        if (root.TryGetProperty("resultCode", out var code) && code.GetInt32() == 401)
        {
            throw new ArasGlobalTokenExpiredException("Aras Global oturum tokeninizin süresi doldu (401 Unauthorized).");
        }

        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.String)
        {
            return payloadElem.GetString() ?? string.Empty;
        }

        return string.Empty;
    }

    private async Task<(bool isConcluded, List<ArasGlobalQuoteOffer> offers)> QueryBasePricesAsync(
        string referenceCode,
        string token,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + GetPricesEndpoint);
        ApplyHeaders(httpRequest, token);

        var payload = new { ReferenceCode = referenceCode };
        string jsonBody = JsonSerializer.Serialize(payload);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        if (root.TryGetProperty("resultCode", out var code) && code.GetInt32() == 401)
        {
            throw new ArasGlobalTokenExpiredException("Aras Global oturum tokeninizin süresi doldu (401 Unauthorized).");
        }

        var offers = new List<ArasGlobalQuoteOffer>();
        bool isConcluded = false;

        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payloadElem.EnumerateArray())
            {
                if (item.TryGetProperty("isConcluded", out var concludedElem))
                {
                    isConcluded = concludedElem.GetBoolean();
                }

                if (item.TryGetProperty("baseShipmentPriceModels", out var modelsElem) &&
                    modelsElem.ValueKind == JsonValueKind.Array)
                {
                    foreach (var m in modelsElem.EnumerateArray())
                    {
                        var offer = new ArasGlobalQuoteOffer
                        {
                            Cargo = m.TryGetProperty("cargo", out var c) ? c.GetString() ?? "" : "",
                            Price = m.TryGetProperty("price", out var p) ? p.GetDecimal() : 0m,
                            UnDiscountedPrice = m.TryGetProperty("unDiscountedPrice", out var up) ? up.GetDecimal() : 0m,
                            Currency = m.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "USD" : "USD",
                            DiscountRate = m.TryGetProperty("discountRate", out var dr) ? dr.GetDouble() : 0.0,
                            EstimatedStartDeliveryDate = m.TryGetProperty("estimatedStartDeliveryDate", out var sd) ? sd.GetDouble() : 0.0,
                            EstimatedEndDeliveryDate = m.TryGetProperty("estimatedEndDeliveryDate", out var ed) ? ed.GetDouble() : 0.0,
                            ProviderServiceType = m.TryGetProperty("providerServiceType", out var pst) ? pst.GetString() ?? "" : "",
                            IsActive = m.TryGetProperty("isActive", out var ia) && ia.GetBoolean(),
                            IsLivePrice = true
                        };

                        if (offer.Price > 0)
                        {
                            offers.Add(offer);
                        }
                    }
                }
            }
        }

        return (isConcluded, offers);
    }

    private static void ApplyHeaders(HttpRequestMessage req, string token)
    {
        req.Headers.Accept.Clear();
        req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json") { CharSet = "utf-8" });
        req.Headers.TryAddWithoutValidation("Accept-Language", "tr-TR");
        req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
    }

    private static void ValidateStatus(HttpResponseMessage response)
    {
        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            throw new ArasGlobalTokenExpiredException(
                $"Aras Global oturum tokeninizin süresi doldu! (HTTP {(int)response.StatusCode} {response.ReasonPhrase})");
        }

        response.EnsureSuccessStatusCode();
    }

    public static string CleanToken(string? token)
    {
        if (string.IsNullOrWhiteSpace(token)) return string.Empty;
        string t = token.Trim();
        if (t.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            t = t[7..].Trim();
        }
        return t;
    }
}
