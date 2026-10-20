namespace EtsyMarketPlace.Domain.Entities;

/// <summary>
/// Mağazaya özel Etsy API istemci kimlik bilgileri (Keystring, Shared Secret).
/// </summary>
public class EtsyAppCredential
{
    public string ShopId { get; set; } = string.Empty;
    public string EncryptedKeystring { get; set; } = string.Empty;
    public string EncryptedSharedSecret { get; set; } = string.Empty;
    public string RedirectUri { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ShopConnection? Shop { get; set; }
}
