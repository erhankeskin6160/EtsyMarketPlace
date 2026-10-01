namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed record FinancialAnalysisSnapshot(
    string ShopId,
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    DateTimeOffset GeneratedAt,
    FinancialPerformance Performance,
    IReadOnlyList<EtsyBankPayout> Payouts,
    IReadOnlyList<EtsyFinancialTransaction> Transactions,
    IReadOnlyList<EtsyOrderCostAlert> OrderAlerts);

public sealed record FinancialAnalysisResult(
    FinancialAnalysisSnapshot Snapshot,
    decimal RevenueChangePercent,
    decimal ExpenseRatioPercent,
    decimal ProfitMarginPercent,
    IReadOnlyList<FinancialAnalysisInsight> Insights,
    IReadOnlyList<FinancialAnalysisRecommendation> Recommendations);

public sealed record FinancialAnalysisInsight(
    string Severity,
    string Code,
    string Title,
    string Description,
    decimal? Value = null,
    string? Unit = null);

public sealed record FinancialAnalysisRecommendation(
    string Priority,
    string Title,
    string Description);
