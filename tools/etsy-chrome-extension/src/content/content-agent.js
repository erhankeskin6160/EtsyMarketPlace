// Etsy Copilot v2.4.0 — Katman 2: Dil ve Google-Çeviri Bağımsız Yapısal DOM & Inline Accordion Ajanı
// - "Ship to" / İngilizce metin aramasına BAĞIMLI DEĞİLDİR; varsa başlığı ve gizli .wt-content-toggle__body HTML'ini de çözer.
// - Google Çeviri (<font dir="auto">) ve parçalı "#<a>4210985421</a>" metin düğümlerini birleştirerek okur.
// - Çekmece açıkken arka plandaki <main aria-hidden="true"> kartlarını kaybetmez; hem listeyi hem çekmeceyi eşzamanlı okur.

(function (root) {
  'use strict';

  const AGENT_VERSION = '2.6.0';

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

  function extractNativeInnerTextLines(el) {
    if (!el || typeof el.innerText !== 'string') return [];
    const raw = el.innerText;
    if (!raw || !raw.trim()) return [];
    return raw
      .split(/\r?\n/)
      .map((line) => line.replace(/\u00A0/g, ' ').replace(/\t+/g, ' ').replace(/\s{2,}/g, ' ').trim())
      .filter(Boolean);
  }

  /**
   * Gizli/kapalı .wt-content-toggle__body panellerinde visibility:hidden veya display:none nedeniyle
   * innerText boş dönebildiğinden doğrudan innerHTML'den blok sınırlarını koruyarak temiz satırlar üretir.
   */
  function htmlToCleanLines(rawHtml, doc) {
    if (!rawHtml || typeof rawHtml !== 'string') return [];
    const prepared = rawHtml
      .replace(/<(script|style|svg|noscript|template)\b[^>]*>[\s\S]*?<\/\1>/gi, '')
      .replace(/<[^>]*\b(?:wt-screen-reader-only|screen-reader-only|sr-only)\b[^>]*>[\s\S]*?<\/[^>]+>/gi, '')
      .replace(/<br\s*\/?>/gi, '\n')
      .replace(/<\/(p|div|li|tr|td|dt|dd|h[1-6]|section|article|address|button)>/gi, '\n');

    let text = '';
    try {
      if (typeof DOMParser !== 'undefined') {
        const parsedDoc = new DOMParser().parseFromString(prepared, 'text/html');
        text = parsedDoc && parsedDoc.body ? (parsedDoc.body.textContent || '') : '';
      } else if (doc && doc.createElement) {
        const tmp = doc.createElement('div');
        tmp.innerHTML = prepared;
        text = tmp.textContent || '';
      }
    } catch {
      text = prepared.replace(/<[^>]+>/g, ' ');
    }

    return text
      .split(/\r?\n/)
      .map((line) => line.replace(/\u00A0/g, ' ').replace(/\s{2,}/g, ' ').trim())
      .filter(Boolean);
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

  const KNOWN_CARD_SELECTORS = [
    '[data-region="order-card"]',
    '[class*="order-card"]',
    '[class*="OrderCard"]',
    '.panel-body',
    '.wt-card',
    'tr[data-order-id]',
    'tr.order-row',
    '[data-testid*="order-card"]'
  ].join(', ');

  /**
   * Yalnızca gerçek Shop Manager sayfa kabuğunu (Orders H1 başlığı, arama kutusu, Action needed özeti,
   * 3+ sekmeli All/New/Completed filtre barı veya sol menü #nav-subnav) tespit eder.
   * ÖNEMLİ: Kartların içindeki alıcı/aksiyon açılır menülerinde <nav> veya [role="navigation"] bulunabildiği için
   * jenerik `querySelector('nav')` KULLANILMAZ!
   */
  function isPageShellContainer(el) {
    if (!el || !el.querySelector) return false;
    if (el.querySelector('#nav-subnav, .action-needed, input[placeholder*="Search" i], input[placeholder*="Ara" i], input[type="search"], input[name="search_query"]')) {
      return true;
    }
    const h1 = el.querySelector('h1');
    if (h1 && /\b(?:Orders|Siparişler)\b/i.test((h1.textContent || '').trim())) {
      return true;
    }
    if (el.querySelectorAll && el.querySelectorAll('[role="tab"]').length >= 3) {
      return true;
    }
    return false;
  }

  function hasOrderItemSignals(el) {
    if (!el || !el.querySelector) return false;
    if (el.querySelector('a[href*="/listing/"], img[src*="etsystatic"], img')) return true;
    return /\b(?:Quantity|SKU|Adet|Miktar|Stok\s*Kodu)\b/i.test(el.textContent || '');
  }

  function hasOrderFulfillmentSignals(el) {
    if (!el || !el.querySelector) return false;
    if (el.querySelector('.wt-content-toggle__body, [id*="wt-content-toggle"], [aria-controls], address')) {
      return true;
    }
    return /(?:\bShip\s*to\b|\bShipped\s*to\b|\bOrdered\s+|\bStandard\s*Shipping\b|Standart\s*Kargo|\bExpress\s*Shipping\b|\bPriority\s*Mail\b|\bNo\s*tracking\b|Takip\s+(?:numaras[ıi]\s+)?yok|\bDelivered\b|Teslim\s*edildi|\bIn\s*transit\b|\bPre-transit\b|Şuraya\s*gönder|Gönderim|Teslimat)/i.test(el.textContent || '');
  }

  function isCompleteSingleOrderCard(el) {
    return hasOrderItemSignals(el) && hasOrderFulfillmentSignals(el);
  }

  /**
   * Matematiksel Sipariş Kartı İzolasyonu (v2.2.0):
   * 1. En iç yaprak #4210985421 çapasından başlar (asla dış kart div'inden başlamaz!).
   * 2. Kart içi <nav>/[role="navigation"] menülerine takılmadan gerçek kart kapsayıcısına (.panel-body / order-card) ulaşır.
   * 3. 2. bir sipariş ID'si, sayfa kabuğu veya "Select all" tarih grubu gördüğü anda tam kart sınırında durur!
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
        if (isPageShellContainer(curr)) {
          break;
        }
        const ids = collectUniqueOrderIds(curr);
        if (ids.size > 1) break;

        if (ids.size === 1 && ids.has(anchor.id)) {
          const bestComplete = isCompleteSingleOrderCard(best);
          if (bestComplete) {
            const currCheckboxes = curr.querySelectorAll ? curr.querySelectorAll('input[type="checkbox"]').length : 0;
            if (currCheckboxes >= 2) {
              break;
            }
            const bestTxt = best.textContent || '';
            const currTxt = curr.textContent || '';
            if (/\b(?:Select\s+all|Tümünü\s+seç)\b/i.test(currTxt) && !/\b(?:Select\s+all|Tümünü\s+seç)\b/i.test(bestTxt)) {
              break;
            }
          }
          best = curr;
        }
        curr = curr.parentElement;
      }

      // Bilinen kart kapsayıcısı (.panel-body / [data-region="order-card"]) tam kart içeriğine sahipse ve tekil ID taşıyorsa gürültüsüz iç kartı tercih et
      if (anchor.el && anchor.el.closest) {
        const known = anchor.el.closest(KNOWN_CARD_SELECTORS);
        if (known && best && best.contains && best.contains(known)) {
          const kIds = collectUniqueOrderIds(known);
          if (kIds.size === 1 && kIds.has(anchor.id) && isCompleteSingleOrderCard(known)) {
            best = known;
          }
        }
      }

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
    // Öncelik 2: Kendi metni veya ebeveyn başlığı "Ship to / Shipped to / Şuraya gönder / Gönderim" içeren ("Ship today" hariç!) buton/link
    // Öncelik 3: Sağ kargo sütunu içindeki chevron/ok içeren buton veya ilk aksiyon-dışı buton
    const shipHeadingHintRe = /(?:\bShip\s*to\b(?!\s*(?:day|today))|\bShipped\s*to\b|\bShipping\s*address\b|\bDelivery\s*address\b|Şuraya\s*gönder|Gönderim|Teslimat|Kargo\s*adresi)/i;
    const searchScope = result.rightColumnEl || cardEl;

    const ariaToggles = Array.from(cardEl.querySelectorAll('[aria-controls], [aria-expanded], .wt-content-toggle, [data-wt-content-toggle], a[class*="toggle"]'))
      .filter((btn) => isFollowingInDom(idAnchor, btn))
      .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn))
      .filter((btn) => !(idLineEl && idLineEl.contains && idLineEl.contains(btn)));

    result.toggleBtn = ariaToggles.find((b) => b.hasAttribute('aria-controls')) || ariaToggles[0] || null;

    if (!result.toggleBtn) {
      const allFollowingBtns = Array.from(searchScope.querySelectorAll('button, [role="button"], a.wt-btn, a[class*="toggle"], summary'))
        .filter((btn) => isFollowingInDom(idAnchor, btn))
        .filter((btn) => collectUniqueOrderIds(btn).size === 0 && !isMenuOrActionBtn(btn))
        .filter((btn) => !(idLineEl && idLineEl.contains && idLineEl.contains(btn)))
        .filter((btn) => !(btn.parentElement && isPureIconToolbar(btn.parentElement)));

      const shipHeaderMatches = allFollowingBtns
        .filter((btn) => {
          const ownTxt = extractInlineText(btn);
          const parentTxt = btn.parentElement ? extractInlineText(btn.parentElement) : '';
          const hasChevronHint = /[▾▼⌄⌃^]/.test(ownTxt)
            || (typeof btn.className === 'string' && /chevron|toggle|arrow|expand/i.test(btn.className))
            || Boolean(btn.querySelector && btn.querySelector('svg, [class*="chevron" i], [class*="arrow" i]'));
          return shipHeadingHintRe.test(ownTxt) || (parentTxt.length <= 140 && shipHeadingHintRe.test(parentTxt)) || (hasChevronHint && shipHeadingHintRe.test(parentTxt));
        })
        .sort((a, b) => extractInlineText(a).length - extractInlineText(b).length);

      result.toggleBtn = shipHeaderMatches[0] || (result.rightColumnEl ? allFollowingBtns[0] : null) || null;
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
    // DİKKAT: Takip numarası deseni EN AZ 6 RAKAM içermelidir; aksi halde "Jessica Benson" / "Cindy Nguyen" / "daymsleftnut" gibi kişi adlarını takip kodu sanıp yutar!
    let start = 0;
    while (start < colLines.length) {
      const l = colLines[start];
      const digitCount = (l.match(/\d/g) || []).length;
      if (
        /(?:\bNo\s+tracking\b|Takip\s+(?:numaras[ıi]\s+)?yok|\bShip\s+by\b|kargoya\s+verin|\bOrdered\s+|sipariş\s+edildi|\bStandard\s+Shipping\b|Standart\s+Kargo|\bExpress\s+Shipping\b|\bPriority\s+Mail\b|\bExpedited\s+Shipping\b|\bShipped\b|\bCompleted?\b|Tamamland[ıi]|\bPre-transit\b|\bIn\s+transit\b|\bOut\s+for\s+delivery\b|\bDelivered\b|Teslim\s+edildi|\b\d+\s+days?\s+ago\b|gün\s+önce|\bOverdue\b|Gecikmiş|\bUSPS\b|\bFedEx\b|\bUPS\b|\bDHL\b|\bShipEntegra\b|\bRoyal\s+Mail\b|\bCanada\s+Post\b)/i.test(l)
        || /\(\s*(?:ABD\s*\$|US\s*\$|\$|€|₺)[\d.,]+\s*\)/i.test(l)
        || (digitCount >= 6 && /^[A-Z0-9]{10,30}$/i.test(l.replace(/\s+/g, '')))
      ) {
        start++;
        continue;
      }
      break;
    }
    return colLines.slice(start);
  }

  const ITEM_META_LABEL_STOP_RE = /^(?:Quantity|Adet|Miktar|SKU|Stok\s*Kodu|Personalization|Height|Width|Length|Size|Scale|Color|Colour|Style|Material|Finish|Option|Version|Edition|Model|Kişiselleştirme|Boyut|Renk|Yükseklik|Genişlik|Uzunluk|Ölçü|Seçenek|Malzeme|Ship\s*to|Shipped\s*to|Ship\s*by|Ordered|Standard\s*Shipping|No\s*tracking)\b/i;

  function extractSkuFromLines(lines, fallbackLines = []) {
    for (const pool of [lines, fallbackLines]) {
      if (!Array.isArray(pool) || pool.length === 0) continue;
      for (let i = 0; i < pool.length; i++) {
        const line = pool[i];
        const sameLine = line.match(/^(?:SKU|Stok\s*Kodu)(?:\s*:\s*|\s+)(.+)$/i);
        if (sameLine && sameLine[1].trim()) {
          return sameLine[1].trim();
        }
        if (/^(?:SKU|Stok\s*Kodu)\s*:?$/i.test(line) && i + 1 < pool.length) {
          const next = pool[i + 1].trim();
          if (next && next.length <= 64 && !ITEM_META_LABEL_STOP_RE.test(next)) {
            return next;
          }
        }
      }
    }
    return '';
  }

  function extractVariationFromLines(lines, fallbackLines = []) {
    const varKeySameLineRe = /^(Personalization|Height|Width|Length|Size|Scale|Color|Colour|Style|Material|Finish|Option|Version|Edition|Model|Kişiselleştirme|Boyut|Renk|Yükseklik|Genişlik|Uzunluk|Ölçü|Seçenek|Malzeme)(?:\s*:\s*|\s+)(.+)$/i;
    const varKeyAloneRe = /^(Personalization|Height|Width|Length|Size|Scale|Color|Colour|Style|Material|Finish|Option|Version|Edition|Model|Kişiselleştirme|Boyut|Renk|Yükseklik|Genişlik|Uzunluk|Ölçü|Seçenek|Malzeme)\s*:?$/i;

    for (const pool of [lines, fallbackLines]) {
      if (!Array.isArray(pool) || pool.length === 0) continue;
      const collected = [];
      for (let i = 0; i < pool.length; i++) {
        const line = pool[i];
        const same = line.match(varKeySameLineRe);
        if (same && same[2].trim()) {
          const formatted = `${same[1]}: ${same[2].trim()}`;
          if (!collected.includes(formatted)) collected.push(formatted);
          continue;
        }
        const alone = line.match(varKeyAloneRe);
        if (alone && i + 1 < pool.length) {
          const next = pool[i + 1].trim();
          if (next && next.length <= 140 && !ITEM_META_LABEL_STOP_RE.test(next)) {
            const formatted = `${alone[1]}: ${next}`;
            if (!collected.includes(formatted)) collected.push(formatted);
            i++;
          }
        }
      }
      if (collected.length > 0) return collected.join(' | ');
    }
    return '';
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
    const sampleCards = [];
    let toggleButtonsFound = 0;
    let panelsFound = 0;

    const shipHeadingStandaloneRe = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in|ildi)?|Gönderilecek\s*adres|Gönderildiği\s*yer|Kargo\s*adresi)\s*:?$/i;

    for (const { id, idAnchor, cardEl } of isolated) {
      const structuredLines = extractStructuredLines(cardEl, false);
      const innerTextLines = extractNativeInnerTextLines(cardEl);
      const cardLines = structuredLines.length > 0 ? structuredLines : innerTextLines;
      const cardText = cardLines.join('\n');
      const zone = locateFulfillmentZone(cardEl, idAnchor, targetDoc);

      if (zone.toggleBtn) toggleButtonsFound++;
      panelsFound += zone.addressPanels.length;

      const buyerAccountName = extractBuyerAccountFromLeftColumn(cardEl, idAnchor, id, cardLines)
        || (innerTextLines.length > 0 ? extractBuyerAccountFromLeftColumn(cardEl, idAnchor, id, innerTextLines) : '');

      // 1. Önce yapısal adres panellerini (#wt-content-toggle-..., .wt-content-toggle__body, <address> vb.)
      // hem yapısal DOM yürüyüşü hem de gizli innerHTML -> htmlToCleanLines ile dene
      let bestAddr = null;
      let addrSource = 'Yapısal Sağ Sütun';

      for (const panel of zone.addressPanels) {
        const panelSources = [
          extractStructuredLines(panel, true),
          panel && typeof panel.innerHTML === 'string' ? htmlToCleanLines(panel.innerHTML, targetDoc) : []
        ];
        for (const pLines of panelSources) {
          if (!pLines || pLines.length === 0) continue;
          const firstClean = pLines[0].replace(/[▾▼⌄⌃^]/g, '').trim();
          const trimmedPanel = shipHeadingStandaloneRe.test(firstClean) ? pLines.slice(1) : pLines;
          const candidate = parser.parseAddressLines(trimmedPanel, cardText);
          if (candidate.isFullAddress) {
            bestAddr = candidate;
            addrSource = 'Yapısal Adres Paneli (Tam Adres)';
            break;
          }
          if (!bestAddr && (candidate.city || candidate.state || candidate.recipientName)) {
            bestAddr = candidate;
          }
        }
        if (bestAddr && bestAddr.isFullAddress) break;
      }

      // 2. Sağ kargo sütunundan dil-bağımsız satır ayıklama
      if (!bestAddr || !bestAddr.isFullAddress) {
        if (zone.rightColumnEl) {
          const rightSources = [
            extractStructuredLines(zone.rightColumnEl, true),
            extractNativeInnerTextLines(zone.rightColumnEl)
          ];
          for (const rRaw of rightSources) {
            if (!rRaw || rRaw.length === 0) continue;
            const rightLines = sliceAddressLinesFromColumn(rRaw);
            const rightParsed = parser.parseAddressLines(rightLines, cardText);
            if (rightParsed.isFullAddress || !bestAddr || (!bestAddr.city && (rightParsed.city || rightParsed.recipientName))) {
              bestAddr = rightParsed;
              if (rightParsed.isFullAddress) {
                addrSource = 'Yapısal Sağ Sütun (Tam Adres)';
                break;
              }
            }
          }
        }
      }

      // 3. Yedek: Kartta "Ship to / Gönderim adresi" başlığı varsa YALNIZCA başlık sonrasını dene (requireShipTo disiplini)
      if (!bestAddr || (!bestAddr.isFullAddress && !bestAddr.city && !bestAddr.recipientName)) {
        for (const lineSet of [cardLines, innerTextLines]) {
          if (!lineSet || lineSet.length === 0) continue;
          let lastHeadingIdx = -1;
          for (let hi = lineSet.length - 1; hi >= 0; hi--) {
            if (shipHeadingStandaloneRe.test(lineSet[hi].replace(/[▾▼⌄⌃^]/g, '').trim())) {
              lastHeadingIdx = hi;
              break;
            }
          }
          if (lastHeadingIdx !== -1) {
            const fallbackParsed = parser.parseAddressLines(lineSet.slice(lastHeadingIdx + 1), cardText);
            if (fallbackParsed.isFullAddress || (!bestAddr?.city && (fallbackParsed.city || fallbackParsed.recipientName))) {
              bestAddr = fallbackParsed;
              if (fallbackParsed.isFullAddress) {
                addrSource = 'Kart İçi Ship-To Bloğu (Tam Adres)';
                break;
              }
            }
          }
        }
      }

      const addr = bestAddr || parser.parseAddressLines([], cardText);
      const buyerName = addr.recipientName || buyerAccountName || 'Etsy Müşterisi';
      const moneyPrimary = extractMoneyFromCardLines(cardLines, id);
      const { amount: orderTotal, currency } = moneyPrimary.amount != null
        ? moneyPrimary
        : extractMoneyFromCardLines(innerTextLines, id);

      // Ürün adı (doğrudan a[href*="/listing/"] yapısal çapasından veya başlık sınıfından)
      let productTitle = '';
      const listingLink = cardEl.querySelector
        ? cardEl.querySelector('a[href*="/listing/"], [class*="listing-title" i], [class*="item-title" i]')
        : null;
      if (listingLink) {
        productTitle = extractInlineText(listingLink);
      }
      if (!productTitle) {
        for (const pool of [cardLines, innerTextLines]) {
          const qtyIdx = pool.findIndex((l) => /^(?:Quantity|Adet|Miktar)\b/i.test(l));
          if (qtyIdx > 0 && !/#\s*\d{9,12}/.test(pool[qtyIdx - 1]) && !/coupon|kupon/i.test(pool[qtyIdx - 1])) {
            productTitle = pool[qtyIdx - 1];
            break;
          }
        }
      }

      // SKU, Varyasyon (iki noktalı VEYA iki noktasız: "Height 7.87 Inches", "Personalization 7.5 inches"), Kargo Yöntemi, Takip Kodu, Tarih
      const sku = extractSkuFromLines(cardLines, innerTextLines);
      const variation = extractVariationFromLines(cardLines, innerTextLines);

      const shipMethodLine = cardLines.find((l) => /\b(?:Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Expedited\s+Shipping)\b/i.test(l))
        || innerTextLines.find((l) => /\b(?:Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Expedited\s+Shipping)\b/i.test(l));
      const shippingMethod = shipMethodLine ? shipMethodLine.replace(/\s*\([^)]*\)\s*$/, '').trim() : '';

      const combinedText = innerTextLines.length > 0 ? `${cardText}\n${innerTextLines.join('\n')}` : cardText;
      const trkMatch = combinedText.match(/\b(\d{12,30})\b[\s\r\n]+(?:Shipped|In\s+transit)/i)
        || combinedText.match(/(?:Tracking|Takip\s*No)\s*:\s*([A-Za-z0-9]{8,30})/i);
      const trackingNumber = trkMatch ? trkMatch[1].trim() : '';

      const orderDate = extractOrderDateFromLines(cardLines) || extractOrderDateFromLines(innerTextLines);
      const status = trackingNumber ? 'Shipped' : 'Processing';

      if (sampleCards.length < 5) {
        sampleCards.push({
          id,
          tag: (cardEl.tagName || '').toLowerCase(),
          cls: String(typeof cardEl.className === 'string' ? cardEl.className : '').trim().slice(0, 70),
          lineCount: cardLines.length,
          hasListing: Boolean(listingLink),
          hasToggle: Boolean(zone.toggleBtn),
          panelCount: zone.addressPanels.length,
          buyerName,
          city: addr.city || '',
          sku,
          isFull: addr.isFullAddress
        });
      }

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
        drawerDetectedId: drawerOrder ? drawerOrder.receiptId : null,
        sampleCards
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
      const txMatch = drText.match(/(?:Transaction\s*ID|İşlem\s*Kimliği)\s*:?\s*(\d{7,14})/i);
      const drNativeLines = extractNativeInnerTextLines(dr);
      const sku = extractSkuFromLines(drLines, drNativeLines);
      const variation = extractVariationFromLines(drLines, drNativeLines);
      const shipMethodLine = drLines.find((l) => /\b(?:Standard\s+Shipping|Standart\s+Kargo|Express\s+Shipping|Priority\s+Mail|Expedited\s+Shipping)\b/i.test(l));
      const shippingMethod = shipMethodLine ? shipMethodLine.replace(/\s*\([^)]*\)\s*$/, '').trim() : '';

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
        sku,
        variation,
        shippingMethod,
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
   * Ana iş parçacığını bloklamadan ve DataDome bot alarmı vermeden insani aralıklarla (65ms)
   * kapalı satır içi adres akordeonlarını güvenle açar.
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
      let btn = zone.toggleBtn;

      if (!btn && cardEl.querySelectorAll) {
        // Fallback: Kart içinde "Ship to" içeren başlığın yanındaki butonu veya chevron butonunu bul
        const following = Array.from(cardEl.querySelectorAll('button, [role="button"], a.wt-btn, a[class*="toggle"], summary'))
          .filter((b) => isFollowingInDom(idAnchor, b) && !isMenuOrActionBtn(b) && collectUniqueOrderIds(b).size === 0);
        btn = following.find((b) => {
          const p = b.parentElement ? extractInlineText(b.parentElement) : '';
          const hasChevron = /[▾▼⌄⌃^]/.test(b.textContent || '')
            || (typeof b.className === 'string' && /chevron|toggle|arrow/i.test(b.className))
            || Boolean(b.querySelector && b.querySelector('svg'));
          return /(?:\bShip\s*to\b|\bShipped\s*to\b|Şuraya\s*gönder|Gönderim)/i.test(p) || hasChevron;
        }) || null;
      }

      if (!btn) continue;

      const ariaExp = btn.getAttribute ? btn.getAttribute('aria-expanded') : null;
      if (ariaExp === 'false' || ariaExp === null) {
        toClick.push(btn);
      }
    }

    function safeTriggerClick(el) {
      if (!el) return;
      try {
        if (typeof MouseEvent !== 'undefined') {
          el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
          el.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true }));
        }
        el.click();
        if (typeof MouseEvent !== 'undefined') {
          el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        }
      } catch {
        try { el.click(); } catch {}
      }
    }

    for (let i = 0; i < toClick.length; i++) {
      safeTriggerClick(toClick[i]);
      if (i < toClick.length - 1) {
        await new Promise((r) => setTimeout(r, 65));
      }
    }

    // Açılan panellerin DOM'a yerleşmesini bekle (maks 1200ms)
    const start = Date.now();
    while (toClick.length > 0 && Date.now() - start < 1200) {
      const allReady = toClick.every((btn) => !btn.isConnected || btn.getAttribute('aria-expanded') === 'true');
      if (allReady) break;
      await new Promise((r) => setTimeout(r, 70));
    }
    if (toClick.length > 0) {
      await new Promise((r) => setTimeout(r, 120));
    }

    const extracted = extractOrdersFromDom(targetDoc);
    extracted.diagnostics.clickedInlineToggles = toClick.length;
    return extracted;
  }

  /**
   * Çekmeceleri tek tek elle açma zahmetini ortadan kaldıran güvenli ve insani DOM gezgini (v2.5.0):
   * 1. Önce satır içi akordeonları açar (expandAllInlineAccordions).
   * 2. Hâlâ tam adresi olmayan sipariş ID'lerini belirler (stale DOM nesneleri yerine salt kimlik listesi).
   * 3. Her eksik sipariş için canlı DOM'dan taze referansla sipariş açıcıyı bulur.
   *    Önceki çekmeceyi güvenle kapatıp yenisini açar, adresi yakalayıp kaydeder.
   * 4. İşlem bittiğinde çekmeceyi %100 kapatır ve tam listeyi döndürür.
   */
  async function walkAndCaptureOrderDrawers(doc) {
    const targetDoc = doc || (typeof document !== 'undefined' ? document : null);
    if (!targetDoc || !targetDoc.body) return extractOrdersFromDom(targetDoc);

    // 1. Önce satır içi açılabilenleri aç
    let currentResult = await expandAllInlineAccordions(targetDoc);
    const resolvedOrders = new Map();
    for (const ord of currentResult.orders) {
      resolvedOrders.set(ord.receiptId, ord);
    }
    if (currentResult.drawerOrder && currentResult.drawerOrder.isFullAddress) {
      resolvedOrders.set(currentResult.drawerOrder.receiptId, currentResult.drawerOrder);
    }

    // 2. Eksik sipariş ID'lerini topla (stale DOM düğümleri yerine salt kimlik listesi)
    const targetOrderIds = [];
    for (const ord of currentResult.orders) {
      if (!ord.isFullAddress) {
        targetOrderIds.push(ord.receiptId);
      }
    }

    if (targetOrderIds.length === 0) {
      return currentResult;
    }

    function safeTriggerClick(el) {
      if (!el) return;
      try {
        if (typeof MouseEvent !== 'undefined') {
          el.dispatchEvent(new MouseEvent('mousedown', { bubbles: true, cancelable: true }));
          el.dispatchEvent(new MouseEvent('mouseup', { bubbles: true, cancelable: true }));
        }
        el.click();
        if (typeof MouseEvent !== 'undefined') {
          el.dispatchEvent(new MouseEvent('click', { bubbles: true, cancelable: true }));
        }
      } catch {
        try { el.click(); } catch {}
      }
    }

    function closeAnyOpenDrawer() {
      try {
        // A. Etsy çekmece overlay kapatma butonları (Örn: gri daire içindeki 'X' butonu veya [aria-label="Close"])
        const closeBtns = targetDoc.querySelectorAll(
          '[data-wt-overlay-close], button.wt-overlay__close, button[class*="overlay__close" i], [class*="overlay-close" i], button[aria-label="Close" i], button[aria-label="Kapat" i], button[aria-label*="close" i], [data-testid*="close" i]'
        );
        for (const b of closeBtns) {
          if (!isElementHiddenCss(b, targetDoc)) {
            safeTriggerClick(b);
          }
        }

        // B. Etsy karartma arka planı (Backdrop click) - tıklandığında çekmeceyi %100 kapatır
        const backdrops = targetDoc.querySelectorAll('.wt-overlay__backdrop, [class*="overlay__backdrop" i]');
        for (const bd of backdrops) {
          if (bd.isConnected && !isElementHiddenCss(bd, targetDoc)) {
            safeTriggerClick(bd);
          }
        }

        // C. Klavye Escape tuşu (hem targetDoc hem window üzerinde tetikle)
        if (typeof KeyboardEvent !== 'undefined') {
          const escEvt = new KeyboardEvent('keydown', {
            key: 'Escape',
            code: 'Escape',
            keyCode: 27,
            which: 27,
            bubbles: true,
            cancelable: true
          });
          targetDoc.dispatchEvent(escEvt);
          if (typeof window !== 'undefined' && window.dispatchEvent) {
            window.dispatchEvent(escEvt);
          }
        }
      } catch {}
    }

    let drawersOpenedCount = 0;

    for (let i = 0; i < targetOrderIds.length; i++) {
      const id = targetOrderIds[i];

      // Eğer bu sipariş zaten başka bir adımda veya önceki çekmecede tam çözüldüyse atla
      const existing = resolvedOrders.get(id);
      if (existing && existing.isFullAddress) {
        continue;
      }

      // Canlı DOM'da bu siparişe ait tıklanabilir elementi TAZE olarak ara (Stale DOM Prevention!)
      let clickable = targetDoc.querySelector(`a[href*="order_id=${id}"], [data-order-id="${id}"] a, tr[data-order-id="${id}"]`);

      if (!clickable) {
        const freshCards = isolateOrderCards(targetDoc);
        const cardMatch = freshCards.find((c) => c.id === id);
        if (cardMatch) {
          clickable = (cardMatch.cardEl.querySelector && cardMatch.cardEl.querySelector(`a[href*="order_id="], a[href*="/sold/all"], [class*="order-link" i], [class*="receipt" i]`))
            || cardMatch.idAnchor;
        }
      }

      if (!clickable) {
        const { listAnchors } = findMinimalOrderAnchors(targetDoc);
        const anchorMatch = listAnchors.find((a) => a.id === id);
        if (anchorMatch) {
          clickable = (anchorMatch.el.closest ? anchorMatch.el.closest('a, button, tr, [class*="row" i]') : null) || anchorMatch.el;
        }
      }

      if (clickable) {
        // Eğer önceki bir siparişten çekmece açık kaldıysa önce hızlıca kapat (100ms)
        const currentOpen = extractOpenDrawerOrder(targetDoc);
        if (currentOpen && String(currentOpen.receiptId) !== String(id)) {
          closeAnyOpenDrawer();
          await new Promise((r) => setTimeout(r, 100));
        }

        safeTriggerClick(clickable);
        drawersOpenedCount++;

        // Çekmecenin render olmasını bekle (maks 600ms, 50ms aralıklarla)
        const start = Date.now();
        let captured = null;
        while (Date.now() - start < 600) {
          await new Promise((r) => setTimeout(r, 50));
          const drOrder = extractOpenDrawerOrder(targetDoc);
          if (drOrder && String(drOrder.receiptId) === String(id) && drOrder.isFullAddress) {
            captured = drOrder;
            break;
          }
        }

        if (captured) {
          resolvedOrders.set(id, captured);
          if (isExtensionContextValid() && typeof chrome !== 'undefined' && chrome.runtime) {
            chrome.runtime.sendMessage({
              action: 'CA_DRAWER_CAPTURED',
              order: captured
            }).catch(() => {});
          }
        }
      }

      if (i < targetOrderIds.length - 1) {
        await new Promise((r) => setTimeout(r, 120));
      }
    }

    // İşlem bittiğinde çekmeceyi kapat ve sayfayı temiz bırak
    await new Promise((r) => setTimeout(r, 100));
    closeAnyOpenDrawer();
    await new Promise((r) => setTimeout(r, 150));

    // Son durumu derle (Çözülen tam adresleri ana listeye enjekte et)
    const finalScan = extractOrdersFromDom(targetDoc);
    const finalOrders = finalScan.orders.map((ord) => {
      const rich = resolvedOrders.get(ord.receiptId);
      if (rich && rich.isFullAddress) {
        return rich;
      }
      return ord;
    });

    finalScan.orders = finalOrders;
    if (finalScan.diagnostics) {
      finalScan.diagnostics.drawersOpenedCount = drawersOpenedCount;
      finalScan.diagnostics.autoWalkCompleted = true;
    }

    return finalScan;
  }

  // Tarayıcı Content Script mesaj dinleyicisi ve kullanıcı kontrollü pasif Drawer gözlemcisi
  if (isExtensionContextValid() && typeof document !== 'undefined' && document.body) {
    let lastDrawerId = '';
    let mutationDebounceTimer = null;

    const observer = new MutationObserver(() => {
      if (!isExtensionContextValid()) {
        try { observer.disconnect(); } catch {}
        return;
      }
      if (detectPageType() !== 'orders') return;

      // Hızlı ön kontrol: Ekranda çekmece/dialog/aside kapsayıcısı yoksa ASLA tarama yapma! (%0 CPU & sıfır gereksiz işlem)
      if (!document.querySelector('aside, [role="dialog"], [aria-modal="true"], .wt-overlay, [class*="drawer" i]')) {
        return;
      }

      if (mutationDebounceTimer) clearTimeout(mutationDebounceTimer);
      mutationDebounceTimer = setTimeout(() => {
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
      }, 400);
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
          const resolved = await walkAndCaptureOrderDrawers(document);
          sendResponse({
            ok: true,
            version: AGENT_VERSION,
            orders: resolved.orders,
            drawerOrder: resolved.drawerOrder,
            diagnostics: resolved.diagnostics
          });
        })();
        return true;
      }

      if (message.action === 'CA_EXTRACT_LISTING_VARIATIONS') {
        (async () => {
          const selects = Array.from(document.querySelectorAll('select[id^="variation-selector-"]'));
          const groups = selects.map((sel, idx) => {
            const lbl = document.querySelector(`label[for="${sel.id}"]`);
            const name = lbl ? (lbl.innerText || lbl.textContent || '').trim() : `Variation ${idx + 1}`;
            const options = Array.from(sel.options || []).map((o) => (o.text || '').trim()).filter(Boolean);
            return { name, options };
          }).filter((g) => g.options.length > 0);

          let basePrice = 0;
          const ldEl = document.querySelector('script[type="application/ld+json"]');
          if (ldEl && ldEl.textContent) {
            const m = ldEl.textContent.match(/"(?:lowPrice|price)"\s*:\s*"?(\d+(?:[.,]\d{1,2})?)"?/i);
            if (m) basePrice = parseFloat(m[1].replace(',', '.')) || 0;
          }

          sendResponse({
            ok: true,
            version: AGENT_VERSION,
            url: location.href,
            groups,
            basePrice
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
    expandAllInlineAccordions,
    walkAndCaptureOrderDrawers
  });

  root.EtsyCopilotContentAgent = exported;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = exported;
  }
})(typeof globalThis !== 'undefined' ? globalThis : window);
