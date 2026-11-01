// Etsy Copilot v2.6.0 — Katman 3: Durumsuz (Stateless) MV3 Service Worker Orkestratörü
// KURAL: Hiçbir global değişkende durum tutulmaz; tüm durum chrome.storage.local üzerinden okunur/yazılır.

import {
  EXTENSION_VERSION,
  STORAGE_SCHEMA_VERSION,
  STORAGE_KEYS,
  DEFAULT_SETTINGS,
  ACTIONS,
  isValidReceiptId,
  normalizeOrderDto,
  mergeOrderDto
} from '../shared/contracts.js';

import {
  checkVdsHealth,
  syncOrdersToVds,
  syncFinanceSnapshotToVds,
  parseAddressViaAi
} from './vds-api-client.js';

const EMPTY_STATE = Object.freeze({
  schemaVersion: STORAGE_SCHEMA_VERSION,
  extensionVersion: EXTENSION_VERSION,
  pageType: 'other',
  tabUrl: '',
  livePageOrders: [],
  verifiedArchive: [],
  financeSnapshot: null,
  lastDiagnostics: null,
  lastScanAt: null
});

async function loadSettings() {
  const res = await chrome.storage.local.get([STORAGE_KEYS.SETTINGS, 'apiUrl', 'apiKey', 'shopId', 'autoSync', 'autoExpandAddresses']);
  const saved = res[STORAGE_KEYS.SETTINGS] || {};
  return {
    apiUrl: saved.apiUrl || res.apiUrl || DEFAULT_SETTINGS.apiUrl,
    apiKey: saved.apiKey || res.apiKey || DEFAULT_SETTINGS.apiKey,
    shopId: saved.shopId || res.shopId || DEFAULT_SETTINGS.shopId,
    autoSyncFinance: Boolean(saved.autoSyncFinance ?? res.autoSync ?? DEFAULT_SETTINGS.autoSyncFinance),
    autoExpandAddresses: Boolean(saved.autoExpandAddresses ?? res.autoExpandAddresses ?? DEFAULT_SETTINGS.autoExpandAddresses)
  };
}

async function saveSettings(partial) {
  const current = await loadSettings();
  const next = { ...current, ...partial };
  await chrome.storage.local.set({ [STORAGE_KEYS.SETTINGS]: next });
  return next;
}

async function loadState() {
  const res = await chrome.storage.local.get(STORAGE_KEYS.STATE);
  const raw = res[STORAGE_KEYS.STATE];
  if (!raw || raw.schemaVersion !== STORAGE_SCHEMA_VERSION) {
    return { ...EMPTY_STATE };
  }
  return {
    ...EMPTY_STATE,
    ...raw,
    livePageOrders: Array.isArray(raw.livePageOrders) ? raw.livePageOrders : [],
    verifiedArchive: Array.isArray(raw.verifiedArchive) ? raw.verifiedArchive : []
  };
}

async function saveState(nextState) {
  const normalized = {
    ...EMPTY_STATE,
    ...nextState,
    schemaVersion: STORAGE_SCHEMA_VERSION,
    extensionVersion: EXTENSION_VERSION
  };
  await chrome.storage.local.set({ [STORAGE_KEYS.STATE]: normalized });
  await updateActionBadge(normalized.livePageOrders.length);
  return normalized;
}

async function updateActionBadge(count) {
  try {
    if (count > 0) {
      await chrome.action.setBadgeText({ text: String(count) });
      await chrome.action.setBadgeBackgroundColor({ color: '#059669' });
    } else {
      await chrome.action.setBadgeText({ text: '' });
    }
  } catch {}
}

async function resolveTargetEtsyTab(explicitTabId) {
  if (explicitTabId) {
    try {
      const tab = await chrome.tabs.get(explicitTabId);
      if (tab && tab.url && tab.url.includes('etsy.com')) return tab;
    } catch {}
  }

  const activeTabs = await chrome.tabs.query({ active: true, lastFocusedWindow: true });
  if (activeTabs[0] && activeTabs[0].url && activeTabs[0].url.includes('etsy.com')) {
    return activeTabs[0];
  }

  const etsyTabs = await chrome.tabs.query({ url: '*://*.etsy.com/*' });
  if (etsyTabs.length === 0) return null;

  return etsyTabs.find((t) => t.url && (t.url.includes('/orders') || t.url.includes('/sold')))
    || etsyTabs.find((t) => t.url && (t.url.includes('/payments') || t.url.includes('/finances')))
    || etsyTabs[0];
}

