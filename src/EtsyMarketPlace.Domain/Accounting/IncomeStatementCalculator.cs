using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using EtsyMarketPlace.Domain.Accounting.Entities;

namespace EtsyMarketPlace.Domain.Accounting;

/// <summary>
/// Anadolu Üniversitesi Genel Muhasebe (MSUGT 1 No.lu Tebliğ / Tekdüzen Hesap Planı)
/// resmi dikey Gelir Tablosu, 2026 GVK / Genç Girişimci vergi karşılığı ve 690-691-692 kapanış motoru.
/// </summary>
public static class IncomeStatementCalculator
{
    public static IReadOnlyList<GvkTaxBracket> Default2026GvkBrackets { get; } = new GvkTaxBracket[]
    {
        new(190_000m, 15m),
        new(400_000m, 20m),
        new(1_000_000m, 27m),
        new(5_300_000m, 35m),
        new(null, 40m)
    };

    private static readonly JsonSerializerOptions BracketJsonOptions = new() { PropertyNameCaseInsensitive = true };

    public static IncomeStatementDocument Calculate(IncomeStatementInput input)
    {
        ArgumentNullException.ThrowIfNull(input);
        var fx = input.ExchangeRate > 0m ? input.ExchangeRate : 49.0m;

        // Yaprak hesapları 2 ondalığa sabitleyerek bölüm toplamları ve yevmiye kapanışlarıyla tam kuruş mutabakatı sağla.
        AccountBucketInput RB(AccountBucketInput b) => b with { AmountTry = R(b.AmountTry), AmountUsd = R(b.AmountUsd) };

        var domSales600 = RB(input.DomesticSales600);
        var expSales601 = RB(input.ExportSales601);
        var othSales602 = RB(input.OtherSalesIncome602);
        var ret610 = RB(input.SalesReturns610);
        var disc611 = RB(input.SalesDiscounts611);
        var othDed612 = RB(input.OtherSalesDeductions612);
        var cogs620 = RB(input.CostOfManufacturedGoods620);
        var cogs621 = RB(input.CostOfMerchandise621);
        var cogs622 = RB(input.CostOfServices622);
        var rnd630 = RB(input.ResearchAndDev630);
        var (mktExp631, shiftedMktRefunds) = NormalizeExpenseOrPriorRefund(RB(input.MarketingSelling631));
        var mgmt632 = RB(input.GeneralManagement632);
        var int642 = RB(input.InterestIncome642);
        var fxGain646 = RB(input.FxGains646);
        var ordInc649 = RB(input.OtherOrdinaryIncome649);
        var (commExp653, shiftedCommissionRefunds) = NormalizeExpenseOrPriorRefund(RB(input.CommissionExpenses653));
        var prov654 = RB(input.ProvisionExpenses654);
        var fxLoss656 = RB(input.FxLosses656);
        var ordExp659 = RB(input.OtherOrdinaryExpenses659);
        var fin660 = RB(input.ShortTermFinancing660);
        var fin661 = RB(input.LongTermFinancing661);
        var priorIncome671 = RB(MergeBuckets(RB(input.PriorPeriodIncome671), shiftedCommissionRefunds, shiftedMktRefunds));
        var extInc679 = RB(input.ExtraordinaryIncome679);
        var idle680 = RB(input.IdleCapacityLoss680);
        var priorExp681 = RB(input.PriorPeriodExpense681);
        var extExp689 = RB(input.ExtraordinaryExpenses689);

        // Hesap tutarlarını ham toplamlara indirge
        decimal grossSalesTry = R(domSales600.AmountTry + expSales601.AmountTry + othSales602.AmountTry);
        decimal grossSalesUsd = R(domSales600.AmountUsd + expSales601.AmountUsd + othSales602.AmountUsd);

        decimal salesDedTry = R(ret610.AmountTry + disc611.AmountTry + othDed612.AmountTry);
        decimal salesDedUsd = R(ret610.AmountUsd + disc611.AmountUsd + othDed612.AmountUsd);

        decimal netSalesTry = R(grossSalesTry - salesDedTry);
        decimal netSalesUsd = R(grossSalesUsd - salesDedUsd);

        decimal cogsTry = R(cogs620.AmountTry + cogs621.AmountTry + cogs622.AmountTry);
        decimal cogsUsd = R(cogs620.AmountUsd + cogs621.AmountUsd + cogs622.AmountUsd);

        decimal grossProfitTry = R(netSalesTry - cogsTry);
        decimal grossProfitUsd = R(netSalesUsd - cogsUsd);

        decimal opExpTry = R(rnd630.AmountTry + mktExp631.AmountTry + mgmt632.AmountTry);
        decimal opExpUsd = R(rnd630.AmountUsd + mktExp631.AmountUsd + mgmt632.AmountUsd);

        decimal opProfitTry = R(grossProfitTry - opExpTry);
        decimal opProfitUsd = R(grossProfitUsd - opExpUsd);

        decimal otherOrdIncTry = R(int642.AmountTry + fxGain646.AmountTry + ordInc649.AmountTry);
        decimal otherOrdIncUsd = R(int642.AmountUsd + fxGain646.AmountUsd + ordInc649.AmountUsd);

        decimal otherOrdExpTry = R(commExp653.AmountTry + prov654.AmountTry + fxLoss656.AmountTry + ordExp659.AmountTry);
        decimal otherOrdExpUsd = R(commExp653.AmountUsd + prov654.AmountUsd + fxLoss656.AmountUsd + ordExp659.AmountUsd);

        decimal finExpTry = R(fin660.AmountTry + fin661.AmountTry);
        decimal finExpUsd = R(fin660.AmountUsd + fin661.AmountUsd);

        decimal ordinaryProfitTry = R(opProfitTry + otherOrdIncTry - otherOrdExpTry - finExpTry);
        decimal ordinaryProfitUsd = R(opProfitUsd + otherOrdIncUsd - otherOrdExpUsd - finExpUsd);

        decimal extraIncTry = R(priorIncome671.AmountTry + extInc679.AmountTry);
        decimal extraIncUsd = R(priorIncome671.AmountUsd + extInc679.AmountUsd);

        decimal extraExpTry = R(idle680.AmountTry + priorExp681.AmountTry + extExp689.AmountTry);
        decimal extraExpUsd = R(idle680.AmountUsd + priorExp681.AmountUsd + extExp689.AmountUsd);

        decimal commercialProfit690Try = R(ordinaryProfitTry + extraIncTry - extraExpTry);
        decimal commercialProfit690Usd = R(ordinaryProfitUsd + extraIncUsd - extraExpUsd);

        // Vergi Köprüsü (Ticari -> Mali Kâr -> 691 -> 692)
        var taxBridge = ComputeTaxBridge(commercialProfit690Try, grossSalesTry, expSales601.AmountTry, input);
        decimal tax691Try = taxBridge.EstimatedTax691Try;
        decimal tax691Usd = fx > 0m ? R(tax691Try / fx) : 0m;
        decimal netProfit692Try = taxBridge.PeriodNetProfit692Try;
        decimal netProfit692Usd = R(commercialProfit690Usd - tax691Usd);

        // Bölümler & Satırlar
        decimal ratioBase = netSalesTry != 0m ? Math.Abs(netSalesTry) : (grossSalesTry != 0m ? Math.Abs(grossSalesTry) : 1m);
        bool hasRatioBase = netSalesTry != 0m || grossSalesTry != 0m;

        TdhpAccountLine Line(string code, string name, string sec, bool isDed, AccountBucketInput b) =>
            new(
                code, name, sec, isDed, false, false,
                R(b.AmountTry), R(b.AmountUsd),
                isDed ? -R(b.AmountTry) : R(b.AmountTry),
                isDed ? -R(b.AmountUsd) : R(b.AmountUsd),
                hasRatioBase ? R((b.AmountTry / ratioBase) * 100m) : 0m,
                b.Note,
                b.Sources ?? Array.Empty<IncomeStatementSourceItem>());

        TdhpAccountLine Subtotal(string code, string name, string sec, decimal tryVal, decimal usdVal, bool isFinal = false, string? note = null) =>
            new(
                code, name, sec, false, true, isFinal,
                R(tryVal), R(usdVal),
                R(tryVal), R(usdVal),
                hasRatioBase ? R((tryVal / ratioBase) * 100m) : 0m,
                note,
                Array.Empty<IncomeStatementSourceItem>());

        var sections = new List<IncomeStatementSection>
        {
            new("A", "A - BRÜT SATIŞLAR", false, grossSalesTry, grossSalesUsd, new[]
            {
                Line("600", "Yurt İçi Satışlar", "A", false, domSales600),
                Line("601", "Yurt Dışı Satışlar (İhracat)", "A", false, expSales601),
                Line("602", "Diğer Gelirler", "A", false, othSales602)
            }),
            new("B", "B - SATIŞ İNDİRİMLERİ (-)", true, salesDedTry, salesDedUsd, new[]
            {
                Line("610", "Satıştan İadeler (-)", "B", true, ret610),
                Line("611", "Satış İskontoları (-)", "B", true, disc611),
                Line("612", "Diğer İndirimler (-)", "B", true, othDed612)
            }),
            new("C", "C - NET SATIŞLAR", false, netSalesTry, netSalesUsd, new[]
            {
                Subtotal("C", "NET SATIŞLAR (A - B)", "C", netSalesTry, netSalesUsd)
            }),
            new("D", "D - SATIŞLARIN MALİYETİ (-)", true, cogsTry, cogsUsd, new[]
            {
                Line("620", "Satılan Mamuller Maliyeti (-)", "D", true, cogs620),
                Line("621", "Satılan Ticari Mallar Maliyeti (-)", "D", true, cogs621),
                Line("622", "Satılan Hizmet Maliyeti (-)", "D", true, cogs622)
            }),
            new("S1", "BRÜT SATIŞ KÂRI VEYA ZARARI", false, grossProfitTry, grossProfitUsd, new[]
            {
                Subtotal("BRUT_KAR", grossProfitTry >= 0 ? "BRÜT SATIŞ KÂRI" : "BRÜT SATIŞ ZARARI (-)", "S1", grossProfitTry, grossProfitUsd)
            }),
            new("E", "E - FAALİYET GİDERLERİ (-)", true, opExpTry, opExpUsd, new[]
            {
                Line("630", "Araştırma ve Geliştirme Giderleri (-)", "E", true, rnd630),
                Line("631", "Pazarlama, Satış ve Dağıtım Giderleri (-)", "E", true, mktExp631),
                Line("632", "Genel Yönetim Giderleri (-)", "E", true, mgmt632)
            }),
            new("S2", "FAALİYET KÂRI VEYA ZARARI", false, opProfitTry, opProfitUsd, new[]
            {
                Subtotal("FAALIYET_KAR", opProfitTry >= 0 ? "FAALİYET KÂRI" : "FAALİYET ZARARI (-)", "S2", opProfitTry, opProfitUsd)
            }),
            new("F", "F - DİĞER FAALİYETLERDEN OLAĞAN GELİR VE KÂRLAR", false, otherOrdIncTry, otherOrdIncUsd, new[]
            {
                Line("642", "Faiz Gelirleri", "F", false, int642),
                Line("646", "Kambiyo Kârları", "F", false, fxGain646),
                Line("649", "Diğer Olağan Gelir ve Kârlar", "F", false, ordInc649)
            }),
            new("G", "G - DİĞER FAALİYETLERDEN OLAĞAN GİDER VE ZARARLAR (-)", true, otherOrdExpTry, otherOrdExpUsd, new[]
            {
                Line("653", "Komisyon Giderleri (-)", "G", true, commExp653),
                Line("654", "Karşılık Giderleri (-)", "G", true, prov654),
                Line("656", "Kambiyo Zararları (-)", "G", true, fxLoss656),
                Line("659", "Diğer Olağan Gider ve Zararlar (-)", "G", true, ordExp659)
            }),
            new("H", "H - FİNANSMAN GİDERLERİ (-)", true, finExpTry, finExpUsd, new[]
            {
                Line("660", "Kısa Vadeli Borçlanma Giderleri (-)", "H", true, fin660),
                Line("661", "Uzun Vadeli Borçlanma Giderleri (-)", "H", true, fin661)
            }),
            new("S3", "OLAĞAN KÂR VEYA ZARAR", false, ordinaryProfitTry, ordinaryProfitUsd, new[]
            {
                Subtotal("OLAGAN_KAR", ordinaryProfitTry >= 0 ? "OLAĞAN KÂR" : "OLAĞAN ZARAR (-)", "S3", ordinaryProfitTry, ordinaryProfitUsd)
            }),
            new("I", "I - OLAĞANDIŞI GELİR VE KÂRLAR", false, extraIncTry, extraIncUsd, new[]
            {
                Line("671", "Önceki Dönem Gelir ve Kârları", "I", false, priorIncome671),
                Line("679", "Diğer Olağandışı Gelir ve Kârlar", "I", false, extInc679)
            }),
            new("J", "J - OLAĞANDIŞI GİDER VE ZARARLAR (-)", true, extraExpTry, extraExpUsd, new[]
            {
                Line("680", "Çalışmayan Kısım Gider ve Zararları (-)", "J", true, idle680),
                Line("681", "Önceki Dönem Gider ve Zararları (-)", "J", true, priorExp681),
                Line("689", "Diğer Olağandışı Gider ve Zararlar (-)", "J", true, extExp689)
            }),
            new("S4", "DÖNEM KÂRI VEYA ZARARI (TİCARİ KÂR)", false, commercialProfit690Try, commercialProfit690Usd, new[]
            {
                Subtotal("690", commercialProfit690Try >= 0 ? "DÖNEM KÂRI (Ticari Kâr)" : "DÖNEM ZARARI (Ticari Zarar) (-)", "S4", commercialProfit690Try, commercialProfit690Usd)
            }),
            new("K", "K - DÖNEM KÂRI VERGİ VE DİĞER YASAL YÜKÜMLÜLÜK KARŞILIKLARI (-)", true, tax691Try, tax691Usd, new[]
            {
                Line("691", "Dönem Kârı Vergi ve Yasal Yük. Karşılıkları (-)", "K", true,
                    new AccountBucketInput(tax691Try, tax691Usd, taxBridge.TaxRegimeLabel, Array.Empty<IncomeStatementSourceItem>()))
            }),
            new("S5", "DÖNEM NET KÂRI VEYA ZARARI", false, netProfit692Try, netProfit692Usd, new[]
            {
                Subtotal("692", netProfit692Try >= 0 ? "DÖNEM NET KÂRI" : "DÖNEM NET ZARARI (-)", "S5", netProfit692Try, netProfit692Usd, isFinal: true)
            })
        };

        var allLines = sections.SelectMany(s => s.Lines).ToList();

        var fxNetTry = R(fxGain646.AmountTry - fxLoss656.AmountTry);
        var extraNetTry = R(extraIncTry - extraExpTry);
        var reconciliation = new ReconciliationBridge(
            R(input.EtsyContributionProfitTry),
            grossSalesTry,
            input.EtsyDirectFeesAndAdsTry,
            input.EtsyDirectOrderCostsTry,
            input.SeparatedCanceledCostLossTry,
            input.NonEtsyOperatingExpensesTry,
            input.DepreciationExpenseTry,
            fxNetTry,
            extraNetTry,
            commercialProfit690Try,
            tax691Try,
            netProfit692Try);

        var closingVouchers = BuildClosingVouchers(allLines, rnd630.AmountTry, mktExp631.AmountTry, mgmt632.AmountTry, finExpTry, commercialProfit690Try, tax691Try, netProfit692Try, taxBridge);

        return new IncomeStatementDocument(
            input.ShopId,
            input.StartDate,
            input.EndDate,
            fx,
            input.LedgerOk,
            input.Warning,
            input.Profile,
            grossSalesTry,
            salesDedTry,
            netSalesTry,
            cogsTry,
            grossProfitTry,
            opExpTry,
            opProfitTry,
            otherOrdIncTry,
            otherOrdExpTry,
            finExpTry,
            ordinaryProfitTry,
            extraIncTry,
            extraExpTry,
            commercialProfit690Try,
            tax691Try,
            netProfit692Try,
            grossSalesUsd,
            netSalesUsd,
            grossProfitUsd,
            opProfitUsd,
            ordinaryProfitUsd,
            commercialProfit690Usd,
            tax691Usd,
            netProfit692Usd,
            sections,
            allLines,
            taxBridge,
            reconciliation,
            closingVouchers,
            input.AssetDepreciations);
    }

