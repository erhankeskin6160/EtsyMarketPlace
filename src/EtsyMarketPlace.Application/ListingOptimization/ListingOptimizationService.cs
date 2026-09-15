namespace EtsyMarketPlace.Application.ListingOptimization;

using System.Text.RegularExpressions;

public sealed class ListingOptimizationService
{
    private static readonly Regex WordRegex = new(@"[\p{L}\p{N}]+", RegexOptions.Compiled);
    private static readonly HashSet<string> StopWords = new(StringComparer.OrdinalIgnoreCase)
    {
        "and", "the", "for", "with", "from", "your", "you", "this", "that", "of", "to", "in", "on", "or", "a", "an",
        "ve", "ile", "icin", "bir", "bu", "da", "de", "veya", "pdf", "svg", "png",
    };

    private static readonly string[] RiskTerms =
    [
        "disney", "marvel", "dc", "pokemon", "nintendo", "harry potter", "star wars", "lotr", "lord of the rings",
        "ben 10", "omnitrix", "minecraft", "valorant", "spiderman", "spider-man", "batman", "superman",
        "captain america", "iron man", "hulk", "naruto", "demon slayer", "god of war",
    ];

    public ListingOptimizationResult Optimize(ListingOptimizationInput input)
    {
        var targetTerms = Tokenize(input.TargetKeyword).ToList();
        var titleTerms = Tokenize(input.Title).ToList();
        var descriptionTerms = Tokenize(input.Description).ToList();
        var tagTerms = input.Tags.SelectMany(Tokenize).ToList();
        var allTerms = titleTerms.Concat(descriptionTerms).Concat(tagTerms).ToList();
        var strongTerms = BuildStrongTerms(targetTerms, titleTerms, tagTerms);
        var missingTerms = strongTerms
            .Where(term => !ContainsTerm(allTerms, term))
            .Take(8)
            .ToList();
        var suggestedTags = BuildTagSuggestions(input, strongTerms);
        var suggestedTitles = BuildTitleSuggestions(input, strongTerms, suggestedTags);
        var suggestedMaterials = BuildMaterialSuggestions(input);
        var descriptionDraft = BuildDescriptionDraft(input, strongTerms, suggestedTags);
        var currentScore = Score(input.Title, input.Description, input.Tags, targetTerms);
        var optimizedScore = Score(
            suggestedTitles.FirstOrDefault() ?? input.Title,
            descriptionDraft,
            suggestedTags,
            targetTerms);

        return new ListingOptimizationResult(
            currentScore,
            Math.Max(currentScore, optimizedScore),
            suggestedTitles,
            suggestedTags,
            suggestedMaterials,
            descriptionDraft,
            missingTerms,
            BuildRiskWarnings(input),
            BuildChecklist(input, currentScore, optimizedScore, suggestedTags, missingTerms));
    }

    private static int Score(string title, string description, IReadOnlyList<string> tags, IReadOnlyList<string> targetTerms)
    {
        var score = 0m;
        var titleTerms = Tokenize(title).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var tagText = string.Join(' ', tags);
        var tagTerms = Tokenize(tagText).ToHashSet(StringComparer.OrdinalIgnoreCase);

        score += title.Length is >= 55 and <= 135 ? 22 : title.Length is >= 35 and <= 140 ? 15 : 7;
        score += targetTerms.Count > 0 && targetTerms.All(titleTerms.Contains) ? 18 : targetTerms.Count(term => titleTerms.Contains(term)) * 6;
        score += tags.Count >= 13 ? 18 : tags.Count * 18m / 13m;
        score += tags.Count(tag => tag.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2) >= 5 ? 12 : 6;
        score += targetTerms.Count > 0 && targetTerms.Any(tagTerms.Contains) ? 12 : 0;
        score += description.Length >= 500 ? 12 : description.Length >= 250 ? 8 : 3;
        score += tags.All(tag => tag.Length <= 20) ? 6 : 0;
        return (int)Math.Round(Math.Clamp(score, 0m, 100m));
    }

