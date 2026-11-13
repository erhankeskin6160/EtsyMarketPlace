using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.Accounting;
using EtsyMarketPlace.Application.Common.Models;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Domain.Accounting;
using EtsyMarketPlace.Domain.Accounting.Entities;
using MediatR;

namespace EtsyMarketPlace.Application.Features.Accounting;

// =========================================================================
// Response DTO
// =========================================================================

public sealed record IncomeStatementResponseDto(
    IncomeStatementDocument Statement,
    IReadOnlyList<OperatingExpenseEntry> ConfiguredExpenses,
    IReadOnlyList<FixedAssetItem> FixedAssets,
    IReadOnlyList<InventoryPeriodAdjustment> InventoryAdjustments,
    IReadOnlyList<string> DataCompletenessNotes);

// =========================================================================
// 1. Resmi Gelir Tablosu Query & Handler
// =========================================================================

public sealed record GetIncomeStatementQuery(
    string ShopId,
    string Period = "this_month",
    DateTimeOffset? StartDate = null,
    DateTimeOffset? EndDate = null,
    bool? YoungEntrepreneurModeOverride = null) : IRequest<ServiceResponse>;

public sealed class GetIncomeStatementHandler(
    IEtsyLedgerReportService ledgerReportService,
    IEtsyDashboardService dashboardService,
    IAccountingRepository accountingRepository) : IRequestHandler<GetIncomeStatementQuery, ServiceResponse>
{
    private const decimal FallbackFxRate = 48.25m;

    public async Task<ServiceResponse> Handle(GetIncomeStatementQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);

        DateTimeOffset start, end;
        try
        {
            (start, end) = EtsyDashboardService.ResolvePeriod(request.Period, request.StartDate, request.EndDate);
        }
        catch (ArgumentException ex)
        {
            return ServiceResponse.CreateError(ex.Message, HttpStatusCode.BadRequest);
        }

        // Aynı scoped EF Core DbContext üzerinden paralel sorgu açılmaması için sırayla okunur.
        var storedProfile = await accountingRepository.GetOrDefaultProfileAsync(request.ShopId, cancellationToken);
        var profile = ApplyProfileOverride(storedProfile, request.YoungEntrepreneurModeOverride);
        var expenses = await accountingRepository.GetOperatingExpensesAsync(request.ShopId, cancellationToken);
        var fixedAssets = await accountingRepository.GetFixedAssetsAsync(request.ShopId, cancellationToken);
        var inventoryAdjustments = await accountingRepository.GetInventoryAdjustmentsAsync(request.ShopId, cancellationToken);

        var ledgerData = await ledgerReportService.GetAccountingLedgerDataAsync(request.ShopId, start, end, cancellationToken);
        if (ledgerData is null)
        {
            var live = await ledgerReportService.GetLiveReportAsync(request.ShopId, start, end, cancellationToken);
            ledgerData = BuildFallbackLedgerData(live);
        }

        IReadOnlyList<EtsyBankPayout> payouts = Array.Empty<EtsyBankPayout>();
        try
        {
            payouts = await dashboardService.GetBankPayoutsAsync(request.ShopId, start, end, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _ = ex;
        }

        var notes = new List<string>();
        var report = ledgerData.Report;
        var sub = ledgerData.SubBreakdown;
        var split = ledgerData.CostSplit;

        var avgFx = report.AverageExchangeRate > 0m
            ? report.AverageExchangeRate
            : HistoricalExchangeRateProvider.GetRateForDate(end.UtcDateTime.Date, FallbackFxRate);

        if (!ledgerData.LedgerOk)
            notes.Add(ledgerData.Warning ?? "Canlı Etsy defterine ulaşılamadı; veriler eksik olabilir.");
        else if (!string.IsNullOrWhiteSpace(ledgerData.Warning))
            notes.Add(ledgerData.Warning!);

        // --- A. BRÜT SATIŞLAR: 600 Yurt İçi vs 601 Yurt Dışı (country_iso) ---
        var paidReceipts = ledgerData.Receipts.Where(r => r.IsPaid).ToList();
        var missingCountryCount = paidReceipts.Count(r => string.IsNullOrWhiteSpace(r.CountryCode));
        if (missingCountryCount > 0)
        {
            notes.Add($"{missingCountryCount} ödenmiş siparişte Etsy country_iso bilgisi bulunmadığı için varsayılan olarak 601 Yurt Dışı Satışlar (İhracat) hesabına alındı.");
        }

        var totalPaidReceiptUsd = paidReceipts.Sum(r => Math.Max(0m, r.GrandTotal));
        var domesticReceiptUsd = paidReceipts
            .Where(r => string.Equals(r.CountryCode?.Trim(), "TR", StringComparison.OrdinalIgnoreCase))
            .Sum(r => Math.Max(0m, r.GrandTotal));

        var domesticShare = totalPaidReceiptUsd > 0m ? domesticReceiptUsd / totalPaidReceiptUsd : 0m;
        var grossTotalTry = R(sub.GrossSalesRawTry);
        var grossTotalUsd = R(sub.GrossSalesUsd);
        var dom600Try = R(grossTotalTry * domesticShare);
        var exp601Try = R(grossTotalTry - dom600Try);
        var dom600Usd = R(grossTotalUsd * domesticShare);
        var exp601Usd = R(grossTotalUsd - dom600Usd);

        var domesticSources = new List<IncomeStatementSourceItem>();
        var exportSources = new List<IncomeStatementSourceItem>();
        foreach (var r in paidReceipts)
        {
            var rUsd = R(r.GrandTotal);
            var rRate = HistoricalExchangeRateProvider.GetRateForDate(r.CreatedAt.UtcDateTime.Date, avgFx);
            var rTry = R(rUsd * rRate);
            var item = new IncomeStatementSourceItem(
                r.CreatedAt,
                $"#{r.ReceiptId}",
                $"{r.BuyerName} ({r.CountryCode ?? "EXPORT"})",
                "Etsy Sipariş",
                rTry,
                rUsd);

            if (string.Equals(r.CountryCode?.Trim(), "TR", StringComparison.OrdinalIgnoreCase))
                domesticSources.Add(item);
            else
                exportSources.Add(item);
        }

        // --- B. SATIŞ İNDİRİMLERİ (610 / 611 / 612) ---
        var refundSources = ledgerData.Entries
            .Where(e => e.Type == "refund")
            .Select(e => new IncomeStatementSourceItem(
                e.CreatedAt,
                e.ReferenceId > 0 ? $"#{e.ReferenceId}" : $"E-{e.EntryId}",
                string.IsNullOrWhiteSpace(e.Description) ? "Etsy İade / İptal" : e.Description,
                "Etsy Defter (İade)",
                R(-e.AmountTry),
                R(-e.Amount)))
            .ToList();

        // --- MANUEL İŞLETME GİDER FİŞLERİ & BAĞ-KUR (890) ---
        var accumulators = new Dictionary<OperatingExpenseCategory, MutableBucket>();
        decimal kkeg950Try = 0m;
        decimal bagkur890Try = 0m;
        decimal nonEtsyOperatingExpensesNetTry = 0m;
        var expandedExpenses = ExpandExpensesForPeriod(expenses, start, end, avgFx);

        foreach (var item in expandedExpenses)
        {
            if (item.Category == OperatingExpenseCategory.BagkurPremiumDeduction)
            {
                bagkur890Try += item.AmountTry;
                continue;
            }

            GetBucket(accumulators, item.Category).Add(
                item.AmountTry,
                item.AmountUsd,
                new IncomeStatementSourceItem(
                    item.OccurrenceDate,
                    string.IsNullOrWhiteSpace(item.DocumentNo) ? $"GİDER-{item.Id}" : item.DocumentNo,
                    item.Description,
                    item.IsTaxDeductible ? "Manuel Gider/Gelir Fişi" : "KKEG Fişi (950/951)",
                    item.AmountTry,
                    item.AmountUsd,
                    item.IsTaxDeductible));

            if (!item.IsTaxDeductible)
                kkeg950Try += item.AmountTry;

            // Etsy katkı kârından 690 Ticari Kâra köprü: olağanüstü hesaplar (671/679/680/681/689)
            // ExtraordinaryNetTry altında gösterildiği için yalnızca olağan dışı olmayan kalemler burada toplanır.
            if (!IsExtraordinaryCategory(item.Category))
            {
                var signedEffectOnExpenses = item.Category is OperatingExpenseCategory.InterestIncome_642 or OperatingExpenseCategory.OtherOrdinaryIncome_649
                    ? -item.AmountTry
                    : item.AmountTry;
                nonEtsyOperatingExpensesNetTry += signedEffectOnExpenses;
            }
        }

        // --- DURAN VARLIK AMORTİSMANLARI (253 / 255 -> 257) ---
        var assetDepreciations = fixedAssets
            .Select(a => DepreciationCalculator.CalculateForPeriod(a, start, end))
            .Where(d => d.PeriodDepreciationTry > 0m || d.AcquisitionCostTry > 0m)
            .ToList();

        decimal totalPeriodDepreciationTry = 0m;
        foreach (var dep in assetDepreciations.Where(d => d.PeriodDepreciationTry > 0m))
        {
            totalPeriodDepreciationTry += dep.PeriodDepreciationTry;
            var depUsd = avgFx > 0m ? R(dep.PeriodDepreciationTry / avgFx) : 0m;
            GetBucket(accumulators, dep.ExpenseTargetCategory).Add(
                dep.PeriodDepreciationTry,
                depUsd,
                new IncomeStatementSourceItem(
                    end,
                    $"{dep.AssetAccountCode}/257",
                    $"Amortisman: {dep.AssetName} ({(dep.Method == DepreciationMethod.DecliningBalance ? "Azalan Bakiyeler" : "Normal")})",
                    "Duran Varlık Amortismanı",
                    dep.PeriodDepreciationTry,
                    depUsd));
        }

        // --- D. SATIŞLARIN MALİYETİ (620): Devamlı vs Aralıklı Envanter & Seçenek A/B ---
        var missingCostCount = paidReceipts.Count(r =>
            !split.OrderCostLines.Any(l => l.ReceiptId == r.ReceiptId));
        if (missingCostCount > 0)
        {
            notes.Add($"{missingCostCount} ödenmiş sipariş için Sipariş Maliyeti (ürün/kargo/paketleme) henüz girilmemiş.");
        }

        bool useShippingInCogs = profile.ShippingTarget == ShippingPackagingCostTarget.CostOfGoods_620;
        bool canceledToCogs = profile.CanceledCostTarget == CanceledOrderCostTarget.CostOfGoods_620;

        decimal perpetualCogsUsd = split.ActiveProductCostUsd;
        decimal perpetualCogsTry = split.ActiveProductCostTry;
        if (useShippingInCogs)
        {
            perpetualCogsUsd += split.ActiveShippingCostUsd + split.ActivePackagingCostUsd;
            perpetualCogsTry += split.ActiveShippingCostTry + split.ActivePackagingCostTry;
        }
        if (canceledToCogs)
        {
            perpetualCogsUsd += split.CanceledProductCostUsd + (useShippingInCogs ? split.CanceledShippingCostUsd + split.CanceledPackagingCostUsd : 0m);
            perpetualCogsTry += split.CanceledProductCostTry + (useShippingInCogs ? split.CanceledShippingCostTry + split.CanceledPackagingCostTry : 0m);
        }

        var cogsBucket = GetBucket(accumulators, OperatingExpenseCategory.CostOfGoodsAdjustment_620);
        if (profile.InventoryMethod == InventoryValuationMethod.PeriodicCount)
        {
            var matchedAdjustments = FilterAdjustmentsForPeriod(inventoryAdjustments, start, end);
            if (matchedAdjustments.Count > 0)
            {
                var periodicTotalTry = matchedAdjustments.Sum(a => a.ComputePeriodicCogsTry());
                var periodicTotalUsd = avgFx > 0m ? R(periodicTotalTry / avgFx) : 0m;
                foreach (var adj in matchedAdjustments)
                {
                    var val = adj.ComputePeriodicCogsTry();
                    cogsBucket.Add(val, avgFx > 0m ? R(val / avgFx) : 0m, new IncomeStatementSourceItem(
                        new DateTimeOffset(adj.Year, adj.Month, 1, 0, 0, 0, TimeSpan.FromHours(3)),
                        $"STMM-{adj.Year}/{adj.Month:D2}",
                        $"Aralıklı Envanter (D.Başı ₺{adj.OpeningInventoryTry:N2} + Net Alış ₺{adj.GrossPurchasesTry - adj.PurchaseReturnsAndDiscountsTry:N2} - D.Sonu ₺{adj.ClosingInventoryTry:N2})",
                        "Aralıklı Envanter Sayımı",
                        val,
                        avgFx > 0m ? R(val / avgFx) : 0m));
                }
                notes.Add($"Aralıklı Envanter uygulandı: Dönem 620 STMM = ₺{periodicTotalTry:N2} (Sipariş bazlı maliyet ₺{perpetualCogsTry:N2} yerine kullanıldı).");
            }
            else
            {
                notes.Add("Aralıklı Envanter seçili ancak bu dönem için stok sayım kaydı bulunamadı; devamlı envanter (sipariş maliyeti) esas alındı.");
                AddOrderCostSourcesToBucket(cogsBucket, split, useShippingInCogs, includeCanceled: canceledToCogs, perpetualCogsTry, perpetualCogsUsd, includeActive: true);
            }
        }
        else
        {
            AddOrderCostSourcesToBucket(cogsBucket, split, useShippingInCogs, includeCanceled: canceledToCogs, perpetualCogsTry, perpetualCogsUsd, includeActive: true);
        }

        // --- E. PAZARLAMA SATIŞ DAĞITIM (631) & F/G KOMİSYONLAR (653 vs 631) ---
        var mktBucket = GetBucket(accumulators, OperatingExpenseCategory.MarketingAndSelling_631);
        var commBucket = GetBucket(accumulators, OperatingExpenseCategory.OtherCommission_653);
        var mgmtBucket = GetBucket(accumulators, OperatingExpenseCategory.GeneralManagement_632);

        AddLedgerCategoryToBucket(mktBucket, "Etsy İçi Arama Reklamı (Etsy Ads)", "ad_fee", sub.InnerAdFeeTry, sub.InnerAdFeeUsd, ledgerData.Entries);
        AddLedgerCategoryToBucket(mktBucket, "Dış Reklam Payı (Offsite Ads)", "offsite_ads", sub.OffsiteAdFeeTry, sub.OffsiteAdFeeUsd, ledgerData.Entries);
        if (sub.ShippingLedgerNetTry != 0m || sub.ShippingLedgerNetUsd != 0m)
        {
            AddLedgerCategoryToBucket(mktBucket, "Etsy Kargo Etiketi Kesintileri", "shipping", sub.ShippingLedgerNetTry, sub.ShippingLedgerNetUsd, ledgerData.Entries);
        }

        if (!useShippingInCogs)
        {
            var shipAndPackTry = split.ActiveShippingCostTry + split.ActivePackagingCostTry;
            var shipAndPackUsd = split.ActiveShippingCostUsd + split.ActivePackagingCostUsd;
            if (canceledToCogs)
            {
                shipAndPackTry += split.CanceledShippingCostTry + split.CanceledPackagingCostTry;
                shipAndPackUsd += split.CanceledShippingCostUsd + split.CanceledPackagingCostUsd;
            }
            if (shipAndPackTry != 0m || shipAndPackUsd != 0m)
            {
                mktBucket.Add(shipAndPackTry, shipAndPackUsd, new IncomeStatementSourceItem(
                    end,
                    "760-KARGO",
                    $"Aktif Sipariş Kargo Navlunu + Paketleme Maliyeti ({split.ActiveCostedOrderCount} sipariş)",
                    "Sipariş Kargo/Paketleme",
                    shipAndPackTry,
                    shipAndPackUsd));
            }
        }

        var feeTargetBucket = profile.CommissionTarget == CommissionAccountTarget.OtherCommission_653 ? commBucket : mktBucket;
        AddLedgerCategoryToBucket(feeTargetBucket, "Etsy İşlem Komisyonu (Transaction Fee)", "transaction_fee", sub.TransactionFeeTry, sub.TransactionFeeUsd, ledgerData.Entries);
        AddLedgerCategoryToBucket(feeTargetBucket, "Etsy Listeleme Ücreti (Listing Fee)", "listing_fee", sub.ListingFeeTry, sub.ListingFeeUsd, ledgerData.Entries);
        AddLedgerCategoryToBucket(feeTargetBucket, "Etsy Ödeme İşleme (Payment Processing)", "payment_processing", sub.PaymentProcessingTry, sub.PaymentProcessingUsd, ledgerData.Entries);

        if (sub.RegulatoryAndTaxFeeTry != 0m || sub.RegulatoryAndTaxFeeUsd != 0m)
        {
            mgmtBucket.Add(sub.RegulatoryAndTaxFeeTry, sub.RegulatoryAndTaxFeeUsd, new IncomeStatementSourceItem(
                end,
                "770-VERGI",
                "Etsy Düzenleyici İşletme Ücreti & Kesinti KDV'si (VAT/Regulatory Fee)",
                "Etsy Defter (Vergi/Düzenleyici)",
                sub.RegulatoryAndTaxFeeTry,
                sub.RegulatoryAndTaxFeeUsd));
        }
        if (sub.OtherUnknownFeeTry != 0m || sub.OtherUnknownFeeUsd != 0m)
        {
            mgmtBucket.Add(sub.OtherUnknownFeeTry, sub.OtherUnknownFeeUsd, new IncomeStatementSourceItem(
                end,
                "770-DIGER",
                "Diğer Etsy Platform Kesintileri",
                "Etsy Defter (Diğer)",
                sub.OtherUnknownFeeTry,
                sub.OtherUnknownFeeUsd));
        }

        // --- F/G KUR FARKLARI (646 / 656) ---
        var fxGainBucket = new MutableBucket();
        var fxLossBucket = new MutableBucket();
        if (profile.IncludeFxSpreadAndExchangeDifference && payouts.Count > 0)
        {
            var salesAvgRate = grossTotalUsd > 0m ? grossTotalTry / grossTotalUsd : avgFx;
            foreach (var p in payouts)
            {
                var pUsd = Math.Abs(p.Amount);
                var pRate = p.ExchangeRateToTry ?? HistoricalExchangeRateProvider.GetRateForDate(p.OccurredAt.UtcDateTime.Date, avgFx);
                var diffTry = R(pUsd * (pRate - salesAvgRate));
                if (diffTry > 0m)
                {
                    fxGainBucket.Add(diffTry, 0m, new IncomeStatementSourceItem(
                        p.OccurredAt,
                        p.ReferenceId,
                        $"Banka Hakedişi Kur Artışı (${pUsd:N2} @ ₺{pRate:N2} vs satış ort. ₺{salesAvgRate:N2})",
                        "646 Kur Farkı",
                        diffTry,
                        0m));
                }
                else if (diffTry < 0m)
                {
                    var absLoss = Math.Abs(diffTry);
                    fxLossBucket.Add(absLoss, 0m, new IncomeStatementSourceItem(
                        p.OccurredAt,
                        p.ReferenceId,
                        $"Banka Hakedişi Kur Düşüşü (${pUsd:N2} @ ₺{pRate:N2} vs satış ort. ₺{salesAvgRate:N2})",
                        "656 Kur Farkı",
                        absLoss,
                        0m));
                }
            }
        }

        // --- J. İPTAL / İADE SİPARİŞ MALİYET ZARARLARI (689) ---
        decimal separatedCanceledLossTry = 0m;
        if (!canceledToCogs && (split.CanceledTotalTry > 0m || split.CanceledTotalUsd > 0m))
        {
            separatedCanceledLossTry = R(split.CanceledTotalTry);
            var extBucket = GetBucket(accumulators, OperatingExpenseCategory.ExtraordinaryExpense_689);
            foreach (var line in split.OrderCostLines.Where(l => l.IsCanceledOrRefunded && l.TotalCostTry > 0m))
            {
                extBucket.Add(line.TotalCostTry, line.TotalCostUsd, new IncomeStatementSourceItem(
                    line.Date,
                    $"#{line.ReceiptId}",
                    $"{(line.IsCrossPeriodRefund ? "Önceki Dönem İadesi" : "İptal/İade")} Maliyet Zararı ({line.BuyerName}: Ürün ${line.ProductCostUsd:N2} + Kargo ${line.ShippingCostUsd:N2} + Paket ${line.PackagingCostUsd:N2})",
                    "689 İptal/İade Zararı",
                    line.TotalCostTry,
                    line.TotalCostUsd));
            }
        }

        // --- MUTABAKAT META DEĞERLERİ ---
        var directFeesAndAdsTry = R(
            Math.Abs(sub.TransactionFeeTry + sub.ListingFeeTry + sub.PaymentProcessingTry + sub.RegulatoryAndTaxFeeTry + sub.OtherUnknownFeeTry)
            + sub.InnerAdFeeTry
            + sub.OffsiteAdFeeTry
            + sub.ShippingLedgerNetTry);

        var input = new IncomeStatementInput
        {
            ShopId = request.ShopId,
            StartDate = start,
            EndDate = end,
            ExchangeRate = avgFx,
            LedgerOk = ledgerData.LedgerOk,
            Warning = ledgerData.Warning,
            Profile = profile,

            DomesticSales600 = new AccountBucketInput(dom600Try, dom600Usd, domesticSources.Count > 0 ? $"{domesticSources.Count} yurt içi (TR) sipariş" : null, domesticSources),
            ExportSales601 = new AccountBucketInput(exp601Try, exp601Usd, exportSources.Count > 0 ? $"{exportSources.Count} yurt dışı sipariş" : null, exportSources),
            SalesReturns610 = new AccountBucketInput(R(sub.RefundTry), R(sub.RefundUsd), refundSources.Count > 0 ? $"{refundSources.Count} iade kaydı" : null, refundSources),
            SalesDiscounts611 = ToBucket(accumulators, OperatingExpenseCategory.SalesDiscount_611),
            OtherSalesDeductions612 = ToBucket(accumulators, OperatingExpenseCategory.OtherSalesDeduction_612),

            CostOfManufacturedGoods620 = ToBucket(accumulators, OperatingExpenseCategory.CostOfGoodsAdjustment_620,
                profile.InventoryMethod == InventoryValuationMethod.PeriodicCount ? "Aralıklı Envanter STMM" : "3D Baskı / Mamul Üretim Maliyeti"),
            ResearchAndDev630 = ToBucket(accumulators, OperatingExpenseCategory.ResearchAndDevelopment_630),
            MarketingSelling631 = ToBucket(accumulators, OperatingExpenseCategory.MarketingAndSelling_631,
                profile.CommissionTarget == CommissionAccountTarget.MarketingSelling_631
                    ? "Reklam + Kargo + Platform Komisyonları (Seçenek A)"
                    : "Etsy Ads + Offsite Ads + Kargo Navlunu & Paketleme"),
            GeneralManagement632 = ToBucket(accumulators, OperatingExpenseCategory.GeneralManagement_632,
                "Yönetim, Muhasebe, Amortisman & Etsy KDV/Düzenleyici Ücretler"),

            InterestIncome642 = ToBucket(accumulators, OperatingExpenseCategory.InterestIncome_642),
            FxGains646 = fxGainBucket.ToInput("Döviz lehine gerçekleşen kur farkları"),
            OtherOrdinaryIncome649 = ToBucket(accumulators, OperatingExpenseCategory.OtherOrdinaryIncome_649),

            CommissionExpenses653 = ToBucket(accumulators, OperatingExpenseCategory.OtherCommission_653,
                "Etsy İşlem, Listeleme ve Ödeme İşleme Komisyonları (Seçenek B)"),
            FxLosses656 = fxLossBucket.ToInput("Döviz aleyhine kur farkları"),
            OtherOrdinaryExpenses659 = ToBucket(accumulators, OperatingExpenseCategory.OtherOrdinaryExpense_659),

            ShortTermFinancing660 = ToBucket(accumulators, OperatingExpenseCategory.Financing_660),
            PriorPeriodIncome671 = ToBucket(accumulators, OperatingExpenseCategory.PriorPeriodIncome_671),
            ExtraordinaryIncome679 = ToBucket(accumulators, OperatingExpenseCategory.ExtraordinaryIncome_679),
            IdleCapacityLoss680 = ToBucket(accumulators, OperatingExpenseCategory.IdleCapacityLoss_680),
            PriorPeriodExpense681 = ToBucket(accumulators, OperatingExpenseCategory.PriorPeriodExpense_681),
            ExtraordinaryExpenses689 = ToBucket(accumulators, OperatingExpenseCategory.ExtraordinaryExpense_689,
                separatedCanceledLossTry > 0m ? "Üretilmiş/kargolanmış iptal/iade sipariş maliyet zararları" : null),

            KkegAdditions950Try = R(kkeg950Try),
            BagkurDeductionTry = R(bagkur890Try),
            EtsyContributionProfitTry = R(report.NetProfitTry),
            EtsyDirectFeesAndAdsTry = directFeesAndAdsTry,
            EtsyDirectOrderCostsTry = R(split.TotalTry),
            SeparatedCanceledCostLossTry = separatedCanceledLossTry,
            NonEtsyOperatingExpensesTry = R(nonEtsyOperatingExpensesNetTry),
            DepreciationExpenseTry = R(totalPeriodDepreciationTry),
            AssetDepreciations = assetDepreciations
        };

        var document = IncomeStatementCalculator.Calculate(input);
        return ServiceResponse.CreateSuccess(
            ResponseMessage.Succeed,
            new IncomeStatementResponseDto(document, expenses, fixedAssets, inventoryAdjustments, notes));
    }

    private sealed class MutableBucket
    {
        public decimal Try { get; private set; }
        public decimal Usd { get; private set; }
        public List<IncomeStatementSourceItem> Sources { get; } = new();

        public void Add(decimal amountTry, decimal amountUsd, IncomeStatementSourceItem? source = null)
        {
            Try += amountTry;
            Usd += amountUsd;
            if (source != null) Sources.Add(source);
        }

        public AccountBucketInput ToInput(string? note = null) =>
            new(R(Try), R(Usd), note, Sources);
    }

    private static MutableBucket GetBucket(Dictionary<OperatingExpenseCategory, MutableBucket> map, OperatingExpenseCategory category)
    {
        if (!map.TryGetValue(category, out var bucket))
        {
            bucket = new MutableBucket();
            map[category] = bucket;
        }
        return bucket;
    }

    private static AccountBucketInput ToBucket(Dictionary<OperatingExpenseCategory, MutableBucket> map, OperatingExpenseCategory category, string? note = null) =>
        map.TryGetValue(category, out var b) ? b.ToInput(note) : new AccountBucketInput(0m, 0m, note, Array.Empty<IncomeStatementSourceItem>());

    private static void AddOrderCostSourcesToBucket(
        MutableBucket bucket,
        EtsyOrderCostAccountSplit split,
        bool includeShippingInCogs,
        bool includeCanceled,
        decimal totalTry,
        decimal totalUsd,
        bool includeActive)
    {
        var addedTry = 0m;
        var addedUsd = 0m;
        var relevantLines = split.OrderCostLines.Where(l =>
            (includeActive && !l.IsCanceledOrRefunded) || (includeCanceled && l.IsCanceledOrRefunded)).ToList();

        if (relevantLines.Count == 0 && totalTry == 0m && totalUsd == 0m)
            return;

        foreach (var line in relevantLines)
        {
            var u = includeShippingInCogs ? line.TotalCostUsd : line.ProductCostUsd;
            var t = includeShippingInCogs ? line.TotalCostTry : R(line.ProductCostUsd * line.ExchangeRate);
            if (t == 0m && u == 0m) continue;
            addedTry += t;
            addedUsd += u;
            bucket.Add(t, u, new IncomeStatementSourceItem(
                line.Date,
                $"#{line.ReceiptId}",
                includeShippingInCogs
                    ? $"Sipariş Tam Maliyet ({line.BuyerName})"
                    : $"Mamul Üretim Maliyeti ({line.BuyerName})",
                "Sipariş Maliyeti (620)",
                t,
                u));
        }

        // Günlük yuvarlama farkını tam eşitle
        var residualTry = R(totalTry - addedTry);
        var residualUsd = R(totalUsd - addedUsd);
        if (residualTry != 0m || residualUsd != 0m)
        {
            bucket.Add(residualTry, residualUsd);
        }
    }

    private static void AddLedgerCategoryToBucket(
        MutableBucket bucket,
        string label,
        string entryType,
        decimal netExpenseTry,
        decimal netExpenseUsd,
        IReadOnlyList<EtsyLedgerEntryDetail> entries)
    {
        if (netExpenseTry == 0m && netExpenseUsd == 0m)
            return;

        var count = entries.Count(e => e.Type == entryType);
        bucket.Add(netExpenseTry, netExpenseUsd, new IncomeStatementSourceItem(
            entries.Where(e => e.Type == entryType).Select(e => e.CreatedAt).DefaultIfEmpty(DateTimeOffset.UtcNow).Max(),
            entryType.ToUpperInvariant(),
            $"{label} ({count} defter kaydı)",
            "Etsy Defter",
            netExpenseTry,
            netExpenseUsd));
    }

    private sealed record ExpandedExpenseItem(
        long Id,
        DateTimeOffset OccurrenceDate,
        string DocumentNo,
        OperatingExpenseCategory Category,
        string Description,
        decimal AmountTry,
        decimal AmountUsd,
        bool IsTaxDeductible);

    private static IReadOnlyList<ExpandedExpenseItem> ExpandExpensesForPeriod(
        IReadOnlyList<OperatingExpenseEntry> entries,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        decimal avgFx)
    {
        var trOffset = TimeSpan.FromHours(3);
        var startDate = periodStart.ToOffset(trOffset).Date;
        var endTr = periodEnd.ToOffset(trOffset);
        var endDateInclusive = endTr.TimeOfDay == TimeSpan.Zero && endTr.Date > startDate
            ? endTr.Date.AddDays(-1)
            : endTr.Date;

        var result = new List<ExpandedExpenseItem>();

        foreach (var e in entries)
        {
            var amountTry = e.AmountTry > 0m ? e.AmountTry : (e.Currency == "USD" ? R(e.Amount * (e.ExchangeRateToTry > 0m ? e.ExchangeRateToTry : avgFx)) : e.Amount);
            var amountUsd = e.Currency == "USD" ? e.Amount : (avgFx > 0m ? R(amountTry / avgFx) : 0m);

            if (e.Frequency == ExpenseFrequency.OneTime)
            {
                var d = DateTime.SpecifyKind(e.EntryDate.Date, DateTimeKind.Unspecified);
                if (d >= startDate && d <= endDateInclusive)
                {
                    result.Add(new ExpandedExpenseItem(e.Id, new DateTimeOffset(d, trOffset), e.DocumentNo, e.Category, e.Description, R(amountTry), R(amountUsd), e.IsTaxDeductible));
                }
            }
            else
            {
                var entryMonthStart = new DateTime(e.EntryDate.Year, e.EntryDate.Month, 1);
                var cursor = new DateTime(startDate.Year, startDate.Month, 1);
                var endMonth = new DateTime(endDateInclusive.Year, endDateInclusive.Month, 1);

                while (cursor <= endMonth)
                {
                    if (cursor >= entryMonthStart && (!e.RecurringEndDate.HasValue || cursor <= e.RecurringEndDate.Value.Date))
                    {
                        var occDay = Math.Min(e.EntryDate.Day, DateTime.DaysInMonth(cursor.Year, cursor.Month));
                        var occDate = new DateTime(cursor.Year, cursor.Month, occDay);
                        result.Add(new ExpandedExpenseItem(
                            e.Id,
                            new DateTimeOffset(occDate, trOffset),
                            e.DocumentNo,
                            e.Category,
                            $"{e.Description} ({cursor:yyyy-MM} Aylık Tekrar)",
                            R(amountTry),
                            R(amountUsd),
                            e.IsTaxDeductible));
                    }
                    cursor = cursor.AddMonths(1);
                }
            }
        }

        return result;
    }

    private static IReadOnlyList<InventoryPeriodAdjustment> FilterAdjustmentsForPeriod(
        IReadOnlyList<InventoryPeriodAdjustment> adjustments,
        DateTimeOffset start,
        DateTimeOffset end)
    {
        var trOffset = TimeSpan.FromHours(3);
        var startDate = start.ToOffset(trOffset).Date;
        var endTr = end.ToOffset(trOffset);
        var endDate = endTr.TimeOfDay == TimeSpan.Zero && endTr.Date > startDate ? endTr.Date.AddDays(-1) : endTr.Date;

        var startKey = startDate.Year * 100 + startDate.Month;
        var endKey = endDate.Year * 100 + endDate.Month;

        return adjustments
            .Where(a => a.Year * 100 + a.Month >= startKey && a.Year * 100 + a.Month <= endKey)
            .OrderBy(a => a.Year).ThenBy(a => a.Month)
            .ToList();
    }

    private static bool IsExtraordinaryCategory(OperatingExpenseCategory c) =>
        c is OperatingExpenseCategory.PriorPeriodIncome_671
            or OperatingExpenseCategory.ExtraordinaryIncome_679
            or OperatingExpenseCategory.IdleCapacityLoss_680
            or OperatingExpenseCategory.PriorPeriodExpense_681
            or OperatingExpenseCategory.ExtraordinaryExpense_689;

    private static ShopAccountingProfile ApplyProfileOverride(ShopAccountingProfile p, bool? youngModeOverride)
    {
        if (!youngModeOverride.HasValue || youngModeOverride.Value == p.YoungEntrepreneurModeEnabled)
            return p;

        return new ShopAccountingProfile
        {
            ShopId = p.ShopId,
            CommissionTarget = p.CommissionTarget,
            ShippingTarget = p.ShippingTarget,
            CanceledCostTarget = p.CanceledCostTarget,
            InventoryMethod = p.InventoryMethod,
            TaxRegime = p.TaxRegime,
            YoungEntrepreneurModeEnabled = youngModeOverride.Value,
            YoungEntrepreneurAnnualLimitTry = p.YoungEntrepreneurAnnualLimitTry,
            YoungEntrepreneurPriorUsedTry = p.YoungEntrepreneurPriorUsedTry,
            EExportExemptionRatePct = p.EExportExemptionRatePct,
            CustomOrCorporateTaxRatePct = p.CustomOrCorporateTaxRatePct,
            CustomGvkBracketsJson = p.CustomGvkBracketsJson,
            IncludeFxSpreadAndExchangeDifference = p.IncludeFxSpreadAndExchangeDifference,
            UpdatedAt = p.UpdatedAt
        };
    }

    private static EtsyAccountingLedgerData BuildFallbackLedgerData(EtsyLedgerLiveResult live)
    {
        var perf = live.Performance;
        var fx = perf.ExchangeRateUsed ?? FallbackFxRate;
        var grossUsd = perf.GrossSales;
        var grossTry = perf.GrossSalesTRY ?? R(grossUsd * fx);
        var feesUsd = perf.PlatformFees;
        var feesTry = perf.PlatformFeesTRY ?? R(feesUsd * fx);
        var innerUsd = perf.InternalAdsCost;
        var innerTry = perf.InternalAdsCostTRY ?? R(innerUsd * fx);
        var offsiteUsd = perf.ExternalAdsCost;
        var offsiteTry = perf.ExternalAdsCostTRY ?? R(offsiteUsd * fx);
        var refundsUsd = perf.Refunds;
        var refundsTry = perf.RefundsTRY ?? R(refundsUsd * fx);
        var prodUsd = perf.ProductCosts;
        var prodTry = perf.ProductCostsTRY ?? R(prodUsd * fx);
        var netTry = perf.NetProfitTRY ?? R(grossTry - feesTry - innerTry - offsiteTry - refundsTry - prodTry);

        var report = new EtsyLedgerFinancialReport(
            grossUsd, -feesUsd, -innerUsd, -offsiteUsd, -refundsUsd, 0m, -perf.ShippingCosts, prodUsd,
            perf.GrossSales - feesUsd - innerUsd - offsiteUsd - refundsUsd - perf.ShippingCosts,
            perf.NetProfit,
            grossTry, -feesTry, -innerTry, -offsiteTry, -refundsTry, prodTry, netTry, fx,
            live.Daily, live.Monthly);

        var sub = new EtsyLedgerSubBreakdown(
            grossUsd, grossTry,
            feesUsd, feesTry,
            0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m,
            innerUsd, innerTry,
            offsiteUsd, offsiteTry,
            refundsUsd, refundsTry,
            perf.ShippingCosts, R(perf.ShippingCosts * fx),
            0m, 0m);

        var split = new EtsyOrderCostAccountSplit(
            prodUsd, prodTry, 0m, 0m, 0m, 0m,
            0m, 0m, 0m, 0m, 0m, 0m,
            live.ReceiptCount, 0, Array.Empty<EtsyOrderCostAccountingLine>());

        return new EtsyAccountingLedgerData(
            live.LedgerOk, live.Warning, live.GeneratedAt,
            report, sub, split,
            Array.Empty<EtsyDashboardReceipt>(),
            Array.Empty<EtsyLedgerEntryDetail>());
    }

    private static decimal R(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
}

// =========================================================================
// 2. İşletme Gider Fişi CRUD Commands & Query
// =========================================================================

public sealed record GetOperatingExpensesQuery(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetOperatingExpensesHandler(IAccountingRepository repo) : IRequestHandler<GetOperatingExpensesQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetOperatingExpensesQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        var list = await repo.GetOperatingExpensesAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, list);
    }
}

