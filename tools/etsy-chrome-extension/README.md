# Etsy MarketPlace Satıcı Asistanı (Copilot v2.0.0)

Etsy Mağazası (`etsy.com/your/orders/*` ve `etsy.com/your/account/payments`) ile Merkezi VDS Sunucusu arasında **sıfır bot riskiyle** çalışan 4 katmanlı Manifest V3 Chrome uzantısı.

## Mimari Katmanlar (`v2.0.0`)

1. **Katman 1 — `src/page/react-reader.main.js` (`MAIN` World Salt-Okunur Bellek Okuyucu):**
   - `window.fetch` veya `XMLHttpRequest` prototiplerine dokunmaz (DataDome %100 güvenli).
   - Etsy React 16/17/18 Fiber ağacını (`__reactFiber$*` / `__reactProps$*` / Redux Store) ve `window.Etsy.Context` ülke tablosunu doğrudan JS belleğinden okur.
   - Chrome Google Çeviri (`translated-ltr` / `<font>`) metin değişimlerinden %100 bağışıktır.
2. **Katman 2 — `src/content/content-agent.js` (`ISOLATED` World Yapısal DOM & Accordion Ajanı):**
   - İngilizce/Türkçe `"Ship to"` başlığına bağımlı olmadan DOM sırası, `aria-controls` ve yapısal dışlama ile sağ kargo sütununu izole eder.
   - `requestAnimationFrame` gruplarıyla kapalı satır içi adres panellerini nazikçe genişletir ve yan sipariş çekmecesini (`Drawer`) canlı izler.
3. **Katman 3 — `src/background/service-worker.js` & `vds-api-client.js` (Durumsuz MV3 Orkestratör):**
   - Eski kirli önbellekleri temizler; `livePageOrders` ile `verifiedArchive` verisini ayrı tutar.
   - VDS senkronizasyonlarını (`/api/etsy/orders/sync-from-extension`, `/api/etsy/financial/payment-reserve-snapshot`, `/api/etsy/orders/ai-parse-address`) arka planda kesintisiz yürütür.
4. **Katman 4 — `src/popup/*` (Reaktif Koyu Tema Kokpit & Canlı Tanılama):**
   - Sıfır sabit (mock) veri, veri katmanı rozetleri (`⚡ React Bellek` / `🧩 Yapısal DOM` / `🤖 DeepSeek AI`) ve tek tıkla `🔬 Tanılama` JSON kopyalama.

## Kurulum / Güncelleme
1. Chrome'da `chrome://extensions` adresini açın ve sağ üstten **Geliştirici modu**nu etkinleştirin.
2. Mevcut uzantı kartında **Yenile (↻)** simgesine tıklayın (veya **Paketlenmemiş öğe yükle** ile `etsy-chrome-extension` klasörünü seçin) ve sürümün **`2.0.0`** olduğunu doğrulayın.
