// Etsy Copilot v2.5.0 — Katman 1: Salt-Okunur MAIN World React Fiber, Preact/Backbone & Etsy.Context Okuyucu
// GÜVENLİK GARANTİSİ (Anti-Ban / DataDome Uyumlu):
// - window.fetch, XMLHttpRequest veya herhangi bir tarayıcı prototipine KESİNLİKLE DOKUNMAZ.
// - Sayfada kalıcı global değişken veya event listener bırakmaz.
// - Yalnızca bellekteki mevcut React 16/17/18 Fiber ağacını ve window.Etsy.Context verisini salt-okunur tarar.

(function () {
  'use strict';

  const ISO_COUNTRIES = {
    US: 'United States', USA: 'United States', CA: 'Canada', GB: 'United Kingdom', UK: 'United Kingdom',
    AU: 'Australia', DE: 'Germany', FR: 'France', NL: 'Netherlands', IT: 'Italy', ES: 'Spain',
    TR: 'Turkey', AT: 'Austria', BE: 'Belgium', CH: 'Switzerland', SE: 'Sweden', NO: 'Norway',
    DK: 'Denmark', FI: 'Finland', PL: 'Poland', IE: 'Ireland', NZ: 'New Zealand', JP: 'Japan',
    PT: 'Portugal', GR: 'Greece', CZ: 'Czech Republic', HU: 'Hungary', RO: 'Romania', BG: 'Bulgaria',
    HR: 'Croatia', SK: 'Slovakia', SI: 'Slovenia', EE: 'Estonia', LV: 'Latvia', LT: 'Lithuania',
    CY: 'Cyprus', MT: 'Malta', IL: 'Israel', AE: 'United Arab Emirates', MX: 'Mexico', BR: 'Brazil'
  };

  const FALLBACK_ETSY_COUNTRY_IDS = {
    209: 'United States', 79: 'Canada', 105: 'United Kingdom', 61: 'Australia', 91: 'Germany',
    103: 'France', 150: 'Netherlands', 128: 'Italy', 99: 'Spain', 200: 'Turkey', 63: 'Austria',
    68: 'Belgium', 194: 'Switzerland', 193: 'Sweden', 162: 'Norway', 93: 'Denmark', 102: 'Finland',
    174: 'Poland', 123: 'Ireland', 158: 'New Zealand', 131: 'Japan', 175: 'Portugal', 112: 'Greece'
  };

  function runMainWorldScan(rootWindow, rootDoc) {
    const win = rootWindow || (typeof window !== 'undefined' ? window : null);
    const doc = rootDoc || (typeof document !== 'undefined' ? document : null);
    if (!win || !doc || !doc.body) {
      return { orders: [], finance: null, diagnostics: { error: 'NO_DOM' } };
    }

    const diagnostics = {
      layer: 'react-reader.main',
      version: '2.4.0',
      fiberNodesVisited: 0,
      fiberDetected: false,
      reduxStoresFound: 0,
      countriesLoaded: 0,
      domOrderAnchors: 0,
      drawerAnchors: 0,
      ordersMatched: 0,
      fullAddressesMatched: 0,
      etsyContextTopKeys: [],
      firstAnchorFrameworkKeys: [],
      sampleOrderKeys: [],
      sampleAddressKeys: []
    };

    // 1. window.Etsy.Context içinden ülke tablosu ve mağaza kimliğini çöz
    const countryById = { ...FALLBACK_ETSY_COUNTRY_IDS };
    const countryByCode = { ...ISO_COUNTRIES };
    const skippedArrays = new WeakSet();
    let detectedShopId = null;

    try {
      const ctxData = win.Etsy && win.Etsy.Context ? win.Etsy.Context.data : null;
      if (ctxData && typeof ctxData === 'object') {
        diagnostics.etsyContextTopKeys = Object.keys(ctxData).slice(0, 35);
        const seenCtx = new WeakSet();
        const scanContext = (obj, depth) => {
          if (!obj || typeof obj !== 'object' || depth > 8 || seenCtx.has(obj)) return;
          seenCtx.add(obj);
          if (Array.isArray(obj)) {
            if (obj.length > 20 && obj[0] && typeof obj[0] === 'object' && 'id' in obj[0] && 'name' in obj[0] && ('code' in obj[0] || 'iso_country_code' in obj[0])) {
              skippedArrays.add(obj);
              for (const c of obj) {
                if (c && c.id != null && c.name) {
                  countryById[Number(c.id)] = String(c.name);
                  diagnostics.countriesLoaded++;
                }
                if (c && c.code && c.name) {
                  countryByCode[String(c.code).toUpperCase()] = String(c.name);
                }
              }
            }
            return;
          }
          if (!detectedShopId && (obj.shop_id || obj.shopId) && /^\d{5,12}$/.test(String(obj.shop_id || obj.shopId))) {
            detectedShopId = String(obj.shop_id || obj.shopId);
          }
          for (const key of Object.keys(obj)) {
            scanContext(obj[key], depth + 1);
          }
        };
        scanContext(ctxData, 0);
      }
    } catch {}

    function resolveCountry(val) {
      if (val == null || val === '') return '';
      if (typeof val === 'number' || /^\d+$/.test(String(val).trim())) {
        return countryById[Number(val)] || '';
      }
      if (typeof val === 'object') {
        return resolveCountry(val.country_name || val.name || val.country_code || val.iso_country_code || val.code || val.country_id || val.id);
      }
      const s = String(val).trim();
      if (countryByCode[s.toUpperCase()]) return countryByCode[s.toUpperCase()];
      return s.length >= 3 ? s : '';
    }

    function parseMoney(val, fallbackCurrency) {
      if (val == null) return { amount: null, currency: fallbackCurrency || '' };
      if (typeof val === 'number') {
        return { amount: Number(val.toFixed(2)), currency: fallbackCurrency || '' };
      }
      if (typeof val === 'string') {
        const clean = val.replace(/[^\d.,+-]/g, '').trim();
        if (!clean) return { amount: null, currency: fallbackCurrency || '' };
        const normalized = clean.includes(',') && clean.includes('.')
          ? (clean.lastIndexOf(',') > clean.lastIndexOf('.') ? clean.replace(/\./g, '').replace(',', '.') : clean.replace(/,/g, ''))
          : (clean.includes(',') && clean.length - clean.lastIndexOf(',') - 1 <= 2 ? clean.replace(',', '.') : clean.replace(/,/g, ''));
        const num = Number.parseFloat(normalized);
        const cur = val.includes('₺') || /\bTL\b|\bTRY\b/i.test(val)
          ? 'TRY'
          : (val.includes('€') || /\bEUR\b/i.test(val)
            ? 'EUR'
            : (val.includes('£') || /\bGBP\b/i.test(val) ? 'GBP' : (fallbackCurrency || 'USD')));
        return { amount: Number.isFinite(num) ? Number(num.toFixed(2)) : null, currency: cur };
      }
      if (typeof val === 'object') {
        const cur = String(val.currency_code || val.currencyCode || val.currency || fallbackCurrency || 'USD').toUpperCase();
        if (val.amount != null && val.divisor != null && Number(val.divisor) > 0) {
          const computed = Number(val.amount) / Number(val.divisor);
          if (Number.isFinite(computed)) {
            return { amount: Number(computed.toFixed(2)), currency: cur };
          }
        }
        for (const k of ['raw', 'value', 'decimal_value', 'formatted', 'amount_formatted', 'display']) {
          if (val[k] != null) {
            const sub = parseMoney(val[k], cur);
            if (sub.amount != null) return sub;
          }
        }
        // Kuruş/cent tamsayısı olmayan doğrudan ondalık amount
        if (typeof val.amount === 'number' && !('divisor' in val)) {
          return { amount: Number(val.amount.toFixed(2)), currency: cur };
        }
      }
      return { amount: null, currency: fallbackCurrency || '' };
    }

    function formatEpochOrDate(val) {
      if (!val) return '';
      if (typeof val === 'number' || /^\d{10,13}$/.test(String(val).trim())) {
        const n = Number(val);
        const ms = n < 1e11 ? n * 1000 : n;
        const d = new Date(ms);
        if (!Number.isNaN(d.getTime()) && d.getFullYear() >= 2015 && d.getFullYear() <= 2035) {
          return d.toLocaleDateString('en-US', { month: 'short', day: 'numeric', year: 'numeric' });
        }
      }
      if (typeof val === 'string') {
        const t = val.trim();
        if (t.length <= 40) return t;
      }
      return '';
    }

    // 2. Ekrandaki görünür sipariş ID'lerini ve izole DOM satırlarını bul
    const ordersAcc = new Map();
    const visibleOrderRows = new Map();

    function ensureOrderRecord(orderId) {
      const id = String(orderId || '').trim();
      if (!/^\d{9,12}$/.test(id)) return null;
      if (!ordersAcc.has(id)) {
        ordersAcc.set(id, {
          receiptId: id,
          recipientName: '',
          buyerAccountName: '',
          streetAddress: '',
          secondAddress: '',
          city: '',
          state: '',
          postalCode: '',
          country: '',
          phone: '',
          iossNumber: '',
          productTitle: '',
          orderTotal: null,
          currency: '',
          orderDate: '',
          status: '',
          trackingNumber: '',
          sku: '',
          variation: '',
          transactionId: '',
          shippingMethod: '',
          dataLayer: 'react-fiber',
          sourceType: 'React Bellek (MAIN Fiber)'
        });
      }
      return ordersAcc.get(id);
    }

    function countIdsInElement(el) {
      const set = new Set();
      if (!el) return set;
      const txt = (el.textContent || '').replace(/\u00A0/g, ' ');
      for (const m of txt.matchAll(/#\s*(\d{9,12})\b/g)) set.add(m[1]);
      for (const m of txt.matchAll(/\b(\d{9,12})\s*(?:\(\s*Etsy\s*\)|via\s+Etsy\b)/gi)) set.add(m[1]);
      return set;
    }

    function isCssHidden(el) {
      let cur = el;
      while (cur && cur !== doc.body) {
        if (cur.hasAttribute && cur.hasAttribute('hidden')) return true;
        if (cur.id === 'gdpr-privacy-settings') return true;
        if (typeof win.getComputedStyle === 'function') {
          try {
            const st = win.getComputedStyle(cur);
            if (st && (st.display === 'none' || st.visibility === 'hidden')) return true;
          } catch {}
        }
        cur = cur.parentElement;
      }
      return false;
    }

    function matchOrderIdOnCompactElement(el) {
      if (!el || !el.tagName) return null;
      const tag = el.tagName.toUpperCase();
      if (tag === 'SCRIPT' || tag === 'STYLE' || tag === 'NOSCRIPT' || tag === 'TEMPLATE' || tag === 'SVG') return null;

      if (tag === 'A') {
        const href = el.getAttribute && el.getAttribute('href');
        const hm = href && href.match(/[?&]order_id=(\d{9,12})\b/);
        if (hm) {
          const t = (el.textContent || '').replace(/\s+/g, '').trim();
          if (t.includes(hm[1]) && t.length <= 80) return hm[1];
        }
      }

      const norm = (el.textContent || '').replace(/\u00A0/g, ' ').replace(/\s+/g, ' ').trim();
      if (!norm || norm.length > 180) return null;
      const m = norm.match(/#\s*(\d{9,12})\b/) || norm.match(/\b(\d{9,12})\s*(?:\(\s*Etsy\s*\)|via\s+Etsy\b)/i);
      return m ? m[1] : null;
    }

    const drawerRowScopes = [];
    try {
      // P-minimal (en iç yaprak) sipariş çapası keşfi: "#" ve "<a>4210985421</a>" veya Google Çeviri "<font>" sarmallarında bile tam yaprak elementi bulur!
      const allEls = doc.body.querySelectorAll('*');
      const minimalAnchors = [];
      for (const el of allEls) {
        const id = matchOrderIdOnCompactElement(el);
        if (!id) continue;
        const descendants = el.querySelectorAll ? el.querySelectorAll('*') : (el.children || []);
        let descendantAlsoMatches = false;
        for (let i = 0; i < descendants.length; i++) {
          if (matchOrderIdOnCompactElement(descendants[i]) === id) {
            descendantAlsoMatches = true;
            break;
          }
        }
        if (descendantAlsoMatches) continue;
        if (isCssHidden(el)) continue;
        minimalAnchors.push({ id, el });
      }

      // İlk çapanın ve atalarının JS framework özelliklerini tanılamaya kaydet
      if (minimalAnchors.length > 0) {
        const fwSet = new Set();
        let probe = minimalAnchors[0].el;
        let steps = 0;
        while (probe && steps < 10) {
          steps++;
          for (const k of Object.keys(probe)) {
            if (k.startsWith('__') || k.startsWith('_react') || k.startsWith('jQuery') || k.startsWith('$')) {
              fwSet.add(k.replace(/\$[a-z0-9]+$/i, '$*'));
            }
          }
          probe = probe.parentElement;
        }
        diagnostics.firstAnchorFrameworkKeys = Array.from(fwSet).slice(0, 15);
      }

      for (const { id, el } of minimalAnchors) {
        const ctxText = ((el.parentElement ? el.parentElement.textContent : el.textContent) || '').replace(/\s+/g, ' ').slice(0, 160);
        const isDrawerAnchor = /\bvia\s+Etsy\b|\bEtsy\s+(?:üzerinden|aracılığıyla)\b/i.test(ctxText)
          || Boolean(el.closest && el.closest('aside, [role="dialog"], [aria-modal="true"], .wt-overlay, [data-wt-overlay], [data-region*="drawer" i], [class*="drawer" i], [class*="flyout" i]'));

        let curr = el;
        let rowEl = el;
        while (curr && curr !== doc.body && curr.tagName !== 'MAIN' && curr.id !== 'root' && curr.id !== 'content') {
          if (curr.querySelector && curr.querySelector('#nav-subnav, .action-needed, input[placeholder*="Search" i], input[placeholder*="Ara" i], input[type="search"]')) {
            break;
          }
          if (curr.querySelectorAll && curr.querySelectorAll('[role="tab"]').length >= 3) {
            break;
          }
          const ids = countIdsInElement(curr);
          if (ids.size > 1) break;
          if (isDrawerAnchor && curr.querySelector && curr.querySelector('input[type="checkbox"]')) break;
          if (ids.size === 1 && ids.has(id)) rowEl = curr;
          curr = curr.parentElement;
        }

        if (isDrawerAnchor) {
          drawerRowScopes.push({ id, rowEl });
        } else if (!visibleOrderRows.has(id)) {
          visibleOrderRows.set(id, rowEl);
        }
      }
    } catch {}

    diagnostics.domOrderAnchors = visibleOrderRows.size;
    diagnostics.drawerAnchors = drawerRowScopes.length;

    // 3. Nesne şekillerinden (Address / Buyer / Transaction / Order) alan çıkarıcılar
    function getFiberFromDom(el) {
      if (!el || typeof el !== 'object') return null;
      const keys = Object.keys(el);
      for (const k of keys) {
        if (k.startsWith('__reactFiber$') || k.startsWith('__reactInternalInstance$')) {
          return el[k];
        }
      }
      return null;
    }

    function getPropsFromDom(el) {
      if (!el || typeof el !== 'object') return null;
      const keys = Object.keys(el);
      for (const k of keys) {
        if (k.startsWith('__reactProps$')) {
          return el[k];
        }
      }
      return null;
    }

    function getRootContainerFiber(el) {
      if (!el || typeof el !== 'object') return null;
      const keys = Object.keys(el);
      for (const k of keys) {
        if (k.startsWith('__reactContainer$')) return el[k];
      }
      if (el._reactRootContainer && el._reactRootContainer._internalRoot) {
        return el._reactRootContainer._internalRoot.current;
      }
      return null;
    }

    function pickString(obj, keys) {
      if (!obj || typeof obj !== 'object') return '';
      for (const k of keys) {
        const v = obj[k];
        if (typeof v === 'string' && v.trim()) return v.trim();
        if (typeof v === 'number' && Number.isFinite(v)) return String(v);
      }
      return '';
    }

    function looksLikeAddressObject(obj) {
      if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return false;
      const hasStreetKey = ('first_line' in obj) || ('firstLine' in obj) || ('address_line_1' in obj)
        || ('addressLine1' in obj) || ('address1' in obj) || ('street' in obj) || ('street_1' in obj)
        || ('streetAddress' in obj);
      const hasCityOrZipKey = ('city' in obj) || ('zip' in obj) || ('postal_code' in obj)
        || ('postalCode' in obj) || ('country_id' in obj) || ('countryId' in obj) || ('country_code' in obj);
      return Boolean(hasStreetKey && hasCityOrZipKey);
    }

    function applyAddressObject(rec, addr) {
      if (!rec || !addr || typeof addr !== 'object') return;
      if (diagnostics.sampleAddressKeys.length === 0) {
        diagnostics.sampleAddressKeys = Object.keys(addr).slice(0, 18);
      }

      const recName = pickString(addr, ['name', 'recipient_name', 'recipientName', 'full_name', 'fullName', 'addressee']);
      const line1 = pickString(addr, ['first_line', 'firstLine', 'address_line_1', 'addressLine1', 'address1', 'line1', 'street_1', 'street', 'streetAddress']);
      const line2 = pickString(addr, ['second_line', 'secondLine', 'address_line_2', 'addressLine2', 'address2', 'line2', 'street_2', 'apt', 'subpremise']);
      const city = pickString(addr, ['city', 'town', 'locality', 'city_name']);
      const state = pickString(addr, ['state', 'region', 'province', 'state_code', 'administrative_area']);
      const zip = pickString(addr, ['zip', 'postal_code', 'postalCode', 'postcode']);
      const country = resolveCountry(addr.country_name || addr.countryName || addr.country || addr.country_code || addr.countryCode || addr.iso_country_code || addr.country_id || addr.countryId);
      const phone = pickString(addr, ['phone', 'phone_number', 'phoneNumber', 'formatted_phone', 'recipient_phone']);
      const ioss = pickString(addr, ['ioss_number', 'iossNumber', 'vat_number', 'tax_id']);

      if (recName && !rec.recipientName) rec.recipientName = recName;
      if (line1) rec.streetAddress = line1;
      if (line2 && !rec.secondAddress) rec.secondAddress = line2;
      if (city && (!rec.city || line1)) rec.city = city;
      if (state && (!rec.state || line1)) rec.state = state;
      if (zip && (!rec.postalCode || line1)) rec.postalCode = zip;
      if (country && (!rec.country || line1)) rec.country = country;
      if (phone && !rec.phone) rec.phone = phone;
      if (ioss && !rec.iossNumber) rec.iossNumber = ioss;

      // Eğer formatted_address dizisi varsa ve first_line boşsa satırları destekle
      if (!rec.streetAddress && Array.isArray(addr.address_lines || addr.lines) && (addr.address_lines || addr.lines).length >= 3) {
        const arr = (addr.address_lines || addr.lines).map((s) => String(s || '').trim()).filter(Boolean);
        if (arr.length >= 3 && !rec.recipientName) rec.recipientName = arr[0];
        if (arr.length >= 3) rec.streetAddress = arr[1];
      }
    }

    function applyBuyerObject(rec, buyer) {
      if (!rec || !buyer || typeof buyer !== 'object') return;
      const bName = pickString(buyer, ['real_name', 'realName', 'full_name', 'fullName', 'name', 'display_name', 'displayName', 'buyer_name', 'buyerName', 'login_name', 'username']);
      if (bName && !rec.buyerAccountName) {
        rec.buyerAccountName = bName;
      }
      if (!rec.phone) {
        const bPhone = pickString(buyer, ['phone', 'phone_number', 'phoneNumber']);
        if (bPhone) rec.phone = bPhone;
      }
      for (const addrKey of ['shipping_address', 'shippingAddress', 'to_address', 'toAddress', 'address', 'default_address']) {
        if (buyer[addrKey] && typeof buyer[addrKey] === 'object') {
          applyAddressObject(rec, buyer[addrKey]);
        }
      }
    }

    function applyTransactionObject(rec, tx) {
      if (!rec || !tx || typeof tx !== 'object') return;
      const txId = pickString(tx, ['transaction_id', 'transactionId']);
      if (txId && /^\d{7,14}$/.test(txId) && txId !== rec.receiptId && !rec.transactionId) {
        rec.transactionId = txId;
      }
      const title = pickString(tx, ['title', 'listing_title', 'listingTitle', 'product_title', 'productTitle', 'name']);
      if (title && title.length > 2 && !rec.productTitle) {
        rec.productTitle = title;
      }
      const sku = pickString(tx, ['sku', 'sku_number', 'product_sku'])
        || (tx.product_data && pickString(tx.product_data, ['sku']))
        || (tx.offering && pickString(tx.offering, ['sku']));
      if (sku && !rec.sku) {
        rec.sku = sku;
      }

      if (!rec.variation) {
        const vars = tx.variations || tx.selected_variations || tx.formatted_variations || tx.properties;
        const varParts = [];
        if (Array.isArray(vars)) {
          for (const v of vars) {
            if (!v || typeof v !== 'object') continue;
            const pName = pickString(v, ['formatted_name', 'property_name', 'name', 'label']);
            const pVal = pickString(v, ['formatted_value', 'value', 'selected_value']);
            if (pName && pVal) varParts.push(`${pName}: ${pVal}`);
            else if (pVal) varParts.push(pVal);
          }
        }
        const pers = pickString(tx, ['personalization', 'buyer_personalization', 'personalization_value']);
        if (pers) varParts.push(`Personalization: ${pers}`);
        if (varParts.length > 0) rec.variation = varParts.join(' | ');
      }
    }

    function detectOrderIdOnObject(obj) {
      if (!obj || typeof obj !== 'object' || Array.isArray(obj)) return null;
      for (const explicitKey of ['order_id', 'receipt_id', 'orderId', 'receiptId', 'etsy_receipt_id']) {
        const v = obj[explicitKey];
        if (v != null && /^\d{9,12}$/.test(String(v).trim())) {
          return String(v).trim();
        }
      }
      // Jenerik 'id' yalnızca açıkça sipariş benzeri alanlar varsa veya ekrandaki görünür sipariş ID'siyle eşleşiyorsa kabul edilir
      if (obj.id != null && /^\d{9,12}$/.test(String(obj.id).trim())) {
        const idStr = String(obj.id).trim();
        if (String(obj.listing_id || '') === idStr || String(obj.transaction_id || '') === idStr) {
          return null;
        }
        if (visibleOrderRows.has(idStr)) return idStr;
        const hasOrderSignature = ('fulfillment' in obj) || ('shipping_address' in obj) || ('to_address' in obj)
          || ('buyer_user_id' in obj) || ('is_shipped' in obj) || ('grandtotal' in obj)
          || ('transactions' in obj) || ('order_date' in obj);
        if (hasOrderSignature) return idStr;
      }
      return null;
    }

    function applyOrderLikeObject(rec, obj, entitiesLookup) {
      if (!rec || !obj || typeof obj !== 'object') return;
      if (diagnostics.sampleOrderKeys.length === 0 && ('order_id' in obj || 'receipt_id' in obj || 'fulfillment' in obj || 'grandtotal' in obj)) {
        diagnostics.sampleOrderKeys = Object.keys(obj).slice(0, 22);
      }

      // Alıcı adı (doğrudan alan veya alt nesne veya normalize buyer_id)
      const directBuyer = pickString(obj, ['buyer_name', 'buyerName', 'buyer_real_name', 'customer_name']);
      if (directBuyer && !rec.buyerAccountName) rec.buyerAccountName = directBuyer;
      if (obj.name && ('first_line' in obj || 'zip' in obj || 'city' in obj)) {
        applyAddressObject(rec, obj);
      }

      for (const bk of ['buyer', 'buyer_user', 'buyerUser', 'customer', 'user']) {
        if (obj[bk] && typeof obj[bk] === 'object') {
          applyBuyerObject(rec, obj[bk]);
        }
      }
      if (entitiesLookup) {
        const bId = obj.buyer_id || obj.buyer_user_id || obj.buyerId;
        if (bId != null && entitiesLookup.buyers && entitiesLookup.buyers[bId]) {
          applyBuyerObject(rec, entitiesLookup.buyers[bId]);
        }
      }

      // Adres alt nesneleri (Etsy v3, Mission Control SCLC fulfillment.to_address, shipping.destination vb.)
      for (const ak of ['shipping_address', 'shippingAddress', 'to_address', 'toAddress', 'destination', 'delivery_address', 'address', 'shipping_destination']) {
        if (obj[ak] && typeof obj[ak] === 'object') {
          applyAddressObject(rec, obj[ak]);
        }
      }
      if (obj.fulfillment && typeof obj.fulfillment === 'object') {
        for (const fk of ['to_address', 'toAddress', 'shipping_address', 'shippingAddress', 'destination', 'address']) {
          if (obj.fulfillment[fk] && typeof obj.fulfillment[fk] === 'object') {
            applyAddressObject(rec, obj.fulfillment[fk]);
          }
        }
        const shipMeth = pickString(obj.fulfillment, ['shipping_method', 'shippingMethod', 'mail_class', 'carrier_service']);
        if (shipMeth && !rec.shippingMethod) rec.shippingMethod = shipMeth;
        if (Array.isArray(obj.fulfillment.shipments)) {
          for (const sh of obj.fulfillment.shipments) {
            const trk = pickString(sh, ['tracking_code', 'trackingCode', 'tracking_number', 'trackingNumber']);
            if (trk && !rec.trackingNumber) rec.trackingNumber = trk;
            const mc = pickString(sh, ['mail_class', 'shipping_method', 'carrier_name']);
            if (mc && !rec.shippingMethod) rec.shippingMethod = mc;
          }
        }
      }
      if (entitiesLookup) {
        const addrId = obj.to_address_id || obj.shipping_address_id || obj.address_id || (obj.fulfillment && (obj.fulfillment.to_address_id || obj.fulfillment.address_id));
        if (addrId != null && entitiesLookup.addresses && entitiesLookup.addresses[addrId]) {
          applyAddressObject(rec, entitiesLookup.addresses[addrId]);
        }
      }

      // Düzleştirilmiş Etsy v3 Receipt adres alanları (name, first_line, second_line, city, state, zip, country_iso doğrudan receipt üstündeyse)
      if (('first_line' in obj || 'zip' in obj) && ('city' in obj || 'country_iso' in obj || 'country_id' in obj)) {
        applyAddressObject(rec, {
          ...obj,
          country_code: obj.country_iso || obj.country_code
        });
      }

      // Sipariş tutarı ve para birimi
      if (rec.orderTotal == null) {
        for (const pk of ['grandtotal', 'total_price', 'totalPrice', 'order_total', 'orderTotal', 'total_cost', 'total']) {
          if (obj[pk] != null) {
            const m = parseMoney(obj[pk], obj.currency_code || obj.currencyCode || rec.currency);
            if (m.amount != null) {
              rec.orderTotal = m.amount;
              if (m.currency) rec.currency = m.currency;
              break;
            }
          }
        }
        if (rec.orderTotal == null && obj.payment && typeof obj.payment === 'object') {
          for (const pk of ['grandtotal', 'total_price', 'total', 'buyer_total', 'order_total']) {
            if (obj.payment[pk] != null) {
              const m = parseMoney(obj.payment[pk], obj.payment.currency_code || rec.currency);
              if (m.amount != null) {
                rec.orderTotal = m.amount;
                if (m.currency) rec.currency = m.currency;
                break;
              }
            }
          }
        }
      }

      // Sipariş tarihi
      if (!rec.orderDate) {
        for (const dk of ['order_date', 'orderDate', 'ordered_date', 'creation_tsz', 'created_timestamp', 'create_timestamp', 'purchase_date']) {
          if (obj[dk] != null) {
            const dStr = formatEpochOrDate(obj[dk]);
            if (dStr) {
              rec.orderDate = dStr;
              break;
            }
          }
        }
      }

      // Kargo takip ve durum
      if (Array.isArray(obj.shipments)) {
        for (const sh of obj.shipments) {
          const trk = pickString(sh, ['tracking_code', 'trackingCode', 'tracking_number']);
          if (trk && !rec.trackingNumber) rec.trackingNumber = trk;
          const mc = pickString(sh, ['mail_class', 'shipping_method', 'carrier_name']);
          if (mc && !rec.shippingMethod) rec.shippingMethod = mc;
        }
      }
      const directTrk = pickString(obj, ['tracking_code', 'trackingCode', 'tracking_number', 'trackingNumber']);
      if (directTrk && !rec.trackingNumber) rec.trackingNumber = directTrk;

      if (!rec.status) {
        if (obj.is_canceled || obj.status === 'canceled') rec.status = 'Canceled';
        else if (rec.trackingNumber || obj.is_shipped === true) rec.status = 'Shipped';
        else if ('is_shipped' in obj && obj.is_shipped === false) rec.status = 'Processing';
      }

      // IOSS / Vergi
      const ioss = pickString(obj, ['ioss_number', 'iossNumber', 'vat_id']);
      if (ioss && !rec.iossNumber) rec.iossNumber = ioss;

      // Kalemler / Transactions
      for (const tk of ['transactions', 'items', 'line_items', 'lineItems', 'order_items']) {
        if (Array.isArray(obj[tk])) {
          for (const tx of obj[tk]) {
            if (tx && typeof tx === 'object') applyTransactionObject(rec, tx);
            else if (entitiesLookup && entitiesLookup.transactions && entitiesLookup.transactions[tx]) {
              applyTransactionObject(rec, entitiesLookup.transactions[tx]);
            }
          }
        }
      }
      if (('sku' in obj || 'listing_id' in obj || 'transaction_id' in obj) && ('title' in obj || 'variations' in obj)) {
        applyTransactionObject(rec, obj);
      }
    }

    // Derin nesne gezgini (döngüsel referans korumalı, bağlamsal contextOrderId destekli)
    const visitedObjects = new WeakSet();
    const discoveredStores = new Set();

    const SKIPPED_OBJECT_KEYS = new Set([
      'children', 'ownerDocument', 'view', 'window', 'document', 'parentNode', 'parentElement',
      'firstChild', 'lastChild', 'nextSibling', 'previousSibling',
      'return', 'child', 'sibling', 'alternate', 'stateNode',
      '_owner', '_debugOwner', '_debugSource', '_debugHookTypes', '__proto__'
    ]);

    const PRIORITY_OBJECT_KEYS = new Set([
      'orders', 'receipts', 'initial_data', 'initialData', 'data', 'states', 'state',
      'entities', 'orm', 'buyers', 'users', 'addresses', 'shippingAddresses', 'destinations',
      'fulfillment', 'to_address', 'toAddress', 'shipping_address', 'shippingAddress',
      'transactions', 'items', 'line_items', 'lineItems', 'results', 'shop', 'payload',
      'records', 'byId', 'models', 'attributes', 'collection', 'order', 'receipt'
    ]);

    function inspectJsValue(val, contextOrderId, depth, entitiesLookup) {
      if (!val || typeof val !== 'object' || depth > 12) return;
      if (val instanceof Node || val === win || val === doc || skippedArrays.has(val)) return;
      if (visitedObjects.has(val)) {
        // Aynı nesne farklı bir sipariş bağlamında değilse tekrar tarama; ancak doğrudan adres nesnesiyse ve contextOrderId varsa uygula
        if (contextOrderId && looksLikeAddressObject(val)) {
          const rec = ensureOrderRecord(contextOrderId);
          if (rec) applyAddressObject(rec, val);
        }
        return;
      }
      visitedObjects.add(val);

      // Redux Store tespiti (store.getState())
      if (typeof val.getState === 'function' && typeof val.dispatch === 'function' && !discoveredStores.has(val)) {
        discoveredStores.add(val);
        diagnostics.reduxStoresFound++;
        try {
          const reduxState = val.getState();
          inspectReduxState(reduxState);
        } catch {}
        return;
      }

      if (Array.isArray(val)) {
        const limit = Math.min(val.length, 200);
        for (let i = 0; i < limit; i++) {
          inspectJsValue(val[i], contextOrderId, depth + 1, entitiesLookup);
        }
        return;
      }

      if (val instanceof Map) {
        let count = 0;
        for (const [mk, mv] of val.entries()) {
          if (++count > 200) break;
          const nextCtx = /^\d{9,12}$/.test(String(mk)) ? String(mk) : contextOrderId;
          inspectJsValue(mv, nextCtx, depth + 1, entitiesLookup);
        }
        return;
      }

      const detectedId = detectOrderIdOnObject(val);
      const activeOrderId = detectedId || contextOrderId;

      if (activeOrderId) {
        const rec = ensureOrderRecord(activeOrderId);
        if (rec) {
          if (looksLikeAddressObject(val)) {
            applyAddressObject(rec, val);
          } else {
            applyOrderLikeObject(rec, val, entitiesLookup);
          }
        }
      }

      const rawKeys = Object.keys(val);
      // Etsy.Context.data yüzlerce feature-flag anahtarı içerdiğinden öncelikli veri/sipariş anahtarlarını her zaman en başta tara!
      const prioritizedKeys = [];
      const otherKeys = [];
      for (let i = 0; i < rawKeys.length; i++) {
        const k = rawKeys[i];
        if (SKIPPED_OBJECT_KEYS.has(k)) continue;
        if (PRIORITY_OBJECT_KEYS.has(k) || /^\d{9,12}$/.test(k)) {
          prioritizedKeys.push(k);
        } else {
          otherKeys.push(k);
        }
      }
      const orderedKeys = prioritizedKeys.length > 0 ? prioritizedKeys.concat(otherKeys) : otherKeys;
      const keyLimit = Math.min(orderedKeys.length, depth <= 3 ? 600 : 200);
      for (let i = 0; i < keyLimit; i++) {
        const k = orderedKeys[i];
        let childVal;
        try {
          childVal = val[k];
        } catch {
          continue;
        }
        if (!childVal || typeof childVal !== 'object') continue;
        const keyAsOrderId = /^\d{9,12}$/.test(k) ? k : activeOrderId;
        inspectJsValue(childVal, keyAsOrderId, depth + 1, entitiesLookup);
      }
    }

    function inspectReduxState(state) {
      if (!state || typeof state !== 'object') return;
      const entities = state.entities || (state.orm && state.orm.entities) || {};
      const lookup = {
        buyers: entities.buyers || entities.users || state.buyersById || null,
        addresses: entities.addresses || entities.shippingAddresses || entities.destinations || state.addressesById || null,
        transactions: entities.transactions || entities.lineItems || state.transactionsById || null
      };
      inspectJsValue(state, null, 0, lookup);
    }

    function inspectFiberHooks(fiber, contextOrderId) {
      let hook = fiber && fiber.memoizedState;
      let steps = 0;
      while (hook && typeof hook === 'object' && steps < 25) {
        steps++;
        if ('memoizedState' in hook && hook.memoizedState && typeof hook.memoizedState === 'object') {
          inspectJsValue(hook.memoizedState, contextOrderId, 1, null);
        }
        hook = hook.next;
      }
    }

    // 4. AŞAMA A0: window.Etsy.Context.data ve sayfa global state depolarını doğrudan tara
    try {
      if (win.Etsy && win.Etsy.Context && win.Etsy.Context.data) {
        inspectReduxState(win.Etsy.Context.data);
      }
      for (const gk of ['__INITIAL_STATE__', '__PRELOADED_STATE__', '__NEXT_DATA__']) {
        if (win[gk] && typeof win[gk] === 'object') {
          inspectReduxState(win[gk]);
        }
      }
    } catch {}

    // 4. AŞAMA A: Ekrandaki her izole sipariş kartının (ve açık çekmecenin) alt DOM düğümlerinden Fiber / Props okuma
    const visitedFibers = new WeakSet();
    const allRowEntries = [
      ...Array.from(visibleOrderRows.entries()),
      ...drawerRowScopes.map((d) => [d.id, d.rowEl])
    ];
    for (const [orderId, rowEl] of allRowEntries) {
      const descendants = [rowEl, ...Array.from(rowEl.querySelectorAll('a, button, address, [id*="toggle"], div, span')).slice(0, 180)];
      for (const domNode of descendants) {
        const directProps = getPropsFromDom(domNode);
        if (directProps) {
          diagnostics.fiberDetected = true;
          inspectJsValue(directProps, orderId, 0, null);
        }

        // Salt-okunur Preact / Backbone / Marionette DOM model bağlarını da kontrol et
        for (const altKey of ['__k', '__preactattr_', '_component', '_model', 'model', '__backboneView', 'view']) {
          if (domNode && altKey in domNode && domNode[altKey] && typeof domNode[altKey] === 'object') {
            inspectJsValue(domNode[altKey], orderId, 0, null);
          }
        }

        let fiber = getFiberFromDom(domNode);
        let climb = 0;
        while (fiber && climb < 22) {
          climb++;
          diagnostics.fiberDetected = true;
          // Ata fiber'ın DOM düğümü birden fazla sipariş içeriyorsa sipariş bağlamından çık
          if (fiber.stateNode instanceof Element && countIdsInElement(fiber.stateNode).size > 1) {
            if (fiber.memoizedProps && fiber.memoizedProps.store) {
              inspectJsValue(fiber.memoizedProps.store, null, 0, null);
            }
            break;
          }
          if (!visitedFibers.has(fiber)) {
            visitedFibers.add(fiber);
            diagnostics.fiberNodesVisited++;
            if (fiber.memoizedProps) inspectJsValue(fiber.memoizedProps, orderId, 0, null);
            if (fiber.stateNode && typeof fiber.stateNode === 'object' && !(fiber.stateNode instanceof Node)) {
              inspectJsValue(fiber.stateNode, orderId, 0, null);
            }
            inspectFiberHooks(fiber, orderId);
          }
          fiber = fiber.return;
        }
      }
    }

    // 5. AŞAMA B: Sayfadaki tüm derin React 16/17/18 kök konteynerlerinden üstten-aşağıya Fiber & Redux Store taraması
    const rootCandidates = [
      doc.body,
      ...Array.from(doc.querySelectorAll('main, aside, [role="dialog"], .wt-overlay, #root, #content, [id*="mission"], [id*="app"], [data-appelement], div')).slice(0, 600)
    ].filter(Boolean);
    const rootFibers = new Set();

    for (const rc of rootCandidates) {
      const rf = getRootContainerFiber(rc);
      if (rf) rootFibers.add(rf.current || rf);
      let f = getFiberFromDom(rc);
      if (f) {
        let guard = 0;
        while (f.return && guard++ < 80) f = f.return;
        rootFibers.add(f);
      }
    }

    const MAX_FIBER_WALK = 25000;
    for (const rootFiber of rootFibers) {
      const stack = [{ fiber: rootFiber, ctxOrderId: null }];
      while (stack.length > 0 && diagnostics.fiberNodesVisited < MAX_FIBER_WALK) {
        const { fiber, ctxOrderId } = stack.pop();
        if (!fiber || typeof fiber !== 'object') continue;
        diagnostics.fiberDetected = true;

        let localOrderId = ctxOrderId;
        if (fiber.memoizedProps && typeof fiber.memoizedProps === 'object') {
          const propOrderId = detectOrderIdOnObject(fiber.memoizedProps);
          if (propOrderId) localOrderId = propOrderId;
        }

        if (!visitedFibers.has(fiber)) {
          visitedFibers.add(fiber);
          diagnostics.fiberNodesVisited++;
          if (fiber.memoizedProps) inspectJsValue(fiber.memoizedProps, localOrderId, 0, null);
          inspectFiberHooks(fiber, localOrderId);
        }

        if (fiber.sibling) stack.push({ fiber: fiber.sibling, ctxOrderId });
        if (fiber.child) stack.push({ fiber: fiber.child, ctxOrderId: localOrderId });
      }
    }

    // 6. Sonuçları temizle ve tanılamayı özetle
    const finalOrders = [];
    for (const rec of ordersAcc.values()) {
      const buyerName = rec.recipientName || rec.buyerAccountName || '';
      const isFullAddress = Boolean(rec.streetAddress && (rec.city || rec.state || rec.postalCode || rec.country));
      const hasUsefulData = Boolean(
        isFullAddress || buyerName || rec.city || rec.orderTotal != null || rec.sku || rec.productTitle
      );
      if (!hasUsefulData) continue;

      if (isFullAddress) diagnostics.fullAddressesMatched++;

      finalOrders.push({
        receiptId: rec.receiptId,
        buyerName: buyerName || 'Etsy Müşterisi',
        recipientName: rec.recipientName,
        buyerAccountName: rec.buyerAccountName,
        streetAddress: rec.streetAddress,
        secondAddress: rec.secondAddress,
        city: rec.city,
        state: rec.state,
        postalCode: rec.postalCode,
        country: rec.country,
        phone: rec.phone,
        iossNumber: rec.iossNumber,
        productTitle: rec.productTitle,
        orderTotal: rec.orderTotal,
        currency: rec.currency || 'USD',
        orderDate: rec.orderDate,
        status: rec.status || (rec.trackingNumber ? 'Shipped' : 'Processing'),
        trackingNumber: rec.trackingNumber,
        sku: rec.sku,
        variation: rec.variation,
        transactionId: rec.transactionId,
        shippingMethod: rec.shippingMethod,
        isFullAddress,
        dataLayer: 'react-fiber',
        sourceType: isFullAddress ? 'React Bellek (Tam Adres)' : 'React Bellek (Özet)'
      });
    }

    diagnostics.ordersMatched = finalOrders.length;

    return {
      shopId: detectedShopId,
      orders: finalOrders,
      finance: null,
      diagnostics
    };
  }

  // Node.js birim testi ortamı için dışa aktarım, Chrome MAIN world için doğrudan dönüş değeri
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = { runMainWorldScan };
  }
  if (typeof window !== 'undefined' && typeof document !== 'undefined') {
    return runMainWorldScan(window, document);
  }
})();
