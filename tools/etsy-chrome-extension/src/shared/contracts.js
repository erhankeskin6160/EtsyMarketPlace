// Etsy Copilot v2.5.0 — Ortak Sözleşmeler, Depolama Anahtarları ve VDS DTO Eşleyicileri

export const EXTENSION_VERSION = '2.5.0';
export const STORAGE_SCHEMA_VERSION = 2;

export const STORAGE_KEYS = Object.freeze({
  SETTINGS: 'copilot_v2_settings',
  STATE: 'copilot_v2_state',
  LEGACY_STORED_ORDERS: 'storedOrders'
});

export const DEFAULT_SETTINGS = Object.freeze({
  apiUrl: 'http://localhost:5263',
  fallbackApiUrl: 'http://5.180.81.148:5263',
  apiKey: 'etsy-vds-sync-key-2026',
  shopId: '53236321',
  autoSyncFinance: false,
  autoExpandAddresses: true
});

export const ACTIONS = Object.freeze({
  // Popup <-> Service Worker
  SW_SCAN_ACTIVE_TAB: 'SW_SCAN_ACTIVE_TAB',
  SW_RESOLVE_FULL_ADDRESSES: 'SW_RESOLVE_FULL_ADDRESSES',
  SW_SYNC_ORDERS_TO_VDS: 'SW_SYNC_ORDERS_TO_VDS',
  SW_SYNC_FINANCE_TO_VDS: 'SW_SYNC_FINANCE_TO_VDS',
  SW_AI_PARSE_ADDRESS: 'SW_AI_PARSE_ADDRESS',
  SW_CHECK_VDS_HEALTH: 'SW_CHECK_VDS_HEALTH',
  SW_CLEAR_ARCHIVE: 'SW_CLEAR_ARCHIVE',
  SW_SAVE_SETTINGS: 'SW_SAVE_SETTINGS',
  SW_GET_DIAGNOSTICS: 'SW_GET_DIAGNOSTICS',

  // Content Agent <-> Service Worker
  CA_EXTRACT_STRUCTURAL: 'CA_EXTRACT_STRUCTURAL',
  CA_EXPAND_INLINE_ADDRESSES: 'CA_EXPAND_INLINE_ADDRESSES',
  CA_DRAWER_CAPTURED: 'CA_DRAWER_CAPTURED',
  CA_PING: 'CA_PING'
});

export function isValidReceiptId(value) {
  return /^\d{9,12}$/.test(String(value || '').trim());
}

/**
 * İki sipariş kaydını deterministik öncelikle birleştirir:
 * 1. Tam açık adres (streetAddress dolu) her zaman kapalı özete göre önceliklidir.
 * 2. React Fiber (MAIN world) yapılandırılmış alanları, eksik DOM alanlarını tamamlar.
 * 3. Geçersiz veya jenerik alıcı adı ('Etsy Müşterisi'), gerçek alıcı adını asla ezemez.
 */
