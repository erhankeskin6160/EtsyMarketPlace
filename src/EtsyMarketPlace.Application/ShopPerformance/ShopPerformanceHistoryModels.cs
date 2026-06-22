namespace EtsyMarketPlace.Application.ShopPerformance;

public sealed record ShopPerformanceHistoryRecord(
    long Id,
    OwnShopProfile Shop,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    DateTimeOffset CapturedAt,
    string CurrencyCode,
    int OrderCount,
    int UnitsSold,
    decimal GrossRevenue,
    decimal AverageOrderValue,
    IReadOnlyList<ProductPerformance> Products);

public interface IShopPerformanceHistoryRepository
{
    Task InitializeAsync(CancellationToken cancellationToken = default);

    Task<ShopPerformanceHistoryRecord> SaveDailyAsync(
        ShopPerformanceReport report,
        DateTimeOffset capturedAt,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ShopPerformanceHistoryRecord>> GetByShopAsync(
        long shopId,
        CancellationToken cancellationToken = default);
}

public sealed class ShopPerformanceHistoryService(IShopPerformanceHistoryRepository repository)
{
    public Task InitializeAsync(CancellationToken cancellationToken = default) =>
        repository.InitializeAsync(cancellationToken);

    public Task<ShopPerformanceHistoryRecord> SaveAsync(
        ShopPerformanceReport report,
        DateTimeOffset? capturedAt = null,
        CancellationToken cancellationToken = default) =>
        repository.SaveDailyAsync(report, capturedAt ?? DateTimeOffset.Now, cancellationToken);

    public Task<IReadOnlyList<ShopPerformanceHistoryRecord>> GetHistoryAsync(
        long shopId,
        CancellationToken cancellationToken = default) =>
        repository.GetByShopAsync(shopId, cancellationToken);
}