public sealed record UpsertOperatingExpenseCommand(
    long? Id,
    string ShopId,
    DateTime EntryDate,
    string? DocumentNo,
    OperatingExpenseCategory Category,
    string Description,
    decimal Amount,
    string Currency,
    decimal ExchangeRateToTry,
    bool IsTaxDeductible,
    ExpenseFrequency Frequency,
    DateTime? RecurringEndDate) : IRequest<ServiceResponse>;

public sealed class UpsertOperatingExpenseHandler(IAccountingRepository repo) : IRequestHandler<UpsertOperatingExpenseCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpsertOperatingExpenseCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        if (string.IsNullOrWhiteSpace(request.Description))
            return ServiceResponse.CreateError("Gider açıklaması zorunludur.", HttpStatusCode.BadRequest);
        if (request.Amount <= 0m)
            return ServiceResponse.CreateError("Tutar 0'dan büyük olmalıdır.", HttpStatusCode.BadRequest);

        var currency = string.IsNullOrWhiteSpace(request.Currency) ? "TRY" : request.Currency.Trim().ToUpperInvariant();
        var rate = currency == "TRY"
            ? 1m
            : (request.ExchangeRateToTry > 0m
                ? request.ExchangeRateToTry
                : HistoricalExchangeRateProvider.GetRateForDate(request.EntryDate.Date, 48.25m));
        var amountTry = Math.Round(request.Amount * rate, 2, MidpointRounding.AwayFromZero);

        var entity = new OperatingExpenseEntry
        {
            Id = request.Id ?? 0,
            ShopId = request.ShopId.Trim(),
            EntryDate = DateTime.SpecifyKind(request.EntryDate.Date, DateTimeKind.Utc),
            DocumentNo = (request.DocumentNo ?? string.Empty).Trim(),
            Category = request.Category,
            Description = request.Description.Trim(),
            Amount = Math.Round(request.Amount, 2, MidpointRounding.AwayFromZero),
            Currency = currency,
            ExchangeRateToTry = Math.Round(rate, 4, MidpointRounding.AwayFromZero),
            AmountTry = amountTry,
            IsTaxDeductible = request.IsTaxDeductible,
            Frequency = request.Frequency,
            RecurringEndDate = request.RecurringEndDate.HasValue
                ? DateTime.SpecifyKind(request.RecurringEndDate.Value.Date, DateTimeKind.Utc)
                : null,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var updated = await repo.UpdateOperatingExpenseAsync(entity, cancellationToken);
            if (updated is null) return ServiceResponse.CreateNotFound("Gider fişi bulunamadı.");
            return ServiceResponse.CreateSuccess("Gider fişi güncellendi.", updated);
        }

        entity.CreatedAt = DateTime.UtcNow;
        var created = await repo.AddOperatingExpenseAsync(entity, cancellationToken);
        return ServiceResponse.CreateSuccess("Gider fişi eklendi.", created);
    }
}