    public static IReadOnlyList<GvkTaxBracket> ResolveGvkBrackets(string? customJson)
    {
        if (string.IsNullOrWhiteSpace(customJson))
            return Default2026GvkBrackets;

        try
        {
            var parsed = JsonSerializer.Deserialize<List<GvkTaxBracket>>(customJson, BracketJsonOptions);
            if (parsed is null || parsed.Count == 0)
                return Default2026GvkBrackets;

            return parsed
                .OrderBy(b => b.UpperLimitTry ?? decimal.MaxValue)
                .ToList();
        }
        catch
        {
            return Default2026GvkBrackets;
        }
    }

    public static (decimal TotalTaxTry, IReadOnlyList<GvkBracketSlice> Breakdown) ComputeProgressiveGvkTax(
        decimal taxableAmountTry,
        IReadOnlyList<GvkTaxBracket> brackets)
    {
        if (taxableAmountTry <= 0m || brackets.Count == 0)
            return (0m, Array.Empty<GvkBracketSlice>());

        var slices = new List<GvkBracketSlice>();
        decimal prevLimit = 0m;
        decimal remaining = taxableAmountTry;
        decimal totalTax = 0m;

        foreach (var b in brackets)
        {
            if (remaining <= 0m) break;

            var upper = b.UpperLimitTry ?? decimal.MaxValue;
            var width = upper == decimal.MaxValue ? remaining : Math.Max(0m, upper - prevLimit);
            var slice = Math.Min(remaining, width);
            if (slice <= 0m)
            {
                prevLimit = upper;
                continue;
            }

            var sliceTax = R(slice * (b.RatePct / 100m));
            var label = upper == decimal.MaxValue
                ? $"₺{prevLimit:N0} üzeri (%{b.RatePct:0.#})"
                : $"₺{prevLimit:N0} – ₺{upper:N0} (%{b.RatePct:0.#})";

            slices.Add(new GvkBracketSlice(label, R(slice), b.RatePct, sliceTax));
            totalTax = R(totalTax + sliceTax);
            remaining -= slice;
            prevLimit = upper;
        }

        return (totalTax, slices);
    }

