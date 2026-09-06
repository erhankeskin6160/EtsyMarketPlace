namespace EtsyMarketPlace.Application.SeasonalTrends;

public sealed class SeasonalTrendService
{
    public IReadOnlyList<SeasonalTrendItem> GetActiveSeasonalTrends(DateTime? referenceDate = null)
    {
        var now = referenceDate ?? DateTime.UtcNow;
        var month = now.Month;

        var allTrends = new List<SeasonalTrendItem>
        {
            new(
                "halloween_fall",
                "🎃",
                "Cadılar Bayramı & Sonbahar Dekorasyonu",
                "Sonbahar (Fall / Halloween)",
                "Ağustos - Ekim Sonu",
                GetStatusForMonths(month, [8, 9, 10], [6, 7]),
                GetColorHexForMonths(month, [8, 9, 10], [6, 7]),
                1,
                ["halloween decor", "pumpkin mug", "spooky gift", "witch aesthetic", "fall sweater", "ghost figurine", "cozy autumn art"],
                ["halloween decor", "spooky ghost gift", "pumpkin desk art", "witchy room decor", "fall festival gift", "cozy autumn mug", "goth home decor"],
                ["Seramik Kupa", "Ahşap Duvar Dekoru", "3D Baskı Figür", "Cosplay Kostüm Aksesuarı", "Mum & Koku"],
                "Etsy'de en erken patlayan sezonlardan biridir. Ağustos ayından itibaren yüklemeleri tamamlayıp 'spooky' ve 'fall decor' anahtar kelimelerini listenize dahil edin."
            ),
            new(
                "christmas_holidays",
                "🎄",
                "Yılbaşı & Noel Hediye Dalgası (Christmas & Q4)",
                "Kış (Q4 Holiday Rush)",
                "Ekim - Aralık Sonu",
                GetStatusForMonths(month, [10, 11, 12], [8, 9]),
                GetColorHexForMonths(month, [10, 11, 12], [8, 9]),
                2,
                ["christmas ornament", "personalized gift", "stocking stuffer", "secret santa gift", "holiday decor", "custom family portrait", "xmas sweater"],
                ["custom xmas ornament", "personalized gift", "holiday stocking", "secret santa mug", "christmas tree art", "family keepsake", "unique winter gift"],
                ["Kişiye Özel İsimli Süsler", "Hediyelik Setler", "Cosplay & Oyun Koleksiyonları", "Deri Cüzdan & Aksesuar"],
                "Etsy trafiğinin ve cirosunun %40'ından fazlası bu dönemde döner. 'Personalized Gift' ve 'Stocking Stuffer' tagleri en yüksek dönüşümü sağlar."
            ),
            new(
                "valentines_day",
                "❤️",
                "Sevgililer Günü & Romantik Hediyeler",
                "Kış Sonu (Valentine's)",
                "Ocak Başı - 14 Şubat",
                GetStatusForMonths(month, [1, 2], [11, 12]),
                GetColorHexForMonths(month, [1, 2], [11, 12]),
                3,
                ["valentines day gift", "couple bracelet", "boyfriend gift", "girlfriend necklace", "anniversary box", "custom love portrait", "romantic date night"],
                ["valentines gift", "couple bracelet set", "romantic gift box", "boyfriend keepsake", "custom love note", "girlfriend jewelry", "anniversary gift"],
                ["Minimalist Takı", "Çift Bileklikleri", "Özel Mesajlı Ahşap Kutular", "3D Baskı Kalp/Işık Heykeli"],
                "Kargo teslimat süreleri yüzünden siparişler 10 Ocak - 1 Şubat arasında zirve yapar. 'Boyfriend gift' ve 'Couple keepsake' taglerine odaklanın."
            ),
            new(
                "mothers_day",
                "🌸",
                "Anneler Günü & Bahar Uyanışı",
                "İlkbahar (Mother's Day & Spring)",
                "Mart - Mayıs Ortası",
                GetStatusForMonths(month, [3, 4, 5], [1, 2]),
                GetColorHexForMonths(month, [3, 4, 5], [1, 2]),
                4,
                ["mothers day gift", "personalized mom necklace", "spring floral decor", "first time mom gift", "grandma gift", "custom family garden", "easter basket stuffer"],
                ["mothers day gift", "personalized mom", "grandma keepsake", "custom mom necklace", "spring desk decor", "floral home accent", "first time mom"],
                ["İsimli Kolyeler", "Bahar Çiçekli Tablolar", "Kişiselleştirilmiş Ahşap Kesme Tahtası", "Seramik Çiçeklik"],
                "ABD ve İngiltere'de en çok hediye aranan dönemdir. Duygusal hediyeler, kişiselleştirilmiş ürünler ve annelere özel setler çok satar."
            ),
            new(
                "fathers_day_summer",
                "☀️",
                "Babalar Günü & Yaz Sezonu",
                "Yaz (Father's Day & Summer)",
                "Mayıs - Temmuz",
                GetStatusForMonths(month, [5, 6, 7], [3, 4]),
                GetColorHexForMonths(month, [5, 6, 7], [3, 4]),
                5,
                ["fathers day gift", "dad keychain", "outdoor game", "beach tote bag", "bachelor party favors", "geeky dad gift", "bbq grill tool set"],
                ["fathers day gift", "custom dad gift", "leather keychain", "geeky dad tool", "summer party game", "personalized wallet", "outdoor garden art"],
                ["Deri Cüzdan & Anahtarlık", "Masaüstü Düzenleyici (Docking Station)", "Düğün & Bekarlığa Veda Hediyelikleri"],
                "Babalar günü için erkeklere hediye kategorisi öne çıkar. Ayrıca yaz düğünleri ve açık hava partileri için hediyelikler talep görür."
            ),
            new(
                "back_to_school",
                "🎒",
                "Okula Dönüş & Öğretmen Hediyeleri",
                "Geç Yaz (Back to School)",
                "Temmuz - Eylül Başı",
                GetStatusForMonths(month, [7, 8, 9], [5, 6]),
                GetColorHexForMonths(month, [7, 8, 9], [5, 6]),
                6,
                ["back to school", "teacher appreciation gift", "dorm room decor", "personalized pencil case", "custom planner", "desk organizer", "student notebook"],
                ["back to school gift", "teacher appreciation", "dorm room decor", "custom pencil case", "aesthetic planner", "student desk caddy", "classroom sign"],
                ["Kırtasiye & Defter", "Yurt Odası Duvar Sanatı", "Öğretmen Teşekkür Bardakları & Çantaları"],
                "Üniversiteye başlayanlar için oda dekoru ve öğretmenlere hediye odaklı listelemeler hızla satılır."
            )
        };

        return allTrends
            .OrderBy(t => t.Status == "🔥 ZİRVE SEZONU (Şimdi Satış Zamanı)" ? 0 : (t.Status == "⚡ HAZIRLIK ZAMANI (Ürün Yükle)" ? 1 : 2))
            .ThenBy(t => t.Priority)
            .ToList();
    }

