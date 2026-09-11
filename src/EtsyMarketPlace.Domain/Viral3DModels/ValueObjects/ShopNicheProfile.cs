namespace EtsyMarketPlace.Domain.Viral3DModels.ValueObjects;

using System;
using System.Collections.Generic;

public sealed class ShopNicheProfile
{
    public string ShopId { get; set; } = string.Empty;
    public string ShopName { get; set; } = string.Empty;

    /// <summary>
    /// Tespit edilen birincil niş (Örn: "🎮 Eklemli Figür, Oyuncak & Fidget Modeller", "🌿 Modern Ev & Geometrik Saksı Tasarımları")
    /// </summary>
    public string PrimaryNiche { get; set; } = "Genel 3D Baskı & Tasarım";

    /// <summary>
    /// İkincil nişler veya yan kategoriler
    /// </summary>
    public List<string> SecondaryNiches { get; set; } = [];

    /// <summary>
    /// Mağazanın hedef müşteri kitlesi (Örn: "Masaüstü oyuncak koleksiyoncuları, fidget severler")
    /// </summary>
    public string TargetAudience { get; set; } = "Genel Etsy Alıcıları";

    /// <summary>
    /// 3D modellerle eşleştirilecek pozitif anahtar kelimeler ve etiketler
    /// (Örn: "figure", "toy", "dragon", "action", "fidget", "articulated", "jointed", "doll")
    /// </summary>
    public HashSet<string> AffinityKeywords { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Analizi yapan AI motoru adı (Örn: "Google Gemini 2.5 Flash", "OpenAI GPT-4o", "Claude 3.7", "Yerel NLP")
    /// </summary>
    public string ActiveAiProviderName { get; set; } = "Yapay Zeka Radarı";

    /// <summary>
    /// AI tarafından oluşturulan gerekçeli mağaza özeti ve tavsiyesi
    /// </summary>
    public string AiReasoning { get; set; } = "Mağaza listingleri analiz edilerek en uygun 3D modeller filtreleniyor.";

    /// <summary>
    /// AI güven skoru (0 - 100)
    /// </summary>
    public int ConfidenceScore { get; set; } = 90;

    /// <summary>
    /// İncelenen listing sayısı
    /// </summary>
    public int AnalyzedListingCount { get; set; }

    public DateTime AnalyzedAtUtc { get; set; } = DateTime.UtcNow;

    public static ShopNicheProfile CreateDefaultFigureAndToy(string shopName = "3DArtDesignsStore")
    {
        return new ShopNicheProfile
        {
            ShopName = shopName,
            PrimaryNiche = "🎮 Eklemli Figür, Oyuncak & Fidget Modeller",
            SecondaryNiches = ["Masaüstü Dekorasyon", "Anime & Oyun Karakterleri", "Print-In-Place Mekanizmalar"],
            TargetAudience = "Masaüstü koleksiyoncuları, çocuk/genç hediye alıcıları, fidget severler",
            AffinityKeywords = new(StringComparer.OrdinalIgnoreCase)
            {
                "figure", "figür", "toy", "oyuncak", "dragon", "action figure", "articulated", "eklemli",
                "jointed", "fidget", "robot", "dummy 13", "octopus", "flexi", "character", "anime", "desk toy"
            },
            ActiveAiProviderName = "Yapay Zeka (Aktif)",
            AiReasoning = "Mağazanız ağırlıklı olarak eklemli oyuncaklar, hareketli ejderhalar ve robot figürleri satıyor. Müşteri kitleniz hareketli (print-in-place) STL tasarımlarına yüksek ilgi göstermektedir.",
            ConfidenceScore = 96,
            AnalyzedListingCount = 12
        };
    }
}