public sealed record DeleteOperatingExpenseCommand(long Id, string ShopId) : IRequest<ServiceResponse>;

public sealed class DeleteOperatingExpenseHandler(IAccountingRepository repo) : IRequestHandler<DeleteOperatingExpenseCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(DeleteOperatingExpenseCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        var deleted = await repo.DeleteOperatingExpenseAsync(request.Id, request.ShopId, cancellationToken);
        return deleted
            ? ServiceResponse.CreateSuccess("Gider fişi silindi.", true)
            : ServiceResponse.CreateNotFound("Gider fişi bulunamadı.");
    }
}

// =========================================================================
// 3. Duran Varlık & Amortisman CRUD Commands & Query
// =========================================================================

public sealed record GetFixedAssetsQuery(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetFixedAssetsHandler(IAccountingRepository repo) : IRequestHandler<GetFixedAssetsQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetFixedAssetsQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        var list = await repo.GetFixedAssetsAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, list);
    }
}

public sealed record UpsertFixedAssetCommand(
    long? Id,
    string ShopId,
    string AssetName,
    string AssetAccountCode,
    OperatingExpenseCategory ExpenseTargetCategory,
    DateTime AcquisitionDate,
    decimal AcquisitionCostTry,
    decimal SalvageValueTry,
    int UsefulLifeYears,
    DepreciationMethod Method,
    bool IsActive) : IRequest<ServiceResponse>;