export function mergeOrderDto(base, incoming) {
  if (!base) return incoming ? normalizeOrderDto(incoming) : null;
  if (!incoming) return normalizeOrderDto(base);

  const a = normalizeOrderDto(base);
  const b = normalizeOrderDto(incoming);
  if (!a) return b;
  if (!b) return a;

  const preferBAddress = Boolean(b.isFullAddress || (!a.isFullAddress && (b.city || b.state || b.postalCode)));

  const streetAddress = preferBAddress ? (b.streetAddress || a.streetAddress) : (a.streetAddress || b.streetAddress);
  const secondAddress = preferBAddress ? (b.secondAddress || a.secondAddress) : (a.secondAddress || b.secondAddress);
  const city = preferBAddress ? (b.city || a.city) : (a.city || b.city);
  const state = preferBAddress ? (b.state || a.state) : (a.state || b.state);
  const postalCode = preferBAddress ? (b.postalCode || a.postalCode) : (a.postalCode || b.postalCode);
  const country = preferBAddress
    ? (b.country || a.country)
    : (a.country || b.country);
  const phone = b.phone || a.phone || '';
  const iossNumber = b.iossNumber || a.iossNumber || '';

  const aHasRealBuyer = Boolean(a.buyerName && a.buyerName !== 'Etsy Müşterisi');
  const bHasRealBuyer = Boolean(b.buyerName && b.buyerName !== 'Etsy Müşterisi');

  let buyerName = 'Etsy Müşterisi';
  if (bHasRealBuyer && (preferBAddress || !aHasRealBuyer || b.dataLayer === 'react-fiber')) {
    buyerName = b.buyerName;
  } else if (aHasRealBuyer) {
    buyerName = a.buyerName;
  } else if (bHasRealBuyer) {
    buyerName = b.buyerName;
  }

  const recipientName = b.recipientName || a.recipientName || '';
  const buyerAccountName = b.buyerAccountName || a.buyerAccountName || '';

  const isFullAddress = Boolean(streetAddress && (city || state || postalCode || country));
  const shippingAddress = [streetAddress, secondAddress, city, state, postalCode, country]
    .filter(Boolean)
    .join(', ');

  const rawAddressLines = [
    buyerName !== 'Etsy Müşterisi' ? buyerName : '',
    streetAddress,
    secondAddress,
    [postalCode, city, state].filter(Boolean).join(' '),
    country,
    phone ? `Tel: ${phone}` : '',
    iossNumber ? `IOSS: ${iossNumber}` : ''
  ].filter(Boolean);

  const dataLayer = (b.dataLayer === 'deepseek-ai' || a.dataLayer === 'deepseek-ai')
    ? 'deepseek-ai'
    : ((b.dataLayer === 'react-fiber' && b.isFullAddress)
      ? 'react-fiber'
      : ((a.dataLayer === 'react-fiber' && a.isFullAddress)
        ? 'react-fiber'
        : (b.dataLayer || a.dataLayer || 'structural-dom')));

  const sourceType = preferBAddress ? (b.sourceType || a.sourceType) : (a.sourceType || b.sourceType);

  return {
    receiptId: a.receiptId,
    buyerName,
    recipientName,
    buyerAccountName,
    shippingAddress: shippingAddress || b.shippingAddress || a.shippingAddress || '',
    streetAddress: streetAddress || '',
    secondAddress: secondAddress || '',
    city: city || '',
    state: state || '',
    postalCode: postalCode || '',
    country: country || '',
    phone,
    iossNumber,
    rawAddressText: rawAddressLines.join('\n') || b.rawAddressText || a.rawAddressText || '',
    isFullAddress,
    productTitle: (b.productTitle && b.productTitle !== 'Etsy Ürünü') ? b.productTitle : (a.productTitle || b.productTitle || ''),
    orderTotal: b.orderTotal != null ? b.orderTotal : a.orderTotal,
    currency: b.currency || a.currency || 'USD',
    orderDate: b.orderDate || a.orderDate || '',
    status: b.status || a.status || 'Processing',
    trackingNumber: b.trackingNumber || a.trackingNumber || '',
    sku: b.sku || a.sku || '',
    variation: b.variation || a.variation || '',
    transactionId: b.transactionId || a.transactionId || '',
    shippingMethod: b.shippingMethod || a.shippingMethod || '',
    dataLayer,
    sourceType: sourceType || 'Yapısal Okuma',
    updatedAt: new Date().toISOString()
  };
}

export function normalizeOrderDto(raw) {
  if (!raw) return null;
  const receiptId = String(raw.receiptId || raw.orderId || raw.receipt_id || raw.order_id || '').trim();
  if (!isValidReceiptId(receiptId)) return null;

  const streetAddress = cleanSingleLine(raw.streetAddress);
  const secondAddress = cleanSingleLine(raw.secondAddress);
  const city = cleanSingleLine(raw.city);
  const state = cleanSingleLine(raw.state);
  const postalCode = cleanSingleLine(raw.postalCode);
  const country = cleanSingleLine(raw.country);
  const phone = cleanSingleLine(raw.phone);
  const iossNumber = cleanSingleLine(raw.iossNumber);

  const recipientName = cleanPersonName(raw.recipientName);
  const buyerAccountName = cleanPersonName(raw.buyerAccountName);
  const explicitBuyer = cleanPersonName(raw.buyerName);
  const buyerName = explicitBuyer || recipientName || buyerAccountName || 'Etsy Müşterisi';

  const isFullAddress = Boolean(streetAddress && (city || state || postalCode || country));
  const computedShipping = [streetAddress, secondAddress, city, state, postalCode, country]
    .filter(Boolean)
    .join(', ');

  const totalNum = raw.orderTotal != null && !Number.isNaN(Number(raw.orderTotal))
    ? Number(Number(raw.orderTotal).toFixed(2))
    : null;

  const rawAddressLines = [
    buyerName !== 'Etsy Müşterisi' ? buyerName : '',
    streetAddress,
    secondAddress,
    [postalCode, city, state].filter(Boolean).join(' '),
    country,
    phone ? `Tel: ${phone}` : '',
    iossNumber ? `IOSS: ${iossNumber}` : ''
  ].filter(Boolean);

  return {
    receiptId,
    buyerName,
    recipientName,
    buyerAccountName,
    shippingAddress: computedShipping || cleanSingleLine(raw.shippingAddress) || '',
    streetAddress,
    secondAddress,
    city,
    state,
    postalCode,
    country,
    phone,
    iossNumber,
    rawAddressText: cleanMultiline(raw.rawAddressText) || rawAddressLines.join('\n'),
    isFullAddress,
    productTitle: cleanSingleLine(raw.productTitle),
    orderTotal: totalNum,
    currency: cleanSingleLine(raw.currency) || 'USD',
    orderDate: cleanSingleLine(raw.orderDate),
    status: cleanSingleLine(raw.status) || 'Processing',
    trackingNumber: cleanSingleLine(raw.trackingNumber || raw.trackingCode),
    sku: cleanSingleLine(raw.sku),
    variation: cleanSingleLine(raw.variation),
    transactionId: cleanSingleLine(raw.transactionId),
    shippingMethod: cleanSingleLine(raw.shippingMethod),
    dataLayer: raw.dataLayer || 'structural-dom',
    sourceType: cleanSingleLine(raw.sourceType) || 'Yapısal Okuma',
    updatedAt: raw.updatedAt || new Date().toISOString()
  };
}

