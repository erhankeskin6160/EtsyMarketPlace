using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Tracking;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyIntegrationDatabaseInitializer(
    SqliteEtsyIntegrationStore store,
    IAbTestRepository abTestRepo,
    IAiUsageRepository aiUsageRepo,
    IBatchQueueRepository batchQueueRepo,
    ITrackingRepository trackingRepo,
    IShopPerformanceHistoryRepository shopPerfRepo,
    ILogger<EtsyIntegrationDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.InitializeAsync(cancellationToken);
            await abTestRepo.InitializeAsync(cancellationToken);
            await aiUsageRepo.InitializeAsync(cancellationToken);
            await batchQueueRepo.InitializeAsync(cancellationToken);
            await trackingRepo.InitializeAsync(cancellationToken);
            await shopPerfRepo.InitializeAsync(cancellationToken);
            logger.LogInformation("Etsy finans & analitik SQLite veritabanları başarıyla hazırlandı.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Etsy SQLite veritabanları başlatılamadı. Dosya yolu ve klasör izinlerini kontrol edin.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}

