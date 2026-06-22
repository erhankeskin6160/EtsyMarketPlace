# Geliştirme Yol Haritası

## Aşama 0 - Pazar araştırma temeli

Durum: Tamamlandı

- Etsy ürün arama
- Mağaza ve listing bağlantıları
- Fiyat, favori, görüntülenme ve etiketler
- Ürün görselleri
- SEO ve pazar puanı
- CSV aktarımı ve API ayarları

## Aşama 1 - Rakip mağaza analizi

Durum: İlk sürüm tamamlandı, canlı API kabul testi yapılıyor

- Ayrı sekmeli analiz formu
- Mağaza KPI değerleri
- İlk 50 aktif ürün
- Fiyat istatistikleri
- En güçlü ürünler
- Etiket ve başlık kelime frekansları
- Taksonomi dağılımı
- Rakip güç sinyali
- CSV aktarımı

## Aşama 2 - Anahtar kelime ve fırsat analizi

Durum: İlk sürüm feature dalında geliştirildi, canlı API ve arayüz kabul testi bekleniyor

- Arama sonuç yoğunluğu
- Minimum, maksimum, ortalama ve medyan fiyat
- Sık kullanılan etiketler
- Başlık kelime frekansları
- Long-tail anahtar kelime önerileri
- Rekabet, talep sinyali ve fırsat puanı
- Anahtar kelimeleri yan yana karşılaştırma
- Araştırma sonuçlarını CSV ve HTML olarak dışa aktarma

İlk sürümde CSV aktarımı tamamlandı. HTML raporu, feature kabul testinden sonra ayrı bir geliştirme olarak ele alınacak.

## Aşama 3 - Takip ve geçmiş veriler

Durum: İlk sürüm feature dalında geliştirildi, kullanıcı kabul testi bekleniyor

- Ürün, mağaza ve anahtar kelime takip listeleri
- SQLite veri tabanı
- Tarihli veri anları
- Fiyat, favori, görüntülenme ve mağaza satışı değişimleri
- Anahtar kelime rekabet, talep ve fırsat değişimleri
- Son iki snapshot karşılaştırması
- Takip geçmişini CSV olarak dışa aktarma

Yerel veri tabanı `%LocalAppData%/EtsyMarketPlace/market-tracking.db` konumunda tutulur ve Git deposuna eklenmez. Aynı öğeyi yeniden takibe eklemek yeni öğe oluşturmaz; yeni tarihli snapshot kaydeder.

## Aşama 4 - Kontrol paneli ve grafikler

Durum: İlk sürüm feature dalında geliştirildi, kullanıcı kabul testi bekleniyor

- KPI özeti
- Yükselen ürün ve etiketler
- Fiyat ve performans grafikleri
- Kritik değişim uyarıları

İlk sürüm; takip sayıları, snapshot toplamı, anahtar kelime fırsatları, en büyük değişimler ve seçili takip trendini içerir. Ürün trendinde favori, mağazada toplam satış, anahtar kelimede fırsat puanı kullanılır. Dashboard yalnızca yerel SQLite verisini okur ve açılışta Etsy API kotası tüketmez.

## Aşama 5 - Kendi mağaza performansı

Durum: Temel mağaza performansı ve dönem karşılaştırması tamamlandı; kalıcı performans geçmişi feature testinde

- Etsy OAuth ile mağaza bağlantısı
- `shops_r`, `listings_r` ve `transactions_r` kapsamları
- Kendi ürünlerinde kesin sipariş adedi ve ciro
- Dönem ve ürün performansı karşılaştırması

İlk sürüm; OAuth kullanıcısına ait mağazayı bulur, seçilen tarih aralığındaki ödenmiş ve iptal edilmemiş siparişleri getirir. Sipariş, satılan adet, brüt ciro, ortalama sipariş ve ürün bazlı satış/ciro tablosu gösterilir. Dönem karşılaştırması seçilen aralığı hemen önceki eşit uzunluktaki dönemle karşılaştırır; KPI ve ürün bazında önceki değer, fark ve yüzde değişimi sunar. Seçilen dönem raporları ve ürün kırılımları SQLite üzerinde günlük snapshot olarak saklanır; aynı mağaza, dönem ve gün tekrar alındığında kayıt güncellenir.

## Aşama 6 - Otomasyon ve raporlama

Durum: Uygulama içi zamanlayıcı ve haftalık rapor ilk sürümü feature testinde

- Zamanlanmış veri yenileme
- API hız limiti ve yeniden deneme kuyruğu
- Haftalık HTML/CSV raporları
- Fiyat ve performans uyarıları

İlk sürüm uygulama açıkken ayarlanan saat aralığında kendi mağaza verisini yeniler, günlük SQLite snapshot kaydeder ve seçilen klasöre HTML/CSV raporu üretir. Sipariş ve ciro düşüş eşikleri kullanıcı tarafından ayarlanabilir. API retry/rate-limit kuyruğu ile Windows kapalıyken çalışacak görev desteği sonraki feature parçalarında geliştirilecektir.

## Aşama 7 - Yapay zeka destekli optimizasyon

- Başlık, etiket ve açıklama taslakları
- Önce/sonra SEO karşılaştırması
- Marka ve telif riski uyarıları
- Kullanıcı onaylı çalışma akışı
