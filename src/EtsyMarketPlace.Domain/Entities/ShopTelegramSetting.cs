namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağaza Telegram bildirim botu ayarları.
/// </summary>
public class ShopTelegramSetting
{
    public string ShopId { get; set; } = string.Empty;
    public string EncryptedBotToken { get; set; } = string.Empty;
    public string ChatId { get; set; } = string.Empty;
    public bool IsEnabled { get; set; } = false;
    public bool NotifyOnOrders { get; set; } = true;
    public bool NotifyOnStock { get; set; } = true;
    public bool DailyBriefEnabled { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