public sealed class UpsertFixedAssetHandler(IAccountingRepository repo) : IRequestHandler<UpsertFixedAssetCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpsertFixedAssetCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        if (string.IsNullOrWhiteSpace(request.AssetName))
            return ServiceResponse.CreateError("Varlık adı zorunludur.", HttpStatusCode.BadRequest);
        if (request.AcquisitionCostTry <= 0m)
            return ServiceResponse.CreateError("Alış bedeli 0'dan büyük olmalıdır.", HttpStatusCode.BadRequest);
        if (request.UsefulLifeYears is <= 0 or > 50)
            return ServiceResponse.CreateError("Ekonomik ömür 1–50 yıl arasında olmalıdır.", HttpStatusCode.BadRequest);
        if (request.SalvageValueTry < 0m || request.SalvageValueTry >= request.AcquisitionCostTry)
            return ServiceResponse.CreateError("Hurda değer 0 ile alış bedeli arasında olmalıdır.", HttpStatusCode.BadRequest);

        var code = string.IsNullOrWhiteSpace(request.AssetAccountCode) ? "253" : request.AssetAccountCode.Trim();
        var entity = new FixedAssetItem
        {
            Id = request.Id ?? 0,
            ShopId = request.ShopId.Trim(),
            AssetName = request.AssetName.Trim(),
            AssetAccountCode = code is "253" or "254" or "255" ? code : "255",
            ExpenseTargetCategory = request.ExpenseTargetCategory,
            AcquisitionDate = DateTime.SpecifyKind(request.AcquisitionDate.Date, DateTimeKind.Utc),
            AcquisitionCostTry = Math.Round(request.AcquisitionCostTry, 2, MidpointRounding.AwayFromZero),
            SalvageValueTry = Math.Round(request.SalvageValueTry, 2, MidpointRounding.AwayFromZero),
            UsefulLifeYears = request.UsefulLifeYears,
            Method = request.Method,
            IsActive = request.IsActive,
            UpdatedAt = DateTime.UtcNow
        };

        if (request.Id.HasValue && request.Id.Value > 0)
        {
            var updated = await repo.UpdateFixedAssetAsync(entity, cancellationToken);
            if (updated is null) return ServiceResponse.CreateNotFound("Duran varlık bulunamadı.");
            return ServiceResponse.CreateSuccess("Duran varlık güncellendi.", updated);
        }

        entity.CreatedAt = DateTime.UtcNow;
        var created = await repo.AddFixedAssetAsync(entity, cancellationToken);
        return ServiceResponse.CreateSuccess("Duran varlık kaydedildi.", created);
    }
}

