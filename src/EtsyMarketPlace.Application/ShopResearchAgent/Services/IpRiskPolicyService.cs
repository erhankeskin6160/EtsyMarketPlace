namespace EtsyMarketPlace.Application.ShopResearchAgent.Services;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using EtsyMarketPlace.Domain.ShopResearchAgent;

public sealed record IpRiskEvaluationResult(
    int RiskScore,
    IpRiskDecision Decision,
    IReadOnlyList<string> Reasons);

/// <summary>
/// 3-tier Intellectual Property & Trademark Risk Policy Engine (Allow / Review / Block).
/// Designed with asymmetric safety: false negatives (missed trademark infringement) are strictly penalized.
/// </summary>
public sealed class IpRiskPolicyService
{
    private static readonly string[] HardBlockBrandsAndIps =
    [
        "disney", "mickey", "minnie", "marvel", "avengers", "spiderman", "spider-man", "batman", "superman",
        "iron man", "captain america", "hulk", "pokemon", "pikachu", "nintendo", "mario", "zelda",
        "harry potter", "hogwarts", "star wars", "darth vader", "yoda", "mandalorian", "lotr",
        "lord of the rings", "gandalf", "sauron", "minecraft", "roblox", "fortnite", "valorant",
        "naruto", "one piece", "demon slayer", "dragon ball", "goku", "sanrio", "hello kitty", "kuromi",
        "lego", "barbie", "nike", "adidas", "gucci", "louis vuitton", "chanel", "rolex", "apple",
        "iphone", "playstation", "xbox", "nfl", "nba", "fifa", "formula 1", "f1", "taylor swift",
        "studio ghibli", "totoro", "god of war", "ben 10", "omnitrix", "jack sparrow", "warhammer"
    ];

    private static readonly string[] ReviewTriggerPhrases =
    [
        "inspired by", "official", "licensed", "replica", "dupe", "fan art", "fanart", "cosplay prop", "logo"
    ];

    public IpRiskEvaluationResult Evaluate(
        string title,
        string? description,
        IReadOnlyList<string>? tags,
        IReadOnlyList<string>? shopForbiddenKeywords = null)
    {
        var rawBlob = $"{title} {description} {string.Join(" ", tags ?? Array.Empty<string>())}";
        var normalized = NormalizeText(rawBlob);
        var compact = normalized.Replace(" ", string.Empty);

        var reasons = new List<string>();
        var hardHits = 0;
        var reviewHits = 0;

        foreach (var brand in HardBlockBrandsAndIps)
        {
            var normBrand = NormalizeText(brand);
            if (ContainsWholePhrase(normalized, normBrand) || (normBrand.Length >= 6 && compact.Contains(normBrand.Replace(" ", string.Empty), StringComparison.Ordinal)))
            {
                hardHits++;
                reasons.Add($"Telif/Marka Koruması (Kesin Blok): '{brand}' tespit edildi.");
            }
        }

        if (shopForbiddenKeywords != null)
        {
            foreach (var kw in shopForbiddenKeywords.Where(k => !string.IsNullOrWhiteSpace(k)))
            {
                var normKw = NormalizeText(kw);
                if (!string.IsNullOrWhiteSpace(normKw) && ContainsWholePhrase(normalized, normKw))
                {
                    hardHits++;
                    reasons.Add($"Mağaza Yasaklı Kelimesi: '{kw.Trim()}' tespit edildi.");
                }
            }
        }

        foreach (var phrase in ReviewTriggerPhrases)
        {
            var normPhrase = NormalizeText(phrase);
            if (ContainsWholePhrase(normalized, normPhrase))
            {
                reviewHits++;
                reasons.Add($"Politika İnceleme Uyarısı: '{phrase}' ifadesi manuel kontrol gerektirir.");
            }
        }

        if (hardHits > 0)
        {
            var score = Math.Min(100, 75 + (hardHits * 10));
            return new IpRiskEvaluationResult(score, IpRiskDecision.Block, reasons);
        }

        if (reviewHits > 0)
        {
            var score = Math.Min(65, 35 + (reviewHits * 12));
            return new IpRiskEvaluationResult(score, IpRiskDecision.Review, reasons);
        }

        return new IpRiskEvaluationResult(0, IpRiskDecision.Allow, Array.Empty<string>());
    }

    private static string NormalizeText(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var lower = input.ToLowerInvariant();
        var cleaned = Regex.Replace(lower, @"[^a-z0-9\s]", " ");
        return Regex.Replace(cleaned, @"\s+", " ").Trim();
    }

    private static bool ContainsWholePhrase(string haystack, string needle)
    {
        if (string.IsNullOrEmpty(haystack) || string.IsNullOrEmpty(needle)) return false;
        return Regex.IsMatch(haystack, $@"(?:^|\s){Regex.Escape(needle)}(?:$|\s)");
    }
}
