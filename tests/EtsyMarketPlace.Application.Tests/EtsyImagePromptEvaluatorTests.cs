namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EtsyImagePromptEvaluatorTests
{
    [Fact]
    public void Evaluate_ReturnsZeroForEmptyPrompt()
    {
        var result = EtsyImagePromptEvaluator.Evaluate("");

        Assert.Equal(0, result.Score);
        Assert.NotEmpty(result.Warnings);
        Assert.NotEmpty(result.Tips);
        Assert.NotEmpty(result.EnhancedPrompt);
    }

    [Fact]
    public void Evaluate_CalculatesHighScoreForComprehensivePrompt()
    {
        var prompt = "Handmade wooden jewelry box on a rustic oak table with warm morning sunlight, shallow depth of field";
        var result = EtsyImagePromptEvaluator.Evaluate(prompt);

        Assert.True(result.Score >= 80, $"Expected score >= 80, got {result.Score}");
        Assert.Contains(result.Strengths, s => s.Contains("Zemin"));
        Assert.Contains(result.Strengths, s => s.Contains("Aydınlatma"));
        Assert.Contains(result.Strengths, s => s.Contains("Odak"));
    }

    [Fact]
    public void Evaluate_WarnsWhenSurfaceIsMissing()
    {
        var prompt = "Bright golden hour lighting with soft bokeh background";
        var result = EtsyImagePromptEvaluator.Evaluate(prompt);

        Assert.Contains(result.Warnings, w => w.Contains("Zemin") || w.Contains("zemin"));
    }

    [Fact]
    public void Enhance_AddsMissingLightingAndDepth()
    {
        var rawPrompt = "minimalist ceramic mug";
        var enhanced = EtsyImagePromptEvaluator.Enhance(rawPrompt);

        Assert.Contains("wooden surface", enhanced);
        Assert.Contains("daylight", enhanced);
        Assert.Contains("depth of field", enhanced);
        Assert.Contains("photorealistic", enhanced);
    }
}
