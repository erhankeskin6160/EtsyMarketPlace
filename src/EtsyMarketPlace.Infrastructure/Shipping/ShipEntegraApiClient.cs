using EtsyMarketPlace.Infrastructure.Http;
namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
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
/// ShipEntegra (api.shipentegra.com) resmi REST API istemcisi.
/// </summary>
public sealed class ShipEntegraApiClient : IShipEntegraApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseEndpoint = "https://api.shipentegra.com/v1/tools/calculate/all";
    private const string TariffByCarrierEndpoint = "https://api.shipentegra.com/v1/tools/tariff-calculator/by-carrier";
    private const string BaseUrlV1 = "https://api.shipentegra.com/v1";

    private static readonly ConcurrentDictionary<string, (DateTime ExpiresUtc, ShipEntegraTariffResponse Response)> TariffCache = new(StringComparer.Ordinal);
    private static readonly TimeSpan TariffCacheTtl = TimeSpan.FromMinutes(30);

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
        LogShipEntegraTrace("Quote-Request", jsonBody);

        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);

        string responseContent = await response.Content.ReadAsStringAsync(cancellationToken);
        LogShipEntegraTrace("Quote-Response", $"Status: {(int)response.StatusCode} | Body: {responseContent}");
        ValidateStatus(response, responseContent);
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
                string serviceName = TryReadString(p, "serviceName");
                string clearName = TryReadString(p, "clearServiceName", serviceName);
                string serviceType = TryReadString(p, "serviceType", "ECO");
                string currency = TryReadString(p, "currency", "USD");
                string addDesc = TryReadString(p, "additionalDescription");
                string tooltip = TryReadString(p, "tooltip");
                string addFeeDesc = TryReadString(p, "additionalFeeDescription");
                string pricing = TryReadString(p, "pricing");

                decimal cargoPrice = TryReadDecimal(p, "cargoPrice");
                decimal fuelCost = TryReadDecimal(p, "fuelCost");
                decimal fuelMultiplier = TryReadDecimal(p, "fuelMultiplier");
                decimal additionalFee = TryReadDecimal(p, "additionalFee");
                decimal discountedCargoPrice = TryReadDecimal(p, "discountedCargoPrice");
                decimal totalPrice = TryReadDecimal(p, "totalPrice");
                decimal totalWithAdd = TryReadDecimal(p, "totalPriceWithAdditionalFee");

                if (totalWithAdd > totalPrice)
                {
                    totalPrice = totalWithAdd;
                }
                else if (totalPrice <= 0m)
                {
                    totalPrice = cargoPrice + fuelCost + additionalFee;
                }

                var (returnNote, insuranceNote) = ShipEntegraTariffCalculator.ParseAdditionalDescription(addDesc);
                decimal discountedTotal = ShipEntegraTariffCalculator.ComputeDiscountedFreightUsd(
                    cargoPrice, fuelCost, additionalFee, totalPrice, discountedCargoPrice);

                bool isBest = !string.IsNullOrWhiteSpace(bestCarrier) &&
                              serviceName.Equals(bestCarrier, StringComparison.OrdinalIgnoreCase);

                offers.Add(new ShipEntegraQuoteOffer
                {
                    ServiceName = serviceName,
                    ClearServiceName = clearName,
                    ServiceType = serviceType,
                    CargoPrice = cargoPrice,
                    FuelCost = fuelCost,
                    FuelMultiplierPercent = fuelMultiplier,
                    AdditionalFeeUsd = additionalFee,
                    AdditionalFeeDescription = !string.IsNullOrWhiteSpace(addFeeDesc) ? addFeeDesc : (additionalFee > 0m ? "Yoğun Sezon Ek Ücreti" : string.Empty),
                    TotalPrice = totalPrice,
                    DiscountedTotalPriceUsd = discountedTotal,
                    PricingTemplate = pricing,
                    Currency = currency,
                    AdditionalDescription = addDesc,
                    ReturnPolicyNote = returnNote,
                    InsuranceNote = insuranceNote,
                    Tooltip = tooltip,
                    IsBestCarrier = isBest,
                    IsLivePrice = true
                });
            }
        }

        if (offers.Count > 0)
        {
            await EnrichOffersWithTariffAsync(offers, request, cleanToken, cancellationToken).ConfigureAwait(false);
        }

        return offers;
    }

    /// <summary>
    /// Belirli taşıyıcı, GTİP (HTS) kodu ve beyan değeri için ShipEntegra'dan canlı Hızlı Gümrük Ödemesi kalemlerini çeker.
    /// </summary>
    public async Task<ShipEntegraTariffResponse> CalculateTariffByCarrierAsync(
        ShipEntegraTariffRequest request,
        string rawBearerToken,
        CancellationToken cancellationToken = default)
    {
        request ??= new ShipEntegraTariffRequest();
        string cleanToken = CleanToken(rawBearerToken);
        string normalizedCarrier = ShipEntegraTariffCalculator.NormalizeTariffCarrierSlug(request.Carrier);
        string cleanHs = new string((request.HtsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (string.IsNullOrWhiteSpace(cleanHs)) cleanHs = "3926400000";
        string destination = string.IsNullOrWhiteSpace(request.Destination) ? "US" : request.Destination.Trim().ToUpperInvariant();
        string origin = string.IsNullOrWhiteSpace(request.Origin) ? "TR" : request.Origin.Trim().ToUpperInvariant();
        decimal unitPrice = request.UnitPrice > 0m ? request.UnitPrice : 1.00m;
        int quantity = request.Quantity > 0 ? request.Quantity : 1;
        double weight = request.Weight > 0d ? request.Weight : 0.4d;

        var normalizedReq = new ShipEntegraTariffRequest
        {
            Carrier = normalizedCarrier,
            Currency = string.IsNullOrWhiteSpace(request.Currency) ? "USD" : request.Currency.Trim().ToUpperInvariant(),
            HtsCode = cleanHs,
            Origin = origin,
            Destination = destination,
            UnitPrice = unitPrice,
            Quantity = quantity,
            Weight = weight
        };

        string cacheKey = $"{normalizedCarrier}|{cleanHs}|{origin}|{destination}|{unitPrice:0.##}|{quantity}";
        if (TariffCache.TryGetValue(cacheKey, out var cached) && DateTime.UtcNow < cached.ExpiresUtc)
        {
            return cached.Response;
        }

        if (string.IsNullOrWhiteSpace(cleanToken))
        {
            return ShipEntegraTariffCalculator.ComputeFallbackTariff(normalizedReq);
        }

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, TariffByCarrierEndpoint);
            ApplyHeaders(httpRequest, cleanToken);

            var payload = new
            {
                carrier = normalizedCarrier,
                currency = normalizedReq.Currency,
                htsCode = cleanHs,
                origin = origin,
                destination = destination,
                unitPrice = unitPrice,
                quantity = quantity,
                weight = weight
            };

            string jsonBody = JsonSerializer.Serialize(payload);
            httpRequest.Content = new StringContent(jsonBody, Encoding.UTF8, "application/json");
            LogShipEntegraTrace("Tariff-Request", jsonBody);

            using var response = await _httpClient.SendAsync(httpRequest, cancellationToken).ConfigureAwait(false);
            string responseContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            LogShipEntegraTrace("Tariff-Response", $"Carrier: {normalizedCarrier} | Status: {(int)response.StatusCode} | Body: {responseContent}");

            if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
            {
                ValidateStatus(response, responseContent);
            }

            if (!response.IsSuccessStatusCode)
            {
                return ShipEntegraTariffCalculator.ComputeFallbackTariff(normalizedReq);
            }

            var parsed = ParseTariffResponse(responseContent, normalizedReq);
            if (parsed.Success)
            {
                TariffCache[cacheKey] = (DateTime.UtcNow.Add(TariffCacheTtl), parsed);
            }
            return parsed;
        }
        catch (ShipEntegraTokenExpiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Swallowed(ex, "ShipEntegraApiClient.CalculateTariffByCarrierAsync");
            return ShipEntegraTariffCalculator.ComputeFallbackTariff(normalizedReq);
        }
    }

    private async Task EnrichOffersWithTariffAsync(
        List<ShipEntegraQuoteOffer> offers,
        ShipEntegraQuoteRequest request,
        string cleanToken,
        CancellationToken cancellationToken)
    {
        string hsDigits = new string((request.HsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        string hsCode = string.IsNullOrWhiteSpace(hsDigits) ? "3926400000" : hsDigits;
        decimal declaredUnit = request.DeclaredValueUsd > 0m ? request.DeclaredValueUsd : 1.00m;
        int qty = request.Quantity > 0 ? request.Quantity : 1;
        string destination = string.IsNullOrWhiteSpace(request.ReceiverCountry) ? "US" : request.ReceiverCountry.Trim().ToUpperInvariant();
        double weight = request.WeightKg > 0d ? request.WeightKg : 0.4d;

        var uniqueSlugs = offers
            .Select(o => ShipEntegraTariffCalculator.NormalizeTariffCarrierSlug(o.ServiceName))
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(6)
            .ToList();

        var tariffTasks = uniqueSlugs.ToDictionary(
            slug => slug,
            slug => CalculateTariffByCarrierAsync(new ShipEntegraTariffRequest
            {
                Carrier = slug,
                Currency = "USD",
                HtsCode = hsCode,
                Origin = "TR",
                Destination = destination,
                UnitPrice = declaredUnit,
                Quantity = qty,
                Weight = weight
            }, cleanToken, cancellationToken),
            StringComparer.OrdinalIgnoreCase);

        try
        {
            await Task.WhenAll(tariffTasks.Values).ConfigureAwait(false);
        }
        catch (ShipEntegraTokenExpiredException)
        {
            throw;
        }
        catch (Exception ex)
        {
            AppLog.Swallowed(ex, "ShipEntegraApiClient.EnrichOffersWithTariffAsync");
        }

        // İlk başarılı vergi oranını referans olarak al (bazı özel slug'larda gümrük oranı ortak olur)
        ShipEntegraTariffResponse? referenceDuty = tariffTasks.Values
            .Where(t => t.IsCompletedSuccessfully && t.Result.Success && (t.Result.GeneralDutyRatePercent > 0m || t.Result.AdditionalDutyRatePercent > 0m))
            .Select(t => t.Result)
            .FirstOrDefault();

        var fallbackDefaults = ShipEntegraTariffCalculator.GetDefaultDutyRates(hsCode, destination);
        decimal refGenRate = referenceDuty?.GeneralDutyRatePercent ?? fallbackDefaults.GeneralRate;
        decimal refAddRate = referenceDuty?.AdditionalDutyRatePercent ?? fallbackDefaults.AdditionalRate;
        string refAddHts = referenceDuty != null && !string.IsNullOrEmpty(referenceDuty.AdditionalDutyHtsCode)
            ? referenceDuty.AdditionalDutyHtsCode
            : fallbackDefaults.AdditionalHtsCode;

        foreach (var offer in offers)
        {
            string slug = ShipEntegraTariffCalculator.NormalizeTariffCarrierSlug(offer.ServiceName);
            ShipEntegraTariffResponse? matched = tariffTasks.TryGetValue(slug, out var task) && task.IsCompletedSuccessfully
                ? task.Result
                : referenceDuty;

            if (matched != null && matched.Success)
            {
                decimal genRate = matched.GeneralDutyRatePercent > 0m || matched.AdditionalDutyRatePercent > 0m
                    ? matched.GeneralDutyRatePercent : refGenRate;
                decimal addRate = matched.GeneralDutyRatePercent > 0m || matched.AdditionalDutyRatePercent > 0m
                    ? matched.AdditionalDutyRatePercent : refAddRate;
                string addHts = !string.IsNullOrEmpty(matched.AdditionalDutyHtsCode) ? matched.AdditionalDutyHtsCode : refAddHts;

                ShipEntegraTariffCalculator.PopulateCustomsBreakdown(
                    offer,
                    declaredUnit,
                    qty,
                    hsCode,
                    destination,
                    genRate,
                    addRate,
                    addHts,
                    overrideGeneralDutyUsd: matched.GeneralDutyUsd > 0m || genRate == 0m ? matched.GeneralDutyUsd : null,
                    overrideAdditionalDutyUsd: matched.AdditionalDutyUsd > 0m || addRate == 0m ? matched.AdditionalDutyUsd : null,
                    overrideCarrierFeeUsd: matched.CarrierProcessingFeeUsd);
            }
            else
            {
                ShipEntegraTariffCalculator.PopulateCustomsBreakdown(
                    offer, declaredUnit, qty, hsCode, destination, refGenRate, refAddRate, refAddHts);
            }
        }
    }

    private static ShipEntegraTariffResponse ParseTariffResponse(string json, ShipEntegraTariffRequest req)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (!root.TryGetProperty("status", out var st) || !string.Equals(st.GetString(), "success", StringComparison.OrdinalIgnoreCase))
            {
                return ShipEntegraTariffCalculator.ComputeFallbackTariff(req);
            }

            if (!root.TryGetProperty("data", out var dataElem) ||
                !dataElem.TryGetProperty("duties", out var dutiesElem) ||
                dutiesElem.ValueKind != JsonValueKind.Array)
            {
                return ShipEntegraTariffCalculator.ComputeFallbackTariff(req);
            }

            var result = new ShipEntegraTariffResponse { Success = true };
            foreach (var d in dutiesElem.EnumerateArray())
            {
                string type = TryReadString(d, "type");
                string hts = TryReadString(d, "htsCode");
                decimal price = TryReadDecimal(d, "price");
                decimal rate = TryReadDecimal(d, "adValRate");

                result.Duties.Add(new ShipEntegraDutyItem
                {
                    Type = type,
                    HtsCode = hts,
                    Price = price,
                    AdValRate = rate
                });

                if (type.Equals("general_duty", StringComparison.OrdinalIgnoreCase))
                {
                    result.GeneralDutyUsd += price;
                    if (rate > 0m) result.GeneralDutyRatePercent = rate;
                }
                else if (type.Equals("additional_duty", StringComparison.OrdinalIgnoreCase))
                {
                    result.AdditionalDutyUsd += price;
                    result.AdditionalDutyRatePercent += rate;
                    if (!string.IsNullOrWhiteSpace(hts))
                    {
                        result.AdditionalDutyHtsCode = string.IsNullOrEmpty(result.AdditionalDutyHtsCode)
                            ? hts
                            : $"{result.AdditionalDutyHtsCode}, {hts}";
                    }
                }
                else if (type.Equals("carrier_fee", StringComparison.OrdinalIgnoreCase))
                {
                    result.CarrierProcessingFeeUsd += price;
                }
            }

            result.TaxAndDutySubtotalUsd = Math.Round(result.GeneralDutyUsd + result.AdditionalDutyUsd, 2, MidpointRounding.AwayFromZero);
            result.FastCustomsPaymentUsd = Math.Round(result.TaxAndDutySubtotalUsd + result.CarrierProcessingFeeUsd, 2, MidpointRounding.AwayFromZero);
            result.DiagnosticMessage = "Canlı ShipEntegra Gümrük Tarife Motoru";
            return result;
        }
        catch (Exception ex)
        {
            AppLog.Swallowed(ex, "ShipEntegraApiClient.ParseTariffResponse");
            return ShipEntegraTariffCalculator.ComputeFallbackTariff(req);
        }
    }

    private static string TryReadString(JsonElement element, string name, string defaultValue = "")
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var prop))
            return defaultValue;

        return prop.ValueKind switch
        {
            JsonValueKind.String => prop.GetString() ?? defaultValue,
            JsonValueKind.Number => prop.GetRawText(),
            _ => defaultValue
        };
    }

    private static decimal TryReadDecimal(JsonElement element, string name)
    {
        if (element.ValueKind != JsonValueKind.Object || !element.TryGetProperty(name, out var prop))
            return 0m;

        if (prop.ValueKind == JsonValueKind.Number && prop.TryGetDecimal(out decimal dec))
            return dec;

        if (prop.ValueKind == JsonValueKind.String &&
            decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
            return parsed;

        return 0m;
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

    /// <summary>
    /// Etiket oluşturur ve ham yanıt baytlarını döner. Panel sözleşmesi (29.09.2026 canlı trafik):
    /// 200 yanıtı JSON'dur ve gerçek PDF, data.label alanındaki herkese açık URL'dedir;
    /// doğrudan PDF baytı da desteklenir.
    /// </summary>
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

    /// <summary>
    /// files.shipentegra.com üzerindeki etiket dosyasını indirir.
    /// Panel yanıtındaki data.label alanı herkese açık bir PDF URL'sidir; kimlik doğrulama gerekmez.
    /// </summary>
    public async Task<byte[]?> DownloadLabelFileAsync(string url, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return null;
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
        using var response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        byte[] bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        LogShipEntegraTrace("LabelFile-Download", $"{url} | Status: {(int)response.StatusCode} | {bytes.Length} bayt");
        return response.IsSuccessStatusCode && bytes.Length > 0 ? bytes : null;
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
        if (response.IsSuccessStatusCode)
        {
            return;
        }

        // İş kuralı redleri 403 gövdesiyle gelebilir (canlı: ERR.28050.1008, GB için DDP
        // geçersiz, 29.09.2026). Bu durumda oturum yenileme akışı açılmaz; gerçek sunucu
        // mesajı korunur.
        ShipEntegraBusinessException? businessError =
            ShipEntegraApiErrorParser.TryParse(responseBody, (int)response.StatusCode);

        if (businessError != null && ShipEntegraApiErrorParser.IsAuthOrExpiredError(businessError.Code, businessError.Description))
        {
            throw new ShipEntegraTokenExpiredException(
                $"ShipEntegra yetkilendirme hatası: {businessError.Description} ({businessError.Code}). Oturum süreniz dolmuş olabilir.");
        }

        if (response.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
        {
            if (businessError != null)
            {
                throw businessError;
            }

            throw new ShipEntegraTokenExpiredException(
                $"ShipEntegra yetkilendirme hatası ({(int)response.StatusCode} {response.ReasonPhrase}). Oturum süreniz dolmuş olabilir.");
        }

        if (businessError != null)
        {
            throw businessError;
        }

        string detail = string.IsNullOrWhiteSpace(responseBody)
            ? string.Empty
            : " | Yanıt: " + (responseBody!.Length > 400 ? responseBody.Substring(0, 400) : responseBody);
        throw new HttpRequestException(
            $"ShipEntegra API isteği başarısız oldu: {(int)response.StatusCode} {response.ReasonPhrase}{detail}");
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

    /// <summary>
    /// ShipEntegra Geliştirici API Anahtarları (Client ID ve Client Secret) ile resmi token ucundan token alır.
    /// </summary>
    public async Task<ShipEntegraAuthTokens?> AuthenticateWithClientCredentialsAsync(
        string clientId,
        string clientSecret,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(clientId) || string.IsNullOrWhiteSpace(clientSecret))
        {
            return null;
        }

        var endpoints = new[]
        {
            "https://publicapi.shipentegra.com/v1/auth/token",
            "https://api.shipentegra.com/v1/auth/token"
        };

        var payload = new
        {
            clientId = clientId.Trim(),
            clientSecret = clientSecret.Trim()
        };
        var jsonContent = JsonSerializer.Serialize(payload);

        foreach (var endpoint in endpoints)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, endpoint);
                req.Headers.TryAddWithoutValidation("Origin", "https://app.shipentegra.com");
                req.Headers.TryAddWithoutValidation("Referer", "https://app.shipentegra.com/");
                req.Content = new StringContent(jsonContent, Encoding.UTF8, "application/json");

                using var res = await _httpClient.SendAsync(req, cancellationToken);
                var body = await res.Content.ReadAsStringAsync(cancellationToken);
                if (!res.IsSuccessStatusCode)
                {
                    continue;
                }

                using var doc = JsonDocument.Parse(body);
                if (doc.RootElement.TryGetProperty("data", out var dataEl))
                {
                    string? access = null;
                    string? refresh = null;
                    if (dataEl.TryGetProperty("accessToken", out var accEl)) access = accEl.GetString();
                    if (dataEl.TryGetProperty("refreshToken", out var refEl)) refresh = refEl.GetString();

                    if (!string.IsNullOrWhiteSpace(access))
                    {
                        return new ShipEntegraAuthTokens
                        {
                            AccessToken = access,
                            RefreshToken = refresh ?? string.Empty
                        };
                    }
                }
            }
            catch (Exception ex)
            {
                AppLog.Swallowed(ex, "ShipEntegraApiClient.AuthenticateWithClientCredentialsAsync");
            }
        }

        return null;
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
