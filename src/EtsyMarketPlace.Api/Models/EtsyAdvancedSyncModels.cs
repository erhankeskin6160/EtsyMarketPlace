namespace EtsyMarketPlace.Api.Models;

using EtsyMarketPlace.Application.EtsyIntegration;

public sealed record EtsyMonthlyOrderImportRequest(
    string ShopId,
    List<EtsyMonthlyOrderSummary> Summaries);

public sealed record EtsyListingTrafficImportRequest(
    string ShopId,
    List<EtsyListingTrafficRecord> Records);

public sealed record EtsyChartUploadRequest(
    string ShopId,
    string ChartType,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string ImagePngBase64);

public sealed record EtsyAllDataImportRequest(
    string ShopId,
    List<EtsyFinancialTransaction>? Transactions,
    List<EtsyBankPayout>? Payouts,
    List<EtsyOrderCostAlert>? OrderAlerts,
    List<EtsyMonthlyOrderSummary>? MonthlyOrders,
    List<EtsyListingTrafficRecord>? TrafficRecords,
    List<EtsyChartUploadRequest>? Charts,
    DateTimeOffset? PeriodStart,
    DateTimeOffset? PeriodEnd);
