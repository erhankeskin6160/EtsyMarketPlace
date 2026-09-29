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

    /// <summary>
    /// Hata bağlamı Aras Global'e mi ait? Sağlayıcı adı veya hata mesajı
    /// "Aras" içeriyorsa true döner; diğer taşıyıcı hatalarında oturum
    /// yenileme akışı açılmaz.
    /// </summary>
    public static bool IsArasGlobalContext(string? providerName, string? errorMessage)
    {
        if (!string.IsNullOrWhiteSpace(providerName) &&
            providerName.Contains("Aras", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        return !string.IsNullOrWhiteSpace(errorMessage) &&
               errorMessage.Contains("Aras", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hata gerçek bir oturum/yetki sorununa mı işaret ediyor? İş kuralı redleri
    /// (örn. DDP geçersiz) bu kapsama girmez; UI yalnızca gerçek oturum hatalarında
    /// oturum yenileme akışını açar.
    /// </summary>
    public static bool IsSessionFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        return errorMessage.Contains("oturum", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("yetkilendirme", StringComparison.OrdinalIgnoreCase) ||
               errorMessage.Contains("giriş yap", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>
    /// Hata ağ/taşıma katmanına mı ait (soket, DNS, zaman aşımı, bağlantı kesilmesi)?
    /// Bu tür hatalarda oturum yenileme akışı açılmaz; kullanıcıdan yeniden denemesi istenir.
    /// </summary>
    public static bool IsNetworkLevelFailure(string? errorMessage)
    {
        if (string.IsNullOrWhiteSpace(errorMessage))
        {
            return false;
        }

        string[] markers =
        {
            "hizmet sağlayıcısı", "hizmet saglayicisi",
            "socket",
            "bağlantı", "baglanti",
            "bağlanamadı", "baglanamadi",
            "ağ hatası", "ag hatasi",
            "connection", "network",
            "timed out", "timeout", "zaman aşımı", "zaman asimi",
            "aborted", "kesildi", "sıfırlandı", "sifirlandi",
            "çözümlenemedi", "cozumlenemedi",
            "getaddrinfo", "err_name", "err_connection", "err_failed",
            "ssl", "tls"
        };

        return markers.Any(m => errorMessage.Contains(m, StringComparison.OrdinalIgnoreCase));
    }
}
