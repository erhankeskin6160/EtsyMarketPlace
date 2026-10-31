// Etsy Copilot v2.0.0 — Katman 2: Dil ve Google-Çeviri Bağımsız Yapısal DOM & Inline Accordion Ajanı
// - "Ship to" / İngilizce metin aramasına BAĞIMLI DEĞİLDİR.
// - Google Çeviri (<font dir="auto">) metin parçalamasını birleştirerek okur.
// - Kart sınırlarını sipariş numarası + tekil checkbox + aria-controls / geometri ile matematiksel olarak izole eder.

(function (root) {
  'use strict';

  const AGENT_VERSION = '2.0.0';

  if (typeof root.__etsyCopilotV2Cleanup === 'function') {
    try { root.__etsyCopilotV2Cleanup(); } catch {}
  }

  function getParser() {
    return root.EtsyCopilotAddressParser || (typeof require === 'function' ? require('../shared/address-parser.js') : null);
  }

  function isExtensionContextValid() {
    try {
      return Boolean(typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.id);
    } catch {
      return false;
    }
  }

  function detectPageType(urlStr, doc) {
    const u = String(urlStr || (typeof location !== 'undefined' ? location.href : '') || '');
    if (u.includes('/your/account/payments') || u.includes('/finances/payment_account')) {
      return 'finance';
    }
    if (u.includes('/your/orders') || u.includes('/orders/sold')) {
      return 'orders';
    }
    if (doc && doc.querySelector && doc.querySelector('[data-region="order-card"], a[href*="/your/orders/sold"]')) {
      if (u.includes('etsy.com/your/')) return 'orders';
    }
    return 'other';
  }

  const BLOCK_TAGS = new Set([
    'DIV', 'P', 'LI', 'TR', 'TD', 'TH', 'SECTION', 'ARTICLE', 'ASIDE', 'HEADER', 'FOOTER',
    'ADDRESS', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'DT', 'DD', 'UL', 'OL', 'BLOCKQUOTE', 'BUTTON'
  ]);

  const IGNORED_TAGS = new Set(['SVG', 'SCRIPT', 'STYLE', 'NOSCRIPT', 'TEMPLATE']);

  /**
   * Google Çeviri'nin <font dir="auto"> sarmallarını düzleştirerek blok bazlı temiz satırlar üretir.
   * includeHiddenPanels=true olduğunda kapalı .wt-content-toggle__body içindeki tam adres metnini de okur!
   */
  function extractStructuredLines(el, includeHiddenPanels = false) {
    if (!el) return [];
    const chunks = [];

    function walk(node) {
      if (!node) return;
      if (node.nodeType === 3 /* TEXT_NODE */) {
        const val = node.nodeValue;
        if (val) chunks.push(val);
        return;
      }
      if (node.nodeType !== 1 /* ELEMENT_NODE */) return;

      const tag = (node.tagName || '').toUpperCase();
      if (IGNORED_TAGS.has(tag)) return;

      const cls = typeof node.className === 'string' ? node.className : '';
      if (/\b(?:wt-screen-reader-only|screen-reader-only|sr-only|goog-te-spinner)\b/i.test(cls)) {
        return;
      }
      if (!includeHiddenPanels && node.getAttribute && node.getAttribute('aria-hidden') === 'true') {
        return;
      }

      if (tag === 'BR') {
        chunks.push('\n');
        return;
      }

      const isBlock = BLOCK_TAGS.has(tag);
      if (isBlock) chunks.push('\n');
      const children = node.childNodes || [];
      for (let i = 0; i < children.length; i++) {
        walk(children[i]);
      }
      if (isBlock) chunks.push('\n');
    }

    walk(el);

    return chunks
      .join('')
      .split(/\r?\n/)
      .map((line) => line.replace(/\u00A0/g, ' ').replace(/\s{2,}/g, ' ').trim())
      .filter(Boolean);
  }

  function extractInlineText(el) {
    return extractStructuredLines(el, false).join(' ').replace(/[▾▼⌄⌃]/g, '').replace(/\s{2,}/g, ' ').trim();
  }

  function isInsideDrawer(el) {
    if (!el || !el.closest) return false;
    return Boolean(el.closest('aside, [role="dialog"], [data-region="order-drawer"], [class*="order-drawer" i], [class*="panel--flyout" i]'));
  }

  function collectUniqueOrderIds(el) {
    const ids = new Set();
    if (!el) return ids;
    const raw = (el.textContent || '').replace(/\u00A0/g, ' ');
    for (const m of raw.matchAll(/#\s*(\d{9,12})\b/g)) ids.add(m[1]);
    for (const m of raw.matchAll(/\b(\d{9,12})\s*\(\s*Etsy\s*\)/gi)) ids.add(m[1]);
    return ids;
  }

  /**
   * Matematiksel Sipariş Kartı İzolasyonu:
   * 1. #4204665088 (veya Google Çeviri boşluklu "# 4204665088") metin düğümünden başlar.
   * 2. Yukarı tırmanırken:
   *    - 2. bir sipariş ID'si görürse durur.
   *    - Mevcut kapsayıcı zaten ürün linkini (a[href*="/listing/"]) VEYA adres panelini içeriyorsa
   *      ve bir üst kapsayıcı "Select all" gibi 2. bir checkbox ekliyorsa tam sipariş satırında durur!
   */
  function isolateOrderCards(doc) {
    const cards = [];
    const seen = new Set();
    if (!doc || !doc.body) return cards;

    const anchors = [];
    const walker = doc.createTreeWalker(doc.body, 4 /* SHOW_TEXT */);
    let tn;
    while ((tn = walker.nextNode())) {
      const txt = (tn.nodeValue || '').replace(/\u00A0/g, ' ');
      const m = txt.match(/#\s*(\d{9,12})\b/) || txt.match(/\b(\d{9,12})\s*\(\s*Etsy\s*\)/i);
      if (!m) continue;
      const parent = tn.parentElement;
      if (!parent || isInsideDrawer(parent)) continue;
      anchors.push({ id: m[1], el: parent });
    }

    // Yedek: eğer Google Çeviri "#" ile rakamı 2 ayrı <font> etiketine böldüyse
    if (anchors.length === 0) {
      const candidates = doc.querySelectorAll('a, span, div, strong');
      for (const el of candidates) {
        if (isInsideDrawer(el) || el.children.length > 4) continue;
        const txt = (el.textContent || '').replace(/\u00A0/g, ' ');
        const m = txt.match(/#\s*(\d{9,12})\b/) || txt.match(/\b(\d{9,12})\s*\(\s*Etsy\s*\)/i);
        if (m) anchors.push({ id: m[1], el });
      }
    }

    for (const anchor of anchors) {
      if (seen.has(anchor.id)) continue;
      seen.add(anchor.id);

      let curr = anchor.el;
      let best = anchor.el;

      while (curr && curr !== doc.body && curr.tagName !== 'MAIN' && curr.id !== 'root' && curr.id !== 'content') {
        if (curr.querySelector && curr.querySelector('nav, [role="tablist"], [role="navigation"]')) {
          break;
        }
        const ids = collectUniqueOrderIds(curr);
        if (ids.size > 1) break;

        if (ids.size === 1 && ids.has(anchor.id)) {
          // Eğer best zaten hem ürün/fiyat hem sağ sütunu kapsıyorsa ve curr tarih grubu "Select all" checkbox'ını ekliyorsa dur
          const bestHasListing = Boolean(best.querySelector && (best.querySelector('a[href*="/listing/"], img') || best.querySelector('[aria-controls], [aria-expanded], address')));
          const currCheckboxes = curr.querySelectorAll ? curr.querySelectorAll('input[type="checkbox"]').length : 0;
          if (bestHasListing && currCheckboxes >= 2) {
            break;
          }
          best = curr;
        }
        curr = curr.parentElement;
      }

      cards.push({ id: anchor.id, idAnchor: anchor.el, cardEl: best });
    }

    return cards;
  }

  function isMenuOrActionBtn(btn) {
    if (!btn) return true;
    const popup = (btn.getAttribute('aria-haspopup') || '').toLowerCase();
    if (popup === 'menu' || (popup === 'true' && !btn.hasAttribute('aria-controls') && !btn.classList.contains('wt-content-toggle'))) {
      return true;
    }
    const label = `${btn.getAttribute('aria-label') || ''} ${btn.getAttribute('title') || ''} ${extractInlineText(btn)}`.trim();
    return /\b(?:more\s*actions|diğer\s*işlemler|daha\s*fazla|complete\s*order|siparişi\s*tamamla|message|mesaj|contact|order\s*history|geçmiş|private\s*note|not\s*ekle|copy|kopyala|print|yazdır|cancel|iptal|refund|iade|add\s*tracking|takip\s*ekle|update\s*progress)\b/i.test(label);
  }

  function isFollowingInDom(refNode, targetNode) {
    if (!refNode || !targetNode || !refNode.compareDocumentPosition) return true;
    return Boolean(refNode.compareDocumentPosition(targetNode) & 4 /* DOCUMENT_POSITION_FOLLOWING */);
  }

  /**
   * Dil-Bağımsız Sağ Kargo Sütunu ve Adres Paneli Çözücü:
   * 1) DOM sırası (idAnchor ve ürün görselinden SONRA gelen sağ sütun),
   * 2) aria-controls / aria-expanded / React chevron <button> yapısal bağı ile çalışır.
   */
  function locateFulfillmentZone(cardEl, idAnchor, doc) {
    const result = {
      toggleBtn: null,
      addressPanels: [],
      rightColumnEl: null
    };
    if (!cardEl || !cardEl.querySelectorAll) return result;

    const idCol = idAnchor ? idAnchor.parentElement : null;
    const productImg = cardEl.querySelector('img');

    // 1. Önce Sağ Kargo Sütununu (rightColumnEl) DOM sırası ve yapısal dışlama ile kesin olarak izole et
    const allContainers = Array.from(cardEl.querySelectorAll('div, section, td'));
    const candidateCols = allContainers.filter((d) => {
      if (collectUniqueOrderIds(d).size > 0) return false;
      if (d.querySelector && d.querySelector('img, input[type="checkbox"], a[href*="/listing/"]')) return false;
      if (!isFollowingInDom(idAnchor, d)) return false;
      if (productImg && !isFollowingInDom(productImg, d)) return false;
      const lines = extractStructuredLines(d, false);
      if (lines.length < 2 || lines.length > 16) return false;
      // Ürün varyasyon / SKU bloğunu dışla
      if (lines.some((l) => /^(?:Quantity|Adet|Miktar)\s*\d+$/i.test(l) || /^(?:SKU|Stok\s*Kodu)\b/i.test(l))) {
        return false;
      }
      return true;
    });

    const topRightCols = candidateCols.filter((c) => !candidateCols.some((other) => other !== c && other.contains(c)));
    if (topRightCols.length > 0) {
      result.rightColumnEl = topRightCols.sort((a, b) => {
        const linesA = extractStructuredLines(a, false).length;
        const linesB = extractStructuredLines(b, false).length;
        return linesB - linesA;
      })[0];
    }

    // 2. Toggle butonunu bul: Önce aria-controls / aria-expanded, yoksa sağ kargo sütunu içindeki React chevron <button>
    const searchScope = result.rightColumnEl || cardEl;
    const ariaToggles = Array.from(searchScope.querySelectorAll('button[aria-controls], button[aria-expanded], .wt-content-toggle, [role="button"][aria-expanded]'))
      .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn))
      .filter((btn) => !(idCol && idCol.contains(btn)));

    result.toggleBtn = ariaToggles.find((b) => b.hasAttribute('aria-controls')) || ariaToggles[0] || null;

    if (!result.toggleBtn && result.rightColumnEl) {
      const plainButtons = Array.from(result.rightColumnEl.querySelectorAll('button, [role="button"]'))
        .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn));
      if (plainButtons.length > 0) {
        result.toggleBtn = plainButtons[0];
      }
    }

    const addPanel = (el) => {
      if (el && collectUniqueOrderIds(el).size === 0 && !result.addressPanels.includes(el)) {
        result.addressPanels.push(el);
      }
    };

    if (result.toggleBtn) {
      const ctrlId = result.toggleBtn.getAttribute('aria-controls');
      if (ctrlId && doc) {
        const byId = doc.getElementById(ctrlId);
        if (byId && cardEl.contains(byId)) addPanel(byId);
      }
    }

    for (const p of (result.rightColumnEl || cardEl).querySelectorAll('address, .wt-content-toggle__body, [id*="wt-content-toggle"], [class*="ship-to" i], [data-testid*="address" i]')) {
      addPanel(p);
    }

    // Eğer rightColumnEl henüz bulunamadıysa toggleBtn veya addressPanel'den yukarı çık
    if (!result.rightColumnEl) {
      const seed = result.toggleBtn || result.addressPanels[0] || null;
      if (seed) {
        let p = seed.parentElement;
        let col = seed;
        while (p && p !== cardEl && cardEl.contains(p)) {
          if (collectUniqueOrderIds(p).size > 0 || (p.querySelector && p.querySelector('img, input[type="checkbox"], a[href*="/listing/"]'))) {
            break;
          }
          col = p;
          p = p.parentElement;
        }
        result.rightColumnEl = col;
      }
    }

    return result;
  }

  const TR_MONTH_MAP = {
    ocak: 'Jan', oca: 'Jan',
    şubat: 'Feb', subat: 'Feb', şub: 'Feb', sub: 'Feb',
    mart: 'Mar', mar: 'Mar',
    nisan: 'Apr', nis: 'Apr',
    mayıs: 'May', mayis: 'May', may: 'May',
    haziran: 'Jun', haz: 'Jun',
    temmuz: 'Jul', tem: 'Jul',
    ağustos: 'Aug', agustos: 'Aug', ağu: 'Aug', agu: 'Aug',
    eylül: 'Sep', eylul: 'Sep', eyl: 'Sep',
    ekim: 'Oct', eki: 'Oct',
    kasım: 'Nov', kasim: 'Nov', kas: 'Nov',
    aralık: 'Dec', aralik: 'Dec', ara: 'Dec'
  };

  function extractOrderDateFromLines(lines) {
    const joined = lines.join('\n');
    // İngilizce: Ordered Oct 16, 2026
    const enMatch = joined.match(/(?:Ordered|Order\s+date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
      || joined.match(/\b((?:Jan|Feb|Mar|Apr|May|Jun|Jul|Aug|Sep|Oct|Nov|Dec)[a-z]*\s+\d{1,2},\s*\d{4})\b/i);
    if (enMatch) return enMatch[1].trim();

    // Google Çeviri Türkçe: "16 Eki 2026 tarihinde sipariş edildi" veya "16 Ekim 2026"
    const trMatch = joined.match(/\b(\d{1,2})\s+(Ocak|Oca|Şubat|Subat|Şub|Sub|Mart|Mar|Nisan|Nis|Mayıs|Mayis|May|Haziran|Haz|Temmuz|Tem|Ağustos|Agustos|Ağu|Agu|Eylül|Eylul|Eyl|Ekim|Eki|Kasım|Kasim|Kas|Aralık|Aralik|Ara)\s+(\d{4})\b/i);
    if (trMatch) {
      const mEng = TR_MONTH_MAP[trMatch[2].toLowerCase()] || trMatch[2];
      return `${mEng} ${trMatch[1]}, ${trMatch[3]}`;
    }
    return '';
  }

  function extractMoneyFromCardLines(lines, orderId) {
    const parser = getParser();
    const joined = lines.join('\n');
    const idEscaped = orderId.replace(/\D/g, '');
    const idLine = lines.find((l) => l.includes(idEscaped)) || joined;

    for (const source of [idLine, joined]) {
      const m = source.match(/(ABD\s*\$|US\s*\$|\$|€|£|₺|TRY|TL|EUR|GBP|USD)\s*([\d.,]+)/i)
        || source.match(/([\d.,]+)\s*(₺|TL|TRY|EUR|€|USD|\$)/i);
      if (m) {
        const rawSym = (m[1].match(/\d/) ? m[2] : m[1]).toUpperCase();
        const rawNum = m[1].match(/\d/) ? m[1] : m[2];
        const amount = parser ? parser.parseFlexibleMoneyNumber(rawNum) : Number.parseFloat(rawNum);
        let currency = 'USD';
        if (rawSym.includes('₺') || rawSym.includes('TL') || rawSym.includes('TRY')) currency = 'TRY';
        else if (rawSym.includes('€') || rawSym.includes('EUR')) currency = 'EUR';
        else if (rawSym.includes('£') || rawSym.includes('GBP')) currency = 'GBP';
        if (amount != null) return { amount, currency };
      }
    }
    return { amount: null, currency: 'USD' };
  }

  function extractBuyerAccountFromLeftColumn(cardEl, idAnchor, orderId, cardLines) {
    const parser = getParser();
    const isInvalid = (s) => (parser ? parser.isInvalidPersonName(s) : !s);
    const isStreet = (s) => (parser ? parser.looksLikeStreetOrCityLine(s) : false);

    // 1. Sol sütunda doğrudan alıcı menü butonu veya profil linki
    if (idAnchor) {
      let scope = idAnchor.parentElement;
      for (let depth = 0; depth < 3 && scope && cardEl.contains(scope); depth++) {
        const btns = Array.from(scope.querySelectorAll('button, a[href*="/people/"], a[href*="buyer"], [aria-haspopup="true"], [aria-haspopup="menu"]'));
        for (const b of btns) {
          if (collectUniqueOrderIds(b).size > 0) continue;
          const t = extractInlineText(b);
          if (!isInvalid(t) && !isStreet(t)) return t;
        }
        scope = scope.parentElement;
      }
    }

    // 2. Satır dizisinde #siparişNo satırının hemen üstündeki geçerli kişi adı satırı (Örn: "Damien Panico", "Marie Cindy")
    const idLineIdx = cardLines.findIndex((l) => new RegExp(`#\\s*${orderId}\\b|\\b${orderId}\\s*\\(`, 'i').test(l));
    for (let k = idLineIdx - 1; k >= 0 && k >= idLineIdx - 3; k--) {
      const cand = cardLines[k].replace(/[▾▼⌄⌃]/g, '').trim();
      if (!isInvalid(cand) && !isStreet(cand)) {
        return cand;
      }
    }

    return '';
  }

  function sliceAddressLinesFromColumn(colLines) {
    if (!Array.isArray(colLines) || colLines.length === 0) return [];

    // Bilinen İngilizce/Türkçe kargo başlığı varsa hemen sonrasından kes
    const headingRe = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in)?|Gönderilecek\s*adres|Kargo\s*adresi|Alıcı)\s*:?$/i;
    for (let i = colLines.length - 1; i >= 0; i--) {
      if (headingRe.test(colLines[i].replace(/[▾▼⌄⌃]/g, '').trim())) {
        return colLines.slice(i + 1);
      }
    }

    // Dil-bağımsız yapısal kesim: Sağ sütunun üstündeki "No tracking" / "Ship by..." / "Ordered..." / "Standard Shipping"
    // meta satırlarını atla; geriye kalan bölüm doğrudan [Alıcı, Sokak, Şehir/Eyalet, Ülke, Tel] bloğudur!
    let start = 0;
    while (start < colLines.length) {
      const l = colLines[start];
      if (
        /\b(?:No\s+tracking|Takip\s+(?:numarası\s+)?yok|Ship\s+by|kargoya\s+verin|Ordered\s+|sipariş\s+edildi|Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Shipped|Pre-transit|In\s+transit|Delivered|\d+\s+days?\s+ago|gün\s+önce|Overdue|Gecikmiş)\b/i.test(l)
        || /\(\s*(?:ABD\s*\$|US\s*\$|\$|€|₺)[\d.,]+\s*\)/i.test(l)
      ) {
        start++;
        continue;
      }
      break;
    }
    return colLines.slice(start);
  }

  function extractOrdersFromDom(doc) {
    const parser = getParser();
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !targetDoc.body || !parser) {
      return { orders: [], drawerOrder: null, diagnostics: { error: 'NO_DOM_OR_PARSER' } };
    }

    const htmlEl = targetDoc.documentElement;
    const isGoogleTranslated = Boolean(
      (htmlEl && typeof htmlEl.className === 'string' && /translated-(?:ltr|rtl)/i.test(htmlEl.className))
      || targetDoc.querySelector('font[style*="vertical-align"]')
    );

    const isolated = isolateOrderCards(targetDoc);
    const orders = [];
    let toggleButtonsFound = 0;
    let panelsFound = 0;

    for (const { id, idAnchor, cardEl } of isolated) {
      const cardLines = extractStructuredLines(cardEl, false);
      const cardText = cardLines.join('\n');
      const zone = locateFulfillmentZone(cardEl, idAnchor, targetDoc);

      if (zone.toggleBtn) toggleButtonsFound++;
      panelsFound += zone.addressPanels.length;

      const buyerAccountName = extractBuyerAccountFromLeftColumn(cardEl, idAnchor, id, cardLines);

      // 1. Önce yapısal adres panellerini (#wt-content-toggle-..., <address> vb.) hem görünür hem gizli modda dene
      let bestAddr = null;
      let addrSource = 'Yapısal Sağ Sütun';

      for (const panel of zone.addressPanels) {
        const panelLines = extractStructuredLines(panel, true);
        const candidate = parser.parseAddressLines(panelLines, cardText);
        if (candidate.isFullAddress) {
          bestAddr = candidate;
          addrSource = 'Yapısal Adres Paneli (Tam Adres)';
          break;
        }
        if (!bestAddr && (candidate.city || candidate.state || candidate.recipientName)) {
          bestAddr = candidate;
        }
      }

      // 2. Sağ kargo sütunundan dil-bağımsız satır ayıklama
      if (!bestAddr || !bestAddr.isFullAddress) {
        if (zone.rightColumnEl) {
          const rightLines = sliceAddressLinesFromColumn(extractStructuredLines(zone.rightColumnEl, true));
          const rightParsed = parser.parseAddressLines(rightLines, cardText);
          if (rightParsed.isFullAddress || !bestAddr || (!bestAddr.city && (rightParsed.city || rightParsed.recipientName))) {
            bestAddr = rightParsed;
            if (rightParsed.isFullAddress) addrSource = 'Yapısal Sağ Sütun (Tam Adres)';
          }
        }
      }

      // 3. Yedek: Kart satırları içinden başlık sonrası ayıklama
      if (!bestAddr || (!bestAddr.isFullAddress && !bestAddr.city && !bestAddr.recipientName)) {
        const fallbackLines = sliceAddressLinesFromColumn(cardLines);
        const fallbackParsed = parser.parseAddressLines(fallbackLines, cardText);
        if (fallbackParsed.isFullAddress || (!bestAddr?.city && fallbackParsed.city)) {
          bestAddr = fallbackParsed;
        }
      }

      const addr = bestAddr || parser.parseAddressLines([], cardText);
      const buyerName = addr.recipientName || buyerAccountName || 'Etsy Müşterisi';
      const { amount: orderTotal, currency } = extractMoneyFromCardLines(cardLines, id);

      // Ürün adı (doğrudan a[href*="/listing/"] yapısal çapasından)
      let productTitle = '';
      const listingLink = cardEl.querySelector ? cardEl.querySelector('a[href*="/listing/"]') : null;
      if (listingLink) {
        productTitle = extractInlineText(listingLink);
      }
      if (!productTitle) {
        const qtyIdx = cardLines.findIndex((l) => /^(?:Quantity|Adet|Miktar)\s*\d+/i.test(l));
        if (qtyIdx > 0 && !/#\s*\d{9,12}/.test(cardLines[qtyIdx - 1]) && !/coupon|kupon/i.test(cardLines[qtyIdx - 1])) {
          productTitle = cardLines[qtyIdx - 1];
        }
      }

      // SKU, Varyasyon, Kargo Yöntemi, Takip Kodu, Tarih
      const skuLine = cardLines.find((l) => /^(?:SKU|Stok\s*Kodu)\b/i.test(l));
      const sku = skuLine ? skuLine.replace(/^(?:SKU|Stok\s*Kodu)\s*:?\s*/i, '').trim() : '';

      const varLines = cardLines.filter((l) => /^(?:Personalization|Height|Size|Color|Colour|Style|Material|Option|Kişiselleştirme|Boyut|Renk|Yükseklik|Ölçü)\s*:/i.test(l));
      const variation = varLines.join(' | ');

      const shipMethodLine = cardLines.find((l) => /\b(?:Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Expedited\s+Shipping)\b/i.test(l));
      const shippingMethod = shipMethodLine ? shipMethodLine.replace(/\s*\([^)]*\)\s*$/, '').trim() : '';

      const trkMatch = cardText.match(/\b(\d{12,30})\b[\s\r\n]+(?:Shipped|In\s+transit)/i)
        || cardText.match(/(?:Tracking|Takip\s*No)\s*:\s*([A-Za-z0-9]{8,30})/i);
      const trackingNumber = trkMatch ? trkMatch[1].trim() : '';

      const orderDate = extractOrderDateFromLines(cardLines);
      const status = trackingNumber ? 'Shipped' : 'Processing';

      orders.push({
        receiptId: id,
        buyerName,
        recipientName: addr.recipientName || '',
        buyerAccountName: buyerAccountName || '',
        streetAddress: addr.streetAddress,
        secondAddress: addr.secondAddress,
        city: addr.city,
        state: addr.state,
        postalCode: addr.postalCode,
        country: addr.country || (addr.state ? 'United States' : ''),
        phone: addr.phone,
        iossNumber: addr.iossNumber,
        rawAddressText: addr.rawAddressText,
        isFullAddress: addr.isFullAddress,
        productTitle,
        orderTotal,
        currency,
        orderDate,
        status,
        trackingNumber,
        sku,
        variation,
        shippingMethod,
        dataLayer: 'structural-dom',
        sourceType: addrSource
      });
    }

    const drawerOrder = extractOpenDrawerOrder(targetDoc);

    return {
      orders,
      drawerOrder,
      diagnostics: {
        layer: 'content-agent.dom',
        agentVersion: AGENT_VERSION,
        isGoogleTranslated,
        isolatedCardCount: isolated.length,
        toggleButtonsFound,
        panelsFound,
        fullAddressDomCount: orders.filter((o) => o.isFullAddress).length,
        drawerOpen: Boolean(drawerOrder)
      }
    };
  }

  /**
   * Açık yan sipariş çekmecesini (<aside> / [role="dialog"]) yapısal olarak okur.
   */
  function extractOpenDrawerOrder(doc) {
    const parser = getParser();
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !parser) return null;

    const drawers = Array.from(targetDoc.querySelectorAll('aside, [role="dialog"], [data-region="order-drawer"], [class*="order-drawer" i], [class*="panel--flyout" i]'));
    for (const dr of drawers) {
      if (dr.getAttribute && dr.getAttribute('aria-hidden') === 'true') continue;
      const ids = collectUniqueOrderIds(dr);
      if (ids.size !== 1) continue;
      const receiptId = Array.from(ids)[0];

      const drLines = extractStructuredLines(dr, false);
      const drText = drLines.join('\n');

      // Önce <address> etiketi varsa onu dene, yoksa başlık sonrası kes
      let parsed = null;
      const addrTag = dr.querySelector ? dr.querySelector('address') : null;
      if (addrTag) {
        parsed = parser.parseAddressLines(extractStructuredLines(addrTag, false), drText);
      }
      if (!parsed || !parsed.isFullAddress) {
        const sliced = sliceAddressLinesFromColumn(drLines);
        parsed = parser.parseAddressLines(sliced, drText);
      }
      if (!parsed || !parsed.isFullAddress) continue;

      const { amount: orderTotal, currency } = extractMoneyFromCardLines(drLines, receiptId);
      const txMatch = drText.match(/(?:Transaction\s*ID|İşlem\s*Kimliği)\s*:\s*(\d{7,14})/i);
      const skuLine = drLines.find((l) => /^(?:SKU|Stok\s*Kodu)\b/i.test(l));

      return {
        receiptId,
        buyerName: parsed.recipientName || 'Etsy Müşterisi',
        recipientName: parsed.recipientName,
        streetAddress: parsed.streetAddress,
        secondAddress: parsed.secondAddress,
        city: parsed.city,
        state: parsed.state,
        postalCode: parsed.postalCode,
        country: parsed.country || 'United States',
        phone: parsed.phone,
        iossNumber: parsed.iossNumber,
        rawAddressText: parsed.rawAddressText,
        isFullAddress: true,
        orderTotal,
        currency,
        transactionId: txMatch ? txMatch[1] : '',
        sku: skuLine ? skuLine.replace(/^(?:SKU|Stok\s*Kodu)\s*:?\s*/i, '').trim() : '',
        dataLayer: 'structural-dom',
        sourceType: 'Açık Sipariş Çekmecesi (Drawer)'
      };
    }
    return null;
  }

  /**
   * Finans / Ödeme Rezervi Okuyucu (Sıfır Mock İlkesi: yalnızca gerçek okunan değerleri döner!)
   */
  function extractFinanceSnapshot(doc) {
    const parser = getParser();
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !targetDoc.body || !parser) return null;

    const lines = extractStructuredLines(targetDoc.body, false);
    const text = lines.join('\n');

    const curMatch = text.match(/(?:Current(?:\s+balance)?|Mevcut\s+bakiye|Kullanılabilir\s+bakiye)[\s\r\n:]*([+-]?[\d.,]+)\s*(?:TL|₺|TRY)/i)
      || text.match(/([+-]?[\d.,]+)\s*(?:TL|₺|TRY)[\s\r\n]*(?:Current\s+balance|Mevcut\s+bakiye)/i);

    const resMatch = text.match(/(?:In\s+reserve|Pending(?:\s+balance)?|Rezervde(?:ki\s+tutar)?|Bekleyen\s+bakiye)[\s\r\n:]*([+-]?[\d.,]+)\s*(?:TL|₺|TRY)/i)
      || text.match(/([+-]?[\d.,]+)\s*(?:TL|₺|TRY)\s*(?:is\s+in\s+reserve|in\s+reserve|rezervde)/i);

    const totMatch = text.match(/(?:Total(?:\s+balance)?|Toplam)[\s\r\n:]*([+-]?[\d.,]+)\s*(?:TL|₺|TRY)/i);

    if (!curMatch && !resMatch && !totMatch) {
      return null; // Sıfır Mock: Ekranda bakiye yoksa uydurma sayı dönme!
    }

    const cur = curMatch ? parser.parseFlexibleMoneyNumber(curMatch[1]) : 0;
    let pending = resMatch ? parser.parseFlexibleMoneyNumber(resMatch[1]) : null;
    let total = totMatch ? parser.parseFlexibleMoneyNumber(totMatch[1]) : null;

    if (pending == null && total != null && cur != null) pending = Number((total - cur).toFixed(2));
    if (total == null && pending != null && cur != null) total = Number((cur + pending).toFixed(2));

    const pctMatch = text.match(/(\d+)\s*%\s*(?:is\s+in\s+reserve|in\s+reserve|rezervde)/i)
      || text.match(/%\s*(\d+)\s*rezervde/i);
    const daysMatch = text.match(/(?:for\s*(\d+)\s*days|(\d+)\s*gün\s*boyunca)/i);
    const dateMatch = text.match(/(?:reevaluate[^\n.]*on\s+|reevaluated\s+on\s+)([A-Za-z0-9\s,]+)/i);

    const reservePercent = pctMatch ? Number.parseInt(pctMatch[1] || pctMatch[2], 10) : 0;
    const reserveHoldDays = daysMatch ? Number.parseInt(daysMatch[1] || daysMatch[2], 10) : 0;
    const reevaluationDate = dateMatch ? dateMatch[1].trim() : null;

    return {
      currentBalanceTry: cur ?? 0,
      pendingBalanceTry: pending ?? 0,
      totalBalanceTry: total ?? 0,
      reservePercent,
      reserveHoldDays,
      reevaluationDate,
      statusMessage: reservePercent > 0
        ? `${reservePercent}% in reserve${reserveHoldDays ? ` for ${reserveHoldDays} days` : ''}${reevaluationDate ? `. Next review: ${reevaluationDate}` : ''}`
        : 'No active reserve'
    };
  }

  /**
   * Ana iş parçacığını bloklamadan (requestAnimationFrame gruplarıyla) kapalı adres akordeonlarını açar.
   */
  async function expandAllInlineAccordions(doc) {
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !targetDoc.body) return extractOrdersFromDom(targetDoc);

    const beforeScan = extractOrdersFromDom(targetDoc);
    const fullIdsBefore = new Set(beforeScan.orders.filter((o) => o.isFullAddress).map((o) => o.receiptId));

    const isolated = isolateOrderCards(targetDoc);
    const toClick = [];

    for (const { id, idAnchor, cardEl } of isolated) {
      if (fullIdsBefore.has(id)) continue; // Zaten tam adresi açık olan kartı ASLA kapatma!
      const zone = locateFulfillmentZone(cardEl, idAnchor, targetDoc);
      if (!zone.toggleBtn) continue;

      const ariaExp = zone.toggleBtn.getAttribute('aria-expanded');
      if (ariaExp === 'false' || ariaExp === null) {
        toClick.push(zone.toggleBtn);
      }
    }

    const BATCH = 10;
    for (let i = 0; i < toClick.length; i += BATCH) {
      const batch = toClick.slice(i, i + BATCH);
      await new Promise((resolve) => {
        const runner = typeof requestAnimationFrame === 'function' ? requestAnimationFrame : (fn) => setTimeout(fn, 16);
        runner(() => {
          for (const btn of batch) {
            try { btn.click(); } catch {}
          }
          resolve();
        });
      });
      await new Promise((r) => setTimeout(r, 80));
    }

    // Açılan panellerin DOM'a yerleşmesini bekle (maks 1200ms)
    const start = Date.now();
    while (toClick.length > 0 && Date.now() - start < 1200) {
      const allReady = toClick.every((btn) => !btn.isConnected || btn.getAttribute('aria-expanded') === 'true');
      if (allReady) break;
      await new Promise((r) => setTimeout(r, 90));
    }
    if (toClick.length > 0) {
      await new Promise((r) => setTimeout(r, 150));
    }

    const extracted = extractOrdersFromDom(targetDoc);
    extracted.diagnostics.clickedInlineToggles = toClick.length;
    return extracted;
  }

  // Tarayıcı Content Script mesaj dinleyicisi ve otomatik Drawer gözlemcisi
  if (isExtensionContextValid() && typeof document !== 'undefined' && document.body) {
    let lastDrawerId = '';
    const observer = new MutationObserver(() => {
      if (!isExtensionContextValid()) {
        try { observer.disconnect(); } catch {}
        return;
      }
      if (detectPageType() !== 'orders') return;
      try {
        const drOrder = extractOpenDrawerOrder(document);
        if (drOrder && drOrder.isFullAddress && drOrder.receiptId !== lastDrawerId) {
          lastDrawerId = drOrder.receiptId;
          chrome.runtime.sendMessage({
            action: 'CA_DRAWER_CAPTURED',
            order: drOrder
          }).catch(() => {});
        }
      } catch {}
    });

    try {
      observer.observe(document.body, { childList: true, subtree: true });
    } catch {}

    const onMessage = (message, _sender, sendResponse) => {
      if (!message || !message.action) return false;

      if (message.action === 'CA_PING') {
        sendResponse({ ok: true, version: AGENT_VERSION, pageType: detectPageType() });
        return true;
      }

      if (message.action === 'CA_EXTRACT_STRUCTURAL') {
        (async () => {
          const pageType = detectPageType();
          const orderRes = pageType === 'orders' ? extractOrdersFromDom(document) : { orders: [], drawerOrder: null, diagnostics: null };
          const financeRes = pageType === 'finance' ? extractFinanceSnapshot(document) : null;
          sendResponse({
            ok: true,
            version: AGENT_VERSION,
            pageType,
            url: location.href,
            orders: orderRes.orders,
            drawerOrder: orderRes.drawerOrder,
            finance: financeRes,
            diagnostics: orderRes.diagnostics
          });
        })();
        return true;
      }

      if (message.action === 'CA_EXPAND_INLINE_ADDRESSES') {
        (async () => {
          const expanded = await expandAllInlineAccordions(document);
          sendResponse({
            ok: true,
            version: AGENT_VERSION,
            orders: expanded.orders,
            drawerOrder: expanded.drawerOrder,
            diagnostics: expanded.diagnostics
          });
        })();
        return true;
      }

      return false;
    };

    chrome.runtime.onMessage.addListener(onMessage);

    root.__etsyCopilotV2Cleanup = () => {
      try { observer.disconnect(); } catch {}
      try { chrome.runtime.onMessage.removeListener(onMessage); } catch {}
    };
  }

  const exported = Object.freeze({
    AGENT_VERSION,
    detectPageType,
    extractStructuredLines,
    isolateOrderCards,
    extractOrdersFromDom,
    extractOpenDrawerOrder,
    extractFinanceSnapshot,
    expandAllInlineAccordions
  });

  root.EtsyCopilotContentAgent = exported;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = exported;
  }
})(typeof globalThis !== 'undefined' ? globalThis : window);
