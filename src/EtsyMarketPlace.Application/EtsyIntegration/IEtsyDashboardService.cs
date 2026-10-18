namespace EtsyMarketPlace.Application.EtsyIntegration;

using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using EtsyMarketPlace.Domain.Banking;

public record RecentOrdersDashboardResult(
    IReadOnlyList<RecentOrderRowDto> Orders,
    int Count,
    bool LedgerOk,
    string? LedgerWarning);

public record RecentOrderRowDto(
    string Date,
    string ReceiptId,
    string Title,
    int Quantity,
    decimal TotalUsd,
    decimal NetProfitUsd,
    bool HasCost);

public record OrderFinancialsDashboardResult(
    IReadOnlyList<EtsyOrderFinancialDetail> Orders,
    int Count,
    decimal TotalOrderProfitUsd,
    decimal TotalOrderProfitTry,
    decimal ExchangeRateUsed,
    bool LedgerOk,
    string? LedgerWarning);

public record DailySeriesDashboardResult(
    string Month,
    string[] Labels,
    decimal[] GrossSales,
    decimal[] NetProfit,
    string? TopProductTitle,
    decimal TopProductRevenueUsd,
    int OrderCount,
    bool LedgerOk,
    string? LedgerWarning,
    string Source);

public record LiveFinancialPerformanceResult(
    decimal GrossSales,
    decimal PlatformFees,
    decimal InternalAdsCost,
    decimal ExternalAdsCost,
    decimal ProductCosts,
    decimal ShippingCosts,
    decimal Refunds,
    decimal NetProfit,
    decimal NetProfitMargin,
    decimal? GrossSalesTRY,
    decimal? NetProfitTRY,
    decimal? OrderGrossSalesUSD,
    decimal? OrderGrossSalesTRY,
    decimal? OrderNetProfitTRY,
    decimal? ExchangeRateUsed,
    int ReceiptCount,
    bool LedgerOk,
    string? LedgerWarning,
    string Source,
    DateTimeOffset GeneratedAt,
    decimal? BankPayoutsUSD = null,
    decimal? BankPayoutsTRY = null);

public record FinancialSummaryDashboardResult(
    string Period,
    decimal GrossSales,
    decimal EtsyFees,
    decimal NetRevenue,
    decimal ProductCosts,
    decimal RealNetProfit,
    decimal BankPayoutsTotal);

public interface IEtsyDashboardService
{
    Task<LiveFinancialPerformanceResult> GetFinancialPerformanceAsync(
        string shopId,
        string period,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<EtsyBankPayout>> GetBankPayoutsAsync(
        string shopId,
        DateTimeOffset startDate,
        DateTimeOffset endDate,
        CancellationToken cancellationToken = default);

    Task<DailyShopBrief> GetDailyShopBriefAsync(
        string shopId,
        DateTimeOffset? date,
        CancellationToken cancellationToken = default);

    Task<RecentOrdersDashboardResult> GetRecentOrdersAsync(
        string shopId,
        int days,
        int limit,
        CancellationToken cancellationToken = default);

    Task<OrderFinancialsDashboardResult> GetOrderFinancialsAsync(
        string shopId,
        string period,
        DateTimeOffset? startDate,
        DateTimeOffset? endDate,
        CancellationToken cancellationToken = default);

    Task<DailySeriesDashboardResult> GetDailySeriesAsync(
        string shopId,
        string? month,
        CancellationToken cancellationToken = default);

    Task<FinancialSummaryDashboardResult> GetRealFinancialSummaryAsync(
        string shopId,
        CancellationToken cancellationToken = default);

    Task<MonthlyDepositSummary> GetRealBankDepositsAsync(
        string shopId,
        CancellationToken cancellationToken = default);
}
