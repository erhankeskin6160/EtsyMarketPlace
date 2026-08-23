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
    DateTimeOffset CreatedAt,
    decimal ExchangeRate = 36.50m // Sipariş/İşlem Günü Kabul Edilen Dolar/TL Kuru
)
{
    public decimal AmountTRY => Math.Round(Amount * ExchangeRate, 2);
    public decimal NetAmountTRY => Math.Round(NetAmount * ExchangeRate, 2);
}

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
/// Ürün Maliyet Kaydı (COGS - Cost of Goods Sold)
/// </summary>
internal sealed record ProductCostEntry(
    string ListingId,
    string Title,
    decimal UnitCost,         // Ürün üretim / hammadde birim maliyeti ($)
    decimal UnitShippingCost, // Ambalaj / kargo birim maliyeti ($)
    DateTimeOffset UpdatedAt
)
{
    public decimal TotalUnitCost => UnitCost + UnitShippingCost;
}

/// <summary>
/// Dönemsel (Günlük / Haftalık / Aylık / Yıllık) Muhasebe Özeti
/// </summary>
internal sealed record PeriodFinancialSummary(
    string PeriodLabel,        // "23.08.2026", "2026-W34", "Ağustos 2026", "2026"
    decimal GrossSales,
    decimal EtsyFees,
    decimal InnerAdFees,
    decimal OffsiteAdFees,
    decimal Refunds,
    decimal Deposits,
    decimal ProductCosts,      // COGS
    decimal EtsyNetRevenue,    // Gross - Refunds - Fees - InnerAds - OffsiteAds
    decimal RealNetProfitUSD,  // EtsyNetRevenue - ProductCosts
    decimal RealNetProfitTRY,  // Sipariş Gününün Kuru İle Hesaplanan Gerçek TL Kârı
    decimal AverageExchangeRate// Sipariş Günündeki Kur Ortalaması
)
{
    public decimal TotalAds => InnerAdFees + OffsiteAdFees;
    public decimal NetMarginPct => GrossSales == 0 ? 0 : Math.Round(RealNetProfitUSD / GrossSales * 100, 1);
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
    decimal TotalAdFees,        // Toplam Reklam (İç + Dış)
    decimal TotalInnerAdFees,   // İç Reklam (Etsy Ads)
    decimal TotalOffsiteAdFees, // Dış Reklam (Offsite Ads)
    decimal TotalDeposits,      // Etsy Banka Transferleri (Payouts)
    decimal TotalProductCosts,  // Toplam Ürün Üretim & Kargo Maliyetleri (COGS)
    decimal TotalShippingCredits,
    decimal TotalNet,           // Etsy Net Gelir
    decimal ExchangeRate,       // Dolar / TL Kuru (Örn: 36.50)
    List<PeriodFinancialSummary> DailySummaries,
    List<PeriodFinancialSummary> WeeklySummaries,
    List<PeriodFinancialSummary> MonthlySummaries,
    List<PeriodFinancialSummary> YearlySummaries,
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd
)
{
    public decimal RealNetProfitUSD => TotalNet - TotalProductCosts;
    public decimal RealNetProfitTRY => Math.Round(RealNetProfitUSD * ExchangeRate, 2);
    public decimal TotalGrossTRY => Math.Round(TotalGross * ExchangeRate, 2);
    public decimal FeeRatePct => TotalGross == 0 ? 0 : Math.Round(TotalFees / TotalGross * 100, 1);
    public decimal RefundRatePct => TotalGross == 0 ? 0 : Math.Round(TotalRefunds / TotalGross * 100, 1);
    public decimal AdSpendPct => TotalGross == 0 ? 0 : Math.Round(TotalAdFees / TotalGross * 100, 1);
    public int TransactionCount => Entries.Count(e => e.Type == "sale");
    public decimal AverageOrderValue => TransactionCount == 0 ? 0 : Math.Round(TotalGross / TransactionCount, 2);
    public decimal ProfitMarginPct => TotalGross == 0 ? 0 : Math.Round(RealNetProfitUSD / TotalGross * 100, 1);

    public static FinancialReport Empty => new(
        [], [], 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 36.50m,
        [], [], [], [], "USD",
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
