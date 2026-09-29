namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using Xunit;
using EtsyMarketPlace.Application.Orders;

/// <summary>
/// Canlı Etsy siparişlerinin kuyruğa alınma kuralı:
/// son 45 gün + "gönderilmemiş veya takip numarası olmayan".
/// </summary>
public sealed class EtsyFulfillmentQueuePolicyTests
{
    private static readonly DateTimeOffset Now = new(2026, 9, 30, 12, 0, 0, TimeSpan.Zero);

    private static EtsyFulfillmentReceipt Receipt(
        DateTimeOffset created,
        bool wasShipped,
        params string[] trackingCodes)
        => new(
            ReceiptId: 1001,
            BuyerName: "Test Buyer",
            BuyerEmail: "buyer@example.com",
            Phone: "+49 100 200",
            StreetAddress: "Test Str 1",
            SecondAddress: string.Empty,
            City: "Berlin",
            State: string.Empty,
            PostalCode: "10115",
            CountryCode: "DE",
            CountryName: "Germany",
            IossNumber: "IM0000000000",
            TotalPrice: 10m,
            Currency: "USD",
            CreatedAt: created,
            WasShipped: wasShipped,
            TrackingCodes: trackingCodes,
            Lines: new List<EtsyFulfillmentLine>
            {
                new(111, "Test Ürün", 2, 5m)
            });

    [Fact]
    public void ShouldInclude_UnshippedRecentOrder_True()
        => Assert.True(EtsyFulfillmentQueuePolicy.ShouldInclude(Receipt(Now.AddDays(-3), wasShipped: false), Now));

    [Fact]
    public void ShouldInclude_ShippedWithoutTracking_True()
        => Assert.True(EtsyFulfillmentQueuePolicy.ShouldInclude(Receipt(Now.AddDays(-3), wasShipped: true), Now));

    [Fact]
    public void ShouldInclude_ShippedWithTracking_False()
        => Assert.False(EtsyFulfillmentQueuePolicy.ShouldInclude(
            Receipt(Now.AddDays(-3), wasShipped: true, "TRK-1"), Now));

    [Fact]
    public void ShouldInclude_BlankTrackingCountsAsMissing_True()
        => Assert.True(EtsyFulfillmentQueuePolicy.ShouldInclude(
            Receipt(Now.AddDays(-3), wasShipped: true, "   "), Now));

    [Fact]
    public void ShouldInclude_OlderThanLookback_False()
        => Assert.False(EtsyFulfillmentQueuePolicy.ShouldInclude(
            Receipt(Now.AddDays(-46), wasShipped: false), Now));

    [Fact]
    public void ShouldInclude_WithinLookback_True()
        => Assert.True(EtsyFulfillmentQueuePolicy.ShouldInclude(
            Receipt(Now.AddDays(-44), wasShipped: false), Now));

    [Fact]
    public void ShouldInclude_ExactlyLookbackBoundary_True()
        => Assert.True(EtsyFulfillmentQueuePolicy.ShouldInclude(
            Receipt(Now.AddDays(-EtsyFulfillmentQueuePolicy.LookbackDays), wasShipped: false), Now));

    [Fact]
    public void ToQueueItem_MapsBuyerAddressAndLines()
    {
        var item = EtsyFulfillmentQueuePolicy.ToQueueItem(Receipt(Now.AddDays(-1), wasShipped: false));

        Assert.Equal(1001, item.ReceiptId);
        Assert.Equal("Test Buyer", item.BuyerName);
        Assert.Equal("DE", item.CountryCode);
        Assert.Equal("Germany", item.CountryName);
        Assert.Equal("IM0000000000", item.IossNumber);
        Assert.Equal("Unfulfilled", item.Status);
        Assert.Equal(Now.AddDays(-1).UtcDateTime, item.OrderDate);

        var line = Assert.Single(item.Items);
        Assert.Equal("Test Ürün", line.Title);
        Assert.Equal(2, line.Quantity);
        Assert.Equal(5m, line.Price);
    }

    [Fact]
    public void CountryDisplayName_KnownAndUnknownCodes()
    {
        Assert.Equal("Germany", EtsyFulfillmentQueuePolicy.CountryDisplayName("DE"));
        Assert.Equal("United States", EtsyFulfillmentQueuePolicy.CountryDisplayName("US"));
        Assert.Equal("ZZ", EtsyFulfillmentQueuePolicy.CountryDisplayName("ZZ"));
        Assert.Equal(string.Empty, EtsyFulfillmentQueuePolicy.CountryDisplayName(null));
    }
}
