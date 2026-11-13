using System;

namespace EtsyMarketPlace.Domain.Accounting.Entities;

/// <summary>
/// 253 Tesis, Makine ve Cihazlar (3D yazıcı filosu) veya 255 Demirbaşlar sabit kıymet kartı.
/// </summary>
public class FixedAssetItem
{
    public long Id { get; set; }
    public string ShopId { get; set; } = string.Empty;
    public string AssetName { get; set; } = string.Empty;

    /// <summary>TDHP hesap kodu: 253 (Makine/3D Yazıcı) veya 255 (Demirbaş/Bilgisayar).</summary>
    public string AssetAccountCode { get; set; } = "253";

    /// <summary>Amortisman yansıtma gider hedefi: 632 Genel Yönetim veya 620/730 Üretim Maliyeti.</summary>
    public OperatingExpenseCategory ExpenseTargetCategory { get; set; } = OperatingExpenseCategory.GeneralManagement_632;

    public DateTime AcquisitionDate { get; set; }
    public decimal AcquisitionCostTry { get; set; }
    public decimal SalvageValueTry { get; set; }
    public int UsefulLifeYears { get; set; } = 5;
    public DepreciationMethod Method { get; set; } = DepreciationMethod.StraightLine;
    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

