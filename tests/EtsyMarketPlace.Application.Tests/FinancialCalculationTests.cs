namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Text.Json;
using Xunit;

public sealed class FinancialCalculationTests
{
    [Fact]
    public void EtsySeptember2026_Scenario_ComputesExactNetProfit()
    {
        // Gerçek Etsy Shop Manager Eylül 2026 Fiş & Defter Verileri
        decimal grossSalesTRY = 1857.58m;
        decimal buyerTaxTRY = -105.30m;
        decimal netSalesTRY = grossSalesTRY + buyerTaxTRY; // 1,752.28 TL

        decimal listingFeesTRY = -38.73m;
        decimal transactionFeesTRY = -116.82m;
        decimal processingFeesTRY = -134.74m;
        decimal regulatoryFeesTRY = -30.01m;
        decimal totalFeesTRY = listingFeesTRY + transactionFeesTRY + processingFeesTRY + regulatoryFeesTRY; // -320.30 TL

        decimal etsyAdsTRY = -692.39m;
        decimal offsiteAdsTRY = -269.58m;
        decimal totalMarketingTRY = etsyAdsTRY + offsiteAdsTRY; // -961.97 TL

        // Etsy Formülü: Net Satış + Komisyonlar (negatif) + Reklamlar (negatif)
        decimal netProfitTRY = netSalesTRY + totalFeesTRY + totalMarketingTRY;

        Assert.Equal(1752.28m, netSalesTRY);
        Assert.Equal(-320.30m, totalFeesTRY);
        Assert.Equal(-961.97m, totalMarketingTRY);
        Assert.Equal(470.01m, netProfitTRY); // Etsy Panelindeki "+470.01 TL" ile KURUŞU KURUŞUNA BİREBİR!
    }

    [Fact]
    public void CurrencyCode_Extraction_CorrectlyReadsTRYFromLedgerJson()
    {
        var rawJson = """
            {
              "entry_id": 998877,
              "ledger_type": "ad_fee",
              "amount": -69239,
              "currency_code": "TRY",
              "description": "Etsy Ads marketing fee"
            }
            """;

        using var doc = JsonDocument.Parse(rawJson);
        var el = doc.RootElement;

        string currency = "USD";
        if ((el.TryGetProperty("currency_code", out var curr) || el.TryGetProperty("currency", out curr)) && curr.ValueKind == JsonValueKind.String)
        {
            currency = curr.GetString() ?? "USD";
        }

        Assert.Equal("TRY", currency);

        decimal rawCents = el.GetProperty("amount").GetDecimal();
        decimal amountTRY = rawCents / 100m;
        Assert.Equal(-692.39m, amountTRY);

        decimal exRate = 48.51m;
        decimal amountUSD = Math.Round(amountTRY / exRate, 2);
        Assert.Equal(-14.27m, amountUSD);
    }

    [Fact]
    public void PaymentReserve_30Percent_SplitsReleasedAndLockedAmountsCorrectly()
    {
        // Liz Porter Order (#4174015409) Net USD = $73.45
        decimal grandTotal = 88.58m;
        decimal etsyFees = 15.13m;
        decimal offsiteAdFee = 0.00m;
        decimal netUsd = grandTotal - etsyFees - offsiteAdFee; // $73.45
        decimal exchangeRate = 48.64m;
        decimal reservePercent = 30m;

        decimal releasedPct = 100m - reservePercent; // 70%
        decimal netTry = Math.Round(netUsd * exchangeRate, 2); // 73.45 * 48.64 = ₺3,572.61
        decimal lockedUsd = Math.Round(netUsd * (reservePercent / 100m), 2); // $22.04
        decimal releasedUsd = netUsd - lockedUsd; // $51.41 (guarantees exact sum)
        decimal lockedTry = Math.Round(lockedUsd * exchangeRate, 2); // 22.04 * 48.64 = ₺1,072.03
        decimal releasedTry = netTry - lockedTry; // ₺2,500.58 (guarantees exact sum)

        Assert.Equal(73.45m, netUsd);
        Assert.Equal(22.04m, lockedUsd);
        Assert.Equal(51.41m, releasedUsd);
        Assert.Equal(netUsd, lockedUsd + releasedUsd);
        Assert.Equal(3572.61m, netTry);
        Assert.Equal(1072.03m, lockedTry);
        Assert.Equal(2500.58m, releasedTry);
        Assert.Equal(netTry, lockedTry + releasedTry);
    }
}
