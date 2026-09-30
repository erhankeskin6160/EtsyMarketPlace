using System;
using System.Collections.Generic;
using EtsyMarketPlace.Application.Banking;
using EtsyMarketPlace.Domain.Banking;
using Xunit;

namespace EtsyMarketPlace.Application.Tests;

public class BankDepositServiceTests
{
    private readonly BankDepositService _service = new();

    [Fact]
    public void CalculateMonthlyDeposits_FiltersOnlyDepositsAndSortsDescending()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);

        var entries = new List<RawDepositEntryInput>
        {
            new(1, 100, "sale", 50m, 45m, "USD", "Sipariş satışı", new DateTimeOffset(2026, 9, 10, 10, 0, 0, TimeSpan.Zero)),
            new(2, 101, "deposit", 200m, -200m, "USD", "Etsy bank payout", new DateTimeOffset(2026, 9, 12, 14, 0, 0, TimeSpan.Zero)),
            new(3, 102, "listing_fee", 0.20m, -0.20m, "USD", "İlan ücreti", new DateTimeOffset(2026, 9, 15, 12, 0, 0, TimeSpan.Zero)),
            new(4, 103, "disbursement", 350m, -350m, "USD", "Haftalık banka aktarımı", new DateTimeOffset(2026, 9, 20, 16, 0, 0, TimeSpan.Zero)),
            new(5, 104, "deposit", 150m, -150m, "USD", "Eski transfer", new DateTimeOffset(2026, 8, 25, 11, 0, 0, TimeSpan.Zero)) // Dönem dışı
        };

        var summary = _service.CalculateMonthlyDeposits(entries, from, to, _ => 48.0m, 48.0m);

        Assert.Equal(2, summary.DepositCount);
        Assert.Equal(550m, summary.TotalAmount);
        Assert.Equal(550m * 48.0m, summary.TotalAmountTRY);
        Assert.Equal(275m, summary.AverageAmount);

        // Sıralama kontrolü (En yeni transfer en başta)
        Assert.Equal(4, summary.Deposits[0].EntryId);
        Assert.Equal(2, summary.Deposits[1].EntryId);
        Assert.Equal(summary.Deposits[0], summary.LastDeposit);
    }

    [Fact]
    public void CalculateMonthlyDeposits_CalculatesTRYAmountsCorrectly()
    {
        var from = new DateTimeOffset(2026, 9, 1, 0, 0, 0, TimeSpan.Zero);
        var to = new DateTimeOffset(2026, 9, 30, 23, 59, 59, TimeSpan.Zero);

        var entries = new List<RawDepositEntryInput>
        {
            new(10, 500, "deposit", 100m, -100m, "USD", "Banka Ödemesi", new DateTimeOffset(2026, 9, 5, 12, 0, 0, TimeSpan.Zero), PreResolvedRate: 49.00m)
        };

        var summary = _service.CalculateMonthlyDeposits(entries, from, to, _ => 40.0m, 40.0m);

        var deposit = Assert.Single(summary.Deposits);
        Assert.Equal(100m, deposit.Amount);
        Assert.Equal(4900.00m, deposit.AmountTRY);
        Assert.Equal(49.00m, deposit.ExchangeRate);
        Assert.Equal("✅ Yatırıldı", $"✅ {deposit.Status}");
    }
}
