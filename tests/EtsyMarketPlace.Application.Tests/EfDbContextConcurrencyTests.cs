using System;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.EtsyIntegration;
using EtsyMarketPlace.Infrastructure.Persistence.Repositories;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public sealed class EfDbContextConcurrencyTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _sqlitePath;

    public EfDbContextConcurrencyTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"etsy-concurrency-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _sqlitePath = Path.Combine(_tempDir, "etsy-finance.db");
    }

    [Fact]
    public async Task EfRepositories_ConcurrentParallelCalls_WithFactoryAndLegacyContext_DoNotThrowConcurrencyException()
    {
        var dbOptions = new DbContextOptionsBuilder<EtsyMarketPlaceDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=unreachable_test;Username=u;Password=p;Timeout=1;Command Timeout=1")
            .Options;

        var factory = new SimpleDbContextFactory(dbOptions);
        await using var sharedContext = factory.CreateDbContext();
        var protector = new EphemeralDataProtectionProvider();
        var sqliteFallback = new SqliteEtsyIntegrationStore(
            Microsoft.Extensions.Options.Options.Create(new EtsyIntegrationOptions { DatabasePath = _sqlitePath }),
            protector);

        // 1. IDbContextFactory ile eşzamanlı çağrılar
        var tokenStoreWithFactory = new EfPostgreSqlTokenStore(
            sharedContext,
            protector,
            NullLogger<EfPostgreSqlTokenStore>.Instance,
            sqliteFallback,
            factory);

        var settingsRepoWithFactory = new EfPostgreSqlShopSettingsRepository(
            sharedContext,
            protector,
            NullLogger<EfPostgreSqlShopSettingsRepository>.Instance,
            sqliteFallback,
            factory);

        var integrationRepoWithFactory = new EfPostgreSqlIntegrationRepository(
            sharedContext,
            NullLogger<EfPostgreSqlIntegrationRepository>.Instance,
            factory);

        var factoryTasks = Enumerable.Range(0, 6).Select(async _ =>
        {
            await tokenStoreWithFactory.GetAsync("53236321");
            await settingsRepoWithFactory.GetRawEtsyAppCredentialsAsync("53236321");
            await integrationRepoWithFactory.GetOrderCostsAsync("53236321");
        });
        await Task.WhenAll(factoryTasks);

        // 2. Fabrika olmadan paylaşılan DbContext ile eşzamanlı çağrılar (SemaphoreSlim serileştirmesi)
        var tokenStoreLegacy = new EfPostgreSqlTokenStore(
            sharedContext,
            protector,
            NullLogger<EfPostgreSqlTokenStore>.Instance,
            sqliteFallback,
            contextFactory: null);

        var legacyTasks = Enumerable.Range(0, 6)
            .Select(_ => tokenStoreLegacy.GetAsync("53236321"));
        await Task.WhenAll(legacyTasks);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            try { Directory.Delete(_tempDir, recursive: true); }
            catch { }
        }
    }

    private sealed class SimpleDbContextFactory(DbContextOptions<EtsyMarketPlaceDbContext> options)
        : IDbContextFactory<EtsyMarketPlaceDbContext>
    {
        public EtsyMarketPlaceDbContext CreateDbContext() => new(options);
    }
}