    private static IReadOnlyList<string> BuildStrongTerms(
        IReadOnlyList<string> targetTerms,
        IReadOnlyList<string> titleTerms,
        IReadOnlyList<string> tagTerms)
    {
        return targetTerms
            .Concat(titleTerms)
            .Concat(tagTerms)
            .Where(term => term.Length > 2 && !StopWords.Contains(term))
            .GroupBy(term => term, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(group => targetTerms.Contains(group.Key, StringComparer.OrdinalIgnoreCase) ? 100 + group.Count() : group.Count())
            .ThenBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .Select(group => group.Key)
            .Take(18)
            .ToList();
    }

    private static IReadOnlyList<string> BuildTagSuggestions(ListingOptimizationInput input, IReadOnlyList<string> strongTerms)
    {
        var rawText = $"{input.Title} {input.TargetKeyword} {string.Join(' ', input.Tags)} {input.Description}";
        var titleTerms = Tokenize(input.Title).ToList();

        var multiWordCandidates = new List<string>();

        // 1. Kullanıcının mevcut 2+ kelimelik uygun tagleri
        foreach (var tag in input.Tags)
        {
            var norm = NormalizeTag(tag);
            if (norm.Length is >= 4 and <= 20 && norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            {
                multiWordCandidates.Add(norm);
            }
        }

        // 2. Hedef anahtar kelime
        var targetNorm = NormalizeTag(input.TargetKeyword);
        if (targetNorm.Length is >= 4 and <= 20 && targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
        {
            multiWordCandidates.Add(targetNorm);
        }

        // 3. Başlıktan 2'li ve 3'lü ardışık kelime öbekleri (N-gram)
        for (int i = 0; i < titleTerms.Count; i++)
        {
            if (i + 1 < titleTerms.Count)
            {
                var twoWord = $"{titleTerms[i]} {titleTerms[i + 1]}";
                if (twoWord.Length is >= 5 and <= 20) multiWordCandidates.Add(twoWord);
            }
            if (i + 2 < titleTerms.Count)
            {
                var threeWord = $"{titleTerms[i]} {titleTerms[i + 1]} {titleTerms[i + 2]}";
                if (threeWord.Length is >= 8 and <= 20) multiWordCandidates.Add(threeWord);
            }
        }

        // 4. Güçlü kelimelerden 2'li ve 3'lü kombinasyonlar
        for (int i = 0; i < strongTerms.Count; i++)
        {
            for (int j = i + 1; j < strongTerms.Count; j++)
            {
                var pair1 = $"{strongTerms[i]} {strongTerms[j]}";
                if (pair1.Length is >= 6 and <= 20) multiWordCandidates.Add(pair1);

                if (j + 1 < strongTerms.Count)
                {
                    var triple = $"{strongTerms[i]} {strongTerms[j]} {strongTerms[j + 1]}";
                    if (triple.Length is >= 8 and <= 20) multiWordCandidates.Add(triple);
                }
            }
        }

        // 5. Niche ve Kategoriye Özel Zengin Long-Tail Havuzu
        var fallbackPool = new List<string>
        {
            "fantasy desk decor",
            "hand painted statue",
            "3d printed model",
            "collectible figure",
            "geeky boyfriend gift",
            "nerdy room decor",
            "tabletop miniature",
            "custom display prop",
            "movie fan gift",
            "fantasy home art",
            "unique gamer gift",
            "handmade collector",
            "shelf decor prop"
        };

        foreach (var fallback in fallbackPool)
        {
            multiWordCandidates.Add(fallback);
        }

        return multiWordCandidates
            .Select(t => t.Trim().ToLowerInvariant())
            .Where(t => t.Length is >= 4 and <= 20 && t.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();
    }

    private static IReadOnlyList<string> BuildTitleSuggestions(
        ListingOptimizationInput input,
        IReadOnlyList<string> strongTerms,
        IReadOnlyList<string> suggestedTags)
    {
        var rawBaseTitle = SanitizeSourceTitle(input.Title);
        var primaryPart = rawBaseTitle.Split(['|', '-', ',', '–', '—', '/'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? CleanPhrase(input.TargetKeyword);

        if (primaryPart.Length < 10 && rawBaseTitle.Length > primaryPart.Length)
        {
            primaryPart = rawBaseTitle;
        }

        var tagsTitleCased = suggestedTags
            .Select(ToTitleCase)
            .Where(t => !primaryPart.Contains(t, StringComparison.OrdinalIgnoreCase))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        var tag1 = tagsTitleCased.ElementAtOrDefault(0) ?? "Handcrafted Design";
        var tag2 = tagsTitleCased.ElementAtOrDefault(1) ?? "Unique Gift Idea";
        var tag3 = tagsTitleCased.ElementAtOrDefault(2) ?? "Display Prop & Decor";
        var tag4 = tagsTitleCased.ElementAtOrDefault(3) ?? "Collector Edition";
        var tag5 = tagsTitleCased.ElementAtOrDefault(4) ?? "Premium Finish";

        var basePrimary = ToTitleCase(primaryPart);
        var targetTitle = ToTitleCase(input.TargetKeyword);

        // 1. Altın Formül: [Ana Ürün Adı & Vurucu Özellik (İlk 40-54 Krk)] | [Özellik/Kullanım] | [Kitle/Hediye] (120-138 Karakter)
        var title1 = AssembleRichTitle(basePrimary, [tag1, tag2, $"{tag3} & {tag4}", "Gift for Fans"]);

        // 2. Arama & Anahtar Kelime Odaklı 2. Başlık
        var baseWithKeyword = basePrimary.Contains(targetTitle, StringComparison.OrdinalIgnoreCase)
            ? basePrimary
            : $"{basePrimary} - {targetTitle}";
        var title2 = AssembleRichTitle(baseWithKeyword, [tag2, tag4, $"{tag1} Room Decor", "Unique Gift"]);

        // 3. Hediye & Niche Kullanım Odaklı 3. Başlık
        var giftHook = $"{basePrimary} | {tag3}";
        var title3 = AssembleRichTitle(giftHook, [tag1, "Perfect Gift for Collectors & Fans", tag5, tag2]);

        return new[] { title1, title2, title3 }
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string AssembleRichTitle(string primaryHook, IReadOnlyList<string> parts, int maxLen = 138)
    {
        var result = primaryHook.Trim();
        foreach (var part in parts)
        {
            if (string.IsNullOrWhiteSpace(part)) continue;
            var trimmed = part.Trim();
            if (result.Contains(trimmed, StringComparison.OrdinalIgnoreCase)) continue;

            var test = $"{result} | {trimmed}";
            if (test.Length <= maxLen)
            {
                result = test;
            }
        }
        return LimitTitle(result);
    }

    private static string SanitizeSourceTitle(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "Etsy Handmade Product";
        var clean = raw.Trim();
        var junkPatterns = new[]
        {
            "OUTPUT LANGUAGE: English only. Do not write Turkish.",
            "OUTPUT LANGUAGE: English only.",
            "OUTPUT LANGUAGE:",
            "English only.",
            "Do not write Turkish.",
            "Selected marketplace listing:",
            "Etsy search keyword:",
            "Use the selected listing as the product reference",
            "Infer the best Etsy product category",
        };

        foreach (var junk in junkPatterns)
        {
            if (clean.Contains(junk, StringComparison.OrdinalIgnoreCase))
            {
                clean = clean.Replace(junk, "", StringComparison.OrdinalIgnoreCase);
            }
        }

        clean = clean.Trim().TrimStart('|', '-', ':', ' ').TrimEnd('|', '-', ':', ' ');
        return clean.Length > 0 ? clean : "Etsy Handmade Product";
    }

    private static IReadOnlyList<string> BuildMaterialSuggestions(ListingOptimizationInput input)
    {
        var blob = $"{input.Title} {input.Description} {string.Join(' ', input.Tags)}";
        var candidates = new List<string>();
        AddIfMentioned(blob, candidates, "resin", "resin");
        AddIfMentioned(blob, candidates, "pla", "pla");
        AddIfMentioned(blob, candidates, "plastic", "plastic");
        AddIfMentioned(blob, candidates, "wood", "wood");
        AddIfMentioned(blob, candidates, "metal", "metal");
        AddIfMentioned(blob, candidates, "acrylic", "acrylic");
        AddIfMentioned(blob, candidates, "filament", "filament");
        AddIfMentioned(blob, candidates, "paint", "paint");
        AddIfMentioned(blob, candidates, "stl", "digital file");
        AddIfMentioned(blob, candidates, "svg", "digital file");
        AddIfMentioned(blob, candidates, "pdf", "digital file");

        return candidates
            .Where(material => material.Length is >= 2 and <= 45)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();
    }

    private static void AddIfMentioned(string blob, List<string> candidates, string needle, string material)
    {
        if (blob.Contains(needle, StringComparison.OrdinalIgnoreCase))
        {
            candidates.Add(material);
        }
    }

    private static string BuildDescriptionDraft(
        ListingOptimizationInput input,
        IReadOnlyList<string> strongTerms,
        IReadOnlyList<string> suggestedTags)
    {
        var materials = BuildMaterialSuggestions(input);
        var formatted = EtsyDescriptionFormatter.FormatToStandardTemplate(
            input.Description,
            input.Title,
            suggestedTags,
            materials,
            input.TargetKeyword);

        return $"{formatted}\r\n\r\nPublishing review: check dimensions, materials, and copyright policies before publishing.";
    }

    private static string SanitizeSourceDescription(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return "";
        var lines = raw.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries);
        var clean = new List<string>();

        foreach (var line in lines)
        {
            var trimmed = line.Trim();
            if (trimmed.StartsWith("Selected listing title:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Etsy search keyword:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Current competitor category:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Competitor description:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("OUTPUT LANGUAGE:", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Do not write Turkish", StringComparison.OrdinalIgnoreCase) ||
                trimmed.StartsWith("Publishing review:", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            clean.Add(line);
        }

        return string.Join(" ", clean).Trim();
    }

    private static IReadOnlyList<string> BuildRiskWarnings(ListingOptimizationInput input)
    {
        var blob = $"{input.Title} {input.Description} {string.Join(' ', input.Tags)} {input.TargetKeyword}";
        return RiskTerms
            .Where(term => blob.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Select(term => $"Marka/telif riski kontrol edilmeli: {term}")
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(10)
            .ToList();
    }

    private static IReadOnlyList<string> BuildChecklist(
        ListingOptimizationInput input,
        int currentScore,
        int optimizedScore,
        IReadOnlyList<string> suggestedTags,
        IReadOnlyList<string> missingTerms)
    {
        var items = new List<string>
        {
            optimizedScore > currentScore
                ? $"SEO puani {currentScore}/100 -> {optimizedScore}/100 seviyesine cikarilabilir."
                : $"Mevcut SEO puani {currentScore}/100; buyuk revizyon yerine ince ayar onerilir.",
            suggestedTags.Count >= 13 ? "13 Etsy tag alanini dolu kullan." : "Eksik tag alanlarini tamamla.",
            input.Title.Length > 140 ? "Basligi Etsy limitine uygun kisalt." : "Basligi 55-135 karakter araliginda tut.",
        };
        if (missingTerms.Count > 0) items.Add($"Eksik niyet kelimelerini ekle: {string.Join(", ", missingTerms.Take(5))}");
        items.Add("Yayinlamadan once marka/telif ve yasakli ifade kontrolu yap.");
        return items;
    }

    private static IEnumerable<string> BuildPhrases(IReadOnlyList<string> terms)
    {
        for (var index = 0; index < terms.Count - 1; index++)
        {
            yield return $"{terms[index]} {terms[index + 1]}";
        }
    }

    private static bool ContainsTerm(IEnumerable<string> terms, string candidate) =>
        terms.Any(term => term.Equals(candidate, StringComparison.OrdinalIgnoreCase));

    private static IEnumerable<string> Tokenize(string value) =>
        WordRegex.Matches(value.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(word => word.Length > 2 && !StopWords.Contains(word));

    private static string NormalizeTag(string value)
    {
        var words = Tokenize(value).Take(4).ToList();
        var tag = string.Join(' ', words);
        return tag.Length <= 20 ? tag : string.Join(' ', words.Take(3));
    }

    private static string CleanPhrase(string value)
    {
        var cleaned = Regex.Replace(value.Trim(), @"\s+", " ");
        return cleaned.Length > 0 ? cleaned : "etsy product";
    }

    private static string LimitTitle(string value)
    {
        var title = CleanPhrase(value);
        return title.Length <= 140 ? title : title[..140].TrimEnd();
    }

    private static string ToTitleCase(string text)
    {
        if (string.IsNullOrWhiteSpace(text)) return text;
        var words = text.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var minorWords = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "a", "an", "the", "and", "but", "or", "for", "nor", "on", "at", "to", "from", "by", "with", "in", "of" };
        
        for (int i = 0; i < words.Length; i++)
        {
            var word = words[i];
            if (i > 0 && minorWords.Contains(word))
            {
                words[i] = word.ToLowerInvariant();
            }
            else if (word.Length > 0)
            {
                words[i] = char.ToUpperInvariant(word[0]) + (word.Length > 1 ? word[1..] : "");
            }
        }
        return string.Join(' ', words);
    }
}
