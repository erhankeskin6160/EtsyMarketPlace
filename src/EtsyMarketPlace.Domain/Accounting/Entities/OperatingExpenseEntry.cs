using System;

namespace EtsyMarketPlace.Domain.Accounting.Entities;

/// <summary>
/// Tekdüzen Hesap Planı (TDHP) uyumlu tek seferlik veya aylık tekrarlayan işletme gider/gelir fişi.
/// Tarafsızlık ve Belgelendirme İlkesi gereği belge numarası veya KKEG işareti taşır.
/// </summary>
public class OperatingExpenseEntry
{
    public long Id { get; set; }
    public string ShopId { get; set; } = string.Empty;
    public DateTime EntryDate { get; set; }
    public string DocumentNo { get; set; } = string.Empty;
    public OperatingExpenseCategory Category { get; set; } = OperatingExpenseCategory.GeneralManagement_632;
    public string Description { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    public decimal ExchangeRateToTry { get; set; } = 1m;
    public decimal AmountTry { get; set; }

    /// <summary>False ise Kanunen Kabul Edilmeyen Gider (950/951 KKEG) olarak Ticari Kâra eklenip Mali Kârdan düşülmez.</summary>
    public bool IsTaxDeductible { get; set; } = true;

    public ExpenseFrequency Frequency { get; set; } = ExpenseFrequency.OneTime;
    public DateTime? RecurringEndDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

