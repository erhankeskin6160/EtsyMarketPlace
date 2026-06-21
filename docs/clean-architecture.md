# Clean Architecture Yaklaşımı

## Neden kullanıyoruz?

WinForms kontrolleri, Etsy HTTP çağrıları ve pazar puanı hesapları aynı sınıfta olduğunda değişiklikler birbirini etkiler. Yeni Anahtar Kelime Analizi modülü, iş kurallarını kullanıcı arayüzünden ve Etsy API'den ayırarak geliştirildi.

## Katmanlar

### Domain

`src/EtsyMarketPlace.Domain`

- Anahtar kelime listeleme örneklemi
- Analiz sonucu
- Frekans metrikleri
- Uygulamaya özgü temel veri modelleri

Domain; WinForms, HTTP, JSON, dosya sistemi veya Etsy hakkında teknik bilgi taşımaz. `System.Drawing.Image` gibi UI tipleri Domain modeline konulmaz.

### Application

`src/EtsyMarketPlace.Application`

- `IKeywordMarketGateway`: dış veri kaynağı portu
- `AnalyzeKeywordUseCase`: kullanım senaryosu
- `KeywordAnalysisCalculator`: saf hesaplama kuralları

Application, verinin Etsy'den mi SQLite'tan mı geldiğini bilmez. Yalnızca Domain ve tanımladığı portlarla çalışır.

### Infrastructure

`Infrastructure/KeywordResearch`

- `EtsyKeywordMarketGateway`: Application portunun Etsy adaptörü
- API DTO'larını Domain modellerine dönüştürme

Mevcut proje geçişli olarak tek WinForms projesi içerdiğinden adaptör şimdilik ana proje içinde yer alıyor. Diğer Etsy servisleri taşındığında bağımsız `EtsyMarketPlace.Infrastructure` projesine dönüştürülecek.

### Presentation

- `KeywordOpportunityAnalysisForm`
- KPI, sekme, tablo, resim ve kullanıcı olayları
- Presentation'a özel `KeywordProductRow` görünüm modeli

Form puan hesaplamaz ve doğrudan HTTP isteği oluşturmaz. `AnalyzeKeywordUseCase` çağırır ve sonucu gösterir.

## Bağımlılık yönü

```text
WinForms/Infrastructure -> Application -> Domain
```

Domain dış katmanlara bağımlı değildir. `Program.cs` Composition Root olarak gerçek Etsy adaptörünü kullanım senaryosuna bağlar.

## Neden bütün eski kodu hemen taşımadık?

Tek seferde büyük mimari dönüşüm yapmak çalışan özelliklerde gereksiz risk oluşturur. Strangler yaklaşımı kullanıyoruz: yeni modüller doğru katmanlarla geliştirilir, eski modüller değişiklik gerektiğinde aşamalı olarak taşınır.

## Test yaklaşımı

Puanlama saf bir fonksiyon olduğu için Etsy API olmadan test edilir. Testler:

- Fiyat aykırı değerinde medyan davranışı
- Etiketlerin ürün başına tek sayılması
- Geçersiz kelimenin gateway çağrısından önce reddedilmesi
- Tüm puanların 0-100 aralığında kalması

Çalıştırma:

```powershell
dotnet test .\SimilarProductsWinForms.sln -c Release
```

## Branch akışı

```text
main <- development <- feature/keyword-analysis
```

Feature dalı birim testleri, Release derlemesi ve kullanıcı kabul testinden sonra `development` dalına merge edilir.
