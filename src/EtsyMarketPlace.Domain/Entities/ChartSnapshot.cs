namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Dashboard grafikleri için Base64 PNG önbelleği.
/// </summary>
public class ChartSnapshot
{
    public string ShopId { get; set; } = string.Empty;
    public string ChartType { get; set; } = string.Empty;
    public string PeriodStart { get; set; } = string.Empty;
    public string PeriodEnd { get; set; } = string.Empty;
    public string ImagePngBase64 { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