    private static string GetStatusForMonths(int currentMonth, int[] activeMonths, int[] prepMonths)
    {
        if (activeMonths.Contains(currentMonth)) return "🔥 ZİRVE SEZONU (Şimdi Satış Zamanı)";
        if (prepMonths.Contains(currentMonth)) return "⚡ HAZIRLIK ZAMANI (Ürün Yükle)";
        return "💤 Sezon Dışı (Gelecek Sezona Planla)";
    }

    private static string GetColorHexForMonths(int currentMonth, int[] activeMonths, int[] prepMonths)
    {
        if (activeMonths.Contains(currentMonth)) return "#EF4444"; // Red
        if (prepMonths.Contains(currentMonth)) return "#F59E0B"; // Amber
        return "#94A3B8"; // Slate
    }

    public TrendNicheAnalysisResult AnalyzeNicheOpportunity(string keyword, IReadOnlyList<NicheListingSample> listings)
    {
        if (listings.Count == 0)
        {
            return new TrendNicheAnalysisResult(
                keyword,
                0,
                0, 0, 0, 0, 0,
                30, 20, 50,
                [], [],
                "Bu kelimede yeterli piyasa verisi bulunamadı. Daha genel bir terim arayın."
            );
        }

        var priced = listings.Where(l => l.Price > 0).Select(l => l.Price).ToList();
        var avgPrice = priced.Count > 0 ? Math.Round(priced.Average(), 2) : 0m;
        var minPrice = priced.Count > 0 ? priced.Min() : 0m;
        var maxPrice = priced.Count > 0 ? priced.Max() : 0m;
        var avgFav = (int)Math.Round(listings.Average(l => l.Favorites));
        var avgViews = (int)Math.Round(listings.Average(l => l.Views));

        var demandScore = Math.Clamp((int)Math.Round(Math.Log10(Math.Max(1, avgFav) + 1) * 32), 20, 98);
        var competitionScore = Math.Clamp((int)Math.Round(Math.Log10(Math.Max(1, listings.Count * 25) + 1) * 22), 15, 95);
        var oppScore = Math.Clamp((int)Math.Round((demandScore * 1.3m) - (competitionScore * 0.45m) + 15), 10, 99);

        var winningTags = listings
            .SelectMany(l => l.Tags)
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length is >= 4 and <= 20 && t.Contains(' '))
            .GroupBy(t => t)
            .OrderByDescending(g => g.Count())
            .Take(13)
            .Select(g => g.Key)
            .ToList();

        var topTitles = listings
            .OrderByDescending(l => l.Favorites)
            .Take(5)
            .Select(l => l.Title)
            .ToList();

        var recommendation = oppScore >= 75
            ? "🚀 YÜKSEK FIRSAT: Bu nişte alıcı talebi çok yüksek ve rekabet henüz doymamış! Hemen kaliteli 1-2 ürün listeleyip kazanabilirsiniz."
            : (oppScore >= 50
                ? "⚖️ ORTA FIRSAT: Talep dengeli ancak güçlü rakipler var. Öne çıkmak için niş 2-3 kelimelik long-tail taglere ve farklı tasarımlara odaklanın."
                : "⚠️ YÜKSEK REKABET: Pazar doygunluğa yakın. Farklı bir varyasyon, kişiselleştirme veya daha rekabetçi bir fiyat teklifi gereklidir.");

        return new TrendNicheAnalysisResult(
            keyword,
            listings.Count,
            avgPrice,
            minPrice,
            maxPrice,
            avgFav,
            avgViews,
            demandScore,
            competitionScore,
            oppScore,
            winningTags,
            topTitles,
            recommendation
        );
    }
}
