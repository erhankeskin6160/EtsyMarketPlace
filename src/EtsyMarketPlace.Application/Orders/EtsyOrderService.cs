namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Etsy siparişlerini yöneten ve kargo stüdyosuna sunan servis.
/// </summary>
public sealed class EtsyOrderService
{
    private readonly List<EtsyOrderFulfillmentItem> _orders = new();

    private readonly IOrderAddressRepository? _addressRepository;

    public EtsyOrderService(IOrderAddressRepository? addressRepository = null)
    {
        _addressRepository = addressRepository;
    }

    public Task<List<EtsyOrderFulfillmentItem>> GetOrdersAsync(string? filter = null, CancellationToken cancellationToken = default)
    {
        var result = _orders.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(filter))
        {
            string q = filter.Trim().ToLowerInvariant();
            result = result.Where(o =>
                o.OrderNumber.ToLowerInvariant().Contains(q) ||
                o.BuyerName.ToLowerInvariant().Contains(q) ||
                o.CountryName.ToLowerInvariant().Contains(q) ||
                o.City.ToLowerInvariant().Contains(q) ||
                o.IossNumber.ToLowerInvariant().Contains(q) ||
                o.Items.Any(i => i.Title.ToLowerInvariant().Contains(q)));
        }

        return Task.FromResult(result.ToList());
    }

    public Task MarkOrderAsShippedAsync(long receiptId, string carrierName, string trackingCode, string labelUrl)
    {
        var order = _orders.FirstOrDefault(o => o.ReceiptId == receiptId);
        if (order != null)
        {
            order.Status = "Shipped";
            order.SelectedCarrier = carrierName;
            order.TrackingCode = trackingCode;
            order.LabelUrl = labelUrl;
        }
        return Task.CompletedTask;
    }

    private readonly Dictionary<long, EtsyOrderFulfillmentItem> _addressOverrides = new();

    public void UpdateOrderAddress(long receiptId, EtsyOrderFulfillmentItem updated, string source = "Manual")
    {
        var order = _orders.FirstOrDefault(o => o.ReceiptId == receiptId);
        if (order != null)
        {
            ApplyAddress(order, updated);
        }
        _addressOverrides[receiptId] = updated;

        if (_addressRepository != null && !string.IsNullOrWhiteSpace(updated.StreetAddress))
        {
            var record = new OrderAddressRecord(
                receiptId,
                updated.BuyerName,
                updated.BuyerEmail,
                updated.Phone,
                updated.StreetAddress,
                updated.SecondAddress,
                updated.City,
                updated.State,
                updated.PostalCode,
                updated.CountryCode,
                updated.CountryName,
                source,
                DateTime.UtcNow);

            _ = Task.Run(async () =>
            {
                try
                {
                    await _addressRepository.SaveAsync(record);
                }
                catch { }
            });
        }
    }

    private static void ApplyAddress(EtsyOrderFulfillmentItem target, EtsyOrderFulfillmentItem source)
    {
        target.BuyerName = source.BuyerName;
        target.BuyerEmail = source.BuyerEmail;
        target.Phone = source.Phone;
        target.StreetAddress = source.StreetAddress;
        target.SecondAddress = source.SecondAddress;
        target.City = source.City;
        target.State = source.State;
        target.HasState = !string.IsNullOrWhiteSpace(source.State);
        target.PostalCode = source.PostalCode;
        target.CountryCode = source.CountryCode;
        target.CountryName = source.CountryName;
    }

    public static void ApplyRecord(EtsyOrderFulfillmentItem target, OrderAddressRecord record)
    {
        if (!string.IsNullOrWhiteSpace(record.BuyerName)) target.BuyerName = record.BuyerName;
        if (!string.IsNullOrWhiteSpace(record.BuyerEmail)) target.BuyerEmail = record.BuyerEmail;
        if (!string.IsNullOrWhiteSpace(record.Phone)) target.Phone = record.Phone;
        if (!string.IsNullOrWhiteSpace(record.StreetAddress)) target.StreetAddress = record.StreetAddress;
        if (!string.IsNullOrWhiteSpace(record.SecondAddress)) target.SecondAddress = record.SecondAddress;
        if (!string.IsNullOrWhiteSpace(record.City)) target.City = record.City;
        if (!string.IsNullOrWhiteSpace(record.State))
        {
            target.State = record.State;
            target.HasState = true;
        }
        if (!string.IsNullOrWhiteSpace(record.PostalCode)) target.PostalCode = record.PostalCode;
        if (!string.IsNullOrWhiteSpace(record.CountryCode)) target.CountryCode = record.CountryCode;
        if (!string.IsNullOrWhiteSpace(record.CountryName)) target.CountryName = record.CountryName;
    }

    /// <summary>
    /// SQLite veri tabanında daha önceden saklanmış adresleri kuyruktaki siparişlere bağlar.
    /// Program açıldığında veya yeniden yüklendiğinde anında çalışır.
    /// </summary>
    public async Task<int> HydrateSavedAddressesAsync(CancellationToken cancellationToken = default)
    {
        if (_addressRepository == null || _orders.Count == 0) return 0;

        try
        {
            var saved = await _addressRepository.GetAllAsync(cancellationToken);
            if (saved.Count == 0) return 0;

            int count = 0;
            foreach (var order in _orders)
            {
                if (saved.TryGetValue(order.ReceiptId, out var record) && !string.IsNullOrWhiteSpace(record.StreetAddress))
                {
                    ApplyRecord(order, record);
                    _addressOverrides[order.ReceiptId] = order;
                    count++;
                }
            }
            return count;
        }
        catch
        {
            return 0;
        }
    }

    /// <summary>
    /// CSV veya diğer kaynaklardan gelen adres kayıtlarını kuyruktaki siparişlerle eşleştirir.
    /// </summary>
    public int ApplyAddressRecords(IEnumerable<OrderAddressRecord> records)
    {
        if (records == null || _orders.Count == 0) return 0;

        int applied = 0;
        var map = records
            .Where(r => r.ReceiptId > 0 && !string.IsNullOrWhiteSpace(r.StreetAddress))
            .ToDictionary(r => r.ReceiptId);

        foreach (var order in _orders)
        {
            if (map.TryGetValue(order.ReceiptId, out var rec))
            {
                ApplyRecord(order, rec);
                _addressOverrides[order.ReceiptId] = order;
                applied++;
            }
        }

        return applied;
    }

    /// <summary>
    /// Kuyruğu canlı Etsy verisiyle senkronize eder: canlı listedeki siparişler baştan
    /// yazılır; bu oturumda kargolanan yerel kayıtlar (Gönderildi) ve yerel SQLite adresleri korunur.
    /// </summary>
    public void SyncLiveQueue(IReadOnlyList<EtsyOrderFulfillmentItem> liveOrders)
    {
        var incoming = liveOrders ?? Array.Empty<EtsyOrderFulfillmentItem>();

        // Bu oturumda kargolanan yerel kayıtlar her zaman önceliklidir: Etsy tarafı henüz
        // "gönderildi" görünmese bile kullanıcı aynı siparişi ikinci kez oluşturmasın.
        var localShipped = _orders.Where(OrderQueueFilter.IsShipped).ToList();
        var localShippedIds = new HashSet<long>(localShipped.Select(o => o.ReceiptId));

        _orders.Clear();
        _orders.AddRange(incoming.Where(o => !localShippedIds.Contains(o.ReceiptId)));
        _orders.AddRange(localShipped);

        // Kullanıcının elle düzenlediği veya SQLite'tan gelen adresleri koru
        foreach (var order in _orders)
        {
            if (_addressOverrides.TryGetValue(order.ReceiptId, out var ovr))
            {
                ApplyAddress(order, ovr);
            }
        }
    }
}
