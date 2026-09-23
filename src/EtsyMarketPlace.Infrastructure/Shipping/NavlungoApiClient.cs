namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
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

    public const string AnonymousActionId = "406fad5769e32876f9c8eeccd5dab4d086b9068ca5";
    public const string AuthenticatedActionId = "40198494a378e36987abc2fe2dd302fceb194b28c4";

    private static string? _cachedAnonymousActionId = AnonymousActionId;
    private static string? _cachedAuthenticatedActionId = AuthenticatedActionId;
    private static DateTime _lastResolvedUtc = DateTime.UtcNow;
    private static readonly SemaphoreSlim _resolveLock = new(1, 1);

    public NavlungoApiClient(HttpClient? httpClient = null)
    {
        _httpClient = httpClient ?? new HttpClient();
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
                        var anonMatch = Regex.Match(js, @"createServerReference\(""([a-f0-9]+)""[^,]+,[^,]+,[^,]+,""callCalculationAnonymousApi""\)");
                        var authMatch = Regex.Match(js, @"createServerReference\(""([a-f0-9]+)""[^,]+,[^,]+,[^,]+,""callCalculationApi""\)");
                        
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
        
        // 1. Güncel veya önbellekteki Action ID'yi al
        var (anonId, authId) = await ResolveActionIdsAsync(_httpClient, forceRefresh: false, cancellationToken);
        string actionId = hasSession ? authId : anonId;

        var (success, content, statusCode, reasonPhrase) = await ExecutePostRequestRawAsync(request, settings, actionId, cancellationToken);

        // 2. Eğer HTML sayfası döndüyse (Next.js build almış ve eski Action ID HTML döndürmüşse),
        // Action ID'yi zorla yenileyerek (forceRefresh) bir kez daha dene!
        if (success && (content.TrimStart().StartsWith("<!DOCTYPE html", StringComparison.OrdinalIgnoreCase) ||
                        content.TrimStart().StartsWith("<html", StringComparison.OrdinalIgnoreCase)))
        {
            var (freshAnon, freshAuth) = await ResolveActionIdsAsync(_httpClient, forceRefresh: true, cancellationToken);
            string freshActionId = hasSession ? freshAuth : freshAnon;
            if (freshActionId != actionId)
            {
                (success, content, statusCode, reasonPhrase) = await ExecutePostRequestRawAsync(request, settings, freshActionId, cancellationToken);
            }
        }

        // Hata tespiti ve canlı izleme için son yanıtı kaydet
        try
        {
            string debugPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "navlungo_last_response.log");
            File.WriteAllText(debugPath, content);
        }
        catch { }

        if (!success)
        {
            throw new NavlungoApiException(
                $"Navlungo HTTP {(int)statusCode} ({reasonPhrase}).",
                statusCode,
                TruncateResponse(content));
        }

        var offers = ParseNavlungoResponse(content, request);
        if (offers.Count == 0)
        {
            throw new NavlungoApiException(
                "Navlungo yanıtı alındı ancak teklif response içinden ayrıştırılamadı.",
                statusCode,
                TruncateResponse(content));
        }

        return offers;
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

        var cookieBuilder = new StringBuilder();
        if (settings != null)
        {
            if (!string.IsNullOrWhiteSpace(settings.IdToken))
            {
                cookieBuilder.Append($"id_token={settings.IdToken.Trim()}; ");
            }
            if (!string.IsNullOrWhiteSpace(settings.SessionCookie))
            {
                cookieBuilder.Append(settings.SessionCookie.Trim());
            }
        }

        if (cookieBuilder.Length > 0)
        {
            httpRequest.Headers.TryAddWithoutValidation("Cookie", cookieBuilder.ToString());
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
        catch { }

        // 2. Next.js RSC streaming satırlarını ve chunk'larındaki JSON nesnelerini ayrıştır
        try
        {
            ExtractFromRscJsonObjects(content, list);
            if (list.Count > 0) return list;
        }
        catch { }

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
            catch { }
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
                catch { }
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
                    catch { }
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
            string carrier = string.Empty;
            decimal price = 0;
            string currency = "USD";
            string delivery = "1-4 iş günü";
            string serviceType = "Express";
            bool isBestExpress = false;
            bool isBestEconomy = false;

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

            // 2. Fiyat kontrolü
            if (element.TryGetProperty("price", out var pProp))
            {
                if (pProp.ValueKind == JsonValueKind.Number) price = pProp.GetDecimal();
                else if (decimal.TryParse(pProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal p)) price = p;
            }
            else if (element.TryGetProperty("amount", out var aProp))
            {
                if (aProp.ValueKind == JsonValueKind.Number) price = aProp.GetDecimal();
                else if (decimal.TryParse(aProp.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out decimal a)) price = a;
            }

            // 3. Para birimi
            if (element.TryGetProperty("currency", out var currProp)) currency = currProp.GetString() ?? "USD";

            // 4. Servis tipi
            if (element.TryGetProperty("serviceType", out var stProp))
            {
                string rawSt = stProp.GetString() ?? "";
                if (rawSt.Equals("economy", StringComparison.OrdinalIgnoreCase)) serviceType = "Ekonomi";
                else if (rawSt.Equals("express", StringComparison.OrdinalIgnoreCase)) serviceType = "Express";
                else if (!string.IsNullOrWhiteSpace(rawSt)) serviceType = rawSt;
            }

            // 5. Teslimat süresi
            if (element.TryGetProperty("minTransitTime", out var minT) && element.TryGetProperty("maxTransitTime", out var maxT))
            {
                if (minT.TryGetInt32(out int minDays) && maxT.TryGetInt32(out int maxDays))
                {
                    delivery = $"{minDays}-{maxDays} iş günü";
                }
            }
            else if (element.TryGetProperty("deliveryTime", out var dProp)) delivery = dProp.GetString() ?? delivery;

            // 6. Etiketler (best-express-price, best-economy-price vb.)
            if (element.TryGetProperty("tags", out var tagsProp) && tagsProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var tag in tagsProp.EnumerateArray())
                {
                    string t = tag.GetString() ?? "";
                    if (t.Contains("best-express", StringComparison.OrdinalIgnoreCase)) isBestExpress = true;
                    if (t.Contains("best-economy", StringComparison.OrdinalIgnoreCase)) isBestEconomy = true;
                }
            }

            if (!string.IsNullOrWhiteSpace(carrier) && price > 0)
            {
                if (!list.Exists(x => x.Carrier.Equals(carrier, StringComparison.OrdinalIgnoreCase) && x.Price == price && x.ServiceType.Equals(serviceType, StringComparison.OrdinalIgnoreCase)))
                {
                    list.Add(new NavlungoQuoteOffer
                    {
                        Carrier = carrier,
                        ServiceName = $"{carrier} {delivery}",
                        ServiceType = serviceType,
                        DeliveryEstimate = delivery,
                        Price = price,
                        Currency = currency,
                        IsBestExpress = isBestExpress,
                        IsBestEconomy = isBestEconomy,
                        Note = "Navlungo API Canlı Teklif"
                    });
                }
            }
        }
    }

    /// <summary>
    /// Çevrimdışı veya Cloudflare koruması devredeyken gerçeğe uygun piyasa fiyatlarını üretir.
    /// </summary>
    public static List<NavlungoQuoteOffer> GenerateRealisticFallbackQuotes(NavlungoQuoteRequest request)
    {
        double billable = request.BillableWeightKg;

        // Navlungo Piyasa Tabanlı Canlı Oranlar (Ekran görüntünüzdeki Widect $15.03, FedEx $20.75, UPS $32.96 referans alınarak):
        decimal widectBase = 11.50m + (decimal)(billable * 8.80);
        decimal fedexBase = 16.00m + (decimal)(billable * 11.80);
        decimal upsExpressBase = 26.00m + (decimal)(billable * 17.40);
        decimal upsSaverBase = 30.50m + (decimal)(billable * 18.00);

        return new List<NavlungoQuoteOffer>
        {
            new()
            {
                Carrier = "Widect",
                ServiceName = "Widect 3-7 iş günü",
                ServiceType = "Ekonomi",
                DeliveryEstimate = "3-7 iş günü",
                Price = Math.Round(widectBase, 2),
                Currency = "USD",
                IsBestEconomy = true,
                Note = "Navlungo Özel Anlaşmalı Eko Navlun"
            },
            new()
            {
                Carrier = "FedEx",
                ServiceName = "FedEx 1-3 iş günü",
                ServiceType = "Express",
                DeliveryEstimate = "1-3 iş günü",
                Price = Math.Round(fedexBase, 2),
                Currency = "USD",
                IsBestExpress = true,
                Note = "En Uygun Express Fiyat (Navlungo)"
            },
            new()
            {
                Carrier = "UPS",
                ServiceName = "UPS 1-3 iş günü",
                ServiceType = "Express",
                DeliveryEstimate = "1-3 iş günü",
                Price = Math.Round(upsExpressBase, 2),
                Currency = "USD",
                Note = "UPS Hava Kargo Güvencesi"
            },
            new()
            {
                Carrier = "UPS",
                ServiceName = "UPS 2-5 iş günü",
                ServiceType = "Express Saver",
                DeliveryEstimate = "2-5 iş günü",
                Price = Math.Round(upsSaverBase, 2),
                Currency = "USD",
                Note = "UPS Standart Yurtdışı Teslimat"
            }
        };
    }
}
