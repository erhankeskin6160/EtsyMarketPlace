namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed class EtsyFinancialAnalysisService(IEtsyReportingService reportingService, IEtsyLedgerReportService ledgerReportService) : IEtsyFinancialAnalysisService
{
    public async Task<FinancialAnalysisResult> AnalyzeAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default)
    {
        // Performans: sunucu finans motoru (masaustu paritesi); motor erisilemezse isaretli yedek.
        // Bkz. docs/finans-motoru-ve-parite.md
        var live = await ledgerReportService.GetLiveReportAsync(shopId, startDate, endDate, cancellationToken);
        var performance = live.LedgerOk
            ? live.Performance
            : await reportingService.GetFinancialPerformanceAsync(shopId, startDate, endDate, cancellationToken);
        var payouts = await reportingService.GetBankPayoutsAsync(shopId, startDate, endDate, cancellationToken);
        var alerts = await reportingService.GetUnfulfilledCostAlertsAsync(shopId, cancellationToken);
        var transactions = Array.Empty<EtsyFinancialTransaction>();

        var grossSales = performance.GrossSales;
        var totalExpenses = performance.PlatformFees + performance.InternalAdsCost + performance.ExternalAdsCost +
                             performance.ProductCosts + performance.ShippingCosts + performance.Refunds;
        var expenseRatio = grossSales == 0 ? 0 : totalExpenses / grossSales * 100m;
        var profitMargin = grossSales == 0 ? 0 : performance.NetProfit / grossSales * 100m;
        var insights = new List<FinancialAnalysisInsight>();
        var recommendations = new List<FinancialAnalysisRecommendation>();

        if (profitMargin < 20m)
        {
            insights.Add(new("warning", "LOW_PROFIT_MARGIN", "Düşük kâr marjı",
                $"Net kâr marjı %{profitMargin:F1} seviyesinde.", profitMargin, "%"));
            recommendations.Add(new("high", "Kâr marjını inceleyin",
                "Ürün maliyeti, Etsy komisyonu ve reklam giderlerini ürün bazında karşılaştırın."));
        }

        if (grossSales > 0 && performance.InternalAdsCost + performance.ExternalAdsCost > grossSales * 0.15m)
        {
            var adRatio = (performance.InternalAdsCost + performance.ExternalAdsCost) / grossSales * 100m;
            insights.Add(new("warning", "HIGH_AD_COST", "Yüksek reklam yükü",
                $"Reklam giderleri satışların %{adRatio:F1} seviyesinde.", adRatio, "%"));
            recommendations.Add(new("medium", "Reklam kampanyalarını optimize edin",
                "Düşük dönüşümlü kampanyaları ve ürünleri ayırarak reklam bütçesini yeniden dağıtın."));
        }

        if (grossSales > 0 && performance.PlatformFees / grossSales > 0.20m)
        {
            var feeRatio = performance.PlatformFees / grossSales * 100m;
            insights.Add(new("info", "HIGH_PLATFORM_FEES", "Yüksek platform kesintisi",
                $"Platform kesintileri satışların %{feeRatio:F1} seviyesinde.", feeRatio, "%"));
        }

        if (alerts.Count > 0)
        {
            insights.Add(new("warning", "OPEN_ORDER_COSTS", "Maliyeti eksik açık siparişler",
                $"{alerts.Count} açık sipariş için ürün veya kargo maliyeti eksik.", alerts.Count, "sipariş"));
            recommendations.Add(new("high", "Açık sipariş maliyetlerini tamamlayın",
                "Eksik maliyetleri girerek gerçek sipariş kârlılığını güncelleyin."));
        }

        if (insights.Count == 0)
        {
            insights.Add(new("success", "HEALTHY_FINANCIALS", "Finansal görünüm dengeli",
                "Seçilen dönem için kritik bir finansal uyarı tespit edilmedi."));
        }

        return new FinancialAnalysisResult(
            new FinancialAnalysisSnapshot(shopId, startDate, endDate, DateTimeOffset.UtcNow, performance, payouts, transactions, alerts),
            0m,
            expenseRatio,
            profitMargin,
            insights,
            recommendations);
    }
}
