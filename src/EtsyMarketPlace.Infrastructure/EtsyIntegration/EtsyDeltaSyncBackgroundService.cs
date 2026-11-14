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
        var rawCacheStore = scope.ServiceProvider.GetService<IEtsyRawDataCacheStore>();
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

        var now = DateTimeOffset.UtcNow;
        var (monthStart, monthEnd) = EtsyDashboardService.ResolvePeriod("this_month", null, null);
        var rollingStart = new DateTimeOffset(now.UtcDateTime.Date.AddDays(-35), TimeSpan.Zero);
        var syncStart = monthStart < rollingStart ? monthStart : rollingStart;
        var intervalSeconds = Math.Clamp(apiOptions.Value.DeltaSyncIntervalMinutes, 5, 180) * 60L;
        var minElapsedSeconds = Math.Max(60L, intervalSeconds - 60L);
        var nowUnix = now.ToUnixTimeSeconds();

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

            var ledgerFresh = false;
            var receiptsFresh = false;
            if (rawCacheStore is not null)
            {
                var ledgerWatermark = await rawCacheStore.GetWatermarkAsync(shop.ShopId, "ledger_active:latest", cancellationToken);
                var receiptsWatermark = await rawCacheStore.GetWatermarkAsync(shop.ShopId, "receipts_active:latest", cancellationToken);
                ledgerFresh = ledgerWatermark is not null && (nowUnix - ledgerWatermark.LastDeltaSyncUnix) < minElapsedSeconds;
                receiptsFresh = receiptsWatermark is not null && (nowUnix - receiptsWatermark.LastDeltaSyncUnix) < minElapsedSeconds;
                if (ledgerFresh && receiptsFresh)
                {
                    continue;
                }
            }

            try
            {
                if (!ledgerFresh)
                {
                    using (EtsySyncContext.BeginForceRefresh())
                    {
                        await dataClient.GetLedgerEntriesDetailedAsync(shop.ShopId, syncStart, monthEnd, cancellationToken);
                    }
                }

                if (!receiptsFresh)
                {
                    using (EtsySyncContext.BeginForceRefresh())
                    {
                        await dataClient.GetShopReceiptsAsync(shop.ShopId, syncStart, monthEnd, cancellationToken);
                    }
                }

                logger.LogInformation("Etsy arka plan delta senkronizasyonu tamamlandı (ShopId: {ShopId}).", shop.ShopId);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "Etsy arka plan delta senkronizasyonu başarısız (ShopId: {ShopId}): {Message}", shop.ShopId, ex.Message);
            }
        }
    }
}
