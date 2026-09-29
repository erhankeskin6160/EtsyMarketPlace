namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;
using EtsyMarketPlace.Application.Orders;
using EtsyMarketPlace.Domain.Orders;

public sealed class OrderQueueViewTests
{
    private static readonly DateTimeOffset Base = new(2026, 9, 30, 10, 0, 0, TimeSpan.Zero);

    private static EtsyOrderFulfillmentItem Order(
        long id,
        string buyer,
        DateTimeOffset date,
        string country = "DE",
        string title = "Ürün")
        => new()
        {
            ReceiptId = id,
            BuyerName = buyer,
            CountryCode = country,
            CountryName = country,
            OrderDate = date.UtcDateTime,
            Status = "Unfulfilled",
            Items = new List<EtsyOrderItem> { new() { Title = title, Quantity = 1 } }
        };

    [Fact]
    public void Filter_MatchesBuyerNameCaseInsensitive()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "Inge Neuer", Base.AddDays(-1)),
            Order(2, "Sarah Jenkins", Base.AddDays(-2))
        };

        var result = OrderQueueView.FilterAndSort(list, "inge", OrderQueueSort.NewestFirst);

        Assert.Single(result);
        Assert.Equal("Inge Neuer", result[0].BuyerName);
    }

    [Fact]
    public void Filter_MatchesReceiptId()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(4176634453, "Inge Neuer", Base.AddDays(-1)),
            Order(4178829104, "Sarah Jenkins", Base.AddDays(-2))
        };

        var result = OrderQueueView.FilterAndSort(list, "4176634", OrderQueueSort.NewestFirst);

        Assert.Single(result);
        Assert.Equal(4176634453, result[0].ReceiptId);
    }

    [Fact]
    public void Filter_MatchesProductTitle()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "A", Base.AddDays(-1), title: "Gothic Gargoyle"),
            Order(2, "B", Base.AddDays(-2), title: "Keyboard Rest")
        };

        var result = OrderQueueView.FilterAndSort(list, "gargoyle", OrderQueueSort.NewestFirst);

        Assert.Single(result);
        Assert.Equal(1, result[0].ReceiptId);
    }

    [Fact]
    public void Filter_MatchesCountry()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "A", Base.AddDays(-1), country: "DE"),
            Order(2, "B", Base.AddDays(-2), country: "US")
        };

        var result = OrderQueueView.FilterAndSort(list, "US", OrderQueueSort.NewestFirst);

        Assert.Single(result);
        Assert.Equal(2, result[0].ReceiptId);
    }

    [Fact]
    public void Sort_NewestFirst_PutsLatestOnTop()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "A", Base.AddDays(-3)),
            Order(2, "B", Base.AddDays(-1)),
            Order(3, "C", Base.AddDays(-2))
        };

        var result = OrderQueueView.FilterAndSort(list, null, OrderQueueSort.NewestFirst);

        Assert.Equal(new long[] { 2, 3, 1 }, result.Select(o => o.ReceiptId));
    }

    [Fact]
    public void Sort_OldestFirst()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "A", Base.AddDays(-3)),
            Order(2, "B", Base.AddDays(-1))
        };

        var result = OrderQueueView.FilterAndSort(list, null, OrderQueueSort.OldestFirst);

        Assert.Equal(new long[] { 1, 2 }, result.Select(o => o.ReceiptId));
    }

    [Fact]
    public void Sort_CustomerAscending_OrdersByBuyerName()
    {
        var list = new List<EtsyOrderFulfillmentItem>
        {
            Order(1, "Zeynep", Base),
            Order(2, "Ahmet", Base),
            Order(3, "Mehmet", Base)
        };

        var result = OrderQueueView.FilterAndSort(list, null, OrderQueueSort.CustomerAscending);

        Assert.Equal(new[] { "Ahmet", "Mehmet", "Zeynep" }, result.Select(o => o.BuyerName));
    }

    [Fact]
    public void EmptyQuery_ReturnsAllOrders()
    {
        var list = new List<EtsyOrderFulfillmentItem> { Order(1, "A", Base), Order(2, "B", Base) };

        Assert.Equal(2, OrderQueueView.FilterAndSort(list, "   ", OrderQueueSort.NewestFirst).Count);
        Assert.Equal(2, OrderQueueView.FilterAndSort(list, null, OrderQueueSort.NewestFirst).Count);
    }

    [Fact]
    public void NullInput_ReturnsEmpty()
    {
        Assert.Empty(OrderQueueView.FilterAndSort(null, "x", OrderQueueSort.NewestFirst));
    }
}
