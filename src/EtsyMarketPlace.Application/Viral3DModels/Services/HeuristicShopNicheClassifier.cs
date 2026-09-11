namespace EtsyMarketPlace.Application.Viral3DModels.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

public static class HeuristicShopNicheClassifier
{
    private static readonly (string Niche, string[] Keywords, string Audience, string[] Secondaries)[] NicheProfiles =
    [
        (
            "🎮 Eklemli Figür, Oyuncak & Fidget Modeller",
            ["figure", "figür", "toy", "oyuncak", "dragon", "action figure", "articulated", "eklemli", "jointed", "fidget", "robot", "dummy", "octopus", "flexi", "character", "anime", "desk toy"],
            "Masaüstü figür koleksiyoncuları, çocuk ve genç hediye alıcıları, fidget severler",
            ["Masaüstü Dekorasyon", "Anime & Oyun Karakterleri", "Print-In-Place Mekanizmalar"]
        ),
        (
            "🌿 Modern Ev, Geometrik Saksı & Bitki Tasarımları",
            ["planter", "saksı", "pot", "succulent", "plant", "vase", "vazo", "botanical", "flower", "home decor", "terracotta", "gardening"],
            "Modern iç mekan bitki meraklıları, ev dekorasyon severler, sukulent yetiştiricileri",
            ["Boho & Minimalist Ev Dekoru", "Kendi Kendini Sulayan Sistemler", "Masa Üstü Yeşillik"]
        ),
        (
            "🗄️ Masaüstü, Atölye & Modüler Çekmece Düzenleyiciler",
            ["organizer", "drawer", "wavegrid", "gridfinity", "storage", "box", "pegboard", "hsw", "honeycomb", "tool", "workshop", "kutusu", "düzenleyici", "shelf"],
            "Düzen takıntılı profesyoneller, atölye ve garaj meraklıları, masaüstü minimalizm severler",
            ["Modüler Çekmece İçi Bins", "Atölye Duvar Panelleri", "Kablo ve Donanım Saklama"]
        ),
        (
            "🎲 Masaüstü Kutu Oyunları, D&D & Zar Aksesuarları",
            ["dice", "zar", "dice tower", "dnd", "d&d", "rpg", "tabletop", "miniature", "castle", "dungeon", "polyhedral"],
            "Dungeons & Dragons oyuncuları, masaüstü RPG severler ve Dungeon Master'lar",
            ["Zar Tepsileri & Kuleleri", "Minyatür Savaş Figürleri", "Oyun Odası Tematik Aksesuarları"]
        ),
        (
            "💡 Ambiyans Lambaları, LED Tabela & Gece Işıkları",
            ["lamp", "lamba", "lightbox", "led", "light", "sign", "crystal", "illuminated", "lithophane", "gece lambası", "neon"],
            "Oda ve oyun alanı aydınlatması arayanlar, hediye alıcıları, modern ambiyans sevenler",
            ["2 Renkli LED Paneller", "Geometrik Kristal Lambalar", "Kişiselleştirilmiş İsim Işıkları"]
        )
    ];

    public static ShopNicheProfile Classify(string shopName, IEnumerable<ShopListingItem> listings, string providerName = "Yerel NLP (Çevrimdışı)")
    {
        var items = listings?.ToList() ?? [];
        if (items.Count == 0)
        {
            return ShopNicheProfile.CreateDefaultFigureAndToy(shopName);
        }

        var textCorpus = new List<string>();
        foreach (var it in items)
        {
            if (!string.IsNullOrWhiteSpace(it.Title)) textCorpus.Add(it.Title);
            if (!string.IsNullOrWhiteSpace(it.Category)) textCorpus.Add(it.Category);
            if (it.Tags != null) textCorpus.AddRange(it.Tags);
        }
        string combinedText = string.Join(" ", textCorpus).ToLowerInvariant();

        int bestScore = -1;
        var bestNiche = NicheProfiles[0];

        foreach (var np in NicheProfiles)
        {
            int score = 0;
            foreach (var kw in np.Keywords)
            {
                if (combinedText.Contains(kw.ToLowerInvariant()))
                {
                    score += 10;
                }
            }

            if (score > bestScore)
            {
                bestScore = score;
                bestNiche = np;
            }
        }

        var affinitySet = new HashSet<string>(bestNiche.Keywords, StringComparer.OrdinalIgnoreCase);

        // Also add high-frequency tokens from user's actual listing titles
        var words = combinedText.Split([' ', ',', '-', '/', '|', '(', ')'], StringSplitOptions.RemoveEmptyEntries)
            .Where(w => w.Length >= 4)
            .GroupBy(w => w)
            .OrderByDescending(g => g.Count())
            .Take(10)
            .Select(g => g.Key);

        foreach (var w in words)
        {
            affinitySet.Add(w);
        }

        int confidence = Math.Min(98, Math.Max(75, 70 + (bestScore / 2)));

        return new ShopNicheProfile
        {
            ShopName = !string.IsNullOrWhiteSpace(shopName) ? shopName : "Bağlı Etsy Mağazası",
            PrimaryNiche = bestNiche.Niche,
            SecondaryNiches = [.. bestNiche.Secondaries],
            TargetAudience = bestNiche.Audience,
            AffinityKeywords = affinitySet,
            ActiveAiProviderName = providerName,
            AiReasoning = $"Mağazanızdaki {items.Count} listing incelendi. Ürün başlıklarında ve etiketlerinde '{string.Join(", ", affinitySet.Take(5))}' kelimelerinin yoğunluğu tespit edildi. Hedef kitleniz bu doğrultuda modellenmiştir.",
            ConfidenceScore = confidence,
            AnalyzedListingCount = items.Count,
            AnalyzedAtUtc = DateTime.UtcNow
        };
    }
}
