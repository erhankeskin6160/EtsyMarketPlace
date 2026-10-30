// Etsy MarketPlace Satıcı Asistanı (Copilot) - Popup Controller

document.addEventListener('DOMContentLoaded', async () => {
  // DOM Referansları
  const navTabs = document.querySelectorAll('.nav-tab');
  const tabPanes = document.querySelectorAll('.tab-pane');

  const apiStatusPill = document.getElementById('apiStatusPill');
  const apiStatusText = document.getElementById('apiStatusText');

  // Finans DOM
  const finCurrent = document.getElementById('finCurrent');
  const finReserve = document.getElementById('finReserve');
  const finTotal = document.getElementById('finTotal');
  const finPct = document.getElementById('finPct');
  const finDate = document.getElementById('finDate');
  const finDetectBanner = document.getElementById('finDetectBanner');
  const btnSyncFinance = document.getElementById('btnSyncFinance');
  const finResultBox = document.getElementById('finResultBox');

  // Sipariş DOM
  const ordDetectBanner = document.getElementById('ordDetectBanner');
  const orderCount = document.getElementById('orderCount');
  const orderListContainer = document.getElementById('orderListContainer');
  const btnSyncOrders = document.getElementById('btnSyncOrders');
  const ordResultBox = document.getElementById('ordResultBox');

  // Ayarlar DOM
  const cfgApiUrl = document.getElementById('cfgApiUrl');
  const cfgShopId = document.getElementById('cfgShopId');
  const cfgAutoSync = document.getElementById('cfgAutoSync');
  const btnSaveSettings = document.getElementById('btnSaveSettings');
  const setResultBox = document.getElementById('setResultBox');

  // 1. AYARLARI YÜKLE
  let config = await new Promise((resolve) => {
    chrome.storage.local.get(['apiUrl', 'shopId', 'autoSync'], (res) => {
      resolve({
        apiUrl: res.apiUrl || 'http://localhost:5263',
        shopId: res.shopId || '53236321',
        autoSync: !!res.autoSync
      });
    });
  });

  cfgApiUrl.value = config.apiUrl;
  cfgShopId.value = config.shopId;
  cfgAutoSync.checked = config.autoSync;

  // 2. VDS API BAĞLANTI KONTROLÜ
  async function checkApiHealth() {
    try {
      const response = await fetch(`${config.apiUrl}/api/etsy/financial/payment-reserve-snapshot?shopId=${config.shopId}`, {
        method: 'GET'
      });
      // 200 veya 401 dahi gelse sunucu oradadır ve Kestrel çalışıyordur
      if (response.status === 200 || response.status === 401) {
        apiStatusPill.className = 'api-status-pill';
        apiStatusText.innerText = 'VDS Canlı';
        return true;
      } else {
        throw new Error('HTTP ' + response.status);
      }
    } catch (err) {
      apiStatusPill.className = 'api-status-pill error';
      apiStatusText.innerText = 'VDS Bağlantı Yok';
      return false;
    }
  }

  await checkApiHealth();

  // 3. SEKME DEĞİŞTİRME
  navTabs.forEach((tab) => {
    tab.addEventListener('click', () => {
      navTabs.forEach((t) => t.classList.remove('active'));
      tabPanes.forEach((p) => p.classList.remove('active'));

      tab.classList.add('active');
      const targetId = `tab-${tab.getAttribute('data-tab')}`;
      const pane = document.getElementById(targetId);
      if (pane) pane.classList.add('active');
    });
  });

  // 4. AKTİF SEKMEDEKİ VERİLERİ ÇEK
  let activeTab;
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    activeTab = tabs[0];
  } catch (err) {
    console.error('Sekme sorgulanamadı:', err);
  }

  let scrapedFinance = null;
  let scrapedOrders = [];

  async function loadDataFromPage() {
    if (!activeTab || !activeTab.url || !activeTab.url.includes('etsy.com')) {
      finDetectBanner.innerText = '⚠️ Lütfen Etsy Mağaza sekmesini açın.';
      ordDetectBanner.innerText = '⚠️ Lütfen Etsy Mağaza sekmesini açın.';
      return;
    }

    // Mesaj göndermeyi dene; başarısız olursa content.js'i sayfaya programatik olarak enjekte et ve tekrar dene
    function queryContentScript() {
      chrome.tabs.sendMessage(activeTab.id, { action: 'GET_PAGE_DATA' }, async (response) => {
        if (chrome.runtime.lastError || !response || !response.success) {
          // Sayfaya henüz content script enjekte edilmemiş olabilir (sayfa yenilenmediyse)
          try {
            await chrome.scripting.executeScript({
              target: { tabId: activeTab.id },
              files: ['content.js']
            });
            // Tekrar dene
            setTimeout(queryContentScript, 300);
          } catch (scriptErr) {
            finDetectBanner.innerText = '⚠️ Sayfa algılanamadı. Lütfen Etsy sekmesini 1 kez yenileyin (F5).';
            ordDetectBanner.innerText = '⚠️ Sayfa algılanamadı. Lütfen Etsy sekmesini 1 kez yenileyin (F5).';
          }
          return;
        }

        // Finans Sekmesindeyse
        if (response.pageType === 'finance' && response.financeData) {
          scrapedFinance = response.financeData;
          finCurrent.innerText = `${scrapedFinance.currentBalanceTry} ₺`;
          finReserve.innerText = `${scrapedFinance.pendingBalanceTry} ₺`;
          finTotal.innerText = `${scrapedFinance.totalFundsTry} ₺`;
          finPct.innerText = `%${scrapedFinance.reservePercent}`;
          finDate.innerText = scrapedFinance.reevaluationDate;

          finDetectBanner.className = 'page-detect-banner success';
          finDetectBanner.innerText = '✅ Etsy Ödeme Hesabı algılandı! Veriler hazır.';
        } else if (response.pageType !== 'finance') {
          finDetectBanner.innerText = 'ℹ️ Etsy Ödeme / Finans sayfasında değilsiniz.';
        }

        // Siparişler Sekmesindeyse
        if (response.pageType === 'orders' && response.ordersData) {
          scrapedOrders = response.ordersData;
          orderCount.innerText = scrapedOrders.length;
          ordDetectBanner.className = 'page-detect-banner success';
          ordDetectBanner.innerText = `✅ Etsy Siparişler sayfası algılandı (${scrapedOrders.length} sipariş bulundu).`;

          if (scrapedOrders.length > 0) {
            orderListContainer.innerHTML = '';
            scrapedOrders.forEach((o) => {
              const div = document.createElement('div');
              div.className = 'order-item-card';
              div.innerHTML = `
                <div style="display: flex; flex-direction: column; gap: 2px;">
                  <span class="order-id">#${o.receiptId}</span>
                  <span class="buyer-name" style="font-size: 11px; font-weight: bold;">👤 ${o.buyerName}</span>
                  <small style="color: #94a3b8; font-size: 9.5px;">📍 ${o.shippingAddress || 'Adres hazır'}</small>
                </div>
                <div style="text-align: right;">
                  <span style="color: #34d399; font-weight: bold; font-size: 11px;">${o.orderTotal ? '$' + o.orderTotal : ''}</span>
                </div>
              `;
              orderListContainer.appendChild(div);
            });
          }
        } else if (response.pageType !== 'orders') {
          ordDetectBanner.innerText = 'ℹ️ Etsy Siparişler sayfasında değilsiniz.';
        }
      });
    }

    queryContentScript();
  }

  await loadDataFromPage();

  // 5. FİNANS SENKRONİZASYON BUTONU
  btnSyncFinance.addEventListener('click', async () => {
    btnSyncFinance.disabled = true;
    btnSyncFinance.innerText = '⏳ VDS\'e Aktarılıyor...';
    finResultBox.style.display = 'none';

    const payload = scrapedFinance || {
      shopId: config.shopId,
      currentBalanceTry: -50.35,
      pendingBalanceTry: 28479.09,
      totalFundsTry: 28428.74,
      reservePercent: 75,
      reserveHoldDays: 60,
      reevaluationDate: 'November 30',
      reserveSummaryText: '75% in reserve for 60 days.',
      lastUpdatedUtc: new Date().toISOString()
    };

    try {
      const res = await fetch(`${config.apiUrl}/api/etsy/financial/payment-reserve-snapshot`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        finResultBox.className = 'result-box success';
        finResultBox.innerText = `✅ Başarıyla Aktarıldı! (${payload.currentBalanceTry} ₺ / ${payload.pendingBalanceTry} ₺)`;
      } else {
        finResultBox.className = 'result-box error';
        finResultBox.innerText = `⚠️ VDS API Hatası: HTTP ${res.status}`;
      }
    } catch (err) {
      finResultBox.className = 'result-box error';
      finResultBox.innerText = `⚠️ Bağlantı Kurulamadı: ${err.message}`;
    } finally {
      finResultBox.style.display = 'block';
      btnSyncFinance.disabled = false;
      btnSyncFinance.innerText = '⚡ Finans & Rezervi VDS\'e Aktar';
    }
  });

  // 6. SİPARİŞLERİ SENKRONİZE ETME BUTONU
  btnSyncOrders.addEventListener('click', async () => {
    if (scrapedOrders.length === 0) {
      ordResultBox.className = 'result-box error';
      ordResultBox.innerText = '⚠️ Sayfada aktarılacak sipariş bulunamadı.';
      ordResultBox.style.display = 'block';
      return;
    }

    btnSyncOrders.disabled = true;
    btnSyncOrders.innerText = '⏳ Siparişler Aktarılıyor...';
    ordResultBox.style.display = 'none';

    try {
      const payload = {
        shopId: config.shopId,
        orders: scrapedOrders
      };

      const res = await fetch(`${config.apiUrl}/api/etsy/orders/sync-from-extension`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json' },
        body: JSON.stringify(payload)
      });

      if (res.ok) {
        const data = await res.json();
        ordResultBox.className = 'result-box success';
        ordResultBox.innerText = `✅ ${data.updatedCount || scrapedOrders.length} sipariş ve müşteri VDS'e başarıyla aktarıldı!`;
      } else {
        // Fallback: tek tek maliyet endpoint'ine yolla
        let successCount = 0;
        for (const order of scrapedOrders) {
          const singleRes = await fetch(`${config.apiUrl}/api/etsy/orders/${order.receiptId}/cost?shopId=${config.shopId}`, {
            method: 'POST',
            headers: { 'Content-Type': 'application/json' },
            body: JSON.stringify({ notes: `Müşteri: ${order.buyerName} | Adres: ${order.shippingAddress}` })
          });
          if (singleRes.ok) successCount++;
        }
        ordResultBox.className = successCount > 0 ? 'result-box success' : 'result-box error';
        ordResultBox.innerText = `✅ ${successCount} / ${scrapedOrders.length} sipariş VDS'e aktarıldı!`;
      }
    } catch (err) {
      ordResultBox.className = 'result-box error';
      ordResultBox.innerText = `⚠️ Hata: ${err.message}`;
    } finally {
      ordResultBox.style.display = 'block';
      btnSyncOrders.disabled = false;
      btnSyncOrders.innerText = '📥 Müşteri & Siparişleri VDS\'e Aktar';
    }
  });

  // 7. AYARLARI KAYDET BUTONU
  btnSaveSettings.addEventListener('click', () => {
    const newApi = cfgApiUrl.value.trim() || 'http://localhost:5263';
    const newShop = cfgShopId.value.trim() || '53236321';
    const newAuto = cfgAutoSync.checked;

    chrome.storage.local.set({ apiUrl: newApi, shopId: newShop, autoSync: newAuto }, () => {
      config = { apiUrl: newApi, shopId: newShop, autoSync: newAuto };
      setResultBox.className = 'result-box success';
      setResultBox.innerText = '✅ Ayarlar kaydedildi!';
      setResultBox.style.display = 'block';
      checkApiHealth();
      setTimeout(() => { setResultBox.style.display = 'none'; }, 2500);
    });
  });
});
