using EtsyMarketPlace.Application.AbTesting;
using EtsyMarketPlace.Application.AiUsage;
using EtsyMarketPlace.Application.BatchQueue;
using EtsyMarketPlace.Application.ShopPerformance;
using EtsyMarketPlace.Application.Tracking;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyIntegrationDatabaseInitializer(
    IServiceScopeFactory scopeFactory,
    IShopPerformanceHistoryRepository shopPerfRepo,
    ILogger<EtsyIntegrationDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            using (var scope = scopeFactory.CreateScope())
            {
                var store = scope.ServiceProvider.GetService<SqliteEtsyIntegrationStore>();
                if (store != null)
                {
                    await store.InitializeAsync(cancellationToken);
                }

                var abTestRepo = scope.ServiceProvider.GetService<IAbTestRepository>();
                if (abTestRepo != null) await abTestRepo.InitializeAsync(cancellationToken);

                var aiUsageRepo = scope.ServiceProvider.GetService<IAiUsageRepository>();
                if (aiUsageRepo != null) await aiUsageRepo.InitializeAsync(cancellationToken);

                var batchQueueRepo = scope.ServiceProvider.GetService<IBatchQueueRepository>();
                if (batchQueueRepo != null) await batchQueueRepo.InitializeAsync(cancellationToken);

                var trackingRepo = scope.ServiceProvider.GetService<ITrackingRepository>();
                if (trackingRepo != null) await trackingRepo.InitializeAsync(cancellationToken);
            }

            await shopPerfRepo.InitializeAsync(cancellationToken);
            logger.LogInformation("Etsy finans & analitik veritabanları başarıyla hazırlandı.");
        }
        catch (Exception exception)
        {
            logger.LogWarning(exception, "Etsy veritabanları başlatılırken uyarı oluştu: {Message}", exception.Message);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
