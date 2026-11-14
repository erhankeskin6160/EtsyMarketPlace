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

        string rawToken = (settings.IdToken ?? string.Empty).Trim();
        if (rawToken.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
        {
            req.Headers.TryAddWithoutValidation("Authorization", rawToken);
        }
        else if (rawToken.Count(c => c == '.') == 2)
        {
            req.Headers.TryAddWithoutValidation("Authorization", $"Bearer {rawToken}");
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
