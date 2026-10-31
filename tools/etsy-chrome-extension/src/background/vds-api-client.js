// Etsy Copilot v2.0.0 — VDS API İstemcisi (Otomatik Failover + Sıkı DTO Eşleme)

import {
  DEFAULT_SETTINGS,
  toVdsExtensionOrdersPayload,
  toVdsFinanceSnapshotPayload
} from '../shared/contracts.js';

async function fetchWithTimeout(url, options = {}, timeoutMs = 6000) {
  const controller = new AbortController();
  const timer = setTimeout(() => controller.abort(), timeoutMs);
  try {
    return await fetch(url, { ...options, signal: controller.signal });
  } finally {
    clearTimeout(timer);
  }
}

function getCandidateBaseUrls(settings) {
  const primary = String(settings?.apiUrl || DEFAULT_SETTINGS.apiUrl).replace(/\/+$/, '');
  const alt = primary.includes('localhost')
    ? 'http://5.180.81.148:5263'
    : 'http://localhost:5263';
  return [primary, alt];
}

function buildAuthHeaders(settings) {
  return {
    'Content-Type': 'application/json',
    'X-Api-Key': String(settings?.apiKey || DEFAULT_SETTINGS.apiKey).trim()
  };
}

export async function checkVdsHealth(settings) {
  const urls = getCandidateBaseUrls(settings);
  for (const baseUrl of urls) {
    try {
      const res = await fetchWithTimeout(`${baseUrl}/health`, { method: 'GET' }, 2500);
      if (res.ok) {
        return { online: true, activeApiUrl: baseUrl, status: res.status };
      }
    } catch {
      // Sonraki aday URL'i dene
    }
  }
  return { online: false, activeApiUrl: urls[0], status: 0 };
}

export async function syncOrdersToVds(settings, orders) {
  const payload = toVdsExtensionOrdersPayload(settings?.shopId, orders);
  if (payload.orders.length === 0) {
    return { ok: false, error: 'Aktarılacak geçerli sipariş bulunamadı.' };
  }

  const health = await checkVdsHealth(settings);
  const baseUrl = health.activeApiUrl;

  try {
    const res = await fetchWithTimeout(
      `${baseUrl}/api/etsy/orders/sync-from-extension`,
      {
        method: 'POST',
        headers: buildAuthHeaders(settings),
        body: JSON.stringify(payload)
      },
      12000
    );

    const text = await res.text();
    let data = null;
    try { data = JSON.parse(text); } catch { data = { raw: text }; }

    if (res.ok) {
      return {
        ok: true,
        activeApiUrl: baseUrl,
        syncedCount: data?.updatedCount || data?.count || payload.orders.length,
        fullAddressCount: payload.orders.filter((o) => o.isFullAddress).length,
        data
      };
    }
    return {
      ok: false,
      activeApiUrl: baseUrl,
      status: res.status,
      error: data?.message || data?.error || `HTTP ${res.status}`
    };
  } catch (err) {
    return {
      ok: false,
      activeApiUrl: baseUrl,
      error: err?.message || 'VDS sunucusuna bağlanılamadı.'
    };
  }
}

export async function syncFinanceSnapshotToVds(settings, snapshot) {
  const payload = toVdsFinanceSnapshotPayload(settings?.shopId, snapshot);
  if (!payload) {
    return {
      ok: false,
      error: 'Geçerli ödeme hesabı verisi okunamadı. Lütfen Etsy Ödeme Hesabı (Payment Account) sayfasını açın.'
    };
  }

  const health = await checkVdsHealth(settings);
  const baseUrl = health.activeApiUrl;

  try {
    const res = await fetchWithTimeout(
      `${baseUrl}/api/etsy/financial/payment-reserve-snapshot`,
      {
        method: 'POST',
        headers: buildAuthHeaders(settings),
        body: JSON.stringify(payload)
      },
      8000
    );

    const text = await res.text();
    let data = null;
    try { data = JSON.parse(text); } catch { data = { raw: text }; }

    if (res.ok) {
      return { ok: true, activeApiUrl: baseUrl, payload, data };
    }
    return {
      ok: false,
      activeApiUrl: baseUrl,
      status: res.status,
      error: data?.message || data?.error || `HTTP ${res.status}`
    };
  } catch (err) {
    return {
      ok: false,
      activeApiUrl: baseUrl,
      error: err?.message || 'VDS sunucusuna bağlanılamadı.'
    };
  }
}

export async function parseAddressViaAi(settings, rawAddress) {
  const cleanRaw = String(rawAddress || '').trim();
  if (!cleanRaw) {
    return { ok: false, error: 'Ayrıştırılacak ham adres metni boş.' };
  }

  const health = await checkVdsHealth(settings);
  const baseUrl = health.activeApiUrl;

  try {
    const res = await fetchWithTimeout(
      `${baseUrl}/api/etsy/orders/ai-parse-address`,
      {
        method: 'POST',
        headers: buildAuthHeaders(settings),
        body: JSON.stringify({ rawAddress: cleanRaw })
      },
      15000
    );

    if (!res.ok) {
      return { ok: false, status: res.status, error: `HTTP ${res.status}` };
    }
    const data = await res.json();
    return { ok: true, activeApiUrl: baseUrl, data };
  } catch (err) {
    return { ok: false, error: err?.message || 'AI servisine ulaşılamadı.' };
  }
}
