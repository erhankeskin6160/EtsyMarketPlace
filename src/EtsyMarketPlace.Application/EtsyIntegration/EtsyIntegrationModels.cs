namespace EtsyMarketPlace.Application.EtsyIntegration;

public sealed record EtsyOAuthToken(
    string AccessToken,
    string RefreshToken,
    DateTimeOffset AccessTokenExpiresAt,
    string TokenType = "Bearer");

public sealed record EtsyShopConnection(
    string ShopId,
    string ShopName,
    string UserId,
    DateTimeOffset? LastSynchronizedAt,
    bool IsActive);

public sealed record EtsyBankPayout(
    string ShopId,
    string ReferenceId,
    DateTimeOffset OccurredAt,
    decimal Amount,
    string Currency,
    decimal? ExchangeRateToTry,
    string Status,
    string Description);

public sealed record EtsyFinancialTransaction(
    string ShopId,
    string ReferenceId,
    DateTimeOffset OccurredAt,
    decimal GrossSales,
    decimal PlatformFees,
    decimal InternalAdsCost,
    decimal ExternalAdsCost,
    decimal ProductCost,
    decimal ShippingCost,
    decimal Refunds,
    string Currency);

public sealed record EtsyOrderCostAlert(
    string ShopId,
    string OrderId,
    DateTimeOffset CreatedAt,
    string Currency,
    decimal OrderTotal,
    decimal? ProductCost,
    decimal? ShippingCost,
    string Reason);

public sealed record FinancialPerformance(
    DateTimeOffset StartDate,
    DateTimeOffset EndDate,
    string Currency,
    decimal GrossSales,
    decimal PlatformFees,
    decimal InternalAdsCost,
    decimal ExternalAdsCost,
    decimal ProductCosts,
    decimal ShippingCosts,
    decimal Refunds,
    decimal NetProfit,
    decimal NetProfitMargin,
    decimal? GrossSalesTRY = null,
    decimal? NetProfitTRY = null,
    decimal? OrderGrossSalesUSD = null,
    decimal? OrderGrossSalesTRY = null,
    decimal? OrderNetProfitTRY = null,
    decimal? ExchangeRateUsed = null);

public sealed record DailyShopBrief(
    DateTimeOffset Date,
    int HealthScore,
    string HealthStatus,
    int PendingShipments,
    decimal DailyGrossSales,
    decimal DailyNetProfit,
    decimal BankPayouts,
    int UnfulfilledCostAlertCount,
    decimal? DailyGrossSalesTRY = null,
    decimal? DailyNetProfitTRY = null,
    decimal? ExchangeRateUsed = null);

public sealed record EtsySyncResult(
    string ShopId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int PayoutCount,
    int TransactionCount,
    int OrderCount,
    bool Succeeded,
    string? ErrorMessage);

public sealed record EtsyMonthlyOrderSummary(
    string ShopId,
    string YearMonth,
    int OrderCount,
    int UnitsSold,
    decimal GrossRevenue,
    decimal AvgOrderValue,
    int ShippedOrders,
    int UnfulfilledOrders,
    string Currency);

public sealed record EtsyListingTrafficRecord(
    string ShopId,
    long ListingId,
    string SnapshotDate,
    string Title,
    int Views,
    int Favorites,
    int ViewsToday,
    int FavoritesToday,
    int UnitsSoldMonth,
    decimal RevenueMonth,
    decimal ConversionRate,
    string ImageUrl,
    string ListingUrl);

public sealed record EtsyChartSnapshot(
    string ShopId,
    string ChartType,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    string ImagePngBase64,
    DateTimeOffset UpdatedAt);
