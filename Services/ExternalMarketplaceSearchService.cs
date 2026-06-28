namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.ProductOpportunity;
using SimilarProductsWinForms.Models;

internal sealed class ExternalMarketplaceSearchService
{
    private static readonly IReadOnlyList<ExternalSource> Sources =
    [
        new("Google Shopping", "https://www.google.com/search?tbm=shop&q={0}", 78),
        new("Trendyol", "https://www.trendyol.com/sr?q={0}", 72),
        new("Hepsiburada", "https://www.hepsiburada.com/ara?q={0}", 70),
        new("eBay", "https://www.ebay.com/sch/i.html?_nkw={0}", 76),
        new("Google Web", "https://www.google.com/search?q={0}", 68),
    ];

    public IReadOnlyList<string> SourceNames => Sources.Select(source => source.Name).ToList();

    public IReadOnlyList<ExternalProductIdea> BuildSearchIdeas(
        string shopType,
        string keyword,
        IReadOnlyCollection<string> enabledSources)
    {
        var cleanKeyword = string.IsNullOrWhiteSpace(keyword) ? shopType : keyword;
        if (string.IsNullOrWhiteSpace(cleanKeyword))
        {
            return [];
        }

        var query = BuildQuery(shopType, cleanKeyword);
        return Sources
            .Where(source => enabledSources.Count == 0 || enabledSources.Contains(source.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(source => BuildSourceIdeas(source, query, shopType, cleanKeyword))
            .OrderByDescending(idea => idea.OpportunityScore)
            .ToList();
    }

    private static IReadOnlyList<ExternalProductIdea> BuildSourceIdeas(
        ExternalSource source,
        string query,
        string shopType,
        string keyword)
    {
        var variants = new[]
        {
            new QueryVariant(query, "Genel urun aramasi", 0, 35),
            new QueryVariant($"{query} best seller", "Cok satan sinyali aramasi", 4, 55),
            new QueryVariant($"{query} handmade custom", "El yapimi/Etsy uyumu aramasi", 6, 68),
            new QueryVariant($"{query} low competition niche", "Nis bosluk aramasi", 5, 48),
        };

        return variants
            .Select(variant =>
            {
                var scorer = new ProductOpportunityScorer();
                var encoded = Uri.EscapeDataString(variant.Query);
                var url = string.Format(source.SearchUrlFormat, encoded);
                var title = $"{keyword.Trim()} - {source.Name} {variant.Label}";
                var category = InferCategory(shopType, keyword);
                var tags = BuildTags(shopType, keyword, variant.Query);
                var etsyFit = EtsyFitScore(shopType, keyword, variant.Query, source.Name);
                var input = new ProductOpportunityInput(
                    title,
                    $"{variant.Label}. Source: {source.Name}. Shop type: {shopType}. Category: {category}.",
                    tags,
                    variant.PriceEstimate,
                    EstimatedFavorites(source.Name, variant),
                    EstimatedViews(source.Name, variant),
                    EstimatedShopSales(source.Name, variant),
                    Math.Clamp(etsyFit - 8, 35, 92),
                    1,
                    keyword,
                    category);
                var score = scorer.Score(input);
                var riskTerms = scorer.DetectRiskTerms(input);
                var opportunity = Math.Clamp((int)Math.Round(score.Opportunity * 0.72m + etsyFit * 0.28m + source.BaseScore * 0.08m), 0, 100);
                return new ExternalProductIdea
                {
                    Source = source.Name,
                    Title = title,
                    SearchUrl = url,
                    ProductUrl = url,
                    Price = variant.PriceEstimate > 0 ? $"Tahmini USD {variant.PriceEstimate:0.##}" : "Kaynakta kontrol",
                    SellerName = "Kaynakta secilecek",
                    Category = category,
                    Tags = string.Join(", ", tags.Take(13)),
                    OpportunityScore = opportunity,
                    EtsyFitScore = etsyFit,
                    RiskScore = score.Risk,
                    Decision = score.Decision,
                    RiskTerms = riskTerms.Count == 0 ? "Yok" : string.Join(", ", riskTerms),
                    Reasons = string.Join(" | ", score.Reasons.Take(4)),
                    Notes = $"{variant.Label}. {score.Decision}. Kaynakta urunu ac, gercek fiyat/gorsel/satici bilgisini dogrula. Risk: {(riskTerms.Count == 0 ? "belirgin risk yok" : string.Join(", ", riskTerms))}.",
                };
            })
            .ToList();
    }

    private static int ShopTypeBonus(string shopType, string sourceName)
    {
        var text = shopType.ToLowerInvariant();
        if ((text.Contains("3d") || text.Contains("cosplay") || text.Contains("prop")) &&
            sourceName.Contains("eBay", StringComparison.OrdinalIgnoreCase))
        {
            return 5;
        }

        if ((text.Contains("yatak") || text.Contains("bed")) &&
            (sourceName.Contains("Trendyol", StringComparison.OrdinalIgnoreCase) ||
             sourceName.Contains("Hepsiburada", StringComparison.OrdinalIgnoreCase)))
        {
            return 5;
        }

        return 0;
    }

    private static string BuildQuery(string shopType, string keyword)
    {
        var parts = new[] { shopType.Trim(), keyword.Trim(), "product idea" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(' ', parts);
    }

    private static IReadOnlyList<string> BuildTags(string shopType, string keyword, string query)
    {
        var baseTerms = $"{shopType}, {keyword}, {query}, handmade, gift, custom, decor";
        return baseTerms
            .Split([',', ' '], StringSplitOptions.RemoveEmptyEntries)
            .Select(term => term.Trim().ToLowerInvariant())
            .Where(term => term.Length is >= 3 and <= 20)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .Take(13)
            .ToList();
    }

    private static string InferCategory(string shopType, string keyword)
    {
        var blob = $"{shopType} {keyword}".ToLowerInvariant();
        if (blob.Contains("3d") || blob.Contains("cosplay") || blob.Contains("prop") || blob.Contains("figure") || blob.Contains("bust"))
        {
            return "Art & Collectibles > Sculpture > Figurines";
        }

        if (blob.Contains("bed") || blob.Contains("yatak") || blob.Contains("pillow") || blob.Contains("duvet"))
        {
            return "Home & Living > Bedding";
        }

        if (blob.Contains("jewelry") || blob.Contains("ring") || blob.Contains("necklace"))
        {
            return "Jewelry";
        }

        if (blob.Contains("digital") || blob.Contains("svg") || blob.Contains("template"))
        {
            return "Craft Supplies & Tools > Patterns & How To";
        }

        return "Art & Collectibles";
    }

    private static int EtsyFitScore(string shopType, string keyword, string query, string sourceName)
    {
        var blob = $"{shopType} {keyword} {query}".ToLowerInvariant();
        var score = 42;
        foreach (var term in new[] { "handmade", "custom", "personalized", "gift", "decor", "cosplay", "prop", "3d", "figure", "bust", "wood", "linen", "vintage" })
        {
            if (blob.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                score += 7;
            }
        }

        if (sourceName.Contains("eBay", StringComparison.OrdinalIgnoreCase))
        {
            score += 5;
        }

        if (blob.Contains("official") || blob.Contains("licensed") || blob.Contains("replica"))
        {
            score -= 18;
        }

        return Math.Clamp(score, 0, 100);
    }

    private static int EstimatedFavorites(string sourceName, QueryVariant variant) =>
        Math.Clamp(sourceName.Length * 3 + variant.Bonus * 11, 5, 220);

    private static int EstimatedViews(string sourceName, QueryVariant variant) =>
        Math.Clamp(sourceName.Length * 120 + variant.Bonus * 260, 120, 5200);

    private static int EstimatedShopSales(string sourceName, QueryVariant variant) =>
        Math.Clamp(sourceName.Length * 95 + variant.Bonus * 340, 80, 6500);

    private sealed record ExternalSource(string Name, string SearchUrlFormat, int BaseScore);

    private sealed record QueryVariant(string Query, string Label, int Bonus, decimal PriceEstimate);
}
