# 🏛️ EtsyMarketPlace — Sipariş Bazlı Maliyet & Müşteri/Sipariş Arama-Filtreleme Kodlama Mimarisi

Bu rapor; Etsy Finansal Muhasebe Paneli'nde maliyetlerin **ürün (Listing ID)** yerine doğrudan **sipariş numarası (Receipt ID / Order ID)** bazında indekslenmesi ve **müşteri numarası/adı ile sipariş numarasına göre canlı filtreleme** mimarisini açıklamaktadır.

---

## 📌 1. Mevcut Yapı vs. Yeni Mimari

```
[ Eski Yapı: Ürün İndeksli ]
Etsy Siparişi (Receipt) ---> Listing ID ---> SQLite (product_costs) ---> Tek Maliyet Tüm Siparişlere

[ Yeni Yapı: Sipariş İndeksli & Müşteri Filtreli ]
Etsy Siparişi (Receipt) ---> Receipt ID (Sipariş No) ---> SQLite (order_costs) ---> Siparişe Özel Maliyet & Fatura
                        ---> Müşteri ID & İsim       ---> UI Arama / Filtre Motoru
```

### Neden Sipariş Bazlı Olmalı?
1. **Değişken Kargo ve Paketleme**: Aynı ürün farklı ülkelere/eyaletlere farklı kargo ücretleriyle gönderilebilir.
2. **Kişiselleştirme / Varyant Farkları**: Aynı ilan altında satılan ürünlerin hammadde/üretim sarfiyatı her siparişte farklı olabilir.
3. **Fatura Eşleme**: Kargo takip faturası ürün bazında değil, sipariş ve koli bazında düzenlenir.

---

## 🗄️ 2. Veritabanı ve Domain Katmanı (Data & Models)

### 2.1. SQLite `order_costs` Tablo Şeması
Maliyetlerin tekil anahtarı artık `listing_id` değil, doğrudan sipariş kimliği olan `receipt_id` olacaktır.

```sql
CREATE TABLE IF NOT EXISTS order_costs (
    receipt_id TEXT PRIMARY KEY,          -- Etsy Sipariş / Fiş Numarası (#1234567890)
    listing_id TEXT NOT NULL,             -- Ürünün Listing Kimliği
    product_title TEXT NOT NULL,          -- Ürün Başlığı
    buyer_user_id TEXT,                   -- Müşteri Kullanıcı ID / No
    buyer_name TEXT,                      -- Müşteri Adı Soyadı / Alıcı
    buyer_email TEXT,                     -- Müşteri E-Postası
    unit_cost REAL NOT NULL DEFAULT 0,    -- Üretim / Hammadde Maliyeti ($)
    shipping_cost REAL NOT NULL DEFAULT 0,-- Sipariş Kargo Maliyeti ($)
    packaging_cost REAL NOT NULL DEFAULT 0,-- Paketleme Maliyeti ($)
    invoice_file_path TEXT,               -- Kargo Faturası Dosya Yolu (PDF / Resim)
    notes TEXT,                           -- Sipariş Notu
    updated_at TEXT NOT NULL              -- Güncellenme Tarihi
);

CREATE INDEX IF NOT EXISTS idx_order_costs_search ON order_costs(buyer_name, buyer_user_id, product_title);
```

### 2.2. Domain Modelleri (`Models/FinancialModels.cs`)

```csharp
/// <summary>
/// Sipariş Bazlı Maliyet Kaydı (Order-Level COGS)
/// </summary>
public sealed record OrderCostEntry(
    string ReceiptId,
    string ListingId,
    string ProductTitle,
    string? BuyerUserId,
    string? BuyerName,
    string? BuyerEmail,
    decimal UnitCost,          // Üretim maliyeti
    decimal ShippingCost,      // Kargo maliyeti
    decimal PackagingCost,     // Paketleme maliyeti
    DateTimeOffset UpdatedAt,
    string? InvoiceFilePath = null,
    string? Notes = null
)
{
    public decimal TotalCost => UnitCost + ShippingCost + PackagingCost;
    public bool HasInvoice => !string.IsNullOrWhiteSpace(InvoiceFilePath) && File.Exists(InvoiceFilePath);
}

/// <summary>
/// Sipariş Bazında Net Kâr ve Müşteri Özeti
/// </summary>
public sealed record OrderFinancialSummary(
    long ReceiptId,
    DateTimeOffset OrderDate,
    string ProductTitle,
    long ListingId,
    int Quantity,
    decimal GrandTotal,         // Müşteri Ödemesi
    decimal Subtotal,           // Ürün Fiyatı
    decimal ShippingPrice,      // Kargo Bedeli
    decimal DiscountAmt,        // İndirim
    decimal TaxPaidByBuyer,     // Alıcı Vergisi
    decimal TransactionFee,     // %6.5 İşlem Komisyonu
    decimal PaymentProcessingFee, // %6.5 + 3 TL Ödeme İşleme
    decimal RegulatoryOperatingFee, // %1.5 Yasal Pay
    decimal ListingFee,         // $0.20 İlan
    decimal VatOnFees,          // %20 KDV
    decimal EtsyFees,           // Toplam Kesintiler
    decimal OffsiteAdFee,       // %15 Dış Reklam
    decimal ProductCost,        // Siparişe girilen maliyet
    decimal NetProfitUSD,       // Net USD Kâr
    decimal ExchangeRate,       // Sipariş günü kuru
    decimal NetProfitTRY,       // Net TL Kâr
    bool HasCostData,           // Maliyet girildi mi?
    decimal UnitProductionCost, // Üretim maliyeti
    decimal UnitShippingCost,   // Kargo maliyeti
    decimal UnitPackagingCost,  // Paketleme maliyeti
    string? InvoiceFilePath,    // Fatura yolu
    long BuyerUserId = 0,       // Müşteri ID / No
    string BuyerName = "",      // Müşteri Adı
    string BuyerEmail = ""      // Müşteri E-Postası
);
```

