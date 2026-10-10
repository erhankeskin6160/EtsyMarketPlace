using System.Globalization;
using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using EtsyMarketPlace.Api.Models;
using EtsyMarketPlace.Application.EtsyIntegration;

namespace EtsyMarketPlace.Api.Controllers;

[ApiController]
[Route("api/etsy/market")]
public class EtsyMarketController : BaseApiController
{
    private readonly IConfiguration _config;
    private readonly IEtsyTokenStore _tokenStore;
    private readonly IShopSettingsRepository _settingsRepo;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IEtsyOAuthService _oauthService;

    public EtsyMarketController(
        IConfiguration config,
        IEtsyTokenStore tokenStore,
        IShopSettingsRepository settingsRepo,
        IHttpClientFactory httpClientFactory,
        IEtsyOAuthService oauthService)
    {
        _config = config;
        _tokenStore = tokenStore;
        _settingsRepo = settingsRepo;
        _httpClientFactory = httpClientFactory;
        _oauthService = oauthService;
    }

    [HttpGet("search")]
    [EndpointSummary("Etsy v3 Canlı Pazar Araması, Rakip Listelemeleri, SEO & Fırsat Puanları")]
    public async Task<IActionResult> SearchMarket(
        [FromQuery] string keyword = "",
        [FromQuery] int limit = 50,
        [FromQuery] string sortBy = "market_score",
        [FromQuery] string shopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(keyword))
        {
            return Ok(new MarketSearchResponse());
        }

        var resolvedShopId = ResolveShopId(shopId, _config);
        var raw = await _settingsRepo.GetRawEtsyAppCredentialsAsync(resolvedShopId, cancellationToken);
        var keystring = !string.IsNullOrWhiteSpace(raw.Keystring) ? raw.Keystring : (_config["Etsy:ApiKey"] ?? string.Empty);
        var sharedSecret = !string.IsNullOrWhiteSpace(raw.SharedSecret) ? raw.SharedSecret : (_config["Etsy:SharedSecret"] ?? string.Empty);
        if (string.IsNullOrWhiteSpace(keystring))
        {
            return BadRequest(new { error = "Etsy API Keystring bulunamadı. Lütfen Ayarlar > Etsy API sayfasından API anahtarınızı kaydedin." });
        }
        var apiKeyHeader = !string.IsNullOrWhiteSpace(sharedSecret) ? $"{keystring.Trim()}:{sharedSecret.Trim()}" : keystring.Trim();

        var token = await _tokenStore.GetAsync(resolvedShopId, cancellationToken);
        if (token != null && token.AccessTokenExpiresAt <= DateTimeOffset.UtcNow.AddMinutes(1) && !string.IsNullOrWhiteSpace(token.RefreshToken))
        {
            try
            {
                token = await _oauthService.RefreshTokenAsync(resolvedShopId, token.RefreshToken, cancellationToken);
                await _tokenStore.SaveAsync(resolvedShopId, token, cancellationToken);
            }
            catch
            {
                // Canlı pazar araması için token yenilenemezse x-api-key ile genel arama sürdürülür
            }
        }

        var client = _httpClientFactory.CreateClient();
        var clampedLimit = Math.Clamp(limit, 1, 100);
        var encodedKeyword = Uri.EscapeDataString(keyword.Trim());
        var url = $"https://api.etsy.com/v3/application/listings/active?keywords={encodedKeyword}&limit={clampedLimit}&sort_on=score&sort_order=desc&includes=Shop,Images";

        using var request = new HttpRequestMessage(HttpMethod.Get, url);
        request.Headers.Add("x-api-key", apiKeyHeader);
        if (token != null && !string.IsNullOrWhiteSpace(token.AccessToken))
        {
            request.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
        }

