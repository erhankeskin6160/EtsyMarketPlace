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
        InitializeSampleOrders();
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

    private void InitializeSampleOrders()
    {
        // 1. Ekran görüntüsündeki gerçek sipariş (Inge Neuer - Almanya)
        _orders.Add(new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4176634453,
            BuyerName = "Inge Neuer",
            BuyerEmail = "inge.neuer@example.de",
            StreetAddress = "Conrad-Scholl-Str 2",
            SecondAddress = "Wohnung 4B",
            City = "Koblenz",
            PostalCode = "56068",
            CountryCode = "DE",
            CountryName = "Germany",
            HasState = false,
            Phone = "+49 172 555 4321",
            IossNumber = "IM3720000224",
            IsVatCollected = true,
            TotalPrice = 11.00m,
            Currency = "USD",
            OrderDate = DateTime.UtcNow.AddHours(-3),
            Status = "Unfulfilled",
            Items = new List<EtsyOrderItem>
            {
                new()
                {
                    ListingId = 184920491,
                    Title = "3D Printed Gothic Gargoyle Mini Figure",
                    Quantity = 1,
                    Price = 11.00m,
                    Currency = "USD",
                    HsCode = "3926400000",
                    WeightKg = 0.4,
                    WidthCm = 15.0,
                    LengthCm = 20.0,
                    HeightCm = 10.0
                }
            }
        });

        // 2. ABD Siparişi (Sarah Jenkins - New York)
        _orders.Add(new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4178829104,
            BuyerName = "Sarah Jenkins",
            BuyerEmail = "sarah.j@example.com",
            StreetAddress = "742 Evergreen Terrace",
            City = "New York",
            State = "NY",
            PostalCode = "10001",
            CountryCode = "US",
            CountryName = "United States",
            HasState = true,
            Phone = "+1 212 555 0199",
            IossNumber = "",
            IsVatCollected = false,
            TotalPrice = 34.50m,
            Currency = "USD",
            OrderDate = DateTime.UtcNow.AddHours(-7),
            Status = "Unfulfilled",
            Items = new List<EtsyOrderItem>
            {
                new()
                {
                    ListingId = 185011942,
                    Title = "Custom Art Deco Wireless Charging Pad",
                    Quantity = 1,
                    Price = 34.50m,
                    Currency = "USD",
                    HsCode = "8504403000",
                    WeightKg = 0.6,
                    WidthCm = 18.0,
                    LengthCm = 22.0,
                    HeightCm = 8.0
                }
            }
        });

        // 3. İngiltere Siparişi (Oliver Smith - Londra)
        _orders.Add(new EtsyOrderFulfillmentItem
        {
            ReceiptId = 4179301284,
            BuyerName = "Oliver Smith",
            BuyerEmail = "oliver.smith@example.co.uk",
            StreetAddress = "221B Baker Street",
            City = "London",
            PostalCode = "NW1 6XE",
            CountryCode = "GB",
            CountryName = "United Kingdom",
            HasState = false,
            Phone = "+44 20 7946 0912",
            IossNumber = "GB371662991",
            IsVatCollected = true,
            TotalPrice = 24.90m,
            Currency = "USD",
            OrderDate = DateTime.UtcNow.AddHours(-12),
            Status = "Unfulfilled",
            Items = new List<EtsyOrderItem>
            {
                new()
                {
                    ListingId = 185124883,
                    Title = "Handcrafted Mechanical Keyboard Wrist Rest",
                    Quantity = 1,
                    Price = 24.90m,
                    Currency = "USD",
                    HsCode = "4421999000",
                    WeightKg = 0.5,
                    WidthCm = 12.0,
                    LengthCm = 35.0,
                    HeightCm = 5.0
                }
            }
        });
    }
}
