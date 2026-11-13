using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using EtsyMarketPlace.Infrastructure.Database.Postgres;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace EtsyMarketPlace.Infrastructure.Persistence.Repositories;

/// <summary>
/// PostgreSQL 16 EF Core tabanlı Genel Muhasebe (TDHP 600–692) veri erişim katmanı.
/// Eksik tablo (42P01) durumunda şemayı otomatik onarır; PostgreSQL erişilemezse SQLite yedeği üzerinden kesintisiz çalışır.
/// </summary>
public sealed class EfPostgreSqlAccountingRepository(
    EtsyMarketPlaceDbContext context,
    ILogger<EfPostgreSqlAccountingRepository> logger,
    SqliteAccountingStore? fallbackStore = null) : IAccountingRepository
{
    private const string UndefinedTableSqlState = "42P01";

    public Task<ShopAccountingProfile> GetOrDefaultProfileAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.AccountingShopProfiles
                    .AsNoTracking()
                    .FirstOrDefaultAsync(x => x.ShopId == key, cancellationToken);
                return existing ?? new ShopAccountingProfile { ShopId = key };
            },
            store => store.GetOrDefaultProfileAsync(key, cancellationToken),
            () => new ShopAccountingProfile { ShopId = key },
            $"GetOrDefaultProfile({key})",
            cancellationToken);
    }

    public Task<ShopAccountingProfile> UpsertProfileAsync(ShopAccountingProfile profile, CancellationToken cancellationToken = default)
    {
        var key = profile.ShopId.Trim();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.AccountingShopProfiles.FirstOrDefaultAsync(x => x.ShopId == key, cancellationToken);
                if (existing is null)
                {
                    profile.ShopId = key;
                    profile.UpdatedAt = DateTime.UtcNow;
                    context.AccountingShopProfiles.Add(profile);
                    await context.SaveChangesAsync(cancellationToken);
                    return profile;
                }

                existing.CommissionTarget = profile.CommissionTarget;
                existing.ShippingTarget = profile.ShippingTarget;
                existing.CanceledCostTarget = profile.CanceledCostTarget;
                existing.InventoryMethod = profile.InventoryMethod;
                existing.TaxRegime = profile.TaxRegime;
                existing.YoungEntrepreneurModeEnabled = profile.YoungEntrepreneurModeEnabled;
                existing.YoungEntrepreneurAnnualLimitTry = profile.YoungEntrepreneurAnnualLimitTry;
                existing.YoungEntrepreneurPriorUsedTry = profile.YoungEntrepreneurPriorUsedTry;
                existing.EExportExemptionRatePct = profile.EExportExemptionRatePct;
                existing.CustomOrCorporateTaxRatePct = profile.CustomOrCorporateTaxRatePct;
                existing.CustomGvkBracketsJson = profile.CustomGvkBracketsJson;
                existing.IncludeFxSpreadAndExchangeDifference = profile.IncludeFxSpreadAndExchangeDifference;
                existing.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpsertProfileAsync(profile, cancellationToken),
            noFallbackResult: null,
            $"UpsertProfile({key})",
            cancellationToken);
    }

    public Task<IReadOnlyList<OperatingExpenseEntry>> GetOperatingExpensesAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<IReadOnlyList<OperatingExpenseEntry>>(
            async () => await context.AccountingOperatingExpenses
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken),
            store => store.GetOperatingExpensesAsync(key, cancellationToken),
            () => Array.Empty<OperatingExpenseEntry>(),
            $"GetOperatingExpenses({key})",
            cancellationToken);
    }

    public Task<OperatingExpenseEntry?> GetOperatingExpenseByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<OperatingExpenseEntry?>(
            () => context.AccountingOperatingExpenses
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken),
            store => store.GetOperatingExpenseByIdAsync(id, key, cancellationToken),
            () => null,
            $"GetOperatingExpenseById({id}, {key})",
            cancellationToken);
    }

    public Task<OperatingExpenseEntry> AddOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            async () =>
            {
                entry.EntryDate = EnsureUtc(entry.EntryDate);
                entry.RecurringEndDate = entry.RecurringEndDate.HasValue ? EnsureUtc(entry.RecurringEndDate.Value) : null;
                entry.CreatedAt = DateTime.UtcNow;
                entry.UpdatedAt = DateTime.UtcNow;

                context.AccountingOperatingExpenses.Add(entry);
                await context.SaveChangesAsync(cancellationToken);
                return entry;
            },
            store => store.AddOperatingExpenseAsync(entry, cancellationToken),
            noFallbackResult: null,
            $"AddOperatingExpense({entry.ShopId})",
            cancellationToken);
    }

    public Task<OperatingExpenseEntry?> UpdateOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<OperatingExpenseEntry?>(
            async () =>
            {
                var existing = await context.AccountingOperatingExpenses
                    .FirstOrDefaultAsync(x => x.Id == entry.Id && x.ShopId == entry.ShopId, cancellationToken);
                if (existing is null) return null;

                existing.EntryDate = EnsureUtc(entry.EntryDate);
                existing.DocumentNo = entry.DocumentNo;
                existing.Category = entry.Category;
                existing.Description = entry.Description;
                existing.Amount = entry.Amount;
                existing.Currency = entry.Currency;
                existing.ExchangeRateToTry = entry.ExchangeRateToTry;
                existing.AmountTry = entry.AmountTry;
                existing.IsTaxDeductible = entry.IsTaxDeductible;
                existing.Frequency = entry.Frequency;
                existing.RecurringEndDate = entry.RecurringEndDate.HasValue ? EnsureUtc(entry.RecurringEndDate.Value) : null;
                existing.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpdateOperatingExpenseAsync(entry, cancellationToken),
            noFallbackResult: null,
            $"UpdateOperatingExpense({entry.Id})",
            cancellationToken);
    }

    public Task<bool> DeleteOperatingExpenseAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.AccountingOperatingExpenses
                    .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
                if (existing is null) return false;

                context.AccountingOperatingExpenses.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
                return true;
            },
            store => store.DeleteOperatingExpenseAsync(id, key, cancellationToken),
            noFallbackResult: null,
            $"DeleteOperatingExpense({id})",
            cancellationToken);
    }

    public Task<IReadOnlyList<FixedAssetItem>> GetFixedAssetsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<IReadOnlyList<FixedAssetItem>>(
            async () => await context.AccountingFixedAssets
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.AcquisitionDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken),
            store => store.GetFixedAssetsAsync(key, cancellationToken),
            () => Array.Empty<FixedAssetItem>(),
            $"GetFixedAssets({key})",
            cancellationToken);
    }

    public Task<FixedAssetItem?> GetFixedAssetByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<FixedAssetItem?>(
            () => context.AccountingFixedAssets
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken),
            store => store.GetFixedAssetByIdAsync(id, key, cancellationToken),
            () => null,
            $"GetFixedAssetById({id}, {key})",
            cancellationToken);
    }

    public Task<FixedAssetItem> AddFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync(
            async () =>
            {
                asset.AcquisitionDate = EnsureUtc(asset.AcquisitionDate);
                asset.CreatedAt = DateTime.UtcNow;
                asset.UpdatedAt = DateTime.UtcNow;

                context.AccountingFixedAssets.Add(asset);
                await context.SaveChangesAsync(cancellationToken);
                return asset;
            },
            store => store.AddFixedAssetAsync(asset, cancellationToken),
            noFallbackResult: null,
            $"AddFixedAsset({asset.ShopId})",
            cancellationToken);
    }

    public Task<FixedAssetItem?> UpdateFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
    {
        return ExecuteWithResilienceAsync<FixedAssetItem?>(
            async () =>
            {
                var existing = await context.AccountingFixedAssets
                    .FirstOrDefaultAsync(x => x.Id == asset.Id && x.ShopId == asset.ShopId, cancellationToken);
                if (existing is null) return null;

                existing.AssetName = asset.AssetName;
                existing.AssetAccountCode = asset.AssetAccountCode;
                existing.ExpenseTargetCategory = asset.ExpenseTargetCategory;
                existing.AcquisitionDate = EnsureUtc(asset.AcquisitionDate);
                existing.AcquisitionCostTry = asset.AcquisitionCostTry;
                existing.SalvageValueTry = asset.SalvageValueTry;
                existing.UsefulLifeYears = asset.UsefulLifeYears;
                existing.Method = asset.Method;
                existing.IsActive = asset.IsActive;
                existing.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpdateFixedAssetAsync(asset, cancellationToken),
            noFallbackResult: null,
            $"UpdateFixedAsset({asset.Id})",
            cancellationToken);
    }

    public Task<bool> DeleteFixedAssetAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.AccountingFixedAssets
                    .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
                if (existing is null) return false;

                context.AccountingFixedAssets.Remove(existing);
                await context.SaveChangesAsync(cancellationToken);
                return true;
            },
            store => store.DeleteFixedAssetAsync(id, key, cancellationToken),
            noFallbackResult: null,
            $"DeleteFixedAsset({id})",
            cancellationToken);
    }

    public Task<IReadOnlyList<InventoryPeriodAdjustment>> GetInventoryAdjustmentsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<IReadOnlyList<InventoryPeriodAdjustment>>(
            async () => await context.AccountingInventoryAdjustments
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToListAsync(cancellationToken),
            store => store.GetInventoryAdjustmentsAsync(key, cancellationToken),
            () => Array.Empty<InventoryPeriodAdjustment>(),
            $"GetInventoryAdjustments({key})",
            cancellationToken);
    }

    public Task<InventoryPeriodAdjustment?> GetInventoryAdjustmentAsync(string shopId, int year, int month, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return ExecuteWithResilienceAsync<InventoryPeriodAdjustment?>(
            () => context.AccountingInventoryAdjustments
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == key && x.Year == year && x.Month == month, cancellationToken),
            store => store.GetInventoryAdjustmentAsync(key, year, month, cancellationToken),
            () => null,
            $"GetInventoryAdjustment({key}, {year}-{month})",
            cancellationToken);
    }

    public Task<InventoryPeriodAdjustment> UpsertInventoryAdjustmentAsync(InventoryPeriodAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var key = adjustment.ShopId.Trim();
        return ExecuteWithResilienceAsync(
            async () =>
            {
                var existing = await context.AccountingInventoryAdjustments
                    .FirstOrDefaultAsync(x => x.ShopId == key && x.Year == adjustment.Year && x.Month == adjustment.Month, cancellationToken);

                if (existing is null)
                {
                    adjustment.ShopId = key;
                    adjustment.UpdatedAt = DateTime.UtcNow;
                    context.AccountingInventoryAdjustments.Add(adjustment);
                    await context.SaveChangesAsync(cancellationToken);
                    return adjustment;
                }

                existing.OpeningInventoryTry = adjustment.OpeningInventoryTry;
                existing.GrossPurchasesTry = adjustment.GrossPurchasesTry;
                existing.PurchaseReturnsAndDiscountsTry = adjustment.PurchaseReturnsAndDiscountsTry;
                existing.ClosingInventoryTry = adjustment.ClosingInventoryTry;
                existing.Notes = adjustment.Notes;
                existing.UpdatedAt = DateTime.UtcNow;

                await context.SaveChangesAsync(cancellationToken);
                return existing;
            },
            store => store.UpsertInventoryAdjustmentAsync(adjustment, cancellationToken),
            noFallbackResult: null,
            $"UpsertInventoryAdjustment({key}, {adjustment.Year}-{adjustment.Month})",
            cancellationToken);
    }

    private async Task<T> ExecuteWithResilienceAsync<T>(
        Func<Task<T>> pgAction,
        Func<SqliteAccountingStore, Task<T>> sqliteAction,
        Func<T>? noFallbackResult,
        string operationName,
        CancellationToken cancellationToken)
    {
        try
        {
            return await pgAction();
        }
        catch (Exception ex) when (IsUndefinedTableException(ex))
        {
            context.ChangeTracker.Clear();
            logger.LogWarning(ex, "PostgreSQL muhasebe tabloları eksik (42P01), {Operation} öncesi otomatik oluşturuluyor.", operationName);
            var created = await AccountingPostgresSchemaBootstrapper.EnsureTablesExistAsync(context, logger, cancellationToken);
            if (created)
            {
                try
                {
                    return await pgAction();
                }
                catch (Exception retryEx) when (IsDatabaseUnavailableException(retryEx))
                {
                    context.ChangeTracker.Clear();
                    return await FallbackOrDefaultAsync(retryEx, sqliteAction, noFallbackResult, operationName);
                }
            }

            return await FallbackOrDefaultAsync(ex, sqliteAction, noFallbackResult, operationName);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            context.ChangeTracker.Clear();
            return await FallbackOrDefaultAsync(ex, sqliteAction, noFallbackResult, operationName);
        }
    }

    private async Task<T> FallbackOrDefaultAsync<T>(
        Exception ex,
        Func<SqliteAccountingStore, Task<T>> sqliteAction,
        Func<T>? noFallbackResult,
        string operationName)
    {
        if (fallbackStore is not null)
        {
            logger.LogWarning(ex, "PostgreSQL {Operation} başarısız; SQLite yerel yedeği kullanılıyor: {Message}", operationName, ex.Message);
            return await sqliteAction(fallbackStore);
        }

        if (noFallbackResult is not null)
        {
            logger.LogWarning(ex, "PostgreSQL {Operation} başarısız; varsayılan değer dönülüyor: {Message}", operationName, ex.Message);
            return noFallbackResult();
        }

        throw ex;
    }

    private static bool IsUndefinedTableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.PostgresException pgEx && pgEx.SqlState == UndefinedTableSqlState)
                return true;
        }
        return false;
    }

    private static bool IsDatabaseUnavailableException(Exception ex)
    {
        for (var current = ex; current != null; current = current.InnerException)
        {
            if (current is Npgsql.NpgsqlException ||
                current is System.Net.Sockets.SocketException ||
                current is TimeoutException)
            {
                return true;
            }
        }
        return false;
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}
