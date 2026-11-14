using EtsyMarketPlace.Application.EtsyIntegration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

/// <summary>
/// Arka planda yalnızca yeni/güncel Etsy verilerini (Delta Sync) periyodik olarak çekip
/// yerel veritabanına yazar; böylece sayfa açılışları Etsy API kotası tüketmeden DB'den beslenir.
/// </summary>
public sealed class EtsyDeltaSyncBackgroundService(
    IServiceScopeFactory scopeFactory,
    IEtsyRateLimitStatusProvider rateLimitProvider,
    IOptions<EtsyApiOptions> apiOptions,
    IConfiguration configuration,
    ILogger<EtsyDeltaSyncBackgroundService> logger) : BackgroundService
{
    private const int QuotaSafetyFloorRemaining = 250;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (string.Equals(configuration["HealthChecks:BypassDatabaseProbe"], "true", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        try
        {
            await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken);
        }
        catch (OperationCanceledException)
        {
            return;
        }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SyncConnectedShopsAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Etsy arka plan delta senkronizasyon döngüsünde uyarı oluştu: {Message}", ex.Message);
            }

            var intervalMinutes = Math.Clamp(apiOptions.Value.DeltaSyncIntervalMinutes, 5, 180);
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(intervalMinutes), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    private async Task SyncConnectedShopsAsync(CancellationToken cancellationToken)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var tokenStore = scope.ServiceProvider.GetService<IEtsyTokenStore>();
        var dataClient = scope.ServiceProvider.GetService<IEtsyDataClient>();
        if (tokenStore is null || dataClient is null)
            return;

        IReadOnlyList<EtsyConnectedShopDto> shops;
        try
        {
            shops = await tokenStore.GetConnectedShopsAsync(cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogDebug(ex, "Bağlı Etsy mağazaları arka plan senkronizasyonu için okunamadı.");
            return;
        }

        var (monthStart, monthEnd) = EtsyDashboardService.ResolvePeriod("this_month", null, null);
        foreach (var shop in shops)
        {
            if (string.IsNullOrWhiteSpace(shop.ShopId) || !shop.IsActive)
                continue;

            var rateSnapshot = rateLimitProvider.GetSnapshot(shop.ShopId);
            if (rateSnapshot is not null &&
                (rateSnapshot.IsDailyLimitExceeded ||
                 (rateSnapshot.RemainingToday.HasValue && rateSnapshot.RemainingToday.Value < QuotaSafetyFloorRemaining)))
            {
                logger.LogWarning(
                    "Etsy arka plan delta sync ({ShopId}) kota koruması nedeniyle atlandı (Kalan: {Remaining}, Limit Aşıldı: {Exceeded}).",
                    shop.ShopId,
                    rateSnapshot.RemainingToday,
                    rateSnapshot.IsDailyLimitExceeded);
                continue;
            }

            try
            {
                await dataClient.GetLedgerEntriesDetailedAsync(shop.ShopId, monthStart, monthEnd, cancellationToken);
                await dataClient.GetShopReceiptsAsync(shop.ShopId, monthStart, monthEnd, cancellationToken);
                logger.LogInformation("Etsy arka plan delta senkronizasyonu tamamlandı (ShopId: {ShopId}).", shop.ShopId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Etsy arka plan delta senkronizasyonu başarısız (ShopId: {ShopId}): {Message}", shop.ShopId, ex.Message);
            }
        }
    }
}
