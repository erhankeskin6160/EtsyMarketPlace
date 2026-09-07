namespace SimilarProductsWinForms.Services;

using System.Drawing;
using SimilarProductsWinForms.Models;

internal static class ListingHealthCalculator
{
    public static ListingHealthReport Calculate(MarketListingResult listing, string primaryKeyword = "")
    {
        var strengths = new List<string>();
        var warnings = new List<string>();
        var actionItems = new List<string>();

        // 1. Görsel Sağlığı (Max 20)
        var imageCount = listing.ImageUrls.Count;
        int imageScore;
        if (imageCount >= 10)
        {
            imageScore = 20;
            strengths.Add("Etsy'nin önerdiği 10 görsel alanının tamamı dolu (+20 Puan).");
        }
        else if (imageCount >= 7)
        {
            imageScore = 16;
            strengths.Add($"Görsel sayısı iyi seviyede ({imageCount}/10).");
            warnings.Add($"{10 - imageCount} adet görsel alanı boş bırakılmış.");
            actionItems.Add("Ürünün ölçek, kullanım, ambalaj ve detay fotoğraflarını ekleyerek 10 resme tamamlayın.");
        }
        else if (imageCount >= 4)
        {
            imageScore = 12;
            warnings.Add($"Yalnızca {imageCount} görsel kullanılmış (Önerilen: 10).");
            actionItems.Add("Alıcı güvenini artırmak için ürün boyutunu gösteren ve kullanım anı fotoğrafları ekleyin.");
        }
        else if (imageCount >= 1)
        {
            imageScore = 6;
            warnings.Add($"Kritik az görsel: Sadece {imageCount} görsel var.");
            actionItems.Add("Etsy arama algoritması çok görselli ürünleri öne çıkarır. Acilen en az 5-6 görsel yükleyin.");
        }
        else
        {
            imageScore = 0;
            warnings.Add("Üründe hiç görsel bulunamadı veya yüklenemedi.");
            actionItems.Add("Ürün için kaliteli kapak ve detay fotoğrafları yükleyin.");
        }

        // 2. Başlık Kalitesi & Uzunluğu (Max 20)
        var title = (listing.Title ?? "").Trim();
        int titleScore;
        if (title.Length is >= 70 and <= 135)
        {
            titleScore = 20;
            strengths.Add($"Başlık uzunluğu Etsy SEO için ideal seviyede ({title.Length} karakter).");
        }
        else if (title.Length is >= 40 and < 70)
        {
            titleScore = 15;
            strengths.Add("Başlık anlaşılır ve net.");
            warnings.Add("Başlık biraz kısa; ikincil arama niyetleri ve kullanım alanları eklenebilir.");
            actionItems.Add("Başlığa ürünün kullanılacağı ortam, hediye fikri veya stil kelimelerini ekleyin.");
        }
        else if (title.Length is > 135 and <= 140)
        {
            titleScore = 15;
            warnings.Add("Başlık Etsy limitine çok yakın (140 karakter sınırı).");
            actionItems.Add("Kelime yığılmasını önlemek için başlığın sonundaki gereksiz tekrarları temizleyin.");
        }
        else if (title.Length < 40)
        {
            titleScore = 8;
            warnings.Add("Başlık çok kısa; arama motorları için yeterli veri içermiyor.");
            actionItems.Add("Başlığa anahtar kelime öbeklerini ve ürün türünü netleştiren kelimeleri ekleyin.");
        }
        else
        {
            titleScore = 8;
            warnings.Add("Başlık 140 karakter sınırını aşıyor veya sınırda kesiliyor.");
            actionItems.Add("Başlığı 140 karakterin altına düşürün ve en önemli anahtar kelimeleri başa alın.");
        }

        if (!string.IsNullOrWhiteSpace(primaryKeyword) &&
            !title.Contains(primaryKeyword, StringComparison.CurrentCultureIgnoreCase))
        {
            titleScore = Math.Max(0, titleScore - 5);
            warnings.Add($"Hedef anahtar kelime ('{primaryKeyword}') başlıkta geçmiyor.");
            actionItems.Add($"'{primaryKeyword}' ifadesini başlığın ilk 40 karakteri içerisine yerleştirin.");
        }

        // 3. Tag (Etiket) Doluluğu & Kalitesi (Max 20)
        var tags = listing.Tags ?? [];
        var tagCount = tags.Count;
        int tagScore;
        if (tagCount >= 13)
        {
            tagScore = 10;
            strengths.Add("13 etiket hakkının tamamı kullanılmış.");
        }
        else
        {
            tagScore = Math.Max(0, (int)Math.Round(tagCount * (10.0 / 13.0)));
            warnings.Add($"Eksik etiket: 13 etiketten sadece {tagCount} tanesi tanımlı.");
            actionItems.Add($"Eksik {13 - tagCount} etiket alanını dükkan alanınızla ilgili kelimelerle doldurun.");
        }

        var longTailTags = tags.Count(t => t.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
        if (tagCount > 0)
        {
            double longTailRatio = (double)longTailTags / tagCount;
            if (longTailRatio >= 0.65)
            {
                tagScore += 10;
                strengths.Add($"Etiketlerin %{longTailRatio * 100:F0}'i çok kelimeli (Long-tail) arama kalıbı.");
            }
            else
            {
                tagScore += (int)Math.Round(longTailRatio * 10);
                warnings.Add("Tek kelimelik etiket oranı yüksek. Tek kelimeler rekabeti zorlaştırır.");
                actionItems.Add("Tek kelimelik etiketler yerine 'gift for him', 'custom desk decor' gibi 2-3 kelimeli öbekler seçin.");
            }
        }

        // 4. Açıklama Derinliği (Max 15)
        var desc = (listing.Description ?? "").Trim();
        int descScore;
        if (desc.Length >= 1000)
        {
            descScore = 15;
            strengths.Add($"Açıklama metni oldukça detaylı ve zengin ({desc.Length} karakter).");
        }
        else if (desc.Length >= 500)
        {
            descScore = 11;
            strengths.Add("Açıklama yeterli uzunlukta.");
            actionItems.Add("Açıklamaya SSS (Sıkça Sorulan Sorular), kargo süreleri ve bakım/temizlik talimatları ekleyin.");
        }
        else if (desc.Length >= 200)
        {
            descScore = 7;
            warnings.Add("Açıklama kısa. Ölçü, malzeme ve kargo detayları eksik olabilir.");
            actionItems.Add("Açıklamayı en az 500 karaktere çıkarın; ürün ölçüleri ve paket içeriğini ekleyin.");
        }
        else
        {
            descScore = 3;
            warnings.Add("Açıklama metni yetersiz veya boş.");
            actionItems.Add("Müşteri sorularını azaltmak ve SEO için detaylı bir açıklama metni hazırlayın.");
        }

        // 5. Stok & Varyasyon Yapısı (Max 15)
        int inventoryScore = 0;
        if (listing.Quantity > 5)
        {
            inventoryScore += 7;
            strengths.Add($"Stok durumu iyi ({listing.Quantity} adet stokta).");
        }
        else if (listing.Quantity >= 1)
        {
            inventoryScore += 4;
            warnings.Add($"Stok seviyesi kritik az ({listing.Quantity} adet kalmış).");
            actionItems.Add("Stok bittiğinde arama sıralaması düşer. Stok adedini güncelleyin.");
        }
        else
        {
            warnings.Add("Stok tükenmiş (0 stok). Ürün aramada görünmeyebilir.");
            actionItems.Add("Ürünü tekrar satışa açmak için stok girin.");
        }

        if (listing.VariationOptions.Count > 0)
        {
            inventoryScore += 8;
            strengths.Add($"Üründe {listing.VariationOptions.Count} farklı varyasyon seçeneği mevcut.");
        }
        else
        {
            inventoryScore += 4;
            actionItems.Add("Renk, boyut veya malzeme varyasyonu ekleyerek dönüşüm oranını artırabilirsiniz.");
        }

        // 6. SEOSkoru Entegrasyonu (Max 10)
        var seoCalc = SeoScoreCalculator.Calculate(title, desc, tags, primaryKeyword);
        int seoScore = (int)Math.Round(seoCalc.Score / 10.0);

        int totalScore = Math.Clamp(imageScore + titleScore + tagScore + descScore + inventoryScore + seoScore, 0, 100);

        string grade = totalScore switch
        {
            >= 90 => "A+",
            >= 80 => "A",
            >= 70 => "B",
            >= 55 => "C",
            >= 40 => "D",
            _ => "F"
        };

        string summary = totalScore switch
        {
            >= 90 => "Harika! Bu listing Etsy algoritması ve SEO kurallarına yüksek derecede uyumlu.",
            >= 75 => "Başarılı! Listing iyi durumda, ancak birkaç küçük görsel ve etiket dokunuşuyla A+ seviyesine çıkabilir.",
            >= 55 => "Geliştirilmeli. Ürünün arama motorlarında ve mağazada öne çıkması için eksikleri tamamlayın.",
            _ => "Kritik Derecede Zayıf. Başlık, etiket ve görsel alanlarında acil iyileştirme gerekiyor."
        };

        var breakdown = new List<HealthCategoryBreakdown>
        {
            new("📸 Görseller", imageScore, 20, $"{imageScore}/20 Puan ({imageCount}/10 Görsel)", GetScoreColor(imageScore, 20)),
            new("🔤 Başlık", titleScore, 20, $"{titleScore}/20 Puan ({title.Length} Karakter)", GetScoreColor(titleScore, 20)),
            new("🏷️ Etiketler", tagScore, 20, $"{tagScore}/20 Puan ({tagCount}/13 Tag, {longTailTags} Long-tail)", GetScoreColor(tagScore, 20)),
            new("📝 Açıklama", descScore, 15, $"{descScore}/15 Puan ({desc.Length} Karakter)", GetScoreColor(descScore, 15)),
            new("📦 Stok & Varyasyon", inventoryScore, 15, $"{inventoryScore}/15 Puan (Stok: {listing.Quantity})", GetScoreColor(inventoryScore, 15)),
            new("⚡ SEO Uyum Sinyali", seoScore, 10, $"{seoScore}/10 Puan (SEO Skor: {seoCalc.Score})", GetScoreColor(seoScore, 10))
        };

        return new ListingHealthReport
        {
            TotalScore = totalScore,
            Grade = grade,
            SummaryText = summary,
            ImageScore = imageScore,
            TitleScore = titleScore,
            TagScore = tagScore,
            DescriptionScore = descScore,
            InventoryScore = inventoryScore,
            SeoScore = seoScore,
            BreakdownList = breakdown,
            Strengths = strengths,
            Warnings = warnings,
            ActionItems = actionItems
        };
    }

    private static Color GetScoreColor(int score, int max)
    {
        double ratio = (double)score / max;
        if (ratio >= 0.85) return Color.FromArgb(16, 185, 129); // Yeşil
        if (ratio >= 0.60) return Color.FromArgb(245, 158, 11); // Turuncu
        return Color.FromArgb(239, 68, 68); // Kırmızı
    }
}
