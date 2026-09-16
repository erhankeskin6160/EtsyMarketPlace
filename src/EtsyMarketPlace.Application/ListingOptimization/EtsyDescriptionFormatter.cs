namespace EtsyMarketPlace.Application.ListingOptimization;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Etsy ürün açıklamalarını alıcı odaklı, okunaklı, emojili ve paragraflara ayrılmış
/// standart Etsy şablonuna dönüştüren ve Etsy API v3 için çift satır atlamasını (\r\n\r\n) garanti eden formatlayıcı.
/// </summary>
public static class EtsyDescriptionFormatter
{
    private static readonly Regex MarkdownHeadingRegex = new(@"^#{1,6}\s*(.+)$", RegexOptions.Multiline);
    private static readonly Regex BoldRegex = new(@"\*\*(.+?)\*\*|__(.+?)__");
    private static readonly Regex BulletRegex = new(@"^[\*\-\+]\s+", RegexOptions.Multiline);

    /// <summary>
    /// Ham veya AI tarafından üretilen metni Etsy standartlarında temiz, çift satır aralıklı paragraflara dönüştürür.
    /// Etsy mobil ve masaüstü arayüzünde metinlerin birbirine yapışmasını (merged block) engeller.
    /// </summary>
    public static string NormalizeForEtsy(string? rawText)
    {
        if (string.IsNullOrWhiteSpace(rawText)) return "";

        // 1. Debug ve UI etiketlerini temizle (kullanıcı tüm arayüzü kopyaladıysa)
        var cleaned = rawText;
        cleaned = Regex.Replace(cleaned, @"\[SATIŞ\s+ODAKLI\s+AÇIKLAMA\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[MEVCUT\s+BAŞLIK[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[MEVCUT\s+TAGLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[PUAN\s+&\s+METRİKLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[TESPİT\s+EDİLEN\s+EKSİKLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[RİSK\s+VE\s+KURAL\s+UYARILARI[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[ÖNERİLEN\s+MATERYALLER[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[ÖNERİLEN\s+13\s+LONG-TAIL\s+TAG[^\]]*\]", "", RegexOptions.IgnoreCase);
        cleaned = Regex.Replace(cleaned, @"\[AI\s+İLE\s+OPTİMİZE\s+EDİLMİŞ\s+BAŞLIK[^\]]*\]", "", RegexOptions.IgnoreCase);

        // 2. Markdown başlıklarını emoji/büyük harfli Etsy başlıklarına çevir
        cleaned = MarkdownHeadingRegex.Replace(cleaned, m =>
        {
            var text = m.Groups[1].Value.Trim();
            return EnsureSectionHeaderEmoji(text);
        });

        // 3. Kalın / İtalik markdown işaretlerini kaldır
        cleaned = BoldRegex.Replace(cleaned, m => m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);

        // 4. Madde işaretlerini standart '• ' ile değiştir
        cleaned = BulletRegex.Replace(cleaned, "• ");

        // 4.5. Yan yana sıkışmış olabilecek emoji başlıkları ve madde imlerini yeni satıra taşı
        cleaned = Regex.Replace(cleaned, @"(?<=[^\r\n])\s*(✨|📏|🎁|📦|💬|🧼)\s*", "\r\n\r\n$1 ");
        cleaned = Regex.Replace(cleaned, @"(?<=[^\r\n])\s+(•\s+)", "\r\n$1");

        // 5. Satır sonlarını ayrıştır ve temiz bloklar oluştur
        var rawLines = cleaned.Replace("\r\n", "\n").Replace('\r', '\n').Split('\n');
        var blocks = new List<string>();
        var currentBlock = new StringBuilder();

        foreach (var line in rawLines)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed))
            {
                if (currentBlock.Length > 0)
                {
                    blocks.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
                continue;
            }

            // Eğer satır bir bölüm başlığı ise önceki bloğu bitir ve başlığı ayrı blok yap
            if (IsSectionHeader(trimmed))
            {
                if (currentBlock.Length > 0)
                {
                    blocks.Add(currentBlock.ToString().Trim());
                    currentBlock.Clear();
                }
                blocks.Add(EnsureSectionHeaderEmoji(trimmed));
                continue;
            }

            // Madde işareti ise
            if (trimmed.StartsWith("•") || trimmed.StartsWith("-") || trimmed.StartsWith("*"))
            {
                if (!trimmed.StartsWith("• "))
                {
                    trimmed = "• " + trimmed.TrimStart('•', '-', '*', ' ');
                }
            }

            if (currentBlock.Length > 0)
            {
                // Eğer satır veya mevcut blok madde işareti içeriyorsa her madde yeni satırda olmalı
                if (trimmed.StartsWith("• ") || currentBlock.ToString().Contains("• "))
                {
                    currentBlock.Append("\r\n").Append(trimmed);
                }
                else
                {
                    currentBlock.Append(" ").Append(trimmed);
                }
            }
            else
            {
                currentBlock.Append(trimmed);
            }
        }

        if (currentBlock.Length > 0)
        {
            blocks.Add(currentBlock.ToString().Trim());
        }

        // 6. Blokları Etsy'de ferah görünecek şekilde çift satır sonu (\r\n\r\n) ile birleştir
        var result = new StringBuilder();
        for (int i = 0; i < blocks.Count; i++)
        {
            var block = blocks[i].Trim();
            if (string.IsNullOrWhiteSpace(block)) continue;

            result.Append(block);
            if (i < blocks.Count - 1)
            {
                result.Append("\r\n\r\n");
            }
        }

        return result.ToString().Trim();
    }

    /// <summary>
    /// Herhangi bir ham metni, ürün başlığı ve etiketlerini kullanarak 6 bölümlü Altın Etsy Paragraf Şablonuna dönüştürür.
    /// </summary>
    public static string FormatToStandardTemplate(
        string? rawDesc,
        string title,
        IReadOnlyList<string>? tags = null,
        IReadOnlyList<string>? materials = null,
        string? targetKeyword = null)
    {
        var cleanTitle = SanitizeTitle(title);
        var target = !string.IsNullOrWhiteSpace(targetKeyword) 
            ? targetKeyword.Trim() 
            : (tags?.FirstOrDefault() ?? cleanTitle);

        var matList = materials != null && materials.Count > 0 
            ? string.Join(", ", materials.Take(4)) 
            : "High-grade materials & precision craft";

        var cleanSource = ExtractSourceDetails(rawDesc);
        var theme = DetectProductTheme(cleanTitle, rawDesc, tags);

        var sb = new StringBuilder();

        // BÖLÜM 1: Google Meta Hook & Giriş Paragrafı (Kategoriye & Ürüne Özel Canlı Kanca)
        sb.AppendLine(BuildDynamicHook(theme, cleanTitle, target));
        sb.AppendLine();

        // BÖLÜM 2: Öne Çıkan Özellikler (Ürünün Gerçek Özellikleri)
        sb.AppendLine("✨ WHY YOU'LL LOVE IT:");
        if (cleanSource.KeyFeatures.Count > 0)
        {
            foreach (var feat in cleanSource.KeyFeatures.Take(3))
            {
                sb.AppendLine($"• {feat}");
            }
        }
        else
        {
            sb.AppendLine($"• Premium Craftsmanship: Expertly manufactured with durable {matList} for a smooth, high-detail finish.");
            sb.AppendLine(BuildThemeDisplayHighlight(theme));
        }
        sb.AppendLine("• Collector & Fan Approved: Meticulously inspected and finished with exceptional attention to detail.");
        sb.AppendLine();

        // BÖLÜM 3: Boyut ve Teknik Özellikler
        sb.AppendLine("📏 SPECIFICATIONS & DETAILS:");
        sb.AppendLine($"• Materials: {matList}");
        if (!string.IsNullOrWhiteSpace(cleanSource.Dimensions))
        {
            sb.AppendLine($"• Dimensions: {cleanSource.Dimensions}");
        }
        else
        {
            sb.AppendLine("• Dimensions: Standard display size (detailed dimensions available upon request).");
        }
        if (!string.IsNullOrWhiteSpace(cleanSource.IncludedItems))
        {
            sb.AppendLine($"• Package Includes: {cleanSource.IncludedItems}");
        }
        sb.AppendLine("• Finish: Clean, hand-finished surface with vibrant, durable detailing.");
        if (!string.IsNullOrWhiteSpace(cleanSource.ExtraNotes))
        {
            sb.AppendLine($"• Note: {cleanSource.ExtraNotes}");
        }
        sb.AppendLine();

        // BÖLÜM 4: Kimler İçin Uygun / Hediye (Ürün Temasına Özel)
        sb.AppendLine("🎁 PERFECT FOR:");
        sb.AppendLine(BuildThemeAudience(theme));
        sb.AppendLine("• An unforgettable birthday, anniversary, holiday, or special celebration gift.");
        sb.AppendLine();

        // BÖLÜM 5: Güvenli Paketleme & Kargo
        sb.AppendLine("📦 PACKAGING & SHIPPING:");
        sb.AppendLine("• Carefully wrapped in multi-layer protective packaging to guarantee 100% safe worldwide arrival.");
        sb.AppendLine("• Every order includes tracked dispatch sent directly to your email upon shipment.");
        sb.AppendLine();

        // BÖLÜM 6: Özel İstekler & İletişim
        sb.AppendLine("💬 CUSTOM REQUESTS & QUESTIONS:");
        sb.AppendLine("• Looking for a custom color, size, or special personalization? Feel free to reach out anytime—we are happy to help!");

        return NormalizeForEtsy(sb.ToString());
    }

    private static string BuildDynamicHook(ProductTheme theme, string title, string target) => theme switch
    {
        ProductTheme.MusicOrCelebrity =>
            $"Celebrate the legendary icon with this stunning {title}! Tailored for shoppers searching for {target}, this handcrafted tribute brings extraordinary detail, charisma, and presence to your space.",
        ProductTheme.LampOrLighting =>
            $"Transform your space with the ambient glow of this {title}! Perfect for shoppers searching for {target}, this artisan creation seamlessly blends cozy atmosphere, captivating lighting, and modern decor.",
        ProductTheme.CosplayOrProp =>
            $"Complete your setup with this show-stopping {title}! Specially designed for shoppers searching for {target}, this piece delivers authentic presence, fine craftsmanship, and durable detail.",
        ProductTheme.JewelryOrWearable =>
            $"Add a touch of distinctive artisan charm with this elegant {title}! Handcrafted for shoppers searching for {target}, this piece combines refined beauty, comfort, and timeless character.",
        ProductTheme.GamingOrAnime =>
            $"Level up your sanctuary with this authentic {title}! Tailored for shoppers searching for {target}, this piece brings standout craftsmanship and unmistakable character to your setup.",
        ProductTheme.HomeDecorOrArt =>
            $"Elevate your interior aesthetics with this handcrafted {title}! Designed for shoppers searching for {target}, this distinct showpiece brings warmth, style, and conversation-starting artistry to any room.",
        _ =>
            $"Elevate your space with this unique {title}! Tailored for shoppers searching for {target}, this handcrafted piece brings outstanding quality and distinct character to any collection or setup."
    };

    private static string BuildThemeDisplayHighlight(ProductTheme theme) => theme switch
    {
        ProductTheme.MusicOrCelebrity => "• Iconic Tribute: A must-have centerpiece for music studios, vinyl shelves, entertainment rooms, or display cabinets.",
        ProductTheme.LampOrLighting => "• Atmospheric Ambiance: Creates a soothing, aesthetic lighting effect for desks, nightstands, and living rooms.",
        ProductTheme.CosplayOrProp => "• Display & Cosplay Ready: Perfectly weighted and proportioned for photo shoots, cosplay events, or premium wall display.",
        ProductTheme.GamingOrAnime => "• Battlestation Ready: Designed to sit proudly next to your PC setup, gaming console, or collector bookcase.",
        _ => "• Eye-Catching Display: Designed to stand out on your desk, shelf, studio, or living space."
    };

    private static string BuildThemeAudience(ProductTheme theme) => theme switch
    {
        ProductTheme.MusicOrCelebrity =>
            "• Dedicated music fans, pop culture enthusiasts, vinyl collectors, and tribute art lovers.",
        ProductTheme.LampOrLighting =>
            "• Home decor lovers, night owls, bedroom aesthetics, and cozy workspace setups.",
        ProductTheme.CosplayOrProp =>
            "• Cosplayers, convention goers, fantasy fans, and theatrical prop collectors.",
        ProductTheme.JewelryOrWearable =>
            "• Style enthusiasts, vintage jewelry collectors, and anyone who appreciates bespoke handcrafted accessories.",
        ProductTheme.GamingOrAnime =>
            "• Gamers, anime lovers, cosplay enthusiasts, and tabletop/novelty decor collectors.",
        ProductTheme.HomeDecorOrArt =>
            "• Interior design lovers, aesthetic home stylists, art enthusiasts, and modern decor collectors.",
        _ =>
            "• Discerning collectors, home decor enthusiasts, and fans of unique handcrafted goods."
    };

    private enum ProductTheme
    {
        General,
        MusicOrCelebrity,
        LampOrLighting,
        CosplayOrProp,
        GamingOrAnime,
        JewelryOrWearable,
        HomeDecorOrArt
    }

    private static ProductTheme DetectProductTheme(string title, string? desc, IReadOnlyList<string>? tags)
    {
        var blob = $"{title} {desc} {string.Join(' ', tags ?? [])}".ToLowerInvariant();

        if (blob.Contains("michael jackson") || blob.Contains("singer") || blob.Contains("musician") ||
            blob.Contains("king of pop") || blob.Contains("music legend") || blob.Contains("rock star") ||
            blob.Contains("guitar") || blob.Contains("vinyl") || blob.Contains("concert") || blob.Contains("pop star"))
        {
            return ProductTheme.MusicOrCelebrity;
        }

        if (blob.Contains("lamp") || blob.Contains("night light") || blob.Contains("nightlight") ||
            blob.Contains("led light") || blob.Contains("lantern") || blob.Contains("ambient light") ||
            blob.Contains("desk lamp") || blob.Contains("moon lamp") || blob.Contains("table lamp"))
        {
            return ProductTheme.LampOrLighting;
        }

        if (blob.Contains("cosplay") || blob.Contains("prop replica") || blob.Contains("helmet") ||
            blob.Contains("sword") || blob.Contains("dagger") || blob.Contains("shield") ||
            blob.Contains("wearable prop") || blob.Contains("costume prop"))
        {
            return ProductTheme.CosplayOrProp;
        }

        if (blob.Contains("necklace") || blob.Contains("bracelet") || blob.Contains("ring") ||
            blob.Contains("earring") || blob.Contains("pendant") || blob.Contains("jewelry"))
        {
            return ProductTheme.JewelryOrWearable;
        }

        if (blob.Contains("gaming") || blob.Contains("gamer") || blob.Contains("anime") ||
            blob.Contains("manga") || blob.Contains("video game") || blob.Contains("rpg") ||
            blob.Contains("dnd") || blob.Contains("tabletop mini"))
        {
            return ProductTheme.GamingOrAnime;
        }

        if (blob.Contains("vase") || blob.Contains("planter") || blob.Contains("wall art") ||
            blob.Contains("shelf decor") || blob.Contains("home decor") || blob.Contains("candle holder") ||
            blob.Contains("sculpture") || blob.Contains("clock"))
        {
            return ProductTheme.HomeDecorOrArt;
        }

        return ProductTheme.General;
    }

    private static bool IsSectionHeader(string line)
    {
        var upper = line.ToUpperInvariant();
        return upper.Contains("WHY YOU'LL LOVE IT") ||
               upper.Contains("SPECIFICATIONS") ||
               upper.Contains("DETAILS") ||
               upper.Contains("PERFECT FOR") ||
               upper.Contains("PACKAGING") ||
               upper.Contains("SHIPPING") ||
               upper.Contains("CUSTOM REQUESTS") ||
               upper.Contains("CARE INSTRUCTIONS") ||
               upper.Contains("HOW TO ORDER") ||
               upper.Contains("OVERVIEW");
    }

    private static string EnsureSectionHeaderEmoji(string text)
    {
        var upper = text.ToUpperInvariant();
        if (upper.Contains("WHY YOU'LL LOVE IT") && !text.Contains("✨")) return "✨ " + text.Trim();
        if (upper.Contains("SPECIFICATIONS") && !text.Contains("📏")) return "📏 " + text.Trim();
        if (upper.Contains("PERFECT FOR") && !text.Contains("🎁")) return "🎁 " + text.Trim();
        if (upper.Contains("PACKAGING") && !text.Contains("📦")) return "📦 " + text.Trim();
        if (upper.Contains("CUSTOM REQUESTS") && !text.Contains("💬")) return "💬 " + text.Trim();
        if (upper.Contains("CARE") && !text.Contains("🧼")) return "🧼 " + text.Trim();
        return text;
    }

    private static string SanitizeTitle(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return "Handcrafted Item";
        var parts = title.Split(['|', '-', ',', '–', '—', '/'], StringSplitOptions.RemoveEmptyEntries);
        var first = parts.FirstOrDefault()?.Trim();
        return !string.IsNullOrWhiteSpace(first) && first.Length >= 4 ? first : title.Trim();
    }

    private sealed record ExtractedDetails(
        string Dimensions,
        List<string> KeyFeatures,
        string IncludedItems,
        string ExtraNotes);

    private static ExtractedDetails ExtractSourceDetails(string? rawDesc)
    {
        if (string.IsNullOrWhiteSpace(rawDesc)) return new("", [], "", "");

        string dimensions = "";
        string included = "";
        string extraNotes = "";
        var features = new List<string>();

        var lines = rawDesc.Split(['\r', '\n', '.', ';'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var l in lines)
        {
            var line = l.Trim();
            if (line.Contains("Elevate your space", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Elevate your collection", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Tailored for shoppers", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Premium Craftsmanship", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Eye-Catching Display", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Collector & Fan Approved", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Carefully wrapped in", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("Looking for a custom", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("WHY YOU'LL LOVE IT", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("SPECIFICATIONS & DETAILS", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("PERFECT FOR", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("PACKAGING & SHIPPING", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("CUSTOM REQUESTS", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Overview:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("Overview:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Materials:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Craftsmanship:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Dimensions:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Finish:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Note:", StringComparison.OrdinalIgnoreCase) ||
                line.StartsWith("• Package Includes:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var cleanItem = line.TrimStart('•', '-', '*', ' ').Trim();
            if (cleanItem.Length < 6) continue;

            // Boyut ayıklama
            if (string.IsNullOrEmpty(dimensions) &&
                (line.Contains("cm", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("inch", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("\"") ||
                 line.Contains("dimension", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("height:", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("width:", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("size:", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("scale:", StringComparison.OrdinalIgnoreCase)))
            {
                if (cleanItem.Length < 140)
                {
                    dimensions = cleanItem;
                    continue;
                }
            }

            // Kutu içeriği
            if (string.IsNullOrEmpty(included) &&
                (line.Contains("includes:", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("package includes", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("comes with", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("box includes", StringComparison.OrdinalIgnoreCase) ||
                 line.Contains("set of", StringComparison.OrdinalIgnoreCase)))
            {
                if (cleanItem.Length < 140)
                {
                    included = cleanItem;
                    continue;
                }
            }

            // Önemli ürün nitelikleri (el boyaması, LED, özel kaplama vb.)
            if (features.Count < 3 && cleanItem.Length is >= 15 and <= 120 &&
                (cleanItem.Contains("hand-painted", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("hand painted", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("handcrafted", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("high detail", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("articulated", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("magnetic", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("custom", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("textured", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("durable", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("led", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("smooth finish", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("resin", StringComparison.OrdinalIgnoreCase) ||
                 cleanItem.Contains("wood", StringComparison.OrdinalIgnoreCase)))
            {
                if (!IsSectionHeader(cleanItem) && !features.Contains(cleanItem))
                {
                    features.Add(cleanItem);
                    continue;
                }
            }

            // Genel ekstra not
            if (string.IsNullOrEmpty(extraNotes) && cleanItem.Length is >= 25 and <= 180 && !IsSectionHeader(cleanItem))
            {
                extraNotes = cleanItem;
            }
        }

        return new ExtractedDetails(dimensions, features, included, extraNotes);
    }
}