async function ensureContentAgentInjected(tabId) {
  try {
    const ping = await chrome.tabs.sendMessage(tabId, { action: ACTIONS.CA_PING });
    if (ping && ping.ok && ping.version === EXTENSION_VERSION) {
      return true;
    }
  } catch {}

  try {
    await chrome.scripting.executeScript({
      target: { tabId },
      files: ['src/shared/address-parser.js', 'src/content/content-agent.js']
    });
    return true;
  } catch (err) {
    console.warn('[Copilot v2] Content Agent enjekte edilemedi:', err?.message);
    return false;
  }
}

async function executeMainWorldReactReader(tabId) {
  try {
    const results = await chrome.scripting.executeScript({
      target: { tabId },
      world: 'MAIN',
      files: ['src/page/react-reader.main.js']
    });
    if (Array.isArray(results) && results[0] && results[0].result) {
      return results[0].result;
    }
  } catch (err) {
    return {
      orders: [],
      finance: null,
      diagnostics: { layer: 'react-reader.main', error: err?.message || 'MAIN_EXEC_FAILED' }
    };
  }
  return { orders: [], finance: null, diagnostics: { layer: 'react-reader.main', empty: true } };
}

function mergeIntoArchive(existingArchive, incomingOrders) {
  const archiveMap = new Map();
  for (const item of existingArchive) {
    const norm = normalizeOrderDto(item);
    if (norm) archiveMap.set(norm.receiptId, norm);
  }
  for (const inc of incomingOrders) {
    const norm = normalizeOrderDto(inc);
    if (!norm) continue;
    const prev = archiveMap.get(norm.receiptId);
    archiveMap.set(norm.receiptId, prev ? mergeOrderDto(prev, norm) : norm);
  }
  // En son güncellenen veya eklenen 500 siparişi tut
  return Array.from(archiveMap.values()).slice(0, 500);
}

/**
 * Çift Motorlu Birleşik Tarama:
 * Motor 1: MAIN world React Fiber & Etsy.Context salt-okunur bellek taraması
 * Motor 2: ISOLATED world dil-bağımsız yapısal DOM taraması
 */
