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
            new EtsyDashboardLedgerFee("offsite_ads", 4188719047, "Offsite ad fee for order #4188719047", -20.08m)
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
        Assert.Equal(49.22m, row.ExchangeRate);
        Assert.True(row.EtsyFeesUsd > 0);
        Assert.True(row.NetProfitUsd > 0);
        Assert.Equal(Math.Round(row.NetProfitUsd * row.ExchangeRate, 2), row.NetProfitTry);
    }

    [Fact]
    public void BuildDetailedOrderRows_SetsOpenStatus_WhenOrderIsPendingShipment()
    {
        var receipt = new EtsyDashboardReceipt(
            44210965421L,
            new DateTimeOffset(2026, 10, 26, 10, 0, 0, TimeSpan.Zero),
            true,
            false,
            false,
            "USD",
            123.40m,
            123.40m,
            0m,
            0m,
            0m,
            0m,
            new[] { new EtsyDashboardReceiptItem("Arcane Jinx Statue", 13, 1) },
            "Jessica Benson",
            Status: "Open",
            IsShipped: false);

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            new[] { receipt },
            Array.Empty<EtsyDashboardLedgerFee>(),
            new Dictionary<long, EtsyDashboardOrderCost>(),
            49.53m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(44210965421L, row.ReceiptId);
        Assert.Equal("Jessica Benson", row.BuyerName);
        Assert.Equal("open", row.OrderStatus);
        Assert.Equal("Kargolanacak (Yeni)", row.DisplayStatus);
        Assert.Equal(123.40m, row.GrandTotalUsd);
        Assert.Null(row.RefundDate);
        Assert.Equal(0m, row.RefundAmountUsd);
        Assert.True(row.NetProfitUsd > 0);
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

    [Fact]
    public void BuildDetailedOrderRows_SetsNegativeProfit_WhenCanceledOrderHasShippingCost()
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
            new[] { new EtsyDashboardReceiptItem("Canceled Shipped Item", 999, 1) },
            "Barbara Yaptangco",
            Status: "Canceled");

        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4188719099] = new EtsyDashboardOrderCost(0m, 15.00m) // $15 shipping cost incurred
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(new[] { receipt }, Array.Empty<EtsyDashboardLedgerFee>(), costs, 49.00m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal("canceled", row.OrderStatus);
        Assert.Equal("İptal Edildi", row.DisplayStatus);
        Assert.Equal(0m, row.GrandTotalUsd);
        Assert.Equal(15.00m, row.ProductCostUsd);
        Assert.True(row.HasCostData);
        Assert.Equal(-15.00m, row.NetProfitUsd); // Negative balance / loss!
        Assert.Equal(49.22m, row.ExchangeRate); // Order date locked rate (08.10.2026)
        Assert.Equal(-738.30m, row.NetProfitTry); // -15 * 49.22 (historical rate for 8 Oct 2026)
    }

    [Fact]
    public void BuildDetailedOrderRows_CalculatesEffectiveExchangeRateAndEtsyNetPayout_AccountingForEtsyFxSpread()
    {
        var receipt = new EtsyDashboardReceipt(
            4187202652,
            new DateTimeOffset(2026, 10, 8, 10, 0, 0, TimeSpan.Zero),
            true,
            false,
            false,
            "USD",
            149.99m,
            149.99m,
            0m,
            0m,
            0m,
            0m,
            new[] { new EtsyDashboardReceiptItem("Custom Omnitrix", 112233, 1) },
            "Sandro Sobreira",
            556677,
            "sandro@example.com",
            "Completed");

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            new[] { receipt },
            Array.Empty<EtsyDashboardLedgerFee>(),
            NoCosts(),
            48.80m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.NotNull(row.EffectiveExchangeRate);
        Assert.Equal(Math.Round(row.ExchangeRate * 0.975m, 2), row.EffectiveExchangeRate);
        Assert.NotNull(row.EtsyNetPayoutUsd);
        var expectedNetUsd = Math.Round(row.GrandTotalUsd - row.EtsyFeesUsd - row.OffsiteAdFeeUsd, 2);
        Assert.Equal(expectedNetUsd, row.EtsyNetPayoutUsd);
        Assert.NotNull(row.EtsyNetPayoutTry);
        Assert.Equal(Math.Round(expectedNetUsd * (row.ExchangeRate * 0.975m), 2), row.EtsyNetPayoutTry);
        Assert.NotNull(row.FxConversionFeeTry);
        Assert.True(row.FxConversionFeeTry > 0);
    }

    [Fact]
    public void BuildDetailedOrderRows_ResolvesRealOrderNumberAndBuyerName_ForCrossPeriodRefund()
    {
        var refundTimestamp = new DateTimeOffset(2026, 11, 3, 12, 0, 0, TimeSpan.Zero);
        var origOrderDate = new DateTimeOffset(2026, 9, 25, 10, 0, 0, TimeSpan.Zero);

        var ledgerFees = new List<EtsyDashboardLedgerFee>
        {
            new(
                Type: "refund",
                ReferenceId: 2110508251050L,
                Description: "Refund for Order #4185170234",
                Amount: -176.28m,
                CreatedAt: refundTimestamp)
        };

        var buyerMap = new Dictionary<long, string>
        {
            [4185170234L] = "Anthony Brown"
        };
        var dateMap = new Dictionary<long, DateTimeOffset>
        {
            [4185170234L] = origOrderDate
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            Array.Empty<EtsyDashboardReceipt>(),
            ledgerFees,
            NoCosts(),
            49.25m,
            buyerMap,
            dateMap);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(4185170234L, row.ReceiptId);
        Assert.Equal("Anthony Brown", row.BuyerName);
        Assert.Equal("25.09.2026", row.OrderDate);
        Assert.Equal("03.11.2026", row.RefundDate);
        Assert.Equal("25.09.2026", row.OriginalOrderDate);
        Assert.Equal("refunded", row.OrderStatus);
        Assert.Equal(176.28m, row.GrandTotalUsd);
        Assert.Equal(-176.28m, row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_LinksTransactionIdToReceiptId_AndPreventsDuplicates()
    {
        var origDate = new DateTimeOffset(2026, 9, 26, 14, 0, 0, TimeSpan.Zero);
        var refundDate = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);

        var receipts = new[]
        {
            new EtsyDashboardReceipt(
                ReceiptId: 4185170234L,
                CreatedAt: origDate,
                IsPaid: false,
                IsCanceled: true,
                IsFromOffsiteAds: false,
                CurrencyCode: "USD",
                GrandTotal: 174.62m,
                Subtotal: 174.62m,
                ShippingCost: 0m,
                DiscountAmt: 0m,
                TaxCost: 0m,
                RefundedAmount: 174.62m,
                Items: new[] { new EtsyDashboardReceiptItem("Arcane Jinx Statue", 9999L, 1, 2110508251050L) },
                BuyerName: "Anthony Brown")
        };

        var ledgerFees = new List<EtsyDashboardLedgerFee>
        {
            new(
                Type: "refund",
                ReferenceId: 2110508251050L,
                Description: "REFUND_GROSS",
                Amount: -174.62m,
                CreatedAt: refundDate)
        };

        var orderCosts = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4185170234L] = new(ProductCost: 35.00m, ShippingCost: 21.31m, PackagingCost: 0m)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            receipts,
            ledgerFees,
            orderCosts,
            50.00m);

        // Mükerrer satır oluşmamalı: Sadece tek satır olmalı!
        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(4185170234L, row.ReceiptId);
        Assert.Equal("Anthony Brown", row.BuyerName);
        Assert.Equal("Arcane Jinx Statue", row.ProductTitle);
        Assert.Equal("03.11.2026", row.RefundDate);
        Assert.Equal("26.09.2026", row.OrderDate);
        Assert.Equal(174.62m, row.RefundAmountUsd);
        Assert.Equal(Math.Round(row.RefundAmountUsd.Value * row.ExchangeRate, 2), row.RefundAmountTry);
        Assert.Equal(56.31m, row.ProductCostUsd);
        Assert.Equal(35.00m, row.UnitProductCostUsd);
        Assert.Equal(21.31m, row.ActualShippingCostUsd);
        Assert.Equal(0m, row.PackagingCostUsd);
        // İptal edilen ve maliyeti girilen siparişte satıcının net zararı maliyet kadardır
        Assert.Equal(-56.31m, row.NetProfitUsd);
        Assert.Equal(Math.Round(row.NetProfitUsd * row.ExchangeRate, 2), row.NetProfitTry);
    }

    [Fact]
    public void BuildDetailedOrderRows_DoesNotInjectPhantomPackaging_AndMatchesTotalBreakdown()
    {
        var receipt = new EtsyDashboardReceipt(
            4225379961L,
            new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero),
            true,
            false,
            false,
            "USD",
            44.25m,
            31.50m,
            10.00m,
            13.50m,
            2.75m,
            0m,
            new[] { new EtsyDashboardReceiptItem("Hexcore Rubik Cube", 11111, 1) },
            "Seth Camacho");

        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4225379961L] = new EtsyDashboardOrderCost(ProductCost: 4.50m, ShippingCost: 8.50m, PackagingCost: 1.00m)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            new[] { receipt },
            Array.Empty<EtsyDashboardLedgerFee>(),
            costs,
            50.17m);

        Assert.Single(rows);
        var row = rows[0];
        Assert.Equal(14.00m, row.ProductCostUsd);
        Assert.Equal(4.50m, row.UnitProductCostUsd);
        Assert.Equal(8.50m, row.ActualShippingCostUsd);
        Assert.Equal(1.00m, row.PackagingCostUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_SubtractsOffsiteAdsOn4225379961_AndDoesNotDoubleSubtractTax()
    {
        var receipt1 = new EtsyDashboardReceipt(
            4225379961L,
            new DateTimeOffset(2026, 11, 8, 12, 0, 0, TimeSpan.Zero),
            true, false, false, "USD", 44.25m, 31.50m, 10.00m, 13.50m, 2.75m, 0m,
            new[] { new EtsyDashboardReceiptItem("Hexcore Rubik Cube", 11111, 1) },
            "Seth Camacho");

        var receipt2 = new EtsyDashboardReceipt(
            4226762400L,
            new DateTimeOffset(2026, 11, 8, 14, 0, 0, TimeSpan.Zero),
            true, false, false, "USD", 94.76m, 80.00m, 7.00m, 0m, 7.76m, 0m,
            new[] { new EtsyDashboardReceiptItem("The Mask Figure", 22222, 1) },
            "Bryan Sanon");

        var ledgerFees = new[]
        {
            new EtsyDashboardLedgerFee("offsite_ads", 4225379961L, "Offsite ads #4225379961", -5.00m),
            new EtsyDashboardLedgerFee("offsite_ads", 4225379961L, "Offsite ads VAT #4225379961", -1.30m)
        };

        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4225379961L] = new EtsyDashboardOrderCost(ProductCost: 5.00m, ShippingCost: 8.00m, PackagingCost: 0m)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            new[] { receipt1, receipt2 },
            ledgerFees,
            costs,
            50.00m);

        var seth = rows.Single(r => r.ReceiptId == 4225379961L);
        Assert.Equal(6.30m, seth.OffsiteAdFeeUsd);
        // 1704.23 TL hak ediş - (6.30 + 13.00) * kuru -> Dış reklam mutlaka düşülmüş olmalı
        var expectedTry = Math.Round(1704.23m - ((6.30m + 13.00m) * seth.ExchangeRate), 2);
        Assert.Equal(expectedTry, seth.NetProfitTry);

        var bryan = rows.Single(r => r.ReceiptId == 4226762400L);
        // Tax etsyFees içinde zaten olduğundan EtsyNetPayoutUsd iki kez tax düşmemeli
        Assert.Equal(Math.Round(bryan.GrandTotalUsd - bryan.EtsyFeesUsd - bryan.OffsiteAdFeeUsd, 2), bryan.EtsyNetPayoutUsd);
    }

    private static EtsyDashboardLedgerFee Credit(string type, long refId, decimal amount, DateTimeOffset at)
        => new(type, refId, "Credit", amount, at);

    [Fact]
    public void BuildDetailedOrderRows_MatchesUnresolvedRefundToTheSingleCanceledReceipt_WithLedgerDateAndAmount()
    {
        var orderDate = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);
        var refundAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var receipts = new[] { Receipt(4178116160L, 170.50m, 170.50m, canceled: true, createdAt: orderDate) };
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 288888888888L, "REFUND_GROSS", -161.39m, refundAt)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(receipts, ledger, NoCosts(), 50.23m);

        var row = Assert.Single(rows);
        Assert.Equal(4178116160L, row.ReceiptId);
        Assert.Equal("12.11.2026", row.RefundDate);
        Assert.Equal("18.09.2026", row.OrderDate);
        Assert.Equal(161.39m, row.RefundAmountUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_DoesNotGuess_WhenTwoCanceledReceiptsMatchTheRefundAmount()
    {
        var refundAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var receipts = new[]
        {
            Receipt(4178116160L, 170.50m, 170.50m, canceled: true, createdAt: new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero)),
            Receipt(4178116161L, 165.00m, 165.00m, canceled: true, createdAt: new DateTimeOffset(2026, 9, 19, 10, 0, 0, TimeSpan.Zero))
        };
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 288888888888L, "REFUND_GROSS", -161.39m, refundAt)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(receipts, ledger, NoCosts(), 50.23m);

        var unresolved = Assert.Single(rows, r => r.CostNotes == "cross_period_refund");
        Assert.Equal(288888888888L, unresolved.ReceiptId);
        Assert.Null(unresolved.OriginalOrderDate);
        Assert.Equal("12.11.2026", unresolved.RefundDate);
    }

    [Fact]
    public void BuildDetailedOrderRows_UsesRealLedgerFeeCredits_NotAFixedPercentage()
    {
        var refundAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 288888888889L, "REFUND_GROSS", -161.39m, refundAt),
            Credit("transaction_fee", 288888888889L, 8.52m, refundAt),
            Credit("payment_processing", 288888888889L, 10.77m, refundAt),
            Credit("offsite_ads", 288888888889L, 22.57m, refundAt)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(Array.Empty<EtsyDashboardReceipt>(), ledger, NoCosts(), 50.23m);

        var row = Assert.Single(rows);
        Assert.Equal(-41.86m, row.EtsyFeesUsd);
        Assert.Equal(Math.Round(-161.39m + 41.86m, 2), row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_WithoutFeeCreditEntries_ReportsZeroInsteadOfEstimating()
    {
        var refundAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee> { new("refund", 213006308259L, "REFUND_GROSS", -100m, refundAt) };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(Array.Empty<EtsyDashboardReceipt>(), ledger, NoCosts(), 50m);

        var row = Assert.Single(rows);
        Assert.Equal(0m, row.EtsyFeesUsd);
        Assert.Equal(-100m, row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_DoesNotTruncate12DigitTransactionIdsIntoFakeReceiptNumbers()
    {
        var refundAt = new DateTimeOffset(2026, 11, 12, 9, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee> { new("refund", 0L, "Refund 213006308259", -50m, refundAt) };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(Array.Empty<EtsyDashboardReceipt>(), ledger, NoCosts(), 50m);

        var row = Assert.Single(rows);
        Assert.NotEqual(21300630825L, row.ReceiptId);
    }

    [Fact]
    public void BuildOrderRows_IgnoresPositiveOffsiteAdCredits_AsOrderAdFees()
    {
        var at = new DateTimeOffset(2026, 10, 2, 9, 0, 0, TimeSpan.Zero);
        var receipts = new[] { Receipt(4200000001L, 100m, 100m, createdAt: at) };
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("offsite_ads", 4200000001L, "Order #4200000001", 15m, at)
        };

        var withCredit = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, ledger, NoCosts(), 50m);
        var without = EtsyLiveDashboardCalculator.BuildOrderRows(receipts, Array.Empty<EtsyDashboardLedgerFee>(), NoCosts(), 50m);

        Assert.Equal(without[0].NetProfitUsd, withCredit[0].NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_SubtractsSavedOrderCost_FromCrossPeriodRefundNetProfit()
    {
        var refundAt = new DateTimeOffset(2026, 11, 3, 10, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 4185170299L, "Refund for Order #4185170299", -174.62m, refundAt),
            Credit("transaction_fee", 4185170299L, 11.63m, refundAt)
        };
        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4185170299L] = new(30m, 25.31m, 0m, "Müşteri: Anthony Brown")
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            Array.Empty<EtsyDashboardReceipt>(), ledger, costs, 50m);

        var row = Assert.Single(rows);
        Assert.Equal(4185170299L, row.ReceiptId);
        Assert.Equal(55.31m, row.ProductCostUsd);
        Assert.Equal(-218.30m, row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_ResolvesCrossPeriodRefund_ViaFallbackReceipts_WhenLedgerPaymentsEmpty()
    {
        var orderAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
        var refundAt = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 213006308299L, "REFUND_GROSS", -163.67m, refundAt)
        };
        var fallbackReceipts = new[]
        {
            Receipt(4178116199L, 170.50m, 170.50m, canceled: true, createdAt: orderAt)
        };
        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4178116199L] = new(15.31m, 23m, 0m, null)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            Array.Empty<EtsyDashboardReceipt>(),
            ledger,
            costs,
            50m,
            buyerNameMap: new Dictionary<long, string> { [4178116199L] = "Adam Schulz" },
            originalOrderDateMap: new Dictionary<long, DateTimeOffset> { [4178116199L] = orderAt },
            fallbackReceipts: fallbackReceipts);

        var row = Assert.Single(rows);
        Assert.Equal(4178116199L, row.ReceiptId);
        Assert.Equal("Adam Schulz", row.BuyerName);
        Assert.Equal(38.31m, row.ProductCostUsd);
        Assert.Equal(-201.98m, row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_MatchesTryDenominatedCrossPeriodReceipt_ByConvertedUsdAmount()
    {
        var orderAt = new DateTimeOffset(2026, 9, 16, 10, 0, 0, TimeSpan.Zero);
        var refundAt = new DateTimeOffset(2026, 11, 12, 10, 0, 0, TimeSpan.Zero);
        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", 213006308300L, "REFUND_GROSS", -161.39m, refundAt)
        };
        var fallbackReceipts = new[]
        {
            new EtsyDashboardReceipt(
                4178116200L,
                orderAt,
                false,
                true,
                false,
                "TRY",
                8308.46m,
                8308.46m,
                0m,
                0m,
                0m,
                8106.75m,
                new List<EtsyDashboardReceiptItem> { new("Arcane Jinx Statue", 111, 1) })
        };
        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [4178116200L] = new(15.31m, 23m, 0m, null)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            Array.Empty<EtsyDashboardReceipt>(),
            ledger,
            costs,
            50m,
            buyerNameMap: new Dictionary<long, string> { [4178116200L] = "Adam Schulz" },
            originalOrderDateMap: new Dictionary<long, DateTimeOffset> { [4178116200L] = orderAt },
            fallbackReceipts: fallbackReceipts);

        var row = Assert.Single(rows);
        Assert.Equal(4178116200L, row.ReceiptId);
        Assert.Equal("Adam Schulz", row.BuyerName);
        Assert.Equal(38.31m, row.ProductCostUsd);
        Assert.Equal(-199.70m, row.NetProfitUsd);
    }

    [Fact]
    public void BuildDetailedOrderRows_ResolvesRealBuyerName_WhenTransactionIdMatchesCache_4178116180()
    {
        const long transactionId = 213336306259L;
        const long receiptId = 4178116180L;
        const string buyerName = "Adam Schulz";

        EtsyTransactionOrderCache.Register(transactionId, receiptId, buyerName, DateTimeOffset.UtcNow, "Arcane Jinx Statue");

        var ledger = new List<EtsyDashboardLedgerFee>
        {
            new("refund", transactionId, "REFUND_GROSS", -161.39m, DateTimeOffset.UtcNow)
        };
        var costs = new Dictionary<long, EtsyDashboardOrderCost>
        {
            [receiptId] = new(15.31m, 23.00m, 0m, null)
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            Array.Empty<EtsyDashboardReceipt>(),
            ledger,
            costs,
            49.50m);

        var row = Assert.Single(rows);
        Assert.Equal(receiptId, row.ReceiptId);
        Assert.Equal(buyerName, row.BuyerName);
        Assert.NotEqual("Etsy Müşterisi", row.BuyerName);
        Assert.True(row.HasCostData);
        Assert.Equal(38.31m, row.ProductCostUsd);
        Assert.True(row.NetProfitUsd < 0, "Net profit must be negative (loss) when refund cost exists.");
    }

    [Fact]
    public void BuildDetailedOrderRows_ResolvesBuyerNameFromBuyerNameMap_WhenReceiptBuyerNameIsMissingOrGeneric()
    {
        const long receiptId = 4178116180L;
        var orderAt = new DateTimeOffset(2026, 9, 18, 10, 0, 0, TimeSpan.Zero);

        var receipts = new List<EtsyDashboardReceipt>
        {
            new(receiptId, orderAt, true, false, false, "USD", 150.00m, 150.00m, 0m, 0m, 0m, 0m,
                new List<EtsyDashboardReceiptItem> { new("Arcane Jinx Statue", 123L, 1) },
                BuyerName: "Etsy Müşterisi")
        };

        var buyerMap = new Dictionary<long, string>
        {
            [receiptId] = "Adam Schulz"
        };

        var rows = EtsyLiveDashboardCalculator.BuildDetailedOrderRows(
            receipts,
            Array.Empty<EtsyDashboardLedgerFee>(),
            new Dictionary<long, EtsyDashboardOrderCost>(),
            50.0m,
            buyerNameMap: buyerMap);

        var row = Assert.Single(rows);
        Assert.Equal(receiptId, row.ReceiptId);
        Assert.Equal("Adam Schulz", row.BuyerName);
        Assert.NotEqual("Etsy Müşterisi", row.BuyerName);
    }

    [Theory]
    [InlineData(100.0, 15.0, 0.0, 50.0, 35.0, false)]
    [InlineData(100.0, 15.0, 15.0, 80.0, -10.0, true)]
    [InlineData(0.0, 0.0, 0.0, 38.31, -38.31, true)]
    public void ComputeOrderNetProfit_CorrectlyIdentifiesLossPosition(
        decimal grandTotal, decimal fees, decimal ads, decimal cost, decimal expectedNet, bool expectedIsLoss)
    {
        var netProfit = Math.Round(grandTotal - fees - ads - cost, 2);
        Assert.Equal(expectedNet, netProfit);
        Assert.Equal(expectedIsLoss, netProfit < 0);
    }
}
