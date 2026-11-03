# 🤖 Yapay Zeka ve Pazar Araştırması

EtsyMarketPlace, **DeepSeek V3 / V4-Flash** ve **Google Gemini Flash** gibi son teknoloji geniş dil modellerini (LLM) mağaza operasyonlarının merkezine yerleştirir.

---

## 🏷️ Kutsal 13 Etiket Kuralı (13 Tags Rule)

Etsy algoritmasında ürünün bulunabilirliğini belirleyen en temel unsur **13 etiketin tamamının doldurulmasıdır**.

Sistem aşağıdaki kurallara göre etiket üretir ve denetler:
1. **Tam 13 Etiket:** Asla 12 veya 14 olamaz; her zaman 13 adet hedeflenmiş etiket oluşturulur.
2. **Karakter Sınırı:** Her etiket en fazla 20 karakter uzunluğunda olabilir.
3. **Çoklu Kelime (Long-tail):** Tek kelimelik genel etiketler (örn. `gift`, `art`) yerine en az iki kelimelik dönüşüm oranı yüksek tamlamalar (örn. `retro anime prop`, `custom 3d print`) tercih edilir.
4. **Tekrarsızlık:** Başlıkta ve etiketlerde gereksiz kelime tekrarından kaçınılır.

---

## 🔍 Pazar Araştırma Modülleri

### 1. Viral 3D Model Avcısı (`Viral3DModelHunter`)
- Thingiverse, Printables, Cults3D ve MakerWorld gibi 3D model platformlarındaki trend tasarımları tarar.
- Etsy'deki arama hacimleri ve rekabet oranlarıyla çapraz eşleştirme yaparak yüksek kâr marjlı ürün fırsatlarını listeler.

### 2. Rakip Mağaza Casusu (`CompetitorSpy`)
- Hedef rakip mağazanın en çok satan ürünlerini, fiyatlandırma stratejilerini, haftalık tahmini sipariş adetlerini ve kullandıkları anahtar kelimeleri analiz eder.

### 3. A/B Test Laboratuvarı (`AbTestExperiment`)
- Aynı ürün için iki farklı ana görsel veya başlık/etiket kombinasyonu tanımlanır.
- Günlük görüntülenme, sepete ekleme ve dönüşüm oranları (Conversion Rate) izlenerek en karlı varyasyon tespit edilir.

### 4. AI Destekli İlan Oluşturucu (`FastListingCreator`)
- Ürün fotoğrafı veya 3D model adı verildiğinde; SEO uyumlu başlık, satış odaklı açıklama metni, materyal bilgisi ve 13 etiketi saniyeler içinde üretir.
- Tek tıkla resmi Etsy API üzerinden taslak (draft) veya aktif ilan olarak yayınlar.
