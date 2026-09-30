using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.EtsyIntegration;

public sealed class EtsyIntegrationDatabaseInitializer(
    SqliteEtsyIntegrationStore store,
    ILogger<EtsyIntegrationDatabaseInitializer> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await store.InitializeAsync(cancellationToken);
            logger.LogInformation("Etsy finans SQLite veritabanı hazırlandı.");
        }
        catch (Exception exception)
        {
            logger.LogCritical(exception, "Etsy finans SQLite veritabanı başlatılamadı. Dosya yolu ve klasör izinlerini kontrol edin.");
            throw;
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
