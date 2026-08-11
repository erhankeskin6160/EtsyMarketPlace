namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Text;
using System.Text.RegularExpressions;

public static class EtsyListingKnowledgeBase
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    private static readonly string[] GenericDescriptionStarts =
    [
        "this item is prepared",
        "this item is positioned",
        "etsy-ready product listing",
        "optimized listing draft",
    ];

    private static readonly string[] RiskTerms =
    [
        "disney", "marvel", "dc", "pokemon", "nintendo", "harry potter", "star wars", "lotr", "lord of the rings",
        "ben 10", "omnitrix", "minecraft", "valorant", "spiderman", "spider-man", "batman", "superman",
        "captain america", "iron man", "hulk", "naruto", "demon slayer", "god of war", "league of legends",
        "arcane", "jack sparrow", "pirates of the caribbean",
    ];

    public static string BuildAiInstructionBlock() =>
        """
        Etsy listing knowledge:
        - Write every buyer-facing field in natural English. Do not write Turkish in title, tags, materials, or description.
        - Title: make it readable, not keyword-stuffed. Put the product identity in the first words. Keep under 140 characters and avoid vague claims such as perfect, best, official, licensed, or authentic unless proven.
        - Tags: provide up to 13 Etsy tags. Each tag must be 20 characters or less, buyer-searchable, not repetitive, and preferably multi-word when possible.
        - Description: write unique buyer-facing copy for the exact product. Use short paragraphs: what it is, who it is for, material/finish/size cues, use cases, and a final review/risk note.
        - Materials: only list materials explicitly supported by the source listing text. Never invent materials.
        - Category: infer the most relevant Etsy category from the product identity and source category; do not force unrelated categories.
        - Images: prompts must preserve the selected product shape, pose, color, proportions, and visible details. Improve background, lighting, focus, and marketplace presentation only.
        - Risk: flag brand, character, movie, game, or fan-art terms in Turkish risk_warnings instead of hiding them.
        - Never copy a competitor description verbatim. Create original wording based on observable product facts.
        """;

    public static ListingDraftQualityReport EvaluateDraft(
        string title,
        string description,
        IReadOnlyList<string> tags,
        IReadOnlyList<string> materials,
        string category,
        string targetKeyword)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        if (string.IsNullOrWhiteSpace(title))
        {
            score -= 18;
            issues.Add("Baslik bos.");
        }
        else
        {
            if (title.Length > 140)
            {
                score -= 14;
                issues.Add("Baslik Etsy 140 karakter limitini asiyor.");
            }
            else if (title.Length is >= 45 and <= 135)
            {
                strengths.Add("Baslik Etsy icin okunabilir uzunlukta.");
            }

            if (!ContainsAnyTargetTerm(title, targetKeyword))
            {
                score -= 8;
                issues.Add("Baslik ana arama niyetini yeterince tasimiyor.");
            }
        }

        if (LooksTurkish(description))
        {
            score -= 18;
            issues.Add("Aciklama Turkce ifadeler iceriyor; Etsy taslagi Ingilizce olmali.");
        }

        if (description.Length < 350)
        {
            score -= 12;
            issues.Add("Aciklama kisa; materyal, kullanim alani ve alici faydasi daha net yazilmali.");
        }
        else
        {
            strengths.Add("Aciklama yeterli detay seviyesinde.");
        }

        if (GenericDescriptionStarts.Any(start => description.Contains(start, StringComparison.OrdinalIgnoreCase)))
        {
            score -= 15;
            issues.Add("Aciklama tekrar eden sabit kalip gibi gorunuyor.");
        }

        if (tags.Count < 13)
        {
            score -= Math.Min(14, (13 - tags.Count) * 2);
            issues.Add($"Tag sayisi eksik: {tags.Count}/13.");
        }
        else
        {
            strengths.Add("13 tag alani dolu.");
        }

        var repeatedTagWords = tags
            .SelectMany(Tokenize)
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= 5)
            .Select(group => group.Key)
            .Take(3)
            .ToList();
        if (repeatedTagWords.Count > 0)
        {
            score -= 7;
            issues.Add($"Taglerde fazla tekrar var: {string.Join(", ", repeatedTagWords)}.");
        }

        if (tags.Any(tag => tag.Length > 20))
        {
            score -= 10;
            issues.Add("Bazi tagler Etsy 20 karakter limitini asiyor.");
        }

        if (materials.Count == 0)
        {
            score -= 7;
            issues.Add("Materyal alani bos; gercek materyaller belirtilmeli.");
        }

        if (string.IsNullOrWhiteSpace(category))
        {
            score -= 8;
            issues.Add("Kategori bos veya belirlenmemis.");
        }

        var riskTerms = RiskTerms
            .Where(term => ContainsTerm(title, term) || ContainsTerm(description, term) || tags.Any(tag => ContainsTerm(tag, term)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(5)
            .ToList();
        var riskLevel = riskTerms.Count switch
        {
            0 => "Dusuk",
            <= 2 => "Orta",
            _ => "Yuksek",
        };
        if (riskTerms.Count > 0)
        {
            score -= Math.Min(15, riskTerms.Count * 5);
            issues.Add($"Marka/telif riski kontrol edilmeli: {string.Join(", ", riskTerms)}.");
        }

        if (issues.Count == 0)
        {
            issues.Add("Yayin oncesi fiyat, stok, kargo profili ve gercek urun bilgilerini son kez kontrol et.");
        }

        return new ListingDraftQualityReport(
            Math.Clamp(score, 0, 100),
            riskLevel,
            issues,
            strengths.Count > 0 ? strengths : ["Taslak Etsy bilgi tabani kurallarina gore denetlendi."]);
    }

    public static string FormatReport(ListingDraftQualityReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Taslak kalite puani: {report.Score}/100 | Risk: {report.RiskLevel}");
        builder.AppendLine("Eksikler:");
        foreach (var issue in report.Issues.Take(6))
        {
            builder.AppendLine($"- {issue}");
        }

        builder.AppendLine("Artilar:");
        foreach (var strength in report.Strengths.Take(4))
        {
            builder.AppendLine($"- {strength}");
        }

        return builder.ToString().TrimEnd();
    }

    private static bool ContainsAnyTargetTerm(string value, string targetKeyword)
    {
        var targetTerms = Tokenize(targetKeyword).Where(term => term.Length > 3).ToList();
        return targetTerms.Count == 0 || targetTerms.Any(term => ContainsTerm(value, term));
    }

    private static bool ContainsTerm(string value, string term) =>
        value.Contains(term, StringComparison.OrdinalIgnoreCase);

    private static bool LooksTurkish(string value)
    {
        if (value.Contains('ı') || value.Contains('İ') || value.Contains('ğ') || value.Contains('Ğ') ||
            value.Contains('ş') || value.Contains('Ş') || value.Contains('ç') || value.Contains('Ç'))
        {
            return true;
        }

        var lower = value.ToLowerInvariant();
        return lower.Contains(" icin ", StringComparison.Ordinal) ||
            lower.Contains(" urun ", StringComparison.Ordinal) ||
            lower.Contains(" alici", StringComparison.Ordinal) ||
            lower.Contains(" taslak", StringComparison.Ordinal);
    }

    private static IEnumerable<string> Tokenize(string value) =>
        WordRegex.Matches(value.ToLowerInvariant()).Select(match => match.Value);
}

public sealed record ListingDraftQualityReport(
    int Score,
    string RiskLevel,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Strengths);
