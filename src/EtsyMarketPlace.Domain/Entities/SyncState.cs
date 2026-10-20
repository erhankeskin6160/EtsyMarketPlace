namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağaza veri tipi bazında senkronizasyon ve imleç (cursor) takibi.
/// </summary>
public class SyncState
{
    public string ShopId { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public DateTime? LastSuccessfulSyncAt { get; set; }
    public DateTime? LastCursorAt { get; set; }
    public string? LastError { get; set; }

    public ShopConnection? Shop { get; set; }
}
