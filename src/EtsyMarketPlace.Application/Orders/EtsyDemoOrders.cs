namespace EtsyMarketPlace.Application.Orders;

using System;
using System.Collections.Generic;
using EtsyMarketPlace.Domain.Orders;

/// <summary>
/// Etsy API bağlantısı olmayan ortamlarda (ör. yerel test PC'si) sipariş kuyruğunun
/// boş kalmaması için kullanılan demo siparişler. Canlı Etsy bağlantısı kurulduğunda
/// bu liste hiç kullanılmaz; yerine gerçek siparişler yüklenir.
///
/// Buradaki veriler uydurma test verisidir ve hiçbir Etsy yazma işlemi tetiklemez;
/// yalnızca arayüz akışının (teklif, gönderi oluşturma, yerel "Gönderildi" durumu)
/// denenebilmesi içindir.
/// </summary>
public static class EtsyDemoOrders
{
    public static List<EtsyOrderFulfillmentItem> CreateQueue() => new()
    {
        // Almanya siparişi (GTİP çözümleme akışı için)
        new EtsyOrderFulfillmentItem
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
        },

        // ABD siparişi (US HS kodu kapısı için)
        new EtsyOrderFulfillmentItem
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
                    HsCode = "8504409580",
                    WeightKg = 0.6,
                    WidthCm = 18.0,
                    LengthCm = 22.0,
                    HeightCm = 8.0
                }
            }
        },

        // İngiltere siparişi (Eko Plus ülke eşlemesi için)
        new EtsyOrderFulfillmentItem
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
        }
    };
}
