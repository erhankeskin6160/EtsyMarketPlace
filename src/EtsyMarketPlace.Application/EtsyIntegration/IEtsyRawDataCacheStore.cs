namespace EtsyMarketPlace.Application.EtsyIntegration;

/// <summary>
/// Etsy API v3'ten çekilen ham JSON kayıtlarının (defter satırı veya sipariş fişi) SQLite'ta kalıcı önbellekleme DTO'su.
/// </summary>
public sealed record EtsyCachedRawItem(
    string ItemId,
    long CreatedUnix,
    long UpdatedUnix,
    string RawJson);

/// <summary>
/// DB-First + Delta Sync için dönem ve veri kümesi bazlı senkronizasyon sınır işaretçisi (watermark).
/// </summary>
public sealed record EtsySyncWatermark(
    string ShopId,
    string DatasetKey,
    long MinSyncedUnix,
    long MaxSyncedUnix,
    long LastDeltaSyncUnix);

/// <summary>
/// Etsy ham defter kayıtları, sipariş fişleri ve ledger->receipt eşleşmeleri için kalıcı önbellek deposu.
/// API kota tüketimini %95+ azaltmak amacıyla EtsyApiClient tarafından DB-First + Delta Sync akışında kullanılır.
/// </summary>
public interface IEtsyRawDataCacheStore
{
    Task<EtsySyncWatermark?> GetWatermarkAsync(string shopId, string datasetKey, CancellationToken cancellationToken = default);

    Task SaveWatermarkAsync(EtsySyncWatermark watermark, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRawLedgerJsonByRangeAsync(
        string shopId,
        long minCreatedUnix,
        long maxCreatedUnix,
        CancellationToken cancellationToken = default);

    Task UpsertRawLedgerItemsAsync(
        string shopId,
        IReadOnlyCollection<EtsyCachedRawItem> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<string>> GetRawReceiptsJsonByRangeAsync(
        string shopId,
        long minCreatedUnix,
        long maxCreatedUnix,
        CancellationToken cancellationToken = default);

    Task<string?> GetRawReceiptJsonByIdAsync(
        string shopId,
        long receiptId,
        CancellationToken cancellationToken = default);

    Task UpsertRawReceiptsAsync(
        string shopId,
        IReadOnlyCollection<EtsyCachedRawItem> items,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyDictionary<long, long>> GetLedgerReceiptMappingsAsync(
        string shopId,
        IReadOnlyCollection<long> ledgerEntryIds,
        CancellationToken cancellationToken = default);

    Task UpsertLedgerReceiptMappingsAsync(
        string shopId,
        IReadOnlyDictionary<long, long> mappings,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// AsyncLocal tabanlı senkronizasyon bağlamı.
/// Controller katmanında refresh=true geldiğinde Delta Sync zorlamasını altyapı istemcisine taşır.
/// </summary>
public static class EtsySyncContext
{
    private static readonly AsyncLocal<bool> _forceDeltaRefresh = new();

    public static bool ForceDeltaRefresh => _forceDeltaRefresh.Value;

    public static IDisposable BeginForceRefresh()
    {
        var previous = _forceDeltaRefresh.Value;
        _forceDeltaRefresh.Value = true;
        return new ResetScope(previous);
    }

    private sealed class ResetScope(bool previous) : IDisposable
    {
        public void Dispose() => _forceDeltaRefresh.Value = previous;
    }
}
