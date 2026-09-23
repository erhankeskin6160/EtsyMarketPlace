namespace EtsyMarketPlace.Infrastructure.Shipping;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Shipping;

/// <summary>
/// Shiptomore (shiptomore.com) Odoo JSON-RPC canlı fiyat hesaplama istemcisi.
/// </summary>
public sealed class ShiptomoreApiClient : IShiptomoreApiClient
{
    private static readonly HttpClient HttpClient = new(new HttpClientHandler
    {
        UseCookies = false,
        AutomaticDecompression = System.Net.DecompressionMethods.GZip | System.Net.DecompressionMethods.Deflate
    })
    {
        Timeout = TimeSpan.FromSeconds(15)
    };

    private const string BaseUrl = "https://shiptomore.com";
    private const string CalculateEndpoint = $"{BaseUrl}/parcel/calculate";
    private const string CountriesEndpoint = $"{BaseUrl}/parcel/countries";

    // Yaygın ülkelerin Shiptomore Odoo veritabanı ID eşleştirmeleri
    private static readonly ConcurrentDictionary<string, int> CountryIdCache = new(StringComparer.OrdinalIgnoreCase)
    {
        ["TR"] = 224, // Türkiye
        ["US"] = 233, // Amerika Birleşik Devletleri
        ["DE"] = 57,  // Almanya
        ["GB"] = 231, // Birleşik Krallık
        ["UK"] = 231,
        ["FR"] = 75,  // Fransa
        ["IT"] = 109, // İtalya
        ["CA"] = 38,  // Kanada
        ["AU"] = 13,  // Avustralya
        ["NL"] = 165, // Hollanda
        ["ES"] = 68,  // İspanya
        ["BE"] = 20,  // Belçika
        ["AT"] = 12,  // Avusturya
        ["CH"] = 43,  // İsviçre
        ["SE"] = 196, // İsveç
        ["NO"] = 166, // Norveç
        ["DK"] = 59,  // Danimarka
        ["FI"] = 70,  // Finlandiya
        ["PL"] = 178, // Polonya
        ["IE"] = 101, // İrlanda
        ["PT"] = 183, // Portekiz
        ["AE"] = 2,   // Birleşik Arap Emirlikleri
        ["SA"] = 192  // Suudi Arabistan
    };

    public async Task<List<ShiptomoreQuoteOffer>> FetchLiveQuotesAsync(
        ShiptomoreQuoteRequest request,
        ShiptomoreSettings? settings = null,
        CancellationToken cancellationToken = default)
    {
        int toCountryId = await ResolveCountryIdAsync(request.ToCountry, cancellationToken);
        int fromCountryId = await ResolveCountryIdAsync(request.FromCountry, cancellationToken);
        if (fromCountryId <= 0) fromCountryId = 224; // Varsayılan TR

        var rpcPayload = new
        {
            jsonrpc = "2.0",
            method = "call",
            params_ = new
            {
                from_country_id = fromCountryId,
                country_id = toCountryId,
                to_state_id = (int?)null,
                description = "Website Calculation",
                package_type = string.IsNullOrWhiteSpace(request.PackageType) ? "custom" : request.PackageType,
                quick_calc = true,
                packages = new[]
                {
                    new
                    {
                        weight = Math.Round(request.WeightKg, 2),
                        qty = request.Quantity > 0 ? request.Quantity : 1,
                        length = Math.Round(request.LengthCm, 1),
                        width = Math.Round(request.WidthCm, 1),
                        height = Math.Round(request.HeightCm, 1)
                    }
                }
            }
        };

        // Odoo 'params' anahtar sözcüğü için özel serialize
        string jsonBody = JsonSerializer.Serialize(rpcPayload).Replace("\"params_\":", "\"params\":");

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, CalculateEndpoint)
        {
            Content = new StringContent(jsonBody, Encoding.UTF8, "application/json")
        };

        httpRequest.Headers.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/126.0.0.0 Safari/537.36");
        httpRequest.Headers.Add("Accept", "application/json, text/plain, */*");
        httpRequest.Headers.Add("Origin", BaseUrl);
        httpRequest.Headers.Add("Referer", $"{BaseUrl}/my/parcel-calculator");

