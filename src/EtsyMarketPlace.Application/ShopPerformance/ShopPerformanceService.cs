namespace EtsyMarketPlace.Application.ShopPerformance;

public sealed class ShopPerformanceService(IOwnShopGateway gateway)
{
    public async Task<ShopPerformanceComparison> GetComparisonAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);
        var previousEnd = periodStart.AddTicks(-1);
        var previousStart = previousEnd - (periodEnd - periodStart);
        var current = await GetReportAsync(periodStart, periodEnd, cancellationToken);
        var previous = await GetReportAsync(previousStart, previousEnd, cancellationToken);
        return BuildComparison(current, previous);
    }

    public async Task<ShopPerformanceReport> GetReportAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        ValidatePeriod(periodStart, periodEnd);

        var source = await gateway.GetPerformanceSourceAsync(periodStart, periodEnd, cancellationToken);
        return Build(source, periodStart, periodEnd);
    }

    public static ShopPerformanceReport Build(
        OwnShopPerformanceSource source,
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd)
    {
        var receipts = source.Receipts
            .Where(item => item.IsPaid && !item.IsCanceled)
            .Where(item => item.CreatedAt >= periodStart && item.CreatedAt <= periodEnd)
            .ToList();
        var currency = receipts.Select(item => item.CurrencyCode)
            .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item)) ?? "USD";
        var revenue = receipts.Where(item => item.CurrencyCode == currency).Sum(item => item.GrandTotal);

        var products = receipts
            .SelectMany(receipt => receipt.Transactions.Select(transaction => (receipt.ReceiptId, Transaction: transaction)))
            .GroupBy(item => new { item.Transaction.ListingId, item.Transaction.Title, item.Transaction.CurrencyCode })
            .Select(group => new ProductPerformance(
                group.Key.ListingId,
                group.Key.Title,
                group.Select(item => item.ReceiptId).Distinct().Count(),
                group.Sum(item => item.Transaction.Quantity),
                group.Sum(item => item.Transaction.Amount * item.Transaction.Quantity),
                group.Key.CurrencyCode))
            .OrderByDescending(item => item.UnitsSold)
            .ThenByDescending(item => item.Revenue)
            .ToList();

        return new ShopPerformanceReport
        {
            Shop = source.Shop,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            CurrencyCode = currency,
            OrderCount = receipts.Count,
            UnitsSold = receipts.SelectMany(item => item.Transactions).Sum(item => item.Quantity),
            GrossRevenue = revenue,
            AverageOrderValue = receipts.Count == 0 ? 0 : revenue / receipts.Count,
            Products = products,
        };
    }

    public static ShopPerformanceComparison BuildComparison(
        ShopPerformanceReport current,
        ShopPerformanceReport previous)
    {
        if (current.Shop.ShopId != previous.Shop.ShopId)
        {
            throw new InvalidOperationException("Farkli magazalara ait donemler karsilastirilamaz.");
        }

        var previousByListing = previous.Products.ToDictionary(item => item.ListingId);
        var currentByListing = current.Products.ToDictionary(item => item.ListingId);
        var listingIds = currentByListing.Keys.Union(previousByListing.Keys);
        var products = listingIds.Select(listingId =>
        {
            currentByListing.TryGetValue(listingId, out var currentProduct);
            previousByListing.TryGetValue(listingId, out var previousProduct);
            return new ProductPerformanceComparison(
                listingId,
                currentProduct?.Title ?? previousProduct?.Title ?? $"Listing {listingId}",
                currentProduct?.OrderCount ?? 0,
                previousProduct?.OrderCount ?? 0,
                currentProduct?.UnitsSold ?? 0,
                previousProduct?.UnitsSold ?? 0,
                currentProduct?.Revenue ?? 0,
                previousProduct?.Revenue ?? 0,
                currentProduct?.CurrencyCode ?? previousProduct?.CurrencyCode ?? current.CurrencyCode);
        })
        .OrderByDescending(item => item.CurrentUnitsSold)
        .ThenByDescending(item => item.UnitDifference)
        .ThenByDescending(item => item.CurrentRevenue)
        .ToList();

        return new ShopPerformanceComparison
        {
            Current = current,
            Previous = previous,
            Orders = new PerformanceMetric(current.OrderCount, previous.OrderCount),
            Units = new PerformanceMetric(current.UnitsSold, previous.UnitsSold),
            Revenue = new PerformanceMetric(current.GrossRevenue, previous.GrossRevenue),
            AverageOrder = new PerformanceMetric(current.AverageOrderValue, previous.AverageOrderValue),
            Products = products,
        };
    }

    private static void ValidatePeriod(DateTimeOffset periodStart, DateTimeOffset periodEnd)
    {
        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("Donem bitisi, baslangictan sonra olmalidir.");
        }
    }
}
