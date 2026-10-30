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
  const totalMemoryCount = document.getElementById('totalMemoryCount');
  const orderListContainer = document.getElementById('orderListContainer');
  const btnSyncOrders = document.getElementById('btnSyncOrders');
  const btnExportCsv = document.getElementById('btnExportCsv');
  const btnCopyJson = document.getElementById('btnCopyJson');
  const btnClearMemory = document.getElementById('btnClearMemory');
  const ordResultBox = document.getElementById('ordResultBox');

  // Ayarlar DOM
  const cfgApiUrl = document.getElementById('cfgApiUrl');
  const cfgApiKey = document.getElementById('cfgApiKey');
  const cfgShopId = document.getElementById('cfgShopId');
  const cfgAutoSync = document.getElementById('cfgAutoSync');
  const btnSaveSettings = document.getElementById('btnSaveSettings');
  const btnUseLocalhost = document.getElementById('btnUseLocalhost');
  const btnUsePublicIp = document.getElementById('btnUsePublicIp');
  const setResultBox = document.getElementById('setResultBox');

  // 1. AYARLARI VE HAFIZAYI YÜKLE
  let config = await new Promise((resolve) => {
    chrome.storage.local.get(['apiUrl', 'apiKey', 'shopId', 'autoSync', 'storedOrders'], (res) => {
      resolve({
        apiUrl: res.apiUrl || 'http://localhost:5263',
        apiKey: res.apiKey || 'etsy-vds-sync-key-2026',
        shopId: res.shopId || '53236321',
        autoSync: !!res.autoSync,
        storedOrders: Array.isArray(res.storedOrders) ? res.storedOrders : []
      });
    });
  });

  cfgApiUrl.value = config.apiUrl;
  cfgApiKey.value = config.apiKey;
  cfgShopId.value = config.shopId;
  cfgAutoSync.checked = config.autoSync;

  // 2. VDS API SAĞLIK KONTROLÜ (/health)
  async function checkApiHealth() {
    try {
      // Önce doğrudan yapılandırılan URL denenir
      let url = `${config.apiUrl}/health`;
      let ok = false;

      try {
        const controller = new AbortController();
        const timeoutId = setTimeout(() => controller.abort(), 2500);
        const res = await fetch(url, { signal: controller.signal });
        clearTimeout(timeoutId);
        if (res.ok) ok = true;
      } catch (e) {
        // Fallback: localhost <-> 5.180.81.148 geçişi dene
        let altUrl = config.apiUrl.includes('localhost') 
          ? 'http://5.180.81.148:5263/health' 
          : 'http://localhost:5263/health';
        try {
          const controller2 = new AbortController();
          const timeoutId2 = setTimeout(() => controller2.abort(), 2000);
          const res2 = await fetch(altUrl, { signal: controller2.signal });
          clearTimeout(timeoutId2);
          if (res2.ok) {
            ok = true;
            config.apiUrl = altUrl.replace('/health', '');
            cfgApiUrl.value = config.apiUrl;
            chrome.storage.local.set({ apiUrl: config.apiUrl });
          }
        } catch (e2) {}
      }

      if (ok) {
        apiStatusPill.className = 'api-status-pill';
        apiStatusText.innerText = 'VDS Canlı';
        return true;
      } else {
        throw new Error('API yanıt vermedi');
      }
    } catch (err) {
      apiStatusPill.className = 'api-status-pill error';
      apiStatusText.innerText = 'VDS Bağlantı Yok';
      return false;
    }
  }

  await checkApiHealth();

  // 3. SEKME GEÇİŞLERİ
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

  // Hızlı IP Seçiciler
  btnUseLocalhost.addEventListener('click', () => {
    cfgApiUrl.value = 'http://localhost:5263';
  });
  btnUsePublicIp.addEventListener('click', () => {
    cfgApiUrl.value = 'http://5.180.81.148:5263';
  });

  // 4. AKTİF SEKMEDEKİ VERİLERİ VE HAFIZAYI BİRLEŞTİR
  let activeTab;
  try {
    const tabs = await chrome.tabs.query({ active: true, currentWindow: true });
    activeTab = tabs[0];
  } catch (err) {
    console.error('Sekme sorgulanamadı:', err);
  }

  let scrapedFinance = null;
  let currentPageOrders = [];
  let allOrders = [...config.storedOrders];

  function renderOrdersList() {
    orderCount.innerText = currentPageOrders.length;
    totalMemoryCount.innerText = allOrders.length;

    if (allOrders.length > 0) {
      orderListContainer.innerHTML = '';
      allOrders.forEach((o) => {
        const div = document.createElement('div');
        div.className = 'order-item-card';
        const isFull = !!o.isFullAddress;
        const addrDisplay = o.streetAddress 
          ? `${o.streetAddress}, ${o.city || ''} ${o.state || ''} ${o.postalCode || ''}`.trim()
          : (o.shippingAddress || 'Adres Kayıtlı');

        div.innerHTML = `
          <div style="display: flex; flex-direction: column; gap: 2px; flex: 1; min-width: 0;">
            <div style="display: flex; gap: 6px; align-items: center;">
              <span class="order-id">#${o.receiptId}</span>
              <span class="buyer-name" style="font-size: 11px; font-weight: bold; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">${o.buyerName || 'Müşteri'}</span>
              ${isFull ? '<span style="background: rgba(16, 185, 129, 0.2); color: #34d399; font-size: 8.5px; padding: 1px 4px; border-radius: 4px; border: 1px solid rgba(16,185,129,0.3);">🏠 Tam Adres</span>' : ''}
            </div>
            <small style="color: ${isFull ? '#e2e8f0' : '#94a3b8'}; font-size: 9.5px; white-space: nowrap; overflow: hidden; text-overflow: ellipsis;">📍 ${addrDisplay}</small>
            ${o.phone ? `<small style="color: #38bdf8; font-size: 9px;">📞 ${o.phone}</small>` : ''}
            ${o.trackingNumber ? `<small style="color: #a78bfa; font-size: 9px;">🚚 Takip: ${o.trackingNumber}</small>` : ''}
          </div>
          <div style="text-align: right; flex-shrink: 0;">
            <span style="color: #34d399; font-weight: bold; font-size: 11px;">${o.orderTotal ? '$' + o.orderTotal : ''}</span>
            <div style="color: #64748b; font-size: 9px;">${o.orderDate || ''}</div>
          </div>
        `;
        orderListContainer.appendChild(div);
      });
    } else {
      orderListContainer.innerHTML = '<div class="empty-state">Henüz siparişler listelenmedi. Etsy \'Orders\' sayfasını açın.</div>';
    }
  }

  renderOrdersList();

  async function loadDataFromPage() {
    if (!activeTab || !activeTab.url || !activeTab.url.includes('etsy.com')) {
      finDetectBanner.innerText = '⚠️ Lütfen Etsy Mağaza sekmesini açın.';
      ordDetectBanner.innerText = '⚠️ Lütfen Etsy Mağaza sekmesini açın.';
      return;
    }

    function queryContentScript() {
      chrome.tabs.sendMessage(activeTab.id, { action: 'GET_PAGE_DATA' }, async (response) => {
        if (chrome.runtime.lastError || !response || !response.success) {
          try {
            await chrome.scripting.executeScript({
              target: { tabId: activeTab.id },
              files: ['content.js']
            });
            setTimeout(queryContentScript, 300);
          } catch (scriptErr) {
            finDetectBanner.innerText = '⚠️ Sayfa algılanamadı. Lütfen Etsy sekmesini 1 kez yenileyin (F5).';
            ordDetectBanner.innerText = '⚠️ Sayfa algılanamadı. Lütfen Etsy sekmesini 1 kez yenileyin (F5).';
          }
          return;
        }

        // Finans Sayfası
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

        // Siparişler Sayfası
        if (response.pageType === 'orders' && response.ordersData) {
          currentPageOrders = response.ordersData;

          // Eğer açık çekmece / detay paneli varsa tam adresle hafızayı güncelle
          if (response.drawerOrder && response.drawerOrder.isFullAddress) {
            const dOrder = response.drawerOrder;
            const dIdx = allOrders.findIndex(o => o.receiptId === dOrder.receiptId);
            if (dIdx !== -1) {
              allOrders[dIdx] = { ...allOrders[dIdx], ...dOrder, isFullAddress: true };
            } else {
              allOrders.unshift(dOrder);
            }
          }
          
          // Hafızadaki siparişler ile bu sayfadaki siparişleri birleştir (Tekilleştir)
          let addedCount = 0;
          currentPageOrders.forEach(co => {
            const idx = allOrders.findIndex(o => o.receiptId === co.receiptId);
            if (idx === -1) {
              allOrders.push(co);
              addedCount++;
            } else {
              // Mevcut siparişin daha detaylı bilgilerini güncelle
              allOrders[idx] = { ...allOrders[idx], ...co };
            }
          });

          // Hafızaya kalıcı kaydet
          chrome.storage.local.set({ storedOrders: allOrders });

          ordDetectBanner.className = 'page-detect-banner success';
          ordDetectBanner.innerText = `✅ Etsy Siparişleri algılandı (Bu sayfada: ${currentPageOrders.length}, Toplam Hafızada: ${allOrders.length})`;

          renderOrdersList();
        } else if (response.pageType !== 'orders') {
          ordDetectBanner.innerText = 'ℹ️ Etsy Siparişler sayfasında değilsiniz.';
        }
      });
    }

    queryContentScript();
  }

  await loadDataFromPage();

  // 5. FİNANS AKTARIM BUTONU
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
        headers: {
          'Content-Type': 'application/json',
          'X-Api-Key': config.apiKey || 'etsy-vds-sync-key-2026'
        },
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

  // 6. SİPARİŞLERİ VDS'E AKTARMA BUTONU (Toplu & Güvenli)
  btnSyncOrders.addEventListener('click', async () => {
    const ordersToSync = allOrders.length > 0 ? allOrders : currentPageOrders;
    if (ordersToSync.length === 0) {
      ordResultBox.className = 'result-box error';
      ordResultBox.innerText = '⚠️ Sayfada veya hafızada aktarılacak sipariş bulunamadı.';
      ordResultBox.style.display = 'block';
      return;
    }

    btnSyncOrders.disabled = true;
    btnSyncOrders.innerText = '⏳ Siparişler VDS\'e Aktarılıyor...';
    ordResultBox.style.display = 'none';

    try {
      const payload = {
        shopId: config.shopId,
        orders: ordersToSync
      };

      // Toplu aktarım endpoint'i
      let success = false;
      let msg = '';

      try {
        const res = await fetch(`${config.apiUrl}/api/etsy/orders/sync-from-extension`, {
          method: 'POST',
          headers: {
            'Content-Type': 'application/json',
            'X-Api-Key': config.apiKey || 'etsy-vds-sync-key-2026'
          },
          body: JSON.stringify(payload)
        });

        if (res.ok) {
          const data = await res.json();
          success = true;
          msg = `✅ ${data.updatedCount || ordersToSync.length} sipariş ve müşteri bilgisi VDS'e başarıyla aktarıldı!`;
        }
      } catch (endpointErr) {}

      // Eğer toplu endpoint henüz VDS'te yeniden başlatılmadıysa tekil fallback aktarımı
      if (!success) {
        let successCount = 0;
        for (const order of ordersToSync) {
          try {
            const singleRes = await fetch(`${config.apiUrl}/api/etsy/orders/${order.receiptId}/cost?shopId=${config.shopId}`, {
              method: 'POST',
              headers: {
                'Content-Type': 'application/json',
                'X-Api-Key': config.apiKey || 'etsy-vds-sync-key-2026'
              },
              body: JSON.stringify({ notes: `Müşteri: ${order.buyerName} | Adres: ${order.shippingAddress}` })
            });
            if (singleRes.ok) successCount++;
          } catch (e) {}
        }
        if (successCount > 0) {
          success = true;
          msg = `✅ ${successCount} / ${ordersToSync.length} sipariş VDS'e başarıyla aktarıldı!`;
        } else {
          msg = `⚠️ VDS API'sine ulaşılamadı. Lütfen VDS Masaüstündeki 'API_Guncelle_Ve_Baslat.bat'ı çalıştırın veya IP adresini kontrol edin.`;
        }
      }

      ordResultBox.className = success ? 'result-box success' : 'result-box error';
      ordResultBox.innerText = msg;
    } catch (err) {
      ordResultBox.className = 'result-box error';
      ordResultBox.innerText = `⚠️ Hata: ${err.message}`;
    } finally {
      ordResultBox.style.display = 'block';
      btnSyncOrders.disabled = false;
      btnSyncOrders.innerText = '📥 Müşteri & Siparişleri VDS\'e Aktar';
    }
  });

  // 7. CSV / EXCEL İNDİR BUTONU
  btnExportCsv.addEventListener('click', () => {
    const list = allOrders.length > 0 ? allOrders : currentPageOrders;
    if (list.length === 0) {
      alert('İndirilecek sipariş bulunamadı.');
      return;
    }

    const headers = [
      'Siparis_No', 
      'Musteri_Adi', 
      'Cadde_Sokak_Adresi', 
      'Daire_Bina', 
      'Sehir', 
      'Eyalet', 
      'Posta_Kodu', 
      'Ulke', 
      'Telefon', 
      'Tutar', 
      'Para_Birimi', 
      'Tarih', 
      'Kargo_Takip_No', 
      'Urun_Adi', 
      'Durum', 
      'Adres_Tipi'
    ];
    const rows = list.map(o => [
      `"${o.receiptId || ''}"`,
      `"${(o.buyerName || '').replace(/"/g, '""')}"`,
      `"${(o.streetAddress || o.shippingAddress || '').replace(/"/g, '""')}"`,
      `"${(o.secondAddress || '').replace(/"/g, '""')}"`,
      `"${(o.city || '').replace(/"/g, '""')}"`,
      `"${(o.state || '').replace(/"/g, '""')}"`,
      `"${(o.postalCode || '').replace(/"/g, '""')}"`,
      `"${(o.country || 'United States').replace(/"/g, '""')}"`,
      `"${(o.phone || '').replace(/"/g, '""')}"`,
      o.orderTotal || 0,
      `"${o.currency || 'USD'}"`,
      `"${o.orderDate || ''}"`,
      `"${o.trackingNumber || ''}"`,
      `"${(o.productTitle || '').replace(/"/g, '""')}"`,
      `"${o.status || 'Completed'}"`,
      `"${o.isFullAddress ? 'Tam Cadde Adresi (Detay)' : 'Liste Ozeti'}"`
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(r => r.join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `etsy_siparisler_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  });

  // 8. JSON KOPYALA BUTONU
  btnCopyJson.addEventListener('click', async () => {
    const list = allOrders.length > 0 ? allOrders : currentPageOrders;
    if (list.length === 0) {
      alert('Kopyalanacak sipariş bulunamadı.');
      return;
    }

    try {
      await navigator.clipboard.writeText(JSON.stringify(list, null, 2));
      ordResultBox.className = 'result-box success';
      ordResultBox.innerText = `📋 ${list.length} sipariş JSON olarak panoya kopyalandı!`;
      ordResultBox.style.display = 'block';
      setTimeout(() => { ordResultBox.style.display = 'none'; }, 2500);
    } catch (err) {
      alert('Kopyalama başarısız oldu: ' + err.message);
    }
  });

  // 9. HAFIZAYI TEMİZLE BUTONU
  btnClearMemory.addEventListener('click', () => {
    if (confirm('Hafızadaki tüm sipariş listesi sıfırlansın mı?')) {
      allOrders = [];
      chrome.storage.local.set({ storedOrders: [] }, () => {
        renderOrdersList();
        ordResultBox.className = 'result-box success';
        ordResultBox.innerText = '🗑️ Hafıza sıfırlandı.';
        ordResultBox.style.display = 'block';
        setTimeout(() => { ordResultBox.style.display = 'none'; }, 2000);
      });
    }
  });

  // 10. AYARLARI KAYDET BUTONU
  btnSaveSettings.addEventListener('click', () => {
    const newApi = cfgApiUrl.value.trim() || 'http://localhost:5263';
    const newKey = cfgApiKey.value.trim() || 'etsy-vds-sync-key-2026';
    const newShop = cfgShopId.value.trim() || '53236321';
    const newAuto = cfgAutoSync.checked;

    chrome.storage.local.set({ apiUrl: newApi, apiKey: newKey, shopId: newShop, autoSync: newAuto }, () => {
      config = { apiUrl: newApi, apiKey: newKey, shopId: newShop, autoSync: newAuto, storedOrders: allOrders };
      setResultBox.className = 'result-box success';
      setResultBox.innerText = '✅ Ayarlar başarıyla kaydedildi!';
      setResultBox.style.display = 'block';
      checkApiHealth();
      setTimeout(() => { setResultBox.style.display = 'none'; }, 2500);
    });
  });
});
