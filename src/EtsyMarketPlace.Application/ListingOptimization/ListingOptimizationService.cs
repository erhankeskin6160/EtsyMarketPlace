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
        var candidates = input.Tags
            .Concat([input.TargetKeyword])
            .Concat(strongTerms)
            .Concat(BuildPhrases(strongTerms))
            .Select(NormalizeTag)
            .Where(tag => tag.Length is >= 2 and <= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();

        return candidates.Count > 0 ? candidates : ["etsy product", "gift idea", "handmade"];
    }

    private static IReadOnlyList<string> BuildTitleSuggestions(
        ListingOptimizationInput input,
        IReadOnlyList<string> strongTerms,
        IReadOnlyList<string> suggestedTags)
    {
        var target = CleanPhrase(input.TargetKeyword);
        var baseTerms = strongTerms
            .Where(term => !target.Contains(term, StringComparison.OrdinalIgnoreCase))
            .Take(5)
            .ToList();
        var modifier = suggestedTags.FirstOrDefault(tag => tag.Split(' ').Length >= 2) ?? suggestedTags.FirstOrDefault() ?? "gift";
        return new[]
        {
            LimitTitle($"{target} {string.Join(' ', baseTerms.Take(3))} - {modifier}"),
            LimitTitle($"{target} for gift, {string.Join(' ', baseTerms.Take(4))}"),
            LimitTitle($"{CleanPhrase(input.Title)} | {string.Join(' ', suggestedTags.Take(3))}"),
        }
            .Where(title => !string.IsNullOrWhiteSpace(title))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(3)
            .ToList();
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
        var target = CleanPhrase(input.TargetKeyword);
        var benefits = string.Join(", ", strongTerms.Take(6));
        var tags = string.Join(", ", suggestedTags.Take(8));
        return
            $"{target} icin optimize edilmis listeleme taslagi.{Environment.NewLine}{Environment.NewLine}" +
            $"Bu urun {benefits} arayan alicilar icin konumlandirilir. Baslik, etiket ve aciklama ayni ana niyeti destekleyecek sekilde yazilmalidir.{Environment.NewLine}{Environment.NewLine}" +
            $"Kullanim alanlari: hediye, koleksiyon, dekor, dijital proje veya kisiye ozel sunum senaryosuna gore netlestirilebilir.{Environment.NewLine}{Environment.NewLine}" +
            $"One cikarilacak SEO kelimeleri: {tags}.{Environment.NewLine}{Environment.NewLine}" +
            "Not: Marka, karakter veya telifli evren isimleri kullaniliyorsa listing yayinlanmadan once hak sahipligi ve Etsy politika riski kontrol edilmelidir.";
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
}