async function scanActiveEtsyTab(explicitTabId, expandInlineFirst = false) {
  const state = await loadState();
  const tab = await resolveTargetEtsyTab(explicitTabId);

  if (!tab || !tab.id) {
    return {
      ok: false,
      reason: 'NO_ETSY_TAB',
      message: 'Açık bir Etsy Mağaza sekmesi bulunamadı.',
      state
    };
  }

  const agentReady = await ensureContentAgentInjected(tab.id);
  if (!agentReady) {
    return {
      ok: false,
      reason: 'INJECTION_FAILED',
      message: 'Etsy sekmesine erişilemedi. Lütfen sekmeyi yenileyip (F5) tekrar deneyin.',
      state
    };
  }

  let domResponse = null;
  let clickedInlineToggles = 0;
  // Sıfır Bot Riski Güvencesi: Kullanıcı bizzat butona basmadıkça ASLA sayfada otomatik tıklama yapılmaz!
  const shouldExpand = Boolean(expandInlineFirst === true);

  try {
    if (shouldExpand) {
      const expandRes = await chrome.tabs.sendMessage(tab.id, { action: ACTIONS.CA_EXPAND_INLINE_ADDRESSES });
      clickedInlineToggles = expandRes?.diagnostics?.clickedInlineToggles || 0;
      domResponse = expandRes;
      if (clickedInlineToggles > 0) {
        await new Promise((r) => setTimeout(r, 120));
      }
    } else {
      domResponse = await chrome.tabs.sendMessage(tab.id, { action: ACTIONS.CA_EXTRACT_STRUCTURAL });
    }
    if (domResponse?.diagnostics && shouldExpand) {
      domResponse.diagnostics.clickedInlineToggles = clickedInlineToggles;
    }
  } catch (err) {
    return {
      ok: false,
      reason: 'DOM_MESSAGE_FAILED',
      message: `Sayfa okuma hatası: ${err?.message || 'Bilinmeyen hata'}`,
      state
    };
  }

  const pageType = domResponse?.pageType || 'other';
  const mainResult = pageType === 'orders'
    ? await executeMainWorldReactReader(tab.id)
    : { orders: [], finance: null, diagnostics: null };

  // Mağaza ID'si Etsy.Context'ten tespit edildiyse ve kullanıcı varsayılanı kullanıyorsa sakla
  if (mainResult?.shopId) {
    const curSettings = await loadSettings();
    if (!curSettings.shopId) {
      await saveSettings({ shopId: mainResult.shopId });
    }
  }

  // Canlı sayfa sipariş listesini sıralamayı koruyarak birleştir (Önce DOM sırası, üzerine React Bellek zenginleştirmesi + Arşivdeki daha önceden çözülmüş tam adres koruması!)
  const archiveById = new Map();
  for (const arch of state.verifiedArchive) {
    if (arch && isValidReceiptId(arch.receiptId)) {
      archiveById.set(arch.receiptId, arch);
    }
  }

  const reactById = new Map();
  for (const ro of (mainResult?.orders || [])) {
    const norm = normalizeOrderDto(ro);
    if (norm) reactById.set(norm.receiptId, norm);
  }

  const liveMap = new Map();
  for (const domOrder of (domResponse?.orders || [])) {
    const normDom = normalizeOrderDto(domOrder);
    if (!normDom) continue;
    let combined = normDom;

    const reactMatch = reactById.get(normDom.receiptId);
    if (reactMatch) {
      combined = mergeOrderDto(combined, reactMatch);
    }
    const prevArchived = archiveById.get(normDom.receiptId);
    if (prevArchived && prevArchived.isFullAddress && !combined.isFullAddress) {
      combined = mergeOrderDto(prevArchived, combined);
    }
    liveMap.set(normDom.receiptId, combined);
  }

  // React Fiber'da olup DOM'da henüz yakalanmamış sipariş varsa ekle
  for (const [id, reactOrd] of reactById.entries()) {
    if (!liveMap.has(id)) {
      const prevArchived = archiveById.get(id);
      liveMap.set(id, prevArchived ? mergeOrderDto(prevArchived, reactOrd) : reactOrd);
    }
  }

  // Açık yan çekmece (Drawer) varsa 100% tam adres olarak birleştir
  if (domResponse?.drawerOrder) {
    const normDrawer = normalizeOrderDto(domResponse.drawerOrder);
    if (normDrawer) {
      const existing = liveMap.get(normDrawer.receiptId);
      liveMap.set(normDrawer.receiptId, existing ? mergeOrderDto(existing, normDrawer) : normDrawer);
    }
  }

  const livePageOrders = pageType === 'orders' ? Array.from(liveMap.values()) : state.livePageOrders;
  const verifiedArchive = pageType === 'orders'
    ? mergeIntoArchive(state.verifiedArchive, livePageOrders)
    : state.verifiedArchive;

  const financeSnapshot = pageType === 'finance' && domResponse?.finance
    ? domResponse.finance
    : state.financeSnapshot;

  const diagnostics = {
    timestamp: new Date().toISOString(),
    extensionVersion: EXTENSION_VERSION,
    tabId: tab.id,
    tabUrl: tab.url,
    pageType,
    reactMainWorld: mainResult?.diagnostics || null,
    structuralDom: domResponse?.diagnostics || null,
    summary: {
      liveCount: livePageOrders.length,
      liveFullAddresses: livePageOrders.filter((o) => o.isFullAddress).length,
      archiveCount: verifiedArchive.length,
      archiveFullAddresses: verifiedArchive.filter((o) => o.isFullAddress).length
    }
  };

  const nextState = await saveState({
    pageType,
    tabUrl: tab.url || '',
    livePageOrders,
    verifiedArchive,
    financeSnapshot,
    lastDiagnostics: diagnostics,
    lastScanAt: diagnostics.timestamp
  });

  // Otomatik finans senkronu açıksa ve finans sayfasındaysak VDS'e gönder
  if (pageType === 'finance' && domResponse?.finance) {
    const settings = await loadSettings();
    if (settings.autoSyncFinance) {
      await syncFinanceSnapshotToVds(settings, domResponse.finance);
    }
  }

  return { ok: true, state: nextState, diagnostics };
}

// 1. Kurulum / Güncelleme Dinleyicisi (Eski v1 bozuk 'storedOrders' önbelleğini temizler!)
chrome.runtime.onInstalled.addListener((details) => {
  (async () => {
    try {
      // Kirli v1 'storedOrders' önbelleğini kalıcı olarak kaldır
      await chrome.storage.local.remove(STORAGE_KEYS.LEGACY_STORED_ORDERS);
      const settings = await loadSettings();
      await saveSettings(settings);
      if (details.reason === 'install' || details.reason === 'update') {
        const current = await loadState();
        await saveState(current);
      }
    } catch (err) {
      console.error('[Copilot v2] onInstalled hatası:', err);
    }
  })();
});

