using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Accounting.Entities;

namespace EtsyMarketPlace.Application.Accounting;

/// <summary>
/// Clean Architecture Persistence Port: Mağaza TDHP muhasebe profili, gider fişleri,
/// sabit kıymet (demirbaş/makine) kayıtları ve aralıklı envanter dönem sayımları.
/// </summary>
public interface IAccountingRepository
{
    Task<ShopAccountingProfile> GetOrDefaultProfileAsync(string shopId, CancellationToken cancellationToken = default);
    Task<ShopAccountingProfile> UpsertProfileAsync(ShopAccountingProfile profile, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<OperatingExpenseEntry>> GetOperatingExpensesAsync(string shopId, CancellationToken cancellationToken = default);
    Task<OperatingExpenseEntry?> GetOperatingExpenseByIdAsync(long id, string shopId, CancellationToken cancellationToken = default);
    Task<OperatingExpenseEntry> AddOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default);
    Task<OperatingExpenseEntry?> UpdateOperatingExpenseAsync(OperatingExpenseEntry entry, CancellationToken cancellationToken = default);
    Task<bool> DeleteOperatingExpenseAsync(long id, string shopId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<FixedAssetItem>> GetFixedAssetsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<FixedAssetItem?> GetFixedAssetByIdAsync(long id, string shopId, CancellationToken cancellationToken = default);
    Task<FixedAssetItem> AddFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default);
    Task<FixedAssetItem?> UpdateFixedAssetAsync(FixedAssetItem asset, CancellationToken cancellationToken = default);
    Task<bool> DeleteFixedAssetAsync(long id, string shopId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<InventoryPeriodAdjustment>> GetInventoryAdjustmentsAsync(string shopId, CancellationToken cancellationToken = default);
    Task<InventoryPeriodAdjustment?> GetInventoryAdjustmentAsync(string shopId, int year, int month, CancellationToken cancellationToken = default);
    Task<InventoryPeriodAdjustment> UpsertInventoryAdjustmentAsync(InventoryPeriodAdjustment adjustment, CancellationToken cancellationToken = default);
}

