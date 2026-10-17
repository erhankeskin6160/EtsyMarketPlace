namespace EtsyMarketPlace.Application.MarketResearch;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.ListingOptimization;

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
}
