// Etsy Copilot v2.1.0 — Katman 2: Dil ve Google-Çeviri Bağımsız Yapısal DOM & Inline Accordion Ajanı
// - "Ship to" / İngilizce metin aramasına BAĞIMLI DEĞİLDİR.
// - Google Çeviri (<font dir="auto">) ve parçalı "#<a>4210985421</a>" metin düğümlerini birleştirerek okur.
// - Çekmece açıkken arka plandaki <main aria-hidden="true"> kartlarını kaybetmez; hem listeyi hem çekmeceyi eşzamanlı okur.

(function (root) {
  'use strict';

  const AGENT_VERSION = '2.1.0';

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
   * Google Çeviri'nin <font dir="auto"> sarmallarını ve parçalı inline etiketleri düzleştirerek blok bazlı temiz satırlar üretir.
   * ÖNEMLİ: Çekmece (Drawer) açıkken Etsy arka plandaki <main> kapsayıcısına aria-hidden="true" koyar;
   * bu yüzden kapsayıcı elementlerde aria-hidden nedeniyle okuma KESİNLİKLE iptal edilmez!
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
      if (node.id === 'gdpr-privacy-settings') return;

      const cls = typeof node.className === 'string' ? node.className : '';
      if (/\b(?:wt-screen-reader-only|screen-reader-only|sr-only|goog-te-spinner|wt-menu__body|wt-tooltip__content|wt-popover__content)\b/i.test(cls)) {
        return;
      }

      const role = node.getAttribute ? (node.getAttribute('role') || '').toLowerCase() : '';
      if (role === 'menu' || role === 'tooltip') return;

      // Yalnızca kapalı modal/overlay pencereleri veya dekoratif ikonları atla; <main aria-hidden="true"> veya kart kapsayıcılarını ASLA atlama!
      if (node !== el && node.getAttribute && node.getAttribute('aria-hidden') === 'true') {
        if (role === 'dialog' || /\b(?:wt-overlay|wt-icon)\b/i.test(cls) || node.hasAttribute('data-wt-overlay')) {
          return;
        }
        if (!includeHiddenPanels && /\bwt-content-toggle__body\b/i.test(cls)) {
          return;
        }
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

  function isElementHiddenCss(el, doc) {
    let cur = el;
    const stopNode = doc ? doc.body : null;
    while (cur && cur !== stopNode && cur.nodeType === 1) {
      if (cur.hasAttribute && cur.hasAttribute('hidden')) return true;
      if (cur.id === 'gdpr-privacy-settings') return true;
      const ariaHidden = cur.getAttribute && cur.getAttribute('aria-hidden') === 'true';
      if (ariaHidden) {
        const cls = typeof cur.className === 'string' ? cur.className : '';
        const role = (cur.getAttribute('role') || '').toLowerCase();
        if (role === 'dialog' || /\bwt-overlay\b/i.test(cls) || cur.hasAttribute('data-wt-overlay')) {
          return true;
        }
      }
      const inlineStyle = (cur.getAttribute && cur.getAttribute('style')) || '';
      if (/display\s*:\s*none|visibility\s*:\s*hidden/i.test(inlineStyle)) return true;
      if (typeof getComputedStyle === 'function') {
        try {
          const st = getComputedStyle(cur);
          if (st && (st.display === 'none' || st.visibility === 'hidden')) return true;
        } catch {}
      }
      cur = cur.parentElement;
    }
    return false;
  }

  const DRAWER_CONTAINER_SELECTOR = 'aside, [role="dialog"], [aria-modal="true"], .wt-overlay, [data-wt-overlay], [data-region*="order-drawer" i], [data-region*="drawer" i], [class*="order-drawer" i], [class*="panel--flyout" i], [class*="flyout" i]';

  function isInsideDrawer(el) {
    if (!el || !el.closest) return false;
    const hit = el.closest(DRAWER_CONTAINER_SELECTOR);
    if (!hit) return false;
    // Eğer .wt-overlay bütün sayfayı sarmalıyorsa (içinde >1 sipariş varsa) bu bir çekmece değil ana kapsayıcıdır
    return collectUniqueOrderIds(hit).size <= 1;
  }

  function collectUniqueOrderIds(el) {
    const ids = new Set();
    if (!el) return ids;
    const raw = (el.textContent || '').replace(/\u00A0/g, ' ');
    for (const m of raw.matchAll(/#\s*(\d{9,12})\b/g)) ids.add(m[1]);
    for (const m of raw.matchAll(/\b(\d{9,12})\s*(?:\(\s*Etsy\s*\)|via\s+Etsy\b)/gi)) ids.add(m[1]);
    return ids;
  }

  function matchOrderIdOnCompactElement(el) {
    if (!el || !el.tagName) return null;
    const tag = el.tagName.toUpperCase();
    if (IGNORED_TAGS.has(tag)) return null;

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

  /**
   * P-Minimal (En İç Yaprak) Sipariş Çapası Keşfi:
   * Etsy "#<a href='...'>4210985421</a> (Etsy)" şeklinde metin düğümlerini böldüğünde bile
   * dış kapsayıcı div'leri DEĞİL, doğrudan en içteki yaprak sipariş satırını/linkini yakalar!
   */
  function findMinimalOrderAnchors(doc) {
    const listAnchors = [];
    const drawerAnchors = [];
    if (!doc || !doc.body) return { listAnchors, drawerAnchors };

    const candidates = doc.body.querySelectorAll('*');
    for (const el of candidates) {
      const id = matchOrderIdOnCompactElement(el);
      if (!id) continue;

      // P-Minimality: Alt ağaçtaki (descendant) herhangi bir element aynı sipariş ID'sini eşliyorsa üst kapsayıcıyı atla!
      const descendants = el.querySelectorAll ? el.querySelectorAll('*') : (el.children || []);
      let descendantMatches = false;
      for (let i = 0; i < descendants.length; i++) {
        if (matchOrderIdOnCompactElement(descendants[i]) === id) {
          descendantMatches = true;
          break;
        }
      }
      if (descendantMatches) continue;
      if (isElementHiddenCss(el, doc)) continue;

      const lineCtx = ((el.parentElement ? el.parentElement.textContent : el.textContent) || '')
        .replace(/\u00A0/g, ' ')
        .replace(/\s+/g, ' ')
        .trim()
        .slice(0, 180);

      const viaEtsyDrawer = /\bvia\s+Etsy\b|\bEtsy\s+(?:üzerinden|aracılığıyla)\b/i.test(lineCtx);
      if (viaEtsyDrawer || isInsideDrawer(el)) {
        drawerAnchors.push({ id, el });
      } else {
        listAnchors.push({ id, el });
      }
    }

    return { listAnchors, drawerAnchors };
  }

  /**
   * Matematiksel Sipariş Kartı İzolasyonu:
   * 1. En iç yaprak #4210985421 çapasından başlar (asla dış kart div'inden başlamaz!).
   * 2. Yukarı tırmanırken:
   *    - 2. bir sipariş ID'si görürse durur.
   *    - Mevcut kapsayıcı zaten ürün/adres alanlarını içeriyorsa ve üst kapsayıcı "Select all" gibi 2. checkbox ekliyorsa durur!
   */
  function isolateOrderCards(doc) {
    const cards = [];
    const seen = new Set();
    if (!doc || !doc.body) return cards;

    const { listAnchors } = findMinimalOrderAnchors(doc);

    for (const anchor of listAnchors) {
      if (seen.has(anchor.id)) continue;

      let curr = anchor.el;
      let best = anchor.el;

      while (curr && curr !== doc.body && curr.tagName !== 'MAIN' && curr.id !== 'root' && curr.id !== 'content') {
        if (curr.querySelector && curr.querySelector('nav, [role="tablist"], [role="navigation"]')) {
          break;
        }
        const ids = collectUniqueOrderIds(curr);
        if (ids.size > 1) break;

        if (ids.size === 1 && ids.has(anchor.id)) {
          const bestHasContent = Boolean(
            best.querySelector && (
              best.querySelector('a[href*="/listing/"], img')
              || best.querySelector('[aria-controls], [aria-expanded], address')
              || extractStructuredLines(best, false).length >= 5
            )
          );
          const currCheckboxes = curr.querySelectorAll ? curr.querySelectorAll('input[type="checkbox"]').length : 0;
          if (bestHasContent && currCheckboxes >= 2) {
            break;
          }
          best = curr;
        }
        curr = curr.parentElement;
      }

      // Eğer aynı sipariş ID'si hem listede hem portal çekmecede geçiyorsa ve bu kopya çekmece kopyasıysa (checkbox yok ama daha önce checkbox'lı kart eklendiyse) seen korur
      seen.add(anchor.id);
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

  function isPureIconToolbar(container) {
    if (!container || !container.querySelectorAll) return false;
    const btns = Array.from(container.querySelectorAll('button, [role="button"]'));
    if (btns.length < 2) return false;
    return btns.every((b) => extractInlineText(b).length === 0);
  }

  /**
   * Dil-Bağımsız Sağ Kargo Sütunu ve Adres Paneli Çözücü:
   * 1) DOM sırası (yaprak idAnchor ve ürün görselinden SONRA gelen sağ sütun),
   * 2) aria-controls / aria-expanded / "Ship to" başlık satırı chevron <button> yapısal bağı ile çalışır.
   */
  function locateFulfillmentZone(cardEl, idAnchor, doc) {
    const result = {
      toggleBtn: null,
      addressPanels: [],
      rightColumnEl: null
    };
    if (!cardEl || !cardEl.querySelectorAll) return result;

    // idLineEl yalnızca kısa "#4210985421 (Etsy) US$128.40" satırı olabilir; asla tüm kartı kapsayamaz!
    let idLineEl = idAnchor;
    if (idAnchor && idAnchor.parentElement && cardEl.contains(idAnchor.parentElement)) {
      const pText = (idAnchor.parentElement.textContent || '').replace(/\s+/g, ' ').trim();
      if (pText.length <= 140 && !(idAnchor.parentElement.querySelector && idAnchor.parentElement.querySelector('img, button[aria-controls]'))) {
        idLineEl = idAnchor.parentElement;
      }
    }

    const productImg = cardEl.querySelector('a[href*="/listing/"] img, img[src*="etsystatic"]') || cardEl.querySelector('img');

    // 1. Önce Sağ Kargo Sütununu (rightColumnEl) DOM sırası ve yapısal dışlama ile kesin olarak izole et
    const allContainers = Array.from(cardEl.querySelectorAll('div, section, td'));
    const candidateCols = allContainers.filter((d) => {
      if (collectUniqueOrderIds(d).size > 0) return false;
      if (d.querySelector && d.querySelector('input[type="checkbox"], a[href*="/listing/"]')) return false;
      if (productImg && d.contains && d.contains(productImg)) return false;
      if (!isFollowingInDom(idAnchor, d)) return false;
      if (productImg && !isFollowingInDom(productImg, d)) return false;
      if (isPureIconToolbar(d)) return false;
      const lines = extractStructuredLines(d, true);
      if (lines.length < 2 || lines.length > 28) return false;
      // Ürün varyasyon / SKU bloğunu dışla
      if (lines.some((l) => /^(?:Quantity|Adet|Miktar)\s*\d+$/i.test(l) || /^(?:SKU|Stok\s*Kodu)\b/i.test(l))) {
        return false;
      }
      return true;
    });

    const topRightCols = candidateCols.filter((c) => !candidateCols.some((other) => other !== c && other.contains(c)));
    if (topRightCols.length > 0) {
      result.rightColumnEl = topRightCols.sort((a, b) => {
        const linesA = extractStructuredLines(a, true).length;
        const linesB = extractStructuredLines(b, true).length;
        return linesB - linesA;
      })[0];
    }

    // 2. Toggle butonunu bul:
    // Öncelik 1: aria-controls / aria-expanded / .wt-content-toggle (idAnchor'dan sonra gelen)
    // Öncelik 2: Kendi metni veya ebeveyn başlığı "Ship to / Shipped to / Şuraya gönder / Gönderim" içeren chevron buton
    // Öncelik 3: Sağ kargo sütunu içindeki aksiyon-dışı chevron <button>
    const shipHeadingHintRe = /\b(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Şuraya\s*gönder|Gönderim|Teslimat|Kargo\s*adresi)\b/i;
    const searchScope = result.rightColumnEl || cardEl;

    const ariaToggles = Array.from(cardEl.querySelectorAll('button[aria-controls], button[aria-expanded], .wt-content-toggle, [role="button"][aria-expanded]'))
      .filter((btn) => isFollowingInDom(idAnchor, btn))
      .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn))
      .filter((btn) => !(idLineEl && idLineEl.contains && idLineEl.contains(btn)));

    result.toggleBtn = ariaToggles.find((b) => b.hasAttribute('aria-controls')) || ariaToggles[0] || null;

    if (!result.toggleBtn) {
      const allFollowingBtns = Array.from(searchScope.querySelectorAll('button, [role="button"]'))
        .filter((btn) => isFollowingInDom(idAnchor, btn))
        .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn))
        .filter((btn) => !(idLineEl && idLineEl.contains && idLineEl.contains(btn)))
        .filter((btn) => !(btn.parentElement && isPureIconToolbar(btn.parentElement)));

      const shipHeaderBtn = allFollowingBtns.find((btn) => {
        const ownTxt = extractInlineText(btn);
        const parentTxt = btn.parentElement ? extractInlineText(btn.parentElement) : '';
        return shipHeadingHintRe.test(ownTxt) || (parentTxt.length <= 80 && shipHeadingHintRe.test(parentTxt));
      });

      result.toggleBtn = shipHeaderBtn || (result.rightColumnEl ? allFollowingBtns[0] : null) || null;
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
      // Eğer buton bir başlık satırının (ship-hdr) içindeyse hemen sonraki kardeş div tam adres kutusudur!
      const hdrParent = result.toggleBtn.parentElement;
      if (hdrParent && hdrParent !== cardEl && hdrParent.nextElementSibling && cardEl.contains(hdrParent.nextElementSibling)) {
        addPanel(hdrParent.nextElementSibling);
      }
      if (result.toggleBtn.nextElementSibling && cardEl.contains(result.toggleBtn.nextElementSibling)) {
        addPanel(result.toggleBtn.nextElementSibling);
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
          if (collectUniqueOrderIds(p).size > 0 || (p.querySelector && p.querySelector('input[type="checkbox"], a[href*="/listing/"]'))) {
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
    const idEscaped = orderId.replace(/\D/g, '');
    const idLine = lines.find((l) => l.includes(idEscaped)) || '';
    // Kargo satırındaki "(US$0.00)" parantez içi ücretsiz kargo tutarının sipariş toplamını ezmesini engelle
    const sanitizedJoined = lines
      .map((l) => l.replace(/\b(?:Standard|Standart|Express|Expedited|Priority)[^\n(]*\([^)]*\)/gi, ''))
      .join('\n');

    for (const source of [idLine, sanitizedJoined]) {
      if (!source) continue;
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

    // 1. Kart içinde DOM sırasına göre #siparişNo çapasından HEMEN ÖNCE gelen alıcı butonu/linki (Örn: "jessica benson ▾", "Damien Panico ▾")
    if (cardEl && cardEl.querySelectorAll && idAnchor) {
      const interactivePreceding = Array.from(cardEl.querySelectorAll('button, a[href*="/people/"], a[href*="buyer"], [aria-haspopup="true"], [aria-haspopup="menu"], .wt-menu__trigger'))
        .filter((el) => !el.contains(idAnchor) && collectUniqueOrderIds(el).size === 0 && isFollowingInDom(el, idAnchor));

      for (let i = interactivePreceding.length - 1; i >= 0; i--) {
        const t = extractInlineText(interactivePreceding[i]);
        if (!isInvalid(t) && !isStreet(t)) return t;
      }
    }

    // 2. Satır dizisinde #siparişNo satırının üstündeki geçerli kişi adı satırı
    const idLineIdx = cardLines.findIndex((l) => l.includes(orderId));
    for (let k = idLineIdx - 1; k >= 0 && k >= idLineIdx - 6; k--) {
      const cand = cardLines[k].replace(/[▾▼⌄⌃]/g, '').trim();
      if (!isInvalid(cand) && !isStreet(cand)) {
        return cand;
      }
    }

    return '';
  }

  function sliceAddressLinesFromColumn(colLines) {
    if (!Array.isArray(colLines) || colLines.length === 0) return [];

    // Bilinen İngilizce/Türkçe kargo başlığı varsa EN SONUNCUSUNUN hemen sonrasından kes
    const headingRe = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in|ildi)?|Gönderilecek\s*adres|Gönderildiği\s*yer|Kargo\s*adresi|Alıcı)\s*:?$/i;
    for (let i = colLines.length - 1; i >= 0; i--) {
      const cleaned = colLines[i].replace(/[▾▼⌄⌃^]/g, '').trim();
      if (headingRe.test(cleaned)) {
        return colLines.slice(i + 1);
      }
    }

    // Dil-bağımsız yapısal kesim: Sağ sütunun üstündeki kargo durum / tarih / taşıyıcı / takip no satırlarını atla
    let start = 0;
    while (start < colLines.length) {
      const l = colLines[start];
      if (
        /\b(?:No\s+tracking|Takip\s+(?:numarası\s+)?yok|Ship\s+by|kargoya\s+verin|Ordered\s+|sipariş\s+edildi|Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Expedited\s+Shipping|Shipped|Completed?|Tamamland[ıi]|Pre-transit|In\s+transit|Out\s+for\s+delivery|Delivered|Teslim\s+edildi|\d+\s+days?\s+ago|gün\s+önce|Overdue|Gecikmiş|USPS|FedEx|UPS|DHL|ShipEntegra|Royal\s+Mail|Canada\s+Post)\b/i.test(l)
        || /\(\s*(?:ABD\s*\$|US\s*\$|\$|€|₺)[\d.,]+\s*\)/i.test(l)
        || /^[A-Z0-9]{10,30}$/i.test(l.replace(/\s+/g, ''))
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

      // 3. Yedek: Kartta "Ship to / Gönderim adresi" başlığı varsa başlık sonrasını dene (başlık yoksa tüm kartı parseAddressLines'a verip #siparişNo satırında kesilmesine izin verme!)
      if (!bestAddr || (!bestAddr.isFullAddress && !bestAddr.city && !bestAddr.recipientName)) {
        const headingRe = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in|ildi)?|Gönderilecek\s*adres|Gönderildiği\s*yer|Kargo\s*adresi)\s*:?$/i;
        let lastHeadingIdx = -1;
        for (let hi = cardLines.length - 1; hi >= 0; hi--) {
          if (headingRe.test(cardLines[hi].replace(/[▾▼⌄⌃^]/g, '').trim())) {
            lastHeadingIdx = hi;
            break;
          }
        }
        if (lastHeadingIdx !== -1) {
          const fallbackParsed = parser.parseAddressLines(cardLines.slice(lastHeadingIdx + 1), cardText);
          if (fallbackParsed.isFullAddress || (!bestAddr?.city && (fallbackParsed.city || fallbackParsed.recipientName))) {
            bestAddr = fallbackParsed;
          }
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
        summaryCityCount: orders.filter((o) => Boolean(o.city)).length,
        realBuyerCount: orders.filter((o) => o.buyerName && o.buyerName !== 'Etsy Müşterisi').length,
        drawerOpen: Boolean(drawerOrder),
        drawerDetectedId: drawerOrder ? drawerOrder.receiptId : null
      }
    };
  }

  /**
   * Açık yan sipariş çekmecesini (<aside>, [role="dialog"], .wt-overlay, "#4210985421 via Etsy" veya ?order_id=...) yapısal olarak okur.
   */
  function extractOpenDrawerOrder(doc, urlHint) {
    const parser = getParser();
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !targetDoc.body || !parser) return null;

    const candidateContainers = [];
    const addCandidate = (el) => {
      if (!el || el === targetDoc.body || el.tagName === 'MAIN') return;
      if (isElementHiddenCss(el, targetDoc)) return;
      if (!candidateContainers.includes(el)) candidateContainers.push(el);
    };

    // Yöntem 1: Bilinen çekmece / overlay / dialog kapsayıcıları
    for (const dr of targetDoc.querySelectorAll(DRAWER_CONTAINER_SELECTOR)) {
      if (collectUniqueOrderIds(dr).size === 1) {
        addCandidate(dr);
      }
    }

    // Yöntem 2: "#4210985421 via Etsy" çapasından yukarıya tekil sipariş tırmanışı (Etsy Mission Control gerçek çekmece imzası!)
    const { drawerAnchors, listAnchors } = findMinimalOrderAnchors(targetDoc);
    const climbDrawerAnchor = (anchorEl, orderId) => {
      let curr = anchorEl;
      let best = anchorEl;
      while (curr && curr !== targetDoc.body && curr.tagName !== 'MAIN' && curr.id !== 'root' && curr.id !== 'content') {
        const ids = collectUniqueOrderIds(curr);
        if (ids.size > 1) break;
        if (curr.querySelector && curr.querySelector('input[type="checkbox"]')) break;
        // Arka plandaki liste kartıyla birleşmemesi için aynı ID'nin "(Etsy)" liste satırını içeriyorsa dur
        const rawTxt = (curr.textContent || '').replace(/\u00A0/g, ' ');
        if (new RegExp(`${orderId}\\s*\\(\\s*Etsy\\s*\\)`, 'i').test(rawTxt) && /\bvia\s+Etsy\b/i.test(rawTxt)) {
          break;
        }
        if (ids.size === 1 && ids.has(orderId)) best = curr;
        curr = curr.parentElement;
      }
      return best;
    };

    for (const da of drawerAnchors) {
      addCandidate(climbDrawerAnchor(da.el, da.id));
    }

    // Yöntem 3: URL'de ?order_id=4210985421 varsa checkbox içermeyen ikincil çapayı çekmece olarak tırman
    const currentUrl = String(urlHint || (targetDoc.location && targetDoc.location.href) || (typeof location !== 'undefined' ? location.href : '') || '');
    const urlOrderMatch = currentUrl.match(/[?&]order_id=(\d{9,12})\b/);
    if (urlOrderMatch) {
      const targetId = urlOrderMatch[1];
      for (const la of [...drawerAnchors, ...listAnchors]) {
        if (la.id !== targetId) continue;
        const climbed = climbDrawerAnchor(la.el, targetId);
        if (climbed && !(climbed.querySelector && climbed.querySelector('input[type="checkbox"]'))) {
          addCandidate(climbed);
        }
      }
    }

    const standaloneShipHeadingRe = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in|ildi)?|Gönderilecek\s*adres|Kargo\s*adresi)\s*:?$/i;

    for (const dr of candidateContainers) {
      const ids = collectUniqueOrderIds(dr);
      if (ids.size !== 1) continue;
      const receiptId = Array.from(ids)[0];

      const drLines = extractStructuredLines(dr, true);
      const drText = drLines.join('\n');

      let parsed = null;
      // 1) <address> etiketi varsa dene
      const addrTag = dr.querySelector ? dr.querySelector('address') : null;
      if (addrTag) {
        parsed = parser.parseAddressLines(extractStructuredLines(addrTag, true), drText);
      }

      // 2) Çekmecenin altındaki müstakil "Ship to" başlığının kendi yerel kutusunu (ebeveynini) dene (sağdaki "Selected by buyer" sütunuyla karışmasını %100 önler!)
      if (!parsed || !parsed.isFullAddress) {
        const subBlocks = Array.from(dr.querySelectorAll('div, section, td'));
        for (let i = subBlocks.length - 1; i >= 0; i--) {
          const sbLines = extractStructuredLines(subBlocks[i], true);
          if (sbLines.length >= 4 && sbLines.length <= 14 && standaloneShipHeadingRe.test(sbLines[0].replace(/[▾▼⌄⌃^]/g, '').trim())) {
            const candidate = parser.parseAddressLines(sbLines.slice(1), drText);
            if (candidate.isFullAddress) {
              parsed = candidate;
              break;
            }
          }
        }
      }

      // 3) Tüm çekmece satırlarında son "Ship to" başlığından sonrasını kes
      if (!parsed || !parsed.isFullAddress) {
        const sliced = sliceAddressLinesFromColumn(drLines);
        parsed = parser.parseAddressLines(sliced, drText);
      }

      if (!parsed || !parsed.isFullAddress) continue;

      const orderFromMatch = drText.match(/Order\s+from\s+([^\r\n]+)/i);
      const buyerAcc = orderFromMatch && !parser.isInvalidPersonName(orderFromMatch[1].trim())
        ? orderFromMatch[1].trim()
        : '';

      const { amount: orderTotal, currency } = extractMoneyFromCardLines(drLines, receiptId);
      const orderDate = extractOrderDateFromLines(drLines);
      const txMatch = drText.match(/(?:Transaction\s*ID|İşlem\s*Kimliği)\s*:\s*(\d{7,14})/i);
      const skuLine = drLines.find((l) => /^(?:SKU|Stok\s*Kodu)\b/i.test(l));

      let productTitle = '';
      const listingLink = dr.querySelector ? dr.querySelector('a[href*="/listing/"]') : null;
      if (listingLink) productTitle = extractInlineText(listingLink);

      return {
        receiptId,
        buyerName: parsed.recipientName || buyerAcc || 'Etsy Müşterisi',
        recipientName: parsed.recipientName,
        buyerAccountName: buyerAcc,
        streetAddress: parsed.streetAddress,
        secondAddress: parsed.secondAddress,
        city: parsed.city,
        state: parsed.state,
        postalCode: parsed.postalCode,
        country: parsed.country || (parsed.state ? 'United States' : ''),
        phone: parsed.phone,
        iossNumber: parsed.iossNumber,
        rawAddressText: parsed.rawAddressText,
        isFullAddress: true,
        productTitle,
        orderTotal,
        currency,
        orderDate,
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
    findMinimalOrderAnchors,
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