    private static TaxBridgeSummary ComputeTaxBridge(decimal commercialProfit690, decimal grossSalesTry, decimal exportSales601Try, IncomeStatementInput input)
    {
        var p = input.Profile;
        var exportSalesSharePct = grossSalesTry > 0m
            ? Math.Clamp(R((exportSales601Try / grossSalesTry) * 100m), 0m, 100m)
            : 0m;

        var profitAfterKkeg = R(commercialProfit690 + input.KkegAdditions950Try);
        decimal exportExemption = 0m;
        decimal bagkurDeduction = 0m;
        decimal baseBeforeYe = Math.Max(0m, profitAfterKkeg);

        if (baseBeforeYe > 0m)
        {
            if (p.EExportExemptionRatePct > 0m && exportSalesSharePct > 0m)
            {
                exportExemption = R(baseBeforeYe * (exportSalesSharePct / 100m) * (Math.Clamp(p.EExportExemptionRatePct, 0m, 100m) / 100m));
                baseBeforeYe = Math.Max(0m, R(baseBeforeYe - exportExemption));
            }

            if (p.TaxRegime == TaxRegimeType.SoleProprietorshipGvk && input.BagkurDeductionTry > 0m)
            {
                bagkurDeduction = Math.Min(baseBeforeYe, R(input.BagkurDeductionTry));
                baseBeforeYe = Math.Max(0m, R(baseBeforeYe - bagkurDeduction));
            }
        }

        var brackets = ResolveGvkBrackets(p.CustomGvkBracketsJson);
        decimal appliedYe = 0m;
        decimal remainingYe = Math.Max(0m, R(p.YoungEntrepreneurAnnualLimitTry - p.YoungEntrepreneurPriorUsedTry));

        var taxableBase = baseBeforeYe;
        if (p.TaxRegime == TaxRegimeType.SoleProprietorshipGvk && p.YoungEntrepreneurModeEnabled && baseBeforeYe > 0m)
        {
            appliedYe = Math.Min(baseBeforeYe, remainingYe);
            taxableBase = Math.Max(0m, R(baseBeforeYe - appliedYe));
            remainingYe = Math.Max(0m, R(remainingYe - appliedYe));
        }

        var (taxWithoutYe, _) = ComputeRegimeTax(baseBeforeYe, p, brackets);
        var (estimatedTax, sliceBreakdown) = ComputeRegimeTax(taxableBase, p, brackets);

        var savings = p.TaxRegime == TaxRegimeType.SoleProprietorshipGvk && p.YoungEntrepreneurModeEnabled
            ? Math.Max(0m, R(taxWithoutYe - estimatedTax))
            : 0m;

        var effectiveRate = profitAfterKkeg > 0m ? R((estimatedTax / profitAfterKkeg) * 100m) : 0m;
        var netProfit692 = R(commercialProfit690 - estimatedTax);

        var regimeLabel = p.TaxRegime switch
        {
            TaxRegimeType.SoleProprietorshipGvk when p.YoungEntrepreneurModeEnabled =>
                $"Şahıs İşletmesi (2026 GVK) + Genç Girişimci İstisnası (₺{p.YoungEntrepreneurAnnualLimitTry:N0})",
            TaxRegimeType.SoleProprietorshipGvk => "Şahıs İşletmesi (2026 GVK Md. 103 Tarifesi)",
            TaxRegimeType.CorporateKvk25 => "Sermaye Şirketi (Kurumlar Vergisi %25)",
            _ => $"Özel Vergi Oranı (%{p.CustomOrCorporateTaxRatePct:0.##})"
        };

        return new TaxBridgeSummary(
            commercialProfit690,
            R(input.KkegAdditions950Try),
            exportSalesSharePct,
            p.EExportExemptionRatePct,
            exportExemption,
            bagkurDeduction,
            baseBeforeYe,
            p.YoungEntrepreneurModeEnabled,
            R(p.YoungEntrepreneurAnnualLimitTry),
            R(p.YoungEntrepreneurPriorUsedTry),
            R(appliedYe),
            R(remainingYe),
            R(taxableBase),
            R(estimatedTax),
            effectiveRate,
            R(taxWithoutYe),
            R(savings),
            netProfit692,
            regimeLabel,
            sliceBreakdown);
    }

