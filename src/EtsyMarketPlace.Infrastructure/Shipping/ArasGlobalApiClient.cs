namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
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
    private const string TranslateCurrencyEndpoint = "/ShipmentPricing/TranslateCurrency";
    private const string SearchGtipEndpoint = "/Shipment/SearchGtipCode";
    private const string AdditionalOptionsEndpoint = "/ShipmentPricing/GetAdditionalOptions";
    private const string AdditionalInfoEndpoint = "/ShipmentPricing/GetShipmentPricingAdditionalInformation";
    private const string CreateShipmentEndpoint = "/Shipment/CreateShipment";
    private const string UpdateShipmentEndpoint = "/Shipment/UpdateShipment";
    private const string CalculatePriceEndpoint = "/ShipmentPricing/CalculateShipmentPrice";
    private const string LegalDocEndpoint = "/Shipment/GetShipmentLegalDocument";
    private const string SendShipmentPriceEndpoint = "/ShipmentPricing/SendShipmentPrice";

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

        // 2. Adım: Teklifler sonuçlanana kadar GetShipmentBasePriceList'i sorgula (Maks 6 deneme)
        for (int attempt = 1; attempt <= 6; attempt++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Delay(750, cancellationToken); // Kısa bekleme

            var (isConcluded, offers) = await QueryBasePricesAsync(referenceCode, cleanToken, cancellationToken);

            if (isConcluded && offers.Count > 0)
            {
                return offers;
            }

            if (attempt == 6 && offers.Count > 0)
            {
                return offers; // Süre bitti ama gelen teklifler varsa dön
            }
        }

        return new List<ArasGlobalQuoteOffer>();
    }

    /// <summary>
    /// Etsy USD tutarını EUR veya TRY'ye çevirir (IOSS €150 kontrolü ve gümrük için).
    /// </summary>
    public async Task<decimal> TranslateCurrencyAsync(
        decimal price,
        string entryCurrency,
        string exitCurrency,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + TranslateCurrencyEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new
        {
            Price = price,
            EntryCurrency = entryCurrency,
            ExitCurrency = exitCurrency
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Number)
        {
            return payloadElem.GetDecimal();
        }

        return price;
    }

    /// <summary>
    /// GTIP / HS Kodu arar.
    /// </summary>
    public async Task<List<ArasGtipSearchResult>> SearchGtipCodeAsync(
        string keyword,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + SearchGtipEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new { keyword = keyword };
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        var results = new List<ArasGtipSearchResult>();
        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payloadElem.EnumerateArray())
            {
                string code = item.TryGetProperty("code", out var c) ? c.GetString() ?? "" : "";
                string desc = item.TryGetProperty("description", out var d) ? d.GetString() ?? "" : "";
                if (!string.IsNullOrWhiteSpace(code))
                {
                    results.Add(new ArasGtipSearchResult { Code = code, Description = desc });
                }
            }
        }

        return results;
    }

    /// <summary>
    /// DDP, DDU, IOSS gümrük opsiyonlarını ve masraf zorunluluklarını getirir.
    /// </summary>
    public async Task<ArasAdditionalOptions> GetAdditionalOptionsAsync(
        string destinationCountry,
        string provider,
        decimal orderTotalUsd,
        string currency,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + AdditionalOptionsEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new
        {
            DestinationCountry = destinationCountry,
            InternationalShipmentProvider = provider,
            OrderTotalUSD = orderTotalUsd,
            Currency = currency
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        var result = new ArasAdditionalOptions();
        if (root.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            result.DefaultMethod = p.TryGetProperty("defaultMethod", out var dm) ? dm.GetString() ?? "DDP" : "DDP";
            result.CustomsFeeRequired = p.TryGetProperty("customsFee", out var cf) && cf.GetBoolean();
            result.CustomsProcessFeeRequired = p.TryGetProperty("customsProcessFee", out var cpf) && cpf.GetBoolean();

            if (p.TryGetProperty("options", out var optsElem) && optsElem.ValueKind == JsonValueKind.Array)
            {
                foreach (var opt in optsElem.EnumerateArray())
                {
                    if (opt.TryGetProperty("method", out var m) &&
                        opt.TryGetProperty("available", out var avail) && avail.GetBoolean())
                    {
                        result.AvailableMethods.Add(m.GetString() ?? "");
                    }
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Taşıyıcıya ait desi uyarı ve resmi bilgilendirme notlarını getirir.
    /// </summary>
    public async Task<string> GetAdditionalInformationAsync(
        string provider,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + AdditionalInfoEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new { internationalcargoprovider = provider.ToLowerInvariant() };
        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in payloadElem.EnumerateArray())
            {
                if (item.TryGetProperty("notes", out var notesElem))
                {
                    return notesElem.GetString() ?? string.Empty;
                }
            }
        }

        return string.Empty;
    }

    /// <summary>
    /// Gönderi taslağını oluşturur ve gönderi ID'si ile referans kodunu döner.
    /// </summary>
    public async Task<ArasCreateShipmentResponse> CreateShipmentAsync(
        ArasCreateShipmentRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + CreateShipmentEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        string json = JsonSerializer.Serialize(request, JsonOpts);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        LogApiTrace("CreateShipment-Request", json);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogApiTrace("CreateShipment-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Aras Global CreateShipment HTTP ({(int)response.StatusCode}). Body: {responseContent}\nREQUEST JSON: {json}");
        }

        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        int resultCode = root.TryGetProperty("resultCode", out var code) ? code.GetInt32() : (int)response.StatusCode;
        string resultMessage = root.TryGetProperty("resultMessage", out var msgElem) ? msgElem.GetString() ?? "" : "";

        if (resultCode != 200)
        {
            throw new InvalidOperationException($"Aras Global Gönderi Başlatma Hatası ({resultCode}): {resultMessage}" + "\nRAW RESPONSE: " + responseContent + "\nREQUEST JSON: " + json);
        }

        var result = new ArasCreateShipmentResponse
        {
            IsSuccess = true,
            ResultCode = resultCode,
            ResultMessage = resultMessage,
            RawJson = responseContent
        };

        if (root.TryGetProperty("payload", out var payloadElem))
        {
            if (payloadElem.ValueKind == JsonValueKind.String)
            {
                result.ShipmentId = payloadElem.GetString() ?? string.Empty;
                result.ReferenceCode = result.ShipmentId;
            }
            else if (payloadElem.ValueKind == JsonValueKind.Object)
            {
                if (payloadElem.TryGetProperty("id", out var idElem))
                {
                    result.ShipmentId = idElem.GetString() ?? string.Empty;
                }
                else if (payloadElem.TryGetProperty("shipmentId", out var sIdElem))
                {
                    result.ShipmentId = sIdElem.GetString() ?? string.Empty;
                }

                if (payloadElem.TryGetProperty("referenceCode", out var refElem))
                {
                    result.ReferenceCode = refElem.GetString() ?? string.Empty;
                }
                else if (payloadElem.TryGetProperty("shipmentNumber", out var numElem))
                {
                    result.ReferenceCode = numElem.GetString() ?? string.Empty;
                }
                else if (payloadElem.TryGetProperty("code", out var codeElem))
                {
                    result.ReferenceCode = codeElem.GetString() ?? string.Empty;
                }
            }
        }

        if (string.IsNullOrWhiteSpace(result.ShipmentId))
        {
            throw new InvalidOperationException($"Aras Global sunucusu geçerli bir Gönderi ID'si dönmedi. Yanıt: {resultMessage}");
        }

        if (string.IsNullOrWhiteSpace(result.ReferenceCode))
        {
            result.ReferenceCode = result.ShipmentId;
        }

        return result;
    }

    /// <summary>
    /// Gönderiyi seçilen taşıyıcı, ürünler ve nihai detaylarla günceller.
    /// </summary>
    public async Task<bool> UpdateShipmentAsync(
        ArasCreateShipmentRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + UpdateShipmentEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        string json = JsonSerializer.Serialize(request, JsonOpts);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        LogApiTrace("UpdateShipment-Request", json);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogApiTrace("UpdateShipment-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");

        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        int resultCode = root.TryGetProperty("resultCode", out var code) ? code.GetInt32() : (int)response.StatusCode;
        if (resultCode != 200)
        {
            string msg = root.TryGetProperty("resultMessage", out var msgElem) ? msgElem.GetString() ?? "" : "";
            throw new InvalidOperationException($"Aras Global Gönderi Güncelleme Hatası ({resultCode}): {msg}");
        }

        return true;
    }

    /// <summary>
    /// Gümrük, kargo, kur ve hizmet bedellerini kuruşu kuruşuna hesaplar.
    /// </summary>
    public async Task<ArasShipmentPriceBreakdown> CalculateShipmentPriceAsync(
        string shipmentId,
        string provider,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + CalculatePriceEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new
        {
            internationalcargoprovider = provider.ToLowerInvariant(),
            saturdayshipment = false,
            insurancepayment = false,
            extraboxpayment = false,
            location = true,
            servicefeespayment = true,
            iscalculatedservice = true,
            shipmentid = shipmentId
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        var result = new ArasShipmentPriceBreakdown();
        if (root.TryGetProperty("payload", out var p) && p.ValueKind == JsonValueKind.Object)
        {
            result.BasePrice = p.TryGetProperty("basePrice", out var bp) ? bp.GetDecimal() : 0m;
            result.ExchangeTotalPrice = p.TryGetProperty("exchangeTotalPrice", out var etp) ? etp.GetDecimal() : 0m;
            result.ExchangeCurrency = p.TryGetProperty("exchangeCurrency", out var ec) ? ec.GetString() ?? "USD" : "USD";
            result.Currency = p.TryGetProperty("currency", out var cur) ? cur.GetString() ?? "TRY" : "TRY";
            result.ExchangeRate = p.TryGetProperty("exchangeRate", out var er) ? er.GetDecimal() : 1m;
            result.TotalPrice = p.TryGetProperty("totalPrice", out var tp) ? tp.GetDecimal() : 0m;
            result.TotalPriceWithProvisionRate = p.TryGetProperty("totalPriceWithProvisionRate", out var tpr) ? tpr.GetDecimal() : 0m;

            if (p.TryGetProperty("additionalServices", out var addServices) && addServices.ValueKind == JsonValueKind.Array)
            {
                foreach (var s in addServices.EnumerateArray())
                {
                    string name = s.TryGetProperty("name", out var n) ? n.GetString() ?? "" : "";
                    decimal price = s.TryGetProperty("price", out var sp) ? sp.GetDecimal() : 0m;
                    result.AdditionalServices.Add(new ArasAdditionalServiceItem { Name = name, Price = price });
                }
            }
        }

        return result;
    }

    /// <summary>
    /// Ön bilgilendirme ve yurtdışı taşıma sözleşmesi onay GUID'ini alır.
    /// </summary>
    public async Task<string> GetShipmentLegalDocumentAsync(
        string shipmentId,
        string docType,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + LegalDocEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new
        {
            ShipmentId = shipmentId,
            DocType = docType // "shipmentpreinformation" veya "shipmentagreement"
        };

        httpRequest.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

        if (root.TryGetProperty("payload", out var payloadElem) && payloadElem.ValueKind == JsonValueKind.String)
        {
            return payloadElem.GetString() ?? string.Empty;
        }

        return Guid.NewGuid().ToString();
    }

    /// <summary>
    /// Fiyatlandırma adımında seçilen taşıyıcı teklifini ve onayları Aras Global'e gönderir (SendShipmentPrice).
    /// </summary>
    public async Task<bool> SendShipmentPriceAsync(
        string shipmentId,
        string provider,
        decimal cargoPrice,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        string cleanToken = CleanToken(rawBearerToken);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseUrl + SendShipmentPriceEndpoint);
        ApplyHeaders(httpRequest, cleanToken);

        var payload = new
        {
            shipmentId = shipmentId,
            internationalCargoProvider = provider.ToLowerInvariant(),
            cargoPrice = cargoPrice,
            isPreInformationApproved = true,
            isAgreementAccepted = true
        };

        string json = JsonSerializer.Serialize(payload, JsonOpts);
        httpRequest.Content = new StringContent(json, Encoding.UTF8, "application/json");

        LogApiTrace("SendShipmentPrice-Request", json);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogApiTrace("SendShipmentPrice-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");

        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;
        CheckTokenExpired(root);

        int resultCode = root.TryGetProperty("resultCode", out var code) ? code.GetInt32() : (int)response.StatusCode;
        if (resultCode != 200)
        {
            string msg = root.TryGetProperty("resultMessage", out var msgElem) ? msgElem.GetString() ?? "" : "";
            throw new InvalidOperationException($"Aras Global Fiyat/Taşıyıcı Onay Hatası ({resultCode}): {msg}");
        }

        return true;
    }

    private async Task<string> StartCalculationAsync(
        ArasGlobalQuoteRequest request,
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
                    Height = request.HeightCm,
                    Width = request.WidthCm,
                    Length = request.LengthCm,
                    Weight = request.WeightKg,
                    PackageCount = 1
                }
            },
            Currency = request.Currency,
            DiscountCode = "",
            InternationalShipmentCategory = "4",
            IsMicroExport = true,
            PackageCount = 1,
            ReceiverCity = request.ReceiverCity,
            ReceiverCountryCode = request.ReceiverCountry,
            ReceiverPostalCode = request.ReceiverPostalCode,
            ReceiverTown = request.ReceiverTown,
            SenderCity = "ankara",
            SenderCountry = request.SenderCountry,
            SenderDistrict = "altındağ",
            SenderTown = "örnekler",
            ShipmentItems = new[]
            {
                new
                {
                    Height = request.HeightCm,
                    Width = request.WidthCm,
                    Length = request.LengthCm,
                    Weight = request.WeightKg,
                    Category = "1",
                    HsCode = "3926400000",
                    ItemDescription = "3d print figür",
                    PackageCount = 1,
                    Quantity = 1,
                    UnitPrice = request.ItemUnitPrice
                }
            },
            ShipmentId = "00000000-0000-0000-0000-000000000000",
            TotalPrice = request.ItemUnitPrice
        };

        string jsonBody = JsonSerializer.Serialize(payload);
        httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        ValidateStatus(response);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        using var doc = JsonDocument.Parse(responseContent);
        var root = doc.RootElement;

        CheckTokenExpired(root);

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

        CheckTokenExpired(root);

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
                            EstimatedStartDeliveryDate = m.TryGetProperty("estimatedDeliveryMinDay", out var minD)
                                ? minD.GetDouble()
                                : (m.TryGetProperty("estimatedStartDeliveryDate", out var sd) ? sd.GetDouble() : 0.0),
                            EstimatedEndDeliveryDate = m.TryGetProperty("estimatedDeliveryMaxDay", out var maxD)
                                ? maxD.GetDouble()
                                : (m.TryGetProperty("estimatedEndDeliveryDate", out var ed) ? ed.GetDouble() : 0.0),
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
        if (!string.IsNullOrWhiteSpace(token))
        {
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        }
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

    private static void CheckTokenExpired(JsonElement root)
    {
        if (root.TryGetProperty("resultCode", out var code) && code.GetInt32() == 401)
        {
            throw new ArasGlobalTokenExpiredException("Aras Global oturum tokeninizin süresi doldu (401 Unauthorized).");
        }
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

    public static void LogApiTrace(string step, string content)
    {
        try
        {
            var folder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "SimilarProductsWinForms");
            Directory.CreateDirectory(folder);
            string logPath = Path.Combine(folder, "aras_shipment_api_trace.log");
            string line = $"[{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss.fff}] [{step}]\n{content}\n----------------------------------------\n";
            File.AppendAllText(logPath, line);
        }
        catch { }
    }
}
