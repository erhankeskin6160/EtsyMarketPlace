namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Domain.KeywordResearch;
using Xunit;

public sealed class KeywordAnalysisCalculatorTests
{
    [Fact]
    public void Calculate_UsesMedianSoPriceOutlierDoesNotDistortTypicalPrice()
    {
        var sample = Sample(
            Listing(price: 10),
            Listing(price: 20),
            Listing(price: 1000));

        var result = KeywordAnalysisCalculator.Calculate(sample);

        Assert.Equal(20m, result.MedianPrice);
        Assert.Equal(1000m, result.MaximumPrice);
    }

    [Fact]
    public void Calculate_CountsSameTagOncePerListing()
    {
        var sample = Sample(
            Listing(tags: ["sword wall mount", "sword wall mount"]),
            Listing(tags: ["sword wall mount", "fantasy decor"]));

        var result = KeywordAnalysisCalculator.Calculate(sample);
        var metric = Assert.Single(result.LongTailSuggestions, item => item.Value == "sword wall mount");

        Assert.Equal(2, metric.ListingCount);
        Assert.Equal(100m, metric.Percentage);
    }

    [Fact]
    public async Task UseCase_RejectsShortKeywordBeforeCallingGateway()
    {
        var gateway = new RecordingGateway();
        var useCase = new AnalyzeKeywordUseCase(gateway);

        await Assert.ThrowsAsync<ArgumentException>(() => useCase.ExecuteAsync("x", 100));

        Assert.False(gateway.WasCalled);
    }

    [Fact]
    public void Calculate_ProducesScoresWithinExpectedRange()
    {
        var sample = Sample(
            Listing(favorites: 120, views: 4500, shopSales: 3000, seo: 88),
            Listing(favorites: 80, views: 2600, shopSales: 1800, seo: 75));

        var result = KeywordAnalysisCalculator.Calculate(sample);

        Assert.InRange(result.CompetitionScore, 0, 100);
        Assert.InRange(result.DemandSignalScore, 0, 100);
        Assert.InRange(result.OpportunityScore, 0, 100);
        Assert.InRange(result.ConfidenceScore, 0, 100);
    }

    private static KeywordMarketSample Sample(params KeywordListingSnapshot[] listings) =>
        new("sword display", 5000, listings);

    private static KeywordListingSnapshot Listing(
        decimal price = 35,
        int favorites = 10,
        int views = 200,
        int shopSales = 500,
        int seo = 80,
        IReadOnlyList<string>? tags = null) =>
        new(
            1,
            "Sword wall display fantasy decor",
            "Detailed product description for a handmade sword display.",
            price,
            "USD",
            favorites,
            views,
            5,
            shopSales,
            seo,
            70,
            "ExampleShop",
            "https://www.etsy.com/shop/ExampleShop",
            "https://www.etsy.com/listing/1",
            "https://example.com/image.jpg",
            tags ?? ["sword wall mount", "fantasy decor"]);

    private sealed class RecordingGateway : IKeywordMarketGateway
    {
        public bool WasCalled { get; private set; }

        public Task<KeywordMarketSample> GetSampleAsync(string keyword, int limit, CancellationToken cancellationToken = default)
        {
            WasCalled = true;
            return Task.FromResult(new KeywordMarketSample(keyword, 0, []));
        }
    }
}
