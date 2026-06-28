namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.ExternalMarketplaces;
using EtsyMarketPlace.Infrastructure.ExternalMarketplaces;
using SimilarProductsWinForms.Models;

internal sealed class ExternalMarketplaceSearchService
{
    private readonly ExternalMarketplaceOpportunityService opportunityService = new(
    [
        new GoogleShoppingMarketplaceProvider(),
        new EbayMarketplaceProvider(new EbayApiClient(), EbayApiSettingsStore.Load),
        new TrendyolMarketplaceProvider(),
        new HepsiburadaMarketplaceProvider(),
        new GoogleWebMarketplaceProvider(),
    ]);

    public IReadOnlyList<string> SourceNames => opportunityService.SourceNames;

    public IReadOnlyList<ExternalProductIdea> BuildSearchIdeas(
        string shopType,
        string keyword,
        IReadOnlyCollection<string> enabledSources) =>
        opportunityService
            .Search(new ExternalMarketplaceSearchRequest(shopType, keyword, enabledSources))
            .Select(ToViewModel)
            .ToList();

    private static ExternalProductIdea ToViewModel(ExternalMarketplaceOpportunity opportunity)
    {
        var product = opportunity.Product;
        var riskTerms = opportunity.RiskTerms.Count == 0
            ? "Yok"
            : string.Join(", ", opportunity.RiskTerms);

        return new ExternalProductIdea
        {
            Source = product.Source,
            Title = product.Title,
            SearchUrl = product.SearchUrl,
            ProductUrl = product.ProductUrl,
            Price = product.Price > 0 ? $"Tahmini USD {product.Price:0.##}" : "Kaynakta kontrol",
            SellerName = product.SellerName,
            ImageUrl = product.ImageUrl,
            Category = opportunity.Category,
            Tags = string.Join(", ", opportunity.Tags.Take(13)),
            OpportunityScore = opportunity.OpportunityScore,
            EtsyFitScore = opportunity.EtsyFitScore,
            RiskScore = opportunity.RiskScore,
            DemandScore = opportunity.ScoreBreakdown.Demand,
            CompetitionAdvantageScore = opportunity.ScoreBreakdown.CompetitionAdvantage,
            SeoGapScore = opportunity.ScoreBreakdown.SeoGap,
            PricePotentialScore = opportunity.ScoreBreakdown.PricePotential,
            Decision = opportunity.Decision,
            RiskTerms = riskTerms,
            Reasons = string.Join(" | ", opportunity.Reasons.Take(4)),
            ScoreDetails = string.Join(" | ", opportunity.ScoreBreakdown.Summary),
            Notes = $"{product.Notes}. {opportunity.Decision}. Skor: {string.Join(" | ", opportunity.ScoreBreakdown.Summary)}. Kaynakta urunu ac, gercek fiyat/gorsel/satici bilgisini dogrula. Risk: {(opportunity.RiskTerms.Count == 0 ? "belirgin risk yok" : riskTerms)}.",
        };
    }
}
