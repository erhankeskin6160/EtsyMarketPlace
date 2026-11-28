using EtsyMarketPlace.Infrastructure.Http;
namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Navlungo canlı fiyat isteğinin başarısız olduğunu, yedek fiyatla gizlemeden taşıyan hata.
/// </summary>
public sealed class NavlungoApiException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string ResponseBody { get; }

    public NavlungoApiException(string message, HttpStatusCode? statusCode = null, string responseBody = "", Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        ResponseBody = responseBody;
    }
}

/// <summary>
/// Navlungo (quick-price-calculator.navlungo.com) canlı fiyat hesaplama istemcisi.
/// Widect, FedEx, UPS ve anlaşmalı navlun tekliflerini çeker.
/// </summary>
public sealed class NavlungoApiClient : INavlungoApiClient
{
    private readonly HttpClient _httpClient;
    private const string BaseEndpoint = "https://quick-price-calculator.navlungo.com/tr?source=user";
    private const string PortalApiBase = "https://api.navlungo.com";
    private const string PortalOrigin = "https://ship.navlungo.com";

    public const string AnonymousActionId = "406fad5769e32876f9c8eeccd5dab4d086b9068ca5";
    public const string AuthenticatedActionId = "40198494a378e36987abc2fe2dd302fceb194b28c4";

    private static string? _cachedAnonymousActionId = AnonymousActionId;
    private static string? _cachedAuthenticatedActionId = AuthenticatedActionId;
    private static DateTime _lastResolvedUtc = DateTime.UtcNow;
    private static readonly SemaphoreSlim _resolveLock = new(1, 1);

