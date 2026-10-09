# Finans Motoru ve Masaüstü Parite Sözleşmesi (VDS Web)

> Bu belge, web kontrol panelinin finansal verilerinin kaynağını, masaüstü WinForms paneliyle
> parite kurallarını ve doğrulama adımlarını tanımlar. Finansla ilgili kod değiştirmeden önce okuyunuz.

## 1. Amaç ve Kapsam
- Web KPI'ları (BU AYKI BRÜT CİRO, GERÇEK NET KÂR, kesinti kırılımı) artık **masaüstü programının
  gönderdiği verilere bağımlı değildir**.
- VDS API, Etsy ödeme hesabı defterini **canlı** okur ve sunucudaki **finans motorunda** işler.
- Masaüstü panelindeki değerler referans kabul edilir; motor aynı Etsy kaynağından aynı sonucu üretmelidir.
  Bu motor; performans uçlarının yanı sıra kontrol paneli trend serisini (`financial/daily-series`),
  finansal analizi (`financial/analysis`) ve günlük bülteni (`shop/daily-brief`) de besler.

## 2. Mimari Akış
```
Etsy API (salt-okunur)
  ├─ payment-account/ledger-entries  → EtsyApiClient.GetLedgerEntriesDetailedAsync
  └─ receipts                        → EtsyApiClient.GetShopReceiptsAsync (+ order_costs tablosu)
        │
        ▼
EtsyLedgerFinancialEngine  (sınıflandırma + günlük özet + toplamlar)
        │
        ▼
EtsyLedgerReportService → GET /api/etsy/financial/performance (4 dk bellek içi önbellek)
        │
        ▼
Web kontrol paneli kartları
```

## 3. Bileşenler (dosya konumları)
| Bileşen | Konum |
|---|---|
| Sınıflandırma + toplamlar motoru | `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyLedgerFinancialEngine.cs` |
| Orkestrasyon + yedek akış | `src/EtsyMarketPlace.Application/EtsyIntegration/EtsyLedgerReportService.cs` |
| Defter çekimi (tam alanlı) | `src/EtsyMarketPlace.Infrastructure/EtsyIntegration/EtsyApiClient.cs` (`GetLedgerEntriesDetailedAsync`) |
| Uç + önbellek | `src/EtsyMarketPlace.Api/Program.cs` (`/api/etsy/financial/performance`, `DashboardResponseCache`) |
| Masaüstü referansı (port kaynağı, DEĞİŞTİRİLMEZ) | `Services/FinancialReportService.cs` (WinForms) |

## 4. Parite Sözleşmesi (özet kurallar)
1. Sınıflandırma sırası masaüstü `MapEntry` ile birebiridir: deposit → offsite → ad_fee → shipping →
   listing_fee → transaction_fee → payment_processing → etsy_tax_fee → refund → sale → (ham tür).
   Deposit kuralında istisna yoktur: "fee" içeren kayıtlar deposit sayılmaz.
2. Tutar dönüşümü: cent/100; TRY → günün tarihsel kuru (`HistoricalExchangeRateProvider`, varsayılan 48,25),
   EUR ×1.1, GBP ×1.3.
3. Net formülü (işaretli): `netRevenue = gross + refunds + fees + innerAds + offsiteAds (+ shipping)`;
   `gerçek net = netRevenue − ürün maliyetleri`. Giderler negatiftir.
4. Bilinmeyen tür + negatif tutar → ücret sayılır; pozitif bilinmeyen → yok sayılır.
5. Gün bölünmesi UTC'dir. Ürün maliyetleri sipariş gününe (receipt `created_at`, UTC) yazılır;
   iptal edilen siparişlerin maliyeti sıfırlanır.
6. Depozito/banka transferleri net kâra DAHİL EDİLMEZ (yalnız `deposits` alanında raporlanır).

## 5. Referans Değerler (masaüstü gönderiminden kayıtlı — regresyon hedefleri)
| Gün (UTC) | Brüt | Komisyon | Dış Reklam | Maliyet |
|---|---|---|---|---|
| 2026-10-01 | 35,5742 | 5,9898 | 5,4729 | 10,42 |
| 2026-10-05 | 0 | 1,2777 | 0 | 0 |
| 2026-10-08 | 137,7959 | 27,0870 | 20,0760 | 35,00 |
| 2026-09-25 | 180,9188 | 38,2569 | 26,0128 | 35,31 |
| 2026-09-30 | 251,3218 | 73,1318 | 19,8802 | 68,06 |

## 6. Doğrulama
- Canlı kontrol: `GET /api/etsy/financial/performance?shopId=53236321&period=this_month`
  → `ledgerOk: true` ve `source: "etsy-ledger-live"` olmalı; tutarlar masaüstü kartıyla birebir olmalıdır.
- Panel karşılaştırması: masaüstü "BU AYKI BRÜT CİRO" = web kartı; "GERÇEK NET KÂR" = web kartı.
- Testler: `dotnet test` (EtsyLedgerFinancialEngineTests dahil).

## 7. Gerileme Yasağı
- Web KPI'ları **hiçbir koşulda** `financial_transactions` tablosundaki ham satırlara veya masaüstü
  push'una geri bağlanamaz. Tek kaynak: bu motor + Etsy defteri.
- Motorda değişiklik yapılırsa §5 referans değerleriyle regresyon testleri güncellenmeli ve canlı
  doğrulama tekrarlanmalıdır.

## 8. Sorun Giderme
- `ledgerOk: false` → defter ucu geçici dalgalanması: motor 1 kez otomatik yeniden dener; hâlâ
  başarısızsa işaretli yedek (legacy-fallback) döner. Teknik detay kullanıcıya gösterilmez.
- Tutar sapması şüphesinde: önce masaüstü kartının son değeriyle karşılaştırın.
- Önbellek: yanıtlar 4 dakika önbelleklenir; tazelik için `generatedAt` alanına bakın.

## 9. Kapsam Notları
- Masaüstü kodu bu mimariden bağımsız yaşar; burada YALNIZCA referanstır.
- Etsy'ye yazma yoktur; tüm çağrılar salt-okunurdur.
- `financial_transactions` tablosu eski akış uyumluluğu için KORUNUR (silinmez); KPI kaynağı değildir.
- **Durum (09.10.2026 akşam):** `financial/daily-series`, `financial/analysis` ve `shop/daily-brief`
  uçları da motora bağlandı. Sıradaki opsiyonel adım: MCP `get_daily_shop_brief` aracı; mağaza performans
  geçmişi ve kargo oturumu paylaşımı ayrı kapsam.
