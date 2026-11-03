# 🏗️ Mimari ve Sistem Tasarımı

EtsyMarketPlace, **Clean Architecture (Temiz Mimari)** ve **Domain-Driven Design (DDD)** prensiplerine göre yapılandırılmış, kurumsal seviyede çok katmanlı bir sistemdir.

---

## 🧩 Katmanlar ve Proje Hiyerarşisi

Sistem `src/` klasörü altında 4 temel katmana ayrılmıştır:

```
src/
├── EtsyMarketPlace.Domain/           # Saf İş Varlıkları, Enum'lar, Sabitler
├── EtsyMarketPlace.Application/      # Use-Case Servisleri, DTO'lar, Arayüzler (Interfaces), MediatR CQRS
├── EtsyMarketPlace.Infrastructure/   # SQLite, Kargo İstemcileri, HTTP Client somutlaştırmaları
└── EtsyMarketPlace.Api/              # ASP.NET Core Minimal API / Thin Controller Katmanı
```

### 1. Domain Katmanı (`EtsyMarketPlace.Domain`)
- Dış kütüphanelerden ve framework bağımlılıklarından tamamen bağımsızdır.
- Varlıklar (Entities): `Listing`, `Order`, `PaymentAccountLedgerEntry`, `FinancialTransaction`, `CarrierShipment`.
- Değer Nesneleri (Value Objects): Para birimleri, kargo barkod tipleri, SEO puanlama modelleri.

### 2. Application Katmanı (`EtsyMarketPlace.Application`)
- **İş Mantığı ve Orkestrasyon:** Bütün finansal hesaplamalar, sipariş işleme, faturalandırma ve AI yönlendirmeleri buradadır.
- **Dependency Inversion:** Dış dünya arayüzleri (`IEtsyApiClient`, `IShippingCarrierService`, `IFinancialRepository`, `IAiModelService`) bu katmanda tanımlanır.
- **MediatR CQRS:** Komutlar (`Commands`) ve Sorgular (`Queries`) ayrı handler sınıflarında yönetilir.
- **Finans Motoru:** `EtsyLedgerFinancialEngine` ve `EtsyLedgerReportService` bu katmanda konumlanır.

### 3. Infrastructure Katmanı (`EtsyMarketPlace.Infrastructure`)
- SQLite veritabanı erişimi (Entity Framework Core ve Dapper/ADO.NET optimize sorgular, WAL modu).
- Şifreli yerel arşivleme (`.etsyvault`), dosya sistemine PDF/fatura yazma.
- Dış servis istemcileri: Resmi Etsy API v3, Aras Kargo SOAP/REST, Navlungo REST, Shiptomore REST.

### 4. API Katmanı (`EtsyMarketPlace.Api`)
- **Thin Controller Prensibi:** Controller metodları kesinlikle doğrudan veritabanı, dosya veya ham HTTP çağrısı barındıramaz. Tek görevi gelen isteği karşılayıp MediatR veya ilgili Application servisine ileterek HTTP 200/400/500 sonucunu dönmektir.
- **Güvenlik Middleware:** Swagger Basic Auth (`SwaggerBasicAuthMiddleware`), API Anahtarı denetimi (`ApiKeyAuthenticationHandler`), JWT Bearer Token doğrulama (`BearerAuthenticationHandler`).

---

## 🖥️ İstemciler

### A. Masaüstü WinForms İstemcisi (`SimilarProductsWinForms.csproj`)
- **Teknoloji:** .NET 8 Windows Forms, SkiaSharp, System.Text.Json.
- **Kritik Yetenekler:**
  - **SkiaSharp Görsel İşleme:** Yüklenen ürün fotoğraflarından EXIF meta verilerini soyma, pHash parmak izini değiştirme (Etsy yinelenen görsel cezasından korunma).
  - **Donanım Erişimi:** USB veya yerel ağ üzerinden Termal Etiket Yazıcılarına (ZPL ve ESC/POS) doğrudan kargo etiketi gönderme.
  - **Çevrimdışı Çalışma:** İnternet kesintilerinde dahi yerel SQLite Model Lake üzerinden ürün ve sipariş arşivi tutabilme.

### B. Web Kokpiti (`client/etsy-web`)
- **Teknoloji:** Angular 18 Standalone Components, TypeScript, RxJS, Angular Signals.
- **Tasarım Dili:** Koyu Cyberpunk / Neon SaaS Estetiği, CSS Grid & Flexbox, SVG ikonografi.
- **Reaktif State:** Angular Signals ve RxJS stream'leri ile canlı kur güncellemeleri, anlık sipariş süzme ve dinamik modallar.

---

## 💾 Veritabanı ve Eşzamanlılık (SQLite WAL Modu)

Merkezi SQLite veritabanı yüksek eşzamanlılık (concurrency) için **WAL (Write-Ahead Logging)** modunda çalıştırılır:
- Okuma işlemleri yazma işlemlerini engellemez (Non-blocking reads).
- VDS API sunucusu ile masaüstü senkronizasyonu veri kaybı yaşanmadan milisaniyeler içinde gerçekleşir.