public sealed record DeleteFixedAssetCommand(long Id, string ShopId) : IRequest<ServiceResponse>;

public sealed class DeleteFixedAssetHandler(IAccountingRepository repo) : IRequestHandler<DeleteFixedAssetCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(DeleteFixedAssetCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        var deleted = await repo.DeleteFixedAssetAsync(request.Id, request.ShopId, cancellationToken);
        return deleted
            ? ServiceResponse.CreateSuccess("Duran varlık silindi.", true)
            : ServiceResponse.CreateNotFound("Duran varlık bulunamadı.");
    }
}

// =========================================================================
// 4. Muhasebe Politikası & Aralıklı Envanter Commands/Queries
// =========================================================================

public sealed record GetAccountingProfileQuery(string ShopId) : IRequest<ServiceResponse>;

public sealed class GetAccountingProfileHandler(IAccountingRepository repo) : IRequestHandler<GetAccountingProfileQuery, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(GetAccountingProfileQuery request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        var profile = await repo.GetOrDefaultProfileAsync(request.ShopId, cancellationToken);
        return ServiceResponse.CreateSuccess(ResponseMessage.Succeed, profile);
    }
}

public sealed record UpsertAccountingProfileCommand(
    string ShopId,
    CommissionAccountTarget CommissionTarget,
    ShippingPackagingCostTarget ShippingTarget,
    CanceledOrderCostTarget CanceledCostTarget,
    InventoryValuationMethod InventoryMethod,
    TaxRegimeType TaxRegime,
    bool YoungEntrepreneurModeEnabled,
    decimal YoungEntrepreneurAnnualLimitTry,
    decimal YoungEntrepreneurPriorUsedTry,
    decimal EExportExemptionRatePct,
    decimal CustomOrCorporateTaxRatePct,
    string? CustomGvkBracketsJson,
    bool IncludeFxSpreadAndExchangeDifference) : IRequest<ServiceResponse>;

