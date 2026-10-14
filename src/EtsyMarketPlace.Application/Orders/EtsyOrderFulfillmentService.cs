namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Shipping;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Etsy canlı sipariş kuyruğu ve teslimat adresi servisi (Clean Architecture Application Katmanı).
/// </summary>
public sealed class EtsyOrderFulfillmentService : IEtsyOrderFulfillmentService
{
    private readonly IEtsyLogisticsApiClient _logisticsApiClient;
    private readonly IOrderAddressRepository? _addressRepository;

    public EtsyOrderFulfillmentService(
        IEtsyLogisticsApiClient logisticsApiClient,
        IOrderAddressRepository? addressRepository = null)
    {
        _logisticsApiClient = logisticsApiClient ?? throw new ArgumentNullException(nameof(logisticsApiClient));
        _addressRepository = addressRepository;
    }

    public async Task<IReadOnlyList<EtsyOrderFulfillmentItem>> GetFulfillmentQueueAsync(
        string shopId,
        int lookbackDays = 45,
        bool? wasShipped = null,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(shopId))
        {
            return Array.Empty<EtsyOrderFulfillmentItem>();
        }

        var receipts = await _logisticsApiClient.GetFulfillmentReceiptsAsync(shopId, lookbackDays, wasShipped, cancellationToken);
        if (receipts == null || receipts.Count == 0)
        {
            return Array.Empty<EtsyOrderFulfillmentItem>();
        }

        // Kayıtlı yerel adresleri yükle
        IReadOnlyDictionary<long, OrderAddressRecord>? savedAddresses = null;
        if (_addressRepository != null)
        {
            try
            {
                savedAddresses = await _addressRepository.GetAllAsync(cancellationToken);
            }
            catch
            {
                // Yerel depo okunamasa dahi canlı API verisiyle devam et
            }
        }

        var queue = new List<EtsyOrderFulfillmentItem>(receipts.Count);
        foreach (var receipt in receipts)
        {
            var item = EtsyFulfillmentQueuePolicy.ToQueueItem(receipt);

            // Takip no ve gönderim durumu
            if (receipt.TrackingCodes != null && receipt.TrackingCodes.Count > 0)
            {
                item.TrackingCode = receipt.TrackingCodes[0];
            }

            if (receipt.WasShipped || !string.IsNullOrWhiteSpace(item.TrackingCode))
            {
                item.Status = "shipped";
            }
            else
            {
                item.Status = "unfulfilled";
            }

            // Yerel kayıtlı adres düzeltmesi varsa uygula
            if (savedAddresses != null && savedAddresses.TryGetValue(item.ReceiptId, out var savedRecord) && !string.IsNullOrWhiteSpace(savedRecord.StreetAddress))
            {
                EtsyOrderService.ApplyRecord(item, savedRecord);
            }

            // Eksik adres tespiti
            bool isMissing = string.IsNullOrWhiteSpace(item.StreetAddress) || string.IsNullOrWhiteSpace(item.City);
            item.IsAddressMissing = isMissing;
            if (isMissing)
            {
                item.AddressWarning = "Alıcı sokak veya şehir bilgisi eksik. Lütfen düzenleyin.";
            }

            queue.Add(item);
        }

        return queue;
    }

    public async Task SaveOrderAddressAsync(
        long receiptId,
        EtsyOrderFulfillmentItem updatedAddress,
        CancellationToken cancellationToken = default)
    {
        if (receiptId <= 0 || updatedAddress == null || _addressRepository == null)
        {
            return;
        }

        var record = new OrderAddressRecord(
            receiptId,
            updatedAddress.BuyerName,
            updatedAddress.BuyerEmail,
            updatedAddress.Phone,
            updatedAddress.StreetAddress,
            updatedAddress.SecondAddress,
            updatedAddress.City,
            updatedAddress.State,
            updatedAddress.PostalCode,
            updatedAddress.CountryCode,
            updatedAddress.CountryName,
            "WebManual",
            DateTime.UtcNow);

        await _addressRepository.SaveAsync(record, cancellationToken);
    }
}
