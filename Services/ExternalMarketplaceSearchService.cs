namespace SimilarProductsWinForms.Services;

using EtsyMarketPlace.Application.ExternalMarketplaces;
using EtsyMarketPlace.Application.Automation;
using EtsyMarketPlace.Domain.ProductOpportunity;
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
    private readonly OpportunityAutomationService automationService = new();

    public IReadOnlyList<string> SourceNames => opportunityService.SourceNames;

    public IReadOnlyList<ExternalProductIdea> BuildSearchIdeas(
        string shopType,
        string keyword,
        IReadOnlyCollection<string> enabledSources) =>
        opportunityService
            .Search(new ExternalMarketplaceSearchRequest(shopType, keyword, enabledSources))
            .Select(ToViewModel)
            .ToList();

    private ExternalProductIdea ToViewModel(ExternalMarketplaceOpportunity opportunity)
    {
        var product = opportunity.Product;
        var recommendation = automationService.Recommend(new OpportunityAutomationInput(
            product.Title,
            opportunity.OpportunityScore,
            opportunity.ScoreBreakdown.Demand,
            opportunity.RiskScore,
            opportunity.EtsyFitScore,
            opportunity.DecisionGroup,
            opportunity.SuggestedStatus));
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
            DecisionGroup = DecisionGroupLabel(opportunity.DecisionGroup),
            SuggestedStatus = StatusLabel(opportunity.SuggestedStatus),
            UserStatus = StatusLabel(opportunity.SuggestedStatus),
            RecommendedAction = recommendation.Action,
            ActionPriority = recommendation.Priority,
            ActionReason = recommendation.Reason,
            Decision = opportunity.Decision,
            RiskTerms = riskTerms,
            Reasons = string.Join(" | ", opportunity.Reasons.Take(4)),
            ScoreDetails = string.Join(" | ", opportunity.ScoreBreakdown.Summary),
            Notes = $"{product.Notes}. {opportunity.Decision}. Skor: {string.Join(" | ", opportunity.ScoreBreakdown.Summary)}. Kaynakta urunu ac, gercek fiyat/gorsel/satici bilgisini dogrula. Risk: {(opportunity.RiskTerms.Count == 0 ? "belirgin risk yok" : riskTerms)}.",
        };
    }

    public OpportunityAutomationReport BuildAutomationReport(IReadOnlyList<ExternalProductIdea> ideas)
    {
        var inputs = ideas.Select(idea => new OpportunityAutomationInput(
            idea.Title,
            idea.OpportunityScore,
            idea.DemandScore,
            idea.RiskScore,
            idea.EtsyFitScore,
            ParseDecisionGroup(idea.DecisionGroup),
            ParseStatus(idea.UserStatus)));
        return automationService.BuildReport(inputs);
    }

    public static string DecisionGroupLabel(OpportunityDecisionGroup group) => group switch
    {
        OpportunityDecisionGroup.StrongOpportunity => "Guclu firsat",
        OpportunityDecisionGroup.WorthTesting => "Test edilebilir",
        OpportunityDecisionGroup.Risky => "Riskli",
        OpportunityDecisionGroup.Weak => "Zayif / beklet",
        _ => "Tum kararlar",
    };

    public static string StatusLabel(OpportunityUserStatus status) => status switch
    {
        OpportunityUserStatus.Watch => "Izle",
        OpportunityUserStatus.TestList => "Test listesi",
        OpportunityUserStatus.DraftGenerated => "Taslak uretildi",
        OpportunityUserStatus.AddedToEtsyDraft => "Etsy taslak eklendi",
        OpportunityUserStatus.Rejected => "Reddedildi",
        _ => "Yeni",
    };

    private static OpportunityDecisionGroup ParseDecisionGroup(string label) => label switch
    {
        "Guclu firsat" => OpportunityDecisionGroup.StrongOpportunity,
        "Test edilebilir" => OpportunityDecisionGroup.WorthTesting,
        "Riskli" => OpportunityDecisionGroup.Risky,
        "Zayif / beklet" => OpportunityDecisionGroup.Weak,
        _ => OpportunityDecisionGroup.All,
    };

    private static OpportunityUserStatus ParseStatus(string label) => label switch
    {
        "Izle" => OpportunityUserStatus.Watch,
        "Test listesi" => OpportunityUserStatus.TestList,
        "Taslak uretildi" => OpportunityUserStatus.DraftGenerated,
        "Etsy taslak eklendi" => OpportunityUserStatus.AddedToEtsyDraft,
        "Reddedildi" => OpportunityUserStatus.Rejected,
        _ => OpportunityUserStatus.New,
    };
}
