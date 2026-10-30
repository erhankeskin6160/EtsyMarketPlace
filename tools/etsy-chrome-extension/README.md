# 🛡️ Etsy MarketPlace Satıcı Asistanı (Copilot & VDS Köprüsü)

Bu Google Chrome Uzantısı (Manifest V3), Etsy satıcılarının VDS üzerinde açık olan mağaza oturumundan:
1. **Canlı Finans & Fon Rezervi (%75 Bloke, Net Bakiye, İnceleme Tarihi)** verilerini okur.
2. **Siparişler & Müşteri Ad/Adres** verilerini tek tıkla VDS veritabanınıza (`localhost:5263`) aktarır.
3. **%100 Güvenli ve Sıfır Ban Riski:** Etsy sunucularına arka planda bot istekleri atmaz. Tamamen siz normal oturumunuzda gezinirken ekrandaki veriyi okur ve doğrudan yerel VDS API'nize iletir.

---

## 🚀 VDS Chrome'a Nasıl Kurulur? (Yalnızca 1 Dakika)

1. VDS Chrome tarayıcınızı açın.
2. Adres çubuğuna şunu yazıp Enter'a basın:
   ```text
   chrome://extensions
   ```
3. Sağ üst köşedeki **"Geliştirici modu" (Developer mode)** anahtarını açık (mavi) konuma getirin.
4. Sol üstte beliren **"Paketlenmemiş öğe yükle" (Load unpacked)** butonuna tıklayın.
5. Açılan klasör seçim penceresinden şu dizini seçin:
   ```text
   <PROJE_KOK_DIZINI>\tools\etsy-chrome-extension
   ```
6. **Tebrikler!** Chrome sağ üst araç çubuğunda **"Etsy Copilot"** simgesi belirecektir. İğne (Pin) simgesine basarak sabitleyebilirsiniz.

---

## 🎯 Nasıl Kullanılır?

### 1. Finans & Rezerv Senkronizasyonu:
- Etsy'de **Ödeme Hesabı** (`etsy.com/your/account/payments`) sekmesini açın.
- Chrome'un sağ üstündeki **Etsy Copilot** simgesine tıklayın.
- Açılan pencerede **"Finans & Rezerv"** sekmesinde verilerin algılandığını göreceksiniz.
- **`[⚡ Finans & Rezervi VDS'e Aktar]`** butonuna basın.
- Kendi bilgisayarınızdaki Web Kokpitinde (`localhost:4200/finance/accounting`) tüm fon ve rezerv rakamları anında güncellenir!

### 2. Sipariş & Müşteri Bilgisi Aktarımı:
- Etsy'de **Siparişler** (`etsy.com/your/orders/sold`) sekmesini açın.
- Eklenti her sipariş kartının altına küçük bir **`[⚡ VDS'e Aktar]`** butonu ekler.
- Dilerseniz doğrudan o butona tıklayarak, dilerseniz eklenti penceresinden **`[📥 Müşteri & Siparişleri VDS'e Aktar]`** diyerek müşteri adı ve sipariş detaylarını anında kendi sisteminize aktarabilirsiniz.
