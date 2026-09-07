namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal static class CompetitorListingAnalytics
{
    public static (int EstMonthlySales, decimal EstMonthlyRevenue, string Badge) EstimateListingSales(
        MarketListingResult listing,
        decimal totalEstimatedDailySales,
        IReadOnlyList<MarketListingResult> allListings)
    {
        if (allListings.Count == 0 || listing.Price <= 0)
            return (0, 0, "");

        // EverBee weight algorithm:
        // Weight is determined by Favorites (engagement), Views (traffic), and base active constant
        decimal listingWeight = (listing.Favorites * 2.5m) + (listing.Views * 0.08m) + 3m;
        decimal totalWeight = allListings.Sum(l => (l.Favorites * 2.5m) + (l.Views * 0.08m) + 3m);

        decimal shopMonthlySales = Math.Max(5m, totalEstimatedDailySales * 30m);
        decimal share = totalWeight > 0 ? listingWeight / totalWeight : 1m / allListings.Count;

        int monthlySales = Math.Max(0, (int)Math.Round(shopMonthlySales * share));
        // Sanity clamp: a single listing shouldn't exceed total shop sales
        monthlySales = Math.Clamp(monthlySales, 0, (int)Math.Ceiling(shopMonthlySales));
        decimal monthlyRevenue = Math.Round(monthlySales * listing.Price, 2);

        string badge = monthlySales >= 15 || listing.Favorites >= 150
            ? "🔥 Best Seller"
            : (monthlySales >= 5 || listing.Favorites >= 30 ? "⭐ Hot Item" : "");

        return (monthlySales, monthlyRevenue, badge);
    }

    public static (string Grade, Color GradeColor, string Explanation) CalculateLqs(MarketListingResult listing)
    {
        int score = 0;

        // 1. Title evaluation (Max 35 pts)
        var titleLen = listing.Title?.Length ?? 0;
        if (titleLen is >= 115 and <= 140) score += 35;
        else if (titleLen is >= 85 and < 115) score += 25;
        else if (titleLen is >= 50 and < 85) score += 15;
        else score += 5;

        // 2. Tag count evaluation (Max 35 pts)
        var tagCount = listing.Tags?.Count ?? 0;
        if (tagCount >= 13) score += 35;
        else if (tagCount >= 10) score += 25;
        else if (tagCount >= 6) score += 15;
        else score += 5;

        // 3. Multi-word tag quality (Max 20 pts)
        if (listing.Tags != null && listing.Tags.Count > 0)
        {
            var multiWord = listing.Tags.Count(t => t.Trim().Contains(' '));
            var multiWordRatio = (decimal)multiWord / listing.Tags.Count;
            score += (int)Math.Round(multiWordRatio * 20m);
        }

        // 4. Activity & Engagement (Max 10 pts)
        if (listing.Favorites > 10 || listing.Views > 50) score += 10;
        else if (listing.Favorites > 0 || listing.Views > 0) score += 5;

        if (score >= 90) return ("A+", Color.FromArgb(34, 197, 94), "Mükemmel SEO ve optimizasyon. Başlık ve 13 tag eksiksiz.");
        if (score >= 80) return ("A", Color.FromArgb(74, 222, 128), "Güçlü listeleme. Organik sıralamada üst sıralarda yer alır.");
        if (score >= 68) return ("B", Color.FromArgb(250, 204, 21), "İyi. Ancak başlık uzunluğu veya 2-3 kelimelik tag sayısı artırılabilir.");
        if (score >= 50) return ("C", Color.FromArgb(251, 146, 60), "Geliştirilmeli (Zayıf Karın). Eksik tag veya kısa başlık var; kolay geçilebilir!");
        return ("D", Color.FromArgb(239, 68, 68), "Kritik Eksikler. Zayıf SEO. Bu üründen ilham alıp altın SEO ile hemen öne geçebilirsiniz.");
    }

    public static Bitmap GeneratePlaceholderThumbnail(string title)
    {
        var bmp = new Bitmap(50, 50);
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        // Dark slate background
        using var brush = new SolidBrush(Color.FromArgb(30, 41, 59));
        g.FillRectangle(brush, 0, 0, 50, 50);

        // Accent border
        using var pen = new Pen(Color.FromArgb(51, 65, 85), 1);
        g.DrawRectangle(pen, 0, 0, 49, 49);

        // Initials or Icon
        var text = "ET";
        if (!string.IsNullOrWhiteSpace(title))
        {
            var words = title.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            text = words.Length >= 2
                ? $"{char.ToUpperInvariant(words[0][0])}{char.ToUpperInvariant(words[1][0])}"
                : (words.Length == 1 ? words[0][..Math.Min(2, words[0].Length)].ToUpperInvariant() : "ET");
        }

        using var font = new Font("Segoe UI", 12F, FontStyle.Bold);
        using var textBrush = new SolidBrush(Color.FromArgb(148, 163, 184));
        var size = g.MeasureString(text, font);
        g.DrawString(text, font, textBrush, (50 - size.Width) / 2, (50 - size.Height) / 2);

        return bmp;
    }
}
