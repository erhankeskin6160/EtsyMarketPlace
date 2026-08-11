namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class ListingDraftInstructionBuilderTests
{
    [Fact]
    public void BuildSystemInstruction_ContainsEtsySellerHandbookReference()
    {
        var instruction = ListingDraftInstructionBuilder.BuildSystemInstruction();

        Assert.Contains("Etsy", instruction, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("JSON", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildSystemInstruction_EnforcesJsonOnlyResponse()
    {
        var instruction = ListingDraftInstructionBuilder.BuildSystemInstruction();

        Assert.Contains("valid JSON only", instruction, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsTargetKeyword()
    {
        var input = new ListingOptimizationInput(
            "Dragon wall art",
            "A fantasy decor item",
            ["dragon art", "wall decor"],
            "dragon wall decor");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("dragon wall decor", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsCurrentTitleAndTags()
    {
        var input = new ListingOptimizationInput(
            "Resin dragon bust",
            "Hand painted bust",
            ["resin bust", "dragon figure"],
            "dragon bust");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("Resin dragon bust", prompt);
        Assert.Contains("resin bust", prompt);
        Assert.Contains("dragon figure", prompt);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsJsonSchemaKeys()
    {
        var input = new ListingOptimizationInput(
            "Test title",
            "Test description",
            ["test tag"],
            "test keyword");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("title_suggestions", prompt);
        Assert.Contains("tag_suggestions", prompt);
        Assert.Contains("material_suggestions", prompt);
        Assert.Contains("description_draft", prompt);
        Assert.Contains("risk_warnings", prompt);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsTagRules()
    {
        var input = new ListingOptimizationInput(
            "Test",
            "Test",
            ["tag1"],
            "keyword");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("13", prompt);
        Assert.Contains("20 characters", prompt);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsTitleCharacterLimit()
    {
        var input = new ListingOptimizationInput(
            "Test",
            "Test",
            ["tag1"],
            "keyword");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("140 characters", prompt);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsTurkishRiskWarningInstruction()
    {
        var input = new ListingOptimizationInput(
            "Test",
            "Test",
            ["tag1"],
            "keyword");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("Turkish", prompt);
    }

    [Fact]
    public void BuildOptimizationPrompt_ContainsEtsyKnowledgeBaseRules()
    {
        var input = new ListingOptimizationInput(
            "Test",
            "Test",
            ["tag1"],
            "keyword");

        var prompt = ListingDraftInstructionBuilder.BuildOptimizationPrompt(input);

        Assert.Contains("Etsy listing knowledge", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCreationPrompt_IncludesProductTypeForDigital()
    {
        var prompt = ListingDraftInstructionBuilder.BuildCreationPrompt(
            "SVG file dragon art",
            "Instant download vector file",
            ["svg file", "dragon svg"],
            "dragon svg",
            productType: "Dijital urun");

        Assert.Contains("digital download", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("file format", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCreationPrompt_IncludesProductTypeForPhysical()
    {
        var prompt = ListingDraftInstructionBuilder.BuildCreationPrompt(
            "Resin dragon figure",
            "Hand painted collectible",
            ["resin figure"],
            "dragon figure",
            productType: "Fiziksel urun");

        Assert.Contains("physical product", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("dimensions", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildCreationPrompt_IncludesCategoryWhenProvided()
    {
        var prompt = ListingDraftInstructionBuilder.BuildCreationPrompt(
            "Dragon bust",
            "Fantasy sculpture",
            ["dragon bust"],
            "dragon bust",
            category: "Sculptures & Figurines");

        Assert.Contains("Sculptures & Figurines", prompt);
    }

    [Fact]
    public void BuildCreationPrompt_OmitsProductTypeWhenNull()
    {
        var prompt = ListingDraftInstructionBuilder.BuildCreationPrompt(
            "Dragon bust",
            "Fantasy sculpture",
            ["dragon bust"],
            "dragon bust");

        Assert.DoesNotContain("Product type:", prompt);
        Assert.DoesNotContain("digital download", prompt, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("physical product", prompt, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildResponseSchemaInstruction_SpecifiesAllRequiredKeys()
    {
        var schema = ListingDraftInstructionBuilder.BuildResponseSchemaInstruction();

        Assert.Contains("title_suggestions", schema);
        Assert.Contains("tag_suggestions", schema);
        Assert.Contains("material_suggestions", schema);
        Assert.Contains("description_draft", schema);
        Assert.Contains("risk_warnings", schema);
    }

    [Fact]
    public void BuildFieldRules_ContainsMaterialAuthenticityConstraint()
    {
        var rules = ListingDraftInstructionBuilder.BuildFieldRules();

        Assert.Contains("Never invent materials", rules, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void BuildFieldRules_ForbidsGenericDescriptionPhrases()
    {
        var rules = ListingDraftInstructionBuilder.BuildFieldRules();

        Assert.Contains("Etsy-ready product listing", rules);
    }

    [Fact]
    public void BuildCreationPrompt_ContainsAllJsonSchemaKeys()
    {
        var prompt = ListingDraftInstructionBuilder.BuildCreationPrompt(
            "Test",
            "Test",
            ["tag"],
            "keyword");

        Assert.Contains("title_suggestions", prompt);
        Assert.Contains("tag_suggestions", prompt);
        Assert.Contains("material_suggestions", prompt);
        Assert.Contains("description_draft", prompt);
        Assert.Contains("risk_warnings", prompt);
    }
}
