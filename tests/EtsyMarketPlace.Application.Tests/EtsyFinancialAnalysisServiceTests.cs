namespace EtsyMarketPlace.Application.Tests;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Application.EtsyIntegration;
using EtsyMarketPlace.Application.ListingOptimization;
using Xunit;

public sealed class EtsyFinancialAnalysisServiceTests
{
    [Fact]
    public async Task AnalyzeAsync_ReportsLowMarginAndMissingCosts()
    {
        var reporting = new StubReportingService
        {
            Performance = new FinancialPerformance(
                DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, "TRY",
                1000m, 100m, 100m, 100m, 450m, 50m, 0m, 199m, 19.9m),
            Alerts = [new EtsyOrderCostAlert("shop", "order", DateTimeOffset.UtcNow, "TRY", 100m, null, null, "missing")]
        };

        var result = await new EtsyFinancialAnalysisService(reporting, new StubLedgerReportService())
            .AnalyzeAsync("shop", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow);

        Assert.Equal(19.9m, result.ProfitMarginPercent);
        Assert.Equal(80m, result.ExpenseRatioPercent);
        Assert.Contains(result.Insights, insight => insight.Code == "LOW_PROFIT_MARGIN");
        Assert.Contains(result.Insights, insight => insight.Code == "OPEN_ORDER_COSTS");
        Assert.Contains(result.Recommendations, recommendation => recommendation.Priority == "high");
    }

    [Fact]
    public async Task AnalyzeAsync_ReturnsHealthyInsightWhenNoThresholdIsBreached()
    {
        var reporting = new StubReportingService
        {
            Performance = new FinancialPerformance(
                DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, "TRY",
                1000m, 50m, 20m, 20m, 100m, 30m, 0m, 780m, 78m)
        };

        var result = await new EtsyFinancialAnalysisService(reporting, new StubLedgerReportService())
            .AnalyzeAsync("shop", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow);

        Assert.Contains(result.Insights, insight => insight.Code == "HEALTHY_FINANCIALS");
        Assert.Empty(result.Recommendations);
    }

    [Fact]
    public async Task GenerateAiForecastAsync_UsesLiveLlmResponseWhenApiKeyIsAvailable()
    {
        var reporting = new StubReportingService
        {
            Performance = new FinancialPerformance(
                DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, "USD",
                545.45m, 44.90m, 2.75m, 32.48m, 100m, 30m, 0m, 335.32m, 61.4m)
        };
        var llm = new StubLlmClient
        {
            JsonResponse = """
            {
              "expected_gross_usd": 650.00,
              "min_gross_usd": 550.00,
              "max_gross_usd": 780.00,
              "expected_net_profit_usd": 310.00,
              "expected_margin_pct": 47.7,
              "estimated_orders": 11,
              "monthly_growth_pct": 19.2,
              "executive_summary": "Canlı DeepSeek CFO analizi tamamlandı.",
              "product_profitability_analysis": "Lider ürün yüksek marjla katkı sağlıyor.",
              "ad_spend_and_roas_analysis": "Reklam harcamaları verimli.",
              "refund_and_cost_alert_analysis": "İade riski düşük.",
              "action_plan": ["Maliyetleri güncel tutun", "Lider üründe varyasyon artırın"]
            }
            """
        };

        var service = new EtsyFinancialAnalysisService(reporting, new StubLedgerReportService(), llm);
        var cmd = new AiFinancialForecastCommand(
            ShopId: "53236321",
            StartDate: DateTimeOffset.UtcNow.AddDays(-30),
            EndDate: DateTimeOffset.UtcNow,
            Provider: "DeepSeek",
            Model: "DeepSeek-V4-Flash",
            ApiKey: "sk-test-deepseek-key-123456",
            ExchangeRate: 50.0m,
            CurrentOrderCount: 9,
            TopProductTitle: "Custom 3D Art Lamp");

        var result = await service.GenerateAiForecastAsync(cmd);

        Assert.True(result.UsedAi);
        Assert.Equal("DeepSeek", result.Provider);
        Assert.Equal("DeepSeek-V4-Flash", result.Model);
        Assert.Equal(650.00m, result.ExpectedGrossUsd);
        Assert.Equal(32500.00m, result.ExpectedGrossTry);
        Assert.Equal(310.00m, result.ExpectedNetProfitUsd);
        Assert.Equal(11, result.EstimatedOrders);
        Assert.Contains("Canlı DeepSeek CFO", result.ExecutiveSummary);
        Assert.Equal(2, result.ActionPlan.Count);
    }

