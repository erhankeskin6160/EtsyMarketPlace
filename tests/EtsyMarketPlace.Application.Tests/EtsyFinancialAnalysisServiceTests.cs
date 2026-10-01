namespace EtsyMarketPlace.Application.Tests;

using EtsyMarketPlace.Application.EtsyIntegration;
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

        var result = await new EtsyFinancialAnalysisService(reporting)
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

        var result = await new EtsyFinancialAnalysisService(reporting)
            .AnalyzeAsync("shop", DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow);

        Assert.Contains(result.Insights, insight => insight.Code == "HEALTHY_FINANCIALS");
        Assert.Empty(result.Recommendations);
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
}
