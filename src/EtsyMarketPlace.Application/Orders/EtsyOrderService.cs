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

    public EtsyOrderService()
    {
        // Kuyruk artık uydurma veriyle doldurulmaz; canlı Etsy verisi
        // arayüz tarafından SyncLiveQueue ile beslenir.
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

    public void UpdateOrderAddress(long receiptId, EtsyOrderFulfillmentItem updated)
    {
        var order = _orders.FirstOrDefault(o => o.ReceiptId == receiptId);
        if (order != null)
        {
            ApplyAddress(order, updated);
        }
        _addressOverrides[receiptId] = updated;
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

    /// <summary>
    /// Kuyruğu canlı Etsy verisiyle senkronize eder: canlı listedeki siparişler baştan
    /// yazılır; bu oturumda kargolanan yerel kayıtlar (Gönderildi) ve manuel adres düzenlemeleri korunur.
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

        // Kullanıcının elle düzenlediği adresleri koru
        foreach (var order in _orders)
        {
            if (_addressOverrides.TryGetValue(order.ReceiptId, out var ovr))
            {
                ApplyAddress(order, ovr);
            }
        }
    }
}
