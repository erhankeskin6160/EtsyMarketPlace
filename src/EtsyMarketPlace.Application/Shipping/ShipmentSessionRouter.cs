namespace EtsyMarketPlace.Application.Shipping;

/// <summary>
/// Gönderi oluşturma hatasında hangi taşıyıcı oturumunun yenileneceğini belirleyen
/// saf karar mantığı. UI katmanı bu kararı buradan alır; böylece davranış test edilebilir kalır.
/// </summary>
public static class ShipmentSessionRouter
{
    /// <summary>
    /// Hata bağlamı ShipEntegra'ya mı ait? Sağlayıcı adı veya hata mesajı
    /// "ShipEntegra" içeriyorsa true döner.
    /// </summary>
    public static bool IsShipEntegraContext(string? providerName, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(providerName) &&
            providerName.Contains("ShipEntegra", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(errorMessage) &&
               errorMessage.Contains("ShipEntegra", StringComparison.OrdinalIgnoreCase);
    }
}
