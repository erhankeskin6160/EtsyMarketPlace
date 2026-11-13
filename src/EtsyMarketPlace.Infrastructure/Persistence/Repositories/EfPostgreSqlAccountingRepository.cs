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
/// </summary>
public sealed class EfPostgreSqlAccountingRepository(
    EtsyMarketPlaceDbContext context,
    ILogger<EfPostgreSqlAccountingRepository> logger) : IAccountingRepository
{
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

    public async Task<ShopAccountingProfile> GetOrDefaultProfileAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        try
        {
            var existing = await context.AccountingShopProfiles
                .AsNoTracking()
                .FirstOrDefaultAsync(x => x.ShopId == key, cancellationToken);

            return existing ?? new ShopAccountingProfile { ShopId = key };
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            logger.LogWarning(ex, "PostgreSQL muhasebe profili okunamadı (ShopId={ShopId}); varsayılan profil dönülüyor.", key);
            return new ShopAccountingProfile { ShopId = key };
        }
    }

    public async Task<ShopAccountingProfile> UpsertProfileAsync(ShopAccountingProfile profile, CancellationToken cancellationToken = default)
    {
        var key = profile.ShopId.Trim();
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
    }

    public async Task<IReadOnlyList<OperatingExpenseEntry>> GetOperatingExpensesAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        try
        {
            return await context.AccountingOperatingExpenses
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.EntryDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            logger.LogWarning(ex, "PostgreSQL gider fişleri okunamadı (ShopId={ShopId}).", key);
            return Array.Empty<OperatingExpenseEntry>();
        }
    }

    public Task<OperatingExpenseEntry?> GetOperatingExpenseByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return context.AccountingOperatingExpenses
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
    }

    public async Task<OperatingExpenseEntry> AddOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
    {
        entry.EntryDate = EnsureUtc(entry.EntryDate);
        entry.RecurringEndDate = entry.RecurringEndDate.HasValue ? EnsureUtc(entry.RecurringEndDate.Value) : null;
        entry.CreatedAt = DateTime.UtcNow;
        entry.UpdatedAt = DateTime.UtcNow;

        context.AccountingOperatingExpenses.Add(entry);
        await context.SaveChangesAsync(cancellationToken);
        return entry;
    }

    public async Task<OperatingExpenseEntry?> UpdateOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default)
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
    }

    public async Task<bool> DeleteOperatingExpenseAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        var existing = await context.AccountingOperatingExpenses
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
        if (existing is null) return false;

        context.AccountingOperatingExpenses.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<FixedAssetItem>> GetFixedAssetsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        try
        {
            return await context.AccountingFixedAssets
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.AcquisitionDate)
                .ThenByDescending(x => x.Id)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            logger.LogWarning(ex, "PostgreSQL sabit kıymet listesi okunamadı (ShopId={ShopId}).", key);
            return Array.Empty<FixedAssetItem>();
        }
    }

    public Task<FixedAssetItem?> GetFixedAssetByIdAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return context.AccountingFixedAssets
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
    }

    public async Task<FixedAssetItem> AddFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
    {
        asset.AcquisitionDate = EnsureUtc(asset.AcquisitionDate);
        asset.CreatedAt = DateTime.UtcNow;
        asset.UpdatedAt = DateTime.UtcNow;

        context.AccountingFixedAssets.Add(asset);
        await context.SaveChangesAsync(cancellationToken);
        return asset;
    }

    public async Task<FixedAssetItem?> UpdateFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default)
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
    }

    public async Task<bool> DeleteFixedAssetAsync(long id, string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        var existing = await context.AccountingFixedAssets
            .FirstOrDefaultAsync(x => x.Id == id && x.ShopId == key, cancellationToken);
        if (existing is null) return false;

        context.AccountingFixedAssets.Remove(existing);
        await context.SaveChangesAsync(cancellationToken);
        return true;
    }

    public async Task<IReadOnlyList<InventoryPeriodAdjustment>> GetInventoryAdjustmentsAsync(string shopId, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        try
        {
            return await context.AccountingInventoryAdjustments
                .AsNoTracking()
                .Where(x => x.ShopId == key)
                .OrderByDescending(x => x.Year)
                .ThenByDescending(x => x.Month)
                .ToListAsync(cancellationToken);
        }
        catch (Exception ex) when (IsDatabaseUnavailableException(ex))
        {
            logger.LogWarning(ex, "PostgreSQL aralıklı envanter sayımları okunamadı (ShopId={ShopId}).", key);
            return Array.Empty<InventoryPeriodAdjustment>();
        }
    }

    public Task<InventoryPeriodAdjustment?> GetInventoryAdjustmentAsync(string shopId, int year, int month, CancellationToken cancellationToken = default)
    {
        var key = shopId.Trim();
        return context.AccountingInventoryAdjustments
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.ShopId == key && x.Year == year && x.Month == month, cancellationToken);
    }

    public async Task<InventoryPeriodAdjustment> UpsertInventoryAdjustmentAsync(InventoryPeriodAdjustment adjustment, CancellationToken cancellationToken = default)
    {
        var key = adjustment.ShopId.Trim();
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
    }

    private static DateTime EnsureUtc(DateTime value) =>
        value.Kind == DateTimeKind.Utc
            ? value
            : DateTime.SpecifyKind(value, DateTimeKind.Utc);
}

