namespace SimilarProductsWinForms.Infrastructure.ShopPerformance;

using EtsyMarketPlace.Application.ShopPerformance;
using SimilarProductsWinForms.Services;

internal sealed class EtsyOwnShopGateway(
    EtsyApiClient apiClient,
    Func<Models.EtsyApiSettings> settingsProvider) : IOwnShopGateway
{
    public async Task<OwnShopPerformanceSource> GetPerformanceSourceAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        var settings = settingsProvider();
        try
        {
            return await apiClient.GetOwnShopPerformanceSourceAsync(
                settings,
                periodStart,
                periodEnd,
                cancellationToken);
        }
        finally
        {
            EtsyApiSettingsStore.Save(settings);
        }
    }
}
