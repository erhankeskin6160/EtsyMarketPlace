namespace EtsyMarketPlace.Application.Tests;

using System.Linq;
using Xunit;
using EtsyMarketPlace.Application.Orders;

public sealed class EtsyDemoOrdersTests
{
    [Fact]
    public void CreateQueue_ReturnsWellFormedDemoOrders()
    {
        var orders = EtsyDemoOrders.CreateQueue();

        Assert.Equal(3, orders.Count);
        Assert.Contains(orders, o => o.CountryCode == "DE"
            && o.Items.Count > 0
            && !string.IsNullOrWhiteSpace(o.Items[0].HsCode));
        Assert.Contains(orders, o => o.CountryCode == "US");
        Assert.Contains(orders, o => o.CountryCode == "GB");
        Assert.All(orders, o => Assert.True(o.ReceiptId > 0));
        Assert.All(orders, o => Assert.Equal("Unfulfilled", o.Status));
        Assert.All(orders, o => Assert.All(o.Items, i => Assert.True(i.WeightKg > 0)));
    }

    [Fact]
    public void CreateQueue_ReturnsFreshInstancesPerCall()
    {
        var first = EtsyDemoOrders.CreateQueue();
        var second = EtsyDemoOrders.CreateQueue();

        Assert.NotSame(first, second);
        Assert.NotSame(first[0], second[0]);

        first[0].Status = "Shipped";
        Assert.Equal("Unfulfilled", second[0].Status);
    }
}