        // Oturum çerezi (session_id) ekleme
        bool hasSession = false;
        string? rawCookie = settings?.SessionCookie;
        if (!string.IsNullOrWhiteSpace(rawCookie))
        {
            string sanitized = NavlungoCookieSanitizer.Sanitize(rawCookie);
            if (!string.IsNullOrWhiteSpace(sanitized))
            {
                // Eğer doğrudan 'session_id=...' değilse veya birden çok çerez varsa
                if (!sanitized.Contains("session_id=", StringComparison.OrdinalIgnoreCase) && !sanitized.Contains('='))
                {
                    sanitized = $"session_id={sanitized}";
                }
                httpRequest.Headers.Add("Cookie", sanitized);
                hasSession = true;
            }
        }

        try
        {
            using var response = await HttpClient.SendAsync(httpRequest, cancellationToken);
            if (!response.IsSuccessStatusCode)
            {
                return GenerateRealisticFallbackQuotes(request, hasSession);
            }

            string responseJson = await response.Content.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(responseJson);

            if (doc.RootElement.TryGetProperty("result", out var resultEl) &&
                resultEl.TryGetProperty("success", out var successEl) &&
                successEl.GetBoolean() &&
                resultEl.TryGetProperty("options", out var optionsEl) &&
                optionsEl.ValueKind == JsonValueKind.Array)
            {
                var offers = new List<ShiptomoreQuoteOffer>();
                foreach (var opt in optionsEl.EnumerateArray())
                {
                    string carrier = opt.TryGetProperty("provider_name", out var pName) ? pName.GetString() ?? "" : "";
                    string serviceName = opt.TryGetProperty("service_name", out var sName) ? sName.GetString() ?? "" : "";
                    string serviceDesc = opt.TryGetProperty("service_description", out var sDesc) ? sDesc.GetString() ?? "" : "";
                    string deliveryEst = opt.TryGetProperty("estimatedDelivery", out var est) ? est.GetString() ?? serviceDesc : serviceDesc;

                    decimal price = 0;
                    if (opt.TryGetProperty("price", out var priceEl))
                    {
                        if (priceEl.ValueKind == JsonValueKind.Number)
                            price = priceEl.GetDecimal();
                        else if (decimal.TryParse(priceEl.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsed))
                            price = parsed;
                    }

                    int providerId = opt.TryGetProperty("provider_id", out var pid) && pid.ValueKind == JsonValueKind.Number ? pid.GetInt32() : 0;
                    string? logoUrl = null;
                    if (opt.TryGetProperty("provider_logo", out var logoEl))
                    {
                        string? rawLogo = logoEl.GetString();
                        if (!string.IsNullOrWhiteSpace(rawLogo))
                        {
                            logoUrl = rawLogo.StartsWith("http", StringComparison.OrdinalIgnoreCase)
                                ? rawLogo
                                : $"{BaseUrl}{rawLogo}";
                        }
                    }

                    double billableWeight = 0;
                    if (opt.TryGetProperty("weight", out var weightEl) && weightEl.ValueKind == JsonValueKind.Number)
                    {
                        billableWeight = weightEl.GetDouble();
                    }

                    offers.Add(new ShiptomoreQuoteOffer
                    {
                        ProviderId = providerId,
                        Carrier = carrier,
                        ServiceName = serviceName,
                        ServiceDescription = serviceDesc,
                        DeliveryEstimate = deliveryEst,
                        Price = price,
                        Currency = "USD",
                        CurrencySymbol = "$",
                        BillableWeight = billableWeight > 0 ? billableWeight : request.BillableWeightKg,
                        ProviderLogoUrl = logoUrl,
                        IsMemberRate = hasSession,
                        Note = hasSession ? "Shiptomore Üye İndirimi (Canlı)" : "Shiptomore Standart Liste Fiyatı"
                    });
                }

                if (offers.Count > 0)
                {
                    return offers.OrderBy(o => o.Price).ToList();
                }
            }

            return GenerateRealisticFallbackQuotes(request, hasSession);
        }
        catch
        {
            return GenerateRealisticFallbackQuotes(request, hasSession);
        }
    }

    private static async Task<int> ResolveCountryIdAsync(string countryCode, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(countryCode)) return 233; // US
        string clean = countryCode.Trim().ToUpperInvariant();

        if (CountryIdCache.TryGetValue(clean, out int cachedId))
        {
            return cachedId;
        }

        // Dinamik olarak Shiptomore'dan ülke listesini çekip cache'i doldurmayı dene
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, CountriesEndpoint)
            {
                Content = new StringContent("{\"jsonrpc\":\"2.0\",\"method\":\"call\",\"params\":{}}", Encoding.UTF8, "application/json")
            };
            using var resp = await HttpClient.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
            {
                string json = await resp.Content.ReadAsStringAsync(ct);
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("result", out var res) &&
                    res.TryGetProperty("countries", out var cList) &&
                    cList.ValueKind == JsonValueKind.Array)
                {
                    foreach (var c in cList.EnumerateArray())
                    {
                        if (c.TryGetProperty("code", out var codeProp) &&
                            c.TryGetProperty("id", out var idProp) &&
                            idProp.ValueKind == JsonValueKind.Number)
                        {
                            string? code = codeProp.GetString();
                            if (!string.IsNullOrWhiteSpace(code))
                            {
                                CountryIdCache[code.Trim().ToUpperInvariant()] = idProp.GetInt32();
                            }
                        }
                    }

                    if (CountryIdCache.TryGetValue(clean, out int freshId))
                    {
                        return freshId;
                    }
                }
            }
        }
        catch { }

        return 233; // Bulunamazsa US
    }

    /// <summary>
    /// API çevrimdışı veya hata verdiğinde çalışan gerçekçi yedek fiyat motoru.
    /// </summary>
    public static List<ShiptomoreQuoteOffer> GenerateRealisticFallbackQuotes(ShiptomoreQuoteRequest req, bool isMember = false)
    {
        double bw = req.BillableWeightKg;
        decimal weightFactor = (decimal)Math.Max(0.5, bw);

        // US ve Avrupa için baz tarifeler
        bool isUs = req.ToCountry.Equals("US", StringComparison.OrdinalIgnoreCase);

        decimal widectBase = isUs ? 13.00m : 12.50m;
        decimal fedexBase = isUs ? 18.55m : 17.50m;

        if (!isMember)
        {
            widectBase = isUs ? 16.96m : 15.80m;
            fedexBase = isUs ? 24.19m : 22.00m;
        }

        // Ağırlık arttıkça makul artış katsayısı
        decimal weightMultiplier = 1.0m;
        if (bw > 0.5)
        {
            weightMultiplier = 1.0m + ((decimal)(bw - 0.5) * 0.45m);
        }

        decimal widectPrice = Math.Round(widectBase * weightMultiplier, 2);
        decimal fedexPrice = Math.Round(fedexBase * weightMultiplier, 2);

        return new List<ShiptomoreQuoteOffer>
        {
            new()
            {
                ProviderId = 2,
                Carrier = "Widect",
                ServiceName = "by THY",
                ServiceDescription = "4-9 İş Günü",
                DeliveryEstimate = "4-9 İş Günü",
                Price = widectPrice,
                Currency = "USD",
                CurrencySymbol = "$",
                BillableWeight = bw,
                IsMemberRate = isMember,
                Note = isMember ? "Shiptomore Üye İndirimi (Simüle)" : "Shiptomore Standart Liste (Simüle)"
            },
            new()
            {
                ProviderId = 3,
                Carrier = "FedEx",
                ServiceName = "Express - 3 Gün",
                ServiceDescription = "1-3 İş Günü",
                DeliveryEstimate = "1-3 İş Günü",
                Price = fedexPrice,
                Currency = "USD",
                CurrencySymbol = "$",
                BillableWeight = bw,
                IsMemberRate = isMember,
                Note = isMember ? "Shiptomore Üye İndirimi (Simüle)" : "Shiptomore Standart Liste (Simüle)"
            }
        };
    }
}
