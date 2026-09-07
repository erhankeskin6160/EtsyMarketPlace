namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.SeasonalTrends;
using Xunit;

public sealed class SeasonalTrendServiceTests
{
    private readonly SeasonalTrendService _sut = new();

    [Fact]
    public void GetActiveSeasonalTrends_ReturnsCompleteEtsyAnnualCalendar()
    {
        var trends = _sut.GetActiveSeasonalTrends(new DateTime(2026, 10, 15));

        Assert.NotEmpty(trends);
        Assert.Equal(6, trends.Count);
        Assert.Contains(trends, t => t.Id == "halloween_fall");
        Assert.Contains(trends, t => t.Id == "christmas_holidays");
        Assert.Contains(trends, t => t.Id == "valentines_day");
        Assert.Contains(trends, t => t.Id == "mothers_day");
        Assert.Contains(trends, t => t.Id == "fathers_day_summer");
        Assert.Contains(trends, t => t.Id == "back_to_school");
    }

    [Fact]
    public void GetActiveSeasonalTrends_InOctober_PrioritizesHalloweenAndChristmas()
    {
        // October 15th
        var trends = _sut.GetActiveSeasonalTrends(new DateTime(2026, 10, 15));

        var halloween = trends.First(t => t.Id == "halloween_fall");
        var christmas = trends.First(t => t.Id == "christmas_holidays");

        Assert.Contains("ZİRVE SEZONU", halloween.Status);
        Assert.Contains("ZİRVE SEZONU", christmas.Status);
    }

    [Fact]
    public void AnalyzeNicheOpportunity_EmptyList_ReturnsFallbackSafely()
    {
        var result = _sut.AnalyzeNicheOpportunity("empty niche", []);

        Assert.Equal("empty niche", result.Keyword);
        Assert.Equal(0, result.TotalListingsSampled);
        Assert.Equal(0, result.AveragePrice);
        Assert.NotEmpty(result.Recommendation);
    }

    [Fact]
    public void AnalyzeNicheOpportunity_ValidSamples_CalculatesScoresAndWinningTags()
    {
        var samples = new List<NicheListingSample>
        {
            new("Spooky Halloween Mug Ceramic", 24.99m, 450, 2100, ["halloween mug", "spooky coffee cup", "witchy room decor", "ceramic mug"]),
            new("Ghost Coffee Mug Spooky Gift", 29.50m, 620, 3500, ["halloween mug", "ghost lover gift", "fall coffee cup", "cozy autumn"]),
            new("Pumpkin Fall Mug Desk Decor", 21.00m, 310, 1800, ["halloween mug", "pumpkin spice gift", "fall aesthetic art"]),
        };

        var result = _sut.AnalyzeNicheOpportunity("halloween mug", samples);

        Assert.Equal("halloween mug", result.Keyword);
        Assert.Equal(3, result.TotalListingsSampled);
        Assert.True(result.AveragePrice > 20m);
        Assert.True(result.OpportunityScore > 0);
        Assert.True(result.DemandScore > 0);
        Assert.Contains("halloween mug", result.TopWinningTags);
    }
}
