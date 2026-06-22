namespace EtsyMarketPlace.Application.ShopPerformance;

public sealed record OwnShopProfile(long ShopId, string ShopName, string ShopUrl);

public sealed record OwnShopTransaction(
    long ListingId,
    string Title,
    int Quantity,
    decimal Amount,
    string CurrencyCode);

public sealed record OwnShopReceipt(
    long ReceiptId,
    DateTimeOffset CreatedAt,
    bool IsPaid,
    bool IsCanceled,
    decimal GrandTotal,
    string CurrencyCode,
    IReadOnlyList<OwnShopTransaction> Transactions);

public sealed record OwnShopPerformanceSource(
    OwnShopProfile Shop,
    IReadOnlyList<OwnShopReceipt> Receipts);

public sealed record ProductPerformance(
    long ListingId,
    string Title,
    int OrderCount,
    int UnitsSold,
    decimal Revenue,
    string CurrencyCode);

public sealed class ShopPerformanceReport
{
    public required OwnShopProfile Shop { get; init; }
    public required DateTimeOffset PeriodStart { get; init; }
    public required DateTimeOffset PeriodEnd { get; init; }
    public required string CurrencyCode { get; init; }
    public int OrderCount { get; init; }
    public int UnitsSold { get; init; }
    public decimal GrossRevenue { get; init; }
    public decimal AverageOrderValue { get; init; }
    public required IReadOnlyList<ProductPerformance> Products { get; init; }
}

public sealed record PerformanceMetric(decimal Current, decimal Previous)
{
    public decimal Difference => Current - Previous;
    public decimal? PercentageChange => Previous == 0 ? null : Difference * 100m / Previous;
}

public sealed record ProductPerformanceComparison(
    long ListingId,
    string Title,
    int CurrentOrderCount,
    int PreviousOrderCount,
    int CurrentUnitsSold,
    int PreviousUnitsSold,
    decimal CurrentRevenue,
    decimal PreviousRevenue,
    string CurrencyCode)
{
    public int UnitDifference => CurrentUnitsSold - PreviousUnitsSold;
    public decimal RevenueDifference => CurrentRevenue - PreviousRevenue;
}

public sealed class ShopPerformanceComparison
{
    public required ShopPerformanceReport Current { get; init; }
    public required ShopPerformanceReport Previous { get; init; }
    public required PerformanceMetric Orders { get; init; }
    public required PerformanceMetric Units { get; init; }
    public required PerformanceMetric Revenue { get; init; }
    public required PerformanceMetric AverageOrder { get; init; }
    public required IReadOnlyList<ProductPerformanceComparison> Products { get; init; }
}

public interface IOwnShopGateway
{
    Task<OwnShopPerformanceSource> GetPerformanceSourceAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default);
}
