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

            // Takip no ve gönderim durumu: "No tracking" takip kodu sayılamaz
            if (receipt.TrackingCodes != null && receipt.TrackingCodes.Count > 0)
            {
                var candidate = receipt.TrackingCodes[0]?.Trim();
                if (!string.IsNullOrWhiteSpace(candidate) && !candidate.Equals("No tracking", StringComparison.OrdinalIgnoreCase))
                {
                    item.TrackingCode = candidate;
                }
                else
                {
                    item.TrackingCode = string.Empty;
                }
            }

            bool hasValidTracking = !string.IsNullOrWhiteSpace(item.TrackingCode) &&
                                    !string.Equals(item.TrackingCode.Trim(), "No tracking", StringComparison.OrdinalIgnoreCase);

            item.Status = hasValidTracking ? "shipped" : "unfulfilled";

            // Yerel kayıtlı adres / maliyet / GTİP / takip no düzeltmesi varsa uygula
            if (savedAddresses != null && savedAddresses.TryGetValue(item.ReceiptId, out var savedRecord))
            {
                EtsyOrderService.ApplyRecord(item, savedRecord);
            }

            // Kural: "New" siparişleri ve "Completed" olup takip kodu olmayan siparişler kesinlikle Bekleyen (unfulfilled) sekmesindedir.
            // Sadece ve sadece gerçek, geçerli bir kargo takip numarası olanlar Gönderildi (shipped) sekmesine geçer.
            bool finalHasValidTracking = !string.IsNullOrWhiteSpace(item.TrackingCode) &&
                                         !string.Equals(item.TrackingCode.Trim(), "No tracking", StringComparison.OrdinalIgnoreCase);

            item.Status = finalHasValidTracking ? "shipped" : "unfulfilled";

            // Eksik adres tespiti
            bool isMissing = string.IsNullOrWhiteSpace(item.StreetAddress) || string.IsNullOrWhiteSpace(item.City);
            item.IsAddressMissing = isMissing;
            item.AddressWarning = isMissing
                ? "Alıcı sokak veya şehir bilgisi eksik. Lütfen düzenleyin."
                : string.Empty;

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

        string gtip = !string.IsNullOrWhiteSpace(updatedAddress.GtipCode)
            ? updatedAddress.GtipCode.Trim()
            : (updatedAddress.Items?.FirstOrDefault(i => !string.IsNullOrWhiteSpace(i.HsCode))?.HsCode ?? string.Empty);

        var record = new OrderAddressRecord(
            receiptId,
            updatedAddress.BuyerName ?? string.Empty,
            updatedAddress.BuyerEmail ?? string.Empty,
            updatedAddress.Phone ?? string.Empty,
            updatedAddress.StreetAddress ?? string.Empty,
            updatedAddress.SecondAddress ?? string.Empty,
            updatedAddress.City ?? string.Empty,
            updatedAddress.State ?? string.Empty,
            updatedAddress.PostalCode ?? string.Empty,
            updatedAddress.CountryCode ?? string.Empty,
            updatedAddress.CountryName ?? string.Empty,
            "WebManual",
            DateTime.UtcNow)
        {
            IossNumber = updatedAddress.IossNumber ?? string.Empty,
            ProductCostUsd = updatedAddress.ProductCostUsd,
            ShippingCostUsd = updatedAddress.ShippingCostUsd,
            WeightKg = updatedAddress.WeightKg,
            WidthCm = updatedAddress.WidthCm,
            LengthCm = updatedAddress.LengthCm,
            HeightCm = updatedAddress.HeightCm,
            GtipCode = gtip,
            SelectedCarrier = updatedAddress.SelectedCarrier ?? string.Empty,
            CarrierServiceName = updatedAddress.CarrierServiceName ?? string.Empty,
            TrackingCode = updatedAddress.TrackingCode ?? string.Empty,
            LabelUrl = updatedAddress.LabelUrl ?? string.Empty
        };

        await _addressRepository.SaveAsync(record, cancellationToken);
    }
}
