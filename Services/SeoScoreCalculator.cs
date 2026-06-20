namespace SimilarProductsWinForms.Services;

using SimilarProductsWinForms.Models;

internal static class SeoScoreCalculator
{
    public static SeoScoreResult Calculate(string title, string description, IEnumerable<string> tags, string primaryKeyword)
    {
        var normalizedTitle = title.Trim();
        var normalizedDescription = description.Trim();
        var normalizedKeyword = primaryKeyword.Trim();
        var cleanTags = tags
            .Select(tag => tag.Trim())
            .Where(tag => !string.IsNullOrWhiteSpace(tag))
            .ToList();

        var score = 100;
        var strengths = new List<string>();
        var warnings = new List<string>();
        var suggestions = new List<string>();

        if (normalizedTitle.Length < 35)
        {
            score -= 12;
            warnings.Add("Baslik kisa; arama niyetini ve urun tipini daha net anlatabilir.");
            suggestions.Add("Basliga urun tipi, kullanim amaci ve ana karakter/tema bilgisini ekleyin.");
        }
        else if (normalizedTitle.Length > 140)
        {
            score -= 10;
            warnings.Add("Baslik cok uzun; alici icin keyword yigini gibi gorunebilir.");
            suggestions.Add("Basligi okunabilir 1 ana ifade ve 1-2 destekleyici ifade ile sinirlayin.");
        }
        else
        {
            strengths.Add("Baslik uzunlugu okunabilir aralikta.");
        }

        if (!string.IsNullOrWhiteSpace(normalizedKeyword) &&
            !normalizedTitle.Contains(normalizedKeyword, StringComparison.CurrentCultureIgnoreCase))
        {
            score -= 10;
            warnings.Add("Ana keyword baslikta gecmiyor.");
            suggestions.Add("Ana keywordu basligin ilk bolumune dogal sekilde yerlestirin.");
        }
        else
        {
            strengths.Add("Ana keyword baslikta geciyor.");
        }

        if (normalizedDescription.Length < 180)
        {
            score -= 12;
            warnings.Add("Aciklama kisa; malzeme, olcu, kullanim ve paket icerigi eksik kalabilir.");
            suggestions.Add("Ilk paragrafta urunu net tarif edin; sonraki satirlarda olcu, malzeme, uretim ve kargo notlarini ekleyin.");
        }
        else
        {
            strengths.Add("Aciklama yeterli detay icin uygun uzunlukta.");
        }

        var firstDescriptionPart = normalizedDescription.Length <= 220
            ? normalizedDescription
            : normalizedDescription[..220];
        if (!string.IsNullOrWhiteSpace(normalizedKeyword) &&
            !firstDescriptionPart.Contains(normalizedKeyword, StringComparison.CurrentCultureIgnoreCase))
        {
            score -= 8;
            warnings.Add("Ana keyword aciklamanin ilk bolumunde gecmiyor.");
            suggestions.Add("Aciklamanin ilk 1-2 cumlesine ana keywordu insan gibi okunacak sekilde ekleyin.");
        }

        if (cleanTags.Count < 13)
        {
            score -= (13 - cleanTags.Count) * 3;
            warnings.Add($"{13 - cleanTags.Count} tag eksik.");
            suggestions.Add("13 tag alaninin tamamini farkli arama niyetleriyle doldurun.");
        }
        else
        {
            strengths.Add("13 tag alani dolu.");
        }

        var duplicateTagCount = cleanTags
            .GroupBy(tag => tag.ToLowerInvariant())
            .Where(group => group.Count() > 1)
            .Sum(group => group.Count() - 1);
        if (duplicateTagCount > 0)
        {
            score -= duplicateTagCount * 5;
            warnings.Add("Tekrar eden tag var.");
            suggestions.Add("Tekrar eden tagleri farkli kullanim, hediye, dekor veya cosplay niyetleriyle degistirin.");
        }
        else if (cleanTags.Count > 0)
        {
            strengths.Add("Taglerde birebir tekrar yok.");
        }

        var longTailTagCount = cleanTags.Count(tag => tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2);
        if (longTailTagCount < 8)
        {
            score -= 10;
            warnings.Add("Long-tail tag sayisi dusuk.");
            suggestions.Add("Tek kelimeler yerine 'cosplay prop', 'desk decor', '3d printed gift' gibi cok kelimeli tagler kullanin.");
        }
        else
        {
            strengths.Add("Long-tail tag orani iyi.");
        }

        var overLimitTags = cleanTags.Where(tag => tag.Length > 20).ToList();
        if (overLimitTags.Count > 0)
        {
            score -= overLimitTags.Count * 2;
            warnings.Add("20 karakterden uzun tagler var; Etsy tag limiti icin bolunmeli.");
            suggestions.Add("Uzun tagleri 20 karakter altinda parcalara ayirin.");
        }

        score = Math.Clamp(score, 0, 100);
        return new SeoScoreResult(score, cleanTags.Count, duplicateTagCount, longTailTagCount, strengths, warnings, suggestions);
    }
}
