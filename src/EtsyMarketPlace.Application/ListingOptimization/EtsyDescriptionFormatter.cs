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
                // Eğer mevcut blok madde işareti içeriyorsa her madde yeni satırda olmalı
                if (trimmed.StartsWith("• ") || currentBlock.ToString().Contains("• "))
                {
                    currentBlock.AppendLine();
                    currentBlock.Append(trimmed);
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

        var sb = new StringBuilder();

        // BÖLÜM 1: Google Meta Hook & Giriş Paragrafı
        sb.AppendLine($"Elevate your space with this unique {cleanTitle}! Tailored for shoppers looking for {target}, this handcrafted piece brings outstanding quality and distinct character to any collection or setup.");
        sb.AppendLine();

        // BÖLÜM 2: Öne Çıkan Özellikler
        sb.AppendLine("✨ WHY YOU'LL LOVE IT:");
        sb.AppendLine($"• Premium Craftsmanship: Expertly manufactured with durable {matList} for a smooth, high-detail finish.");
        sb.AppendLine("• Eye-Catching Display: Designed to stand out on your desk, shelf, gaming room, or living space.");
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
        sb.AppendLine("• Finish: Clean, hand-finished surface with vibrant, durable detailing.");
        if (!string.IsNullOrWhiteSpace(cleanSource.ExtraNotes))
        {
            sb.AppendLine($"• Note: {cleanSource.ExtraNotes}");
        }
        sb.AppendLine();

        // BÖLÜM 4: Kimler İçin Uygun / Hediye
        sb.AppendLine("🎁 PERFECT FOR:");
        sb.AppendLine("• Gamers, anime lovers, cosplay enthusiasts, and novelty decor collectors.");
        sb.AppendLine("• An unforgettable birthday, anniversary, holiday, or housewarming gift.");
        sb.AppendLine();

        // BÖLÜM 5: Güvenli Paketleme & Kargo
        sb.AppendLine("📦 PACKAGING & SHIPPING:");
        sb.AppendLine("• Carefully wrapped in multi-layer protective packaging to guarantee 100% safe worldwide arrival.");
        sb.AppendLine("• Every order includes tracked dispatch sent directly to your email upon shipment.");
        sb.AppendLine();

        // BÖLÜM 6: Özel İstekler & İletişim
        sb.AppendLine("💬 CUSTOM REQUESTS & QUESTIONS:");
        sb.AppendLine("• Looking for a custom color, size, or special personalization? Feel free to reach out anytime—we are happy to help!");

        return sb.ToString().Trim();
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

    private static (string Dimensions, string ExtraNotes) ExtractSourceDetails(string? rawDesc)
    {
        if (string.IsNullOrWhiteSpace(rawDesc)) return ("", "");

        string dimensions = "";
        string extraNotes = "";

        var lines = rawDesc.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        foreach (var l in lines)
        {
            var line = l.Trim();
            if (line.Contains("cm", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("inch", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("dimension", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("size", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("height", StringComparison.OrdinalIgnoreCase) ||
                line.Contains("width", StringComparison.OrdinalIgnoreCase))
            {
                if (string.IsNullOrEmpty(dimensions) && line.Length < 120)
                {
                    dimensions = line.TrimStart('•', '-', '*', ' ');
                }
            }
            else if (line.Length > 30 && line.Length < 180 && string.IsNullOrEmpty(extraNotes))
            {
                if (!IsSectionHeader(line))
                {
                    extraNotes = line.TrimStart('•', '-', '*', ' ');
                }
            }
        }

        return (dimensions, extraNotes);
    }
}
