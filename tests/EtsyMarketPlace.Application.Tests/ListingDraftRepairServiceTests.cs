namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ListingDraftRepairServiceTests
{
    private readonly ListingDraftRepairService _repairService = new();

    // --- Evaluate ---

    [Fact]
    public void Evaluate_HighScoreReport_DoesNotNeedRepair()
    {
        var report = _repairService.ValidateDraft(
            "Hand Painted Resin Dragon Bust for Fantasy Shelf Decor Collectors Gift",
            "This beautifully crafted dragon bust is made from high-quality resin and hand painted with intricate detail.\n\nPerfect for fantasy lovers and collectors who want a unique shelf piece.\n\nMaterials: Premium casting resin with acrylic paint finish. Approximate dimensions: 6 x 4 x 5 inches.\n\nPublishing review: No known brand or IP conflicts detected.",
            CreateFullTags(),
            ["resin", "acrylic paint"],
            "Sculptures & Figurines",
            "dragon bust");

        var decision = _repairService.Evaluate(report);

        Assert.False(decision.NeedsRepair);
        Assert.Contains("gerekmiyor", decision.Summary, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Evaluate_LowScoreReport_NeedsRepair()
    {
        var report = _repairService.ValidateDraft(
            "",
            "Kisa aciklama",
            ["tag1"],
            [],
            "",
            "dragon bust");

        var decision = _repairService.Evaluate(report);

        Assert.True(decision.NeedsRepair);
        Assert.NotEmpty(decision.RepairTargets);
    }

    [Fact]
    public void Evaluate_LowTitleScore_IncludesTitleInRepairTargets()
    {
        var report = _repairService.ValidateDraft(
            "",
            "This dragon bust is a premium hand-painted resin collectible.\n\nPerfect for fantasy enthusiasts and shelf decor lovers.\n\nMade from high-quality casting resin.\n\nNo known IP risks.",
            CreateFullTags(),
            ["resin"],
            "Figurines",
            "dragon bust");

        var decision = _repairService.Evaluate(report);

        Assert.True(decision.NeedsRepair);
        Assert.Contains("title", decision.RepairTargets);
    }

    [Fact]
    public void Evaluate_LowTagScore_IncludesTagsInRepairTargets()
    {
        var report = _repairService.ValidateDraft(
            "x",
            "y",
            ["this tag is way too long for etsy limit", "another extremely long tag name over limit"],
            [],
            "",
            "dragon bust");

        var decision = _repairService.Evaluate(report);

        Assert.True(decision.NeedsRepair);
        Assert.Contains("tags", decision.RepairTargets);
    }

    [Fact]
    public void Evaluate_BrandRisk_DetectsRiskTerms()
    {
        var report = _repairService.ValidateDraft(
            "Disney Frozen Elsa Dragon Bust Marvel Spider-Man",
            "Disney Frozen Elsa themed dragon. Marvel Spider-Man inspired design.\n\nA collectible piece.\n\nMade from resin.\n\nNo review notes.",
            ["disney", "frozen", "elsa", "marvel", "spider-man", "tag6", "tag7", "tag8", "tag9", "tag10", "tag11", "tag12", "tag13"],
            ["resin"],
            "Figurines",
            "dragon bust");

        // Risk score should be low due to many brand terms
        Assert.True(report.Risk.Score < 70);
        Assert.NotEqual("Dusuk", report.Risk.RiskLevel);
        Assert.True(report.Risk.DetectedTerms.Count >= 3);
    }

    // --- BuildRepairPrompt ---

    [Fact]
    public void BuildRepairPrompt_ContainsRepairModeMarker()
    {
        var report = _repairService.ValidateDraft(
            "",
            "Short",
            ["tag1"],
            [],
            "",
            "test keyword");
        var decision = _repairService.Evaluate(report);

        var prompt = ListingDraftRepairService.BuildRepairPrompt(
            decision,
            "",
            "Short",
            ["tag1"],
            [],
            "test keyword");

        Assert.Contains("REPAIR MODE", prompt);
    }

    [Fact]
    public void BuildRepairPrompt_ContainsJsonSchemaKeys()
    {
        var report = _repairService.ValidateDraft(
            "Short title",
            "Short",
            ["tag1"],
            [],
            "",
            "test keyword");
        var decision = _repairService.Evaluate(report);

        var prompt = ListingDraftRepairService.BuildRepairPrompt(
            decision,
            "Short title",
            "Short",
            ["tag1"],
            [],
            "test keyword");

        Assert.Contains("title_suggestions", prompt);
        Assert.Contains("tag_suggestions", prompt);
        Assert.Contains("description_draft", prompt);
        Assert.Contains("risk_warnings", prompt);
    }

    [Fact]
    public void BuildRepairPrompt_ContainsSpecificIssues()
    {
        var report = _repairService.ValidateDraft(
            "",
            "Kisa",
            ["tag1"],
            [],
            "",
            "dragon bust");
        var decision = _repairService.Evaluate(report);

        var prompt = ListingDraftRepairService.BuildRepairPrompt(
            decision,
            "",
            "Kisa",
            ["tag1"],
            [],
            "dragon bust");

        Assert.Contains("Issues to fix:", prompt);
        Assert.Contains("TITLE REPAIR:", prompt);
    }

    [Fact]
    public void BuildRepairPrompt_IncludesTargetKeyword()
    {
        var report = _repairService.ValidateDraft(
            "x",
            "y",
            ["z"],
            [],
            "",
            "fantasy sword");
        var decision = _repairService.Evaluate(report);

        var prompt = ListingDraftRepairService.BuildRepairPrompt(
            decision,
            "x",
            "y",
            ["z"],
            [],
            "fantasy sword");

        Assert.Contains("fantasy sword", prompt);
    }

    [Fact]
    public void BuildRepairPrompt_ContainsCurrentDraftContext()
    {
        var report = _repairService.ValidateDraft(
            "Old title here",
            "Old description",
            ["old tag"],
            ["old material"],
            "",
            "keyword");
        var decision = _repairService.Evaluate(report);

        var prompt = ListingDraftRepairService.BuildRepairPrompt(
            decision,
            "Old title here",
            "Old description",
            ["old tag"],
            ["old material"],
            "keyword");

        Assert.Contains("Old title here", prompt);
        Assert.Contains("Old description", prompt);
        Assert.Contains("old tag", prompt);
        Assert.Contains("old material", prompt);
    }

    // --- Constants ---

    [Fact]
    public void RepairThreshold_Is75()
    {
        Assert.Equal(75, ListingDraftRepairService.RepairThreshold);
    }

    [Fact]
    public void MaxRepairIterations_Is2()
    {
        Assert.Equal(2, ListingDraftRepairService.MaxRepairIterations);
    }

    // --- Helpers ---

    private static List<string> CreateFullTags() =>
    [
        "dragon bust", "resin figure", "fantasy decor", "shelf decor", "collector gift",
        "dragon sculpture", "hand painted", "tabletop decor", "fantasy gift", "resin art",
        "dragon art", "gamer gift", "unique decor",
    ];
}
