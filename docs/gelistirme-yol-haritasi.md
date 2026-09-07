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

Durum: Uygulama içi zamanlayıcı ve haftalık rapor tamamlandı; API dayanıklılığı feature testinde

- Zamanlanmış veri yenileme
- API hız limiti ve yeniden deneme kuyruğu
- Haftalık HTML/CSV raporları
- Fiyat ve performans uyarıları

İlk sürüm uygulama açıkken ayarlanan saat aralığında kendi mağaza verisini yeniler, günlük SQLite snapshot kaydeder ve seçilen klasöre HTML/CSV raporu üretir. Sipariş ve ciro düşüş eşikleri kullanıcı tarafından ayarlanabilir. Ortak HTTP katmanı istekleri sıraya alır; güvenli GET çağrılarında `429`, `408`, geçici `5xx` ve ağ hatalarını `Retry-After` veya kademeli beklemeyle yeniden dener. Kalıcı `4xx` ve OAuth POST çağrıları otomatik tekrarlanmaz. Windows kapalıyken çalışacak görev desteği sonraki feature parçasında geliştirilecektir.

## Aşama 7 - Yapay zeka destekli optimizasyon

- Başlık, etiket ve açıklama taslakları
- Önce/sonra SEO karşılaştırması
- Marka ve telif riski uyarıları
- Kullanıcı onaylı çalışma akışı

## Asama 6 ek not - Windows Gorev Zamanlayici

Durum: Gelistiriliyor

- Ayni EXE `--automation-run` parametresiyle formsuz calisabilir.
- Otomasyon ekrani Windows Task Scheduler uzerinde `EtsyMarketPlace-Automation` gorevini olusturur.
- Gorev zamaninda acilir, raporu uretir, log yazar ve kapanir.
- VDS acik oldugu surece uygulama ekrani kapali olsa bile calisabilir; VDS kapaliysa calisamaz.
- Arka plan logu `%LocalAppData%/EtsyMarketPlace/automation-headless.log` konumundadir.

## Asama 7 ek not - Yapay zeka destekli optimizasyon

Durum: Ilk calisma alani feature dalinda gelistirildi

- Secili listing icin baslik onerileri uretilir.
- Etsy tag onerileri 13 alan ve 20 karakter siniri dikkate alinarak hazirlanir.
- Aciklama taslagi ve SEO once/sonra puani gosterilir.
- Marka/telif riski olabilecek kelimeler icin kontrol uyarisi verilir.
- Ilk surum dis AI API kullanmaz; offline kural motoru olarak calisir.
- Optimizasyon sonucu versiyon olarak SQLite gecmisine kaydedilir.
- Gecmis ekraninda once/sonra SEO puani, hedef kelime, onerilen baslik, tagler,
  aciklama taslagi ve risk uyarilari incelenebilir.
- OpenAI provider altyapisi eklendi; ayar yoksa offline motor calisir, ayar varsa
  `AI ile Uret` butonu gercek Responses API cagrisi yapar.
- Sonraki parca, AI ciktilari icin kalite puanlama ve maliyet/kullanim logu olabilir.

## Asama 7 ek not - Kendi magaza listing AI denetimi

Durum: Feature dalinda gelistirildi

- Kendi magaza aktif listingleri resimleriyle birlikte yuklenir.
- Listing bazinda yerel SEO puani, AI puani, fiyat, stok, favori ve tag sayisi gorulur.
- Dusuk puanli listingler once siralanir; kullanici secili listing icin AI onerisi alir.
- Oneriler optimizasyon gecmisine versiyon olarak kaydedilebilir.
- `Kendi Magazam` ekranindan `Listing AI` butonuyla acilir.
- Canli Etsy metin guncellemesi `listings_w`, final onay penceresi ve gecmis kaydiyla ele alindi.
- AI ayarlarinda Gemini, Claude ve Platform Token secenekleri gorunur.
- Gemini adapteri aktif hale getirildi; Google AI Studio API key ile Gemini
  uzerinden baslik, tag, aciklama ve risk uyarisi uretilebilir.
- Claude ve platform token akisi; kullanim maliyeti, kota ve odeme akisindan sonra acilacak.

## Asama 7 ek not - listings_w ile onayli guncelleme

Durum: Feature dalinda gelistirildi

- Etsy OAuth kapsamlarina `listings_w` eklendi.
- AI denetim ekranindaki `Etsy'de Guncelle` butonu secili listing icin
  onerilen baslik, tag ve aciklamayi canli Etsy listingine yazar.
- Guncelleme oncesinde mevcut metin ve yeni metin yan yana gosterilir.
- Kullanici onay kutusunu isaretlemeden Etsy'ye yazma yapilmaz.
- Guncelleme basarili olursa optimizasyon versiyonu gecmise kaydedilir.
- AI promptu listing metnini Etsy SEO/GEO arama niyetine uygun Ingilizce
  hazirlayacak sekilde guncellendi; risk notlari Turkce tutulur.
- AI gorsel uretme, gorsel onaylama ve listing gorseli olarak yukleme sonraki
  feature parcasidir.

## Asama 7 ek not - AI gorsel uretme ve listing gorseli ekleme

Durum: Feature dalinda gelistirildi

- Listing AI ekranina urun adi/tag arama filtresi eklendi.
- SEO eksikleri ve artilari ayri kolonlarda gosterilir.
- `AI Sonrasi Yenile` secili listingi Etsy'den tekrar yukler.
- `AI Gorsel` penceresi secili listing icin OpenAI gorsel modeliyle gorsel uretir.
- Kullanici AI gorseli onizler veya bilgisayardan gorsel secer.
- Gorsel, kullanici son onay vermeden Etsy listing'e yuklenmez.
- Gorsel uretim promptu marka/telif/logolu gorsel riskini azaltacak sekilde yazildi.
