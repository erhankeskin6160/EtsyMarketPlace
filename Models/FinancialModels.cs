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
    decimal ExchangeRate = 48.25m // Sipariş/İşlem Günü Kabul Edilen Dolar/TL Kuru
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
/// Sipariş Bazlı Maliyet Kaydı (Order-Level COGS - Sipariş Numarasına Göre İndeksli)
/// </summary>
internal sealed record OrderCostEntry(
    string ReceiptId,
    string ListingId,
    string Title,
    decimal UnitCost,         // Üretim / Hammadde
    decimal UnitShippingCost, // Kargo
    decimal UnitPackagingCost,// Paketleme
    DateTimeOffset UpdatedAt,
    string? InvoiceFilePath = null, // Kargo Faturası Dosya Yolu (PDF / Görsel)
    long BuyerUserId = 0,
    string BuyerName = "",
    string BuyerEmail = "",
    string? Notes = null
)
{
    public decimal TotalUnitCost => UnitCost + UnitShippingCost + UnitPackagingCost;
    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceFilePath) && System.IO.File.Exists(InvoiceFilePath);
}

/// <summary>
/// Ürün Maliyet Kaydı (COGS - Cost of Goods Sold - Geriye Dönük Uyumluluk)
/// </summary>
internal sealed record ProductCostEntry(
    string ListingId,
    string Title,
    decimal UnitCost,         // Üretim
    decimal UnitShippingCost, // Kargo
    decimal UnitPackagingCost,// Paketleme
    DateTimeOffset UpdatedAt,
    string? InvoiceFilePath = null // Kargo Faturası Dosya Yolu (PDF / Görsel)
)
{
    public decimal TotalUnitCost => UnitCost + UnitShippingCost + UnitPackagingCost;
    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceFilePath) && System.IO.File.Exists(InvoiceFilePath);
}

/// <summary>
/// Sipariş Bazında Net Kâr Özeti (Mağaza Fişi'nden türetilir)
/// </summary>
internal sealed record OrderFinancialSummary(
    long ReceiptId,
    DateTimeOffset OrderDate,
    string ProductTitle,        // İlk transaction başlığı
    long ListingId,             // İlk transaction listing_id
    int Quantity,               // Toplam adet
    decimal GrandTotal,         // Müşterinin ödediği tutar
    decimal Subtotal,           // Ürün fiyatı
    decimal ShippingPrice,      // Kargo
    decimal DiscountAmt,        // İndirim
    decimal TaxPaidByBuyer,     // Vergi
    decimal TransactionFee,     // %6.5 İşlem
    decimal PaymentProcessingFee, // %6.5 + 3TL Ödeme İşleme
    decimal RegulatoryOperatingFee, // %1.5 Yasal
    decimal ListingFee,         // $0.20 İlan
    decimal VatOnFees,          // %20 KDV
    decimal EtsyFees,           // Toplam kesintiler (vergi hariç/dahil)
    decimal OffsiteAdFee,       // %15 Dış Reklam kesimi (eğer uygulanabilirse)
    decimal ProductCost,        // Kullanıcının girdiği maliyet × adet
    decimal NetProfitUSD,       // GrandTotal − EtsyFees − OffsiteAdFee − ProductCost
    decimal ExchangeRate,       // Sipariş günü USD/TRY kuru
    decimal NetProfitTRY,       // NetProfitUSD × ExchangeRate
    bool HasCostData,           // Maliyet girilmiş mi?
    decimal UnitProductionCost = 0m,  // Birim üretim maliyeti
    decimal UnitShippingCost = 0m,    // Birim kargo maliyeti
    decimal UnitPackagingCost = 0m,   // Birim paketleme maliyeti
    string? InvoiceFilePath = null,   // Sipariş/Ürün Kargo Faturası
    long BuyerUserId = 0,             // Müşteri No / User ID
    string BuyerName = "",            // Müşteri Adı / Alıcı
    string BuyerEmail = "",           // Müşteri E-Postası
    bool IsCanceled = false,          // Sipariş iptal edildi mi?
    decimal RefundedAmount = 0m,      // İade edilen tutar ($)
    string OrderStatus = "Completed"  // Completed, Canceled, PartialRefund
)
{
    public decimal TotalOrderShippingCost => UnitShippingCost * Quantity;
    public decimal TotalOrderProductionCost => UnitProductionCost * Quantity;
    public decimal TotalOrderPackagingCost => UnitPackagingCost * Quantity;
    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceFilePath) && System.IO.File.Exists(InvoiceFilePath);
    public string DisplayCustomer => !string.IsNullOrWhiteSpace(BuyerName) 
        ? (BuyerUserId > 0 ? $"{BuyerName} (#{BuyerUserId})" : BuyerName)
        : (BuyerUserId > 0 ? $"Müşteri #{BuyerUserId}" : "—");

    public string DisplayStatus => (IsCanceled || OrderStatus.Equals("Canceled", StringComparison.OrdinalIgnoreCase)) 
        ? "🔴 İptal Edildi" 
        : (RefundedAmount > 0 ? $"🟡 Kısmi İade (-${RefundedAmount:N2})" : "🟢 Tamamlandı");
}

