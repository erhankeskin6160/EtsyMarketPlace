namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;
using EtsyMarketPlace.Application.SeasonalTrends;

public sealed class MarketResearchService : IMarketResearchService
{
    private readonly IEtsyMarketApiClient _apiClient;
    private readonly IGeminiListingApiClient? _geminiClient;

    public MarketResearchService(
        IEtsyMarketApiClient apiClient,
        IGeminiListingApiClient? geminiClient = null)
    {
        _apiClient = apiClient ?? throw new ArgumentNullException(nameof(apiClient));
        _geminiClient = geminiClient;
    }

    // Aynı aramanın kısa sürede yinelenmesi Etsy API kotasını yakmasın diye küçük bir
    // işlem-içi önbellek: 10 dakika TTL ve basit üst sınır koruması.
    private static readonly ConcurrentDictionary<string, (DateTime ExpiresAtUtc, MarketSearchResponse Response)> SearchCache = new();
    private static readonly TimeSpan SearchCacheTtl = TimeSpan.FromMinutes(10);
    private const int SearchCacheMaxEntries = 500;

    public async Task<MarketSearchResponse> SearchMarketAsync(
        MarketSearchQuery query,
        CancellationToken cancellationToken = default)
    {
        if (query == null || string.IsNullOrWhiteSpace(query.Keyword))
        {
            return new MarketSearchResponse();
        }

        var keyword = query.Keyword.Trim();
        var clampedLimit = Math.Clamp(query.Limit, 1, 100);

        var cacheKey = string.Concat(query.ShopId, "|", keyword.ToLowerInvariant(), "|", clampedLimit, "|", query.SortBy);
        if (SearchCache.TryGetValue(cacheKey, out var cachedEntry) && cachedEntry.ExpiresAtUtc > DateTime.UtcNow)
        {
            return cachedEntry.Response;
        }

        string body;
        try
        {
            body = await _apiClient.SearchActiveListingsRawJsonAsync(query.ShopId, keyword, clampedLimit, cancellationToken);
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException($"Pazar arama API hatası: {ex.Message}", ex);
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
            return new MarketSearchResponse
            {
                Keyword = keyword,
                Total = 0,
                Kpis = new MarketSummaryKpisDto(),
                Listings = []
            };
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
                decimal rawAmount = 0;
                if (pObj.TryGetProperty("amount", out var aProp) && pObj.TryGetProperty("divisor", out var divProp))
                {
                    var divisor = divProp.GetInt32();
                    if (divisor > 0) rawAmount = aProp.GetInt64() / (decimal)divisor;
                }
                if (pObj.TryGetProperty("currency_code", out var cProp)) currency = cProp.GetString() ?? "USD";

                // Etsy fiyatları satıcının para biriminde döner (USD, EUR, TRY, IDR...);
                // KPI ve tahmin motoru USD ile çalıştığı için fiyat burada normalize edilir.
                price = MarketCurrencyConverter.TryConvertToUsd(rawAmount, currency) ?? 0;
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

            bool hasFreeShipping = false;
            if (item.TryGetProperty("has_free_shipping", out var hfs) && (hfs.ValueKind == JsonValueKind.True || hfs.ValueKind == JsonValueKind.False))
            {
                hasFreeShipping = hfs.GetBoolean();
            }
            else if (price >= 35m || tags.Any(tg => tg.Contains("free shipping", StringComparison.OrdinalIgnoreCase)))
            {
                hasFreeShipping = true;
            }

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
                Tags = tags,
                Materials = materials,
                ImageUrl = primaryImg,
                ImageUrls = imageUrls,
                Description = description,
                ReviewCount = reviewCount,
                ReviewAverage = reviewAverage,
                Quantity = quantity,
                HasFreeShipping = hasFreeShipping
            });
        }

        // Görsel, etiket ve mağaza bilgisi aktif arama yanıtında eksik gelebilir; Etsy'nin toplu ilan ucuyla
        // (includes=Images,Shop) en iyi çaba prensibiyle zenginleştirilir.
        await EnrichWithBatchDetailsAsync(query.ShopId, listings, cancellationToken);

        // Zenginleştirilmiş ImageUrls, Tags, ShopSales ve ReviewCount üzerinden
        // SEO Skoru, Pazar Skoru, Tahmini Aylık Satış/Ciro ve Fırsat Bayraklarını hesapla.
        foreach (var listing in listings)
        {
            RecalculateListingMetrics(listing, keyword);
        }

        var kpis = new MarketSummaryKpisDto
        {
            TotalListings = listings.Count,
            // Fiyatlar USD'ye normalize edildiği için özet para birimi her zaman USD'dir.
            Currency = "USD"
        };

        if (listings.Count > 0)
        {
            var validPrices = listings.Where(l => l.PriceUsd > 0).Select(l => l.PriceUsd).OrderBy(p => p).ToList();
            if (validPrices.Count > 0)
            {
                kpis.AveragePrice = Math.Round(validPrices.Average(), 2);
                kpis.MinPrice = validPrices[0];
                kpis.MaxPrice = validPrices[^1];
                int mid = validPrices.Count / 2;
                kpis.MedianPrice = validPrices.Count % 2 == 1
                    ? validPrices[mid]
                    : Math.Round((validPrices[mid - 1] + validPrices[mid]) / 2m, 2);
            }

            kpis.AverageFavorites = Math.Round(listings.Average(l => (double)l.Favorites), 1);
            kpis.AverageViews = Math.Round(listings.Average(l => (double)l.Views), 1);
            kpis.TotalEstimatedMarketRevenue = Math.Round(listings.Sum(l => l.EstimatedMonthlyRevenue), 2);
            kpis.AverageEstimatedMonthlySales = Math.Round(listings.Average(l => (double)l.EstimatedMonthlySales), 1);
            kpis.TopSellerMonthlyRevenue = listings.Max(l => l.EstimatedMonthlyRevenue);
            kpis.FreeShippingRatioPct = Math.Round((listings.Count(l => l.HasFreeShipping) / (double)listings.Count) * 100.0, 1);
            kpis.BestsellerCount = listings.Count(l => l.IsBestseller);
            kpis.HighOpportunityCount = listings.Count(l => l.IsOpportunityListing);

            var shopGroups = listings
                .Where(l => !string.IsNullOrWhiteSpace(l.ShopName))
                .GroupBy(l => l.ShopName, StringComparer.OrdinalIgnoreCase)
                .ToList();

            kpis.UniqueShopCount = shopGroups.Count;
            if (shopGroups.Count > 0)
            {
                var maxShopListingCount = shopGroups.Max(g => g.Count());
                kpis.TopShopConcentrationPct = Math.Round((maxShopListingCount / (double)listings.Count) * 100.0, 1);
            }

            var topShop = listings
                .Where(l => !string.IsNullOrWhiteSpace(l.ShopName))
                .OrderByDescending(l => l.ShopSales)
                .ThenByDescending(l => l.EstimatedMonthlyRevenue)
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

        IEnumerable<MarketListingItemDto> sorted = query.SortBy switch
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

        var response = new MarketSearchResponse
        {
            Keyword = keyword,
            Total = Math.Max(totalCount, finalList.Count),
            Kpis = kpis,
            Listings = finalList
        };

        StoreInCache(cacheKey, response);
        return response;
    }

    private static void StoreInCache(string cacheKey, MarketSearchResponse response)
    {
        if (SearchCache.Count >= SearchCacheMaxEntries)
        {
            // Basit üst sınır koruması: tablo şişerse önbelleği tazele.
            SearchCache.Clear();
        }

        SearchCache[cacheKey] = (DateTime.UtcNow.Add(SearchCacheTtl), response);
    }

    /// <summary>
    /// Arama sonuçlarını Etsy toplu ilan ucuyla zenginleştirir (görseller + mağaza adı/satış bilgisi).
    /// En iyi çaba: zenginleştirme başarısız olursa arama sonucu zenginleştirilmeden döner.
    /// </summary>
    private async Task EnrichWithBatchDetailsAsync(string shopId, List<MarketListingItemDto> listings, CancellationToken cancellationToken)
    {
        var targetIds = listings.Where(l => l.Id > 0).Select(l => l.Id).Distinct().ToList();
        if (targetIds.Count == 0)
        {
            return;
        }

        try
        {
            foreach (var chunk in targetIds.Chunk(100))
            {
                var body = await _apiClient.GetListingsBatchRawJsonAsync(shopId, chunk, cancellationToken);
                ApplyBatchDetails(body, listings);
            }
        }
        catch
        {
            // En iyi çaba: zenginleştirme verisi alınamazsa arama sonucu zenginleştirilmeden döner.
        }
    }

    /// <summary>Toplu ilan ucu yanıtını arama sonuçlarına işler (görsel, etiket, mağaza, ilan linki).</summary>
    private static void ApplyBatchDetails(string body, List<MarketListingItemDto> listings)
    {
        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return;
        }

        var byId = listings.ToDictionary(l => l.Id);
        foreach (var item in results.EnumerateArray())
        {
            long listingId = item.TryGetProperty("listing_id", out var lid) ? lid.GetInt64() : 0;
            if (listingId == 0 || !byId.TryGetValue(listingId, out var target))
            {
                continue;
            }

            if (item.TryGetProperty("images", out var imgs) && imgs.ValueKind == JsonValueKind.Array)
            {
                var urls = new List<string>();
                foreach (var img in imgs.EnumerateArray())
                {
                    string? url = null;
                    if (img.TryGetProperty("url_570xN", out var u570)) url = u570.GetString();
                    else if (img.TryGetProperty("url_fullxfull", out var uFull)) url = uFull.GetString();
                    if (!string.IsNullOrWhiteSpace(url)) urls.Add(url.Trim());
                }

                if (urls.Count > 0)
                {
                    target.ImageUrls = urls;
                    target.ImageUrl = urls[0];
                }
            }

            if (item.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
            {
                var tags = new List<string>();
                foreach (var tg in tagsArr.EnumerateArray())
                {
                    var str = tg.GetString();
                    if (!string.IsNullOrWhiteSpace(str)) tags.Add(str.Trim());
                }

                if (tags.Count > 0)
                {
                    target.Tags = tags;
                }
            }

            if (item.TryGetProperty("shop", out var shopObj) && shopObj.ValueKind == JsonValueKind.Object)
            {
                if (shopObj.TryGetProperty("shop_name", out var sn) && !string.IsNullOrWhiteSpace(sn.GetString()))
                {
                    target.ShopName = sn.GetString()!;
                    target.ShopUrl = $"https://www.etsy.com/shop/{target.ShopName}";
                }
                if (shopObj.TryGetProperty("url", out var su) && !string.IsNullOrWhiteSpace(su.GetString()))
                {
                    target.ShopUrl = su.GetString()!;
                }
                if (shopObj.TryGetProperty("transaction_sold_count", out var sc) && sc.ValueKind == JsonValueKind.Number)
                {
                    target.ShopSales = sc.GetInt32();
                }
                if (shopObj.TryGetProperty("review_count", out var rc) && rc.ValueKind == JsonValueKind.Number)
                {
                    target.ReviewCount = rc.GetInt32();
                }
                if (shopObj.TryGetProperty("review_average", out var ra))
                {
                    target.ReviewAverage = ra.ValueKind == JsonValueKind.Number
                        ? ra.GetDecimal()
                        : (decimal.TryParse(ra.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var rav) ? rav : target.ReviewAverage);
                }
            }

            if (item.TryGetProperty("url", out var u) && !string.IsNullOrWhiteSpace(u.GetString()))
            {
                target.ListingUrl = u.GetString()!;
            }
        }
    }

    private static void RecalculateListingMetrics(MarketListingItemDto listing, string keyword)
    {
        // 1. Yapısal SEO Puanı (100 üzerinden)
        int seoScore = 100;
        if (listing.Tags.Count < 13) seoScore -= (13 - listing.Tags.Count) * 4;
        if (listing.Title.Length < 60) seoScore -= 15;
        else if (listing.Title.Length < 110) seoScore -= 6;
        if (listing.Description.Length < 500) seoScore -= 10;
        if (listing.ImageUrls.Count < 5) seoScore -= 10;
        seoScore = Math.Clamp(seoScore, 10, 100);

        // 2. Pazar Fırsat Puanı (Logaritmik talep & rekabet modeli)
        var keywordMatch = listing.Title.Contains(keyword, StringComparison.OrdinalIgnoreCase) ? 15m : 5m;
        var favoriteScore = Math.Min(20m, (decimal)Math.Log10(Math.Max(1, listing.Favorites) + 1) * 8m);
        var viewScore = Math.Min(15m, (decimal)Math.Log10(Math.Max(1, listing.Views) + 1) * 5m);
        var salesScore = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, listing.ShopSales) + 1) * 7m);
        var reviewScore = Math.Min(10m, listing.ReviewAverage * 2m);
        var marketScore = (int)Math.Round(Math.Clamp(keywordMatch + favoriteScore + viewScore + salesScore + reviewScore + (seoScore * 0.15m), 10m, 99m));

        // 3. EverBee & Toolsy Tarzı Tahmini Aylık Satış, Ciro ve Hız Motoru
        double favSignal = Math.Max(1.0, listing.Favorites * 0.18);
        double salesFromFav = favSignal * 0.08;
        double monthlyShopSales = Math.Max(5.0, listing.ShopSales / 20.0);
        double shopWeight = Math.Clamp(listing.Views > 0 ? (listing.Favorites / (double)Math.Max(50, listing.Views)) : 0.05, 0.03, 0.20);
        double salesFromShop = monthlyShopSales * shopWeight;
        double reviewSignal = Math.Max(1.0, listing.ReviewCount / 24.0) * 8.0;

        double estMonthlySalesRaw = (salesFromFav * 0.45) + (salesFromShop * 0.40) + (reviewSignal * 0.15);
        if (listing.PriceUsd > 100) estMonthlySalesRaw *= 0.65;
        else if (listing.PriceUsd < 15 && listing.PriceUsd > 0) estMonthlySalesRaw *= 1.35;

        int estimatedMonthlySales = (int)Math.Round(Math.Clamp(estMonthlySalesRaw, 1.0, 950.0));
        decimal estimatedMonthlyRevenue = Math.Round(estimatedMonthlySales * listing.PriceUsd, 2);

        string velocity;
        if (estimatedMonthlySales >= 45 || estimatedMonthlyRevenue >= 1500)
            velocity = "🔥 Çok Hızlı";
        else if (estimatedMonthlySales >= 15 || estimatedMonthlyRevenue >= 500)
            velocity = "⚡ Düzenli";
        else
            velocity = "🐢 Düşük Hacim";

        decimal conversionRate = listing.Views > 0
            ? Math.Round((decimal)Math.Clamp((estimatedMonthlySales / (double)Math.Max(50, listing.Views * 0.25)) * 100.0, 0.8, 6.5), 1)
            : 2.5m;

        listing.SeoScore = seoScore;
        listing.MarketScore = marketScore;
        listing.EstimatedMonthlySales = estimatedMonthlySales;
        listing.EstimatedMonthlyRevenue = estimatedMonthlyRevenue;
        listing.SalesVelocity = velocity;
        listing.ConversionRateEst = conversionRate;
        listing.IsBestseller = estimatedMonthlySales >= 35 || listing.Favorites >= 200 || estimatedMonthlyRevenue >= 1200m;
        if (!listing.HasFreeShipping && (listing.PriceUsd >= 35m || listing.Tags.Any(tg => tg.Contains("free shipping", StringComparison.OrdinalIgnoreCase))))
        {
            listing.HasFreeShipping = true;
        }

        // Fırsat Ürünü: Yüksek talep/satış olan ancak rakibin SEO'sunu zayıf bıraktığı (< 80) ürünler
        listing.IsOpportunityListing = (estimatedMonthlySales >= 10 || listing.Favorites >= 40 || listing.ShopSales >= 200) && seoScore < 80;
    }

    public async Task<MarketAiReportResultDto> GenerateAiMarketReportAsync(
        MarketAiReportCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command == null || string.IsNullOrWhiteSpace(command.Keyword))
        {
            throw new ArgumentException("Pazar analizi için anahtar kelime gereklidir.", nameof(command));
        }

        var keyword = command.Keyword.Trim();
        var topTitles = command.TopCompetitorTitles?.Where(t => !string.IsNullOrWhiteSpace(t)).Take(6).ToList() ?? [];
        var topTags = command.TopTags?.Where(t => !string.IsNullOrWhiteSpace(t)).Take(15).ToList() ?? [];
        var apiKey = command.ApiKey?.Trim() ?? string.Empty;
        var isOffline = string.Equals(command.Provider, "Offline", StringComparison.OrdinalIgnoreCase);

        if (!isOffline && !string.IsNullOrWhiteSpace(apiKey) && _geminiClient != null)
        {
            try
            {
                var rawModel = !string.IsNullOrWhiteSpace(command.Model) ? command.Model! : "gemini-2.5-flash";
                var geminiModel = EtsyAiModelNormalizer.NormalizeGeminiTextModel(rawModel);

                var systemInstruction =
                    "Sen kıdemli bir Etsy Pazar Araştırma, SEO ve Ürün Stratejisi uzmanısın. " +
                    "Sana verilen gerçek Etsy pazar verilerini analiz ederek Türkçe, uygulanabilir, somut bir niş strateji raporu üret. " +
                    "Yanıtını SADECE geçerli bir JSON nesnesi olarak döndür: " +
                    "{\"market_overview\": \"...\", \"pricing_strategy\": \"...\", \"differentiation_ideas\": [\"...\"], \"recommended_keywords\": [\"...\"], \"action_items\": [\"...\"]}";

                var promptBuilder = new StringBuilder();
                promptBuilder.AppendLine($"Hedef Anahtar Kelime: '{keyword}'");
                promptBuilder.AppendLine($"İncelenen İlan Sayısı: {command.TotalListings} (Farklı Mağaza Sayısı: {command.UniqueShopCount})");
                promptBuilder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Fiyat Koridoru: Min ${command.MinPrice:0.00} | Medyan ${command.MedianPrice:0.00} | Ortalama ${command.AveragePrice:0.00} | Maks ${command.MaxPrice:0.00}"));
                promptBuilder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Ortalama Favori: {command.AverageFavorites:0.0} | Ortalama Görüntülenme: {command.AverageViews:0.0} | Fırsat Skoru: {command.OpportunityScore}/100"));
                promptBuilder.AppendLine(string.Create(CultureInfo.InvariantCulture, $"Tahmini Toplam Aylık Pazar Cirosu: ${command.TotalEstimatedMarketRevenue:0.00} | Lider Mağaza: {command.TopShopName} ({command.TopShopSales} satış)"));

                if (topTitles.Count > 0)
                {
                    promptBuilder.AppendLine("En Başarılı Rakip Başlıkları:");
                    foreach (var title in topTitles)
                    {
                        promptBuilder.AppendLine($"- {title}");
                    }
                }

                if (topTags.Count > 0)
                {
                    promptBuilder.AppendLine($"Pazardaki En Popüler Etiketler: {string.Join(", ", topTags)}");
                }

                var (success, content, _) = await _geminiClient.GenerateOptimizationJsonAsync(
                    systemInstruction,
                    promptBuilder.ToString(),
                    geminiModel,
                    apiKey,
                    cancellationToken);

                if (success && !string.IsNullOrWhiteSpace(content))
                {
                    using var parsed = JsonDocument.Parse(content);
                    var root = parsed.RootElement;

                    var overview = root.TryGetProperty("market_overview", out var ov) ? (ov.GetString() ?? "") : "";
                    var pricing = root.TryGetProperty("pricing_strategy", out var pr) ? (pr.GetString() ?? "") : "";
                    var diffs = ReadStringArray(root, "differentiation_ideas");
                    var recKws = ReadStringArray(root, "recommended_keywords");
                    var actions = ReadStringArray(root, "action_items");

                    if (!string.IsNullOrWhiteSpace(overview) || diffs.Count > 0)
                    {
                        var dto = new MarketAiReportResultDto
                        {
                            Keyword = keyword,
                            UsedAi = true,
                            Provider = "Gemini",
                            Model = geminiModel,
                            MarketOverview = overview,
                            PricingStrategy = pricing,
                            DifferentiationIdeas = diffs,
                            RecommendedKeywords = recKws,
                            ActionItems = actions
                        };
                        dto.ReportHtml = BuildReportHtml(dto);
                        return dto;
                    }
                }
            }
            catch
            {
                // AI çağrısı başarısız olursa deterministik gerçek veri analizine düşer.
            }
        }

        return BuildDeterministicReport(command, keyword, topTitles, topTags);
    }

    private static List<string> ReadStringArray(JsonElement root, string propName)
    {
        var list = new List<string>();
        if (root.TryGetProperty(propName, out var arr) && arr.ValueKind == JsonValueKind.Array)
        {
            foreach (var item in arr.EnumerateArray())
            {
                var str = item.GetString();
                if (!string.IsNullOrWhiteSpace(str))
                {
                    list.Add(str.Trim());
                }
            }
        }
        return list;
    }

    private static MarketAiReportResultDto BuildDeterministicReport(
        MarketAiReportCommand cmd,
        string keyword,
        List<string> topTitles,
        List<string> topTags)
    {
        var avgPrice = cmd.AveragePrice > 0 ? cmd.AveragePrice : 29.90m;
        var minPrice = cmd.MinPrice > 0 ? cmd.MinPrice : Math.Round(avgPrice * 0.55m, 2);
        var maxPrice = cmd.MaxPrice > 0 ? cmd.MaxPrice : Math.Round(avgPrice * 1.85m, 2);
        var medianPrice = cmd.MedianPrice > 0 ? cmd.MedianPrice : Math.Round(avgPrice * 0.92m, 2);
        var sweetSpotLow = Math.Round(medianPrice * 0.88m, 2);
        var sweetSpotHigh = Math.Round(avgPrice * 1.12m, 2);

        string competitionState = cmd.UniqueShopCount > 0 && cmd.TotalListings > 0
            ? (cmd.UniqueShopCount >= cmd.TotalListings * 0.7
                ? $"İncelenen {cmd.TotalListings} ilanda {cmd.UniqueShopCount} farklı mağaza yer alıyor (Lider mağaza: '{cmd.TopShopName}' - {cmd.TopShopSales:N0} satış); pazarda tekelleşme yok ve yeni mağazalar için giriş fırsatı yüksek."
                : $"İncelenen {cmd.TotalListings} ilan {cmd.UniqueShopCount} mağazada toplanmış durumda; lider mağaza '{cmd.TopShopName}' ({cmd.TopShopSales:N0} satış) nişi domine ediyor.")
            : $"Lider mağaza '{cmd.TopShopName}' ({cmd.TopShopSales:N0} satış) yüksek sosyal kanıtla öne çıkıyor.";

        var overview =
            $"'{keyword}' nişinde fırsat skoru {cmd.OpportunityScore}/100 ve tahmini aylık pazar hacmi ${cmd.TotalEstimatedMarketRevenue:N0} seviyesindedir. " +
            $"İlan başına ortalama {cmd.AverageFavorites:0.0} favori ve {cmd.AverageViews:0.0} görüntülenme düşmektedir. {competitionState}";

        var pricing =
            $"Pazar fiyatları ${minPrice:0.00} ile ${maxPrice:0.00} arasında değişiyor (Medyan: ${medianPrice:0.00}, Ortalama: ${avgPrice:0.00}). " +
            $"Pazara ilk girişte dönüşüm hızını yakalamak için ${sweetSpotLow:0.00} – ${sweetSpotHigh:0.00} bandında konumlanmanız, $35+ ücretsiz kargo psikolojik eşiğini ise 'Bundle / İkili Set' varyasyonuyla aşmanız önerilir.";

        var diffs = new List<string>
        {
            $"Görsel Farklılaştırma: Rakiplerin ilk görsellerine kıyasla ölçek belirten (elde/masada kullanım) sıcak ışıklandırmalı kapak fotoğrafı ve 5–10 sn'lik ürün videosu ekleyin.",
            $"Varyasyon & Kişiselleştirme: '{keyword}' aramalarında hediye/kişiselleştirme (Custom Color / Size / Gift Box) seçeneği sunarak ortalama sepet tutarını %25 artırın.",
            $"SEO Zafiyeti Avı: Pazarda yüksek favori alıp 13 etiketi doldurmayan veya başlığı 90 karakterin altında kalan rakiplerin eksik long-tail kelimelerini ilk 55 karakterde hedefleyin."
        };

        var recommendedKws = topTags.Count > 0
            ? topTags.Take(8).Select(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length == 1 ? $"{keyword} {t}" : t).Distinct(StringComparer.OrdinalIgnoreCase).Take(6).ToList()
            : [$"custom {keyword}", $"{keyword} gift", $"handmade {keyword}", $"unique {keyword} decor", $"personalized {keyword}"];

        var actions = new List<string>
        {
            $"Başlığın ilk 55 karakterinde '{keyword}' ana kelimesini ve en yüksek hacimli tamamlayıcı etiketi ({recommendedKws.FirstOrDefault() ?? keyword}) geçirin.",
            $"13 etiketin tamamını doldurun; tek kelimelik genel etiketler yerine 2-3 kelimelik (15-20 karakter) long-tail ifadeler kullanın.",
            $"Giriş fiyatınızı ${sweetSpotLow:0.00} seviyesinde başlatıp ilk 5 satış ve yorumdan sonra ${avgPrice:0.00} ortalamasına çekin."
        };

        var dto = new MarketAiReportResultDto
        {
            Keyword = keyword,
            UsedAi = false,
            Provider = "SmartMarketEngine",
            Model = "EtsyMarketAnalytics-v3",
            MarketOverview = overview,
            PricingStrategy = pricing,
            DifferentiationIdeas = diffs,
            RecommendedKeywords = recommendedKws,
            ActionItems = actions
        };
        dto.ReportHtml = BuildReportHtml(dto);
        return dto;
    }

    private static string BuildReportHtml(MarketAiReportResultDto r)
    {
        var sb = new StringBuilder();
        var badge = r.UsedAi ? $"🤖 {r.Provider} ({r.Model})" : "📊 Akıllı Pazar Analiz Motoru (Canlı Veri)";
        sb.Append($"<div style='margin-bottom:8px;'><span style='font-size:11px;padding:2px 8px;border-radius:999px;background:rgba(139,92,246,0.2);color:#c4b5fd;'>{badge}</span></div>");
        sb.Append($"<p style='margin:0 0 8px 0;'><strong>1. Pazar & Rekabet Özeti:</strong> {System.Net.WebUtility.HtmlEncode(r.MarketOverview)}</p>");
        sb.Append($"<p style='margin:0 0 8px 0;'><strong>2. Fiyatlandırma Stratejisi:</strong> {System.Net.WebUtility.HtmlEncode(r.PricingStrategy)}</p>");

        if (r.DifferentiationIdeas.Count > 0)
        {
            sb.Append("<p style='margin:0 0 4px 0;'><strong>3. Farklılaştırma & Ürün Fırsatları:</strong></p><ul style='margin:0 0 8px 18px;padding:0;'>");
            foreach (var d in r.DifferentiationIdeas)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(d)}</li>");
            }
            sb.Append("</ul>");
        }

        if (r.RecommendedKeywords.Count > 0)
        {
            sb.Append("<p style='margin:0 0 6px 0;'><strong>4. Önerilen Long-Tail Anahtar Kelimeler:</strong> " +
                      string.Join(", ", r.RecommendedKeywords.Select(k => $"<code>{System.Net.WebUtility.HtmlEncode(k)}</code>")) +
                      "</p>");
        }

        if (r.ActionItems.Count > 0)
        {
            sb.Append("<p style='margin:0 0 4px 0;'><strong>5. Hemen Uygulanabilir Eylem Planı:</strong></p><ul style='margin:0 0 4px 18px;padding:0;'>");
            foreach (var a in r.ActionItems)
            {
                sb.Append($"<li>{System.Net.WebUtility.HtmlEncode(a)}</li>");
            }
            sb.Append("</ul>");
        }

        return sb.ToString();
    }

    private static readonly HashSet<string> CompetitorStopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "for", "with", "from", "your", "you", "this", "that", "a", "an", "of", "to", "in", "on", "or",
        "ve", "ile", "icin", "bir", "bu", "da", "de", "veya"
    };

    public IReadOnlyList<SeasonalTrendItem> GetSeasonalTrends(DateTime? referenceDate = null)
    {
        var trendService = new SeasonalTrendService();
        return trendService.GetActiveSeasonalTrends(referenceDate);
    }

    public async Task<CompetitorShopAnalysisDto> AnalyzeCompetitorShopAsync(
        string shopNameOrUrl,
        int limit = 50,
        string authShopId = "53236321",
        CancellationToken cancellationToken = default)
    {
        var clean = ExtractCompetitorShopIdentifier(shopNameOrUrl);
        if (string.IsNullOrWhiteSpace(clean))
        {
            throw new ArgumentException("Lütfen geçerli bir rakip mağaza adı veya Etsy mağaza linki girin.");
        }

        int clampedLimit = Math.Clamp(limit, 1, 100);
        CompetitorShopProfileDto? profile = null;

        // 1. Mağaza profili sorgulama (ID veya İsim üzerinden)
        if (long.TryParse(clean, out var parsedShopId) && parsedShopId > 0)
        {
            try
            {
                var rawShop = await _apiClient.GetShopByIdRawJsonAsync(authShopId, parsedShopId, cancellationToken);
                profile = ParseShopProfileFromJson(rawShop, parsedShopId, clean);
            }
            catch { }
        }
        else
        {
            try
            {
                var rawSearch = await _apiClient.GetShopByNameRawJsonAsync(authShopId, clean, cancellationToken);
                profile = ParseShopProfileFromJson(rawSearch, 0, clean);
            }
            catch { }
        }

        var rawListings = new List<MarketListingItemDto>();

        // 2. Mağaza bulunduysa doğrudan mağazanın aktif ürünlerini çek
        if (profile != null && profile.ShopId > 0)
        {
            try
            {
                var rawListingsJson = await _apiClient.GetShopActiveListingsRawJsonAsync(authShopId, profile.ShopId, clampedLimit, cancellationToken);
                rawListings = ParseShopListingsJson(rawListingsJson, profile);
            }
            catch { }
        }

        // 3. Eğer mağaza adı bulunamadıysa veya aktif ürün listesi boş döndüyse, Pazar Arama üzerinden eşleşen mağazayı yakala
        if (rawListings.Count == 0)
        {
            var fallbackSearch = await SearchMarketAsync(new MarketSearchQuery(clean, clampedLimit, "market_score", authShopId), cancellationToken);
            if (fallbackSearch.Listings.Count > 0)
            {
                var matchedGroup = fallbackSearch.Listings
                    .GroupBy(l => l.ShopName, StringComparer.OrdinalIgnoreCase)
                    .OrderByDescending(g => g.Key.Equals(clean, StringComparison.OrdinalIgnoreCase) ? 1000 : g.Count())
                    .First();

                var firstItem = matchedGroup.First();
                profile ??= new CompetitorShopProfileDto
                {
                    ShopId = 0,
                    ShopName = !string.IsNullOrWhiteSpace(firstItem.ShopName) ? firstItem.ShopName : clean,
                    ShopUrl = !string.IsNullOrWhiteSpace(firstItem.ShopUrl) ? firstItem.ShopUrl : $"https://www.etsy.com/shop/{clean}",
                    Title = $"{firstItem.ShopName} Etsy Store",
                    TotalSales = firstItem.ShopSales,
                    ReviewCount = firstItem.ReviewCount,
                    ReviewAverage = firstItem.ReviewAverage > 0 ? firstItem.ReviewAverage : 4.9m,
                    ActiveListingCount = matchedGroup.Count()
                };

                rawListings = matchedGroup.Key.Equals(clean, StringComparison.OrdinalIgnoreCase)
                    ? matchedGroup.ToList()
                    : fallbackSearch.Listings;
            }
        }

        profile ??= new CompetitorShopProfileDto
        {
            ShopName = clean,
            ShopUrl = $"https://www.etsy.com/shop/{clean}",
            Title = clean
        };

        if (profile.ActiveListingCount <= 0)
        {
            profile.ActiveListingCount = rawListings.Count;
        }

        // 4. Fiyat, Günlük Satış Hızı ve Aylık Ciro Hesaplamaları
        var priced = rawListings.Where(l => l.PriceUsd > 0).Select(l => l.PriceUsd).OrderBy(p => p).ToList();
        decimal minPrice = priced.Count > 0 ? priced[0] : 0m;
        decimal maxPrice = priced.Count > 0 ? priced[^1] : 0m;
        decimal avgPrice = priced.Count > 0 ? Math.Round(priced.Average(), 2) : 0m;
        decimal medianPrice = priced.Count > 0
            ? (priced.Count % 2 == 0 ? Math.Round((priced[priced.Count / 2 - 1] + priced[priced.Count / 2]) / 2m, 2) : priced[priced.Count / 2])
            : 0m;

        decimal dailyVelocity = CalculateCompetitorDailySalesVelocity(profile, rawListings);
        decimal monthlyTurnover = Math.Round(dailyVelocity * 30m * (avgPrice > 0 ? avgPrice : 25m), 2);
        profile.DailySalesEstimate = dailyVelocity;
        profile.MonthlyRevenueEstimate = monthlyTurnover;

        // 5. Ürün Bazlı EverBee Satış/Ciro & eRank LQS Hesaplaması
        decimal totalWeight = rawListings.Sum(l => (l.Favorites * 2.5m) + (l.Views * 0.08m) + 3m);
        decimal shopMonthlySales = Math.Max(5m, dailyVelocity * 30m);

        var competitorListings = new List<CompetitorListingDto>(rawListings.Count);
        for (int i = 0; i < rawListings.Count; i++)
        {
            var item = rawListings[i];
            decimal itemWeight = (item.Favorites * 2.5m) + (item.Views * 0.08m) + 3m;
            decimal share = totalWeight > 0 ? itemWeight / totalWeight : (rawListings.Count > 0 ? 1m / rawListings.Count : 0m);
            int estMonthlySales = item.PriceUsd > 0
                ? Math.Clamp((int)Math.Round(shopMonthlySales * share), 0, (int)Math.Ceiling(shopMonthlySales))
                : 0;
            decimal estMonthlyRev = Math.Round(estMonthlySales * item.PriceUsd, 2);

            string badge = estMonthlySales >= 15 || item.Favorites >= 150
                ? "🔥 Best Seller"
                : (estMonthlySales >= 5 || item.Favorites >= 30 ? "⭐ Hot Item" : "");

            var (grade, lqsScore, explanation) = CalculateCompetitorLqs(item);
            int multiWordTags = item.Tags.Count(t => t.Trim().Contains(' '));

            competitorListings.Add(new CompetitorListingDto
            {
                Id = item.Id,
                Rank = i + 1,
                Title = item.Title,
                PriceUsd = item.PriceUsd,
                Currency = "USD",
                ListingUrl = item.ListingUrl,
                ImageUrl = item.ImageUrl,
                ImageUrls = item.ImageUrls,
                Favorites = item.Favorites,
                Views = item.Views,
                Quantity = item.Quantity,
                SeoScore = item.SeoScore,
                LqsGrade = grade,
                LqsScore = lqsScore,
                LqsExplanation = explanation,
                EstimatedMonthlySales = estMonthlySales,
                EstimatedMonthlyRevenue = estMonthlyRev,
                SalesBadge = badge,
                TitleCharLength = item.Title?.Length ?? 0,
                MissingTagCount = Math.Max(0, 13 - item.Tags.Count),
                MultiWordTagCount = multiWordTags,
                Tags = item.Tags,
                Materials = item.Materials,
                Description = item.Description
            });
        }

        // En yüksek tahmini aylık ciroya göre sırala
        competitorListings = competitorListings
            .OrderByDescending(l => l.EstimatedMonthlyRevenue)
            .ThenByDescending(l => l.Favorites)
            .Select((l, idx) => { l.Rank = idx + 1; return l; })
            .ToList();

        var topTags = BuildCompetitorFrequency(competitorListings.SelectMany(l => l.Tags), competitorListings.Count, 25);
        var topTerms = BuildCompetitorFrequency(
            competitorListings.SelectMany(l => TokenizeTitleWords(l.Title).Distinct(StringComparer.OrdinalIgnoreCase)),
            competitorListings.Count,
            25);

        decimal avgFav = competitorListings.Count > 0 ? Math.Round((decimal)competitorListings.Average(l => l.Favorites), 1) : 0m;
        decimal avgViews = competitorListings.Count > 0 ? Math.Round((decimal)competitorListings.Average(l => l.Views), 1) : 0m;
        int avgSeo = competitorListings.Count > 0 ? (int)Math.Round(competitorListings.Average(l => l.LqsScore)) : 0;
        int strengthScore = CalculateCompetitorStrength(profile, competitorListings, avgSeo);

        profile.AveragePriceUsd = avgPrice;
        profile.MinPriceUsd = minPrice;
        profile.MaxPriceUsd = maxPrice;
        profile.MedianPriceUsd = medianPrice;
        profile.CompetitorStrengthScore = strengthScore;
        profile.AverageLqsGrade = avgSeo >= 90 ? "A+" : (avgSeo >= 80 ? "A" : (avgSeo >= 68 ? "B" : (avgSeo >= 50 ? "C" : "D")));
        profile.BestsellerCount = competitorListings.Count(l => l.SalesBadge.Contains("Best Seller", StringComparison.OrdinalIgnoreCase));

        return new CompetitorShopAnalysisDto
        {
            Shop = profile,
            Listings = competitorListings,
            TopTags = topTags,
            TopTitleTerms = topTerms,
            MinimumPrice = minPrice,
            MaximumPrice = maxPrice,
            AveragePrice = avgPrice,
            MedianPrice = medianPrice,
            PriceCurrency = "USD",
            AverageFavorites = avgFav,
            AverageViews = avgViews,
            AverageSeoScore = avgSeo,
            CompetitorStrengthScore = strengthScore,
            EstimatedDailySalesVelocity = dailyVelocity,
            EstimatedMonthlyTurnover = monthlyTurnover,
            AiCompetitiveGapInsight = BuildCompetitorGapInsight(profile, competitorListings, avgPrice),
            RetrievedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string ExtractCompetitorShopIdentifier(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        string trimmed = input.Trim();
        var match = Regex.Match(trimmed, @"etsy\.com/(?:[a-z]{2}(?:-[a-z]{2})?/)?shop/([^/?#]+)", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim();
        }
        return trimmed.Trim('/');
    }

    private static CompetitorShopProfileDto? ParseShopProfileFromJson(string rawJson, long fallbackId, string fallbackName)
    {
        if (string.IsNullOrWhiteSpace(rawJson)) return null;
        using var doc = JsonDocument.Parse(rawJson);
        var root = doc.RootElement;
        var shopElem = root;

        if (root.TryGetProperty("results", out var results) && results.ValueKind == JsonValueKind.Array)
        {
            if (results.GetArrayLength() == 0) return null;
            shopElem = results[0];
        }

        long shopId = shopElem.TryGetProperty("shop_id", out var sid) && sid.TryGetInt64(out var idVal) ? idVal : fallbackId;
        if (shopId <= 0) return null;

        string shopName = shopElem.TryGetProperty("shop_name", out var sn) && !string.IsNullOrWhiteSpace(sn.GetString())
            ? sn.GetString()!
            : fallbackName;
        string title = shopElem.TryGetProperty("title", out var tl) && !string.IsNullOrWhiteSpace(tl.GetString())
            ? tl.GetString()!
            : shopName;
        int totalSales = shopElem.TryGetProperty("transaction_sold_count", out var sc) && sc.TryGetInt32(out var scVal) ? scVal : 0;
        int reviewCount = shopElem.TryGetProperty("review_count", out var rc) && rc.TryGetInt32(out var rcVal) ? rcVal : 0;
        decimal reviewAvg = 5.0m;
        if (shopElem.TryGetProperty("review_average", out var ra))
        {
            reviewAvg = ra.ValueKind == JsonValueKind.Number
                ? ra.GetDecimal()
                : (decimal.TryParse(ra.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var parsedRa) ? parsedRa : 5.0m);
        }
        int activeCount = shopElem.TryGetProperty("listing_active_count", out var ac) && ac.TryGetInt32(out var acVal) ? acVal : 0;

        DateTimeOffset? createdDate = null;
        if (shopElem.TryGetProperty("create_date", out var cd) && cd.TryGetInt64(out var epoch) && epoch > 0)
        {
            createdDate = DateTimeOffset.FromUnixTimeSeconds(epoch);
        }

        return new CompetitorShopProfileDto
        {
            ShopId = shopId,
            ShopName = shopName,
            ShopUrl = $"https://www.etsy.com/shop/{shopName}",
            Title = title,
            TotalSales = totalSales,
            ReviewCount = reviewCount,
            ReviewAverage = Math.Round(reviewAvg, 2),
            ActiveListingCount = activeCount,
            CreatedDate = createdDate
        };
    }

    private static List<MarketListingItemDto> ParseShopListingsJson(string rawJson, CompetitorShopProfileDto profile)
    {
        var list = new List<MarketListingItemDto>();
        if (string.IsNullOrWhiteSpace(rawJson)) return list;

        using var doc = JsonDocument.Parse(rawJson);
        if (!doc.RootElement.TryGetProperty("results", out var results) || results.ValueKind != JsonValueKind.Array)
        {
            return list;
        }

        int rank = 1;
        foreach (var item in results.EnumerateArray())
        {
            long listingId = item.TryGetProperty("listing_id", out var lid) && lid.TryGetInt64(out var idVal) ? idVal : 0;
            string title = item.TryGetProperty("title", out var t) ? (t.GetString() ?? "") : "";
            string description = item.TryGetProperty("description", out var d) ? (d.GetString() ?? "") : "";
            int views = item.TryGetProperty("views", out var v) && v.TryGetInt32(out var vVal) ? vVal : 0;
            int favorites = 0;
            if (item.TryGetProperty("num_favorers", out var f))
            {
                favorites = f.ValueKind == JsonValueKind.Number ? f.GetInt32() : (int.TryParse(f.GetString(), out var nf) ? nf : 0);
            }
            int quantity = item.TryGetProperty("quantity", out var q) && q.TryGetInt32(out var qVal) ? qVal : 0;

            decimal priceUsd = 0m;
            if (item.TryGetProperty("price", out var pObj) && pObj.ValueKind == JsonValueKind.Object)
            {
                decimal rawAmount = 0m;
                if (pObj.TryGetProperty("amount", out var aProp) && pObj.TryGetProperty("divisor", out var divProp))
                {
                    int divisor = divProp.GetInt32();
                    if (divisor > 0) rawAmount = aProp.GetInt64() / (decimal)divisor;
                }
                string curr = pObj.TryGetProperty("currency_code", out var cProp) ? (cProp.GetString() ?? "USD") : "USD";
                priceUsd = MarketCurrencyConverter.TryConvertToUsd(rawAmount, curr) ?? rawAmount;
            }

            var tags = new List<string>();
            if (item.TryGetProperty("tags", out var tagsArr) && tagsArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var tg in tagsArr.EnumerateArray())
                {
                    var s = tg.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) tags.Add(s.Trim());
                }
            }

            var materials = new List<string>();
            if (item.TryGetProperty("materials", out var matArr) && matArr.ValueKind == JsonValueKind.Array)
            {
                foreach (var mt in matArr.EnumerateArray())
                {
                    var s = mt.GetString();
                    if (!string.IsNullOrWhiteSpace(s)) materials.Add(s.Trim());
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
                    if (!string.IsNullOrWhiteSpace(imgUrl)) imageUrls.Add(imgUrl.Trim());
                }
            }

            list.Add(new MarketListingItemDto
            {
                Id = listingId,
                ListingRank = rank++,
                Title = title,
                Description = description,
                PriceUsd = Math.Round(priceUsd, 2),
                Currency = "USD",
                ShopName = profile.ShopName,
                ShopSales = profile.TotalSales,
                ShopUrl = profile.ShopUrl,
                ListingUrl = listingId > 0 ? $"https://www.etsy.com/listing/{listingId}" : profile.ShopUrl,
                Favorites = favorites,
                Views = views,
                Quantity = quantity,
                Tags = tags,
                Materials = materials,
                ImageUrl = imageUrls.FirstOrDefault() ?? "",
                ImageUrls = imageUrls,
                ReviewCount = profile.ReviewCount,
                ReviewAverage = profile.ReviewAverage
            });
        }

        return list;
    }

    private static decimal CalculateCompetitorDailySalesVelocity(CompetitorShopProfileDto shop, List<MarketListingItemDto> listings)
    {
        if (shop.TotalSales > 0)
        {
            if (shop.CreatedDate.HasValue)
            {
                var days = Math.Max(30.0, (DateTimeOffset.UtcNow - shop.CreatedDate.Value).TotalDays);
                return Math.Round((decimal)shop.TotalSales / (decimal)days, 1);
            }
            var estimatedDays = Math.Clamp(shop.TotalSales * 2.5, 90.0, 1800.0);
            return Math.Round((decimal)shop.TotalSales / (decimal)estimatedDays, 1);
        }

        if (listings.Count > 0)
        {
            var estMonthlyFromFavs = listings.Sum(l => Math.Max(1, l.Favorites / 8));
            return Math.Round(estMonthlyFromFavs / 30m, 1);
        }

        return 0m;
    }

    private static (string Grade, int Score, string Explanation) CalculateCompetitorLqs(MarketListingItemDto listing)
    {
        int score = 0;
        int titleLen = listing.Title?.Length ?? 0;
        if (titleLen is >= 115 and <= 140) score += 35;
        else if (titleLen is >= 85 and < 115) score += 25;
        else if (titleLen is >= 50 and < 85) score += 15;
        else score += 5;

        int tagCount = listing.Tags?.Count ?? 0;
        if (tagCount >= 13) score += 35;
        else if (tagCount >= 10) score += 25;
        else if (tagCount >= 6) score += 15;
        else score += 5;

        if (listing.Tags != null && listing.Tags.Count > 0)
        {
            int multiWord = listing.Tags.Count(t => t.Trim().Contains(' '));
            decimal ratio = (decimal)multiWord / listing.Tags.Count;
            score += (int)Math.Round(ratio * 20m);
        }

        if (listing.Favorites > 10 || listing.Views > 50) score += 10;
        else if (listing.Favorites > 0 || listing.Views > 0) score += 5;

        score = Math.Clamp(score, 0, 100);
        if (score >= 90) return ("A+", score, "Mükemmel SEO ve optimizasyon. Başlık ve 13 etiket eksiksiz.");
        if (score >= 80) return ("A", score, "Güçlü listeleme. Organik sıralamada üst sıralarda yer alır.");
        if (score >= 68) return ("B", score, "İyi. Başlık uzunluğu veya 2-3 kelimelik long-tail etiket oranı artırılabilir.");
        if (score >= 50) return ("C", score, "Geliştirilmeli (Zayıf Karın). Eksik etiket veya kısa başlık var; kolay geçilebilir!");
        return ("D", score, "Kritik SEO Eksikleri. Bu üründen ilham alıp 13 altın etiketle hemen öne geçebilirsiniz.");
    }

    private static int CalculateCompetitorStrength(CompetitorShopProfileDto shop, List<CompetitorListingDto> listings, int avgSeo)
    {
        if (listings.Count == 0) return 0;
        decimal sales = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, shop.TotalSales) + 1) * 6m);
        decimal reviews = Math.Min(15m, shop.ReviewAverage * 2m + (decimal)Math.Log10(Math.Max(1, shop.ReviewCount) + 1) * 2m);
        decimal favorites = Math.Min(20m, (decimal)Math.Log10(Math.Max(1.0, listings.Average(i => i.Favorites)) + 1) * 8m);
        decimal views = Math.Min(15m, (decimal)Math.Log10(Math.Max(1.0, listings.Average(i => i.Views)) + 1) * 5m);
        decimal seo = avgSeo * 0.25m;
        return (int)Math.Round(Math.Clamp(sales + reviews + favorites + views + seo, 0m, 100m));
    }

    private static List<CompetitorFrequencyItemDto> BuildCompetitorFrequency(IEnumerable<string> values, int listingCount, int limit)
    {
        return values
            .Select(v => v.Trim())
            .Where(v => v.Length > 1)
            .GroupBy(v => v, StringComparer.OrdinalIgnoreCase)
            .Select(g => new CompetitorFrequencyItemDto
            {
                Name = g.Key.ToLowerInvariant(),
                Count = g.Count(),
                Percentage = listingCount > 0 ? Math.Round(g.Count() * 100m / listingCount, 1) : 0m
            })
            .OrderByDescending(i => i.Count)
            .ThenBy(i => i.Name, StringComparer.OrdinalIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<string> TokenizeTitleWords(string? title)
    {
        if (string.IsNullOrWhiteSpace(title)) yield break;
        foreach (Match m in Regex.Matches(title.ToLowerInvariant(), @"[\p{L}\p{N}]+"))
        {
            if (m.Value.Length > 2 && !CompetitorStopWords.Contains(m.Value))
            {
                yield return m.Value;
            }
        }
    }

    private static string BuildCompetitorGapInsight(CompetitorShopProfileDto shop, List<CompetitorListingDto> listings, decimal avgPrice)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"🎯 RAKİP AÇIĞI VE STRATEJİK DEĞERLENDİRME: {shop.ShopName.ToUpperInvariant()}");
        sb.AppendLine(new string('─', 56));

        if (avgPrice > 0)
        {
            sb.AppendLine($"• Fiyat Konumlandırması: Rakibin ortalama ürün fiyatı ${avgPrice:0.00}. Benzer ürünleri ${Math.Round(avgPrice * 0.88m, 2):0.00} giriş fiyatıyla veya ikili set (bundle) olarak sunarak dönüşüm oranını yakalayabilirsiniz.");
        }

        if (listings.Count > 0)
        {
            int lowLqsCount = listings.Count(l => l.LqsScore < 68);
            int missingTagsCount = listings.Count(l => l.MissingTagCount > 0);
            int shortTitleCount = listings.Count(l => l.TitleCharLength < 90);

            if (lowLqsCount > 0 || missingTagsCount > 0 || shortTitleCount > 0)
            {
                sb.AppendLine($"• SEO Zafiyeti (Fırsat Alanı): İncelenen {listings.Count} üründen {missingTagsCount} tanesinde 13 etiket eksik, {shortTitleCount} tanesinde başlık 90 karakterden kısa ve {lowLqsCount} tanesi C/D kalite notunda. 140 karakterlik başlık ve 13 long-tail etiket kuralıyla bu rakibin trafiğini organik aramada kesebilirsiniz.");
            }
            else
            {
                sb.AppendLine("• SEO Gücü: Rakibin başlık ve etiket disiplini güçlü. Öne geçmek için doğrudan niş hediye ve kişiselleştirme (custom/personalized) long-tail etiketlerine odaklanın.");
            }

            int uniqueTags = listings.SelectMany(l => l.Tags).Distinct(StringComparer.OrdinalIgnoreCase).Count();
            if (uniqueTags < listings.Count * 6)
            {
                sb.AppendLine($"• Etiket Tekrarı: Rakip tüm mağazasında yalnızca {uniqueTags} farklı etiket döndürüyor. Farklı alıcı persona ve sezon (Q4 / Hediye) etiketleri kullanarak rakibin ulaşamadığı aramaları yakalayabilirsiniz.");
            }
        }

        if (shop.ReviewCount > 0)
        {
            sb.AppendLine($"• Sosyal Kanıt & Güven: {shop.ReviewCount:N0} yorum ve {shop.ReviewAverage:0.0} yıldız ortalamasına sahip. Hızlı kargo (Express DDP) ve hediye paketi seçeneğini kapak görselinde vurgulayarak fark yaratabilirsiniz.");
        }

        return sb.ToString().TrimEnd();
    }
}
