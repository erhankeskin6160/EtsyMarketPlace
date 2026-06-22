# Etsy Market Place

C# ve Windows Forms ile geliştirilen Etsy pazar araştırma, rakip mağaza analizi ve SEO aracıdır.

## Mevcut özellikler

- Etsy Open API v3 ile anahtar kelime bazlı aktif ürün arama
- Ürün resmi ve bütün listeleme görselleri için slayt alanı
- Ürün başlığı, fiyat, favori, görüntülenme, stok ve etiket bilgileri
- Gerçek Etsy mağaza adı, mağaza bağlantısı ve toplam mağaza satışı
- Ürün SEO puanı ve pazar sinyali puanı
- Ayrı Rakip Mağaza Analizi formu
- Rakip mağazanın ilk 50 aktif ürününü inceleme
- Mağaza satış, yorum, fiyat, SEO ve rakip gücü KPI değerleri
- En güçlü ürünler, sık kullanılan etiketler ve başlık kelimeleri
- Taksonomi dağılımı, ürün filtreleme ve sıralama
- CSV dışa aktarma
- Etsy OAuth PKCE altyapısı ve API ayar ekranı
- Ayrı Anahtar Kelime ve Fırsat Analizi formu
- Rekabet, talep sinyali, fırsat ve veri güven puanları
- Etiket, başlık kelimesi ve long-tail analizi
- Dört anahtar kelimeyi yan yana karşılaştırma
- Ürün, rakip mağaza ve anahtar kelime takip listeleri
- SQLite üzerinde tarihli fiyat, etkileşim ve analiz snapshot'ları
- Son iki snapshot arasındaki değişim karşılaştırması
- Takip geçmişini CSV olarak dışa aktarma
- Açılışta pazar kontrol paneli
- Takip türleri ve snapshot toplamı KPI değerleri
- En iyi anahtar kelime fırsatları ve en büyük değişimler
- Ürün favori, mağaza satışı ve keyword fırsat trend grafiği
- OAuth ile bağlı kendi mağazasında gerçek sipariş, satılan adet ve brüt ciro raporu
- Tarih aralığı ve ürün bazlı mağaza performansı tablosu

## Veri doğruluğu

Etsy Open API rakip ürünlerin kesin satış adetlerini paylaşmaz. Uygulama rakip mağazanın toplam satışını gösterir. Ürün başarı değerlendirmeleri favori, görüntülenme, mağaza satışı, yorum ve SEO gibi sinyallerden hesaplanır ve Etsy'nin resmi verisi olarak sunulmaz.

## Gereksinimler

- Windows 10 veya Windows 11
- .NET 8 SDK veya daha yeni uyumlu SDK
- Visual Studio 2022 ya da `dotnet` CLI
- Etsy geliştirici hesabı ve onaylı API keystring/shared secret

## Çalıştırma

```powershell
dotnet restore
dotnet run --project .\SimilarProductsWinForms.csproj
```

Release derlemesi:

```powershell
dotnet build .\SimilarProductsWinForms.csproj -c Release
```

## Etsy API ayarları

Uygulamayı açtıktan sonra `API Ayarları` ekranından keystring ve shared secret girilir. API anahtarları, tokenlar ve kullanıcı ayarları Git deposunda tutulmaz; yerel kullanıcı profilinde saklanır.

## Proje yapısı

- `MarketResearchForm.cs`: ana pazar araştırma ekranı
- `CompetitorShopAnalysisForm.cs`: rakip mağaza analiz ekranı
- `Services/EtsyApiClient.cs`: Etsy API ve OAuth işlemleri
- `Services/CompetitorShopAnalyzer.cs`: rakip mağaza istatistikleri
- `Models/`: API, analiz ve ekran modelleri
- `src/EtsyMarketPlace.Domain`: arayüz ve Etsy bağımlılığı olmayan iş modelleri
- `src/EtsyMarketPlace.Application`: kullanım senaryoları, portlar ve puanlama kuralları
- `src/EtsyMarketPlace.Infrastructure`: SQLite gibi kalıcı veri adaptörleri
- `Infrastructure/`: WinForms composition projesindeki Etsy API adaptörleri
- `tests/`: Domain/Application birim testleri
- `docs/`: geliştirme planı ve teknik notlar

## Yol haritası

1. Pazar araştırma temeli - tamamlandı
2. Rakip mağaza analizi - ilk sürüm tamamlandı
3. Anahtar kelime ve fırsat analizi - feature testi yapılıyor
4. Takip listeleri ve geçmiş veriler - feature testi yapılıyor
5. Kontrol paneli ve grafikler - feature testi yapılıyor
6. Kendi mağaza satış analizi - ilk sürüm feature testi yapılıyor
7. Otomasyon, raporlama ve yapay zeka destekli optimizasyon

Ayrıntılar için [geliştirme yol haritasına](docs/gelistirme-yol-haritasi.md) bakın.

Mimari yaklaşım için [Clean Architecture notlarına](docs/clean-architecture.md) bakın.
