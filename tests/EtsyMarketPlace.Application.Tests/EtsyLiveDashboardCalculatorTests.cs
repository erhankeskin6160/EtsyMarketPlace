namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using Xunit;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class EtsyLiveDashboardCalculatorTests
{
    private static EtsyDashboardReceipt Receipt(
        long id,
        decimal grand,
        decimal subtotal,
        decimal shipping = 0m,
        decimal tax = 0m,
        decimal refunded = 0m,
        bool canceled = false,
        bool paid = true,
        string title = "Test Ürün",
        int quantity = 1,
        DateTimeOffset? createdAt = null)
        => new(
            id,
            createdAt ?? new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            paid,
            canceled,
            false,
            "USD",
            grand,
            subtotal,
            shipping,
            0m,
            tax,
            refunded,
            new List<EtsyDashboardReceiptItem> { new(title, 111, quantity) });

    private static Dictionary<long, EtsyDashboardOrderCost> NoCosts() => new();

    [Fact]
    public void BuildOrderRows_ComputesEtsyFeeBreakdown_OnPaidOrder()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(4188712345, 35.91m, 32.00m, shipping: 3.91m) },
            Array.Empty<EtsyDashboardLedgerFee>(),
            NoCosts());

        var row = Assert.Single(rows);
        Assert.Equal(35.91m, row.GrandTotal);
        Assert.Equal(30.51m, row.NetProfitUsd);
        Assert.False(row.HasCostData);
        Assert.False(row.IsCanceled);
    }

    [Fact]
    public void BuildOrderRows_UsesLedgerOffsiteAdsFee_WhenMappedByReceiptId()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(4188712345, 35.91m, 32.00m, shipping: 3.91m) },
            new[] { new EtsyDashboardLedgerFee("offsite_ads", 4188712345, null, -5.00m) },
            NoCosts());

        Assert.Equal(25.51m, rows[0].NetProfitUsd);
    }

    [Fact]
    public void BuildOrderRows_DeductsOrderCosts_WhenPresent()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(4188712345, 35.91m, 32.00m, shipping: 3.91m) },
            Array.Empty<EtsyDashboardLedgerFee>(),
            new Dictionary<long, EtsyDashboardOrderCost> { [4188712345] = new(10.00m, 5.00m) });

        var row = Assert.Single(rows);
        Assert.Equal(15.51m, row.NetProfitUsd);
        Assert.True(row.HasCostData);
    }

    [Fact]
    public void BuildOrderRows_ZeroesCanceledOrder()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(99, 50.00m, 45.00m, canceled: true) },
            Array.Empty<EtsyDashboardLedgerFee>(),
            NoCosts());

        var row = Assert.Single(rows);
        Assert.Equal(0m, row.GrandTotal);
        Assert.Equal(0m, row.NetProfitUsd);
        Assert.True(row.IsCanceled);
    }

    [Fact]
    public void BuildOrderRows_PartialRefund_ReducesGrandTotal()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(77, 35.91m, 32.00m, shipping: 3.91m, refunded: 10.00m) },
            Array.Empty<EtsyDashboardLedgerFee>(),
            NoCosts());

        Assert.Equal(25.91m, rows[0].GrandTotal);
        // paymentFee, iade sonrasi duzeltilmis grandTotal uzerinden hesaplanir (masaustu formulu):
        // txn=2.33 + payment=round(25.91*0.065)+0.14=1.82 + reg=0.60 => net=25.91-4.75=21.16
        Assert.Equal(21.16m, rows[0].NetProfitUsd);
    }

    [Fact]
    public void BuildOrderRows_SkipsUnpaidNonCanceledReceipt()
    {
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(
            new[] { Receipt(55, 10.00m, 10.00m, paid: false) },
            Array.Empty<EtsyDashboardLedgerFee>(),
            NoCosts());

        Assert.Empty(rows);
    }

    [Fact]
    public void BuildDailySeries_AggregatesLocalDaysWithTurkishLabels()
    {
        var receipts = new[]
        {
            Receipt(11, 10.00m, 10.00m, createdAt: new DateTimeOffset(2026, 10, 1, 0, 30, 0, TimeSpan.Zero)),
            Receipt(12, 20.00m, 20.00m, createdAt: new DateTimeOffset(2026, 9, 30, 22, 15, 0, TimeSpan.Zero))
        };
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, Array.Empty<EtsyDashboardLedgerFee>(), NoCosts());
        var series = EtsyLiveDashboardCalculator.BuildDailySeries(rows, 2026, 10, new DateTimeOffset(2026, 10, 5, 12, 0, 0, TimeSpan.Zero));

        Assert.Equal(5, series.Labels.Length);
        Assert.Equal("01 Eki", series.Labels[0]);
        Assert.Equal("02 Eki", series.Labels[1]);
        Assert.Equal(30.00m, series.GrossSales[0]);
        Assert.Equal(25.32m, series.NetProfit[0]);
        Assert.Equal(0m, series.GrossSales[1]);
    }

    [Fact]
    public void PickTopProduct_ReturnsHighestRevenueTitle()
    {
        var receipts = new[]
        {
            Receipt(21, 20.00m, 20.00m, title: "Alpha"),
            Receipt(22, 10.00m, 10.00m, title: "Beta")
        };
        var rows = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, Array.Empty<EtsyDashboardLedgerFee>(), NoCosts());
        var top = EtsyLiveDashboardCalculator.PickTopProduct(rows);

        Assert.Equal("Alpha", top.Title);
        Assert.Equal(20.00m, top.Revenue);
    }

    [Fact]
    public void BuildDetailedOrderRows_PopulatesAll15Fields_WithAccurateFeesAndProfit()
    {
        var receipt = new EtsyDashboardReceipt(
            4188719047,
            new DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.Zero),
            true,
            false,
            false,
            "USD",
            138.59m,
            110.00m,
            15.00m,
            0m,
            13.59m,
            0m,
            new[] { new EtsyDashboardReceiptItem("Ben10 Albedo Omnitrix", 12345, 1) },
            "Christian Johnson",
            998877,
            "christian@example.com",
            "Completed");

        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4188719047] = new EtsyDashboardOrderCost(25.00m, 10.00m)
        };

        var ledgerFees = new[]
        {
            new EtsyDashboardLedgerFee("offsite_ads", 4188719047, "Offsite ad fee for order #4188719047", 20.08m)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(new[] { receipt }, ledgerFees, costs, 49.19m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(4188719047, row.ReceiptId);
        Assert.Equal("Christian Johnson", row.BuyerName);
        Assert.Equal("Ben10 Albedo Omnitrix", row.ProductTitle);
        Assert.Equal("completed", row.OrderStatus);
        Assert.Equal("Tamamlandı", row.DisplayStatus);
        Assert.Equal(138.59m, row.GrandTotalUsd);
        Assert.Equal(20.08m, row.OffsiteAdFeeUsd);
        Assert.Equal(35.00m, row.ProductCostUsd);
        Assert.True(row.HasCostData);
        Assert.Equal(49.19m, row.ExchangeRate);
        Assert.True(row.EtsyFeesUsd > 0);
        Assert.True(row.NetProfitUsd > 0);
        Assert.Equal(Math.Round(row.NetProfitUsd * 49.19m, 2), row.NetProfitTry);
    }

    [Fact]
    public void BuildDetailedOrderRows_SetsCanceledStatus_WhenOrderIsCanceled()
    {
        var receipt = new EtsyDashboardReceipt(
            4188719099,
            new DateTimeOffset(2026, 10, 8, 14, 30, 0, TimeSpan.Zero),
            true,
            true,
            false,
            "USD",
            100.00m,
            80.00m,
            10.00m,
            0m,
            10.00m,
            100.00m,
            new[] { new EtsyDashboardReceiptItem("Canceled Item", 999, 1) },
            "Jane Doe",
            Status: "Canceled");

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(new[] { receipt }, Array.Empty<EtsyDashboardLedgerFee>(), NoCosts());

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal("canceled", row.OrderStatus);
        Assert.Equal("İptal Edildi", row.DisplayStatus);
        Assert.Equal(0m, row.GrandTotalUsd);
        Assert.Equal(0m, row.NetProfitUsd);
        Assert.Equal(0m, row.NetProfitTry);
    }
}

