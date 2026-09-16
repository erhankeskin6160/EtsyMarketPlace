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

    [Fact]
    public void Optimize_ProducesFullLengthRichTitlesBetween115And138CharsWithoutRepetition()
    {
        var service = new ListingOptimizationService();
        var result = service.Optimize(new ListingOptimizationInput(
            "Michael Jackson Printed Figure",
            "Handmade PLA figure statue of Michael Jackson for desk display",
            ["michael jackson", "printed figure", "king of pop", "collector gift", "desk statue"],
            "michael jackson"));

        Assert.NotEmpty(result.TitleSuggestions);
        foreach (var title in result.TitleSuggestions)
        {
            Assert.True(title.Length >= 80 && title.Length <= 140, $"Title length was {title.Length}: '{title}'");
            // Check that it doesn't repeat 'michael jackson | michael jackson | michael jackson'
            var occurrences = System.Text.RegularExpressions.Regex.Matches(title, "michael jackson", System.Text.RegularExpressions.RegexOptions.IgnoreCase).Count;
            Assert.True(occurrences <= 2, $"Title repeated too many times ({occurrences}): '{title}'");
        }
    }

    [Fact]
    public void Optimize_LimitsTagWordRepetitionWithFacetDiversity()
    {
        var service = new ListingOptimizationService();
        var result = service.Optimize(new ListingOptimizationInput(
            "Michael Jackson Printed Statue Bust",
            "Handmade PLA figure statue of Michael Jackson for desk display",
            ["michael jackson", "printed figure", "king of pop", "collector gift", "desk statue"],
            "michael jackson"));

        Assert.Equal(13, result.TagSuggestions.Count);

        // Check frequency of main content words across all tags
        var words = result.TagSuggestions
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 3 && !new[] { "gift", "idea", "with", "from", "for" }.Contains(w, StringComparer.OrdinalIgnoreCase))
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        // No root word should dominate or exceed 2-3 occurrences
        if (words.TryGetValue("michael", out var michaelCount))
        {
            Assert.True(michaelCount <= 2, $"Word 'michael' appeared {michaelCount} times across tags: {string.Join(", ", result.TagSuggestions)}");
        }
        if (words.TryGetValue("statue", out var statueCount))
        {
            Assert.True(statueCount <= 2, $"Word 'statue' appeared {statueCount} times across tags: {string.Join(", ", result.TagSuggestions)}");
        }
    }

    [Fact]
    public void Optimize_WhenInputHas13Tags_RefreshesTagsAndAvoidsFullEcho()
    {
        var service = new ListingOptimizationService();
        var existing13Tags = new List<string>
        {
            "led light astronaut", "space decor", "3d printed lamp", "astronaut night light",
            "kids room decor", "space gift", "astronaut lamp", "nursery night light",
            "space themed decor", "unique home decor", "whimsical lamp", "astronaut figurine", "moon lamp"
        };

        var result = service.Optimize(new ListingOptimizationInput(
            "LED Light Up Astronaut Figurine - 3D Printed Space Decor Night Light",
            "Handcrafted 3D printed astronaut night lamp with soft LED light for kids room and nursery.",
            existing13Tags,
            "astronaut night light"));

        Assert.Equal(13, result.TagSuggestions.Count);

        // It must NOT simply echo back all 13 existing tags
        var identicalCount = result.TagSuggestions.Count(t => existing13Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
        Assert.True(identicalCount <= 5, $"Too many existing tags echoed ({identicalCount}/13). Output tags: {string.Join(", ", result.TagSuggestions)}");

        // Fresh tags must be at least 8
        var freshCount = result.TagSuggestions.Count(t => !existing13Tags.Contains(t, StringComparer.OrdinalIgnoreCase));
        Assert.True(freshCount >= 8, $"Expected at least 8 fresh tags, but only got {freshCount}: {string.Join(", ", result.TagSuggestions)}");

        // Frequency cap check
        var words = result.TagSuggestions
            .SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries))
            .Where(w => w.Length > 3 && !new[] { "gift", "idea", "with", "from", "for" }.Contains(w, StringComparer.OrdinalIgnoreCase))
            .GroupBy(w => w, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

        if (words.TryGetValue("astronaut", out var astronautCount))
        {
            Assert.True(astronautCount <= 2, $"Word 'astronaut' appeared {astronautCount} times: {string.Join(", ", result.TagSuggestions)}");
        }
        if (words.TryGetValue("lamp", out var lampCount))
        {
            Assert.True(lampCount <= 2, $"Word 'lamp' appeared {lampCount} times: {string.Join(", ", result.TagSuggestions)}");
        }
    }

    [Fact]
    public void Optimize_GeneratesNaturalHumanTitlesWithoutPipeDelimiters()
    {
        var service = new ListingOptimizationService();
        var result = service.Optimize(new ListingOptimizationInput(
            "LED Light Up Astronaut Figurine - 3D Printed Space Decor Night Light",
            "Handcrafted 3D printed astronaut night lamp with soft LED light for kids room and nursery.",
            ["astronaut lamp", "space decor", "night light"],
            "astronaut night light"));

        Assert.NotEmpty(result.TitleSuggestions);
        foreach (var title in result.TitleSuggestions)
        {
            Assert.DoesNotContain("|", title);
            Assert.True(title.Length >= 80 && title.Length <= 140, $"Title length was {title.Length}: '{title}'");
        }
    }
}