    private static (decimal Tax, IReadOnlyList<GvkBracketSlice> Slices) ComputeRegimeTax(
        decimal taxableBase,
        ShopAccountingProfile profile,
        IReadOnlyList<GvkTaxBracket> brackets)
    {
        if (taxableBase <= 0m)
            return (0m, Array.Empty<GvkBracketSlice>());

        if (profile.TaxRegime == TaxRegimeType.SoleProprietorshipGvk)
            return ComputeProgressiveGvkTax(taxableBase, brackets);

        var rate = profile.TaxRegime == TaxRegimeType.CorporateKvk25
            ? 25m
            : Math.Clamp(profile.CustomOrCorporateTaxRatePct, 0m, 100m);
        var tax = R(taxableBase * (rate / 100m));
        return (tax, new[] { new GvkBracketSlice($"Sabit Oran (%{rate:0.##})", taxableBase, rate, tax) });
    }

    private static IReadOnlyList<ClosingJournalVoucher> BuildClosingVouchers(
        IReadOnlyList<TdhpAccountLine> allLines,
        decimal rnd630,
        decimal mkt631,
        decimal mgmt632,
        decimal fin660,
        decimal profit690,
        decimal tax691,
        decimal netProfit692,
        TaxBridgeSummary taxBridge)
    {
        var vouchers = new List<ClosingJournalVoucher>();

        // Fiş 1: 7/A Maliyet Hesaplarından 6'lı Gelir Tablosu Hesaplarına Yansıtma & Kapanış
        var v1Lines = new List<ClosingJournalLine>();
        void AddCostReflection(string costCode, string reflectCode, string targetCode, string targetName, decimal val)
        {
            if (val <= 0m) return;
            v1Lines.Add(new(targetCode, targetName, val, 0m, "7/A yansıtma borcu"));
            v1Lines.Add(new(reflectCode, $"{targetName} Yansıtma", 0m, val, "Gelir tablosu hesabına aktarım"));
            v1Lines.Add(new(reflectCode, $"{targetName} Yansıtma", val, 0m, "Maliyet ve yansıtma kapatma"));
            v1Lines.Add(new(costCode, $"{costCode} Dönem Giderleri", 0m, val, "Maliyet hesabı kapanışı"));
        }

        AddCostReflection("750", "751", "630", "Araştırma ve Geliştirme Gid.", rnd630);
        AddCostReflection("760", "761", "631", "Pazarlama, Satış ve Dağıtım Gid.", mkt631);
        AddCostReflection("770", "771", "632", "Genel Yönetim Giderleri", mgmt632);
        AddCostReflection("780", "781", "660", "Kısa Vadeli Borçlanma Gid.", fin660);

        if (v1Lines.Count > 0)
        {
            vouchers.Add(FinalizeVoucher(
                1,
                "7/A Maliyet Hesapları Yansıtma ve Kapatma Fişi",
                "Ünite 7 & 8: 750/760/770/780 hesapları yansıtma hesapları üzerinden 630/631/632/660 hesaplarına aktarılır ve kapatılır.",
                v1Lines));
        }

        // Fiş 2: Tüm 60–68 Gelir ve Gider Hesaplarının 690 Dönem Kârı veya Zararına Devri
        var v2Lines = new List<ClosingJournalLine>();
        decimal debitSum = 0m;
        decimal creditSum = 0m;

        foreach (var line in allLines)
        {
            if (line.IsSubtotal || line.AccountCode is "690" or "691" or "692" || line.AmountTry == 0m)
                continue;

            var amt = R(Math.Abs(line.AmountTry));
            if (line.IsDeduction)
            {
                // Borç bakiyeli gider / satış indirimi hesapları alacaklandırılarak kapatılır.
                v2Lines.Add(new(line.AccountCode, line.AccountName, 0m, amt, "Sonuç hesabı devir kapanışı"));
                creditSum += amt;
            }
            else
            {
                // Alacak bakiyeli hasılat/gelir hesapları borçlandırılarak kapatılır.
                v2Lines.Add(new(line.AccountCode, line.AccountName, amt, 0m, "Hasılat/gelir devir kapanışı"));
                debitSum += amt;
            }
        }

        // Tam Borç=Alacak dengesi için fark doğrudan v2 üzerinden hesaplanır (yuvarlama farkı kalmaz).
        var netDiff = R(debitSum - creditSum);
        if (netDiff >= 0m)
        {
            v2Lines.Add(new("690", "Dönem Kârı veya Zararı", 0m, netDiff, "Dönem ticari kârının 690 hesaba devri"));
        }
        else
        {
            v2Lines.Add(new("690", "Dönem Kârı veya Zararı", Math.Abs(netDiff), 0m, "Dönem ticari zararının 690 hesaba devri"));
        }

        vouchers.Add(FinalizeVoucher(
            2,
            "Gelir ve Gider Hesaplarının 690 Hesabına Kapanış Fişi",
            "Ünite 8: Tüm 60–68 hesapları sıfırlanır; ticari kâr/zarar 690 Dönem Kârı veya Zararı hesabına alınır.",
            v2Lines));

        // Fiş 3: 691 Vergi Karşılığı -> 692 Dönem Net Kârı -> 590/591 Bilanço Devri
        var v3Lines = new List<ClosingJournalLine>();
        var balancedProfit690 = netDiff;
        var balancedNet692 = R(balancedProfit690 - tax691);

        if (balancedProfit690 > 0m)
        {
            if (tax691 > 0m)
            {
                v3Lines.Add(new("691", "Dönem Kârı Vergi ve Yasal Yük. Karş.", tax691, 0m, "Mali kâr üzerinden vergi tahakkuku"));
                v3Lines.Add(new("370", "Dönem Kârı Vergi ve Yasal Yük. Karş.", 0m, tax691, "Bilanço vergi karşılığı borcu"));
                v3Lines.Add(new("690", "Dönem Kârı veya Zararı", balancedProfit690, 0m, "Ticari kârın kapatılması"));
                v3Lines.Add(new("691", "Dönem Kârı Vergi ve Yasal Yük. Karş.", 0m, tax691, "691 hesabının kapatılması"));
                if (balancedNet692 >= 0m)
                {
                    if (balancedNet692 > 0m)
                        v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", 0m, balancedNet692, "Vergi sonrası net kâr"));
                }
                else
                {
                    v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", Math.Abs(balancedNet692), 0m, "KKEG/vergi sonrası dönem net zararı"));
                }
            }
            else
            {
                var exemptionNote = taxBridge.YoungEntrepreneurModeEnabled
                    ? "GVK Mük. Md. 20 Genç Girişimci istisnası nedeniyle 691=₺0"
                    : "İstisna/indirimler sonrası vergi matrahı oluşmadı";
                v3Lines.Add(new("690", "Dönem Kârı veya Zararı", balancedProfit690, 0m, exemptionNote));
                v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", 0m, balancedProfit690, "Vergisiz net dönem kârı"));
                balancedNet692 = balancedProfit690;
            }

            if (balancedNet692 > 0m)
            {
                v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", balancedNet692, 0m, "692 kapatma"));
                v3Lines.Add(new("590", "Dönem Net Kârı", 0m, balancedNet692, "Öz kaynaklara (bilançoya) devir"));
            }
            else if (balancedNet692 < 0m)
            {
                var netLoss = Math.Abs(balancedNet692);
                v3Lines.Add(new("591", "Dönem Net Zararı (-)", netLoss, 0m, "Bilanço öz kaynak indirimine devir"));
                v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", 0m, netLoss, "692 kapatma"));
            }
        }
        else if (balancedProfit690 < 0m || tax691 > 0m)
        {
            var loss = Math.Abs(balancedProfit690);
            var totalNetLoss = R(loss + tax691);
            if (tax691 > 0m)
            {
                v3Lines.Add(new("691", "Dönem Kârı Vergi ve Yasal Yük. Karş.", tax691, 0m, "KKEG kaynaklı mali kâr vergi tahakkuku"));
                v3Lines.Add(new("370", "Dönem Kârı Vergi ve Yasal Yük. Karş.", 0m, tax691, "Bilanço vergi karşılığı borcu"));
                v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", totalNetLoss, 0m, "Ticari zarar + vergi karşılığı"));
                if (loss > 0m)
                    v3Lines.Add(new("690", "Dönem Kârı veya Zararı", 0m, loss, "690 ticari zarar kapatma"));
                v3Lines.Add(new("691", "Dönem Kârı Vergi ve Yasal Yük. Karş.", 0m, tax691, "691 kapatma"));
            }
            else
            {
                v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", loss, 0m, "Ticari zarar aktarımı"));
                v3Lines.Add(new("690", "Dönem Kârı veya Zararı", 0m, loss, "690 kapatma"));
            }

            v3Lines.Add(new("591", "Dönem Net Zararı (-)", totalNetLoss, 0m, "Bilanço öz kaynak indirimine devir"));
            v3Lines.Add(new("692", "Dönem Net Kârı veya Zararı", 0m, totalNetLoss, "692 kapatma"));
        }

        if (v3Lines.Count > 0)
        {
            vouchers.Add(FinalizeVoucher(
                3,
                "690 → 691 → 692 Vergi Karşılığı ve Dönem Net Kâr/Zarar Kapanış Fişi",
                "Ünite 8: Ticari kârdan Mali Kâra geçilir, vergi karşılığı (691/370) düşülür ve net sonuç 590/591 hesabına devredilir.",
                v3Lines));
        }

        return vouchers;
    }

