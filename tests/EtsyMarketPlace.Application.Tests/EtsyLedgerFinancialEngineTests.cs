namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using Xunit;
using EtsyMarketPlace.Application.EtsyIntegration;

public sealed class EtsyLedgerFinancialEngineTests
{
    private static EtsyLedgerEntryDetail Entry(
        string type,
        decimal amount,
        string description = "",
        DateTimeOffset? createdAt = null,
        decimal netAmount = 0m,
        decimal rate = 48.26m,
        long referenceId = 0)
        => new(
            type,
            type,
            string.Empty,
            description,
            amount,
            netAmount,
            Math.Round(amount * rate, 2),
            "USD",
            createdAt ?? new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero),
            rate,
            referenceId,
            0);

    [Fact]
    public void ClassifyEntry_MatchesDesktopMapEntryOrder()
    {
        Assert.Equal("deposit", EtsyLedgerFinancialEngine.ClassifyEntry("deposit", "", "bank payment"));
        Assert.Equal("offsite_ads", EtsyLedgerFinancialEngine.ClassifyEntry("offsite_ads", "", "offsite ad fee"));
        Assert.Equal("ad_fee", EtsyLedgerFinancialEngine.ClassifyEntry("ad_fee", "", "etsy ads"));
        Assert.Equal("shipping", EtsyLedgerFinancialEngine.ClassifyEntry("shipping", "", "shipping label"));
        Assert.Equal("transaction_fee", EtsyLedgerFinancialEngine.ClassifyEntry("transaction_fee", "", "transaction fee"));
        Assert.Equal("payment_processing", EtsyLedgerFinancialEngine.ClassifyEntry("payment_processing_fee", "", "processing fee"));
        Assert.Equal("etsy_tax_fee", EtsyLedgerFinancialEngine.ClassifyEntry("vat_fee", "", "vat on fees"));
        Assert.Equal("refund", EtsyLedgerFinancialEngine.ClassifyEntry("refund", "", "refund for order"));
        Assert.Equal("sale", EtsyLedgerFinancialEngine.ClassifyEntry("sale", "", "order sale"));
        Assert.Equal("sale", EtsyLedgerFinancialEngine.ClassifyEntry("payment", "", "payment received"));
    }

    [Fact]
    public void ClassifyEntry_DepositRule_ExcludesFeeRecords()
    {
        Assert.Equal("deposit", EtsyLedgerFinancialEngine.ClassifyEntry("transfer", "", "bank transfer"));
        // ucret iceren transferler depozito SAYILMAZ (masaustu kurali)
        Assert.NotEqual("deposit", EtsyLedgerFinancialEngine.ClassifyEntry("transfer_fee", "", "transfer fee"));
    }

    [Fact]
    public void ClassifyEntry_SaleFallback_FromTurkishDescription()
    {
        Assert.Equal("sale", EtsyLedgerFinancialEngine.ClassifyEntry("unknown_type", "", "sipariş #4188719047 ödemesi"));
        Assert.Equal("other", EtsyLedgerFinancialEngine.ClassifyEntry("", "", ""));
    }

    [Fact]
    public void BuildReport_ProducesDesktopParityTotals_ForReferenceDay()
    {
        // 2026-10-01 referans degerleri: brut 35,5742 | komisyon 5,9898 | dis reklam 5,4729 | maliyet 10,42 | net 13,6915
        var day = new DateTimeOffset(2026, 10, 1, 12, 0, 0, TimeSpan.Zero);
        var entries = new List<EtsyLedgerEntryDetail>
        {
            Entry("sale", 35.5742m, "order sale", day),
            Entry("transaction_fee", -2.31m, "transaction fee", day),
            Entry("payment_processing", -2.45m, "processing fee", day),
            Entry("etsy_tax_fee", -0.51m, "vat", day),
            Entry("listing_fee", -0.7198m, "listing", day),
            Entry("offsite_ads", -5.4729m, "offsite", day),
        };
        var costs = new Dictionary<DateTime, decimal> { [day.UtcDateTime.Date] = 10.42m };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, costs);

        Assert.Equal(35.5742m, report.GrossSales);
        Assert.Equal(-5.9898m, report.EtsyFees);
        Assert.Equal(-5.4729m, report.OffsiteAdFees);
        Assert.Equal(10.42m, report.ProductCosts);
        Assert.Equal(24.1115m, report.EtsyNetRevenue);
        Assert.Equal(13.6915m, report.RealNetProfitUsd);
        Assert.Single(report.Daily);
        Assert.Equal(13.6915m, report.Daily[0].RealNetProfitUsd);
        Assert.True(report.Daily[0].GrossSalesTry > 1700m && report.Daily[0].GrossSalesTry < 1720m);
    }

    [Fact]
    public void BuildReport_DailyGrouping_SplitsByUtcDay()
    {
        var d1 = new DateTimeOffset(2026, 9, 30, 22, 4, 0, TimeSpan.Zero);
        var d2 = new DateTimeOffset(2026, 10, 1, 14, 51, 0, TimeSpan.Zero);
        var entries = new List<EtsyLedgerEntryDetail>
        {
            Entry("sale", 145.00m, "sale", d1),
            Entry("sale", 39.90m, "sale", d2),
        };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, new Dictionary<DateTime, decimal>());

        Assert.Equal(2, report.Daily.Count);
        Assert.Equal(new DateTime(2026, 9, 30), report.Daily[0].Date);
        Assert.Equal(145.00m, report.Daily[0].GrossSales);
        Assert.Equal(39.90m, report.Daily[1].GrossSales);
        Assert.Equal(184.90m, report.GrossSales);
    }

    [Fact]
    public void BuildReport_DefaultNegativeUnknownType_CountsAsFee()
    {
        var entries = new List<EtsyLedgerEntryDetail>
        {
            Entry("sale", 100m, "sale"),
            Entry("other_charge", -1.25m, "unknown charge", netAmount: -1.25m),
            Entry("other_credit", 2.00m, "unknown credit", netAmount: 2.00m),
        };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, new Dictionary<DateTime, decimal>());

        Assert.Equal(-1.25m, report.EtsyFees);
        Assert.Equal(98.75m, report.EtsyNetRevenue);
    }

    [Fact]
    public void BuildReport_DepositExcludedFromNet()
    {
        var entries = new List<EtsyLedgerEntryDetail>
        {
            Entry("sale", 100m, "sale"),
            Entry("deposit", -80m, "bank deposit"),
        };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, new Dictionary<DateTime, decimal>());

        Assert.Equal(-80m, report.Deposits);
        Assert.Equal(100m, report.EtsyNetRevenue);
        Assert.Equal(100m, report.RealNetProfitUsd);
    }

    [Fact]
    public void BuildReport_RefundsReduceNetRevenue()
    {
        var entries = new List<EtsyLedgerEntryDetail>
        {
            Entry("sale", 200m, "sale"),
            Entry("refund", -38.59m, "refund", netAmount: -38.59m),
        };

        var report = EtsyLedgerFinancialEngine.BuildReport(entries, new Dictionary<DateTime, decimal>());

        Assert.Equal(-38.59m, report.Refunds);
        Assert.Equal(161.41m, report.EtsyNetRevenue);
    }
}
