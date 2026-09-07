namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ListingDraftValidatorTests
{
    private readonly ListingDraftValidator _validator = new();

    private static ListingDraftValidationInput CreateInput(
        string title = "Hand Painted Resin Dragon Bust for Fantasy Shelf Decor Collectors Gift",
        string description = "This beautifully crafted dragon bust is made from high-quality resin and hand painted with intricate detail.\n\nPerfect for fantasy lovers, tabletop gamers, and collectors who want a unique shelf piece.\n\nMaterials: Premium casting resin with acrylic paint finish. Approximate dimensions: 6 x 4 x 5 inches.\n\nPublishing review: No known brand or IP conflicts detected.",
        string tags = "dragon bust,resin figure,fantasy decor,shelf decor,collector gift,dragon sculpture,hand painted,tabletop decor,fantasy gift,resin art,dragon art,gamer gift,unique decor",
        string materials = "resin,acrylic paint",
        string category = "Sculptures & Figurines",
        string targetKeyword = "dragon bust") =>
        new(
            title,
            description,
            tags.Split(',').Select(t => t.Trim()).ToList(),
            materials.Split(',').Select(m => m.Trim()).Where(m => m.Length > 0).ToList(),
            category,
            targetKeyword);

    // --- Overall ---

    [Fact]
    public void Validate_GoodListing_ReturnsHighScore()
    {
        var report = _validator.Validate(CreateInput());

        Assert.True(report.OverallScore >= 70, $"Beklenen >= 70, gelen: {report.OverallScore}");
    }

    [Fact]
    public void Validate_ReturnsAllFieldResults()
    {
        var report = _validator.Validate(CreateInput());

        Assert.NotNull(report.Title);
        Assert.NotNull(report.Tags);
        Assert.NotNull(report.Description);
        Assert.NotNull(report.Materials);
        Assert.NotNull(report.Risk);
    }

    // --- Title ---

    [Fact]
    public void Validate_EmptyTitle_TitleScoreIsZero()
    {
        var report = _validator.Validate(CreateInput(title: ""));

        Assert.Equal(0, report.Title.Score);
        Assert.Contains(report.Title.Issues, i => i.Contains("bos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TitleOver140Chars_DeductsPoints()
    {
        var longTitle = new string('A', 141);
        var report = _validator.Validate(CreateInput(title: longTitle));

        Assert.True(report.Title.Score < 80);
        Assert.Contains(report.Title.Issues, i => i.Contains("140", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TitleWithKeywordInFirstWords_AddsStrength()
    {
        var report = _validator.Validate(CreateInput(
            title: "Dragon Bust Resin Fantasy Shelf Decor",
            targetKeyword: "dragon bust"));

        Assert.Contains(report.Title.Strengths, s => s.Contains("ilk 5", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TitleWithSubjectiveClaims_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            title: "Perfect Amazing Beautiful Dragon Bust Official Licensed"));

        Assert.True(report.Title.Score < 90);
        Assert.Contains(report.Title.Issues, i => i.Contains("subjektif", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TitleWithKeywordStuffing_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            title: "Dragon Dragon Dragon Dragon Bust Display"));

        Assert.Contains(report.Title.Issues, i => i.Contains("tekrar", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TurkishTitle_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            title: "Ejderha büstü reçine dekorasyon hediye ürün"));

        Assert.True(report.Title.Score < 80);
        Assert.Contains(report.Title.Issues, i => i.Contains("Turkce", StringComparison.OrdinalIgnoreCase));
    }

    // --- Tags ---

    [Fact]
    public void Validate_EmptyTags_TagScoreIsLow()
    {
        var report = _validator.Validate(CreateInput(
            tags: "a"));
        // A single 1-char tag gets heavy penalties: count < 13, and minimal value
        Assert.True(report.Tags.Score < 80);
    }

    [Fact]
    public void Validate_FewerThan13Tags_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(tags: "tag1,tag2,tag3"));

        Assert.True(report.Tags.Score < 80);
        Assert.Contains(report.Tags.Issues, i => i.Contains("eksik", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_13Tags_AddsStrength()
    {
        var report = _validator.Validate(CreateInput());

        Assert.Contains(report.Tags.Strengths, s => s.Contains("13", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_TagOver20Chars_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            tags: "this is a very long tag name that exceeds limit,tag2,tag3,tag4,tag5,tag6,tag7,tag8,tag9,tag10,tag11,tag12,tag13"));

        Assert.Contains(report.Tags.Issues, i => i.Contains("20 karakter", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_DuplicateTags_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            tags: "dragon bust,dragon bust,resin,fantasy,gift,shelf,art,decor,collect,unique,home,hand,paint"));

        Assert.Contains(report.Tags.Issues, i => i.Contains("birden fazla", StringComparison.OrdinalIgnoreCase));
    }

    // --- Description ---

    [Fact]
    public void Validate_EmptyDescription_DescriptionScoreIsZero()
    {
        var report = _validator.Validate(CreateInput(description: ""));

        Assert.Equal(0, report.Description.Score);
    }

    [Fact]
    public void Validate_ShortDescription_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(description: "Short desc."));

        Assert.True(report.Description.Score < 80);
    }

    [Fact]
    public void Validate_TurkishDescription_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            description: "Bu ürün reçine malzemeden üretilmiştir. Alıcılar için özel tasarlanmıştır."));

        Assert.Contains(report.Description.Issues, i => i.Contains("Turkce", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_GenericDescriptionTemplate_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            description: "This item is prepared as an Etsy-ready product listing optimized for search. " + new string('x', 400)));

        Assert.Contains(report.Description.Issues, i => i.Contains("sabit kalip", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_KeywordInFirstParagraph_AddsStrength()
    {
        var report = _validator.Validate(CreateInput(
            description: "This dragon bust is a premium hand-painted resin collectible.\n\nPerfect for fantasy enthusiasts and shelf decor lovers.\n\nMade from high-quality casting resin.\n\nNo known IP risks.",
            targetKeyword: "dragon bust"));

        Assert.Contains(report.Description.Strengths, s => s.Contains("ilk paragraf", StringComparison.OrdinalIgnoreCase));
    }

    // --- Materials ---

    [Fact]
    public void Validate_EmptyMaterials_LowScore()
    {
        var report = _validator.Validate(CreateInput(materials: ""));

        Assert.True(report.Materials.Score <= 30);
        Assert.Contains(report.Materials.Issues, i => i.Contains("bos", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_VagueMaterials_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(materials: "other,various"));

        Assert.Contains(report.Materials.Issues, i => i.Contains("Belirsiz", StringComparison.OrdinalIgnoreCase));
    }

    // --- Risk ---

    [Fact]
    public void Validate_NoBrandTerms_LowRisk()
    {
        var report = _validator.Validate(CreateInput());

        Assert.Equal("Dusuk", report.Risk.RiskLevel);
        Assert.Contains(report.Risk.Strengths, s => s.Contains("tespit edilmedi", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_BrandTermsDetected_HigherRisk()
    {
        var report = _validator.Validate(CreateInput(
            title: "Ben 10 Omnitrix Watch Display Stand",
            tags: "ben 10,omnitrix,watch stand,display,cartoon,collector,resin,fan art,prop,replica,gift,anime,cosplay"));

        Assert.NotEqual("Dusuk", report.Risk.RiskLevel);
        Assert.Contains(report.Risk.DetectedTerms, t => t.Contains("ben 10", StringComparison.OrdinalIgnoreCase));
        Assert.Contains(report.Risk.DetectedTerms, t => t.Contains("omnitrix", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Validate_MisleadingLicenseClaims_DeductsPoints()
    {
        var report = _validator.Validate(CreateInput(
            description: "This is an official licensed authentic product. " + new string('x', 400)));

        Assert.Contains(report.Risk.Issues, i => i.Contains("lisans", StringComparison.OrdinalIgnoreCase));
    }

    // --- Format Report ---

    [Fact]
    public void FormatReport_ContainsAllFieldScores()
    {
        var report = _validator.Validate(CreateInput());
        var formatted = ListingDraftValidator.FormatReport(report);

        Assert.Contains("Baslik:", formatted);
        Assert.Contains("Tag:", formatted);
        Assert.Contains("Aciklama:", formatted);
        Assert.Contains("Materyal:", formatted);
        Assert.Contains("Risk:", formatted);
        Assert.Contains("/100", formatted);
    }

    [Fact]
    public void FormatReport_ContainsOverallScore()
    {
        var report = _validator.Validate(CreateInput());
        var formatted = ListingDraftValidator.FormatReport(report);

        Assert.Contains("Genel puan:", formatted);
    }
}