/// <summary>
/// Dönemsel (Günlük / Haftalık / Aylık / Yıllık) Muhasebe Özeti
/// </summary>
internal sealed record PeriodFinancialSummary(
    string PeriodLabel,        // "23.08.2026", "2026-W34", "Ağustos 2026", "2026"
    DateTime SortDate,         // Gerçek kronolojik sıralama tarihi
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
    decimal AverageExchangeRate,// Sipariş Günündeki Kur Ortalaması
    decimal GrossSalesTRY = 0m,
    decimal EtsyFeesTRY = 0m,
    decimal InnerAdFeesTRY = 0m,
    decimal OffsiteAdFeesTRY = 0m,
    decimal RefundsTRY = 0m,
    decimal ProductCostsTRY = 0m,
    decimal EtsyNetRevenueTRY = 0m
)
{
    public decimal TotalAds => InnerAdFees + OffsiteAdFees;
    public decimal TotalAdsTRY => InnerAdFeesTRY + OffsiteAdFeesTRY;
    public decimal TotalExpensesUSD => Math.Abs(EtsyFees) + Math.Abs(InnerAdFees) + Math.Abs(OffsiteAdFees) + Math.Abs(Refunds) + ProductCosts;
    public decimal TotalExpensesTRY => Math.Abs(EtsyFeesTRY) + Math.Abs(InnerAdFeesTRY) + Math.Abs(OffsiteAdFeesTRY) + Math.Abs(RefundsTRY) + ProductCostsTRY;
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
    List<OrderFinancialSummary> OrderSummaries, // [YENİ] Sipariş bazında net kâr
    string Currency,
    DateTimeOffset PeriodStart,
    DateTimeOffset PeriodEnd,
    bool IsFallbackMode = false
)
{
    public decimal RealNetProfitUSD => TotalNet - TotalProductCosts;
    public decimal RealNetProfitTRY => DailySummaries.Count > 0 
        ? DailySummaries.Sum(d => d.RealNetProfitTRY) 
        : (OrderSummaries.Count > 0 ? OrderSummaries.Sum(o => o.NetProfitTRY) : Math.Round(RealNetProfitUSD * ExchangeRate, 2));

    public decimal TotalGrossTRY => DailySummaries.Count > 0 
        ? DailySummaries.Sum(d => d.GrossSales * d.AverageExchangeRate) 
        : (OrderSummaries.Count > 0 ? OrderSummaries.Sum(o => Math.Round(o.GrandTotal * o.ExchangeRate, 2)) : Math.Round(TotalGross * ExchangeRate, 2));
    public decimal FeeRatePct => TotalGross == 0 ? 0 : Math.Round(TotalFees / TotalGross * 100, 1);
    public decimal RefundRatePct => TotalGross == 0 ? 0 : Math.Round(TotalRefunds / TotalGross * 100, 1);
    public decimal AdSpendPct => TotalGross == 0 ? 0 : Math.Round(TotalAdFees / TotalGross * 100, 1);
    public int TransactionCount => Entries.Count(e => e.Type == "sale");
    public decimal AverageOrderValue => TransactionCount == 0 ? 0 : Math.Round(TotalGross / TransactionCount, 2);
    public decimal ProfitMarginPct => TotalGross == 0 ? 0 : Math.Round(RealNetProfitUSD / TotalGross * 100, 1);

    public static FinancialReport Empty => new(
        [], [], 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 48.25m,
        [], [], [], [], [], "USD",
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

/// <summary>
/// Zaman Serisi Tahmin Veri Noktası
/// </summary>
internal sealed record ForecastDataPoint(
    string PeriodLabel,
    DateTime Date,
    decimal ExpectedGrossUSD,
    decimal ExpectedNetProfitUSD,
    decimal ExpectedNetProfitTRY,
    decimal LowScenarioProfitUSD,
    decimal HighScenarioProfitUSD,
    int ExpectedOrders,
    bool IsHistorical = false
);

/// <summary>
/// AI Finansal Tahmin & Projeksiyon Sonucu
/// </summary>
internal sealed record FinancialForecastResult(
    decimal NextMonthGrossUSD,
    decimal NextMonthGrossTRY,
    decimal NextMonthNetProfitUSD,
    decimal NextMonthNetProfitTRY,
    decimal LowScenarioUSD,
    decimal LowScenarioTRY,
    decimal HighScenarioUSD,
    decimal HighScenarioTRY,
    int NextMonthOrders,
    decimal GrowthRateMoM,
    double EstimatedFilamentKg,
    int EstimatedPackagingBoxes,
    List<ForecastDataPoint> Timeline,
    List<string> AiRecommendations
);
