namespace EtsyMarketPlace.Application.ExternalMarketplaces;

using EtsyMarketPlace.Application.ProductOpportunity;

public sealed class ExternalMarketplaceOpportunityService(
    IEnumerable<IExternalMarketplaceProvider> providers,
    ProductOpportunityScorer? scorer = null,
    OpportunityDecisionService? decisionService = null)
{
    private readonly ProductOpportunityScorer scorer = scorer ?? new ProductOpportunityScorer();
    private readonly OpportunityDecisionService decisionService = decisionService ?? new OpportunityDecisionService();
    private readonly IReadOnlyList<IExternalMarketplaceProvider> providers = providers.ToList();

    public IReadOnlyList<string> SourceNames => providers.Select(provider => provider.Name).ToList();

    public IReadOnlyList<ExternalMarketplaceOpportunity> Search(ExternalMarketplaceSearchRequest request)
    {
        var cleanKeyword = string.IsNullOrWhiteSpace(request.Keyword) ? request.ShopType.Trim() : request.Keyword.Trim();
        if (string.IsNullOrWhiteSpace(cleanKeyword))
        {
            return [];
        }

        var context = new ExternalMarketplaceSearchContext(
            request.ShopType.Trim(),
            cleanKeyword,
            BuildQuery(request.ShopType, cleanKeyword));

        return providers
            .Where(provider => request.EnabledSources.Count == 0 ||
                request.EnabledSources.Contains(provider.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(provider => provider.Search(context))
            .Select(product => Score(product, context))
            .OrderByDescending(result => result.OpportunityScore)
            .ToList();
    }

    private ExternalMarketplaceOpportunity Score(MarketplaceProduct product, ExternalMarketplaceSearchContext context)
    {
        var category = string.IsNullOrWhiteSpace(product.CategoryHint)
            ? InferCategory(context.ShopType, context.Keyword)
            : product.CategoryHint;
        var tags = product.Tags.Count == 0
            ? BuildTags(context.ShopType, context.Keyword, product.Title)
            : product.Tags;
        var etsyFit = EtsyFitScore(context.ShopType, context.Keyword, product.Title, product.Source, product.BaseScore);
        var input = new ProductOpportunityInput(
            product.Title,
            $"{product.Notes}. Source: {product.Source}. Shop type: {context.ShopType}. Category: {category}.",
            tags,
            product.Price,
            product.DemandSignal,
            Math.Clamp(product.DemandSignal * 18, 100, 12000),
            product.ShopSignal,
            Math.Clamp(etsyFit - 8, 35, 92),
            1,
            context.Keyword,
            category);
        var score = scorer.Score(input);
        var riskTerms = scorer.DetectRiskTerms(input);
        var opportunity = Math.Clamp((int)Math.Round(score.Opportunity * 0.70m + etsyFit * 0.24m + product.BaseScore * 0.06m), 0, 100);
        var adjustedScore = score with { Opportunity = opportunity };
        var decision = decisionService.Classify(adjustedScore);

        return new ExternalMarketplaceOpportunity(
            product,
            category,
            tags.Take(13).ToList(),
            opportunity,
            etsyFit,
            score.Risk,
            score.Breakdown,
            decision.Group,
            decision.DefaultStatus,
            decision.Label,
            riskTerms,
            score.Reasons
                .Prepend(decision.Reason)
                .Take(4)
                .ToList());
    }

    public static string BuildQuery(string shopType, string keyword)
    {
        var parts = new[] { shopType.Trim(), keyword.Trim(), "product idea" }
            .Where(part => !string.IsNullOrWhiteSpace(part))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        return string.Join(' ', parts);
    }

    public static IReadOnlyList<string> BuildTags(string shopType, string keyword, string query)
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

    public static string InferCategory(string shopType, string keyword)
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

    private static int EtsyFitScore(string shopType, string keyword, string title, string sourceName, int baseScore)
    {
        var blob = $"{shopType} {keyword} {title}".ToLowerInvariant();
        var score = 38 + (baseScore / 10);
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
}
