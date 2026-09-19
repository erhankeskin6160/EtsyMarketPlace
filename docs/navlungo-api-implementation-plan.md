# Navlungo API Entegrasyon Planı

## 1. Mevcut durum ve bulgular

- Aktif GitHub çalışma ağacında (`C:\Users\Erhan Keskin\EtsyMarketPlace`) Navlungo entegrasyon sınıfı veya Navlungo fiyat hesaplama akışı bulunamadı.
- Arayüz ekranında görünen test girdileri:
  - Hedef ülke: ABD (`US`)
  - Ağırlık: `0.40 kg`
  - Ölçüler: `15 x 20 x 10 cm`
  - Uygulamanın hesapladığı hacimsel ağırlık/desi: `0.60`
- Navlungo portalındaki referans teklifler:
  - Widect Economy, 3–7 gün: `USD 15.03`
  - FedEx Express, 1–3 gün: `USD 20.75`
  - UPS Express, 1–3 gün: `USD 32.96`
  - UPS Standard, 2–5 gün: `USD 37.72`
- Uygulama ekranındaki teklifler sırasıyla `USD 16.78`, `23.08`, `36.44`, `41.30` gösteriyor. Farklar yaklaşık `%9.49–%11.64` aralığında.
- Yeni Network görüntülerinde gerçek fiyat isteği açıkça görüldü: `POST https://quick-price-calculator.navlungo.com/tr?source=user`.
- İstek gövdesi portalda şu anlamı taşıyor: `fromCountry=TR`, `toCountry=US`, `packages[0].weight=0.4`, `length=15`, `width=20`, `height=10`, `source=user`.
- Portal aynı girdiler için `Widect 15.03`, `FedEx 20.75`, `UPS 32.96` ve `UPS 37.72 USD` döndürüyor.
- Programdaki `16.78`, `23.08`, `36.44`, `41.30 USD` değerleri canlı yanıt değil; mevcut kaynak kodundaki `GenerateRealisticFallbackQuotes` formülünün bu girdilerde ürettiği değerlerle birebir aynı.
- `NavlungoApiClient.FetchLiveQuotesAsync` tüm HTTP/parse hatalarını sessizce yakalıyor; canlı teklif listesi boş kalınca fallback fiyatları döndürüyor. Bu nedenle ekranda gerçek API başarısızlığı görünmüyor.
- Programın aktif kaynak ağacındaki Navlungo istemcisi portalın Next.js `text/x-component` yanıtını regex ile tahmin ederek ayrıştırıyor. Bu yaklaşım gerçek RSC stream formatı değiştiğinde parse başarısızlığına yol açıyor.
- Program, portal oturumunu `id_token` ve geniş bir Cookie başlığıyla taklit ediyor. Bu, kullanıcı hesabına bağlı portal fiyatını garanti eden resmi bir API sözleşmesi değildir; oturum/cookie süresi veya gerekli Next.js başlıkları değiştiğinde çağrı başarısız olabilir.
- `Controls/AnimatedShippingComparisonDrawer.cs` Navlungo tekliflerini her zaman `IsLive = true` olarak işaretliyor; gerçek istemci fallback döndürse bile kullanıcıya canlı teklif gibi gösteriliyor.
- Güncel `development` branch'inde bu entegrasyon kodu mevcut ve `feature/navlungo-api-parity-plan` branch'i bu inceleme için açıldı.

## 2. Kullanılacak resmi API sözleşmesi

Navlungo'nun yayınladığı Express Teklif API dokümanına göre:

- Test hostu: `https://api-qa.navlungo.com`
- Canlı host: `https://api.navlungo.com`
- Endpoint: `POST /stores/v2/{store_id}/orders`
- Yetkilendirme: OAuth2 `Authorization: Bearer <access_token>`
- İstek gövdesi:
  - `order`
  - `packages`
  - `shipmentType`
- `shipmentType`: `sales`, `sample`, `micro-export` veya `gift`
- Paket alanları: `quantity`, `type`, `weight`, `width`, `length`, `height`
- Yanıt: `searchId` ve `quotes[]`
- Teklif alanları: `quoteReference`, `price`, `currency`, `serviceType`, `minTransitTime`, `maxTransitTime`, `description`, `carrier`, `additionalServices`

Önemli: `originRegion` alanı doğru gönderilmezse Navlungo ek iç taşıma maliyeti ekleyebilir. Bu alan için mümkünse çıkış posta kodu kullanılmalıdır.

