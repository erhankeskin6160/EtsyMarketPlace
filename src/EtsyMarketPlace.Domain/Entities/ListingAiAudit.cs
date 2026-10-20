namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// AI SEO denetimi, önerilen optimizasyonlar ve SEO puanı.
/// </summary>
public class ListingAiAudit
{
    public string ShopId { get; set; } = string.Empty;
    public string ListingId { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public int CurrentSeoScore { get; set; }
    public int OptimizedSeoScore { get; set; }
    public string Status { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public string ResultJson { get; set; } = "{}";
    public DateTime AuditedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
