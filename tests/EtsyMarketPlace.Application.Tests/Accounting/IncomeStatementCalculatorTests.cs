using System;
using System.Linq;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using Xunit;

namespace EtsyMarketPlace.Application.Tests.Accounting;

public sealed class IncomeStatementCalculatorTests
{
    [Fact]
    public void Calculate_AdalarTextbookCase_MatchesOfficialSubtotalsAndBalancedClosingVouchers()
    {
        // Anadolu Üniversitesi Genel Muhasebe (ADALAR Ticaret Ünite 8) sayısal doğrulaması
        var profile = new ShopAccountingProfile
        {
            ShopId = "adalar",
            TaxRegime = TaxRegimeType.CustomRate,
            CustomOrCorporateTaxRatePct = 20m,
            YoungEntrepreneurModeEnabled = false
        };

        var input = new IncomeStatementInput
        {
            ShopId = "adalar",
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 31, 23, 59, 59, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            DomesticSales600 = new(100_000m, 2_000m),
            SalesReturns610 = new(2_000m, 40m),
            SalesDiscounts611 = new(1_000m, 20m),
            CostOfMerchandise621 = new(60_000m, 1_200m),
            MarketingSelling631 = new(10_000m, 200m),
            GeneralManagement632 = new(5_000m, 100m),
            InterestIncome642 = new(1_000m, 20m),
            FxLosses656 = new(500m, 10m),
            ShortTermFinancing660 = new(2_500m, 50m),
            ExtraordinaryIncome679 = new(3_000m, 60m),
            ExtraordinaryExpenses689 = new(1_000m, 20m)
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        Assert.Equal(100_000m, doc.GrossSalesTry);
        Assert.Equal(3_000m, doc.SalesDeductionsTry);
        Assert.Equal(97_000m, doc.NetSalesTry);
        Assert.Equal(60_000m, doc.CostOfSalesTry);
        Assert.Equal(37_000m, doc.GrossProfitTry);
        Assert.Equal(15_000m, doc.OperatingExpensesTry);
        Assert.Equal(22_000m, doc.OperatingProfitTry);
        Assert.Equal(20_000m, doc.OrdinaryProfitTry);
        Assert.Equal(22_000m, doc.CommercialProfit690Try);
        Assert.Equal(4_400m, doc.TaxProvision691Try);
        Assert.Equal(17_600m, doc.PeriodNetProfit692Try);

        Assert.Equal(3, doc.ClosingVouchers.Count);
        Assert.All(doc.ClosingVouchers, v =>
        {
            Assert.True(v.IsBalanced);
            Assert.Equal(v.TotalDebitTry, v.TotalCreditTry);
        });

        var voucher2 = doc.ClosingVouchers[1];
        Assert.Equal(104_000m, voucher2.TotalDebitTry);
        Assert.Equal(104_000m, voucher2.TotalCreditTry);

        var voucher3 = doc.ClosingVouchers[2];
        Assert.Contains(voucher3.Lines, l => l.AccountCode == "370" && l.CreditTry == 4_400m);
        Assert.Contains(voucher3.Lines, l => l.AccountCode == "590" && l.CreditTry == 17_600m);
    }

    [Fact]
    public void Calculate_YoungEntrepreneur2026Mode_Applies400kLimitAndReportsTaxSavings()
    {
        var profile = new ShopAccountingProfile
        {
            ShopId = "53236321",
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = true,
            YoungEntrepreneurAnnualLimitTry = 400_000m,
            YoungEntrepreneurPriorUsedTry = 0m
        };

        var input = new IncomeStatementInput
        {
            ShopId = "53236321",
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            ExportSales601 = new(600_000m, 12_000m),
            CostOfManufacturedGoods620 = new(100_000m, 2_000m)
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        // 500.000 TL ticari kâr -> 400.000 TL istisna sonrası matrah 100.000 TL -> %15 = 15.000 TL
        // Genç Girişimci olmadan: 190k*0.15 (28.500) + 210k*0.20 (42.000) + 100k*0.27 (27.000) = 97.500 TL
        Assert.Equal(500_000m, doc.CommercialProfit690Try);
        Assert.Equal(400_000m, doc.TaxBridge.YoungEntrepreneurAppliedInPeriodTry);
        Assert.Equal(0m, doc.TaxBridge.YoungEntrepreneurRemainingTry);
        Assert.Equal(100_000m, doc.TaxBridge.TaxableBaseTry);
        Assert.Equal(15_000m, doc.TaxProvision691Try);
        Assert.Equal(97_500m, doc.TaxBridge.TaxWithoutYoungEntrepreneurTry);
        Assert.Equal(82_500m, doc.TaxBridge.YoungEntrepreneurTaxSavingsTry);
        Assert.Equal(485_000m, doc.PeriodNetProfit692Try);
    }

    [Fact]
    public void Calculate_KkegExceedingCommercialProfit_ProducesNetLossIn591AndBalancedVoucher3()
    {
        var profile = new ShopAccountingProfile
        {
            ShopId = "53236321",
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = false
        };

        // Ticari kâr +10.000 TL, ancak 100.000 TL KKEG var -> Mali kâr 110.000 -> Vergi %15 = 16.500 TL
        // Net dönem sonucu = 10.000 - 16.500 = -6.500 TL (591 Dönem Net Zararı)
        var input = new IncomeStatementInput
        {
            ShopId = "53236321",
            StartDate = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            ExportSales601 = new(110_000m, 2_200m),
            GeneralManagement632 = new(100_000m, 2_000m),
            KkegAdditions950Try = 100_000m
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        Assert.Equal(10_000m, doc.CommercialProfit690Try);
        Assert.Equal(16_500m, doc.TaxProvision691Try);
        Assert.Equal(-6_500m, doc.PeriodNetProfit692Try);

        var voucher3 = doc.ClosingVouchers.Last();
        Assert.True(voucher3.IsBalanced);
        Assert.Equal(voucher3.TotalDebitTry, voucher3.TotalCreditTry);
        Assert.Contains(voucher3.Lines, l => l.AccountCode == "591" && l.DebitTry == 6_500m);
        Assert.Contains(voucher3.Lines, l => l.AccountCode == "370" && l.CreditTry == 16_500m);
    }

    [Fact]
    public void Calculate_CommercialLossWithHeavyKkeg_StillTaxesFiscalProfitAndBalances()
    {
        var profile = new ShopAccountingProfile
        {
            ShopId = "53236321",
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = false
        };

        // Ticari zarar -10.000 TL + 110.000 TL KKEG = 100.000 TL Mali Kâr -> Vergi 15.000 TL -> Net zarar -25.000 TL
        var input = new IncomeStatementInput
        {
            ShopId = "53236321",
            StartDate = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            ExportSales601 = new(100_000m, 2_000m),
            GeneralManagement632 = new(110_000m, 2_200m),
            KkegAdditions950Try = 110_000m
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        Assert.Equal(-10_000m, doc.CommercialProfit690Try);
        Assert.Equal(15_000m, doc.TaxProvision691Try);
        Assert.Equal(-25_000m, doc.PeriodNetProfit692Try);

        Assert.All(doc.ClosingVouchers, v =>
        {
            Assert.True(v.IsBalanced);
            Assert.Equal(v.TotalDebitTry, v.TotalCreditTry);
        });
        var voucher3 = doc.ClosingVouchers.Last();
        Assert.Contains(voucher3.Lines, l => l.AccountCode == "591" && l.DebitTry == 25_000m);
    }

    [Fact]
    public void Calculate_NegativeCommissionRefundExcess_ShiftsInto671PriorPeriodIncomeAndBalances()
    {
        var profile = new ShopAccountingProfile
        {
            ShopId = "53236321",
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = true
        };

        var input = new IncomeStatementInput
        {
            ShopId = "53236321",
            StartDate = new DateTimeOffset(2026, 11, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 11, 30, 0, 0, 0, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            ExportSales601 = new(1_000m, 20m),
            CommissionExpenses653 = new(-400m, -8m)
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        var line653 = doc.AllLines.Single(l => l.AccountCode == "653");
        var line671 = doc.AllLines.Single(l => l.AccountCode == "671");
        Assert.Equal(0m, line653.AmountTry);
        Assert.Equal(400m, line671.AmountTry);
        Assert.Equal(1_400m, doc.CommercialProfit690Try);
        Assert.All(doc.ClosingVouchers, v => Assert.True(v.IsBalanced));
    }

    [Fact]
    public void Calculate_EExportExemption50Pct_ReducesTaxableBaseByExportShare()
    {
        var profile = new ShopAccountingProfile
        {
            ShopId = "53236321",
            TaxRegime = TaxRegimeType.SoleProprietorshipGvk,
            YoungEntrepreneurModeEnabled = false,
            EExportExemptionRatePct = 50m
        };

        // %100 ihracat, 200.000 TL kâr -> %50 E-ihracat indirimi (100.000 TL) -> 100.000 TL matrah -> %15 = 15.000 TL
        var input = new IncomeStatementInput
        {
            ShopId = "53236321",
            StartDate = new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
            EndDate = new DateTimeOffset(2026, 12, 31, 0, 0, 0, TimeSpan.Zero),
            ExchangeRate = 50m,
            Profile = profile,
            ExportSales601 = new(300_000m, 6_000m),
            CostOfManufacturedGoods620 = new(100_000m, 2_000m)
        };

        var doc = IncomeStatementCalculator.Calculate(input);

        Assert.Equal(100m, doc.TaxBridge.ExportSalesSharePct);
        Assert.Equal(100_000m, doc.TaxBridge.EExportExemptionDeductionTry);
        Assert.Equal(100_000m, doc.TaxBridge.TaxableBaseTry);
        Assert.Equal(15_000m, doc.TaxProvision691Try);
    }
}

