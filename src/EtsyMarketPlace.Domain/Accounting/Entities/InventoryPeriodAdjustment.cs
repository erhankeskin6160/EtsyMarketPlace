using System;

namespace EtsyMarketPlace.Domain.Accounting.Entities;

/// <summary>
/// Aralıklı Envanter (Dönem Başı Stok + Net Alışlar - Dönem Sonu Stok = STMM) dönem ayarı.
/// </summary>
public class InventoryPeriodAdjustment
{
    public string ShopId { get; set; } = string.Empty;
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal OpeningInventoryTry { get; set; }
    public decimal GrossPurchasesTry { get; set; }
    public decimal PurchaseReturnsAndDiscountsTry { get; set; }
    public decimal ClosingInventoryTry { get; set; }
    public string? Notes { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public decimal ComputePeriodicCogsTry() =>
        Math.Max(0m, Math.Round(OpeningInventoryTry + (GrossPurchasesTry - PurchaseReturnsAndDiscountsTry) - ClosingInventoryTry, 2));
}

