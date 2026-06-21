namespace SimilarProductsWinForms.Infrastructure.KeywordResearch;

using EtsyMarketPlace.Application.KeywordResearch;
using EtsyMarketPlace.Domain.KeywordResearch;
using SimilarProductsWinForms.Models;
using SimilarProductsWinForms.Services;

internal sealed class EtsyKeywordMarketGateway(
    EtsyApiClient apiClient,
    Func<EtsyApiSettings> settingsProvider) : IKeywordMarketGateway
{
    public async Task<KeywordMarketSample> GetSampleAsync(
        string keyword,
        int limit,
        CancellationToken cancellationToken = default)
    {
        var apiSample = await apiClient.GetKeywordMarketSampleAsync(
            settingsProvider(),
            keyword,
            limit,
            cancellationToken);

        var listings = apiSample.Listings.Select(item => new KeywordListingSnapshot(
            item.ListingId,
            item.Title,
            item.Description,
            item.Price,
            item.CurrencyCode,
            item.Favorites,
            item.Views,
            item.Quantity,
            item.ShopSales,
            item.SeoScore,
            item.MarketScore,
            item.ShopName,
            item.ShopUrl,
            item.ListingUrl,
            item.ImageUrl,
            item.Tags)).ToList();

        return new KeywordMarketSample(keyword, apiSample.TotalResults, listings);
    }
}