public sealed class UpsertAccountingProfileHandler(IAccountingRepository repo) : IRequestHandler<UpsertAccountingProfileCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpsertAccountingProfileCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        if (request.EExportExemptionRatePct is < 0m or > 100m)
            return ServiceResponse.CreateError("E-İhracat istisna oranı %0–%100 arasında olmalıdır.", HttpStatusCode.BadRequest);

        var profile = new ShopAccountingProfile
        {
            ShopId = request.ShopId.Trim(),
            CommissionTarget = request.CommissionTarget,
            ShippingTarget = request.ShippingTarget,
            CanceledCostTarget = request.CanceledCostTarget,
            InventoryMethod = request.InventoryMethod,
            TaxRegime = request.TaxRegime,
            YoungEntrepreneurModeEnabled = request.YoungEntrepreneurModeEnabled,
            YoungEntrepreneurAnnualLimitTry = Math.Max(0m, request.YoungEntrepreneurAnnualLimitTry),
            YoungEntrepreneurPriorUsedTry = Math.Max(0m, request.YoungEntrepreneurPriorUsedTry),
            EExportExemptionRatePct = request.EExportExemptionRatePct,
            CustomOrCorporateTaxRatePct = Math.Clamp(request.CustomOrCorporateTaxRatePct, 0m, 100m),
            CustomGvkBracketsJson = string.IsNullOrWhiteSpace(request.CustomGvkBracketsJson) ? null : request.CustomGvkBracketsJson.Trim(),
            IncludeFxSpreadAndExchangeDifference = request.IncludeFxSpreadAndExchangeDifference,
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await repo.UpsertProfileAsync(profile, cancellationToken);
        return ServiceResponse.CreateSuccess("Muhasebe ve vergi profili kaydedildi.", saved);
    }
}