## 3. Kök neden ve çözüm kararı

### Kesinleşen kök neden

Program portal isteğini göndermeye çalışıyor; ancak canlı yanıt parse edilemediğinde veya çağrı herhangi bir nedenle başarısız olduğunda hata vermek yerine sabit/heuristic fallback fiyat üretip ekranda canlı gibi gösteriyor. Ekrandaki dört yanlış fiyatın kaynak kodundaki fallback formülüyle birebir eşleşmesi bunu doğruluyor.

İkincil teknik nedenler:

1. `text/x-component` Next.js RSC yanıtı regex ile parse ediliyor; resmi ve stabil bir JSON sözleşmesi değil.
2. Cookie/id_token kopyalama ile kullanıcı web oturumu taklit ediliyor; bu oturum geçici ve başlık/CSRF/Next.js istek bağlamına bağlı.
3. Fallback ile canlı sonuç arasında `IsLive` bilgisi taşınmıyor; UI fallback'i canlı teklif etiketiyle sunuyor.
4. HTTP status, response body, content-type ve parse başarısızlığı kaydedilmediği için gerçek hata görünmüyor.
5. Kodda resmi hesap/store API'si ile web hızlı fiyat hesaplayıcı endpoint'i ayrıştırılmamış.

### Çözüm kararı

- Fallback fiyatlar fiyat teklifi yerine kullanılamaz; varsayılan davranış canlı API başarısızsa açık hata göstermektir.
- Canlı sonuç/fallback sonucu `QuoteSource` veya `IsLive` alanıyla zorunlu ayrılacak.
- Önce portal isteğinin gerçek response body'si fixture olarak kaydedilecek ve parser düzeltilecek.
- Uzun vadede Navlungo'nun kullanıcı/store API erişimi sağlanırsa resmi OAuth/API endpoint'i tercih edilecek. Web cookie'si resmi API anahtarı gibi kalıcı çözüm olarak kullanılmayacak.
- Aynı hesap ve aynı giriş verileriyle portal sonucu ile program response'u karşılaştırılacak; sabit katsayı uygulanmayacak.

### Önceki hipotezlerin durumu

1. Çıkış posta kodu eksikliği: resmi API için hâlâ kontrol edilmesi gereken bir etkendir; fakat bu ekran görüntüsündeki hızlı hesap isteği `fromCountry`/`toCountry` ve paket alanlarıyla çalıştığı için mevcut yanlış değerlerin doğrudan sebebi değildir.
2. Decimal veya desi hesabı: `0.40 kg` ve `15x20x10 cm` girdileri portal isteğiyle aynı; `0.60` hacimsel değer doğru.
3. Kur/komisyon eklenmesi: gösterilen USD değerleri zaten fallback formülünden geliyor; ilk kök neden kur değildir.

## 4. Uygulama aşamaları

### Faz 0 — Kod ve ortam doğrulama

- Navlungo entegrasyonunun hangi branch/build içinde olduğunu kesinleştir.
- Aktif branch ile çalışan `.exe` dosyasının aynı committen üretildiğini doğrula.
- `git status`, build çıktısı ve uygulama sürüm/commit bilgisini tanılama ekranına ekle.
- Navlungo kodu yoksa entegrasyonu mevcut Etsy kargo modellerinden bağımsız yeni bir modül olarak başlat.

**Çıktı:** Kaynak kod–çalışan binary eşleşmesi ve tekrarlanabilir test ortamı.

### Faz 1 — Domain modelleri ve ayarlar

Oluşturulacak modeller:

- `NavlungoSettings`: environment, client id, client secret, redirect uri, store id, origin country, origin postal code.
- `NavlungoQuoteRequest` ve alt modelleri: order, receiver address, order items, packages.
- `NavlungoQuoteResponse`, `NavlungoQuote`, `NavlungoAdditionalService`.
- `NavlungoApiError`.

Ayarlar:

- Secret veya access token kaynak koda, git'e veya loglara yazılmayacak.
- QA ve production ayarları birbirinden tamamen ayrılacak.
- Store ID ve çıkış posta kodu açıkça gösterilecek; gizli bilgiler maskeli gösterilecek.

**Çıktı:** API sözleşmesiyle birebir eşleşen, test edilebilir DTO katmanı.

### Faz 2 — OAuth2 kimlik doğrulama