---

## ⚙️ 3. Servis Katmanı (Service Layer)

### 3.1. Etsy API İstemcisi (`EtsyApiClient.cs` & `ShopPerformanceModels.cs`)
Etsy `/v3/application/shops/{shop_id}/receipts` endpoint'inden gelen veriden müşteri bilgileri çekilir:
```csharp
long buyerUserId = GetLong(receipt, "buyer_user_id");
string buyerName = GetFirstString(receipt, "name", "buyer_name");
string buyerEmail = GetString(receipt, "buyer_email");
```

### 3.2. Maliyet Deposu (`SqliteOrderCostRepository.cs`)
`listing_id` yerine `receipt_id` üzerinden CRUD işlemleri:
- `GetByReceiptIdAsync(string receiptId)`
- `SaveAsync(OrderCostEntry entry)`
- `GetAllAsync()`
- `DeleteAsync(string receiptId)`

### 3.3. Net Kâr Hesaplama Motoru (`FinancialReportService.cs`)
`BuildOrderSummariesAsync` fonksiyonunda:
```csharp
// 1. O siparişin Receipt ID'sine ait maliyet SQLite'dan alınır
if (costMap.TryGetValue(r.ReceiptId.ToString(), out var entry))
{
    unitProductionCost = entry.UnitCost;
    unitShippingCost = entry.ShippingCost;
    unitPackagingCost = entry.PackagingCost;
    invoicePath = entry.InvoiceFilePath;
    hasCost = true;
}
decimal totalOrderCost = (unitProductionCost + unitShippingCost + unitPackagingCost) * Math.Max(1, totalQty);

// 2. Sipariş Net Kârı kuruşu kuruşuna hesaplanır
decimal netProfitUSD = grandTotal - etsyFees - offsiteAdFee - totalOrderCost;
decimal netProfitTRY = Math.Round(netProfitUSD * rate, 2);
```

---

## 🖥️ 4. Kullanıcı Arayüzü & Arama Filtreleme (UI Layer)

### 4.1. Siparişler Paneli Arama Çubuğu (`FinancialReportForm.cs`)

`BuildOrdersPanel()` içerisine bir arama & filtre şeridi yerleştirilir:
- **Arama Kutusu**: `_txtOrderSearch` (Sipariş No `#...`, Müşteri No, Müşteri Adı veya Ürün Adı).
- **Maliyet Durumu Filtresi**: `_cboCostFilter` (Tümü / Maliyeti Girilenler / Maliyeti Eksikler).
- **Canlı Filtreleme**: Kullanıcı yazdıkça UI anında filtreler.

```csharp
private void ApplyOrderFilters()
{
    string query = _txtOrderSearch.Text.Trim().ToLowerInvariant();
    var filtered = _report.OrderSummaries.AsEnumerable();

    if (!string.IsNullOrWhiteSpace(query))
    {
        filtered = filtered.Where(o =>
            o.ReceiptId.ToString().Contains(query) ||
            o.BuyerName.ToLowerInvariant().Contains(query) ||
            o.BuyerUserId.ToString().Contains(query) ||
            o.BuyerEmail.ToLowerInvariant().Contains(query) ||
            o.ProductTitle.ToLowerInvariant().Contains(query)
        );
    }

    if (_cboCostFilter.SelectedIndex == 1)      // Sadece maliyeti girilenler
        filtered = filtered.Where(o => o.HasCostData);
    else if (_cboCostFilter.SelectedIndex == 2) // Maliyeti eksik olanlar
        filtered = filtered.Where(o => !o.HasCostData);

    PopulateOrdersGrid(filtered);
}
```

### 4.2. Sipariş Maliyet Giriş Penceresi (`OrderCostPopupForm.cs`)
- Siparişe çift tıklandığında veya sağ tık "💰 Maliyet & Fatura Düzenle" denildiğinde açılan form doğrudan **Sipariş Numarasına (#ReceiptId)** bağlanır.
- Üretim maliyeti, kargo maliyeti, paketleme maliyeti ve kargo faturası (PDF/Görsel) girilip kaydedildiğinde sadece o siparişin net kârı ve bilançosu güncellenir.
