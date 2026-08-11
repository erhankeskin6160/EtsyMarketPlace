namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Text;
using System.Text.RegularExpressions;

/// <summary>
/// Validates Etsy listing drafts against Seller Handbook rules and produces
/// per-field scores (title, tags, description, materials, risk) along with
/// specific actionable issues and strengths.
/// </summary>
public sealed class ListingDraftValidator
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);

    private static readonly string[] GenericDescriptionPhrases =
    [
        "this item is prepared",
        "this item is positioned",
        "etsy-ready product listing",
        "optimized listing draft",
        "this product is designed",
    ];

    private static readonly string[] RiskTerms =
    [
        "disney", "marvel", "dc", "pokemon", "nintendo", "harry potter", "star wars",
        "lotr", "lord of the rings", "ben 10", "omnitrix", "minecraft", "valorant",
        "spiderman", "spider-man", "batman", "superman", "captain america", "iron man",
        "hulk", "naruto", "demon slayer", "god of war", "league of legends", "arcane",
        "jack sparrow", "pirates of the caribbean", "frozen", "elsa", "moana",
        "transformers", "hello kitty", "sanrio", "lego", "barbie",
    ];

    private static readonly string[] SubjectiveClaims =
    [
        "perfect", "best", "amazing", "incredible", "stunning", "gorgeous",
        "beautiful", "unique", "official", "licensed", "authentic", "exclusive",
    ];

    /// <summary>
    /// Validates a listing draft and returns a detailed per-field validation report.
    /// </summary>
    public ListingDraftValidationReport Validate(ListingDraftValidationInput input)
    {
        var titleResult = ValidateTitle(input.Title, input.TargetKeyword);
        var tagResult = ValidateTags(input.Tags);
        var descriptionResult = ValidateDescription(input.Description, input.TargetKeyword);
        var materialResult = ValidateMaterials(input.Materials);
        var riskResult = ValidateRisk(input.Title, input.Description, input.Tags);

        var overallScore = CalculateOverallScore(titleResult, tagResult, descriptionResult, materialResult, riskResult);

        var allIssues = new List<string>();
        allIssues.AddRange(titleResult.Issues);
        allIssues.AddRange(tagResult.Issues);
        allIssues.AddRange(descriptionResult.Issues);
        allIssues.AddRange(materialResult.Issues);
        allIssues.AddRange(riskResult.Issues);

        var allStrengths = new List<string>();
        allStrengths.AddRange(titleResult.Strengths);
        allStrengths.AddRange(tagResult.Strengths);
        allStrengths.AddRange(descriptionResult.Strengths);
        allStrengths.AddRange(materialResult.Strengths);
        allStrengths.AddRange(riskResult.Strengths);

        if (allIssues.Count == 0)
        {
            allIssues.Add("Yayin oncesi fiyat, stok, kargo profili ve gercek urun bilgilerini son kez kontrol et.");
        }

        return new ListingDraftValidationReport(
            overallScore,
            titleResult,
            tagResult,
            descriptionResult,
            materialResult,
            riskResult,
            allIssues,
            allStrengths.Count > 0 ? allStrengths : ["Taslak Etsy kurallarina gore denetlendi."]);
    }

    /// <summary>
    /// Formats the validation report as a human-readable Turkish summary.
    /// </summary>
    public static string FormatReport(ListingDraftValidationReport report)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"Genel puan: {report.OverallScore}/100 | Risk: {report.Risk.RiskLevel}");
        builder.AppendLine();
        builder.AppendLine($"  Baslik: {report.Title.Score}/100 | Tag: {report.Tags.Score}/100 | Aciklama: {report.Description.Score}/100 | Materyal: {report.Materials.Score}/100 | Risk: {report.Risk.Score}/100");
        builder.AppendLine();

        if (report.Issues.Count > 0)
        {
            builder.AppendLine("Eksikler:");
            foreach (var issue in report.Issues.Take(8))
            {
                builder.AppendLine($"  - {issue}");
            }
        }

        if (report.Strengths.Count > 0)
        {
            builder.AppendLine("Artilar:");
            foreach (var strength in report.Strengths.Take(5))
            {
                builder.AppendLine($"  + {strength}");
            }
        }

        return builder.ToString().TrimEnd();
    }

    private static FieldValidationResult ValidateTitle(string title, string targetKeyword)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        if (string.IsNullOrWhiteSpace(title))
        {
            return new FieldValidationResult(0, ["Baslik bos."], []);
        }

        // Length check
        if (title.Length > 140)
        {
            score -= 25;
            issues.Add($"Baslik 140 karakter limitini asiyor ({title.Length} karakter).");
        }
        else if (title.Length is >= 45 and <= 135)
        {
            strengths.Add("Baslik Etsy icin ideal uzunlukta.");
        }
        else if (title.Length < 30)
        {
            score -= 10;
            issues.Add("Baslik cok kisa; urun kimligini daha iyi tanimla.");
        }

        // Keyword presence in first words
        var titleWords = Tokenize(title).ToList();
        var keywordTerms = Tokenize(targetKeyword).Where(t => t.Length > 3).ToList();
        if (keywordTerms.Count > 0)
        {
            var firstFiveWords = titleWords.Take(5).ToList();
            var keywordInFirst5 = keywordTerms.Any(term =>
                firstFiveWords.Any(w => w.Equals(term, StringComparison.OrdinalIgnoreCase)));

            if (keywordInFirst5)
            {
                strengths.Add("Anahtar kelime basligin ilk 5 kelimesinde yer aliyor.");
            }
            else if (keywordTerms.Any(term =>
                titleWords.Any(w => w.Equals(term, StringComparison.OrdinalIgnoreCase))))
            {
                score -= 5;
                issues.Add("Anahtar kelime baslikta var ama ilk 5 kelimenin disinda.");
            }
            else
            {
                score -= 15;
                issues.Add("Baslik ana anahtar kelimeyi icermiyor.");
            }
        }

        // Subjective claims check
        var foundClaims = SubjectiveClaims
            .Where(claim => title.Contains(claim, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        if (foundClaims.Count > 0)
        {
            score -= Math.Min(15, foundClaims.Count * 5);
            issues.Add($"Baslikta subjektif ifadeler var: {string.Join(", ", foundClaims)}. Bunlar aciklamaya tasinmali.");
        }

        // Keyword stuffing check (same word > 2 times)
        var repeatedWords = titleWords
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 2)
            .Select(g => g.Key)
            .Take(3)
            .ToList();
        if (repeatedWords.Count > 0)
        {
            score -= 12;
            issues.Add($"Baslikta kelime tekrari var: {string.Join(", ", repeatedWords)}.");
        }

        // Turkish check
        if (LooksTurkish(title))
        {
            score -= 20;
            issues.Add("Baslik Turkce ifadeler iceriyor; Ingilizce olmali.");
        }

        return new FieldValidationResult(Math.Clamp(score, 0, 100), issues, strengths);
    }

    private static FieldValidationResult ValidateTags(IReadOnlyList<string> tags)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        // Count check
        if (tags.Count == 0)
        {
            return new FieldValidationResult(0, ["Tag alani bos."], []);
        }

        if (tags.Count < 13)
        {
            score -= Math.Min(25, (13 - tags.Count) * 3);
            issues.Add($"Tag sayisi eksik: {tags.Count}/13.");
        }
        else
        {
            strengths.Add("13 tag alani dolu.");
        }

        // Character length check
        var longTags = tags.Where(tag => tag.Length > 20).ToList();
        if (longTags.Count > 0)
        {
            score -= Math.Min(20, longTags.Count * 5);
            issues.Add($"{longTags.Count} tag 20 karakter limitini asiyor: {string.Join(", ", longTags.Take(3))}.");
        }

        // Short/single-word tags check
        var singleWordTags = tags.Count(tag => !tag.Contains(' '));
        if (singleWordTags > tags.Count / 2 && tags.Count > 3)
        {
            score -= 10;
            issues.Add("Taglerin cogu tek kelime. Cok kelimeli long-tail arama terimleri daha etkili.");
        }
        else if (singleWordTags <= 3 && tags.Count >= 10)
        {
            strengths.Add("Taglerde cok kelimeli long-tail arama terimleri kullanilmis.");
        }

        // Repetition check
        var allTagWords = tags.SelectMany(Tokenize).ToList();
        var repeatedTagWords = allTagWords
            .GroupBy(word => word, StringComparer.OrdinalIgnoreCase)
            .Where(group => group.Count() >= 5)
            .Select(group => $"{group.Key} ({group.Count()}x)")
            .Take(3)
            .ToList();
        if (repeatedTagWords.Count > 0)
        {
            score -= 12;
            issues.Add($"Taglerde fazla kelime tekrari: {string.Join(", ", repeatedTagWords)}.");
        }

        // Duplicate tag check
        var duplicateTags = tags
            .GroupBy(tag => tag.Trim(), StringComparer.OrdinalIgnoreCase)
            .Where(g => g.Count() > 1)
            .Select(g => g.Key)
            .Take(3)
            .ToList();
        if (duplicateTags.Count > 0)
        {
            score -= 10;
            issues.Add($"Ayni tag birden fazla kullanilmis: {string.Join(", ", duplicateTags)}.");
        }

        // Turkish tag check
        if (tags.Any(tag => LooksTurkish(tag)))
        {
            score -= 15;
            issues.Add("Bazi tagler Turkce; Etsy tagleri Ingilizce olmali.");
        }

        return new FieldValidationResult(Math.Clamp(score, 0, 100), issues, strengths);
    }

    private static FieldValidationResult ValidateDescription(string description, string targetKeyword)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        if (string.IsNullOrWhiteSpace(description))
        {
            return new FieldValidationResult(0, ["Aciklama bos."], []);
        }

        // Length check
        if (description.Length < 200)
        {
            score -= 25;
            issues.Add("Aciklama cok kisa (200 karakterden az). Materyal, kullanim alani ve alici faydasi eklenmeli.");
        }
        else if (description.Length < 350)
        {
            score -= 12;
            issues.Add("Aciklama kisa; daha fazla urun detayi, olcu bilgisi ve kullanim alani eklenebilir.");
        }
        else if (description.Length >= 500)
        {
            strengths.Add("Aciklama yeterli detay seviyesinde.");
        }

        // Turkish language check
        if (LooksTurkish(description))
        {
            score -= 20;
            issues.Add("Aciklama Turkce ifadeler iceriyor; Etsy taslagi Ingilizce olmali.");
        }

        // Generic/template phrases check
        var foundGeneric = GenericDescriptionPhrases
            .Where(phrase => description.Contains(phrase, StringComparison.OrdinalIgnoreCase))
            .Take(2)
            .ToList();
        if (foundGeneric.Count > 0)
        {
            score -= 18;
            issues.Add("Aciklama tekrar eden sabit kalip iceriyor. Urune ozel ozgun metin yazilmali.");
        }

        // Keyword in first paragraph check
        var firstParagraph = description.Split('\n', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";
        var keywordTerms = Tokenize(targetKeyword).Where(t => t.Length > 3).ToList();
        if (keywordTerms.Count > 0)
        {
            var keywordInFirst = keywordTerms.Any(term =>
                firstParagraph.Contains(term, StringComparison.OrdinalIgnoreCase));
            if (keywordInFirst)
            {
                strengths.Add("Anahtar kelime aciklamanin ilk paragrafinda dogal sekilde yer aliyor.");
            }
            else
            {
                score -= 8;
                issues.Add("Anahtar kelime aciklamanin ilk paragrafinda gecmiyor.");
            }
        }

        // Paragraph structure check
        var paragraphs = description.Split('\n', StringSplitOptions.RemoveEmptyEntries)
            .Count(p => p.Trim().Length > 30);
        if (paragraphs >= 3)
        {
            strengths.Add("Aciklama iyi yapilandirilmis paragraflardan olusuyor.");
        }

        return new FieldValidationResult(Math.Clamp(score, 0, 100), issues, strengths);
    }

    private static FieldValidationResult ValidateMaterials(IReadOnlyList<string> materials)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        if (materials.Count == 0)
        {
            return new FieldValidationResult(30, ["Materyal alani bos; gercek materyaller belirtilmeli."], []);
        }

        if (materials.Count >= 2)
        {
            strengths.Add($"{materials.Count} materyal tanimlanmis.");
        }

        // Check for overly long material names
        var longMaterials = materials.Where(m => m.Length > 45).ToList();
        if (longMaterials.Count > 0)
        {
            score -= 10;
            issues.Add($"{longMaterials.Count} materyal 45 karakter limitini asiyor.");
        }

        // Check for generic/vague material names
        var vagueMaterials = new[] { "other", "various", "mixed", "material", "stuff", "diger", "malzeme" };
        if (materials.Any(m => vagueMaterials.Any(v => m.Equals(v, StringComparison.OrdinalIgnoreCase))))
        {
            score -= 15;
            issues.Add("Belirsiz materyal isimleri kullanilmis. Spesifik materyaller belirtilmeli.");
        }

        return new FieldValidationResult(Math.Clamp(score, 0, 100), issues, strengths);
    }

    private static RiskValidationResult ValidateRisk(
        string title,
        string description,
        IReadOnlyList<string> tags)
    {
        var issues = new List<string>();
        var strengths = new List<string>();
        var score = 100;

        var combinedText = $"{title} {description} {string.Join(" ", tags)}";
        var detectedTerms = RiskTerms
            .Where(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(8)
            .ToList();

        var riskLevel = detectedTerms.Count switch
        {
            0 => "Dusuk",
            <= 2 => "Orta",
            _ => "Yuksek",
        };

        if (detectedTerms.Count > 0)
        {
            score -= Math.Min(40, detectedTerms.Count * 8);
            issues.Add($"Marka/telif riski tespit edildi: {string.Join(", ", detectedTerms)}.");

            if (detectedTerms.Count >= 3)
            {
                issues.Add("Birden fazla riskli terim bulundu. Etsy IP politikasi nedeniyle listing kaldirmaya kadar gidebilir.");
            }
        }
        else
        {
            strengths.Add("Marka veya telif hakki riski tespit edilmedi.");
        }

        // Check for misleading claims
        var misleadingTerms = new[] { "official", "licensed", "authentic", "endorsed", "affiliated" };
        var foundMisleading = misleadingTerms
            .Where(term => combinedText.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(3)
            .ToList();
        if (foundMisleading.Count > 0)
        {
            score -= Math.Min(20, foundMisleading.Count * 7);
            issues.Add($"Yaniltici lisans ifadeleri tespit edildi: {string.Join(", ", foundMisleading)}. Kanitlanmadikca kullanilamaz.");
        }

        return new RiskValidationResult(Math.Clamp(score, 0, 100), riskLevel, detectedTerms, issues, strengths);
    }

    private static int CalculateOverallScore(
        FieldValidationResult title,
        FieldValidationResult tags,
        FieldValidationResult description,
        FieldValidationResult materials,
        FieldValidationResult risk)
    {
        // Weighted average: title 25%, tags 25%, description 25%, materials 10%, risk 15%
        var weighted = title.Score * 0.25
            + tags.Score * 0.25
            + description.Score * 0.25
            + materials.Score * 0.10
            + risk.Score * 0.15;
        return Math.Clamp((int)Math.Round(weighted), 0, 100);
    }

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

// --- Models ---

public sealed record ListingDraftValidationInput(
    string Title,
    string Description,
    IReadOnlyList<string> Tags,
    IReadOnlyList<string> Materials,
    string Category,
    string TargetKeyword);

public record FieldValidationResult(
    int Score,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Strengths);

public sealed record RiskValidationResult(
    int Score,
    string RiskLevel,
    IReadOnlyList<string> DetectedTerms,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Strengths) : FieldValidationResult(Score, Issues, Strengths);

public sealed record ListingDraftValidationReport(
    int OverallScore,
    FieldValidationResult Title,
    FieldValidationResult Tags,
    FieldValidationResult Description,
    FieldValidationResult Materials,
    RiskValidationResult Risk,
    IReadOnlyList<string> Issues,
    IReadOnlyList<string> Strengths);
