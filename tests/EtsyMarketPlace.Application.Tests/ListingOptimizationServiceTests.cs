namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ListingOptimizationServiceTests
{
    [Fact]
    public void Optimize_GeneratesTitleTagsAndDescription()
    {
        var service = new ListingOptimizationService();

        var result = service.Optimize(new ListingOptimizationInput(
            "Wall decor",
            "Short description",
            ["decor", "gift"],
            "dragon wall decor"));

        Assert.NotEmpty(result.TitleSuggestions);
        Assert.NotEmpty(result.TagSuggestions);
        Assert.NotNull(result.MaterialSuggestions);
        Assert.Contains(result.TagSuggestions, tag => tag.Contains("dragon", StringComparison.OrdinalIgnoreCase));
        Assert.Contains("dragon wall decor", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.True(result.OptimizedSeoScore >= result.CurrentSeoScore);
    }

    [Fact]
    public void Optimize_FlagsBrandRiskTerms()
    {
        var service = new ListingOptimizationService();

        var result = service.Optimize(new ListingOptimizationInput(
            "Ben 10 Omnitrix display stand",
            "Fan art style prop",
            ["ben 10", "omnitrix"],
            "ben 10 watch"));

        Assert.Contains(result.RiskWarnings, warning => warning.Contains("ben 10", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.RiskWarnings, warning => warning.Contains("omnitrix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Optimize_KeepsSuggestedTagsWithinEtsyLength()
    {
        var service = new ListingOptimizationService();

        var result = service.Optimize(new ListingOptimizationInput(
            "Personalized fantasy sword display wall mount gift for collectors",
            "A long description for a handmade wall mount item designed for collectors and fantasy decor.",
            ["personalized fantasy sword display", "collectible wall decoration"],
            "fantasy sword wall mount"));

        Assert.All(result.TagSuggestions, tag => Assert.InRange(tag.Length, 2, 20));
        Assert.True(result.TagSuggestions.Count <= 13);
    }

    [Fact]
    public void Optimize_SuggestsOnlyLikelyMaterials()
    {
        var service = new ListingOptimizationService();

        var result = service.Optimize(new ListingOptimizationInput(
            "3D printed resin sword display",
            "Printed with resin and hand painted.",
            ["resin prop", "painted decor"],
            "fantasy sword display"));

        Assert.Contains(result.MaterialSuggestions, material => material.Equals("resin", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(result.MaterialSuggestions, material => material.Equals("paint", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Optimize_OfflineDescriptionUsesEnglishEtsyGuidance()
    {
        var service = new ListingOptimizationService();

        var result = service.Optimize(new ListingOptimizationInput(
            "Hand painted resin dragon bust for fantasy shelf decor",
            "Made from resin and paint for collectors.",
            ["dragon bust", "resin decor", "fantasy gift"],
            "dragon bust"));

        Assert.Contains("shoppers searching", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Publishing review", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("icin optimize edilmis", result.DescriptionDraft, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void KnowledgeBase_FlagsGenericOrTurkishDrafts()
    {
        var report = EtsyListingKnowledgeBase.EvaluateDraft(
            "Dragon Bust Shelf Decor",
            "Dragon bust icin optimize edilmis listeleme taslagi. This item is prepared as an Etsy-ready product listing.",
            ["dragon bust", "resin decor"],
            ["resin"],
            "Figurines",
            "dragon bust");

        Assert.True(report.Score < 80);
        Assert.Contains(report.Issues, issue => issue.Contains("Turkce", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Issues, issue => issue.Contains("sabit kalip", StringComparison.OrdinalIgnoreCase));
    }
}
