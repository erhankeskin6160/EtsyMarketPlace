namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.ProductOpportunity;
using SimilarProductsWinForms.Models;

internal sealed class ExternalMarketplaceSearchService
{
    private readonly ProductOpportunityScorer scorer = new();
    private readonly IReadOnlyList<IExternalMarketplaceProvider> providers =
    [
        new GoogleShoppingMarketplaceProvider(),
        new EbayMarketplaceProvider(),
        new TrendyolMarketplaceProvider(),
        new HepsiburadaMarketplaceProvider(),
        new GoogleWebMarketplaceProvider(),
    ];

    public IReadOnlyList<string> SourceNames => providers.Select(provider => provider.Name).ToList();

    public IReadOnlyList<ExternalProductIdea> BuildSearchIdeas(
        string shopType,
        string keyword,
        IReadOnlyCollection<string> enabledSources)
    {
        var cleanKeyword = string.IsNullOrWhiteSpace(keyword) ? shopType.Trim() : keyword.Trim();
        if (string.IsNullOrWhiteSpace(cleanKeyword))
        {
            return [];
        }

        var context = new ExternalMarketplaceSearchContext(shopType.Trim(), cleanKeyword, BuildQuery(shopType, cleanKeyword));
        return providers
            .Where(provider => enabledSources.Count == 0 || enabledSources.Contains(provider.Name, StringComparer.OrdinalIgnoreCase))
            .SelectMany(provider => provider.BuildCandidates(context))
            .Select(candidate => CreateIdea(candidate, context))
            .OrderByDescending(idea => idea.OpportunityScore)
            .ToList();
    }

    private ExternalProductIdea CreateIdea(ExternalMarketplaceCandidate candidate, ExternalMarketplaceSearchContext context)
    {
        var category = string.IsNullOrWhiteSpace(candidate.CategoryHint)
            ? InferCategory(context.ShopType, context.Keyword)
            : candidate.CategoryHint;
        var tags = candidate.Tags.Count == 0
            ? BuildTags(context.ShopType, context.Keyword, candidate.Title)
            : candidate.Tags;
        var etsyFit = EtsyFitScore(context.ShopType, context.Keyword, candidate.Title, candidate.Source, candidate.BaseScore);
        var input = new ProductOpportunityInput(
            candidate.Title,
            $"{candidate.Notes}. Source: {candidate.Source}. Shop type: {context.ShopType}. Category: {category}.",
            tags,
            candidate.PriceEstimate,
            candidate.DemandSignal,
            Math.Clamp(candidate.DemandSignal * 18, 100, 12000),
            candidate.ShopSignal,
            Math.Clamp(etsyFit - 8, 35, 92),
            1,
            context.Keyword,
            category);
        var score = scorer.Score(input);
        var riskTerms = scorer.DetectRiskTerms(input);
        var opportunity = Math.Clamp((int)Math.Round(score.Opportunity * 0.70m + etsyFit * 0.24m + candidate.BaseScore * 0.06m), 0, 100);

        return new ExternalProductIdea
        {
            Source = candidate.Source,
            Title = candidate.Title,
            SearchUrl = candidate.SearchUrl,
            ProductUrl = candidate.ProductUrl,
            Price = candidate.PriceEstimate > 0 ? $"Tahmini USD {candidate.PriceEstimate:0.##}" : "Kaynakta kontrol",
            SellerName = candidate.SellerName,
            ImageUrl = candidate.ImageUrl,
            Category = category,
            Tags = string.Join(", ", tags.Take(13)),
            OpportunityScore = opportunity,
            EtsyFitScore = etsyFit,
            RiskScore = score.Risk,
            Decision = score.Decision,
            RiskTerms = riskTerms.Count == 0 ? "Yok" : string.Join(", ", riskTerms),
            Reasons = string.Join(" | ", score.Reasons.Take(4)),
            Notes = $"{candidate.Notes}. {score.Decision}. Kaynakta urunu ac, gercek fiyat/gorsel/satici bilgisini dogrula. Risk: {(riskTerms.Count == 0 ? "belirgin risk yok" : string.Join(", ", riskTerms))}.",
        };
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

    private interface IExternalMarketplaceProvider
    {
        string Name { get; }

        IReadOnlyList<ExternalMarketplaceCandidate> BuildCandidates(ExternalMarketplaceSearchContext context);
    }

    private abstract class SearchUrlMarketplaceProvider : IExternalMarketplaceProvider
    {
        public abstract string Name { get; }

        protected abstract string SearchUrlFormat { get; }

        protected abstract int BaseScore { get; }

        public IReadOnlyList<ExternalMarketplaceCandidate> BuildCandidates(ExternalMarketplaceSearchContext context) =>
            Variants(context)
                .Select(variant => ToCandidate(context, variant))
                .ToList();

        protected abstract IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context);

        private ExternalMarketplaceCandidate ToCandidate(ExternalMarketplaceSearchContext context, QueryVariant variant)
        {
            var encoded = Uri.EscapeDataString(variant.Query);
            var searchUrl = string.Format(SearchUrlFormat, encoded);
            var category = string.IsNullOrWhiteSpace(variant.CategoryHint)
                ? InferCategory(context.ShopType, context.Keyword)
                : variant.CategoryHint;
            var tags = BuildTags(context.ShopType, context.Keyword, variant.Query);

            return new ExternalMarketplaceCandidate(
                Name,
                $"{context.Keyword} - {Name} {variant.Label}",
                searchUrl,
                searchUrl,
                variant.SellerName,
                "",
                category,
                tags,
                variant.PriceEstimate,
                BaseScore + variant.Bonus,
                Math.Clamp(BaseScore + variant.Bonus * 9, 20, 260),
                Math.Clamp(BaseScore * 12 + variant.Bonus * 280, 80, 7000),
                $"{variant.Label}. Bu kaynak provider'i gercek urun/fiyat/gorsel secimi icin arama sonucunu hazirlar");
        }
    }

