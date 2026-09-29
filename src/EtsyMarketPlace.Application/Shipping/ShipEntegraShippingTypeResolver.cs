namespace EtsyMarketPlace.Application.Shipping;

using System;

/// <summary>
/// ShipEntegra sipariş gümrük/gönderim tipi (shippingType) politikası.
/// Panelin manuel sipariş akışı alanı boş bırakır ve istekte hiç yer almaz
/// (canlı yakalama, 29.09.2026). ABD için DDP (1) canlıda doğrulandı; GB/UK için
/// sunucu DDP'yi reddeder (ERR.28050.1008: "Bu gönderi için DDP seçeneği geçerli
/// değildir"). Bu yüzden yalnızca ABD'de açıkça gönderilir; diğer ülkelerde alan
/// dışarıda bırakılır.
/// </summary>
public static class ShipEntegraShippingTypeResolver
{
    /// <summary>Ülkeye göre shippingType döndürür; null ise alan istekte yer almaz.</summary>
    public static int? Resolve(string? countryCode)
    {
        if (string.Equals(countryCode?.Trim(), "US", StringComparison.OrdinalIgnoreCase))
        {
            return 1;
        }

        return null;
    }
}
