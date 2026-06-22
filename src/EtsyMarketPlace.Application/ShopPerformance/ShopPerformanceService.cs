namespace EtsyMarketPlace.Application.ShopPerformance;

public sealed class ShopPerformanceService(IOwnShopGateway gateway)
{
    public async Task<ShopPerformanceReport> GetReportAsync(
        DateTimeOffset periodStart,
        DateTimeOffset periodEnd,
        CancellationToken cancellationToken = default)
    {
        if (periodEnd <= periodStart)
        {
            throw new ArgumentException("Donem bitisi, baslangictan sonra olmalidir.");
        }

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
}