- Authorization Code akışını kullan.
- `access_token` süresi dolduğunda `refresh_token` ile yenile.
- Token yenileme sırasında eşzamanlı isteklerin birden fazla yenileme başlatmasını engelle (`SemaphoreSlim`).
- 401 sonrası yalnızca bir kez token yenile ve isteği tekrar et.
- 400/403/422 gibi iş kuralı veya yetki hatalarını körlemesine tekrar etme.
- Token değerlerini hata mesajlarından ve HTTP loglarından redakte et.

**Çıktı:** Login, token yenileme ve QA/production ayrımı doğrulanmış kimlik katmanı.

### Faz 3 — Doğru istek eşleme ve doğrulama

Uygulama alanları Navlungo'ya şu şekilde eşlenecek:

- Ülke: ISO-2 kodu, ör. `US`; API'ye gönderirken tek biçim `US` veya dokümanın beklediği biçim korunacak.
- Çıkış ülkesi: `TR`.
- Çıkış bölgesi: tercihen gerçek çıkış posta kodu.
- Hedef: alıcının ülke, eyalet, şehir, ilçe ve posta kodu.
- Ağırlık: kg, pozitif decimal.
- Ölçüler: cm, pozitif decimal.
- Paket tipi: dokümana göre `box` veya `envelope`; `box` için üç ölçü de zorunlu.
- Paket adedi: integer.
- Gönderi türü: fiziksel Etsy siparişi için `sales`.
- `orderReference`: her çağrıda benzersiz ve izlenebilir değer.
- Ürün fiyatı, açıklaması, SKU ve HS kodu `orderItems` içinde açıkça set edilecek.

Desi yalnızca yerel ön izleme için kullanılacak. Navlungo'ya desi yerine dokümanın istediği gerçek `weight`, `width`, `length`, `height` alanları gönderilecek.

**Çıktı:** JSON body'nin kaydedilebilir, maskelenmiş bir debug ön izlemesi ve validasyon hataları.

### Faz 4 — HTTP istemcisi

- Tek bir `NavlungoApiClient` oluştur.
- `HttpClientFactory`/typed client kullan.
- Timeout, cancellation token ve bağlantı hatası ayrımı ekle.
- Sadece geçici ağ/5xx hatalarında sınırlı exponential backoff uygula.
- Yanıtın HTTP durumunu, `searchId` değerini ve teklif sayısını telemetriye yaz; token veya kişisel adresleri yazma.
- 200 yanıtında boş `quotes` listesini hata değil, "teklif bulunamadı" sonucu olarak ele al.

**Çıktı:** Resmi endpoint'e giden tek ve izlenebilir API yolu.

### Faz 4.1 — Canlı/fallback ayrımını düzeltme (ilk kod değişikliği)

- `FetchLiveQuotesAsync` içinde `catch {}` kaldırılacak; hata `NavlungoApiException` veya tanılama sonucu olarak üst kata taşınacak.
- HTTP status, content-type ve response body'nin maskelenmiş tanılama özeti tutulacak.
- `NavlungoPricingResult` içine `Success`, `IsLive`, `QuoteSource`, `FailureReason` ve `RawResponseFingerprint` alanları eklenecek.
- Canlı teklif `quotes.Count == 0` ise fallback'e sessizce geçilmeyecek; UI "Navlungo canlı yanıtı parse edilemedi" gösterecek.
- Fallback yalnızca kullanıcı açıkça "Tahmini fiyatları göster" seçerse kullanılacak ve kartta `🟡 Tahmini/Yedek` etiketi görünecek.
- `AnimatedShippingComparisonDrawer` sonucu koşulsuz `IsLive = true` yapmak yerine istemciden gelen kaynağı taşıyacak.

**Beklenen sonuç:** Program artık yanlış fiyatı doğru API fiyatı gibi göstermeyecek.

### Faz 4.2 — Portal response fixture ve sağlam parser

- Aynı girişlerle portalın tam response body'si DevTools'tan alınacak; cookie, token ve kişisel bilgiler fixture'a girmeden maskelenecek.
- `text/x-component` response'un gerçek RSC kayıt yapısı incelenecek; taşıyıcı, servis, süre, fiyat ve para birimi aynı kayıt bağlamından çıkarılacak.
- Taşıyıcı adından sonraki ilk ondalık sayıyı alan gevşek regex yerine alan/komponent ilişkisine dayalı parser yazılacak.
- Portal response değiştiğinde fixture testi kırılacak; sessizce fallback fiyatına dönülmeyecek.
- Dört referans teklif için test beklentisi: `15.03`, `20.75`, `32.96`, `37.72 USD`.

