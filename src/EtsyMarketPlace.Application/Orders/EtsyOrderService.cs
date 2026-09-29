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

    /// <summary>
    /// Kuyruğu canlı Etsy verisiyle senkronize eder: canlı listedeki siparişler baştan
    /// yazılır; bu oturumda kargolanan yerel kayıtlar (Gönderildi) korunur.
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
    }
}