    private static ClosingJournalVoucher FinalizeVoucher(int no, string title, string explanation, List<ClosingJournalLine> lines)
    {
        var totalDr = R(lines.Sum(l => l.DebitTry));
        var totalCr = R(lines.Sum(l => l.CreditTry));
        return new ClosingJournalVoucher(no, title, explanation, lines, totalDr, totalCr, totalDr == totalCr);
    }

    private static (AccountBucketInput NetExpense, AccountBucketInput ShiftedPriorRefund) NormalizeExpenseOrPriorRefund(AccountBucketInput bucket)
    {
        if (bucket.AmountTry >= 0m)
            return (bucket, AccountBucketInput.Empty);

        var posTry = R(Math.Abs(bucket.AmountTry));
        var posUsd = R(Math.Abs(bucket.AmountUsd));
        var shifted = new AccountBucketInput(posTry, posUsd, "İade alınan komisyon / net alacak farkı", bucket.Sources);
        return (AccountBucketInput.Empty, shifted);
    }

    private static AccountBucketInput MergeBuckets(params AccountBucketInput[] buckets)
    {
        decimal t = 0m, u = 0m;
        var notes = new List<string>();
        var sources = new List<IncomeStatementSourceItem>();

        foreach (var b in buckets)
        {
            if (b.AmountTry == 0m && b.AmountUsd == 0m && (b.Sources == null || b.Sources.Count == 0))
                continue;

            t += b.AmountTry;
            u += b.AmountUsd;
            if (!string.IsNullOrWhiteSpace(b.Note)) notes.Add(b.Note!);
            if (b.Sources != null) sources.AddRange(b.Sources);
        }

        return new AccountBucketInput(R(t), R(u), notes.Count > 0 ? string.Join(" | ", notes.Distinct()) : null, sources);
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}
