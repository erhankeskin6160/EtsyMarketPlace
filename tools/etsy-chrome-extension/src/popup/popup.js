// Etsy Copilot v2.1.0 — Katman 4: Reaktif Popup Görünüm Denetleyicisi (Sıfır Mock Veri)

import { ACTIONS, STORAGE_KEYS } from '../shared/contracts.js';

async function getCurrentWindowActiveTabId() {
  try {
    const [tab] = await chrome.tabs.query({ active: true, currentWindow: true });
    return tab?.id || null;
  } catch {
    return null;
  }
}

function escapeHtml(value) {
  return String(value ?? '')
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;')
    .replace(/'/g, '&#39;');
}

function formatMoney(amount, currency = 'USD') {
  if (amount == null || Number.isNaN(Number(amount))) return '';
  const n = Number(amount).toFixed(2);
  if (currency === 'TRY') return `${n} ₺`;
  if (currency === 'EUR') return `€${n}`;
  if (currency === 'GBP') return `£${n}`;
  return `$${n}`;
}

function showFeedback(el, ok, text) {
  if (!el) return;
  el.className = `feedback-box ${ok ? 'ok' : 'err'}`;
  el.textContent = text;
}

document.addEventListener('DOMContentLoaded', async () => {
  const apiStatusPill = document.getElementById('apiStatusPill');
  const apiStatusText = document.getElementById('apiStatusText');
  const tabBtns = document.querySelectorAll('.tab-btn');
  const tabPanes = document.querySelectorAll('.tab-pane');

  const ordDetectBanner = document.getElementById('ordDetectBanner');
  const liveCountEl = document.getElementById('liveCount');
  const archiveCountEl = document.getElementById('archiveCount');
  const fullAddrStatEl = document.getElementById('fullAddrStat');
  const btnScopeLive = document.getElementById('btnScopeLive');
  const btnScopeArchive = document.getElementById('btnScopeArchive');
  const orderListContainer = document.getElementById('orderListContainer');
  const btnResolveAddresses = document.getElementById('btnResolveAddresses');
  const btnSyncOrders = document.getElementById('btnSyncOrders');
  const btnExportCsv = document.getElementById('btnExportCsv');
  const btnCopyJson = document.getElementById('btnCopyJson');
  const btnCopyDiag = document.getElementById('btnCopyDiag');
  const btnClearStore = document.getElementById('btnClearStore');
  const ordResultBox = document.getElementById('ordResultBox');

  const finDetectBanner = document.getElementById('finDetectBanner');
  const finCurrent = document.getElementById('finCurrent');
  const finReserve = document.getElementById('finReserve');
  const finTotal = document.getElementById('finTotal');
  const finPct = document.getElementById('finPct');
  const finDate = document.getElementById('finDate');
  const btnSyncFinance = document.getElementById('btnSyncFinance');
  const finResultBox = document.getElementById('finResultBox');

  const cfgApiUrl = document.getElementById('cfgApiUrl');
  const cfgApiKey = document.getElementById('cfgApiKey');
  const cfgShopId = document.getElementById('cfgShopId');
  const cfgAutoExpand = document.getElementById('cfgAutoExpand');
  const cfgAutoSync = document.getElementById('cfgAutoSync');
  const btnPresetLocal = document.getElementById('btnPresetLocal');
  const btnPresetRemote = document.getElementById('btnPresetRemote');
  const btnSaveSettings = document.getElementById('btnSaveSettings');
  const setResultBox = document.getElementById('setResultBox');

  let activeScope = 'live'; // 'live' | 'archive'
  let appState = {
    pageType: 'other',
    livePageOrders: [],
    verifiedArchive: [],
    financeSnapshot: null,
    lastDiagnostics: null
  };

  function activateTab(tabName) {
    tabBtns.forEach((b) => b.classList.toggle('active', b.dataset.tab === tabName));
    tabPanes.forEach((p) => p.classList.toggle('active', p.id === `pane-${tabName}`));
  }

  tabBtns.forEach((btn) => {
    btn.addEventListener('click', () => activateTab(btn.dataset.tab));
  });

  btnScopeLive.addEventListener('click', () => {
    activeScope = 'live';
    btnScopeLive.classList.add('active');
    btnScopeArchive.classList.remove('active');
    renderOrders();
  });

  btnScopeArchive.addEventListener('click', () => {
    activeScope = 'archive';
    btnScopeArchive.classList.add('active');
    btnScopeLive.classList.remove('active');
    renderOrders();
  });

  function getDisplayedOrders() {
    if (activeScope === 'archive') {
      return appState.verifiedArchive || [];
    }
    return (appState.livePageOrders && appState.livePageOrders.length > 0)
      ? appState.livePageOrders
      : [];
  }

  function renderLayerBadge(dataLayer) {
    if (dataLayer === 'react-fiber') {
      return '<span class="pill layer-fiber" title="Etsy React Fiber belleğinden doğrudan okundu">⚡ React Bellek</span>';
    }
    if (dataLayer === 'deepseek-ai') {
      return '<span class="pill layer-ai" title="VDS DeepSeek AI ile ayrıştırıldı">🤖 DeepSeek AI</span>';
    }
    return '<span class="pill layer-dom" title="Dil-bağımsız yapısal DOM çapasından okundu">🧩 Yapısal DOM</span>';
  }

  function renderFinance() {
    const snap = appState.financeSnapshot;
    if (!snap) {
      finCurrent.textContent = '—';
      finReserve.textContent = '—';
      finTotal.textContent = '—';
      finPct.textContent = '%0';
      finDate.textContent = '—';
      return;
    }
    finCurrent.textContent = `${Number(snap.currentBalanceTry).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₺`;
    finReserve.textContent = `${Number(snap.pendingBalanceTry).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₺`;
    finTotal.textContent = `${Number(snap.totalBalanceTry).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })} ₺`;
    finPct.textContent = `%${snap.reservePercent || 0}`;
    finDate.textContent = snap.reevaluationDate || 'Belirtilmemiş';
  }

  function renderOrders() {
    const liveList = appState.livePageOrders || [];
    const archList = appState.verifiedArchive || [];
    liveCountEl.textContent = String(liveList.length);
    archiveCountEl.textContent = String(archList.length);

    const list = getDisplayedOrders();
    const fullCount = list.filter((o) => o.isFullAddress).length;
    fullAddrStatEl.textContent = `${fullCount}/${list.length}`;

    if (list.length === 0) {
      const emptyMsg = activeScope === 'live'
        ? 'Açık Etsy sayfasında sipariş bulunamadı. Siparişler sekmesini açın veya Arşiv görünümüne geçin.'
        : 'Arşivde kayıtlı sipariş bulunmuyor.';
      orderListContainer.innerHTML = `<div class="empty-state">${escapeHtml(emptyMsg)}</div>`;
      return;
    }

    orderListContainer.innerHTML = '';

    for (const o of list) {
      const card = document.createElement('div');
      card.className = 'order-card';

      const isFull = Boolean(o.isFullAddress);
      const cityRegion = [o.city, [o.state, o.postalCode].filter(Boolean).join(' '), o.country].filter(Boolean).join(', ');
      const previewAddr = isFull
        ? [o.streetAddress, o.secondAddress, [o.city, o.state, o.postalCode].filter(Boolean).join(' ')].filter(Boolean).join(', ')
        : (cityRegion || o.shippingAddress || 'Adres özeti bekleniyor');

      const buyerDualNote = (o.buyerAccountName && o.recipientName && o.buyerAccountName.toLowerCase() !== o.recipientName.toLowerCase())
        ? `${o.recipientName} (Hesap: ${o.buyerAccountName})`
        : o.buyerName;

      card.innerHTML = `
        <div class="order-card-top">
          <div class="order-main-col">
            <div class="order-badges-row">
              <span class="order-id">#${escapeHtml(o.receiptId)}</span>
              <span class="buyer-name" title="${escapeHtml(buyerDualNote)}">${escapeHtml(o.buyerName)}</span>
              ${isFull
                ? '<span class="pill full">🏠 Tam Adres</span>'
                : '<span class="pill summary">⚠️ Özet</span>'}
              ${renderLayerBadge(o.dataLayer)}
              ${o.sku ? `<span class="pill sku">SKU: ${escapeHtml(o.sku)}</span>` : ''}
            </div>
            <div class="addr-preview ${isFull ? 'full' : ''}">📍 ${escapeHtml(previewAddr)}</div>
          </div>
          <div class="order-price-col">
            <div class="order-total">${escapeHtml(formatMoney(o.orderTotal, o.currency))}</div>
            <div class="order-date">${escapeHtml(o.orderDate || '')} ▾</div>
          </div>
        </div>

        <div class="order-detail-drawer">
          <div class="detail-grid">
            <span class="d-lbl">👤 Alıcı / Hesap:</span>
            <span class="d-val">${escapeHtml(buyerDualNote)}</span>

            <span class="d-lbl">🏠 Cadde / Sokak:</span>
            <span class="d-val">${o.streetAddress ? escapeHtml(o.streetAddress) : '<em>(Açık sokak satırı için mavi butona tıklayın)</em>'}</span>

            ${o.secondAddress ? `<span class="d-lbl">🏢 Daire / Bina:</span><span class="d-val">${escapeHtml(o.secondAddress)}</span>` : ''}

            <span class="d-lbl">🏙️ Şehir / Posta:</span>
            <span class="d-val">${escapeHtml([o.city, o.state, o.postalCode].filter(Boolean).join(', ') || '—')}</span>

            <span class="d-lbl">🌍 Ülke:</span>
            <span class="d-val">${escapeHtml(o.country || '—')}</span>

            <span class="d-lbl">📞 Telefon:</span>
            <span class="d-val">${escapeHtml(o.phone || '(Belirtilmemiş)')}</span>

            ${o.iossNumber ? `<span class="d-lbl">🇪🇺 IOSS:</span><span class="d-val">${escapeHtml(o.iossNumber)}</span>` : ''}
            ${o.productTitle ? `<span class="d-lbl">📦 Ürün:</span><span class="d-val">${escapeHtml(o.productTitle)}</span>` : ''}
            ${o.variation ? `<span class="d-lbl">📏 Varyasyon:</span><span class="d-val">${escapeHtml(o.variation)}</span>` : ''}
            ${o.shippingMethod ? `<span class="d-lbl">🚚 Kargo:</span><span class="d-val">${escapeHtml(o.shippingMethod)}</span>` : ''}
            ${o.trackingNumber ? `<span class="d-lbl">🔢 Takip No:</span><span class="d-val">${escapeHtml(o.trackingNumber)}</span>` : ''}

            <span class="d-lbl">📡 Veri Kaynağı:</span>
            <span class="d-val">${escapeHtml(o.sourceType || o.dataLayer)}</span>
          </div>

          <div class="card-actions">
            <button type="button" class="mini-btn ai" data-role="ai-parse">🤖 DeepSeek AI Doğrula</button>
            <button type="button" class="mini-btn" data-role="copy-addr">📋 Adresi Kopyala</button>
          </div>
        </div>
      `;

      card.addEventListener('click', (ev) => {
        if (ev.target.closest('button')) return;
        card.classList.toggle('expanded');
      });

      const aiBtn = card.querySelector('[data-role="ai-parse"]');
      aiBtn.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        const rawToParse = o.rawAddressText || previewAddr;
        if (!rawToParse) return;
        aiBtn.disabled = true;
        aiBtn.textContent = '⏳ Çözülüyor...';
        const res = await chrome.runtime.sendMessage({
          action: ACTIONS.SW_AI_PARSE_ADDRESS,
          receiptId: o.receiptId,
          rawAddress: rawToParse
        });
        if (res && res.ok && res.state) {
          appState = res.state;
          renderOrders();
        } else {
          aiBtn.disabled = false;
          aiBtn.textContent = '⚠️ Hata — Tekrar Dene';
        }
      });

      const copyBtn = card.querySelector('[data-role="copy-addr"]');
      copyBtn.addEventListener('click', async (ev) => {
        ev.stopPropagation();
        await navigator.clipboard.writeText(o.rawAddressText || previewAddr);
        copyBtn.textContent = '✓ Kopyalandı';
        setTimeout(() => { copyBtn.textContent = '📋 Adresi Kopyala'; }, 1600);
      });

      orderListContainer.appendChild(card);
    }
  }

  // Depolama değişikliklerini canlı dinle (açık çekmece yakalamaları vb.)
  chrome.storage.onChanged.addListener((changes, area) => {
    if (area === 'local' && changes[STORAGE_KEYS.STATE]?.newValue) {
      appState = changes[STORAGE_KEYS.STATE].newValue;
      renderOrders();
      renderFinance();
    }
  });

  // 1. VDS Sağlık Kontrolü ve Ayar Yükleme
  const healthRes = await chrome.runtime.sendMessage({ action: ACTIONS.SW_CHECK_VDS_HEALTH });
  if (healthRes?.settings) {
    cfgApiUrl.value = healthRes.settings.apiUrl;
    cfgApiKey.value = healthRes.settings.apiKey;
    cfgShopId.value = healthRes.settings.shopId;
    if (cfgAutoExpand) {
      cfgAutoExpand.checked = Boolean(healthRes.settings.autoExpandAddresses !== false);
    }
    cfgAutoSync.checked = Boolean(healthRes.settings.autoSyncFinance);
  }
  if (healthRes?.health?.online) {
    apiStatusPill.className = 'api-pill';
    apiStatusText.textContent = 'VDS Canlı';
  } else {
    apiStatusPill.className = 'api-pill offline';
    apiStatusText.textContent = 'VDS Bağlantı Yok';
  }

  // 2. Aktif Penceredeki Aktif Sekmeyi Çift Motorla (React Fiber + Yapısal DOM) Tara
  const initialTabId = await getCurrentWindowActiveTabId();
  const scanRes = await chrome.runtime.sendMessage({
    action: ACTIONS.SW_SCAN_ACTIVE_TAB,
    tabId: initialTabId
  });
  if (scanRes?.state) {
    appState = scanRes.state;
  }

  if (!scanRes?.ok) {
    ordDetectBanner.className = 'detect-banner warn';
    ordDetectBanner.textContent = `⚠️ ${scanRes?.message || 'Lütfen Etsy Mağaza sekmesini açın.'}`;
  } else if (appState.pageType === 'orders') {
    activateTab('orders');
    const reactFull = appState.livePageOrders.filter((o) => o.dataLayer === 'react-fiber' && o.isFullAddress).length;
    const totalFull = appState.livePageOrders.filter((o) => o.isFullAddress).length;
    const namedCount = appState.livePageOrders.filter((o) => o.buyerName && o.buyerName !== 'Etsy Müşterisi').length;
    ordDetectBanner.className = 'detect-banner ok';
    ordDetectBanner.textContent = `🛡️ Pasif İzleme (Sıfır Bot Riski • ${totalFull}/${appState.livePageOrders.length} Tam Adres • ${namedCount} Müşteri)`;
  } else if (appState.pageType === 'finance') {
    activateTab('finance');
    finDetectBanner.className = 'detect-banner ok';
    finDetectBanner.textContent = '✅ Etsy Ödeme Hesabı algılandı! Canlı bakiye ve rezerv okundu.';
    ordDetectBanner.textContent = 'ℹ️ Şu an Ödeme Hesabı sekmesindesiniz.';
  } else {
    ordDetectBanner.textContent = 'ℹ️ Siparişleri okumak için Etsy "Orders" sayfasını açın.';
  }

  renderOrders();
  renderFinance();

  // 3. Mavi Buton: Tüm Siparişlerin Tam Adreslerini Otomatik Çöz (Çekmece Gezgini)
  btnResolveAddresses.addEventListener('click', async () => {
    btnResolveAddresses.disabled = true;
    btnResolveAddresses.textContent = '⏳ Siparişler taranıyor ve çekmeceler otomatik açılıyor...';
    ordResultBox.className = 'feedback-box hidden';

    const targetTabId = await getCurrentWindowActiveTabId();
    const res = await chrome.runtime.sendMessage({
      action: ACTIONS.SW_RESOLVE_FULL_ADDRESSES,
      tabId: targetTabId
    });
    btnResolveAddresses.disabled = false;
    btnResolveAddresses.textContent = '🎯 Tüm Siparişlerin Tam Adreslerini Otomatik Çöz';

    if (res?.ok && res.state) {
      appState = res.state;
      activeScope = 'live';
      btnScopeLive.classList.add('active');
      btnScopeArchive.classList.remove('active');
      renderOrders();

      const full = appState.livePageOrders.filter((o) => o.isFullAddress).length;
      showFeedback(
        ordResultBox,
        full > 0,
        full > 0
          ? `🎯 Sayfadaki ${full}/${appState.livePageOrders.length} siparişin tam sokak adresi otomatik olarak çekmecelerden çözüldü!`
          : 'ℹ️ Sayfadaki siparişlerin adresleri okunamadı; sayfayı yenileyip (F5) tekrar deneyebilirsiniz.'
      );
    } else {
      showFeedback(ordResultBox, false, `⚠️ ${res?.message || res?.error || 'Adres çözümleme başarısız.'}`);
    }
  });

  // 4. Yeşil Buton: VDS'e Aktar (Service Worker üzerinden kesintisiz)
  btnSyncOrders.addEventListener('click', async () => {
    btnSyncOrders.disabled = true;
    btnSyncOrders.textContent = '⏳ VDS Sunucusuna Aktarılıyor...';
    ordResultBox.className = 'feedback-box hidden';

    const res = await chrome.runtime.sendMessage({
      action: ACTIONS.SW_SYNC_ORDERS_TO_VDS,
      scope: activeScope
    });

    btnSyncOrders.disabled = false;
    btnSyncOrders.textContent = '📥 Müşteri & Siparişleri VDS\'e Aktar';

    if (res?.ok) {
      showFeedback(ordResultBox, true, `✅ ${res.syncedCount} sipariş (${res.fullAddressCount} tam adres) VDS'e başarıyla kaydedildi!`);
    } else {
      showFeedback(ordResultBox, false, `⚠️ VDS Aktarım Hatası: ${res?.error || 'Bağlantı kurulamadı'}`);
    }
  });

  // 5. Finans VDS Aktarımı
  btnSyncFinance.addEventListener('click', async () => {
    btnSyncFinance.disabled = true;
    btnSyncFinance.textContent = '⏳ Aktarılıyor...';
    finResultBox.className = 'feedback-box hidden';

    const res = await chrome.runtime.sendMessage({ action: ACTIONS.SW_SYNC_FINANCE_TO_VDS });
    btnSyncFinance.disabled = false;
    btnSyncFinance.textContent = '⚡ Finans & Rezervi VDS\'e Aktar';

    if (res?.ok) {
      showFeedback(finResultBox, true, `✅ Ödeme rezervi VDS'e aktarıldı (${res.payload.currentBalanceTry} ₺ / Rezerv: ${res.payload.pendingBalanceTry} ₺).`);
    } else {
      showFeedback(finResultBox, false, `⚠️ ${res?.error || 'Finans aktarımı başarısız oldu.'}`);
    }
  });

  // 6. CSV İndir
  btnExportCsv.addEventListener('click', () => {
    const list = getDisplayedOrders();
    if (list.length === 0) return;

    const headers = [
      'Siparis_No', 'Musteri_Adi', 'Cadde_Sokak', 'Daire_Bina', 'Sehir', 'Eyalet',
      'Posta_Kodu', 'Ulke', 'Telefon', 'IOSS', 'SKU', 'Varyasyon', 'Tutar',
      'Para_Birimi', 'Tarih', 'Takip_No', 'Urun_Adi', 'Veri_Katmani'
    ];
    const q = (v) => `"${String(v ?? '').replace(/"/g, '""')}"`;
    const rows = list.map((o) => [
      q(o.receiptId), q(o.buyerName), q(o.streetAddress), q(o.secondAddress),
      q(o.city), q(o.state), q(o.postalCode), q(o.country), q(o.phone),
      q(o.iossNumber), q(o.sku), q(o.variation), o.orderTotal ?? '',
      q(o.currency), q(o.orderDate), q(o.trackingNumber), q(o.productTitle), q(o.dataLayer)
    ].join(','));

    const csv = '\uFEFF' + [headers.join(','), ...rows].join('\r\n');
    const url = URL.createObjectURL(new Blob([csv], { type: 'text/csv;charset=utf-8;' }));
    const a = document.createElement('a');
    a.href = url;
    a.download = `etsy_orders_v2_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
  });

  // 7. JSON Kopyala
  btnCopyJson.addEventListener('click', async () => {
    const list = getDisplayedOrders();
    if (list.length === 0) return;
    await navigator.clipboard.writeText(JSON.stringify(list, null, 2));
    showFeedback(ordResultBox, true, `📋 ${list.length} sipariş JSON olarak panoya kopyalandı.`);
  });

  // 8. Canlı Tanılama (Diagnostics) Kopyala
  btnCopyDiag.addEventListener('click', async () => {
    const diagRes = await chrome.runtime.sendMessage({ action: ACTIONS.SW_GET_DIAGNOSTICS });
    const payload = {
      diagnostics: diagRes?.diagnostics || appState.lastDiagnostics,
      sampleOrdersPreview: (appState.livePageOrders || []).slice(0, 3).map((o) => ({
        receiptId: o.receiptId,
        hasBuyer: o.buyerName !== 'Etsy Müşterisi',
        isFullAddress: o.isFullAddress,
        hasCity: Boolean(o.city),
        hasTotal: o.orderTotal != null,
        hasSku: Boolean(o.sku),
        dataLayer: o.dataLayer,
        sourceType: o.sourceType
      }))
    };
    await navigator.clipboard.writeText(JSON.stringify(payload, null, 2));
    showFeedback(ordResultBox, true, '🔬 Tanılama raporu panoya kopyalandı!');
  });

  // 9. Arşivi Sıfırla ve Yeniden Tara
  btnClearStore.addEventListener('click', async () => {
    await chrome.runtime.sendMessage({ action: ACTIONS.SW_CLEAR_ARCHIVE });
    const tabId = await getCurrentWindowActiveTabId();
    const fresh = await chrome.runtime.sendMessage({ action: ACTIONS.SW_SCAN_ACTIVE_TAB, tabId });
    if (fresh?.state) appState = fresh.state;
    renderOrders();
    renderFinance();
    showFeedback(ordResultBox, true, '🗑️ Eski önbellek temizlendi ve aktif sayfa sıfırdan okundu.');
  });

  // 10. Ayarlar
  btnPresetLocal.addEventListener('click', () => { cfgApiUrl.value = 'http://localhost:5263'; });
  btnPresetRemote.addEventListener('click', () => { cfgApiUrl.value = 'http://5.180.81.148:5263'; });

  btnSaveSettings.addEventListener('click', async () => {
    const saved = await chrome.runtime.sendMessage({
      action: ACTIONS.SW_SAVE_SETTINGS,
      settings: {
        apiUrl: cfgApiUrl.value.trim() || 'http://localhost:5263',
        apiKey: cfgApiKey.value.trim() || 'etsy-vds-sync-key-2026',
        shopId: cfgShopId.value.trim() || '53236321',
        autoExpandAddresses: cfgAutoExpand ? cfgAutoExpand.checked : true,
        autoSyncFinance: cfgAutoSync.checked
      }
    });
    if (saved?.health?.online) {
      apiStatusPill.className = 'api-pill';
      apiStatusText.textContent = 'VDS Canlı';
      showFeedback(setResultBox, true, `✅ Ayarlar kaydedildi ve VDS bağlantısı doğrulandı (${saved.settings.apiUrl}).`);
    } else {
      apiStatusPill.className = 'api-pill offline';
      apiStatusText.textContent = 'VDS Bağlantı Yok';
      showFeedback(setResultBox, false, '⚠️ Ayarlar kaydedildi ancak VDS API yanıt vermiyor.');
    }
  });
});
