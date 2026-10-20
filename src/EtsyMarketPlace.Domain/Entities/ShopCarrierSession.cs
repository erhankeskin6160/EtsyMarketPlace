namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağaza kargo taşıyıcı oturumu ve şifreli kimlik bilgileri.
/// </summary>
public class ShopCarrierSession
{
    public string ShopId { get; set; } = string.Empty;
    public string CarrierId { get; set; } = string.Empty;
    public string EncryptedCredentials { get; set; } = string.Empty;
    public string AccountNo { get; set; } = string.Empty;
    public string ServiceLevel { get; set; } = string.Empty;
    public bool IsConnected { get; set; } = false;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
