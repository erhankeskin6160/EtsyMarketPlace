namespace EtsyMarketPlace.Application.Shipping;

using System.Threading;
using System.Threading.Tasks;

public interface IEtsyLogisticsApiClient
{
    Task<string> GetRawShippingProfilesJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string?> GetRawReadinessDefinitionsJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string?> GetRawActiveListingsJsonAsync(string shopId, CancellationToken cancellationToken = default);
    Task<string> GetRawSellerTaxonomyNodesJsonAsync(string shopId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sipariş kuyruğu için son <paramref name="lookbackDays"/> günün Etsy siparişlerini ve tam alıcı adreslerini çeker.
    /// </summary>
    Task<IReadOnlyList<EtsyMarketPlace.Application.Orders.EtsyFulfillmentReceipt>> GetFulfillmentReceiptsAsync(
        string shopId,
        int lookbackDays = 45,
        bool? wasShipped = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Etsy API v3 POST /v3/application/shops/{shop_id}/receipts/{receipt_id}/tracking ucuna takip numarası ve kargo firması bildirir.
    /// </summary>
    Task<bool> SubmitTrackingAsync(
        string shopId,
        long receiptId,
        string trackingCode,
        string carrierName,
        bool sendBcc = false,
        CancellationToken cancellationToken = default);
}
