using System;

namespace EtsyMarketPlace.Domain.Accounting.Entities;

/// <summary>
/// Mağazanın seçtiği TDHP muhasebe politikası, vergi rejimi ve Genç Girişimci istisna profili.
/// </summary>
public class ShopAccountingProfile
{
    public string ShopId { get; set; } = string.Empty;
    public CommissionAccountTarget CommissionTarget { get; set; } = CommissionAccountTarget.OtherCommission_653;
    public ShippingPackagingCostTarget ShippingTarget { get; set; } = ShippingPackagingCostTarget.MarketingSelling_631;
    public CanceledOrderCostTarget CanceledCostTarget { get; set; } = CanceledOrderCostTarget.ExtraordinaryLoss_689;
    public InventoryValuationMethod InventoryMethod { get; set; } = InventoryValuationMethod.PerpetualPerOrder;
    public TaxRegimeType TaxRegime { get; set; } = TaxRegimeType.SoleProprietorshipGvk;

    /// <summary>Özel Mod: Genç Girişimci Kazanç İstisnası (GVK Mükerrer Md. 20) aktif mi?</summary>
    public bool YoungEntrepreneurModeEnabled { get; set; } = true;

    /// <summary>2026 yılı GVK Mükerrer Md. 20 yıllık kazanç istisnası tutarı (varsayılan 400.000 TL).</summary>
    public decimal YoungEntrepreneurAnnualLimitTry { get; set; } = 400_000m;

    /// <summary>Aynı takvim yılı içinde önceki dönemlerde kullanılmış olan istisna tutarı (TL).</summary>
    public decimal YoungEntrepreneurPriorUsedTry { get; set; } = 0m;

    /// <summary>Yurt dışı (601) e-ihracat kazanç istisnası oranı (%0, %50 veya %80).</summary>
    public decimal EExportExemptionRatePct { get; set; } = 0m;

    /// <summary>Kurumlar Vergisi (%25) veya özel vergi modu kullanıldığında uygulanacak oran (%).</summary>
    public decimal CustomOrCorporateTaxRatePct { get; set; } = 25m;

    /// <summary>Kullanıcı özelleştirilmiş GVK vergi dilimleri (JSON array: [{upperLimitTry, ratePct}]).</summary>
    public string? CustomGvkBracketsJson { get; set; }

    /// <summary>Etsy %2.5 döviz çevrim makası ve BankPayout kur farklarını 646/656'ya otomatik yansıt.</summary>
    public bool IncludeFxSpreadAndExchangeDifference { get; set; } = true;

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