        using var response = await client.SendAsync(request, cancellationToken);
        string body;
        if (!response.IsSuccessStatusCode)
        {
            var fallbackUrl = $"https://api.etsy.com/v3/application/listings/active?keywords={encodedKeyword}&limit={clampedLimit}&sort_on=score&sort_order=desc";
            using var fbReq = new HttpRequestMessage(HttpMethod.Get, fallbackUrl);
            fbReq.Headers.Add("x-api-key", apiKeyHeader);
            if (token != null && !string.IsNullOrWhiteSpace(token.AccessToken))
            {
                fbReq.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token.AccessToken);
            }
            using var fbResp = await client.SendAsync(fbReq, cancellationToken);
            if (!fbResp.IsSuccessStatusCode)
            {
                var err = await response.Content.ReadAsStringAsync(cancellationToken);
                return BadRequest(new { error = $"Etsy Pazar Arama hatası (HTTP {(int)response.StatusCode}): {err}" });
            }
            body = await fbResp.Content.ReadAsStringAsync(cancellationToken);
        }
        else
        {
            body = await response.Content.ReadAsStringAsync(cancellationToken);
        }

        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        int totalCount = 0;
        if (root.TryGetProperty("count", out var countProp) && countProp.TryGetInt32(out var c))
        {
            totalCount = c;
        }

        if (!root.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return Ok(new MarketSearchResponse
            {
                Keyword = keyword,
                Total = 0,
                Kpis = new MarketSummaryKpisDto(),
                Listings = []
            });
        }

        var listings = new List<MarketListingItemDto>();
        foreach (var item in results.EnumerateArray())
        {
            long listingId = item.TryGetProperty("listing_id", out var lid) ? lid.GetInt64() : 0;
            long itemShopId = item.TryGetProperty("shop_id", out var sid) ? sid.GetInt64() : 0;
            string title = item.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
            string description = item.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
            int views = item.TryGetProperty("views", out var v) ? v.GetInt32() : 0;

            int favorites = 0;
            if (item.TryGetProperty("num_favorers", out var f))
            {
                favorites = f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int.TryParse(f.GetString(), out var nf) ? nf : 0);
            }
            else if (item.TryGetProperty("favorites", out var favProp))
            {
                favorites = favProp.ValueKind == JsonValueKind.Number ? favProp.GetInt32() : (int.TryParse(favProp.GetString(), out var nf) ? nf : 0);
            }

            int quantity = item.TryGetProperty("quantity", out var q) ? q.GetInt32() : 0;

            decimal price = 0;
            string currency = "USD";
            if (item.TryGetProperty("price", out var pObj) && pObj.ValueKind == JsonValueKind.Object)
            {
                if (pObj.TryGetProperty("amount", out var aProp) && pObj.TryGetProperty("divisor", out var divProp))
                {
                    var divisor = divProp.GetInt32();
                    if (divisor > 0) price = aProp.GetInt64() / (decimal)divisor;
                }
                if (pObj.TryGetProperty("currency_code", out var cProp)) currency = cProp.GetString() ?? "USD";
            }

            var tags = new List<string>();
            if (item.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var tg in tagsArr.EnumerateArray())
                {
                    var str = tg.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) tags.Add(str.Trim());
                }
            }

            var materials = new List<string>();
            if (item.TryGetProperty("materials", out var matArr) && matArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var mt in matArr.EnumerateArray())
                {
                    var str = mt.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) materials.Add(str.Trim());
                }
            }

            var imageUrls = new List<string>();
            var imgProp = (item.TryGetProperty("Images", out var p1) && p1.ValueKind == JsonValueKind.Array) ? p1
                : ((item.TryGetProperty("images", out var p2) && p2.ValueKind == JsonValueKind.Array) ? p2 : default);

            if (imgProp.ValueKind == JsonValueKind.Array)
            {
                foreach (var img in imgProp.EnumerateArray())
                {
                    string? imgUrl = null;
                    if (img.TryGetProperty("url_570xN", out var u570)) imgUrl = u570.GetString();
                    else if (img.TryGetProperty("url_fullxfull", out var uFull)) imgUrl = uFull.GetString();
                    else if (img.TryGetProperty("url_170x135", out var u170)) imgUrl = u170.GetString();
                    else if (img.TryGetProperty("url_75x75", out var u75)) imgUrl = u75.GetString();

                    if (!string.IsNullOrWhiteSpace(imgUrl)) imageUrls.Add(imgUrl.Trim());
                }
            }
            var primaryImg = imageUrls.FirstOrDefault() ?? "";

            // Mağaza verileri
            string shopName = $"Shop {itemShopId}";
            int shopSales = 0;
            int reviewCount = 0;
            decimal reviewAverage = 5.0m;
            string shopUrl = itemShopId > 0 ? $"https://www.etsy.com/shop/{shopName}" : "";

            var shopObj = (item.TryGetProperty("Shop", out var s1) && s1.ValueKind == JsonValueKind.Object) ? s1
                : ((item.TryGetProperty("shop", out var s2) && s2.ValueKind == JsonValueKind.Object) ? s2 : default);

            if (shopObj.ValueKind == JsonValueKind.Object)
            {
                if (shopObj.TryGetProperty("shop_name", out var sn) && !string.IsNullOrWhiteSpace(sn.GetString()))
                {
                    shopName = sn.GetString()!;
                    shopUrl = $"https://www.etsy.com/shop/{shopName}";
                }
                if (shopObj.TryGetProperty("transaction_sold_count", out var sc))
                {
                    shopSales = sc.GetInt32();
                }
                if (shopObj.TryGetProperty("review_count", out var rc))
                {
                    reviewCount = rc.GetInt32();
                }
                if (shopObj.TryGetProperty("review_average", out var ra))
                {
                    reviewAverage = ra.ValueKind == JsonValueKind.Number ? ra.GetDecimal() : (decimal.TryParse(ra.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var rav) ? rav : 5.0m);
                }
                if (shopObj.TryGetProperty("url", out var su) && !string.IsNullOrWhiteSpace(su.GetString()))
                {
                    shopUrl = su.GetString()!;
                }
            }
            else if (item.TryGetProperty("shop_name", out var itemSn) && !string.IsNullOrWhiteSpace(itemSn.GetString()))
            {
                shopName = itemSn.GetString()!;
                shopUrl = $"https://www.etsy.com/shop/{shopName}";
            }

            // 1. Yapısal SEO Puanı (100 üzerinden)
            int seoScore = 100;
            if (tags.Count < 13) seoScore -= (13 - tags.Count) * 4;
            if (title.Length < 60) seoScore -= 15;
            else if (title.Length < 110) seoScore -= 6;
            if (description.Length < 500) seoScore -= 10;
            if (imageUrls.Count < 5) seoScore -= 10;
            seoScore = Math.Clamp(seoScore, 10, 100);

            // 2. Pazar Fırsat Puanı (Logaritmik talep & rekabet modeli)
            var keywordMatch = !string.IsNullOrWhiteSpace(keyword) && title.Contains(keyword.Trim(), StringComparison.OrdinalIgnoreCase) ? 15m : 5m;
            var favoriteScore = Math.Min(20m, (decimal)Math.Log10(Math.Max(1, favorites) + 1) * 8m);
            var viewScore = Math.Min(15m, (decimal)Math.Log10(Math.Max(1, views) + 1) * 5m);
            var salesScore = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, shopSales) + 1) * 7m);
            var reviewScore = Math.Min(10m, reviewAverage * 2m);
            var marketScore = (int)Math.Round(Math.Clamp(keywordMatch + favoriteScore + viewScore + salesScore + reviewScore + (seoScore * 0.15m), 10m, 99m));

            // 3. EverBee & Toolsy Tarzı Tahmini Aylık Satış, Ciro ve Hız Motoru
            double favSignal = Math.Max(1.0, favorites * 0.18);
            double salesFromFav = favSignal * 0.08;
            double monthlyShopSales = Math.Max(5.0, shopSales / 20.0);
            double shopWeight = Math.Clamp(views > 0 ? (favorites / (double)Math.Max(50, views)) : 0.05, 0.03, 0.20);
            double salesFromShop = monthlyShopSales * shopWeight;
            double reviewSignal = Math.Max(1.0, reviewCount / 24.0) * 8.0;

            double estMonthlySalesRaw = (salesFromFav * 0.45) + (salesFromShop * 0.40) + (reviewSignal * 0.15);
            if (price > 100) estMonthlySalesRaw *= 0.65;
            else if (price < 15 && price > 0) estMonthlySalesRaw *= 1.35;

            int estimatedMonthlySales = (int)Math.Round(Math.Clamp(estMonthlySalesRaw, 1.0, 950.0));
            decimal estimatedMonthlyRevenue = Math.Round(estimatedMonthlySales * price, 2);

            string velocity;
            if (estimatedMonthlySales >= 45 || estimatedMonthlyRevenue >= 1500)
                velocity = "🔥 Çok Hızlı";
            else if (estimatedMonthlySales >= 15 || estimatedMonthlyRevenue >= 500)
                velocity = "⚡ Düzenli";
            else
                velocity = "🐢 Düşük Hacim";

            decimal conversionRate = views > 0 ? Math.Round((decimal)Math.Clamp((estimatedMonthlySales / (double)Math.Max(50, views * 0.25)) * 100.0, 0.8, 6.5), 1) : 2.5m;

            string listingUrl = item.TryGetProperty("url", out var u) && !string.IsNullOrWhiteSpace(u.GetString())
                ? u.GetString()!
                : $"https://www.etsy.com/listing/{listingId}";

            listings.Add(new MarketListingItemDto
            {
                Id = listingId,
                ListingRank = 0,
                Title = title,
                PriceUsd = price,
                Currency = currency,
                ShopName = shopName,
                ShopSales = shopSales,
                ShopUrl = shopUrl,
                ListingUrl = listingUrl,
                Favorites = favorites,
                Views = views,
                SeoScore = seoScore,
                MarketScore = marketScore,
                EstimatedMonthlySales = estimatedMonthlySales,
                EstimatedMonthlyRevenue = estimatedMonthlyRevenue,
                SalesVelocity = velocity,
                ConversionRateEst = conversionRate,
                Tags = tags,
                Materials = materials,
                ImageUrl = primaryImg,
                ImageUrls = imageUrls,
                Description = description,
                ReviewCount = reviewCount,
                ReviewAverage = reviewAverage,
                Quantity = quantity
            });
        }

        var kpis = new MarketSummaryKpisDto
        {
            TotalListings = listings.Count,
            Currency = listings.FirstOrDefault()?.Currency ?? "USD"
        };

        if (listings.Count > 0)
        {
            var validPrices = listings.Where(l => l.PriceUsd > 0).Select(l => l.PriceUsd).ToList();
            if (validPrices.Count > 0)
            {
                kpis.AveragePrice = Math.Round(validPrices.Average(), 2);
                kpis.MinPrice = validPrices.Min();
                kpis.MaxPrice = validPrices.Max();
            }

            kpis.AverageFavorites = Math.Round(listings.Average(l => (double)l.Favorites), 1);
            kpis.AverageViews = Math.Round(listings.Average(l => (double)l.Views), 1);
            kpis.TotalEstimatedMarketRevenue = Math.Round(listings.Sum(l => l.EstimatedMonthlyRevenue), 2);
            kpis.AverageEstimatedMonthlySales = Math.Round(listings.Average(l => (double)l.EstimatedMonthlySales), 1);
            kpis.TopSellerMonthlyRevenue = listings.Max(l => l.EstimatedMonthlyRevenue);

            var topShop = listings
                .Where(l => !string.IsNullOrWhiteSpace(l.ShopName))
                .OrderByDescending(l => l.ShopSales)
                .FirstOrDefault();

            if (topShop != null)
            {
                kpis.TopShopName = topShop.ShopName;
                kpis.TopShopSales = topShop.ShopSales;
            }

            double demandFactor = Math.Min(100.0, kpis.AverageFavorites * 1.5);
            double priceFactor = kpis.AveragePrice >= 15 && kpis.AveragePrice <= 75 ? 90 : 65;
            double competitionFactor = listings.Count >= 30 ? 70 : 85;
            kpis.OpportunityScore = (int)Math.Clamp((demandFactor * 0.45) + (priceFactor * 0.35) + (competitionFactor * 0.20), 20, 98);

            // 13 Altın Etiket Frekans Matrisi (eRank / Marmalead Tag Matrix)
            var tagFreq = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            foreach (var l in listings)
            {
                foreach (var tg in l.Tags)
                {
                    var cleaned = tg?.Trim() ?? "";
                    if (cleaned.Length >= 2)
                    {
                        tagFreq[cleaned] = tagFreq.TryGetValue(cleaned, out int cnt) ? cnt + 1 : 1;
                    }
                }
            }

            kpis.TopTags = tagFreq
                .OrderByDescending(x => x.Value)
                .Take(50)
                .Select(x =>
                {
                    var tag = x.Key;
                    var words = tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length;
                    var pct = listings.Count > 0 ? Math.Round((x.Value / (double)listings.Count) * 100.0, 1) : 0;
                    string comp = pct >= 50 ? "Yüksek" : (pct >= 20 ? "Orta" : "Düşük");
                    return new MarketTagFrequencyDto
                    {
                        Tag = tag,
                        Count = x.Value,
                        UsagePercentage = pct,
                        WordCount = words,
                        CharLength = tag.Length,
                        CompetitionLevel = comp
                    };
                })
                .ToList();
        }

        IEnumerable<MarketListingItemDto> sorted = sortBy switch
        {
            "estimated_revenue" => listings.OrderByDescending(x => x.EstimatedMonthlyRevenue),
            "estimated_sales" => listings.OrderByDescending(x => x.EstimatedMonthlySales),
            "seo_score" => listings.OrderByDescending(x => x.SeoScore),
            "favorites" => listings.OrderByDescending(x => x.Favorites),
            "views" => listings.OrderByDescending(x => x.Views),
            "shop_sales" => listings.OrderByDescending(x => x.ShopSales),
            "price_asc" => listings.OrderBy(x => x.PriceUsd),
            "price_desc" => listings.OrderByDescending(x => x.PriceUsd),
            _ => listings.OrderByDescending(x => x.MarketScore)
        };

        var finalList = sorted.ToList();
        for (int i = 0; i < finalList.Count; i++)
        {
            finalList[i].ListingRank = i + 1;
        }

        return Ok(new MarketSearchResponse
        {
            Keyword = keyword,
            Total = Math.Max(totalCount, finalList.Count),
            Kpis = kpis,
            Listings = finalList
        });
    }
}
