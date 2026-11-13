namespace EtsyMarketPlace.Domain.Accounting;

/// <summary>Etsy platform işlem/listeleme/ödeme kesintilerinin MSUGT hesap sınıfı politikası.</summary>
public enum CommissionAccountTarget
{
    /// <summary>Seçenek B (TDHP Hesap Ayrımı): İşlem ve ödeme komisyonları 653 Komisyon Giderleri hesabına yazılır.</summary>
    OtherCommission_653 = 0,

    /// <summary>Seçenek A: Tüm platform komisyonları reklam ile birlikte 631 Pazarlama Satış ve Dağıtım altında toplanır.</summary>
    MarketingSelling_631 = 1
}

/// <summary>Kargo navlunu ve paketleme maliyetlerinin Gelir Tablosu konumu.</summary>
public enum ShippingPackagingCostTarget
{
    /// <summary>Seçenek A: Kargo ve paketleme 760 -&gt; 631 Pazarlama Satış ve Dağıtım Giderleri'ne yazılır; 620 yalnız üretim maliyetidir.</summary>
    MarketingSelling_631 = 0,

    /// <summary>Seçenek B: Ürün, kargo ve paketleme birlikte 620 Satılan Mamuller Maliyeti içinde toplanır.</summary>
    CostOfGoods_620 = 1
}

/// <summary>Üretilmiş/kargolanmış ancak sonradan iptal/iade edilmiş siparişlerin maliyet kaydı.</summary>
public enum CanceledOrderCostTarget
{
    /// <summary>Geri kazanılamayan iptal/fire maliyetleri 689 Diğer Olağandışı Gider ve Zararlar hesabına ayrıştırılır.</summary>
    ExtraordinaryLoss_689 = 0,

    /// <summary>İptal edilen ürün maliyetleri 620 Satışların Maliyeti içinde tutulur.</summary>
    CostOfGoods_620 = 1
}

/// <summary>Genel Muhasebe Ünite 4 stok maliyet hesaplama yöntemi.</summary>
public enum InventoryValuationMethod
{
    /// <summary>Devamlı Envanter: Her satış anında OrderCost üzerinden maliyet düşülür.</summary>
    PerpetualPerOrder = 0,

    /// <summary>Aralıklı Envanter: Dönem Başı Stok + Net Alışlar - Dönem Sonu Fiili Stok = STMM.</summary>
    PeriodicCount = 1
}

/// <summary>Vergi mükellefiyet türü (691 Dönem Kârı Vergi Karşılığı hesabı için).</summary>
public enum TaxRegimeType
{
    /// <summary>Şahıs İşletmesi (GVK Md. 103 artan oranlı tarife %15–%40 + Genç Girişimci / E-İhracat istisnası).</summary>
    SoleProprietorshipGvk = 0,

    /// <summary>Sermaye Şirketi — Kurumlar Vergisi (%25).</summary>
    CorporateKvk25 = 1,

    /// <summary>Kullanıcı tanımlı sabit oran.</summary>
    CustomRate = 2
}

/// <summary>Duran varlık amortisman hesaplama yöntemi (Ünite 6 &amp; 7).</summary>
public enum DepreciationMethod
{
    /// <summary>Normal Amortisman: (Maliyet - Hurda) / Ekonomik Ömür.</summary>
    StraightLine = 0,

    /// <summary>Azalan Bakiyeler: Çift kat oran (%50 tavan sınırlı), son yıl kalan defter değeri sıfırlanır.</summary>
    DecliningBalance = 1
}

/// <summary>İşletme gider/gelir fiş sıklığı.</summary>
public enum ExpenseFrequency
{
    OneTime = 0,
    MonthlyRecurring = 1
}

/// <summary>TDHP uyumlu manuel işletme gideri / indirim / olağandışı işlem kategorisi.</summary>
public enum OperatingExpenseCategory
{
    SalesDiscount_611 = 611,
    OtherSalesDeduction_612 = 612,
    CostOfGoodsAdjustment_620 = 620,
    ResearchAndDevelopment_630 = 630,
    MarketingAndSelling_631 = 631,
    GeneralManagement_632 = 632,
    InterestIncome_642 = 642,
    OtherOrdinaryIncome_649 = 649,
    OtherCommission_653 = 653,
    OtherOrdinaryExpense_659 = 659,
    Financing_660 = 660,
    PriorPeriodIncome_671 = 671,
    ExtraordinaryIncome_679 = 679,
    IdleCapacityLoss_680 = 680,
    PriorPeriodExpense_681 = 681,
    ExtraordinaryExpense_689 = 689,

    /// <summary>Şahıs işletmesi ödenen Bağ-Kur primleri (GVK Md. 89 matrah indirimi).</summary>
    BagkurPremiumDeduction = 890
}