public sealed record UpsertInventoryAdjustmentCommand(
    string ShopId,
    int Year,
    int Month,
    decimal OpeningInventoryTry,
    decimal GrossPurchasesTry,
    decimal PurchaseReturnsAndDiscountsTry,
    decimal ClosingInventoryTry,
    string? Notes) : IRequest<ServiceResponse>;

public sealed class UpsertInventoryAdjustmentHandler(IAccountingRepository repo) : IRequestHandler<UpsertInventoryAdjustmentCommand, ServiceResponse>
{
    public async Task<ServiceResponse> Handle(UpsertInventoryAdjustmentCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.ShopId))
            return ServiceResponse.CreateError("ShopId zorunludur.", HttpStatusCode.BadRequest);
        if (request.Year is < 2020 or > 2100 || request.Month is < 1 or > 12)
            return ServiceResponse.CreateError("Geçerli yıl ve ay (1–12) giriniz.", HttpStatusCode.BadRequest);

        var adj = new InventoryPeriodAdjustment
        {
            ShopId = request.ShopId.Trim(),
            Year = request.Year,
            Month = request.Month,
            OpeningInventoryTry = Math.Max(0m, Math.Round(request.OpeningInventoryTry, 2, MidpointRounding.AwayFromZero)),
            GrossPurchasesTry = Math.Max(0m, Math.Round(request.GrossPurchasesTry, 2, MidpointRounding.AwayFromZero)),
            PurchaseReturnsAndDiscountsTry = Math.Max(0m, Math.Round(request.PurchaseReturnsAndDiscountsTry, 2, MidpointRounding.AwayFromZero)),
            ClosingInventoryTry = Math.Max(0m, Math.Round(request.ClosingInventoryTry, 2, MidpointRounding.AwayFromZero)),
            Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
            UpdatedAt = DateTime.UtcNow
        };

        var saved = await repo.UpsertInventoryAdjustmentAsync(adj, cancellationToken);
        return ServiceResponse.CreateSuccess("Aralıklı envanter dönem sayımı kaydedildi.", saved);
    }
}