/**
 * C# Backend: SyncExtensionOrdersRequest -> ExtensionOrderSyncItemDto birebir sözleşme eşleyicisi
 */
export function toVdsExtensionOrdersPayload(shopId, orders) {
  return {
    shopId: String(shopId || DEFAULT_SETTINGS.shopId).trim(),
    orders: (Array.isArray(orders) ? orders : [])
      .map(normalizeOrderDto)
      .filter(Boolean)
      .map((o) => ({
        receiptId: o.receiptId,
        buyerName: o.buyerName,
        shippingAddress: o.shippingAddress || null,
        streetAddress: o.streetAddress || null,
        secondAddress: o.secondAddress || null,
        city: o.city || null,
        state: o.state || null,
        postalCode: o.postalCode || null,
        country: o.country || null,
        phone: o.phone || null,
        rawAddressText: o.rawAddressText || null,
        isFullAddress: Boolean(o.isFullAddress),
        productTitle: o.productTitle || null,
        orderTotal: o.orderTotal,
        currency: o.currency || 'USD',
        trackingCode: o.trackingNumber || null,
        trackingNumber: o.trackingNumber || null,
        iossNumber: o.iossNumber || null
      }))
  };
}

/**
 * C# Backend: EtsyPaymentReserveSnapshotDto birebir sözleşme eşleyicisi (Sıfır Mock Veri!)
 */
export function toVdsFinanceSnapshotPayload(shopId, snapshot) {
  if (!snapshot || snapshot.currentBalanceTry == null || Number.isNaN(Number(snapshot.currentBalanceTry))) {
    return null;
  }
  const cur = Number(Number(snapshot.currentBalanceTry).toFixed(2));
  const pending = Number(Number(snapshot.pendingBalanceTry ?? 0).toFixed(2));
  const total = snapshot.totalBalanceTry != null && !Number.isNaN(Number(snapshot.totalBalanceTry))
    ? Number(Number(snapshot.totalBalanceTry).toFixed(2))
    : Number((cur + pending).toFixed(2));
  const pct = Number(snapshot.reservePercent ?? 0);
  const days = parseInt(String(snapshot.reserveHoldDays ?? 0), 10) || 0;
  const reeval = cleanSingleLine(snapshot.reevaluationDate) || null;
  const statusMsg = cleanSingleLine(snapshot.statusMessage)
    || (pct > 0 ? `${pct}% in reserve${days ? ` for ${days} days` : ''}${reeval ? `. Next review: ${reeval}` : ''}` : 'Active');

  return {
    shopId: String(shopId || snapshot.shopId || DEFAULT_SETTINGS.shopId).trim(),
    currentBalanceTry: cur,
    pendingBalanceTry: pending,
    totalBalanceTry: total,
    reservePercent: pct,
    reserveHoldDays: days,
    reevaluationDate: reeval,
    statusMessage: statusMsg,
    updatedAt: new Date().toISOString()
  };
}

function cleanSingleLine(val) {
  if (val == null) return '';
  return String(val).replace(/[\r\n\t]+/g, ' ').replace(/\s{2,}/g, ' ').trim();
}

function cleanMultiline(val) {
  if (val == null) return '';
  return String(val)
    .split(/\r?\n/)
    .map((l) => l.trim())
    .filter(Boolean)
    .join('\n');
}

function cleanPersonName(val) {
  if (!val) return '';
  const t = String(val).replace(/[▾▼⌄⌃]/g, '').replace(/\s{2,}/g, ' ').trim();
  if (t.length < 2 || t.length > 80) return '';
  if (/[\r\n]/.test(t) || /^\d+$/.test(t)) return '';
  if (/\b\d{9,12}\b/.test(t) || /\(\s*Etsy\s*\)/i.test(t)) return '';
  if (/(?:ABD\s*\$|US\s*\$|\$|₺|€|£)\s*[\d,.]+/i.test(t)) return '';
  if (/\b(?:shop\s*coupon|coupon|\d+\s*%\s*off|select\s*all|complete\s*order|more\s*actions|ship\s*to|shipped\s*to|no\s*tracking|order\s*in\s*reserve|usps\s*verified|standard\s*shipping|etsy\s*müşterisi|gizlilik\s*ayarlar[ıi]|yard[ıi]m\s*al[ıi]n|mağaza\s*müdürü|tümünü\s*seç|takip\s*yok)\b/i.test(t)) {
    return '';
  }
  return t;
}