### Faz 5 — Fiyatların doğru gösterimi

- Navlungo'dan gelen `price` ve `currency` ham haliyle saklanacak.
- USD → TL dönüşümü yalnızca ayrı bir sunum katmanında ve zaman damgalı kurla yapılacak.
- Navlungo fiyatına tekrar komisyon, KDV, kur veya marj eklenip eklenmediği ekranda açıkça gösterilecek.
- Her teklif için taşıyıcı, servis tipi, minimum/maksimum gün, açıklama, ek hizmetler ve `quoteReference` gösterilecek.
- Kullanıcı seçtiği teklifin ham fiyatını ve yerel maliyet hesabını ayrı ayrı görebilecek.

**Çıktı:** "Navlungo fiyatı" ile "uygulamanın toplam/kar hesabı" birbirine karışmayacak.

### Faz 6 — UI ve tanılama ekranı

Kargo paneline şu alanlar eklenecek:

- API ortamı: QA/Production
- Store ID
- Çıkış ülke/posta kodu
- Hedef ülke/eyalet/şehir/posta kodu
- Paket listesi
- Son istek zamanı, HTTP status, `searchId`
- "Ham API yanıtını göster" düğmesi; kişisel bilgiler ve tokenlar maskeli olacak
- Teklif kaynağı etiketi: `Navlungo API`, `Yerel hesap`, `Demo`

Portal karşılaştırması için aynı giriş verileriyle iki kolon kullanılacak: `Navlungo portalı` ve `Uygulama API sonucu`.

### Faz 7 — Test ve doğrulama

1. DTO serialization testleri.
2. Request mapping testleri: `0.40 kg`, `15x20x10 cm`, `US`, `TR`, çıkış posta kodu.
3. HTTP fixture testleri: başarılı yanıt, boş teklif, 400, 401, 500.
4. Token refresh ve tek seferlik 401 retry testi.
5. Para birimi/decimal testleri: `15.03` değerinin `15.03` kalması.
6. Regression fixture: dört teklifli örnek response; taşıyıcı, servis ve transit sürelerinin kaybolmadığı doğrulanacak.
7. Canlı/QA karşılaştırması: aynı hesap, aynı store, aynı origin/destination, aynı paket; sonuçlar birebir veya fark açıklamasıyla raporlanacak.

## 5. Kabul kriterleri

- Uygulama, Navlungo web sayfasını veya browser cookie'sini kullanmadan resmi API'ye bağlanıyor.
- İstek URL'si, environment ve store ID tanılama ekranında doğrulanabiliyor.
- Gönderilen JSON içinde origin, destination, package dimensions/weight ve shipment type açıkça görülebiliyor.
- Dört veya daha fazla teklifin fiyatı, para birimi, taşıyıcısı ve transit süresi doğru parse ediliyor.
- `searchId` ve `quoteReference` kaybolmuyor.
- Navlungo ham fiyatı ile uygulama içi TL/kar hesabı ayrı gösteriliyor.
- Canlı API başarısız olduğunda fallback fiyatlar otomatik olarak canlı fiyat etiketiyle gösterilmiyor.
- Test fixture'ında portal isteği ve response'u maskelenmiş şekilde bulunuyor.
- Aynı fixture ile dört referans teklif (`15.03`, `20.75`, `32.96`, `37.72 USD`) doğru parse ediliyor.
- `QuoteSource`/`IsLive` değeri UI'ya doğru taşınıyor.
- 401, 4xx, 5xx, timeout, content-type değişimi ve parse edilemeyen response durumları sessizce yedek fiyata dönüşmüyor.
- Token, cookie, client secret, alıcı telefon/e-posta/adres bilgileri loglanmıyor.
- QA ve production yanlışlıkla birbirine karışamıyor.

## 6. İlk uygulama adımı

Network görüntülerinden request eşlemesi artık elde edildi:

```json
[
  {
    "fromCountry": "TR",
    "toCountry": "US",
    "packages": [
      { "weight": 0.4, "length": 15, "width": 20, "height": 10 }
    ],
    "source": "user"
  }
]
```

Sıradaki ilk kod değişikliği, `NavlungoApiClient` içindeki sessiz fallback'i kaldırmak ve gerçek hata/response tanılamasını eklemektir. Daha sonra DevTools'tan response body alınarak parser fixture'ı tamamlanmalıdır. Sabit fiyat formülü veya katsayı ile düzeltme yapılmayacaktır.

