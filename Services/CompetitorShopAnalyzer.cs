namespace SimilarProductsWinForms.Services;

using System.Text.RegularExpressions;
using SimilarProductsWinForms.Models;

internal static partial class CompetitorShopAnalyzer
{
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "for", "with", "from", "your", "you", "this", "that", "a", "an", "of", "to", "in", "on", "or",
        "ve", "ile", "icin", "bir", "bu", "da", "de", "veya",
    };

    public static CompetitorShopAnalysis Analyze(CompetitorShopProfile shop, List<MarketListingResult> listings)
    {
        var dominantCurrency = listings
            .Where(item => item.Price > 0 && !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode)
            .OrderByDescending(group => group.Count())
            .Select(group => group.Key)
            .FirstOrDefault() ?? "";
        var priced = listings
            .Where(item => item.Price > 0 && item.CurrencyCode.Equals(dominantCurrency, StringComparison.OrdinalIgnoreCase))
            .Select(item => item.Price)
            .Order()
            .ToList();
        var currencies = listings
            .Where(item => !string.IsNullOrWhiteSpace(item.CurrencyCode))
            .GroupBy(item => item.CurrencyCode)
            .OrderByDescending(group => group.Count())
            .Select(group => $"{group.Key} ({group.Count()})")
            .ToList();

        return new CompetitorShopAnalysis
        {
            Shop = shop,
            Listings = listings,
            TopTags = BuildFrequency(listings.SelectMany(item => item.Tags), listings.Count, 25),
            TopTitleTerms = BuildFrequency(
                listings.SelectMany(item => Tokenize(item.Title).Distinct(StringComparer.OrdinalIgnoreCase)),
                listings.Count,
                25),
            TaxonomyDistribution = BuildFrequency(
                listings.Where(item => item.TaxonomyId > 0).Select(item => $"Taksonomi #{item.TaxonomyId}"),
                listings.Count,
                20),
            MinimumPrice = priced.Count > 0 ? priced[0] : 0,
            MaximumPrice = priced.Count > 0 ? priced[^1] : 0,
            AveragePrice = priced.Count > 0 ? priced.Average() : 0,
            MedianPrice = Median(priced),
            AverageFavorites = listings.Count > 0 ? (decimal)listings.Average(item => item.Favorites) : 0,
            AverageViews = listings.Count > 0 ? (decimal)listings.Average(item => item.Views) : 0,
            AverageSeoScore = listings.Count > 0 ? (int)Math.Round(listings.Average(item => item.SeoScore)) : 0,
            CompetitorStrengthScore = CalculateStrength(shop, listings),
            RetrievedAt = DateTimeOffset.Now,
            CurrencyDisplay = currencies.Count > 0 ? string.Join(", ", currencies) : "Para birimi yok",
            PriceCurrency = dominantCurrency,
            EstimatedDailySalesVelocity = CalculateDailySalesVelocity(shop),
            EstimatedMonthlyTurnover = CalculateMonthlyTurnover(shop, priced.Count > 0 ? priced.Average() : 0),
            AiCompetitiveGapInsight = GenerateCompetitiveGapInsight(shop, listings, priced.Count > 0 ? priced.Average() : 0),
        };
    }

    private static decimal CalculateDailySalesVelocity(CompetitorShopProfile shop)
    {
        if (shop.TotalSales <= 0) return 0;
        if (shop.DailySalesEstimate > 0) return shop.DailySalesEstimate;

        if (shop.CreatedDate.HasValue)
        {
            var days = Math.Max(30, (DateTimeOffset.UtcNow - shop.CreatedDate.Value).TotalDays);
            return Math.Round((decimal)shop.TotalSales / (decimal)days, 1);
        }

        // Tahmini: Ortalama Etsy mağaza yaşam döngüsüne göre normalize
        var estimatedDays = Math.Clamp(shop.TotalSales * 2.5, 90, 1800);
        return Math.Round((decimal)shop.TotalSales / (decimal)estimatedDays, 1);
    }

    private static decimal CalculateMonthlyTurnover(CompetitorShopProfile shop, decimal avgPrice)
    {
        var daily = CalculateDailySalesVelocity(shop);
        var price = avgPrice > 0 ? avgPrice : (shop.MonthlyRevenueEstimate > 0 ? shop.MonthlyRevenueEstimate / 30 : 35m);
        return Math.Round(daily * 30 * price, 2);
    }

    public static string GenerateCompetitiveGapInsight(CompetitorShopProfile shop, List<MarketListingResult> listings, decimal avgPrice)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"🎯 RAKİP AÇIĞI VE STRATEJİK DEĞERLENDİRME: {shop.ShopName.ToUpperInvariant()}");
        sb.AppendLine(new string('-', 60));

        // 1. Fiyat Konumlandırması
        if (avgPrice > 0)
        {
            sb.AppendLine($"• Fiyatlandırma: Rakibin ortalama ürün fiyatı {avgPrice:C2}. Eğer ürünleriniz benzer kalitede ise %10-15 indirimle veya kargo dahil bundle (set) teklifleriyle öne geçebilirsiniz.");
        }

        // 2. SEO & Başlık Eksikleri
        if (listings.Count > 0)
        {
            var lowSeoCount = listings.Count(l => l.SeoScore < 65);
            var shortTitleCount = listings.Count(l => l.Title.Length < 90);
            if (lowSeoCount > 0 || shortTitleCount > 0)
            {
                sb.AppendLine($"• SEO Zayıflığı: Rakibin {listings.Count} ürününden {shortTitleCount} tanesi kısa başlığa, {lowSeoCount} tanesi ise zayıf SEO puanına sahip. 140 karakteri dolduran altın formüllü başlıklarımızla organik aramada bu rakibi rahatlıkla geçebilirsiniz.");
            }
            else
            {
                sb.AppendLine("• SEO Gücü: Rakibin başlıkları ve SEO optimizasyonu güçlü görünüyor. Öne geçmek için daha niş ve spesifik 2-3 kelimelik long-tail taglere odaklanın.");
            }

            // 3. Tag Çeşitliliği
            var totalTags = listings.SelectMany(l => l.Tags).Distinct().Count();
            if (totalTags < listings.Count * 6)
            {
                sb.AppendLine($"• Tag Tekrarı: Rakip ürünlerinde sürekli aynı kelimeleri tekrar ediyor (Toplam {totalTags} farklı etiket). Farklı kullanım alanları ve hediye kitlelerine hitap eden alternatif taglerle yeni alıcı trafiği yakalayabilirsiniz.");
            }
        }

        // 4. Müşteri Memnuniyeti & Güven
        if (shop.ReviewCount > 0)
        {
            sb.AppendLine($"• Müşteri Güveni: {shop.ReviewCount:N0} değerlendirme ile ortalama {shop.ReviewAverage:0.0} yıldız almış. Hızlı kargo, özenli hediye paketi ve 6 bölümlü profesyonel açıklama ile güven farkı yaratabilirsiniz.");
        }

        return sb.ToString();
    }

    private static List<FrequencyMetric> BuildFrequency(IEnumerable<string> values, int listingCount, int limit)
    {
        return values
            .Select(value => value.Trim())
            .Where(value => value.Length > 1)
            .GroupBy(value => value, StringComparer.OrdinalIgnoreCase)
            .Select(group => new FrequencyMetric(
                group.Key,
                group.Count(),
                listingCount > 0 ? Math.Round(group.Count() * 100m / listingCount, 1) : 0))
            .OrderByDescending(item => item.Count)
            .ThenBy(item => item.Name, StringComparer.CurrentCultureIgnoreCase)
            .Take(limit)
            .ToList();
    }

    private static IEnumerable<string> Tokenize(string title)
    {
        return WordRegex().Matches(title.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length > 2 && !StopWords.Contains(word));
    }

    private static decimal Median(List<decimal> sorted)
    {
        if (sorted.Count == 0)
        {
            return 0;
        }

        var middle = sorted.Count / 2;
        return sorted.Count % 2 == 0 ? (sorted[middle - 1] + sorted[middle]) / 2m : sorted[middle];
    }

    private static int CalculateStrength(CompetitorShopProfile shop, List<MarketListingResult> listings)
    {
        if (listings.Count == 0)
        {
            return 0;
        }

        var sales = Math.Min(25m, (decimal)Math.Log10(Math.Max(1, shop.TotalSales) + 1) * 6m);
        var reviews = Math.Min(15m, shop.ReviewAverage * 2m + (decimal)Math.Log10(Math.Max(1, shop.ReviewCount) + 1) * 2m);
        var favorites = Math.Min(20m, (decimal)Math.Log10(Math.Max(1, listings.Average(item => item.Favorites)) + 1) * 8m);
        var views = Math.Min(15m, (decimal)Math.Log10(Math.Max(1, listings.Average(item => item.Views)) + 1) * 5m);
        var seo = (decimal)listings.Average(item => item.SeoScore) * 0.15m;
        var variety = Math.Min(10m, listings.Select(item => item.TaxonomyId).Where(id => id > 0).Distinct().Count() * 2m);
        return (int)Math.Round(Math.Clamp(sales + reviews + favorites + views + seo + variety, 0m, 100m));
    }

    [GeneratedRegex(@"[\p{L}\p{N}]+")]
    private static partial Regex WordRegex();
}
