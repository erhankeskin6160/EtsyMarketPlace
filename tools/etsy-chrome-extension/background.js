// Etsy MarketPlace Satıcı Asistanı (Copilot) - Service Worker (Background)

chrome.runtime.onInstalled.addListener(() => {
  console.log('🚀 Etsy MarketPlace Copilot uzantısı başarıyla yüklendi.');
  // Varsayılan ayarları kaydet
  chrome.storage.local.get(['apiUrl', 'shopId', 'autoSync'], (res) => {
    if (!res.apiUrl) {
      chrome.storage.local.set({
        apiUrl: 'http://localhost:5263',
        shopId: '53236321',
        autoSync: false
      });
    }
  });
});

// Arka plan mesajlaşma yönlendiricisi
chrome.runtime.onMessage.addListener((request, sender, sendResponse) => {
  if (request.action === 'PING') {
    sendResponse({ status: 'PONG' });
    return true;
  }

  // VDS API Proxy Çağrısı (Gerekirse)
  if (request.action === 'PROXY_FETCH') {
    fetch(request.url, request.options)
      .then(async (res) => {
        const text = await res.text();
        let data;
        try {
          data = JSON.parse(text);
        } catch {
          data = text;
        }
        sendResponse({ success: res.ok, status: res.status, data });
      })
      .catch((err) => {
        sendResponse({ success: false, error: err.message });
      });
    return true; // Asenkron cevap için
  }

  return true;
});