// 2. Merkezi Mesaj Yönlendiricisi (Senkron Üst Seviye Kayıt + return true)
chrome.runtime.onMessage.addListener((message, sender, sendResponse) => {
  if (!message || !message.action) return false;

  (async () => {
    try {
      switch (message.action) {
        case ACTIONS.CA_DRAWER_CAPTURED: {
          const norm = normalizeOrderDto(message.order);
          if (!norm) {
            sendResponse({ ok: false });
            return;
          }
          const state = await loadState();
          const liveIdx = state.livePageOrders.findIndex((o) => o.receiptId === norm.receiptId);
          const nextLive = [...state.livePageOrders];
          if (liveIdx !== -1) {
            nextLive[liveIdx] = mergeOrderDto(nextLive[liveIdx], norm);
          } else {
            nextLive.unshift(norm);
          }
          const nextArchive = mergeIntoArchive(state.verifiedArchive, [norm]);
          const saved = await saveState({
            ...state,
            livePageOrders: nextLive,
            verifiedArchive: nextArchive
          });
          sendResponse({ ok: true, state: saved });
          return;
        }

        case ACTIONS.SW_SCAN_ACTIVE_TAB: {
          const res = await scanActiveEtsyTab(message.tabId || sender?.tab?.id, false);
          sendResponse(res);
          return;
        }

        case ACTIONS.SW_RESOLVE_FULL_ADDRESSES: {
          const res = await scanActiveEtsyTab(message.tabId, true);
          sendResponse(res);
          return;
        }

        case ACTIONS.SW_CHECK_VDS_HEALTH: {
          const settings = await loadSettings();
          const health = await checkVdsHealth(settings);
          if (health.online && health.activeApiUrl !== settings.apiUrl) {
            await saveSettings({ apiUrl: health.activeApiUrl });
          }
          sendResponse({ ok: true, health, settings: await loadSettings() });
          return;
        }

        case ACTIONS.SW_SYNC_ORDERS_TO_VDS: {
          const settings = await loadSettings();
          const state = await loadState();
          const sourceList = message.scope === 'archive' && state.verifiedArchive.length > 0
            ? state.verifiedArchive
            : (state.livePageOrders.length > 0 ? state.livePageOrders : state.verifiedArchive);
          const syncRes = await syncOrdersToVds(settings, sourceList);
          if (syncRes.activeApiUrl && syncRes.activeApiUrl !== settings.apiUrl && syncRes.ok) {
            await saveSettings({ apiUrl: syncRes.activeApiUrl });
          }
          sendResponse(syncRes);
          return;
        }

        case ACTIONS.SW_SYNC_FINANCE_TO_VDS: {
          const settings = await loadSettings();
          const state = await loadState();
          const snap = message.snapshot || state.financeSnapshot;
          const finRes = await syncFinanceSnapshotToVds(settings, snap);
          sendResponse(finRes);
          return;
        }

        case ACTIONS.SW_AI_PARSE_ADDRESS: {
          const settings = await loadSettings();
          const aiRes = await parseAddressViaAi(settings, message.rawAddress);
          if (aiRes.ok && aiRes.data && isValidReceiptId(message.receiptId)) {
            const d = aiRes.data;
            const patch = {
              receiptId: String(message.receiptId),
              buyerName: d.buyerName || '',
              recipientName: d.buyerName || '',
              streetAddress: d.streetAddress || '',
              secondAddress: d.secondAddress || '',
              city: d.city || '',
              state: d.state || '',
              postalCode: d.postalCode || '',
              country: d.countryCode || d.country || '',
              phone: d.phone || '',
              dataLayer: 'deepseek-ai',
              sourceType: 'DeepSeek AI Doğrulama (VDS)'
            };
            const state = await loadState();
            const nextLive = state.livePageOrders.map((o) => (o.receiptId === patch.receiptId ? mergeOrderDto(o, patch) : o));
            const nextArchive = mergeIntoArchive(state.verifiedArchive, [
              nextLive.find((o) => o.receiptId === patch.receiptId) || patch
            ]);
            const updatedState = await saveState({
              ...state,
              livePageOrders: nextLive,
              verifiedArchive: nextArchive
            });
            sendResponse({ ok: true, data: aiRes.data, state: updatedState });
            return;
          }
          sendResponse(aiRes);
          return;
        }

        case ACTIONS.SW_CLEAR_ARCHIVE: {
          await chrome.storage.local.remove(STORAGE_KEYS.LEGACY_STORED_ORDERS);
          const cleared = await saveState({
            ...EMPTY_STATE
          });
          sendResponse({ ok: true, state: cleared });
          return;
        }

        case ACTIONS.SW_SAVE_SETTINGS: {
          const updated = await saveSettings(message.settings || {});
          const health = await checkVdsHealth(updated);
          sendResponse({ ok: true, settings: updated, health });
          return;
        }

        case ACTIONS.SW_GET_DIAGNOSTICS: {
          const state = await loadState();
          sendResponse({ ok: true, diagnostics: state.lastDiagnostics, state });
          return;
        }

        default:
          sendResponse({ ok: false, error: `Bilinmeyen aksiyon: ${message.action}` });
      }
    } catch (err) {
      sendResponse({ ok: false, error: err?.message || 'Service Worker işlem hatası' });
    }
  })();

  return true;
});
