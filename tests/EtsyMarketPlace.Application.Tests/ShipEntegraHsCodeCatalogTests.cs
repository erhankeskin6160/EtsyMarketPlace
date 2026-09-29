namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.Shipping;
using Xunit;

/// <summary>
/// ShipEntegra ABD HTS katalog doğrulamasının panel kuralıyla birebir aynı
/// davrandığını doğrular (panel bundle çıkarımı, 29.09.2026).
/// </summary>
public sealed class ShipEntegraHsCodeCatalogTests
{
    [Theory]
    [InlineData("8504403000", "8504.40.30.00")]
    [InlineData("0302530000", "0302.53.00.00")]
    [InlineData("8504.40.95.80", "8504.40.95.80")]
    [InlineData("8504 40 95 80", "8504.40.95.80")]
    [InlineData("85044095", "8504.40.95")]
    public void Format_StripsNonDigits_AndGroupsLikePanel(string input, string expected)
        => Assert.Equal(expected, ShipEntegraHsCodeCatalog.Format(input));

    [Fact]
    public void Catalog_IsEmbeddedAndLarge()
        => Assert.True(ShipEntegraHsCodeCatalog.AllUsCodes.Count >= 23000);

    [Theory]
    [InlineData("0302530000", true)]
    [InlineData("8504409580", true)]
    [InlineData("85044095", true)]
    [InlineData("8504403000", false)]
    [InlineData("3926400000", false)]
    [InlineData("", false)]
    [InlineData("12345", false)]
    public void IsUsCode_MatchesPanelRule(string code, bool expected)
        => Assert.Equal(expected, ShipEntegraHsCodeCatalog.IsUsCode(code));

    [Fact]
    public void SuggestFor_InvalidUsCode_ReturnsValidAlternatives()
    {
        string suggestions = ShipEntegraHsCodeCatalog.SuggestFor("8504403000");
        Assert.NotEqual(string.Empty, suggestions);
        foreach (string candidate in suggestions.Split(", "))
        {
            Assert.True(
                ShipEntegraHsCodeCatalog.IsUsCode(candidate.Replace(".", string.Empty)),
                candidate + " önerilen kod geçerli olmalı");
        }
    }
}
