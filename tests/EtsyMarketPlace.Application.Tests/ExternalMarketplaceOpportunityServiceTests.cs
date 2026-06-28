namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.ExternalMarketplaces;
using EtsyMarketPlace.Infrastructure.ExternalMarketplaces;
using Xunit;

public sealed class ExternalMarketplaceOpportunityServiceTests
{
    [Fact]
    public void SearchScoresProviderProductsAndOrdersByOpportunity()
    {
        var service = new ExternalMarketplaceOpportunityService(
        [
            new FakeProvider(),
        ]);

        var results = service.Search(new ExternalMarketplaceSearchRequest("3D cosplay prop", "dragon bust", []));

        Assert.Equal(2, results.Count);
        Assert.True(results[0].OpportunityScore >= results[1].OpportunityScore);
        Assert.Equal("Fake Market", results[0].Product.Source);
        Assert.NotEmpty(results[0].Tags);
        Assert.Contains("Sculpture", results[0].Category);
    }

    [Fact]
    public void EbayProviderBuildsMarketplaceProductsWithSearchUrls()
    {
        var provider = new EbayMarketplaceProvider();
        var products = provider.Search(new ExternalMarketplaceSearchContext(
            "3D cosplay prop",
            "dragon bust",
            ExternalMarketplaceOpportunityService.BuildQuery("3D cosplay prop", "dragon bust")));

        Assert.Equal(3, products.Count);
        Assert.All(products, product =>
        {
            Assert.Equal("eBay", product.Source);
            Assert.Contains("ebay.com", product.SearchUrl);
            Assert.Contains("dragon", product.Title, StringComparison.OrdinalIgnoreCase);
            Assert.NotEmpty(product.Tags);
        });
    }

    private sealed class FakeProvider : IExternalMarketplaceProvider
    {
        public string Name => "Fake Market";

        public IReadOnlyList<MarketplaceProduct> Search(ExternalMarketplaceSearchContext context) =>
        [
            new(
                Name,
                "Handmade dragon bust 3D printed cosplay decor",
                "https://example.test/dragon-bust",
                "https://example.test/search?q=dragon",
                "StrongSeller",
                "",
                "",
                ["dragon", "bust", "3d print", "cosplay decor", "handmade gift"],
                85,
                85,
                220,
                3500,
                "Strong visual demand and handmade fit"),
            new(
                Name,
                "Generic small desk item",
                "https://example.test/generic",
                "https://example.test/search?q=generic",
                "SmallSeller",
                "",
                "",
                ["desk", "decor"],
                7,
                35,
                15,
                120,
                "Weak signal"),
        ];
    }
}
