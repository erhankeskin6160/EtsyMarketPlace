// Etsy Copilot v2.7.0 — Web App <-> Chrome Extension Köprüsü
// Web panelinden (localhost:4300, localhost:5263, 5.180.81.148:5263) gelen varyasyon çekme isteklerini
// Chrome Extension Service Worker'a iletir ve sonucu window.postMessage ile geri döndürür.

(function () {
  'use strict';

  const BRIDGE_VERSION = '2.7.0';

  function announceReady() {
    try {
      window.postMessage({ type: 'ETSY_COPILOT_BRIDGE_READY', version: BRIDGE_VERSION }, '*');
    } catch {}
  }

  window.addEventListener('message', async (event) => {
    if (event.source !== window || !event.data || typeof event.data !== 'object') return;

    const msg = event.data;
    if (msg.type === 'ETSY_COPILOT_BRIDGE_PING') {
      announceReady();
      return;
    }

    if (msg.type === 'ETSY_COPILOT_FETCH_VARIATIONS_REQ') {
      const requestId = msg.requestId;
      try {
        const response = await chrome.runtime.sendMessage({
          action: 'SW_FETCH_LISTING_VARIATIONS',
          listingId: msg.listingId,
          listingUrl: msg.listingUrl
        });

        window.postMessage({
          type: 'ETSY_COPILOT_FETCH_VARIATIONS_RES',
          requestId,
          ok: Boolean(response?.ok),
          inventory: response?.inventory || null,
          error: response?.error || null
        }, '*');
      } catch (err) {
        window.postMessage({
          type: 'ETSY_COPILOT_FETCH_VARIATIONS_RES',
          requestId,
          ok: false,
          inventory: null,
          error: err?.message || 'Eklenti köprüsüne erişilemedi.'
        }, '*');
      }
    }
  });

  announceReady();
})();
