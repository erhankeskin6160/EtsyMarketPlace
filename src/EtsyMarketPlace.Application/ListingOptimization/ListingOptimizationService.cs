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
        var blob = $"{input.Title} {input.TargetKeyword} {string.Join(' ', input.Tags)} {input.Description}".ToLowerInvariant();
        var titleTerms = Tokenize(input.Title).ToList();
        var candidateList = new List<string>();

        // 1. Hedef anahtar kelime
        var targetNorm = NormalizeTag(input.TargetKeyword);
        if (targetNorm.Length is >= 4 and <= 20 && targetNorm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
        {
            candidateList.Add(targetNorm);
        }

        // 2. Kullanıcının/rakibin mevcut 2+ kelimelik uygun tagleri
        foreach (var tag in input.Tags)
        {
            var norm = NormalizeTag(tag);
            if (norm.Length is >= 4 and <= 20 && norm.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            {
                candidateList.Add(norm);
            }
        }

        // 3. Başlıktan 2'li ve 3'lü ardışık kelime öbekleri (N-gram)
        for (int i = 0; i < titleTerms.Count; i++)
        {
            if (i + 1 < titleTerms.Count)
            {
                var twoWord = $"{titleTerms[i]} {titleTerms[i + 1]}";
                if (twoWord.Length is >= 5 and <= 20) candidateList.Add(twoWord);
            }
            if (i + 2 < titleTerms.Count)
            {
                var threeWord = $"{titleTerms[i]} {titleTerms[i + 1]} {titleTerms[i + 2]}";
                if (threeWord.Length is >= 8 and <= 20) candidateList.Add(threeWord);
            }
        }

        // 4. Ürün Temasına Özel Zengin Facet Tagleri (Müzik, Lamba, Cosplay, Takı, Dekor vb.)
        var themeTags = GetThemeSpecificTags(blob);
        candidateList.AddRange(themeTags);

        // 5. Malzeme & İşçilik Facet Tagleri
        var materials = BuildMaterialSuggestions(input);
        foreach (var mat in materials)
        {
            candidateList.Add($"handmade {mat}");
            candidateList.Add($"{mat} art piece");
            candidateList.Add($"custom {mat} craft");
        }

        // 6. Alıcı & Hediye Facet Tagleri
        candidateList.Add("unique gift idea");
        candidateList.Add("birthday gift idea");
        candidateList.Add("collector gift idea");
        candidateList.Add("gift for him or her");
        candidateList.Add("thoughtful present");

        // 7. Mekan & Sergileme Facet Tagleri
        candidateList.Add("home shelf decor");
        candidateList.Add("living room display");
        candidateList.Add("studio desk accent");
        candidateList.Add("aesthetic desk art");
        candidateList.Add("modern tabletop art");

        // 8. Güçlü kelimelerden ikili kombinasyonlar
        for (int i = 0; i < strongTerms.Count; i++)
        {
            for (int j = i + 1; j < strongTerms.Count; j++)
            {
                var pair = $"{strongTerms[i]} {strongTerms[j]}";
                if (pair.Length is >= 5 and <= 20) candidateList.Add(pair);
            }
        }

        // Frequency Cap & Diversity Algoritması (Tekrarları önleme)
        return FilterByFrequencyCap(candidateList, 13);
    }

    private static IReadOnlyList<string> FilterByFrequencyCap(IEnumerable<string> candidates, int targetCount = 13)
    {
        var accepted = new List<string>();
        var wordCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        var cleanedCandidates = candidates
            .Select(c => c.Trim().ToLowerInvariant())
            .Where(c => c.Length is >= 4 and <= 20 && c.Split(' ', StringSplitOptions.RemoveEmptyEntries).Length >= 2)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // 1. Geçiş: Her anlamlı kelimenin en fazla 2 kez geçmesine izin ver
        foreach (var cand in cleanedCandidates)
        {
            var words = Tokenize(cand).ToList();
            if (words.Any(w => wordCounts.TryGetValue(w, out var count) && count >= 2))
            {
                continue; // Kök kelime zaten 2 kez kullanılmış, çeşitlilik için atla!
            }

            accepted.Add(cand);
            foreach (var w in words)
            {
                wordCounts[w] = wordCounts.GetValueOrDefault(w) + 1;
            }

            if (accepted.Count >= targetCount) break;
        }

        // 2. Geçiş: Eğer 13 tag dolmadıysa, sınırı 3'e esnet
        if (accepted.Count < targetCount)
        {
            foreach (var cand in cleanedCandidates)
            {
                if (accepted.Contains(cand, StringComparer.OrdinalIgnoreCase)) continue;

                var words = Tokenize(cand).ToList();
                if (words.Any(w => wordCounts.TryGetValue(w, out var count) && count >= 3))
                {
                    continue;
                }

                accepted.Add(cand);
                foreach (var w in words)
                {
                    wordCounts[w] = wordCounts.GetValueOrDefault(w) + 1;
                }

                if (accepted.Count >= targetCount) break;
            }
        }

        // 3. Geçiş: Kalan boşlukları doldur
        if (accepted.Count < targetCount)
        {
            foreach (var cand in cleanedCandidates)
            {
                if (!accepted.Contains(cand, StringComparer.OrdinalIgnoreCase))
                {
                    accepted.Add(cand);
                    if (accepted.Count >= targetCount) break;
                }
            }
        }

        return accepted.Take(targetCount).ToList();
    }

    private static List<string> GetThemeSpecificTags(string blob)
    {
        var list = new List<string>();

        if (blob.Contains("michael jackson") || blob.Contains("singer") || blob.Contains("musician") ||
            blob.Contains("king of pop") || blob.Contains("music legend") || blob.Contains("guitar") ||
            blob.Contains("vinyl") || blob.Contains("pop star") || blob.Contains("concert"))
        {
            list.AddRange([
                "80s music icon",
                "pop legend tribute",
                "retro music art",
                "vinyl lover gift",
                "studio desk display",
                "music room decor",
                "vintage pop star",
                "rock memorabilia",
                "pop culture bust",
                "iconic singer art"
            ]);
        }
        else if (blob.Contains("lamp") || blob.Contains("light") || blob.Contains("lantern") || blob.Contains("glow"))
        {
            list.AddRange([
                "ambient night light",
                "aesthetic room lamp",
                "cozy bedside light",
                "modern table lamp",
                "nursery night light",
                "mood lighting lamp",
                "warm ambient glow",
                "housewarming lamp"
            ]);
        }
        else if (blob.Contains("cosplay") || blob.Contains("prop") || blob.Contains("helmet") || blob.Contains("sword"))
        {
            list.AddRange([
                "cosplay display prop",
                "wearable prop replica",
                "convention costume",
                "theatrical prop art",
                "detailed scale model",
                "collector display prop"
            ]);
        }
        else if (blob.Contains("necklace") || blob.Contains("jewelry") || blob.Contains("ring") || blob.Contains("pendant"))
        {
            list.AddRange([
                "artisan jewelry",
                "statement necklace",
                "handcrafted pendant",
                "delicate charm gift",
                "everyday accessory",
                "custom jewelry gift"
            ]);
        }
        else if (blob.Contains("gaming") || blob.Contains("gamer") || blob.Contains("anime") || blob.Contains("manga"))
        {
            list.AddRange([
                "battlestation decor",
                "gamer room accessory",
                "anime fan gift",
                "geeky desk display",
                "video game art prop",
                "collector figure prop"
            ]);
        }
        else
        {
            list.AddRange([
                "handcrafted sculpture",
                "artisan shelf accent",
                "living room art",
                "modern home decor",
                "unique desk display",
                "aesthetic showpiece"
            ]);
        }

        return list;
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

        var basePrimary = ToTitleCase(primaryPart);
        var targetTitle = ToTitleCase(input.TargetKeyword);

        var nonPrimaryTags = suggestedTags
            .Select(ToTitleCase)
            .Where(t => !basePrimary.Contains(t, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var tagCraft = nonPrimaryTags.FirstOrDefault(t => t.Contains("Resin", StringComparison.OrdinalIgnoreCase) || t.Contains("Wood", StringComparison.OrdinalIgnoreCase) || t.Contains("Print", StringComparison.OrdinalIgnoreCase) || t.Contains("Craft", StringComparison.OrdinalIgnoreCase) || t.Contains("Hand", StringComparison.OrdinalIgnoreCase))
            ?? nonPrimaryTags.ElementAtOrDefault(0) ?? "Handcrafted Design";

        var tagDisplay = nonPrimaryTags.FirstOrDefault(t => t != tagCraft && (t.Contains("Decor", StringComparison.OrdinalIgnoreCase) || t.Contains("Desk", StringComparison.OrdinalIgnoreCase) || t.Contains("Display", StringComparison.OrdinalIgnoreCase) || t.Contains("Art", StringComparison.OrdinalIgnoreCase) || t.Contains("Room", StringComparison.OrdinalIgnoreCase) || t.Contains("Lamp", StringComparison.OrdinalIgnoreCase)))
            ?? nonPrimaryTags.ElementAtOrDefault(1) ?? "Display Art & Decor";

        var tagGift = nonPrimaryTags.FirstOrDefault(t => t != tagCraft && t != tagDisplay && (t.Contains("Gift", StringComparison.OrdinalIgnoreCase) || t.Contains("Fan", StringComparison.OrdinalIgnoreCase) || t.Contains("Collector", StringComparison.OrdinalIgnoreCase) || t.Contains("Present", StringComparison.OrdinalIgnoreCase) || t.Contains("Tribute", StringComparison.OrdinalIgnoreCase)))
            ?? nonPrimaryTags.ElementAtOrDefault(2) ?? "Unique Collector Gift";

        var tagStyle = nonPrimaryTags.FirstOrDefault(t => t != tagCraft && t != tagDisplay && t != tagGift)
            ?? nonPrimaryTags.ElementAtOrDefault(3) ?? "Premium Finish";

        // BAŞLIK 1: Arama & Yüksek Dönüşüm Odaklı (Mobil Öncelikli Hook + Zengin Niteleyiciler)
        var title1Hook = basePrimary.Length <= 45 && !basePrimary.Contains(tagCraft, StringComparison.OrdinalIgnoreCase)
            ? $"{basePrimary} - {tagCraft}"
            : basePrimary;
        var title1Candidates = new List<string> { tagDisplay, tagGift, tagStyle, "Unique Fan Present", "Artisan Collectible" };
        foreach (var t in nonPrimaryTags) if (!title1Candidates.Contains(t)) title1Candidates.Add(t);
        var title1 = AssembleFluidTitle(title1Hook, title1Candidates, 138);

        // BAŞLIK 2: Estetik, Tasarım & Sergileme Odaklı Akıcı Başlık
        var title2Hook = basePrimary.StartsWith("Handcrafted", StringComparison.OrdinalIgnoreCase) || basePrimary.StartsWith("Custom", StringComparison.OrdinalIgnoreCase)
            ? basePrimary
            : $"Handcrafted {basePrimary}";
        var title2Candidates = new List<string> { tagStyle, $"{tagDisplay} Accent", tagGift, "Detailed Craft Art", "Aesthetic Showpiece" };
        foreach (var t in nonPrimaryTags) if (!title2Candidates.Contains(t)) title2Candidates.Add(t);
        var title2 = AssembleFluidTitle(title2Hook, title2Candidates, 138);

        // BAŞLIK 3: Hediye & Hayran/Koleksiyoncu Odaklı Başlık
        var title3Hook = $"{basePrimary} - {tagGift}";
        var title3Candidates = new List<string> { tagCraft, tagDisplay, "Limited Collector Edition", "Memorable Keepsake Gift" };
        foreach (var t in nonPrimaryTags) if (!title3Candidates.Contains(t)) title3Candidates.Add(t);
        var title3 = AssembleFluidTitle(title3Hook, title3Candidates, 138);

        return new[] { title1, title2, title3 }
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
    }

    private static string AssembleFluidTitle(string hook, IReadOnlyList<string> segments, int maxLen = 138)
    {
        var result = hook.Trim();
        foreach (var seg in segments)
        {
            if (string.IsNullOrWhiteSpace(seg)) continue;
            var trimmed = seg.Trim();
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
