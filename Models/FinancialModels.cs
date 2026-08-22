namespace SimilarProductsWinForms.Models;

/// <summary>
/// Etsy Ledger Entry (Defter Kaydı) — /v3/application/shops/{shop_id}/payment-account/ledger-entries
/// </summary>
internal sealed record LedgerEntry(
    long EntryId,
    string Type,         // "sale" | "refund" | "listing_fee" | "transaction_fee" | "shipping" | "ad_fee" | "payment"
    decimal Amount,      // brüt miktar (cent olarak gelir, 100'e bölünerek kullanılır)
    decimal NetAmount,   // net miktar
    string Currency,     // "USD" | "EUR" | "TRY" ...
    string Description,
    DateTimeOffset CreatedAt
);

/// <summary>
/// Aylık finansal özet
/// </summary>
internal sealed record MonthlyFinancial(
    int Year,
    int Month,
    decimal GrossSales,
    decimal Refunds,
    decimal ListingFees,
    decimal TransactionFees,
    decimal AdFees,
    decimal ShippingCredits,
    decimal PaymentProcessingFees,
    decimal OtherFees
)
{
    public decimal TotalFees => ListingFees + TransactionFees + AdFees + PaymentProcessingFees + OtherFees;
    public decimal NetIncome => GrossSales - Refunds - TotalFees + ShippingCredits;
    public string MonthLabel => $"{Year}/{Month:D2}";
    public string MonthName => new DateTime(Year, Month, 1).ToString("MMM yyyy", System.Globalization.CultureInfo.GetCultureInfo("tr-TR"));
}

/// <summary>
/// Finansal rapor özeti
/// </summary>
internal sealed record FinancialReport(
    List<LedgerEntry> Entries,
    List<MonthlyFinancial> Monthly,
    decimal TotalGross,
    decimal TotalRefunds,
    decimal TotalFees,
    decimal TotalAdFees,
    decimal TotalShippingCredits,
    decimal TotalNet,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd
)
{
    public decimal FeeRatePct => TotalGross == 0 ? 0 : Math.Round(TotalFees / TotalGross * 100, 1);
    public decimal RefundRatePct => TotalGross == 0 ? 0 : Math.Round(TotalRefunds / TotalGross * 100, 1);
    public decimal AdSpendPct => TotalGross == 0 ? 0 : Math.Round(TotalAdFees / TotalGross * 100, 1);
    public int TransactionCount => Entries.Count(e => e.Type == "sale");
    public decimal AverageOrderValue => TransactionCount == 0 ? 0 : Math.Round(TotalGross / TransactionCount, 2);

    public static FinancialReport Empty => new(
        [], [], 0, 0, 0, 0, 0, 0, "USD",
        DateTimeOffset.UtcNow.AddDays(-30), DateTimeOffset.UtcNow
    );
}

/// <summary>
/// Tarih aralığı filtresi
/// </summary>
internal enum DateRangePreset
{
    Last7Days,
    Last30Days,
    Last90Days,
    ThisMonth,
    LastMonth,
    ThisYear,
    Custom
}
