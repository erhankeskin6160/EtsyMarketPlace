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
    decimal NetProfitMargin);

public sealed record DailyShopBrief(
    DateTimeOffset Date,
    int HealthScore,
    string HealthStatus,
    int PendingShipments,
    decimal DailyGrossSales,
    decimal DailyNetProfit,
    decimal BankPayouts,
    int UnfulfilledCostAlertCount);

public sealed record EtsySyncResult(
    string ShopId,
    DateTimeOffset StartedAt,
    DateTimeOffset CompletedAt,
    int PayoutCount,
    int TransactionCount,
    int OrderCount,
    bool Succeeded,
    string? ErrorMessage);
