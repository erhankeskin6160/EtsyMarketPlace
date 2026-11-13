using System;
using System.IO;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using EtsyMarketPlace.Infrastructure.Persistence.Repositories;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Accounting;

public sealed class AccountingPersistenceTests : IDisposable
{
    private readonly string _tempDir;
    private readonly string _dbPath;

    public AccountingPersistenceTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"etsy-accounting-tests-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
        _dbPath = Path.Combine(_tempDir, "etsy-finance.db");
    }

    [Fact]
    public async Task SqliteAccountingStore_CrudLifecycle_PersistsFixedAssetsExpensesProfileAndInventory()
    {
        await using var store = new SqliteAccountingStore(_dbPath);
        const string shopId = "53236321";

        // 1. Profil upsert + okuma
        var savedProfile = await store.UpsertProfileAsync(new ShopAccountingProfile
        {
            ShopId = shopId,
            CommissionTarget = CommissionAccountTarget.OtherCommission_653,
            ShippingTarget = ShippingPackagingCostTarget.MarketingSelling_631,
            CanceledCostTarget = CanceledOrderCostTarget.ExtraordinaryLoss_689,
            InventoryMethod = InventoryValuationMethod.PeriodicCount,
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = true,
            YoungEntrepreneurAnnualLimitTry = 400_000m,
            YoungEntrepreneurPriorUsedTry = 25_000m,
            EExportExemptionRatePct = 50m,
            CustomOrCorporateTaxRatePct = 25m,
            IncludeFxSpreadAndExchangeDifference = true
        });
        Assert.Equal(shopId, savedProfile.ShopId);

        var loadedProfile = await store.GetOrDefaultProfileAsync(shopId);
        Assert.Equal(InventoryValuationMethod.PeriodicCount, loadedProfile.InventoryMethod);
        Assert.True(loadedProfile.YoungEntrepreneurModeEnabled);
        Assert.Equal(25_000m, loadedProfile.YoungEntrepreneurPriorUsedTry);
        Assert.Equal(50m, loadedProfile.EExportExemptionRatePct);

        // 2. Duran varlık (örn. Creality H) ekleme, okuma, güncelleme ve silme
        var createdAsset = await store.AddFixedAssetAsync(new FixedAssetItem
        {
            ShopId = shopId,
            AssetName = "Creality H",
            AssetAccountCode = "253",
            ExpenseTargetCategory = OperatingExpenseCategory.GeneralManagement_632,
            AcquisitionDate = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
            AcquisitionCostTry = 22_000m,
            SalvageValueTry = 0m,
            UsefulLifeYears = 5,
            Method = DepreciationMethod.StraightLine,
            IsActive = true
        });

        Assert.True(createdAsset.Id > 0);
        var assets = await store.GetFixedAssetsAsync(shopId);
        Assert.Single(assets);
        Assert.Equal("Creality H", assets[0].AssetName);
        Assert.Equal(22_000m, assets[0].AcquisitionCostTry);

        createdAsset.AcquisitionCostTry = 24_500m;
        createdAsset.UsefulLifeYears = 4;
        var updatedAsset = await store.UpdateFixedAssetAsync(createdAsset);
        Assert.NotNull(updatedAsset);
        Assert.Equal(24_500m, updatedAsset!.AcquisitionCostTry);
        Assert.Equal(4, updatedAsset.UsefulLifeYears);

        // 3. İşletme gideri ekleme, okuma, güncelleme ve silme
        var createdExpense = await store.AddOperatingExpenseAsync(new OperatingExpenseEntry
        {
            ShopId = shopId,
            EntryDate = new DateTime(2026, 11, 10, 0, 0, 0, DateTimeKind.Utc),
            DocumentNo = "FTR-2026-01",
            Category = OperatingExpenseCategory.GeneralManagement_632,
            Description = "Atölye Kirası",
            Amount = 8_500m,
            Currency = "TRY",
            ExchangeRateToTry = 1m,
            AmountTry = 8_500m,
            IsTaxDeductible = true,
            Frequency = ExpenseFrequency.MonthlyRecurring
        });

        Assert.True(createdExpense.Id > 0);
        var expenses = await store.GetOperatingExpensesAsync(shopId);
        Assert.Single(expenses);
        Assert.Equal("Atölye Kirası", expenses[0].Description);
        Assert.Equal(8_500m, expenses[0].AmountTry);

        createdExpense.AmountTry = 9_000m;
        var updatedExpense = await store.UpdateOperatingExpenseAsync(createdExpense);
        Assert.NotNull(updatedExpense);
        Assert.Equal(9_000m, updatedExpense!.AmountTry);

        // 4. Aralıklı envanter dönem sayımı upsert (çakışmada güncelleme)
        await store.UpsertInventoryAdjustmentAsync(new InventoryPeriodAdjustment
        {
            ShopId = shopId,
            Year = 2026,
            Month = 11,
            OpeningInventoryTry = 10_000m,
            GrossPurchasesTry = 15_000m,
            PurchaseReturnsAndDiscountsTry = 500m,
            ClosingInventoryTry = 6_000m,
            Notes = "İlk sayım"
        });
        await store.UpsertInventoryAdjustmentAsync(new InventoryPeriodAdjustment
        {
            ShopId = shopId,
            Year = 2026,
            Month = 11,
            OpeningInventoryTry = 10_000m,
            GrossPurchasesTry = 16_000m,
            PurchaseReturnsAndDiscountsTry = 500m,
            ClosingInventoryTry = 5_500m,
            Notes = "Düzeltilmiş sayım"
        });

        var adjustments = await store.GetInventoryAdjustmentsAsync(shopId);
        Assert.Single(adjustments);
        Assert.Equal(16_000m, adjustments[0].GrossPurchasesTry);
        Assert.Equal(5_500m, adjustments[0].ClosingInventoryTry);
        Assert.Equal("Düzeltilmiş sayım", adjustments[0].Notes);

        // 5. Silme kontrolleri
        Assert.True(await store.DeleteFixedAssetAsync(createdAsset.Id, shopId));
        Assert.Empty(await store.GetFixedAssetsAsync(shopId));
        Assert.True(await store.DeleteOperatingExpenseAsync(createdExpense.Id, shopId));
        Assert.Empty(await store.GetOperatingExpensesAsync(shopId));
    }

    [Fact]
    public async Task EfPostgreSqlRepository_WhenPostgresUnavailable_FallsBackToSqliteForReadsAndWrites()
    {
        var dbOptions = new DbContextOptionsBuilder<EtsyMarketPlaceDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=unreachable_test;Username=u;Password=p;Timeout=1;Command Timeout=1")
            .Options;

        await using var pgContext = new EtsyMarketPlaceDbContext(dbOptions);
        await using var sqliteFallback = new SqliteAccountingStore(_dbPath);
        var repository = new EfPostgreSqlAccountingRepository(
            pgContext,
            NullLogger<EfPostgreSqlAccountingRepository>.Instance,
            sqliteFallback);

        const string shopId = "53236321";

        var savedAsset = await repository.AddFixedAssetAsync(new FixedAssetItem
        {
            ShopId = shopId,
            AssetName = "Creality H",
            AssetAccountCode = "253",
            ExpenseTargetCategory = OperatingExpenseCategory.GeneralManagement_632,
            AcquisitionDate = new DateTime(2026, 11, 13, 0, 0, 0, DateTimeKind.Utc),
            AcquisitionCostTry = 22_000m,
            SalvageValueTry = 0m,
            UsefulLifeYears = 5,
            Method = DepreciationMethod.StraightLine,
            IsActive = true
        });

        Assert.True(savedAsset.Id > 0);
        Assert.Empty(pgContext.ChangeTracker.Entries());

        var loadedAssets = await repository.GetFixedAssetsAsync(shopId);
        Assert.Single(loadedAssets);
        Assert.Equal("Creality H", loadedAssets[0].AssetName);
        Assert.Equal(22_000m, loadedAssets[0].AcquisitionCostTry);

        var directSqliteAssets = await sqliteFallback.GetFixedAssetsAsync(shopId);
        Assert.Single(directSqliteAssets);
        Assert.Equal("Creality H", directSqliteAssets[0].AssetName);
    }

    [Fact]
    public async Task EfPostgreSqlRepository_WithoutFallback_ReturnsEmptyOnReadAndThrowsOnWriteWhenPostgresUnavailable()
    {
        var dbOptions = new DbContextOptionsBuilder<EtsyMarketPlaceDbContext>()
            .UseNpgsql("Host=127.0.0.1;Port=59999;Database=unreachable_test;Username=u;Password=p;Timeout=1;Command Timeout=1")
            .Options;

        await using var pgContext = new EtsyMarketPlaceDbContext(dbOptions);
        var repository = new EfPostgreSqlAccountingRepository(
            pgContext,
            NullLogger<EfPostgreSqlAccountingRepository>.Instance,
            fallbackStore: null);

        var assets = await repository.GetFixedAssetsAsync("53236321");
        Assert.Empty(assets);

        await Assert.ThrowsAnyAsync<Exception>(() => repository.AddFixedAssetAsync(new FixedAssetItem
        {
            ShopId = "53236321",
            AssetName = "Creality H",
            AcquisitionCostTry = 22_000m,
            UsefulLifeYears = 5
        }));
    }

    [Fact]
    public void AccountingPostgresSchemaBootstrapper_EnsureSchemaSql_ContainsAllFourAccountingTables()
    {
        var sql = AccountingPostgresSchemaBootstrapper.EnsureSchemaSql;
        Assert.Contains("CREATE TABLE IF NOT EXISTS accounting_shop_profiles", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS accounting_operating_expenses", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS accounting_fixed_assets", sql, StringComparison.Ordinal);
        Assert.Contains("CREATE TABLE IF NOT EXISTS accounting_inventory_adjustments", sql, StringComparison.Ordinal);
    }

    public void Dispose()
    {
        SqliteConnection.ClearAllPools();
        if (Directory.Exists(_tempDir))
        {
            try
            {
                Directory.Delete(_tempDir, recursive: true);
            }
            catch
            {
                // Geçici test dizini temizleme hatasını yoksay
            }
        }
    }
}

