# 🚚 Kargo, Lojistik ve Anti-Ban

Etsy satıcılarının en büyük risklerinden biri mağaza askıya alınması (ban) ve sipariş teslimat gecikmeleridir. EtsyMarketPlace, hem lojistiği otomatikleştiren hem de algoritma cezalarını sıfırlayan özel güvenlik kalkanlarına sahiptir.

---

## 🛡️ Anti-Ban İlkeleri ve Görsel Manipülasyonu

Etsy algoritması, aynı veya benzer ürün fotoğraflarını tespit ederek arama sıralamasını düşürür veya "yeniden listeleme" cezaları uygular. Masaüstü uygulamasında entegre çalışan **SkiaSharp** motoru bu riski ortadan kaldırır:

### 1. EXIF Meta Verisi Soyma
- Kamera modeli, çekim tarihi, GPS koordinatları ve cihaz kimlik bilgileri tamamen silinir.
- Fotoğraf ham RGB piksel matrisine dönüştürülüp yeniden sıkıştırılır.

### 2. Perceptual Hash (pHash) Bozma
- Görselin pHash (algısal parmak izi) değeri, insan gözünün fark edemeyeceği mikro renk dalgalanmaları ve kenar kırpma ile değiştirilir.
- Etsy botları görseli tamamen "yeni ve benzersiz bir fotoğraf" olarak algılar.

### 3. İnsan Ritmi (Human-like Rate Limiting)
- Bot/Scraper korumasına takılmamak için toplu işlemlerde rastgele milisaniyelik gecikmeler (`jittered sleep`) uygulanır.
- Resmi API sınırlarına (`10 istek/sn`) kesinlikle uyulur.

---

## 📦 Kargo Taşıyıcı Entegrasyonları (Carriers)

Sistem üç büyük kargo sağlayıcısını destekler:

| Taşıyıcı | Protokol | Desteklenen Yetenekler |
| :--- | :--- | :--- |
| **Aras Kargo** | SOAP / XML | Yurtiçi gönderi barkodu oluşturma, otomatik takip no, durum sorgulama. |
| **Navlungo** | REST API | Yurtdışı (Express / Eco) navlun fiyat karşılaştırma, otomatik konşimento oluşturma. |
| **Shiptomore** | REST API | Uluslararası mikro ihracat etiketleri, gümrük beyannameleri, Fedex/UPS/DHL yönlendirme. |

---

## 🖨️ Termal Etiket Yazıcı Desteği (ZPL & PDF)

- Masaüstü uygulaması, USB veya Ethernet üzerinden bağlı termal etiket yazıcılarına (Zebra, Xprinter, HPRT vb.) doğrudan erişir.
- **ZPL (Zebra Programming Language):** Kargo barkodlarını çözünürlük kaybı olmadan, milisaniyeler içinde termal kağıda basar.
- **PDF Önizleme ve Baskı:** İrsaliye, fatura ve etiketleri tek ekranda toplar.