    private sealed class GoogleShoppingMarketplaceProvider : SearchUrlMarketplaceProvider
    {
        public override string Name => "Google Shopping";

        protected override string SearchUrlFormat => "https://www.google.com/search?tbm=shop&q={0}";

        protected override int BaseScore => 78;

        protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
        [
            new(context.Query, "Genel urun aramasi", 0, 35, "Google Shopping", ""),
            new($"{context.Query} best seller", "Cok satan sinyali aramasi", 5, 55, "Google Shopping", ""),
            new($"{context.Query} handmade custom", "El yapimi/Etsy uyumu aramasi", 7, 68, "Google Shopping", ""),
        ];
    }

    private sealed class EbayMarketplaceProvider : SearchUrlMarketplaceProvider
    {
        public override string Name => "eBay";

        protected override string SearchUrlFormat => "https://www.ebay.com/sch/i.html?_nkw={0}";

        protected override int BaseScore => 76;

        protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
        [
            new(context.Query, "Pazar fiyat sinyali", 0, 42, "eBay seller", ""),
            new($"{context.Query} collectible", "Koleksiyon/nis urun sinyali", 6, 75, "eBay seller", ""),
            new($"{context.Query} custom handmade", "Etsy uyumlu varyasyon sinyali", 8, 85, "eBay seller", ""),
        ];
    }

    private sealed class TrendyolMarketplaceProvider : SearchUrlMarketplaceProvider
    {
        public override string Name => "Trendyol";

        protected override string SearchUrlFormat => "https://www.trendyol.com/sr?q={0}";

        protected override int BaseScore => 72;

        protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
        [
            new(context.Query, "Turkiye pazar fiyat kontrolu", 0, 28, "Trendyol magaza", ""),
            new($"{context.Keyword} dekor hediye", "Hediye/dekor trend kontrolu", 4, 38, "Trendyol magaza", ""),
            new($"{context.Keyword} cok satan", "Talep sinyali kontrolu", 6, 45, "Trendyol magaza", ""),
        ];
    }

    private sealed class HepsiburadaMarketplaceProvider : SearchUrlMarketplaceProvider
    {
        public override string Name => "Hepsiburada";

        protected override string SearchUrlFormat => "https://www.hepsiburada.com/ara?q={0}";

        protected override int BaseScore => 70;

        protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
        [
            new(context.Query, "Genel fiyat araligi kontrolu", 0, 30, "Hepsiburada satici", ""),
            new($"{context.Keyword} hediye", "Hediye pazari kontrolu", 4, 40, "Hepsiburada satici", ""),
            new($"{context.Keyword} dekor", "Ev/dekor uyumu kontrolu", 5, 48, "Hepsiburada satici", ""),
        ];
    }

    private sealed class GoogleWebMarketplaceProvider : SearchUrlMarketplaceProvider
    {
        public override string Name => "Google Web";

        protected override string SearchUrlFormat => "https://www.google.com/search?q={0}";

        protected override int BaseScore => 68;

        protected override IReadOnlyList<QueryVariant> Variants(ExternalMarketplaceSearchContext context) =>
        [
            new($"{context.Query} review", "Review ve yorum sinyali", 2, 35, "Web kaynagi", ""),
            new($"{context.Query} site:etsy.com/listing", "Etsy benzer listing kesfi", 7, 55, "Etsy/Web", ""),
            new($"{context.Query} trend 2026", "Yeni trend sinyali", 5, 58, "Web kaynagi", ""),
        ];
    }

    private sealed record ExternalMarketplaceSearchContext(string ShopType, string Keyword, string Query);

    private sealed record ExternalMarketplaceCandidate(
        string Source,
        string Title,
        string ProductUrl,
        string SearchUrl,
        string SellerName,
        string ImageUrl,
        string CategoryHint,
        IReadOnlyList<string> Tags,
        decimal PriceEstimate,
        int BaseScore,
        int DemandSignal,
        int ShopSignal,
        string Notes);

    private sealed record QueryVariant(
        string Query,
        string Label,
        int Bonus,
        decimal PriceEstimate,
        string SellerName,
        string CategoryHint);
}
