namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağazaya özel AI prompt, model ve davranış ayarları (JSON formatında saklanır).
/// </summary>
public class ShopAiSetting
{
    public string ShopId { get; set; } = string.Empty;
    public string SettingsJson { get; set; } = "{}";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