    public NavlungoApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? SharedHttpClient.Instance;
    }

    /// <summary>
    /// Navlungo web uygulamasından (Next.js App Router) güncel Server Action ID'lerini dinamik olarak çözer.
    /// Her yeni deploy sonrasında hash'ler değişse bile otomatik güncellenir.
    /// </summary>
    public static async Task<(string anonId, string authId)> ResolveActionIdsAsync(
        HttpClient httpClient,
        bool forceRefresh = false,
        CancellationToken ct = default)
    {
        if (!forceRefresh && _cachedAnonymousActionId != null && _cachedAuthenticatedActionId != null && (DateTime.UtcNow - _lastResolvedUtc).TotalHours < 6)
        {
            return (_cachedAnonymousActionId, _cachedAuthenticatedActionId);
        }

        await _resolveLock.WaitAsync(ct);
        try
        {
            if (!forceRefresh && _cachedAnonymousActionId != null && _cachedAuthenticatedActionId != null && (DateTime.UtcNow - _lastResolvedUtc).TotalHours < 6)
            {
                return (_cachedAnonymousActionId, _cachedAuthenticatedActionId);
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, BaseEndpoint);
            req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
            
            using var res = await httpClient.SendAsync(req, ct);
            if (res.IsSuccessStatusCode)
            {
                string html = await res.Content.ReadAsStringAsync(ct);
                // HTML içindeki page chunk dosyasını bul: /_next/static/chunks/app/%5Blocale%5D/page-[a-f0-9]+.js
                var chunkMatch = Regex.Match(html, @"src=""(/_next/static/chunks/app/(?:%5Blocale%5D|\[locale\])/page-[a-f0-9]+\.js)""");
                if (chunkMatch.Success)
                {
                    string chunkUrl = "https://quick-price-calculator.navlungo.com" + chunkMatch.Groups[1].Value;
                    using var chunkReq = new HttpRequestMessage(HttpMethod.Get, chunkUrl);
                    chunkReq.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
                    
                    using var chunkRes = await httpClient.SendAsync(chunkReq, ct);
                    if (chunkRes.IsSuccessStatusCode)
                    {
                        string js = await chunkRes.Content.ReadAsStringAsync(ct);
                        var anonMatch = Regex.Match(js, @"createServerReference\)?\(""([a-f0-9]{40,})""[^""]+""callCalculationAnonymousApi""\)");
                        var authMatch = Regex.Match(js, @"createServerReference\)?\(""([a-f0-9]{40,})""[^""]+""callCalculationApi""\)");
                        
                        if (anonMatch.Success) _cachedAnonymousActionId = anonMatch.Groups[1].Value;
                        if (authMatch.Success) _cachedAuthenticatedActionId = authMatch.Groups[1].Value;
                        
                        if (!string.IsNullOrWhiteSpace(_cachedAnonymousActionId) && !string.IsNullOrWhiteSpace(_cachedAuthenticatedActionId))
                        {
                            _lastResolvedUtc = DateTime.UtcNow;
                            return (_cachedAnonymousActionId, _cachedAuthenticatedActionId);
                        }
                    }
                }
            }
        }
        catch
        {
            // Ağ hatası veya Cloudflare kısıtlamasında bilinen sabitlere dön
        }
        finally
        {
            _resolveLock.Release();
        }

        _cachedAnonymousActionId ??= AnonymousActionId;
        _cachedAuthenticatedActionId ??= AuthenticatedActionId;
        return (_cachedAnonymousActionId, _cachedAuthenticatedActionId);
    }

    public async Task<List<NavlungoQuoteOffer>> FetchLiveQuotesAsync(
        NavlungoQuoteRequest request,
        NavlungoSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        bool hasSession = settings != null && (!string.IsNullOrWhiteSpace(settings.IdToken) || !string.IsNullOrWhiteSpace(settings.SessionCookie));

        // 0. Üye oturumu varsa öncelikle resmi ship.navlungo.com / api.navlungo.com portal teklif uç noktasını dene
        if (hasSession && settings != null)
        {
            var portalOffers = await TryFetchPortalShipmentQuotesAsync(request, settings, cancellationToken).ConfigureAwait(false);
            if (portalOffers.Count > 0)
            {
                return portalOffers;
            }
        }

        // 1. Güncel veya önbellekteki Action ID'yi al
        var (anonId, authId) = await ResolveActionIdsAsync(_httpClient, forceRefresh: false, cancellationToken);

        List<NavlungoQuoteOffer> offers = new();
        string lastContent = string.Empty;
        HttpStatusCode? lastStatusCode = null;
        string? lastReasonPhrase = null;

        // 2. Eğer kullanıcı oturumu bağlıysa öncelikle AuthenticatedActionId ile sorgula
        if (hasSession)
        {
            var (authSuccess, authContent, authStatus, authReason) = await ExecutePostRequestRawAsync(request, settings, authId, cancellationToken);
            lastContent = authContent;
            lastStatusCode = authStatus;
            lastReasonPhrase = authReason;

            // HTML döndüyse (build rotasyonu) Action ID'yi zorla yenileyip bir kez daha dene
            if (authSuccess && (authContent.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                                authContent.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
            {
                var (_, freshAuth) = await ResolveActionIdsAsync(_httpClient, forceRefresh: true, cancellationToken);
                if (freshAuth != authId)
                {
                    (authSuccess, authContent, authStatus, authReason) = await ExecutePostRequestRawAsync(request, settings, freshAuth, cancellationToken);
                    lastContent = authContent;
                    lastStatusCode = authStatus;
                    lastReasonPhrase = authReason;
                }
            }

            // Auth başarılı olduysa ve server digest hatası içermiyorsa teklifleri ayrıştır
            if (authSuccess && !authContent.Contains("\"digest\":"))
            {
                offers = ParseNavlungoResponse(authContent, request);
                if (offers.Count > 0)
                {
                    foreach (var o in offers)
                    {
                        o.Note = "Navlungo Üye İndirimli (Canlı)";
                    }
                    SaveLastDebugResponse(authContent);
                    return offers;
                }
            }
            // Oturumlu istek HTTP 500 aldıysa veya çerez geçersizse, doğrudan istisnaya düşmek yerine
            // hemen aşağıdaki Anonim Canlı sorguya (callCalculationAnonymousApi) kademeli geçiş yapılır!
        }

        // 3. Anonim Canlı İstek (Oturumsuz sorgular veya oturumlu istek hata verdiğinde)
        var (anonSuccess, anonContent, anonStatus, anonReason) = await ExecutePostRequestRawAsync(request, null, anonId, cancellationToken);
        lastContent = anonContent;
        lastStatusCode = anonStatus;
        lastReasonPhrase = anonReason;

        if (anonSuccess && (anonContent.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                            anonContent.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
        {
            var (freshAnon, _) = await ResolveActionIdsAsync(_httpClient, forceRefresh: true, cancellationToken);
            if (freshAnon != anonId)
            {
                (anonSuccess, anonContent, anonStatus, anonReason) = await ExecutePostRequestRawAsync(request, null, freshAnon, cancellationToken);
                lastContent = anonContent;
                lastStatusCode = anonStatus;
                lastReasonPhrase = anonReason;
            }
        }

        SaveLastDebugResponse(lastContent);

        if (!anonSuccess)
        {
            throw new NavlungoApiException(
                $"Navlungo HTTP {(int)lastStatusCode} ({lastReasonPhrase}).",
                lastStatusCode,
                TruncateResponse(lastContent));
        }

        offers = ParseNavlungoResponse(anonContent, request);
        if (offers.Count == 0)
        {
            throw new NavlungoApiException(
                "Navlungo yanıtı alındı ancak teklif response içinden ayrıştırılamadı.",
                lastStatusCode,
                TruncateResponse(lastContent));
        }

        foreach (var o in offers)
        {
            o.Note = hasSession ? "Navlungo Canlı Teklif (üye oturumu kullanılamadı)" : "Navlungo Canlı Teklif";
        }

        return offers;
    }

    private static void SaveLastDebugResponse(string content)
    {
        try
        {
            string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "navlungo_last_response.log");
            File.WriteAllText(debugPath, content);
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }
    }

    private static string BuildCookieHeader(NavlungoSettings? settings)
    {
        if (settings == null) return string.Empty;

        var cookieBuilder = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(settings.SessionCookie))
        {
            string sanitized = NavlungoCookieSanitizer.Sanitize(settings.SessionCookie);
            cookieBuilder.Append(sanitized);
        }

        if (!string.IsNullOrWhiteSpace(settings.IdToken))
        {
            string idTok = NavlungoCookieSanitizer.Sanitize(settings.IdToken);
            if (!cookieBuilder.ToString().Contains("id_token=", StringComparison.OrdinalIgnoreCase))
            {
                if (cookieBuilder.Length > 0 && !cookieBuilder.ToString().TrimEnd().EndsWith(";"))
                    cookieBuilder.Append("; ");
                if (idTok.Contains('='))
                    cookieBuilder.Append(idTok);
                else
                    cookieBuilder.Append($"id_token={idTok}");
            }
        }

        return cookieBuilder.ToString();
    }

    private static void ApplyPortalAuthHeaders(HttpRequestMessage req, NavlungoSettings settings)
    {
        req.Headers.TryAddWithoutValidation("Accept", "application/json, text/plain, */*");
        req.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
        req.Headers.TryAddWithoutValidation("Origin", PortalOrigin);
        req.Headers.TryAddWithoutValidation("Referer", PortalOrigin + "/");

        string cookieHeader = BuildCookieHeader(settings);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            req.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        string cleanJwt = settings.CleanToken;
        if (string.IsNullOrWhiteSpace(cleanJwt) && !string.IsNullOrWhiteSpace(settings.SessionCookie))
        {
            var m = Regex.Match(settings.SessionCookie, @"(?:^|;\s*)id_token=([^;\s]+)", RegexOptions.IgnoreCase);
            if (m.Success)
            {
                cleanJwt = NavlungoSettings.SanitizeRawToken(m.Groups[1].Value);
            }
        }

        if (NavlungoSettings.LooksLikeJwt(cleanJwt))
        {
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {cleanJwt}");
        }
    }

    private static string TryExtractShipmentId(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;

        string trimmed = body.Trim();
        try
        {
            using var doc = JsonDocument.Parse(trimmed);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.String)
            {
                return (root.GetString() ?? string.Empty).Trim();
            }

            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("shipmentId", out var sProp) && sProp.ValueKind == JsonValueKind.String)
                    return (sProp.GetString() ?? string.Empty).Trim();
                if (root.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String)
                    return (idProp.GetString() ?? string.Empty).Trim();
            }
        }
        catch
        {
            // Ham UUID veya metin yanıtı olabilir
        }

        string unquoted = trimmed.Trim('"');
        return Guid.TryParse(unquoted, out _) ? unquoted : string.Empty;
    }

    /// <summary>
    /// Aktif ship.navlungo.com üye oturumu (Cookie / Bearer JWT) mevcutsa doğrudan resmi
    /// api.navlungo.com/api/shipments/{id}/quotes uç noktasından canlı üye fiyatlarını ve DDP kırılımını çeker.
    /// </summary>
    public async Task<List<NavlungoQuoteOffer>> TryFetchPortalShipmentQuotesAsync(
        NavlungoQuoteRequest request,
        NavlungoSettings settings,
        CancellationToken cancellationToken = default)
    {
        try
        {
            string shipmentId = (request.ShipmentId ?? string.Empty).Trim();
            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                var createPayload = new
                {
                    fromCountryCode = string.IsNullOrWhiteSpace(request.FromCountry) ? "TR" : request.FromCountry.ToUpperInvariant(),
                    destinationCountryCode = string.IsNullOrWhiteSpace(request.ToCountry) ? "US" : request.ToCountry.ToUpperInvariant(),
                    destinationPostalCode = string.IsNullOrWhiteSpace(request.PostalCode) ? "10001" : request.PostalCode.Trim(),
                    destinationCity = string.IsNullOrWhiteSpace(request.City) ? "Austin" : request.City.Trim(),
                    destinationState = string.IsNullOrWhiteSpace(request.State) ? "TX" : request.State.Trim(),
                    packages = new[]
                    {
                        new
                        {
                            packageType = "parcel",
                            weight = Math.Round(request.WeightKg, 2),
                            length = Math.Round(request.LengthCm, 1),
                            width = Math.Round(request.WidthCm, 1),
                            height = Math.Round(request.HeightCm, 1),
                            quantity = 1
                        }
                    },
                    proformaCurrency = "USD",
                    totalProformaPrice = request.DeclaredValueUsd > 0m ? Math.Round(request.DeclaredValueUsd, 2) : 1.00m
                };

                using var postReq = new HttpRequestMessage(HttpMethod.Post, $"{PortalApiBase}/api/shipments");
                ApplyPortalAuthHeaders(postReq, settings);
                postReq.Content = new StringContent(JsonSerializer.Serialize(createPayload), Encoding.UTF8, "application/json");

                using var postRes = await _httpClient.SendAsync(postReq, cancellationToken).ConfigureAwait(false);
                if (!postRes.IsSuccessStatusCode)
                {
                    return new List<NavlungoQuoteOffer>();
                }

                string postBody = await postRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                shipmentId = TryExtractShipmentId(postBody);
                if (string.IsNullOrWhiteSpace(shipmentId))
                {
                    return new List<NavlungoQuoteOffer>();
                }
            }

            using var getReq = new HttpRequestMessage(HttpMethod.Get, $"{PortalApiBase}/api/shipments/{Uri.EscapeDataString(shipmentId)}/quotes");
            ApplyPortalAuthHeaders(getReq, settings);

            using var getRes = await _httpClient.SendAsync(getReq, cancellationToken).ConfigureAwait(false);
            if (!getRes.IsSuccessStatusCode)
            {
                return new List<NavlungoQuoteOffer>();
            }

            string quotesJson = await getRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            var offers = ParseNavlungoResponse(quotesJson, request);
            if (offers.Count > 0)
            {
                foreach (var o in offers)
                {
                    o.Note = "Navlungo Üye Portalı (Canlı)";
                }
                SaveLastDebugResponse(quotesJson);
            }

            return offers;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception caught)
        {
            AppLog.Swallowed(caught);
            return new List<NavlungoQuoteOffer>();
        }
    }

    /// <summary>
    /// Aktif ship.navlungo.com üye oturumu (Cookie / Bearer JWT) ile Navlungo Portal API üzerinden
    /// gönderi başlatır, canlı teklifleri eşleştirir, seçilen teklifi onaylar ve takip/etiket bilgilerini döner.
    /// </summary>
    public async Task<NavlungoCreateShipmentResponse> CreateShipmentAsync(
        NavlungoCreateShipmentRequest request,
        NavlungoSettings settings,
        CancellationToken cancellationToken = default)
    {
        if (request == null)
        {
            return new NavlungoCreateShipmentResponse { IsSuccess = false, ErrorMessage = "Navlungo gönderi isteği boş olamaz." };
        }

        if (settings == null || !settings.IsSessionValid)
        {
            return new NavlungoCreateShipmentResponse
            {
                IsSuccess = false,
                ErrorMessage = "Navlungo oturumu geçersiz veya süresi dolmuş. Lütfen tekrar oturum açın."
            };
        }

        try
        {
            string countryCode = string.IsNullOrWhiteSpace(request.ReceiverCountryCode) ? "US" : request.ReceiverCountryCode.Trim().ToUpperInvariant();
            string fromCountry = string.IsNullOrWhiteSpace(request.FromCountryCode) ? "TR" : request.FromCountryCode.Trim().ToUpperInvariant();
            decimal unitPrice = Math.Round(Math.Max(1.00m, request.UnitPriceUsd), 2, MidpointRounding.AwayFromZero);
            int quantity = Math.Max(1, request.Quantity);

            // 1a. Kullanıcının Navlungo hesabındaki kayıtlı gönderici ve fatura adreslerini çek
            var senderShippingTask = FetchStoredSenderAddressAsync("/api/Shippers/stored-sender-shipping-address", settings, cancellationToken);
            var senderInvoiceTask = FetchStoredSenderAddressAsync("/api/Shippers/stored-sender-invoice-address", settings, cancellationToken);
            var gtipTask = ResolveNavlungoGtipCodeAsync(request.HsCode, countryCode, cancellationToken);

            await Task.WhenAll(senderShippingTask, senderInvoiceTask, gtipTask).ConfigureAwait(false);

            var (senderShipping, shipAddrStatus, shipAddrErr) = await senderShippingTask.ConfigureAwait(false);
            var (senderInvoice, invAddrStatus, invAddrErr) = await senderInvoiceTask.ConfigureAwait(false);
            string resolvedGtip = await gtipTask.ConfigureAwait(false);

            if (shipAddrStatus == HttpStatusCode.Unauthorized || shipAddrStatus == HttpStatusCode.Forbidden ||
                invAddrStatus == HttpStatusCode.Unauthorized || invAddrStatus == HttpStatusCode.Forbidden)
            {
                int code = (int)(shipAddrStatus ?? invAddrStatus ?? HttpStatusCode.Unauthorized);
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Navlungo oturum süresi dolmuş veya yetkisiz (HTTP {code}). Lütfen üst menüden 'Navlungo → Oturum Aç' penceresinden güncel Bearer JWT token'ınızı yenileyip tekrar deneyin."
                };
            }

            if (senderShipping == null || senderInvoice == null)
            {
                string extraErr = !string.IsNullOrWhiteSpace(shipAddrErr) ? $" ({shipAddrErr})"
                    : (!string.IsNullOrWhiteSpace(invAddrErr) ? $" ({invAddrErr})" : string.Empty);
                string missingWhich = senderShipping == null && senderInvoice == null
                    ? "gönderici ve fatura"
                    : (senderShipping == null ? "gönderici çıkış" : "fatura");
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Navlungo hesabınızda kayıtlı {missingWhich} adresi bulunamadı{extraErr}. Lütfen ship.navlungo.com → Adreslerim bölümünden bir kez gönderici ve fatura adresi ekleyin."
                };
            }

            var (phoneCode, phoneNumber) = SplitPhone(request.ReceiverPhone, countryCode);
            bool isEu = IsEuropeanUnionCountry(countryCode);

            var initPayload = new
            {
                shipmentType = "sales",
                senderShippingAddress = senderShipping,
                senderInvoiceAddress = senderInvoice,
                receiverAddress = new
                {
                    addressType = "individual",
                    contactName = request.ReceiverName.Trim(),
                    email = request.ReceiverEmail.Trim(),
                    phoneCode,
                    phoneNumber,
                    country = new
                    {
                        code = countryCode,
                        alphaThree = ResolveIsoAlpha3(countryCode),
                        isInEuropeanUnion = isEu
                    },
                    stateCode = NullIfEmpty(request.ReceiverStateCode),
                    city = request.ReceiverCity.Trim(),
                    postalCode = request.ReceiverPostalCode.Trim(),
                    addressLine = new
                    {
                        firstLine = request.ReceiverStreet1.Trim(),
                        secondLine = NullIfEmpty(request.ReceiverStreet2)
                    }
                },
                packages = new[]
                {
                    new
                    {
                        loadType = "box",
                        packageType = "box",
                        weight = Math.Round(request.WeightKg, 2),
                        length = Math.Round(request.LengthCm, 1),
                        width = Math.Round(request.WidthCm, 1),
                        height = Math.Round(request.HeightCm, 1),
                        quantity = 1
                    }
                },
                products = new[]
                {
                    new
                    {
                        description = request.ProductDescription,
                        originCountry = fromCountry,
                        gtipCode = resolvedGtip,
                        unitPrice,
                        quantity,
                        currency = "USD",
                        merchantProductIdentifier = isEu ? $"NVM-ETSY-{resolvedGtip.Replace(".", "")}" : null,
                        nonStandardisedManufacturerProductIdentifier = isEu ? $"NVN-ETSY-{resolvedGtip.Replace(".", "")}" : null
                    }
                },
                proformaCurrencyCode = "USD",
                iossNumber = NullIfEmpty(request.IossNumber)
            };

            var initSerializerOpts = new JsonSerializerOptions
            {
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            string initJson = JsonSerializer.Serialize(initPayload, initSerializerOpts);

            // 1b. Gönderiyi başlat (POST /api/shipments/init)
            using var initReq = new HttpRequestMessage(HttpMethod.Post, $"{PortalApiBase}/api/shipments/init");
            ApplyPortalAuthHeaders(initReq, settings);
            initReq.Content = new StringContent(initJson, Encoding.UTF8, "application/json");

            using var initRes = await _httpClient.SendAsync(initReq, cancellationToken).ConfigureAwait(false);
            HttpStatusCode initStatus = initRes.StatusCode;
            string initBody = await initRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            string shipmentId = initRes.IsSuccessStatusCode ? TryExtractShipmentId(initBody) : string.Empty;

            if (string.IsNullOrWhiteSpace(shipmentId))
            {
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ErrorMessage = $"Navlungo gönderi başlatılamadı (HTTP {(int)initStatus}). {ExtractPortalErrorSummary(initBody)}"
                };
            }

            string portalQuoteUrl = $"{PortalOrigin}/ship/quotes/{Uri.EscapeDataString(shipmentId)}";

            // 2. Canlı teklifleri çek ve seçilen taşıyıcı/servis ile eşleştir
            using var quotesReq = new HttpRequestMessage(HttpMethod.Get, $"{PortalApiBase}/api/shipments/{Uri.EscapeDataString(shipmentId)}/quotes");
            ApplyPortalAuthHeaders(quotesReq, settings);

            using var quotesRes = await _httpClient.SendAsync(quotesReq, cancellationToken).ConfigureAwait(false);
            string quotesBody = await quotesRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!quotesRes.IsSuccessStatusCode)
            {
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ShipmentId = shipmentId,
                    PortalQuoteUrl = portalQuoteUrl,
                    ErrorMessage = $"Navlungo teklifleri alınamadı (HTTP {(int)quotesRes.StatusCode}). Taslak portalda hazır: {portalQuoteUrl}"
                };
            }

            var dummyQuoteReq = new NavlungoQuoteRequest
            {
                FromCountry = fromCountry,
                ToCountry = countryCode,
                WeightKg = request.WeightKg,
                LengthCm = request.LengthCm,
                WidthCm = request.WidthCm,
                HeightCm = request.HeightCm
            };
            var offers = ParseNavlungoResponse(quotesBody, dummyQuoteReq);
            var matched = SelectMatchingOffer(offers, request.SelectedSubCarrier, request.SelectedServiceCode, request.ServiceType);
            if (matched == null || string.IsNullOrWhiteSpace(matched.QuoteId))
            {
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ShipmentId = shipmentId,
                    PortalQuoteUrl = portalQuoteUrl,
                    ErrorMessage = $"Navlungo canlı teklif eşleştirilemedi ('{request.ServiceType}'). Taslak gönderiyi portaldan tamamlayabilirsiniz: {portalQuoteUrl}"
                };
            }

            // 3. Seçilen teklifi onayla (POST /api/shipments/{shipmentId}/select-quote)
            var selectedVas = new List<string>();
            if ((request.IsDdp || matched.IsDdpRequired) &&
                (matched.AvailableValueAddedServices.Count == 0 ||
                 matched.AvailableValueAddedServices.Any(v => v.Equals("ddp", StringComparison.OrdinalIgnoreCase))))
            {
                selectedVas.Add("ddp");
            }

            var selectQuotePayload = new
            {
                searchReference = NullIfEmpty(matched.SearchReference),
                searchId = matched.SearchId,
                quoteId = matched.QuoteId,
                selectedValueAddedServices = selectedVas.ToArray()
            };
            string selectQuoteJson = JsonSerializer.Serialize(selectQuotePayload, initSerializerOpts);

            using var approveReq = new HttpRequestMessage(
                HttpMethod.Post,
                $"{PortalApiBase}/api/shipments/{Uri.EscapeDataString(shipmentId)}/select-quote");
            ApplyPortalAuthHeaders(approveReq, settings);
            approveReq.Content = new StringContent(selectQuoteJson, Encoding.UTF8, "application/json");

            using var approveRes = await _httpClient.SendAsync(approveReq, cancellationToken).ConfigureAwait(false);
            string approveBody = await approveRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!approveRes.IsSuccessStatusCode)
            {
                return new NavlungoCreateShipmentResponse
                {
                    IsSuccess = false,
                    ShipmentId = shipmentId,
                    PortalQuoteUrl = portalQuoteUrl,
                    ErrorMessage = $"Navlungo taslak oluşturuldu ({shipmentId}) ancak teklif onayı başarısız oldu (HTTP {(int)approveRes.StatusCode}). {ExtractPortalErrorSummary(approveBody)} — Portal linki: {portalQuoteUrl}"
                };
            }

            // 4. Takip numarası ve etiket URL'ini çek
            string trackingNumber = ExtractTrackingFromJson(approveBody);
            string labelUrl = string.Empty;

            if (string.IsNullOrWhiteSpace(trackingNumber))
            {
                using var detailReq = new HttpRequestMessage(HttpMethod.Get, $"{PortalApiBase}/api/shipments/{Uri.EscapeDataString(shipmentId)}");
                ApplyPortalAuthHeaders(detailReq, settings);
                using var detailRes = await _httpClient.SendAsync(detailReq, cancellationToken).ConfigureAwait(false);
                if (detailRes.IsSuccessStatusCode)
                {
                    string detailBody = await detailRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    trackingNumber = ExtractTrackingFromJson(detailBody);
                }
            }

            using (var consReq = new HttpRequestMessage(HttpMethod.Get, $"{PortalApiBase}/api/shipments/{Uri.EscapeDataString(shipmentId)}/consignment"))
            {
                ApplyPortalAuthHeaders(consReq, settings);
                using var consRes = await _httpClient.SendAsync(consReq, cancellationToken).ConfigureAwait(false);
                if (consRes.IsSuccessStatusCode)
                {
                    string consBody = await consRes.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                    labelUrl = ExtractLabelUrlFromConsignment(consBody);
                }
            }

            return new NavlungoCreateShipmentResponse
            {
                IsSuccess = true,
                ShipmentId = shipmentId,
                TrackingNumber = trackingNumber,
                LabelUrl = string.IsNullOrWhiteSpace(labelUrl) ? $"{PortalOrigin}/ship/myShipments/{Uri.EscapeDataString(shipmentId)}" : labelUrl,
                PortalQuoteUrl = portalQuoteUrl
            };
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            return new NavlungoCreateShipmentResponse
            {
                IsSuccess = false,
                ErrorMessage = $"Navlungo gönderi oluşturma hatası: {ex.Message}"
            };
        }
    }

    private async Task<(System.Text.Json.Nodes.JsonObject? Address, HttpStatusCode? StatusCode, string? ErrorDetail)> FetchStoredSenderAddressAsync(
        string relativePath,
        NavlungoSettings settings,
        CancellationToken ct)
    {
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, $"{PortalApiBase}{relativePath}");
            ApplyPortalAuthHeaders(req, settings);
            using var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
            string body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            if (!res.IsSuccessStatusCode)
            {
                return (null, res.StatusCode, $"HTTP {(int)res.StatusCode}");
            }

            var arr = System.Text.Json.Nodes.JsonNode.Parse(body) as System.Text.Json.Nodes.JsonArray;
            if (arr == null || arr.Count == 0) return (null, res.StatusCode, null);

            System.Text.Json.Nodes.JsonObject? chosen = null;
            foreach (var item in arr)
            {
                if (item is System.Text.Json.Nodes.JsonObject obj)
                {
                    chosen ??= obj;
                    if (obj["isFavorited"]?.GetValue<bool>() == true)
                    {
                        chosen = obj;
                        break;
                    }
                }
            }

            if (chosen == null) return (null, res.StatusCode, null);

            var cleaned = StripEmptyStringFields(chosen) as System.Text.Json.Nodes.JsonObject;
            if (cleaned != null && cleaned["identificationNumber"] == null && cleaned["taxNumber"] != null)
            {
                cleaned["identificationNumber"] = cleaned["taxNumber"]!.DeepClone();
            }
            return (cleaned, res.StatusCode, null);
        }
        catch (Exception ex)
        {
            return (null, null, ex.Message);
        }
    }

    private static System.Text.Json.Nodes.JsonNode? StripEmptyStringFields(System.Text.Json.Nodes.JsonNode? node)
    {
        if (node == null) return null;
        if (node is System.Text.Json.Nodes.JsonValue val)
        {
            if (val.TryGetValue<string>(out var s) && string.IsNullOrEmpty(s))
                return null;
            return val.DeepClone();
        }
        if (node is System.Text.Json.Nodes.JsonArray arr)
        {
            var newArr = new System.Text.Json.Nodes.JsonArray();
            foreach (var el in arr)
            {
                var c = StripEmptyStringFields(el);
                if (c != null) newArr.Add(c);
            }
            return newArr;
        }
        if (node is System.Text.Json.Nodes.JsonObject obj)
        {
            var newObj = new System.Text.Json.Nodes.JsonObject();
            foreach (var kv in obj)
            {
                var c = StripEmptyStringFields(kv.Value);
                if (c != null) newObj[kv.Key] = c;
            }
            return newObj.Count > 0 ? newObj : null;
        }
        return node.DeepClone();
    }

    private async Task<string> ResolveNavlungoGtipCodeAsync(string? rawHsCode, string countryCode, CancellationToken ct)
    {
        string digits = new string((rawHsCode ?? string.Empty).Where(char.IsDigit).ToArray());
        if (digits.Length < 6) digits = "3926400000";

        if (!string.Equals(countryCode, "US", StringComparison.OrdinalIgnoreCase))
        {
            return digits[..6];
        }

        string searchPrefix = $"{digits[..4]}.{digits.Substring(4, 2)}";
        try
        {
            using var req = new HttpRequestMessage(
                HttpMethod.Get,
                $"https://quote-search-api.navlungo.com/htsuscodes/v1?search={Uri.EscapeDataString(searchPrefix)}&limit=15");
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var res = await _httpClient.SendAsync(req, ct).ConfigureAwait(false);
            if (res.IsSuccessStatusCode)
            {
                string json = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("items", out var items) && items.ValueKind == JsonValueKind.Array)
                {
                    string? firstHts = null;
                    foreach (var it in items.EnumerateArray())
                    {
                        if (it.TryGetProperty("htsCode", out var hc) && hc.ValueKind == JsonValueKind.String)
                        {
                            string code = hc.GetString()!.Trim();
                            string codeDigits = new string(code.Where(char.IsDigit).ToArray());
                            firstHts ??= code;
                            if (codeDigits == digits || (digits.Length >= 8 && codeDigits.StartsWith(digits[..8], StringComparison.Ordinal)))
                            {
                                return code;
                            }
                        }
                    }
                    if (!string.IsNullOrWhiteSpace(firstHts)) return firstHts!;
                }
            }
        }
        catch { }

        string padded = digits.Length >= 10 ? digits[..10] : digits.PadRight(10, '0');
        return $"{padded[..4]}.{padded.Substring(4, 2)}.{padded.Substring(6, 2)}.{padded.Substring(8, 2)}";
    }

    private static string? NullIfEmpty(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

    private static string ResolveIsoAlpha3(string iso2) => iso2.ToUpperInvariant() switch
    {
        "US" => "USA",
        "CA" => "CAN",
        "GB" or "UK" => "GBR",
        "DE" => "DEU",
        "FR" => "FRA",
        "IT" => "ITA",
        "ES" => "ESP",
        "NL" => "NLD",
        "BE" => "BEL",
        "AT" => "AUT",
        "CH" => "CHE",
        "AU" => "AUS",
        "SE" => "SWE",
        "NO" => "NOR",
        "DK" => "DNK",
        "FI" => "FIN",
        "PL" => "POL",
        "IE" => "IRL",
        "PT" => "PRT",
        "TR" => "TUR",
        _ => iso2.Length == 2 ? iso2.ToUpperInvariant() + "A" : "USA"
    };

    private static bool IsEuropeanUnionCountry(string iso2) => iso2.ToUpperInvariant() switch
    {
        "AT" or "BE" or "BG" or "HR" or "CY" or "CZ" or "DK" or "EE" or "FI" or "FR" or "DE" or
        "GR" or "HU" or "IE" or "IT" or "LV" or "LT" or "LU" or "MT" or "NL" or "PL" or "PT" or
        "RO" or "SK" or "SI" or "ES" or "SE" => true,
        _ => false
    };

    private static NavlungoQuoteOffer? SelectMatchingOffer(
        List<NavlungoQuoteOffer> offers,
        string? subCarrier,
        string? serviceCode,
        string? serviceType)
    {
        if (offers.Count == 0) return null;

        string needle = $"{subCarrier} {serviceCode} {serviceType}".ToLowerInvariant();
        if (needle.Contains("widect") || needle.Contains("thy") || needle.Contains("eco"))
        {
            var m = offers.FirstOrDefault(o => o.Carrier.Equals("Widect", StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }

        if (needle.Contains("fedex"))
        {
            var m = offers.FirstOrDefault(o => o.Carrier.Equals("FedEx", StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }

        if (needle.Contains("ups"))
        {
            bool wantsSaver = needle.Contains("saver") || needle.Contains("expedited");
            var m = offers.FirstOrDefault(o =>
                o.Carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase) &&
                (wantsSaver
                    ? o.ServiceType.Contains("Saver", StringComparison.OrdinalIgnoreCase) || o.CustomerName.Contains("Expedited", StringComparison.OrdinalIgnoreCase)
                    : !o.ServiceType.Contains("Saver", StringComparison.OrdinalIgnoreCase)));
            if (m != null) return m;

            m = offers.FirstOrDefault(o => o.Carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase));
            if (m != null) return m;
        }

        return offers.OrderBy(o => o.Price).FirstOrDefault();
    }

    internal static (string PhoneCode, string PhoneNumber) SplitPhone(string? rawPhone, string countryCode)
    {
        string digits = new string((rawPhone ?? string.Empty).Where(char.IsDigit).ToArray());
        string cc = (countryCode ?? "US").Trim().ToUpperInvariant();

        string dialDigits = cc switch
        {
            "US" or "CA" => "1",
            "GB" or "UK" => "44",
            "DE" => "49",
            "FR" => "33",
            "AU" => "61",
            "NL" => "31",
            "IT" => "39",
            "ES" => "34",
            "BE" => "32",
            "AT" => "43",
            "CH" => "41",
            "TR" => "90",
            _ => "1"
        };

        if (cc == "US" || cc == "CA")
        {
            if (digits.StartsWith("1") && digits.Length == 11)
                return ("+1", digits[1..]);
            return ("+1", digits.Length >= 10 ? digits[^10..] : "4805550199");
        }

        if (digits.StartsWith(dialDigits) && digits.Length > dialDigits.Length + 6)
        {
            return ($"+{dialDigits}", digits[dialDigits.Length..]);
        }

        if (digits.StartsWith("0") && digits.Length >= 9)
        {
            return ($"+{dialDigits}", digits[1..]);
        }

        return ($"+{dialDigits}", string.IsNullOrWhiteSpace(digits) ? "4805550199" : digits);
    }

    private static string ExtractTrackingFromJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                if (root.TryGetProperty("lastMileTrackingNumber", out var lm) && lm.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(lm.GetString()))
                    return lm.GetString()!.Trim();
                if (root.TryGetProperty("trackingNumber", out var tr) && tr.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(tr.GetString()))
                    return tr.GetString()!.Trim();
                if (root.TryGetProperty("reference", out var rf) && rf.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(rf.GetString()))
                    return rf.GetString()!.Trim();
            }
        }
        catch { }
        return string.Empty;
    }

    private static string ExtractLabelUrlFromConsignment(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            if (doc.RootElement.TryGetProperty("files", out var files) && files.ValueKind == JsonValueKind.Array)
            {
                foreach (var f in files.EnumerateArray())
                {
                    if (f.TryGetProperty("remoteFileUrl", out var url) && url.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(url.GetString()))
                        return url.GetString()!.Trim();
                }
            }
        }
        catch { }
        return string.Empty;
    }

    public static string ExtractPortalErrorSummary(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            if (root.ValueKind == JsonValueKind.Object)
            {
                var fieldErrors = new List<string>();
                if (root.TryGetProperty("errors", out var errorsProp))
                {
                    if (errorsProp.ValueKind == JsonValueKind.Object)
                    {
                        foreach (var prop in errorsProp.EnumerateObject())
                        {
                            if (prop.Value.ValueKind == JsonValueKind.Array)
                            {
                                var msgs = prop.Value.EnumerateArray()
                                    .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                                    .Select(x => x.GetString()!.Trim())
                                    .ToList();
                                if (msgs.Count > 0)
                                {
                                    fieldErrors.Add($"{prop.Name}: {string.Join(", ", msgs)}");
                                }
                            }
                            else if (prop.Value.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.Value.GetString()))
                            {
                                fieldErrors.Add($"{prop.Name}: {prop.Value.GetString()!.Trim()}");
                            }
                        }
                    }
                    else if (errorsProp.ValueKind == JsonValueKind.Array)
                    {
                        foreach (var item in errorsProp.EnumerateArray())
                        {
                            if (item.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(item.GetString()))
                                fieldErrors.Add(item.GetString()!.Trim());
                            else if (item.ValueKind == JsonValueKind.Object)
                            {
                                string f = item.TryGetProperty("propertyName", out var pn) ? (pn.GetString() ?? "")
                                         : (item.TryGetProperty("field", out var fn) ? (fn.GetString() ?? "") : "");
                                string m = item.TryGetProperty("errorMessage", out var em) ? (em.GetString() ?? "")
                                         : (item.TryGetProperty("message", out var mm) ? (mm.GetString() ?? "") : "");
                                if (!string.IsNullOrWhiteSpace(m))
                                    fieldErrors.Add(string.IsNullOrWhiteSpace(f) ? m.Trim() : $"{f.Trim()}: {m.Trim()}");
                            }
                        }
                    }
                }

                if (fieldErrors.Count > 0)
                {
                    return string.Join(" | ", fieldErrors);
                }

                foreach (string key in new[] { "message", "detail", "title", "error" })
                {
                    if (root.TryGetProperty(key, out var prop) && prop.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(prop.GetString()))
                        return prop.GetString()!.Trim();
                }
            }
        }
        catch { }
        return body.Length > 350 ? body[..350] : body;
    }

    private async Task<(bool success, string content, HttpStatusCode statusCode, string? reasonPhrase)> ExecutePostRequestRawAsync(
        NavlungoQuoteRequest request,
        NavlungoSettings? settings,
        string actionId,
        CancellationToken cancellationToken)
    {
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, BaseEndpoint);

        // Navlungo Next.js Server Action zorunlu başlıkları:
        httpRequest.Headers.TryAddWithoutValidation("Next-Action", actionId);
        httpRequest.Headers.TryAddWithoutValidation("Accept", "text/x-component");
        httpRequest.Headers.TryAddWithoutValidation("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/133.0.0.0 Safari/537.36");
        httpRequest.Headers.TryAddWithoutValidation("Origin", "https://quick-price-calculator.navlungo.com");
        httpRequest.Headers.TryAddWithoutValidation("Referer", "https://quick-price-calculator.navlungo.com/tr?source=user");

        string cookieHeader = BuildCookieHeader(settings);
        if (!string.IsNullOrWhiteSpace(cookieHeader))
        {
            httpRequest.Headers.TryAddWithoutValidation("Cookie", cookieHeader);
        }

        var payload = new[]
        {
            new
            {
                fromCountry = string.IsNullOrWhiteSpace(request.FromCountry) ? "TR" : request.FromCountry.ToUpperInvariant(),
                toCountry = string.IsNullOrWhiteSpace(request.ToCountry) ? "US" : request.ToCountry.ToUpperInvariant(),
                packages = new[]
                {
                    new
                    {
                        weight = Math.Round(request.WeightKg, 2),
                        length = Math.Round(request.LengthCm, 1),
                        width = Math.Round(request.WidthCm, 1),
                        height = Math.Round(request.HeightCm, 1)
                    }
                },
                source = string.IsNullOrWhiteSpace(request.Source) ? "user" : request.Source
            }
        };

        httpRequest.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            Encoding.UTF8,
            "text/plain");

        HttpResponseMessage response;
        try
        {
            response = await _httpClient.SendAsync(httpRequest, cancellationToken);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new NavlungoApiException("Navlungo fiyat isteği gönderilemedi.", innerException: ex);
        }

        using (response)
        {
            string content = await response.Content.ReadAsStringAsync(cancellationToken);
            return (response.IsSuccessStatusCode, content, response.StatusCode, response.ReasonPhrase);
        }
    }

    private static string TruncateResponse(string content)
    {
        const int maxLength = 4000;
        return content.Length <= maxLength ? content : content[..maxLength];
    }

    /// <summary>
    /// Next.js RSC (text/x-component) veya JSON yanıtından teklifleri ayrıştırır.
    /// </summary>
    public static List<NavlungoQuoteOffer> ParseNavlungoResponse(string content, NavlungoQuoteRequest request)
    {
        var list = new List<NavlungoQuoteOffer>();
        if (string.IsNullOrWhiteSpace(content)) return list;

        try
        {
            // 1. Doğrudan JSON ayrıştırma denemesi (Eğer doğrudan JSON veya dizi döndüyse)
            string trimmed = content.TrimStart();
            if (trimmed.StartsWith("[") || trimmed.StartsWith("{"))
            {
                using var doc = JsonDocument.Parse(trimmed);
                ExtractFromJsonElement(doc.RootElement, list);
                if (list.Count > 0) return list;
            }
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }

        // 2. Next.js RSC streaming satırlarını ve chunk'larındaki JSON nesnelerini ayrıştır
        try
        {
            ExtractFromRscJsonObjects(content, list);
            if (list.Count > 0) return list;
        }
        catch (Exception caught) { AppLog.Swallowed(caught); }

        // 3. Metin/Regex tabanlı ayrıştırma (RSC string chunk'ları içerisindeki taşıyıcı ve fiyat kalıpları)
        // Örn: Widect USD 15.03, FedEx USD 20.75, UPS USD 32.96
        var carrierMatches = new[]
        {
            new { Carrier = "Widect", ServiceType = "Ekonomi", Estimate = "3-7 iş günü", IsExpress = false, IsEco = true },
            new { Carrier = "FedEx", ServiceType = "Express", Estimate = "1-3 iş günü", IsExpress = true, IsEco = false },
            new { Carrier = "UPS", ServiceType = "Express", Estimate = "1-3 iş günü", IsExpress = true, IsEco = false },
            new { Carrier = "UPS", ServiceType = "Express Saver", Estimate = "2-5 iş günü", IsExpress = false, IsEco = false }
        };

        foreach (var c in carrierMatches)
        {
            int idx = content.IndexOf(c.Carrier, StringComparison.OrdinalIgnoreCase);
            while (idx >= 0)
            {
                int len = Math.Min(300, content.Length - idx);
                string snippet = content.Substring(idx, len);

                // 1. Para birimiyle birlikte ara (örn: USD 15.03 veya 15.03 USD veya $15.03)
                var match = Regex.Match(snippet, @"(?:USD|\$|EUR|€|TRY|TL)\s*([0-9]+[.,][0-9]{2})", RegexOptions.IgnoreCase);
                if (!match.Success)
                {
                    match = Regex.Match(snippet, @"([0-9]+[.,][0-9]{2})\s*(?:USD|\$|EUR|€|TRY|TL)", RegexOptions.IgnoreCase);
                }
                if (!match.Success)
                {
                    // "price": 15.03 veya "amount": 15.03
                    match = Regex.Match(snippet, @"(?:price|amount|tutar|fiyat)""?\s*:\s*""?([0-9]+[.,][0-9]{2})", RegexOptions.IgnoreCase);
                }
                if (!match.Success)
                {
                    // Herhangi bir iki basamaklı ondalık sayı (örn. 15.03) ama 3-7 gibi tireli aralık olmasın
                    match = Regex.Match(snippet, @"(?<![\d\-])([0-9]{1,4}[.,][0-9]{2})(?![\d\-])");
                }

                if (match.Success && decimal.TryParse(match.Groups[1].Value.Replace(',', '.'), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsedPrice))
                {
                    if (parsedPrice > 0 && !list.Exists(x => x.Carrier == c.Carrier && x.Price == parsedPrice))
                    {
                        list.Add(new NavlungoQuoteOffer
                        {
                            Carrier = c.Carrier,
                            ServiceName = $"{c.Carrier} {c.Estimate}",
                            ServiceType = c.ServiceType,
                            DeliveryEstimate = c.Estimate,
                            Price = parsedPrice,
                            Currency = "USD",
                            IsBestExpress = c.IsExpress,
                            IsBestEconomy = c.IsEco,
                            Note = $"{c.Carrier} Navlungo Akıllı Navlun"
                        });
                        break;
                    }
                }

                idx = content.IndexOf(c.Carrier, idx + c.Carrier.Length, StringComparison.OrdinalIgnoreCase);
            }
        }

        return list;
    }

    private static void ExtractFromRscJsonObjects(string content, List<NavlungoQuoteOffer> list)
    {
        // 1. Metin içindeki ilk '[' ile son ']' arasındaki JSON dizisini ayrıştırmayı dene
        int firstBracket = content.IndexOf('[');
        int lastBracket = content.LastIndexOf(']');
        if (firstBracket >= 0 && lastBracket > firstBracket)
        {
            string arrayCandidate = content.Substring(firstBracket, lastBracket - firstBracket + 1);
            try
            {
                using var doc = JsonDocument.Parse(arrayCandidate);
                ExtractFromJsonElement(doc.RootElement, list);
                if (list.Count > 0) return;
            }
            catch (Exception caught) { AppLog.Swallowed(caught); }
        }

        // 2. Standart RSC satır satır chunk ayrıştırma (id:[...] veya id:{...})
        var lines = content.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
        foreach (var line in lines)
        {
            int colonIdx = line.IndexOf(':');
            string jsonCandidate = colonIdx >= 0 && colonIdx < 5 ? line.Substring(colonIdx + 1).Trim() : line.Trim();
            if (jsonCandidate.StartsWith("[") || jsonCandidate.StartsWith("{"))
            {
                try
                {
                    using var doc = JsonDocument.Parse(jsonCandidate);
                    ExtractFromJsonElement(doc.RootElement, list);
                }
                catch (Exception caught) { AppLog.Swallowed(caught); }
            }
        }

        if (list.Count > 0) return;

        // 3. Dengeli süslü parantez { ... } bloklarını tarayarak JSON nesnelerini çıkar
        int start = 0;
        while ((start = content.IndexOf('{', start)) >= 0)
        {
            int braceCount = 0;
            int end = -1;
            for (int i = start; i < content.Length; i++)
            {
                if (content[i] == '{') braceCount++;
                else if (content[i] == '}')
                {
                    braceCount--;
                    if (braceCount == 0)
                    {
                        end = i;
                        break;
                    }
                }
            }

            if (end > start)
            {
                string objStr = content.Substring(start, end - start + 1);
                if (objStr.Contains("lastMile", StringComparison.OrdinalIgnoreCase) ||
                    objStr.Contains("carrier", StringComparison.OrdinalIgnoreCase) ||
                    objStr.Contains("price", StringComparison.OrdinalIgnoreCase))
                {
                    try
                    {
                        using var doc = JsonDocument.Parse(objStr);
                        ExtractFromJsonElement(doc.RootElement, list);
                    }
                    catch (Exception caught) { AppLog.Swallowed(caught); }
                }
                start = end + 1;
            }
            else
            {
                start++;
            }
        }
    }

    private static void ExtractFromJsonElement(JsonElement element, List<NavlungoQuoteOffer> list)
    {
        if (element.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in element.EnumerateArray())
            {
                ExtractFromJsonElement(item, list);
            }
        }
        else if (element.ValueKind == JsonValueKind.Object)
        {
            // Resmi Navlungo Quotes API kök nesnesi: { "shipmentSummary": ..., "quotes": [ ... ] }
            if (element.TryGetProperty("quotes", out var quotesArray) && quotesArray.ValueKind == JsonValueKind.Array)
            {
                foreach (var qItem in quotesArray.EnumerateArray())
                {
                    ExtractFromJsonElement(qItem, list);
                }
                return;
            }

            string carrier = string.Empty;
            string customerName = string.Empty;
            string quoteId = string.Empty;
            string shipmentId = string.Empty;
            string searchId = string.Empty;
            string searchReference = string.Empty;
            var availableVas = new List<string>();
            string quoteDescription = string.Empty;
            decimal price = 0;
            string currency = "USD";
            string delivery = "1-4 iş günü";
            string serviceType = "Express";
            bool isBestExpress = false;
            bool isBestEconomy = false;
            double chargeableWeightKg = 0;
            double volumetricWeightKg = 0;
            decimal baseSystemOfferUsd = 0;
            decimal fuelSurchargeUsd = 0;
            decimal peakSeasonSurchargeUsd = 0;
            decimal ddpFeeUsd = 0;
            decimal insuranceFeeUsd = 0;
            decimal premiumTrackingFeeUsd = 0;
            decimal signatureRequiredFeeUsd = 0;
            decimal tryEquivalentAmount = 0;
            bool earlyTrackingEnabled = false;
            bool isDdpRequired = false;

            // 0. Kimlik ve meta alanları
            if (element.TryGetProperty("quoteId", out var qIdProp) && qIdProp.ValueKind == JsonValueKind.String)
                quoteId = qIdProp.GetString() ?? "";
            if (element.TryGetProperty("shipmentId", out var shIdProp) && shIdProp.ValueKind == JsonValueKind.String)
                shipmentId = shIdProp.GetString() ?? "";
            if (element.TryGetProperty("searchId", out var seIdProp) && seIdProp.ValueKind == JsonValueKind.String)
                searchId = seIdProp.GetString() ?? "";
            if (element.TryGetProperty("searchReference", out var srProp) && srProp.ValueKind == JsonValueKind.String)
                searchReference = srProp.GetString() ?? "";
            if (element.TryGetProperty("customerName", out var custProp) && custProp.ValueKind == JsonValueKind.String)
                customerName = custProp.GetString() ?? "";
            if (element.TryGetProperty("quoteDescription", out var qDescProp) && qDescProp.ValueKind == JsonValueKind.String)
                quoteDescription = qDescProp.GetString() ?? "";
            if (element.TryGetProperty("chargeableWeight", out var cwProp) && cwProp.ValueKind == JsonValueKind.Number)
                chargeableWeightKg = cwProp.GetDouble();
            if (element.TryGetProperty("totalVolumetricWeight", out var vwProp) && vwProp.ValueKind == JsonValueKind.Number)
                volumetricWeightKg = vwProp.GetDouble();
            if (element.TryGetProperty("tryEquivalentAmount", out var tryProp) && tryProp.ValueKind == JsonValueKind.Number)
                tryEquivalentAmount = tryProp.GetDecimal();
            if (element.TryGetProperty("earlyTrackingEnabled", out var etProp) && (etProp.ValueKind == JsonValueKind.True || etProp.ValueKind == JsonValueKind.False))
                earlyTrackingEnabled = etProp.GetBoolean();
            if (element.TryGetProperty("isDDPRequired", out var ddpReqProp) && (ddpReqProp.ValueKind == JsonValueKind.True || ddpReqProp.ValueKind == JsonValueKind.False))
                isDdpRequired = ddpReqProp.GetBoolean();

            // 1. lastMile kontrolü (Navlungo API standart yanıtı: thy -> Widect, fedex -> FedEx, ups -> UPS, dhl -> DHL)
            if (element.TryGetProperty("lastMile", out var lmProp))
            {
                string rawLm = lmProp.GetString()?.ToLowerInvariant() ?? "";
                carrier = rawLm switch
                {
                    "thy" => "Widect",
                    "fedex" => "FedEx",
                    "ups" => "UPS",
                    "dhl" => "DHL",
                    "yp" => "Navlungo",
                    "navlungo" => "Navlungo",
                    _ => rawLm.Length > 0 ? char.ToUpperInvariant(rawLm[0]) + rawLm.Substring(1) : "Navlungo"
                };
            }
            else if (element.TryGetProperty("carrier", out var cProp)) carrier = cProp.GetString() ?? "";
            else if (element.TryGetProperty("name", out var nProp)) carrier = nProp.GetString() ?? "";

            // 2. Fiyat kontrolü (resmi quotePrice.amount + eski price / amount alanları)
            if (element.TryGetProperty("quotePrice", out var qpProp) && TryReadMoneyObject(qpProp, out decimal qpAmt, out string? qpCurr))
            {
                price = qpAmt;
                if (!string.IsNullOrWhiteSpace(qpCurr)) currency = qpCurr;
            }
            else if (element.TryGetProperty("price", out var pProp))
            {
                if (TryReadMoneyObject(pProp, out decimal pAmt, out string? pCurr))
                {
                    price = pAmt;
                    if (!string.IsNullOrWhiteSpace(pCurr)) currency = pCurr;
                }
            }
            else if (element.TryGetProperty("amount", out var aProp))
            {
                TryReadMoneyObject(aProp, out price, out _);
            }

            // 2b. priceLines (system-offer)
            if (element.TryGetProperty("priceLines", out var plProp) && plProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var line in plProp.EnumerateArray())
                {
                    string code = line.TryGetProperty("code", out var c) ? (c.GetString() ?? "") : "";
                    if (line.TryGetProperty("price", out var lp) && TryReadMoneyObject(lp, out decimal lineAmt, out _))
                    {
                        if (code.Equals("system-offer", StringComparison.OrdinalIgnoreCase))
                        {
                            baseSystemOfferUsd += lineAmt;
                        }
                    }
                }
            }

            // 2c. surcharges (fuel-surcharge, peak-season-surcharge)
            if (element.TryGetProperty("surcharges", out var scProp) && scProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var sc in scProp.EnumerateArray())
                {
                    string code = sc.TryGetProperty("code", out var c) ? (c.GetString() ?? "") : "";
                    if (sc.TryGetProperty("price", out var sp) && TryReadMoneyObject(sp, out decimal scAmt, out _))
                    {
                        if (code.Equals("fuel-surcharge", StringComparison.OrdinalIgnoreCase))
                        {
                            fuelSurchargeUsd += scAmt;
                        }
                        else
                        {
                            peakSeasonSurchargeUsd += scAmt;
                        }
                    }
                }
            }

            // 2d. valueAddedServices (ddp, insurance, premium-tracking, signature-required)
            if (element.TryGetProperty("valueAddedServices", out var vasProp) && vasProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var vas in vasProp.EnumerateArray())
                {
                    string code = vas.TryGetProperty("code", out var c) ? (c.GetString() ?? "") : "";
                    if (!string.IsNullOrWhiteSpace(code) && !availableVas.Contains(code, StringComparer.OrdinalIgnoreCase))
                    {
                        availableVas.Add(code);
                    }
                    if (vas.TryGetProperty("price", out var vp) && TryReadMoneyObject(vp, out decimal vasAmt, out _))
                    {
                        switch (code.ToLowerInvariant())
                        {
                            case "ddp":
                                ddpFeeUsd = vasAmt;
                                break;
                            case "insurance":
                                insuranceFeeUsd = vasAmt;
                                break;
                            case "premium-tracking":
                                premiumTrackingFeeUsd = vasAmt;
                                break;
                            case "signature-required":
                                signatureRequiredFeeUsd = vasAmt;
                                break;
                        }
                    }
                }
            }

            // 3. Para birimi
            if (element.TryGetProperty("currency", out var currProp))
            {
                if (currProp.ValueKind == JsonValueKind.String)
                {
                    currency = currProp.GetString() ?? currency;
                }
                else if (currProp.ValueKind == JsonValueKind.Object)
                {
                    if (currProp.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.String)
                        currency = cv.GetString() ?? currency;
                    else if (currProp.TryGetProperty("name", out var cn) && cn.ValueKind == JsonValueKind.String)
                        currency = cn.GetString() ?? currency;
                }
            }

            // 4. Servis tipi (UPS Express vs UPS Expedited ayrımı dahil)
            if (element.TryGetProperty("serviceType", out var stProp))
            {
                string rawSt = stProp.GetString() ?? "";
                if (rawSt.Equals("economy", StringComparison.OrdinalIgnoreCase)) serviceType = "Ekonomi";
                else if (rawSt.Equals("express", StringComparison.OrdinalIgnoreCase)) serviceType = "Express";
                else if (!string.IsNullOrWhiteSpace(rawSt)) serviceType = rawSt;
            }

            if (carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase) &&
                (customerName.Contains("Expedited", StringComparison.OrdinalIgnoreCase) ||
                 customerName.Contains("Saver", StringComparison.OrdinalIgnoreCase)))
            {
                serviceType = "Express Saver";
            }

            // 5. Teslimat süresi (minTransitTime/maxTransitTime ve official minimumTransitTime/maximumTransitTime)
            if ((element.TryGetProperty("minTransitTime", out var minT) && element.TryGetProperty("maxTransitTime", out var maxT)) ||
                (element.TryGetProperty("minimumTransitTime", out minT) && element.TryGetProperty("maximumTransitTime", out maxT)))
            {
                if (minT.TryGetInt32(out int minDays) && maxT.TryGetInt32(out int maxDays))
                {
                    delivery = $"{minDays}-{maxDays} iş günü";
                }
            }
            else if (element.TryGetProperty("deliveryTime", out var dProp)) delivery = dProp.GetString() ?? delivery;

            // 6. Etiketler (best-express-price, best-economy-price, best-price vb.)
            if (element.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    string t = tag.GetString() ?? "";
                    if (t.Contains("best-express", StringComparison.OrdinalIgnoreCase)) isBestExpress = true;
                    if (t.Contains("best-economy", StringComparison.OrdinalIgnoreCase) ||
                        t.Equals("best-price", StringComparison.OrdinalIgnoreCase))
                    {
                        isBestEconomy = true;
                    }
                }
            }

            if (baseSystemOfferUsd <= 0 && price > 0)
            {
                decimal nonBase = fuelSurchargeUsd + peakSeasonSurchargeUsd;
                if (price > nonBase)
                {
                    baseSystemOfferUsd = Math.Round(price - nonBase, 2);
                }
            }

            string serviceName = carrier.Equals("UPS", StringComparison.OrdinalIgnoreCase) &&
                                 serviceType.Equals("Express Saver", StringComparison.OrdinalIgnoreCase)
                ? $"UPS Expedited {delivery}"
                : $"{carrier} {delivery}";

            if (!string.IsNullOrWhiteSpace(carrier) && price > 0)
            {
                if (!list.Exists(x =>
                    x.Carrier.Equals(carrier, StringComparison.OrdinalIgnoreCase) &&
                    x.Price == price &&
                    x.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase) &&
                    x.ServiceName.Equals(serviceName, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new NavlungoQuoteOffer
                    {
                        Carrier = carrier,
                        ServiceName = serviceName,
                        ServiceType = serviceType,
                        DeliveryEstimate = delivery,
                        Price = price,
                        Currency = currency,
                        IsBestExpress = isBestExpress,
                        IsBestEconomy = isBestEconomy,
                        Note = "Navlungo API Canlı Teklif",
                        QuoteId = quoteId,
                        ShipmentId = shipmentId,
                        SearchId = searchId,
                        SearchReference = searchReference,
                        AvailableValueAddedServices = availableVas,
                        CustomerName = customerName,
                        ChargeableWeightKg = chargeableWeightKg,
                        VolumetricWeightKg = volumetricWeightKg,
                        BaseSystemOfferUsd = baseSystemOfferUsd,
                        FuelSurchargeUsd = fuelSurchargeUsd,
                        PeakSeasonSurchargeUsd = peakSeasonSurchargeUsd,
                        DdpFeeUsd = ddpFeeUsd,
                        InsuranceFeeUsd = insuranceFeeUsd,
                        PremiumTrackingFeeUsd = premiumTrackingFeeUsd,
                        SignatureRequiredFeeUsd = signatureRequiredFeeUsd,
                        TryEquivalentAmount = tryEquivalentAmount,
                        EarlyTrackingEnabled = earlyTrackingEnabled,
                        IsDdpRequired = isDdpRequired,
                        QuoteDescription = quoteDescription
                    });
                }
            }
        }
    }

    private static bool TryReadMoneyObject(JsonElement prop, out decimal amount, out string? currency)
    {
        amount = 0;
        currency = null;

        if (prop.ValueKind == JsonValueKind.Number)
        {
            amount = prop.GetDecimal();
            return true;
        }

        if (prop.ValueKind == JsonValueKind.String)
        {
            return decimal.TryParse(prop.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out amount);
        }

        if (prop.ValueKind == JsonValueKind.Object)
        {
            bool foundAmount = false;
            if (prop.TryGetProperty("amount", out var aProp))
            {
                if (aProp.ValueKind == JsonValueKind.Number)
                {
                    amount = aProp.GetDecimal();
                    foundAmount = true;
                }
                else if (aProp.ValueKind == JsonValueKind.String &&
                         decimal.TryParse(aProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal parsed))
                {
                    amount = parsed;
                    foundAmount = true;
                }
            }

            if (prop.TryGetProperty("currency", out var currProp))
            {
                if (currProp.ValueKind == JsonValueKind.String)
                {
                    currency = currProp.GetString();
                }
                else if (currProp.ValueKind == JsonValueKind.Object)
                {
                    if (currProp.TryGetProperty("value", out var cv) && cv.ValueKind == JsonValueKind.String)
                        currency = cv.GetString();
                    else if (currProp.TryGetProperty("name", out var cn) && cn.ValueKind == JsonValueKind.String)
                        currency = cn.GetString();
                }
            }

            return foundAmount;
        }

        return false;
    }

    /// <summary>
    /// Çevrimdışı veya Cloudflare koruması devredeyken gerçeğe uygun piyasa fiyatlarını üretir.
    /// </summary>
    public static List<NavlungoQuoteOffer> GenerateRealisticFallbackQuotes(NavlungoQuoteRequest request)
        => NavlungoFallbackPricing.GenerateRealisticQuotes(request);
}
