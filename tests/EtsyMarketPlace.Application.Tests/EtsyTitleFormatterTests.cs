namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EtsyTitleFormatterTests
{
    [Fact]
    public void NormalizeForEtsy_ShouldReplaceAllAmpersandsWithAnd()
    {
        // Arrange
        const string input = "Handmade Loki Figure 20cm - Marvel Collectible Statue & Trickster God Fan Art - Custom Action Figure Desk Decor & Birthday Gift for Him";

        // Act
        var result = EtsyTitleFormatter.NormalizeForEtsy(input);

        // Assert
        Assert.DoesNotContain("&", result);
        Assert.Contains("Statue and Trickster", result);
        Assert.Contains("Desk Decor and Birthday", result);
        Assert.True(result.Length <= 140);
    }

    [Theory]
    [InlineData("Art & Craft", "Art and Craft")]
    [InlineData("Desk &Room Decor", "Desk and Room Decor")]
    [InlineData("Statue & Model & Figurine", "Statue and Model and Figurine")]
    [InlineData("Gold & Silver & Bronze & Platinum", "Gold and Silver and Bronze and Platinum")]
    public void NormalizeForEtsy_ShouldCleanVariousAmpersandFormats(string input, string expected)
    {
        var result = EtsyTitleFormatter.NormalizeForEtsy(input);
        Assert.Equal(expected, result);
        Assert.DoesNotContain("&", result);
    }

    [Fact]
    public void NormalizeForEtsy_ShouldStripUiHeaders()
    {
        const string input = "[AI İLE OPTİMİZE EDİLMİŞ BAŞLIK - 135/140 Karakter] (Aktif Motor: Google Gemini (gemini-3.5-flash-lite))\nLoki Figure 20cm";

        var result = EtsyTitleFormatter.NormalizeForEtsy(input);

        Assert.Equal("Loki Figure 20cm", result);
    }

    [Fact]
    public void NormalizeForEtsy_ShouldRespect140CharLimit()
    {
        var longTitle = new string('A', 150);
        var result = EtsyTitleFormatter.NormalizeForEtsy(longTitle);

        Assert.True(result.Length <= 140);
    }

    [Fact]
    public void NormalizeForEtsy_EmptyOrNull_ReturnsEmpty()
    {
        Assert.Equal("", EtsyTitleFormatter.NormalizeForEtsy(null));
        Assert.Equal("", EtsyTitleFormatter.NormalizeForEtsy("   "));
    }
}