    [Fact]
    public async Task GenerateAiForecastAsync_BuildsDataDrivenForecastFromRealStoreMetricsWhenOffline()
    {
        var reporting = new StubReportingService
        {
            Performance = new FinancialPerformance(
                DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow, "USD",
                500m, 50m, 10m, 25m, 80m, 40m, 35m, 260m, 52m)
        };

        var service = new EtsyFinancialAnalysisService(reporting, new StubLedgerReportService());
        var cmd = new AiFinancialForecastCommand(
            ShopId: "53236321",
            StartDate: DateTimeOffset.UtcNow.AddDays(-30),
            EndDate: DateTimeOffset.UtcNow,
            Provider: "Offline",
            ExchangeRate: 50.0m,
            CurrentOrderCount: 8,
            MissingCostOrderCount: 1,
            MissingCostOrderIds: ["4199001122"],
            TopProductTitle: "Geometric Desk Organizer",
            TopProductRevenueUsd: 210m,
            TopProductProfitUsd: 120m);

        var result = await service.GenerateAiForecastAsync(cmd);

        Assert.False(result.UsedAi);
        Assert.True(result.ExpectedGrossUsd > 0m);
        Assert.Contains("Geometric Desk Organizer", result.ProductProfitabilityAnalysis);
        Assert.Contains("#4199001122", result.RefundAndCostAlertAnalysis);
        Assert.NotEmpty(result.ActionPlan);
    }

    private sealed class StubLlmClient : IGeminiListingApiClient
    {
        public string JsonResponse { get; init; } = "{}";
        public Task<(bool Success, string? Content, string? ErrorMessage)> GenerateOptimizationJsonAsync(
            string systemInstruction,
            string userPrompt,
            string model,
            string apiKey,
            CancellationToken cancellationToken = default)
            => Task.FromResult<(bool, string?, string?)>((true, JsonResponse, null));
    }

    private sealed class StubReportingService : IEtsyReportingService
    {
        public FinancialPerformance Performance { get; init; }
        public IReadOnlyList<EtsyOrderCostAlert> Alerts { get; init; } = [];
        public Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) => Task.FromResult<IReadOnlyList<EtsyBankPayout>>([]);
        public Task<FinancialPerformance> GetFinancialPerformanceAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default) => Task.FromResult(Performance);
        public Task<IReadOnlyList<EtsyOrderCostAlert>> GetUnfulfilledCostAlertsAsync(string shopId, CancellationToken cancellationToken = default) => Task.FromResult(Alerts);
        public Task<DailyShopBrief> GetDailyShopBriefAsync(string shopId, DateTimeOffset date, CancellationToken cancellationToken = default) => throw new NotSupportedException();
    }

    private sealed class StubLedgerReportService : IEtsyLedgerReportService
    {
        public Task<EtsyLedgerLiveResult> GetLiveReportAsync(string shopId, DateTimeOffset startDate, DateTimeOffset endDate, CancellationToken cancellationToken = default)
            => Task.FromResult(new EtsyLedgerLiveResult(
                false,
                null,
                "test-stub",
                DateTimeOffset.UtcNow,
                new FinancialPerformance(startDate, endDate, "TRY", 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m, 0m),
                Array.Empty<EtsyLedgerDailySummary>(),
                0));
    }
}
