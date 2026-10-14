namespace EtsyMarketPlace.Application.Orders;

using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Etsy canlı sipariş kuyruğu ve alıcı adres yönetimi use-case servisi.
/// </summary>
public interface IEtsyOrderFulfillmentService
{
    /// <summary>
    /// Canlı Etsy API'sinden siparişleri, alıcı teslimat adreslerini ve satır kalemlerini getirir;
    /// yerel kayıtlı adres düzeltmeleriyle (SQLite) zenginleştirir.
    /// </summary>
    Task<IReadOnlyList<EtsyOrderFulfillmentItem>> GetFulfillmentQueueAsync(
        string shopId,
        int lookbackDays = 45,
        bool? wasShipped = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Kullanıcının arayüzden düzenlediği alıcı adresini kalıcı SQLite deposuna kaydeder.
    /// </summary>
    Task SaveOrderAddressAsync(
        long receiptId,
        EtsyOrderFulfillmentItem updatedAddress,
        CancellationToken cancellationToken = default);
}
