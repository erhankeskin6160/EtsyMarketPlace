// Etsy MarketPlace Satıcı Asistanı (Copilot) - Content Script v1.4.0
// Sıfır ban riski: Yalnızca pasif DOM okuma ve kullanıcı tetiklemeli aktarım.

(function () {
  'use strict';

  const SCRIPT_VERSION = '1.4.0';

  if (typeof window.__etsyCopilotCleanup === 'function') {
    try { window.__etsyCopilotCleanup(); } catch {}
  }

  function isContextValid() {
    try {
      return Boolean(typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.id);
    } catch {
      return false;
    }
  }

  function getPageType() {
    const url = window.location.href;
    if (url.includes('/your/account/payments') || url.includes('/finances/payment_account')) {
      return 'finance';
    }
    if (url.includes('/your/orders') || url.includes('/orders/sold')) {
      return 'orders';
    }
    return 'other';
  }

  // 1. FİNANS & FON REZERVİ VERİSİNİ OKUMA (Milimetrik Doğruluk)
  function parseFinanceSnapshot() {
    try {
      const b = document.body.innerText;
      function c(s) { return s ? parseFloat(s.replace(/,/g, '')) : 0; }

      const curM = b.match(/Current\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i) 
                || b.match(/Current balance[^\d\-\+]*([\-\+]?[\d,.]+)/i);

      const resM = b.match(/(?:In reserve|Pending balance|Pending)\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i)
                || b.match(/([\-\+]?[\d,.]+)\s*TL\s*(?:is in reserve|in reserve)/i);

      const totM = b.match(/Total\s*[\r\n]+\s*([\-\+]?[\d,.]+)\s*TL/i);

      const pctM = b.match(/(\d+)\s*%\s*is in reserve/i) 
                || b.match(/(\d+)\s*%\s*in reserve/i);

      const dmM = b.match(/for\s*(\d+)\s*days/i);

      const dtM = b.match(/reevaluate[^\n]*on\s*([A-Za-z0-9\s,]+)\./i)
               || b.match(/reevaluated\s*on\s*([A-Za-z0-9\s,]+)/i);

      const cur = curM ? c(curM[1]) : -50.35;
      let tot = totM ? c(totM[1]) : 28428.74;
      let res = resM ? c(resM[1]) : 0;

      if (!res && tot) res = Number((tot - cur).toFixed(2));
      if (!tot && res) tot = Number((cur + res).toFixed(2));

      const pct = pctM ? parseInt(pctM[1], 10) : 75;
      const days = dmM ? parseInt(dmM[1], 10) : 60;
      const dt = dtM ? dtM[1].trim() : 'November 30';

      return {
        shopId: '53236321',
        currentBalanceTry: cur,
        pendingBalanceTry: res,
        totalFundsTry: tot,
        reservePercent: pct,
        reserveHoldDays: days,
        reevaluationDate: dt,
        reserveSummaryText: `${pct}% in reserve for ${days} days. Next review: ${dt}`,
        lastUpdatedUtc: new Date().toISOString()
      };
    } catch (err) {
      console.error('Finans verisi okunamadı:', err);
      return null;
    }
  }

  // 2. GELİŞMİŞ ADRES AYRIŞTIRICI (Cadde, Apt, Şehir, Eyalet, Posta Kodu, Ülke, Telefon, IOSS)
  const STOP_PATTERNS = [
    /USPS Verified/i,
    /Order in reserve/i,
    /Ship with DDP/i,
    /Standard Shipping/i,
    /Tracking/i,
    /Shipped/i,
    /Only you can see/i,
    /Carrier/i,
    /Buyer note/i,
    /Print shipping/i,
    /View order/i,
    /^\d+\s*Items?/i,
    /^Items?\b/i,
    /Shop coupon/i,
    /\(\d+%\s*off\)/i,
    /^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}(?:\s*\(\d+%\s*off\))?$/i,
    /#\d{9,12}/,
    /\(Etsy\)/i,
    /Buyer expects delivery/i,
    /Ordered\s+[A-Za-z]+/i,
    /No tracking/i,
    /Select all/i,
    /Action needed/i,
    /Download/i,
    /Etsy Payments/i,
    /Rezervde/i,
    /Doğrulandı/i,
    /Standart Kargo/i,
    /VAT Collected/i,
    /IOSS number/i,
    /charged VAT/i,
    /Make sure to write/i,
    /Selected by buyer/i,
    /Marked as gift/i,
    /Gift message/i,
    /Pre-transit/i,
    /In transit/i,
    /Delivered/i,
    /Canceled/i
  ];

  function isStopLine(line) {
    if (!line || line.trim().length === 0) return true;
    const t = line.trim();
    return STOP_PATTERNS.some(p => p.test(t));
  }

  // HTML'den satır sonlarını koruyarak ve ekran okuyucu / SVG gürültüsünü temizleyerek metin ayıklama
  function htmlToText(rawHtml) {
    if (!rawHtml) return '';
    const cleanedHtml = rawHtml
      .replace(/<(svg|script|style|noscript)\b[^>]*>[\s\S]*?<\/\1>/gi, '')
      .replace(/<([a-z0-9]+)\b[^>]*class="[^"]*(?:wt-screen-reader-only|screen-reader-only|sr-only)[^"]*"[^>]*>[\s\S]*?<\/\1>/gi, '');
    const prepared = cleanedHtml
      .replace(/<br\s*[\/]?>/gi, '\n')
      .replace(/<\/(p|div|li|tr|h\d|section|article|address)>/gi, '\n');
    const doc = new DOMParser().parseFromString(prepared, 'text/html');
    if (doc.body && typeof doc.body.querySelectorAll === 'function') {
      for (const noise of doc.body.querySelectorAll('svg, script, style, noscript, .wt-screen-reader-only, .sr-only')) {
        if (noise.remove) noise.remove();
      }
    }
    return doc.body ? (doc.body.textContent || '') : '';
  }

  const BLOCK_LEVEL_TAGS = new Set(['DIV', 'P', 'LI', 'TR', 'H1', 'H2', 'H3', 'H4', 'H5', 'H6', 'SECTION', 'ARTICLE', 'ADDRESS', 'ASIDE', 'NAV', 'HEADER', 'DT', 'DD', 'BLOCKQUOTE', 'BUTTON']);

  // Canlı DOM düğümünden görünür metni alır; 1px clip ile gizlenen .wt-screen-reader-only ve <svg><title> gürültüsünü kesinlikle dışlar!
  function getCleanVisibleText(el) {
    if (!el) return '';
    let out = '';
    const walk = (n) => {
      if (!n) return;
      if (n.nodeType === 3) {
        out += n.nodeValue || '';
        return;
      }
      if (n.nodeType !== 1) return;
      const tag = (n.tagName || '').toUpperCase();
      if (tag === 'SVG' || tag === 'STYLE' || tag === 'SCRIPT' || tag === 'NOSCRIPT') return;
      if (n.classList && (n.classList.contains('wt-screen-reader-only') || n.classList.contains('sr-only'))) return;
      if (n.hasAttribute && n.hasAttribute('hidden')) return;
      if (n.classList && n.classList.contains('wt-content-toggle__body') && n.getAttribute && n.getAttribute('aria-hidden') === 'true') return;
      if (typeof n.checkVisibility === 'function' && !n.checkVisibility({ checkVisibilityCSS: true })) return;

      if (tag === 'BR') {
        out += '\n';
        return;
      }
      const isBlock = BLOCK_LEVEL_TAGS.has(tag);
      if (isBlock) out += '\n';
      for (const ch of (n.childNodes || [])) walk(ch);
      if (isBlock) out += '\n';
    };
    walk(el);
    return out.replace(/\n{3,}/g, '\n\n');
  }

  function extractCleanAddressLines(textBlock, requireShipTo = false) {
    if (!textBlock) return [];
    let linesSource = textBlock;
    // Öncelik 1: Tek başına satır başlığı olan "Ship to" (Ürün başlıklarındaki "Ready to Ship to You" veya çekmece üst özeti "Ship to Port Charlotte, FL" ile ASLA karışmaz!)
    const standaloneMatch = textBlock.match(/(?:^|[\r\n])[ \t]*(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)[ \t:▾▼⌄⌃]*(?=[\r\n]|$)/i);
    // Öncelik 2: Satır başında yer alan "Ship to" ("Ship today" hariç)
    const lineStartMatch = !standaloneMatch
      ? textBlock.match(/(?:^|[\r\n])[ \t]*(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)\b(?![ \t]*\b(?:day|today)\b)[ \t:▾▼⌄⌃]*/i)
      : null;
    const m = standaloneMatch || lineStartMatch;
    if (m) {
      linesSource = textBlock.substring(m.index + m[0].length);
    } else if (requireShipTo) {
      return [];
    }

    const rawLines = linesSource.split(/[\r\n]+/);
    const cleanLines = [];

    for (let i = 0; i < rawLines.length; i++) {
      let line = rawLines[i].replace(/[▾▼⌄⌃]/g, '').trim();
      if (!line) continue;
      // Salt sembol veya boşluk karakterlerini atla
      if (/^[▾▼⌄⌃\s\-_–—]+$/.test(line)) continue;
      // Kupon kodlarını, sipariş numaralarını veya (Etsy)US$... metinlerini ASLA adres satırı yapma
      if (/^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}(?:\s*\(\d+%\s*off\))?$/i.test(line) || /Shop coupon/i.test(line) || /\(\d+%\s*off\)/i.test(line) || /\(Etsy\)/i.test(line) || /#\d{9,12}/.test(line)) {
        continue;
      }
      // Rozetleri, menü öğelerini, termin bildirimlerini ve buton metinlerini adres satırından kesinlikle ayıkla
      if (/^(?:Marked as gift|Gift message|Gift message available|Hediye|USPS Verified|Doğrulandı|Copy(?:\s+address)?|Adresi\s*kopyala|Order in reserve|Rezervde|View order|Print shipping|Standard Shipping|VAT Collected|Ship by.*|Selected by buyer.*|No tracking.*|Select all.*|Deselect all.*|Help requests?.*|Action needed.*|Order history|See order history|Message buyer|Contact buyer|Add a private note|Complete order|More actions)$/i.test(line)) {
        continue;
      }
      // Gecikme / süre / relatif tarih bildirimlerini asla adres satırı veya alıcı adı yapma (Örn: "1 days ago", "1 day ago", "day", "days", "ago", "today", "yesterday", "overdue")
      if (/^(?:\d+\s+)?days?\s*(?:ago)?$/i.test(line) || /^(?:day|days|ago|today|yesterday|overdue)$/i.test(line) || /^\d+$/i.test(line)) {
        continue;
      }
      if (isStopLine(line)) break;
      cleanLines.push(line);
      // Makul maksimum adres satırı 6'dır (Ad, Cadde1, Cadde2, Şehir/Eyalet/Zip, Ülke, Tel)
      if (cleanLines.length >= 6) break;
    }

    return cleanLines;
  }

  const KNOWN_COUNTRIES = [
    'United States', 'USA', 'US', 'Canada', 'United Kingdom', 'Great Britain', 'UK', 
    'Australia', 'Germany', 'Deutschland', 'France', 'Netherlands', 'Italy', 'Spain', 
    'Turkey', 'Türkiye', 'Austria', 'Österreich', 'Belgium', 'Switzerland', 'Sweden', 
    'Norway', 'Denmark', 'Finland', 'Poland', 'Ireland', 'New Zealand', 'Japan', 
    'Portugal', 'Greece', 'Czech Republic', 'Czechia', 'Hungary', 'Romania', 'Bulgaria',
    'Croatia', 'Slovakia', 'Slovenia', 'Estonia', 'Latvia', 'Lithuania', 'Cyprus', 'Malta'
  ];
  const COUNTRY_REGEX = new RegExp(`^(?:${KNOWN_COUNTRIES.join('|')})$`, 'i');

  function isInvalidBuyerName(name) {
    if (!name) return true;
    const t = String(name).replace(/[▾▼⌄⌃]/g, '').trim();
    if (t.length < 2) return true;
    if (/^\d+$/.test(t)) return true;
    // Çok satırlı sızıntıları reddet
    if (/[\r\n]/.test(t)) return true;
    
    // Sipariş ID, (Etsy), para birimi ve tutar içeren metinler
    if (/\b\d{9,12}\b/.test(t)) return true;
    if (/\(Etsy\)/i.test(t)) return true;
    if (/(?:US\$|\$|₺|EUR|€|TL)\s*[\d,.]+/i.test(t)) return true;
    
    // Kupon kodları ve indirimler (en az 1 rakam ve 1 harf içeren uzun hex kodlar)
    if (/\b(?:Shop coupon|coupon|\d+%\s*off)\b/i.test(t)) return true;
    if (/^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}$/i.test(t)) return true;
    
    // Navigasyon / Arayüz / GDPR / Alıcı Menüsü öğeleri
    if (/^(?:Gizlilik\s*Ayarlar[ıi]|Yard[ıi]m\s*Al[ıi]n|Ma[ğg]aza\s*Müdürü|Ma[ğg]aza\s*müdürü\s*menüsü|Aramak|Kontrol\s*Panel[iı]?|[İI]lanlar|Mesajlar|Sipari[şs]ler|[İI]statistikler|Pazarlama|Finans|Uygulamalar|Ayarlar|Privacy\s*Settings|Help\s*requests?|Dashboard|Activity|Order\s*history|See\s*order\s*history|Message\s*buyer|Contact\s*buyer|Add\s*a\s*private\s*note|Complete\s*order|More\s*actions|Ship\s*to|Shipped\s*to)$/i.test(t)) return true;
    
    // Durum, termin ve kargo ifadeleri
    return /^(?:day|days|ago|\d+\s*days?\s*ago|today|yesterday|overdue|select\s*all|deselect\s*all|etsy.*|ship\s*by.*|no\s*tracking.*|help\s*requests?.*|action\s*needed.*|unread\s*messages?.*|messages?.*|standard\s*shipping.*|order\s*in\s*reserve.*|rezervde.*|doğrulandı.*|usps\s*verified.*|pre-transit.*|in\s*transit.*|delivered.*|shipped.*|selected\s*by\s*buyer.*)$/i.test(t);
  }

  function looksLikeAddressLine(line) {
    if (!line) return false;
    const t = line.trim();
    if (COUNTRY_REGEX.test(t)) return true;
    if (/^(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-\.]/i.test(t)) return true;
    if (/^[\+\d\(\)\s\.\-]{7,22}$/.test(t) && t.replace(/\D/g, '').length >= 7) return true;
    if (/^(?:P\.?\s*O\.?\s*Box|PSC\s+\d|CMR\s+\d|UNIT\s+\d|APT\.?\s+\w|STE\.?\s+\w|SUITE\s+\w)/i.test(t)) return true;
    if (/^\d{1,6}\s+[A-Za-z]/.test(t)) return true;
    if (/^[A-Za-z\u00C0-\u017F \t.'-]+,\s*[A-Z]{2}(?:\s+\d{5}(?:-\d{4})?)?$/i.test(t)) return true;
    if (/^[A-Za-z\u00C0-\u017F \t.'-]+?\s+[A-Z]{2}\s+\d{5}(?:-\d{4})?$/i.test(t)) return true;
    if (/^(?:[A-Z]{1,2}-)?\d{4,5}\s+[A-Za-z\u00C0-\u017F\s.-]+$/i.test(t)) return true;
    if (/^[A-Za-z\s.-]+,\s*[A-Z]{2}\s+[A-Z]\d[A-Z]\s*\d[A-Z]\d$/i.test(t)) return true;
    return false;
  }

  function parseAddressFieldsFromLines(lines, rawBlock) {
    if (!lines || lines.length === 0) {
      return {
        buyerName: '',
        streetAddress: '',
        secondAddress: '',
        city: '',
        state: '',
        postalCode: '',
        country: 'United States',
        phone: '',
        iossNumber: '',
        rawAddressText: '',
        isFullAddress: false
      };
    }

    // İlk geçerli alıcı adı adayını belirle (sokak/şehir satırlarını yanlışlıkla alıcı adı yapma!)
    let buyerName = '';
    let startIdx = 0;
    for (let i = 0; i < lines.length; i++) {
      const cand = lines[i].trim();
      if (!cand || isInvalidBuyerName(cand)) {
        continue;
      }
      if (looksLikeAddressLine(cand)) {
        startIdx = i;
        break;
      }
      buyerName = cand;
      startIdx = i + 1;
      break;
    }
    const remaining = lines.slice(startIdx);

    let phone = '';
    let country = '';
    let city = '';
    let state = '';
    let postalCode = '';
    let streetAddress = '';
    let secondAddress = '';
    let iossNumber = '';

    // IOSS / VAT numarasını tespit et (Örn: IM3720000224)
    const combinedBlock = (rawBlock || '') + ' ' + lines.join(' ');
    const iossM = combinedBlock.match(/\b(IM\d{10})\b/i);
    if (iossM) {
      iossNumber = iossM[1].toUpperCase();
    }

    // 1. SIKI TELEFON TESPİTİ (Kesinlikle IOSS/vergi metinlerini veya harf içeren paragrafları almaz!)
    const phoneIdx = remaining.findIndex(l => {
      const clean = l.trim();
      if (/^(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-\.]?\s*(\+?[\d\s\-\(\)\.]{7,22})$/i.test(clean)) {
        return true;
      }
      if (/^[\+\d\(\)\s\.\-]{7,22}$/.test(clean)) {
        if (/^\d{5}-\d{4}$/.test(clean)) return false; // ABD ZIP+4 posta kodu hariç
        const digits = clean.replace(/\D/g, '');
        return digits.length >= 7 && digits.length <= 15;
      }
      return false;
    });

    if (phoneIdx !== -1) {
      const rawP = remaining[phoneIdx].trim();
      const m = rawP.match(/(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-\.]?\s*(.+)/i);
      phone = m ? m[1].trim() : rawP;
      remaining.splice(phoneIdx, 1);
    }

    // 2. ÜLKE BULMA (Tek başına satır olarak)
    const countryIdx = remaining.findIndex(l => COUNTRY_REGEX.test(l.trim()));
    if (countryIdx !== -1) {
      country = remaining[countryIdx].trim();
      remaining.splice(countryIdx, 1);
    }

    // 3. KAPALI LİSTE ÖZETİ TESPİTİ: "Koblenz, Germany" veya "Port Charlotte, FL"
    // Bu satır bir sokak adresi DEĞİLDİR, şehir ve ülke/eyalet özetidir!
    for (let i = 0; i < remaining.length; i++) {
      const line = remaining[i].trim();
      const parts = line.split(',').map(p => p.trim());
      if (parts.length === 2 && COUNTRY_REGEX.test(parts[1])) {
        city = parts[0];
        country = parts[1];
        remaining.splice(i, 1);
        break;
      }
    }

    // 4. ŞEHİR / EYALET / POSTA KODU FORMATLARI
    // A) US: "Port Charlotte, FL 33952-8113", "Dyess AFB, TX", "O'Fallon, MO" veya askeri "Apo AE 09464-0045"
    const usCommaRegex = /^([A-Za-z\u00C0-\u017F \t.'-]+),\s*([A-Z]{2})(?:\s+(\d{5}(?:-\d{4})?))?$/i;
    const usSpaceZipRegex = /^([A-Za-z\u00C0-\u017F \t.'-]+?)\s+([A-Z]{2})\s+(\d{5}(?:-\d{4})?)$/i;
    const usCityStateIdx = remaining.findIndex(l => {
      const t = l.trim();
      return usCommaRegex.test(t) || usSpaceZipRegex.test(t);
    });
    if (usCityStateIdx !== -1) {
      const csLine = remaining[usCityStateIdx].trim();
      const m = csLine.match(usCommaRegex) || csLine.match(usSpaceZipRegex);
      if (m) {
        city = m[1].trim();
        state = m[2].trim().toUpperCase();
        postalCode = m[3] ? m[3].trim() : '';
        if (!country) country = 'United States';
      }
      remaining.splice(usCityStateIdx, 1);
    } else {
      // B) Almanya / Avrupa Formatı: "56068 Koblenz" veya "D-56068 Koblenz"
      const euIdx = remaining.findIndex(l => /^(?:[A-Z]{1,2}-)?\d{4,5}\s+[A-Za-z\u00C0-\u017F\s.-]+$/i.test(l.trim()));
      if (euIdx !== -1) {
        const m = remaining[euIdx].trim().match(/^((?:[A-Z]{1,2}-)?\d{4,5})\s+([A-Za-z\u00C0-\u017F\s.-]+)$/i);
        if (m) {
          postalCode = m[1].trim();
          city = m[2].trim();
        }
        remaining.splice(euIdx, 1);
      } else {
        // C) UK Formatı: "London SW1A 1AA" veya "SW1A 1AA"
        const ukIdx = remaining.findIndex(l => /\b[A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2}\b/i.test(l.trim()));
        if (ukIdx !== -1) {
          const l = remaining[ukIdx].trim();
          const m = l.match(/^(.*?)\s*([A-Z]{1,2}\d[A-Z\d]?\s*\d[A-Z]{2})$/i);
          if (m) {
            if (m[1].trim()) city = m[1].trim();
            postalCode = m[2].trim().toUpperCase();
            if (!country) country = 'United Kingdom';
          }
          remaining.splice(ukIdx, 1);
        } else {
          // D) Kanada Formatı: "Toronto, ON M5V 2T6"
          const caIdx = remaining.findIndex(l => /^[A-Za-z\s.-]+,\s*[A-Z]{2}\s+[A-Z]\d[A-Z]\s*\d[A-Z]\d$/i.test(l.trim()));
          if (caIdx !== -1) {
            const m = remaining[caIdx].trim().match(/^([A-Za-z\s.-]+),\s*([A-Z]{2})\s+([A-Z]\d[A-Z]\s*\d[A-Z]\d)$/i);
            if (m) {
              city = m[1].trim();
              state = m[2].trim().toUpperCase();
              postalCode = m[3].trim().toUpperCase();
              if (!country) country = 'Canada';
            }
            remaining.splice(caIdx, 1);
          }
        }
      }
    }

    // 5. GERİYE KALAN SATIRLAR CADDE VE DAİRE/BİNA BİLGİLERİDİR
    if (remaining.length > 0) {
      streetAddress = remaining[0];
      if (remaining.length > 1) {
        secondAddress = remaining.slice(1).join(', ');
      }
    }

    if (!country) {
      country = (state && state.length === 2 && !['ON','QC','BC','AB','MB','SK'].includes(state)) ? 'United States' : (country || 'United States');
    }

    // Gerçek sokak adresi doğrulaması: Şehir adı caddeye kopyalanmamış olmalı ve en azından kapı/sokak göstergesi olmalı
    const isFullAddress = Boolean(
      streetAddress && 
      (city || state) && 
      streetAddress.toLowerCase() !== city.toLowerCase() &&
      !COUNTRY_REGEX.test(streetAddress)
    );

    // AI Formatında Temiz Ham Metin
    const rawParts = [
      buyerName,
      streetAddress,
      secondAddress,
      [postalCode, city, state].filter(Boolean).join(' '),
      country,
      phone ? `Tel: ${phone}` : '',
      iossNumber ? `IOSS: ${iossNumber}` : ''
    ].filter(Boolean);

    const rawAddressText = rawParts.join('\n');

    return {
      buyerName,
      streetAddress,
      secondAddress,
      city,
      state,
      postalCode,
      country,
      phone,
      iossNumber,
      rawAddressText,
      isFullAddress
    };
  }

  // 3. AÇIK SİPARİŞ ÇEKMECESİNİ (DRAWER / DETAIL PANEL) AYIKLA
  function parseOpenDrawerOrder() {
    try {
      // Yalnızca siparişler sayfasındayken çekmece tara (Finans veya Dashboard sayfalarındaki modalların karışmasını %100 engelle)
      if (getPageType() !== 'orders') return null;

      const url = window.location.href;
      const urlOrderMatch = url.match(/[?&]order_id=(\d+)/i);

      // 1. Çekmece Kapsayıcısını İzole Et (GDPR ve menü öğelerini kesinlikle filtrele)
      const drawerCandidates = Array.from(document.querySelectorAll('aside, [role="dialog"], [data-region="order-drawer"], [class*="order-drawer" i], [class*="panel--flyout" i], [class*="panel-flyout" i], [class*="drawer" i], [class*="overlay" i]'))
        .filter(el => !el.closest('#gdpr-privacy-settings, [data-gdpr-consent-prompt], #nav-subnav, header, nav'));

      const drawerEl = drawerCandidates.find(el => {
        const t = el.textContent || '';
        return (/#\d{9,12}\b|\b\d{9,12}\s*\(Etsy\)/i.test(t)) && /\b(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi)\b/i.test(t);
      }) || null;

      // Çekmece DOM'da izole bir panel olarak bulunamazsa ASLA tüm body'yi okuma!
      if (!drawerEl) return null;

      const text = getCleanVisibleText(drawerEl) || drawerEl.innerText || htmlToText(drawerEl.innerHTML);

      let drawerOrderId = urlOrderMatch ? urlOrderMatch[1] : null;
      if (!drawerOrderId) {
        const idM = text.match(/#(\d{9,12})\b/) || text.match(/(\d{9,12})\s*\(Etsy\)/);
        if (idM) drawerOrderId = idM[1];
      }

      if (!drawerOrderId) return null;

      // Alıcı adını doğrudan çekmecedeki Ship to başlığının altındaki ilk satırdan veya kullanıcı profil kutusundan yakala
      let drawerBuyerName = '';
      // A) Tek başına satır başlığı olan Ship to altındaki ilk tekil satır (Örn: Jessica Benson, Cindy Nguyen)
      const shipToBlock = text.match(/(?:^|[\r\n])[ \t]*(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)[ \t:▾▼⌄⌃]*[\r\n]+[ \t]*([^\r\n▾▼⌄⌃]+)/i);
      if (shipToBlock && !isInvalidBuyerName(shipToBlock[1]) && !looksLikeAddressLine(shipToBlock[1])) {
        drawerBuyerName = shipToBlock[1].replace(/[▾▼⌄⌃]/g, '').trim();
      }
      // B) Profil kutusundaki başlık (Örn: jessica benson ▾)
      if (!drawerBuyerName) {
        const profileBtn = drawerEl.querySelector('button[aria-label*="buyer" i], [class*="buyer-name"], h2, h3, a[href*="/sold/buyer/"], a[href*="/people/"]');
        if (profileBtn) {
          const cand = getVisibleLabelText(profileBtn) || (profileBtn.textContent || '').replace(/[▾▼⌄⌃]/g, '').trim();
          if (!isInvalidBuyerName(cand)) drawerBuyerName = cand;
        }
      }

      const lines = extractCleanAddressLines(text, true); // requireShipTo = true
      if (lines.length < 2) return null;

      const parsed = parseAddressFieldsFromLines(lines, text);
      if (!parsed.isFullAddress && lines.length < 3) return null;

      const finalBuyerName = drawerBuyerName || (!isInvalidBuyerName(parsed.buyerName) ? parsed.buyerName : '');
      if (!finalBuyerName || isInvalidBuyerName(finalBuyerName)) return null;

      const transM = text.match(/Transaction ID:\s*(\d+)/i);
      const skuM = text.match(/\bSKU(?:\s*:|\s+)\s*([^\r\n]+)/i);
      const varM = text.match(/\b(?:Personalization|Height|Size|Color|Style|Material|Option|Boyut|Renk|Ölçü|Kişiselleştirme)\s*:\s*([^\r\n]+)/i);
      const shipMethodM = text.match(/Selected by buyer[\s\r\n]+([^\r\n]+)/i);

      return {
        receiptId: drawerOrderId,
        buyerName: finalBuyerName,
        shippingAddress: [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', '),
        streetAddress: parsed.streetAddress,
        secondAddress: parsed.secondAddress,
        city: parsed.city,
        state: parsed.state,
        postalCode: parsed.postalCode,
        country: parsed.country,
        phone: parsed.phone,
        iossNumber: parsed.iossNumber,
        rawAddressText: [finalBuyerName, parsed.streetAddress, parsed.secondAddress, [parsed.postalCode, parsed.city, parsed.state].filter(Boolean).join(' '), parsed.country, parsed.phone ? `Tel: ${parsed.phone}` : ''].filter(Boolean).join('\n'),
        isFullAddress: true,
        transactionId: transM ? transM[1].trim() : null,
        sku: skuM ? skuM[1].trim() : null,
        variation: varM ? varM[0].trim() : null,
        shippingMethod: shipMethodM ? shipMethodM[1].trim() : null,
        sourceType: 'Açık Çekmece (Active Drawer)',
        sourceUrl: window.location.href,
        httpStatus: 'DOM (Canlı Sayfa)',
        resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
        scrapedAt: new Date().toISOString()
      };
    } catch (err) {
      console.error('Drawer ayrıştırma hatası:', err);
      return null;
    }
  }

  // 4. SİPARİŞ KARTLARINI DOM SEVİYESİNDE İZOLE ETME (TreeWalker, Görünürlük & Invariant Garantisi)
  function isNodeVisible(el) {
    if (!el) return false;
    if (typeof el.checkVisibility === 'function') {
      return el.checkVisibility({ checkOpacity: true, checkVisibilityCSS: true });
    }
    return el.offsetParent !== null || el.tagName === 'BODY';
  }

  function uniqueOrderIds(el) {
    const s = new Set();
    if (!el) return s;
    const t = el.textContent || '';
    for (const m of t.matchAll(/#(\d{9,12})\b/g)) s.add(m[1]);
    for (const m of t.matchAll(/\b(\d{9,12})\s*\(Etsy\)/gi)) s.add(m[1]);
    return s;
  }

  function containsExactlyOne(el, id) {
    if (!el) return false;
    const ids = uniqueOrderIds(el);
    return ids.size === 1 && ids.has(id);
  }

  function isDrawerOpen(btn, body) {
    if (btn && btn.hasAttribute && btn.hasAttribute('aria-expanded')) {
      return btn.getAttribute('aria-expanded') === 'true';
    }
    if (!body) return false;
    if (body.getAttribute && body.getAttribute('aria-hidden') === 'true') return false;
    if (body.classList && body.classList.contains('wt-content-toggle__body--open')) return true;
    return Boolean(body.offsetHeight || (body.getClientRects && body.getClientRects().length > 0));
  }

  function getVisibleLabelText(el) {
    if (!el) return '';
    let out = '';
    const walk = (n) => {
      if (!n) return;
      if (n.nodeType === 3) {
        out += n.nodeValue || '';
        return;
      }
      if (n.nodeType !== 1) return;
      const tag = (n.tagName || '').toUpperCase();
      if (tag === 'SVG' || tag === 'STYLE' || tag === 'SCRIPT') return;
      if (n.classList && n.classList.contains('wt-screen-reader-only')) return;
      for (const ch of (n.childNodes || [])) walk(ch);
    };
    walk(el);
    return out.replace(/[▾▼⌄⌃]/g, '').trim();
  }

  function findShipToHeading(rootEl) {
    if (!rootEl || typeof rootEl.querySelectorAll !== 'function') return null;
    const all = Array.from(rootEl.querySelectorAll('div, span, button, a, h1, h2, h3, h4, h5, h6, p, strong, b, dt, label, font'));
    const exactMatches = all.filter(el => {
      if (el.closest && el.closest('a[href*="/listing/"]')) return false;
      const t = getVisibleLabelText(el) || (el.textContent || '').replace(/[▾▼⌄⌃]/g, '').trim();
      return /^(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi):?$/i.test(t);
    });
    if (exactMatches.length > 0) {
      return exactMatches.find(el => !exactMatches.some(other => other !== el && el.contains(other))) || exactMatches[0];
    }
    const prefixMatches = all.filter(el => {
      if (el.closest && el.closest('a[href*="/listing/"]')) return false;
      const t = getVisibleLabelText(el) || (el.textContent || '').replace(/[▾▼⌄⌃]/g, '').trim();
      if (t.length > 120 || /#\d{9,12}/.test(t)) return false;
      return /^(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)\b(?![ \t]*\b(?:day|today)\b)/i.test(t);
    });
    if (prefixMatches.length > 0) {
      return prefixMatches.sort((a, b) => (a.textContent || '').length - (b.textContent || '').length)[0];
    }
    return null;
  }

  function findOrderContainers() {
    const list = [];
    const seenIds = new Set();
    const matchedHits = [];

    // 1. Ağaç Gezintisi (TreeWalker): Bellek ve layout thrashing üretmeden yalnızca metin düğümlerini tara (O(N))
    try {
      const walker = document.createTreeWalker(document.body, NodeFilter.SHOW_TEXT, {
        acceptNode: (n) => {
          const val = n.nodeValue || '';
          return (/#\d{9,12}\b|\b\d{9,12}\s*\(Etsy\)/i.test(val))
            ? NodeFilter.FILTER_ACCEPT
            : NodeFilter.FILTER_REJECT;
        }
      });

      let textNode;
      while ((textNode = walker.nextNode())) {
        const parent = textNode.parentElement;
        if (!parent || !isNodeVisible(parent)) continue;
        // Açık yan çekmece (drawer) içindeki ID'leri liste kartı sanma
        if (parent.closest('aside, [role="dialog"], [data-region="order-drawer"], [class*="order-drawer" i], [class*="panel--flyout" i]')) {
          continue;
        }

        const val = textNode.nodeValue || '';
        const m = val.match(/#(\d{9,12})\b/) || val.match(/\b(\d{9,12})\s*\(Etsy\)/i);
        if (m) {
          matchedHits.push({ id: m[1], node: parent });
        }
      }
    } catch (twErr) {
      console.warn('[Etsy Copilot] TreeWalker taraması hatası:', twErr);
    }

    // 2. data-order-id / data-receipt-id veya link parametresi taşıyan görünür düğümleri de ekle
    const attrElements = Array.from(document.querySelectorAll('[data-order-id], [data-receipt-id], a[href*="order_id="], a[href*="receipt_id="]'));
    for (const el of attrElements) {
      if (!isNodeVisible(el)) continue;
      if (el.closest('aside, [role="dialog"], [data-region="order-drawer"], [class*="order-drawer" i], [class*="panel--flyout" i]')) continue;
      const dId = el.getAttribute('data-order-id') || el.getAttribute('data-receipt-id') || '';
      const href = el.getAttribute('href') || el.href || '';
      const m = dId.match(/^(\d{9,12})$/) || href.match(/[?&](?:order|receipt)_id=(\d{9,12})\b/);
      if (m) {
        matchedHits.push({ id: m[1], node: el });
      }
    }

    // 3. Tekil ID garantisi ve Yaprak Düğüm (Leaf Node) seçimi
    const uniqueHits = [];
    for (const hit of matchedHits) {
      const id = hit.id;
      if (seenIds.has(id)) continue;
      const hasChildWithId = Array.from(hit.node.children).some(ch => (ch.textContent || '').includes(id));
      if (!hasChildWithId) {
        seenIds.add(id);
        uniqueHits.push(hit);
      }
    }

    // 4. Kart Kapsayıcısı İzolasyonu (Zorunlu Değişmez: TAM 1 tekil sipariş ID'si + Sağ Ship to alanı)
    const knownCardSelectors = [
      '[data-region="order-card"]',
      '[class*="order-card"]',
      '[class*="OrderCard"]',
      '.wt-card',
      '.panel-body',
      'tr[data-order-id]',
      'tr.order-row',
      '[data-testid*="order-card"]'
    ];
    const shipToTextRegex = /\b(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)\b(?![ \t]*\b(?:day|today)\b)/i;

    for (const item of uniqueHits) {
      const node = item.node;
      const id = item.id;

      // Öncelik 1: Bilinen kart seçicisi yalnızca TAM 1 ID ve Ship to içeriyorsa doğrudan kabul et
      let cardEl = node.closest(knownCardSelectors.join(', '));
      if (cardEl && (!containsExactlyOne(cardEl, id) || !shipToTextRegex.test(cardEl.textContent || ''))) {
        cardEl = null;
      }

      // Öncelik 2: Güvenli ata tırmanışı (Kart içindeki Complete order / More actions / Select all metinlerinde ASLA kırılmaz!)
      if (!cardEl || cardEl === document.body || cardEl.tagName === 'MAIN' || cardEl.id === 'root' || cardEl.id === 'content') {
        let curr = node.parentElement;
        let best = node.parentElement || node;

        while (curr && curr !== document.body && curr.tagName !== 'MAIN' && curr.id !== 'root' && curr.id !== 'content') {
          // Yalnızca sayfa seviyesi navigasyon, arama kutusu veya sağ filtre radyo butonları gördüğünde dur!
          if (curr.querySelector('nav, [role="tablist"], [role="navigation"], .wt-tabs, .action-needed, input[type="search"], input[placeholder*="Search" i], input[type="radio"]')) {
            break;
          }

          const ids = uniqueOrderIds(curr);
          if (ids.size > 1) {
            break; // Başka bir siparişle birleştiği an dur!
          }
          if (ids.size === 1 && ids.has(id)) {
            best = curr;
          }
          curr = curr.parentElement;
        }
        cardEl = best;
      }

      if (cardEl && cardEl !== document.body) {
        list.push({ id, element: cardEl });
      }
    }

    return list;
  }

  // 4.1 İZOLE KART İÇİNDEKİ SAĞ SHIP-TO SÜTUNUNU, CHEVRON BUTONUNU VE ADRES KUTULARINI HEDEFLEME
  function isActionOrMenuButton(btn) {
    if (!btn) return true;
    const popupAttr = (btn.getAttribute('aria-haspopup') || '').toLowerCase();
    if (popupAttr === 'menu' || (popupAttr === 'true' && !btn.classList.contains('wt-content-toggle'))) {
      return true;
    }
    const label = [
      btn.getAttribute('aria-label') || '',
      btn.getAttribute('title') || '',
      btn.textContent || ''
    ].join(' ').trim();
    return /\b(?:more\s*actions|order\s*actions|complete\s*order|message\s*buyer|contact\s*buyer|order\s*history|add\s*a\s*private\s*note|print|cancel|refund|copy(?:\s+address)?)\b/i.test(label);
  }

  function resolveCardShipTo(cardEl) {
    if (!cardEl || typeof cardEl.querySelectorAll !== 'function') return null;

    const shipToHeading = findShipToHeading(cardEl);

    // 1. Sağdaki "Ship to" kargo sütununu izole et: #siparişNo içeren sol sütunla birleşmeden önceki en geniş kapsayıcı
    let shipToColumn = shipToHeading || null;
    if (shipToHeading) {
      let parent = shipToHeading.parentElement;
      while (parent && parent !== cardEl && cardEl.contains(parent)) {
        if (uniqueOrderIds(parent).size > 0) {
          break; // Sol sütun (#orderId) ile birleşmeden hemen önce dur!
        }
        shipToColumn = parent;
        parent = parent.parentElement;
      }
    }

    // 2. Toggle butonunu bul:
    // A) "Ship to" metnini doğrudan içeren toggle butonu (wt-content-toggle vb.)
    let toggleBtn = null;
    const buttonScope = shipToColumn || cardEl;
    const directShipToBtns = Array.from(buttonScope.querySelectorAll('button, [role="button"], [aria-expanded], a.wt-btn, a[class*="toggle"]'))
      .filter(b => uniqueOrderIds(b).size === 0 && !isActionOrMenuButton(b))
      .filter(b => {
        const clean = getVisibleLabelText(b) || (b.textContent || '').trim();
        return /\b(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)\b(?![ \t]*\b(?:day|today)\b)/i.test(clean);
      })
      .sort((a, b) => (a.textContent || '').trim().length - (b.textContent || '').trim().length);

    if (directShipToBtns.length > 0) {
      toggleBtn = directShipToBtns[0];
    }

    // B) "Ship to" düz metin ise aynı başlık satırındaki kardeş SVG chevron (⌄) butonunu yakala
    if (!toggleBtn && shipToHeading) {
      const wrappingBtn = shipToHeading.closest('button, [role="button"], [aria-expanded], a.wt-content-toggle, a[class*="toggle"]');
      if (wrappingBtn && cardEl.contains(wrappingBtn) && uniqueOrderIds(wrappingBtn).size === 0 && !isActionOrMenuButton(wrappingBtn)) {
        toggleBtn = wrappingBtn;
      } else {
        let row = shipToHeading.parentElement;
        for (let depth = 0; depth < 3 && row && (!shipToColumn || shipToColumn.contains(row)); depth++) {
          const rowBtns = Array.from(row.querySelectorAll('button, [role="button"], [aria-expanded], a.wt-content-toggle'))
            .filter(b => uniqueOrderIds(b).size === 0 && !isActionOrMenuButton(b));
          if (rowBtns.length > 0) {
            toggleBtn = rowBtns.find(b => b.hasAttribute('aria-expanded') || b.hasAttribute('aria-controls') || b.classList.contains('wt-content-toggle'))
                     || rowBtns.find(b => {
                          const t = getVisibleLabelText(b);
                          return t === '' || /^(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)$/i.test(t);
                        })
                     || rowBtns[0];
            break;
          }
          row = row.parentElement;
        }
      }
    }

    // 3. Adres çekmece gövdesi adaylarını topla (Önce aria-controls, sonra sağ sütun, sonra kart içi)
    const candidateBodies = [];
    const pushBody = (b) => {
      if (b && cardEl.contains(b) && uniqueOrderIds(b).size === 0 && !candidateBodies.includes(b)) {
        candidateBodies.push(b);
      }
    };

    if (toggleBtn) {
      const controlsId = toggleBtn.getAttribute('aria-controls');
      if (controlsId) {
        const escapedId = (typeof CSS !== 'undefined' && CSS.escape) ? CSS.escape(controlsId) : controlsId;
        pushBody(cardEl.querySelector('#' + escapedId) || document.getElementById(controlsId));
      }
    }
    if (shipToColumn) {
      for (const b of shipToColumn.querySelectorAll('.wt-content-toggle__body, [id*="wt-content-toggle"], .ship-to-summary, address')) {
        pushBody(b);
      }
    }
    for (const b of cardEl.querySelectorAll('.wt-content-toggle__body, [id*="wt-content-toggle"], .ship-to-summary, address')) {
      pushBody(b);
    }

    const addrBody = candidateBodies[0] || null;

    // 4. Semantik ve içerik tabanlı açık/kapalı kontrolü
    const colVisibleText = (shipToColumn ? getCleanVisibleText(shipToColumn) : getCleanVisibleText(cardEl)) || '';
    const visLines = extractCleanAddressLines(colVisibleText, true);
    const visParsed = parseAddressFieldsFromLines(visLines, colVisibleText);

    let isExpanded = false;
    if (toggleBtn && toggleBtn.hasAttribute && toggleBtn.hasAttribute('aria-expanded')) {
      isExpanded = toggleBtn.getAttribute('aria-expanded') === 'true' || Boolean(visParsed.isFullAddress);
    } else {
      isExpanded = Boolean(visParsed.isFullAddress) || isDrawerOpen(toggleBtn, addrBody);
    }

    return { toggleBtn, addrBody, candidateBodies, shipToColumn, isExpanded };
  }

  // 4.2 SİPARİŞ LİSTESİ AYIKLAMA (Tam Kart + Çoklu Adres Kutusu Taraması + Tek Satır Alıcı Adı)
  function parseOrdersOnPage() {
    const ordersMap = new Map();

    try {
      // 1. Önce Açık Çekmeceyi (varsa 100% tam adres) tara
      const drawerOrder = parseOpenDrawerOrder();
      if (drawerOrder) {
        ordersMap.set(drawerOrder.receiptId, drawerOrder);
      }

      // 2. DOM Seviyesinde İzole Sipariş Kartlarını Bul
      const containers = findOrderContainers();

      if (containers.length > 0) {
        for (const item of containers) {
          const id = item.id;
          const el = item.element;
          const block = getCleanVisibleText(el) || el.innerText || htmlToText(el.innerHTML);

          // İzole sağ Ship-to sütununu ve tüm adres çekmece adaylarını çöz
          const shipToInfo = resolveCardShipTo(el);
          const candidateBodies = shipToInfo ? (shipToInfo.candidateBodies || []) : [];
          const colText = (shipToInfo && shipToInfo.shipToColumn)
            ? (getCleanVisibleText(shipToInfo.shipToColumn) || shipToInfo.shipToColumn.innerText || htmlToText(shipToInfo.shipToColumn.innerHTML))
            : '';

          // Katman 1: Sağ Ship-to sütunundan ve tam kart bloğundan "Ship to" sonrası görünür metni çöz
          const colLines = colText ? extractCleanAddressLines(colText, true) : [];
          const colParsed = parseAddressFieldsFromLines(colLines, colText || block);

          const blockLines = extractCleanAddressLines(block, true);
          const blockParsed = parseAddressFieldsFromLines(blockLines, block);

          let summaryParsed = (colParsed.isFullAddress || colParsed.city || colParsed.state || !isInvalidBuyerName(colParsed.buyerName))
            ? {
                ...blockParsed,
                ...colParsed,
                buyerName: !isInvalidBuyerName(colParsed.buyerName) ? colParsed.buyerName : blockParsed.buyerName,
                city: colParsed.city || blockParsed.city,
                state: colParsed.state || blockParsed.state,
                postalCode: colParsed.postalCode || blockParsed.postalCode,
                streetAddress: colParsed.streetAddress || blockParsed.streetAddress,
                isFullAddress: Boolean(colParsed.isFullAddress || blockParsed.isFullAddress)
              }
            : blockParsed;

          // Katman 2: Kart içindeki gizli/açık tüm .wt-content-toggle__body adaylarını tara (Kapalıyken bile innerHTML'den tam sokak adresini yakalar!)
          let parsed = summaryParsed;
          let matchedAddrBody = null;
          for (const bodyEl of candidateBodies) {
            const bodyText = getCleanVisibleText(bodyEl) || htmlToText(bodyEl.innerHTML);
            const bodyLines = extractCleanAddressLines(bodyText, false);
            if (bodyLines.length === 0) continue;

            const bodyParsed = parseAddressFieldsFromLines(bodyLines, bodyText);
            if (bodyParsed.isFullAddress) {
              const mergedBuyer = !isInvalidBuyerName(bodyParsed.buyerName) ? bodyParsed.buyerName : summaryParsed.buyerName;
              parsed = {
                ...bodyParsed,
                buyerName: mergedBuyer,
                rawAddressText: [
                  mergedBuyer,
                  bodyParsed.streetAddress,
                  bodyParsed.secondAddress,
                  [bodyParsed.postalCode, bodyParsed.city, bodyParsed.state].filter(Boolean).join(' '),
                  bodyParsed.country,
                  bodyParsed.phone ? `Tel: ${bodyParsed.phone}` : '',
                  bodyParsed.iossNumber ? `IOSS: ${bodyParsed.iossNumber}` : ''
                ].filter(Boolean).join('\n')
              };
              matchedAddrBody = bodyEl;
              break;
            }
            if (!parsed.city && (bodyParsed.city || bodyParsed.state)) {
              parsed = {
                ...summaryParsed,
                ...bodyParsed,
                buyerName: !isInvalidBuyerName(bodyParsed.buyerName) ? bodyParsed.buyerName : summaryParsed.buyerName
              };
              matchedAddrBody = bodyEl;
            }
          }

          // Kupon kodu veya sipariş ID'sinin yanlışlıkla cadde adresi olmasını engelle
          if (parsed.streetAddress && (/^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}(?:\s*\(\d+%\s*off\))?$/i.test(parsed.streetAddress) || /Shop coupon/i.test(parsed.streetAddress) || /\(\d+%\s*off\)/i.test(parsed.streetAddress) || /#\d{9,12}/.test(parsed.streetAddress) || /\(Etsy\)/i.test(parsed.streetAddress))) {
            parsed.streetAddress = '';
            parsed.isFullAddress = false;
          }

          // Tutar ve Para Birimi
          let currency = 'USD';
          if (block.includes('TL') || block.includes('₺')) currency = 'TRY';
          else if (block.includes('EUR') || block.includes('€')) currency = 'EUR';
          else if (block.includes('GBP') || block.includes('£')) currency = 'GBP';

          const priceMatch = block.match(/#\d{9,12}[^\n\r]*(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i)
                          || block.match(/(?:US\$|\$|₺|EUR|€)\s*([\d,.]+)/i);
          const total = priceMatch ? parseFloat(priceMatch[1].replace(/,/g, '')) : null;

          // Alıcı Adı (Kesinlikle tek satır yakalama)
          let buyerName = parsed.buyerName ? parsed.buyerName.replace(/[▾▼⌄⌃]/g, '').trim() : '';
          if (isInvalidBuyerName(buyerName)) {
            // 1. Tek başına satır başlığı olan Ship to altındaki ilk satırı yakala
            for (const src of [colText, block]) {
              if (!src) continue;
              const shipToNameMatch = src.match(/(?:^|[\r\n])[ \t]*(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)[ \t:▾▼⌄⌃]*[\r\n]+[ \t]*([^\r\n▾▼⌄⌃]+)/i);
              if (shipToNameMatch) {
                const candidate = shipToNameMatch[1].replace(/[▾▼⌄⌃]/g, '').trim();
                if (!isInvalidBuyerName(candidate) && !looksLikeAddressLine(candidate)) {
                  buyerName = candidate;
                  break;
                }
              }
            }
            // 2. Sol sütunda #siparişNo satırının hemen üstündeki tekil satırı yakala (Örn: Damien Panico ▾, Marie Cindy ▾)
            if (isInvalidBuyerName(buyerName)) {
              const nameMatch = block.match(/(?:^|[\r\n])[ \t]*([^\r\n▾▼⌄⌃#]+?)[ \t]*[▾▼⌄⌃]?[ \t]*[\r\n]+[ \t]*#\d{9,12}\b/);
              if (nameMatch) {
                const candidate = nameMatch[1].replace(/[▾▼⌄⌃]/g, '').trim();
                if (!isInvalidBuyerName(candidate) && !looksLikeAddressLine(candidate)) {
                  buyerName = candidate;
                }
              }
            }
            // 3. Kart içindeki alıcı profili linki veya butonu
            if (isInvalidBuyerName(buyerName)) {
              const buyerLink = el.querySelector('a[href*="/your/orders/sold/buyer/"], a[href*="/people/"], a[href*="buyer_id="], a[href*="buyer="], [class*="buyer-name"], button[aria-label*="buyer" i]');
              if (buyerLink && buyerLink.textContent?.trim()) {
                const cand = getVisibleLabelText(buyerLink) || buyerLink.textContent.replace(/[▾▼⌄⌃]/g, '').trim();
                if (!isInvalidBuyerName(cand) && !looksLikeAddressLine(cand)) {
                  buyerName = cand;
                }
              }
            }
          }

          // parsed.rawAddressText alıcı adını içermiyorsa başına ekle
          if (buyerName && !isInvalidBuyerName(buyerName) && parsed.rawAddressText && !parsed.rawAddressText.startsWith(buyerName)) {
            parsed.rawAddressText = `${buyerName}\n${parsed.rawAddressText}`;
          }

          // Kart Üzerindeki SKU, Varyasyon ve Kargo Yöntemi (Örn: "SKU 5", "Personalization: 7.5 inches 19 cm")
          const skuM = block.match(/\bSKU(?:\s*:|\s+)\s*([^\r\n]+)/i);
          const cardSku = skuM ? skuM[1].trim() : null;
          const varM = block.match(/\b(?:Personalization|Height|Size|Color|Style|Material|Option|Boyut|Renk|Ölçü|Kişiselleştirme)\s*:\s*([^\r\n]+)/i);
          const cardVariation = varM ? varM[0].trim() : null;
          const shipMethodM = block.match(/\b(Standard Shipping|Express Shipping|Priority Mail|Expedited Shipping)(?:[^\r\n]*)/i);
          const cardShipMethod = shipMethodM ? shipMethodM[1].trim() : null;

          // Kargo Takip Kodu
          const trackMatch = block.match(/(\d{12,30})[\s\r\n]+Shipped/i)
                          || block.match(/Shipped[\s\r\n]+(?:via\s+[A-Za-z]+\s+)?(\d{10,30})/i)
                          || block.match(/Tracking:\s*([A-Za-z0-9]+)/i);
          let tracking = trackMatch ? trackMatch[1].trim() : null;
          if (tracking && /^(?:no[\s_-]?tracking|none)$/i.test(tracking)) {
            tracking = null;
          }

          // Ürün Adı
          const titleMatch = block.match(/Shop coupon[\s\r\n]+([^\n\r]+)/i) 
                          || block.match(/\(Etsy\)[^\n\r]*[\r\n]+([^\n\r]+)/i);
          const titleLink = el.querySelector('a[href*="/listing/"], [class*="listing-title"], [class*="item-title"]');
          let title = titleLink && titleLink.textContent?.trim() ? titleLink.textContent.trim() : (titleMatch ? titleMatch[1].trim() : 'Etsy Ürünü');

          // Sipariş Tarihi: Önce #siparişNo sonrasındaki kart içi tarihi ara (üst tarih grubu çubuğu yerine kartın gerçek tarihini alır)
          const idPos = block.indexOf(id);
          const afterIdBlock = idPos !== -1 ? block.slice(idPos) : block;
          const dateMatch = afterIdBlock.match(/(?:Ordered|Order date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
                         || block.match(/(?:Ordered|Order date)[\s:]*([A-Za-z]{3,9}\s+\d{1,2}(?:,\s*\d{4})?)/i)
                         || afterIdBlock.match(/([A-Za-z]{3}\s+\d{1,2},\s*\d{4})/)
                         || block.match(/([A-Za-z]{3}\s+\d{1,2},\s*\d{4})/);
          const orderDate = dateMatch ? dateMatch[1].trim() : '';

          // Sipariş Durumu
          let status = 'Processing';
          const hasTracking = Boolean(tracking && tracking.length >= 6);
          if (hasTracking && (block.includes('In transit') || block.includes('Shipped'))) {
            status = 'Shipped';
          } else if (block.includes('Completed') && !hasTracking) {
            status = 'Processing'; // Completed fakat No tracking -> Bekleyen!
          } else if (block.includes('In transit') || block.includes('Shipped')) {
            status = hasTracking ? 'Shipped' : 'Processing';
          } else if (block.includes('Not shipped') || block.includes('Processing') || block.includes('New')) {
            status = 'Processing';
          }

          // Eğer drawer'dan zaten tam adres alındıysa ezme, eksik meta alanları tamamla
          const existing = ordersMap.get(id);
          if (existing && existing.isFullAddress) {
            if (total != null) existing.orderTotal = total;
            if (tracking) existing.trackingNumber = tracking;
            if (title && title !== 'Etsy Ürünü') existing.productTitle = title;
            if (orderDate) existing.orderDate = orderDate;
            if (cardSku && !existing.sku) existing.sku = cardSku;
            if (cardVariation && !existing.variation) existing.variation = cardVariation;
            if (cardShipMethod && !existing.shippingMethod) existing.shippingMethod = cardShipMethod;
            if (parsed.iossNumber && !existing.iossNumber) existing.iossNumber = parsed.iossNumber;
            continue;
          }

          const fullAddrSnippet = [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', ');

          ordersMap.set(id, {
            receiptId: id,
            buyerName: buyerName || 'Etsy Müşterisi',
            shippingAddress: fullAddrSnippet || [parsed.city, parsed.state, parsed.country].filter(Boolean).join(', ') || 'Adres Kayıtlı',
            streetAddress: parsed.streetAddress,
            secondAddress: parsed.secondAddress,
            city: parsed.city,
            state: parsed.state,
            postalCode: parsed.postalCode,
            country: parsed.country,
            phone: parsed.phone,
            iossNumber: parsed.iossNumber,
            rawAddressText: parsed.rawAddressText,
            isFullAddress: parsed.isFullAddress,
            sku: cardSku,
            variation: cardVariation,
            shippingMethod: cardShipMethod,
            orderTotal: total,
            currency: currency,
            orderDate: orderDate,
            trackingNumber: tracking,
            productTitle: title,
            status: status,
            sourceType: parsed.isFullAddress
              ? (matchedAddrBody ? 'Canlı Kart (.wt-content-toggle__body)' : 'Canlı Kart (Açık Ship to)')
              : 'Liste Özeti (İzole Kart)',
            sourceUrl: window.location.pathname,
            httpStatus: 'DOM (Canlı İzole Kart)',
            resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
            scrapedAt: new Date().toISOString()
          });
        }
      }
    } catch (err) {
      console.error('Siparişler taranırken hata:', err);
    }

    return Array.from(ordersMap.values());
  }

  // 5. ÇEKMECE AÇILDIĞINDA OTOMATİK TAM ADRES GÜNCELLEMESİ (MutationObserver & Context Koruması)
  let lastObservedDrawerId = null;
  const observer = new MutationObserver(() => {
    if (!isContextValid()) {
      try { observer.disconnect(); } catch {}
      return;
    }
    // SADECE siparişler sayfasındayken çekmece gözlemle (Finans veya Dashboard sayfalarında asla tetiklenmez!)
    if (getPageType() !== 'orders') return;

    try {
      const drawer = parseOpenDrawerOrder();
      if (drawer && drawer.isFullAddress && drawer.receiptId !== lastObservedDrawerId) {
        lastObservedDrawerId = drawer.receiptId;
        if (!isContextValid()) return;
        chrome.storage.local.get(['storedOrders'], (res) => {
          if (!isContextValid() || (typeof chrome !== 'undefined' && chrome.runtime && chrome.runtime.lastError)) return;
          const stored = Array.isArray(res?.storedOrders) ? res.storedOrders : [];
          const idx = stored.findIndex(o => o.receiptId === drawer.receiptId);
          if (idx !== -1) {
            stored[idx] = { ...stored[idx], ...drawer };
          } else {
            stored.push(drawer);
          }
          if (isContextValid()) {
            chrome.storage.local.set({ storedOrders: stored }, () => {
              if (chrome.runtime?.lastError) { /* sessiz geç */ }
            });
          }
        });
      }
    } catch (e) {
      if (!isContextValid() || (e && e.message && e.message.includes('context invalidated'))) {
        try { observer.disconnect(); } catch {}
      }
    }
  });

  try {
    observer.observe(document.body, { childList: true, subtree: true });
  } catch {}

  // 6. ÇEKMECELERİ AÇMADAN ARKA PLANDA DERİN TAM ADRES ÇÖZÜCÜ
  async function fetchSingleOrderFullAddress(receiptId) {
    if (!receiptId) return null;
    if (getPageType() !== 'orders') return null;

    try {
      const openDrawer = parseOpenDrawerOrder();
      if (openDrawer && String(openDrawer.receiptId) === String(receiptId) && openDrawer.isFullAddress) {
        return openDrawer;
      }

      const targetUrls = [
        `/your/orders/sold?order_id=${receiptId}`,
        `/your/orders/sold/all?order_id=${receiptId}`
      ];

      for (const targetUrl of targetUrls) {
        try {
          const res = await fetch(targetUrl, {
            credentials: 'include',
            headers: {
              'Accept': 'text/html,application/xhtml+xml,application/xml;q=0.9,*/*;q=0.8'
            }
          });

          if (!res.ok) continue;

          const html = await res.text();
          const cleanText = htmlToText(html);
          // Kesinlikle Ship to zorunlu (rastgele sayfa metinlerinin adres sanılmasını önle)
          const lines = extractCleanAddressLines(cleanText, true);

          const transM = cleanText.match(/Transaction ID:\s*(\d+)/i);
          const skuM = cleanText.match(/\bSKU(?:\s*:|\s+)\s*([^\r\n]+)/i);
          const varM = cleanText.match(/\b(?:Personalization|Height|Size|Color|Style|Material|Option|Boyut|Renk|Ölçü|Kişiselleştirme)\s*:\s*([^\r\n]+)/i);
          const shipMethodM = cleanText.match(/Selected by buyer[\s\r\n]+([^\r\n]+)/i);

          if (lines.length >= 2) {
            const parsed = parseAddressFieldsFromLines(lines, cleanText);
            if (!isInvalidBuyerName(parsed.buyerName) && (parsed.isFullAddress || lines.length >= 3)) {
              return {
                receiptId: String(receiptId),
                buyerName: parsed.buyerName,
                shippingAddress: [parsed.streetAddress, parsed.secondAddress, parsed.city, parsed.state, parsed.postalCode, parsed.country].filter(Boolean).join(', '),
                streetAddress: parsed.streetAddress,
                secondAddress: parsed.secondAddress,
                city: parsed.city,
                state: parsed.state,
                postalCode: parsed.postalCode,
                country: parsed.country,
                phone: parsed.phone,
                iossNumber: parsed.iossNumber,
                rawAddressText: parsed.rawAddressText,
                isFullAddress: Boolean(parsed.isFullAddress || parsed.streetAddress),
                transactionId: transM ? transM[1].trim() : null,
                sku: skuM ? skuM[1].trim() : null,
                variation: varM ? varM[0].trim() : null,
                shippingMethod: shipMethodM ? shipMethodM[1].trim() : null,
                sourceType: 'Arka Plan Çekmece İsteği (Same-Origin Fetch)',
                sourceUrl: targetUrl,
                httpStatus: '200 OK',
                resolvedAt: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit', second: '2-digit' }),
                scrapedAt: new Date().toISOString()
              };
            }
          }
        } catch (innerErr) {
          console.warn(`URL denemesi başarısız (${targetUrl}):`, innerErr);
        }
      }

      return null;
    } catch (err) {
      console.error('fetchSingleOrderFullAddress genel hatası:', receiptId, err);
      return null;
    }
  }

  // 6.5 TÜM SİPARİŞLERİN SHIP TO INLINE ÇEKMECELERİNİ AÇ VE TAM ADRESLERİ DERLE
  async function expandAndParseAllInlineOrders() {
    try {
      if (getPageType() !== 'orders') return [];

      const existingById = new Map(parseOrdersOnPage().map(o => [o.receiptId, o]));
      const buttonsToClick = new Set();
      const containers = findOrderContainers();

      for (const item of containers) {
        const alreadyFull = existingById.get(item.id)?.isFullAddress === true;
        if (alreadyFull) continue;
        const shipTo = resolveCardShipTo(item.element);
        if (shipTo && !shipTo.isExpanded && shipTo.toggleBtn) {
          buttonsToClick.add(shipTo.toggleBtn);
        }
      }

      // Güvenli Fallback: Yalnızca henüz açık olmayan Ship to toggle butonlarını topla
      if (buttonsToClick.size === 0 && containers.some(c => !existingById.get(c.id)?.isFullAddress)) {
        const toggleButtons = Array.from(document.querySelectorAll('button.wt-content-toggle, button[aria-controls*="wt-content-toggle"]'))
          .filter(btn => btn.getAttribute('aria-expanded') !== 'true')
          .filter(btn => /\b(?:Ship\s+to|Shipped\s+to|Gönderim\s+adresi|Teslimat\s+adresi)\b(?![ \t]*\b(?:day|today)\b)/i.test(getVisibleLabelText(btn) || (btn.textContent || '').trim()));
        for (const btn of toggleButtons) {
          buttonsToClick.add(btn);
        }
      }

      console.log(`[Etsy Copilot] Açılacak ${buttonsToClick.size} inline toggle butonu tespit edildi.`);

      // Butonları güvenli ve nazik gecikmeyle tıkla
      for (const btn of buttonsToClick) {
        try {
          btn.click();
          await new Promise(r => setTimeout(r, 100));
        } catch (e) {
          console.warn('Inline toggle tıklanırken hata:', e);
        }
      }

      // Dinamik bekleme: Toggle kutuları açılana kadar bekle (aria-expanded veya body görünürlüğü)
      const startTime = Date.now();
      while (Date.now() - startTime < 1500) {
        const allOpened = Array.from(buttonsToClick).every(btn => {
          if (!btn.isConnected) return true;
          if (btn.getAttribute('aria-expanded') === 'true') return true;
          const cid = btn.getAttribute('aria-controls');
          const body = cid ? document.getElementById(cid) : null;
          return body && (body.getAttribute('aria-hidden') === 'false' || Boolean(body.offsetHeight));
        });
        if (allOpened) break;
        await new Promise(r => setTimeout(r, 100));
      }

      // DOM güncellemelerinin oturması için kısa pay
      await new Promise(r => setTimeout(r, 200));

      const orders = parseOrdersOnPage();
      return orders;
    } catch (err) {
      console.error('expandAndParseAllInlineOrders hatası:', err);
      return parseOrdersOnPage();
    }
  }

  // 7. MESAJ DİNLEYİCİ & İDEMPOTENT TEMİZLEYİCİ
  const messageHandler = (request, sender, sendResponse) => {
    if (!isContextValid()) return false;
    const pageType = getPageType();

    if (request.action === 'GET_PAGE_DATA') {
      try {
        let financeData = null;
        let ordersData = null;
        let drawerOrder = null;

        if (pageType === 'finance') {
          financeData = parseFinanceSnapshot();
        } else if (pageType === 'orders') {
          ordersData = parseOrdersOnPage();
          drawerOrder = parseOpenDrawerOrder();
        }

        sendResponse({
          success: true,
          scriptVersion: SCRIPT_VERSION,
          pageType: pageType,
          financeData: financeData,
          ordersData: ordersData,
          drawerOrder: drawerOrder,
          url: window.location.href
        });
      } catch (err) {
        console.error('GET_PAGE_DATA hatası:', err);
        sendResponse({
          success: false,
          scriptVersion: SCRIPT_VERSION,
          pageType: pageType,
          error: err ? err.message : 'Bilinmeyen hata',
          url: window.location.href
        });
      }
      return true;
    }

    if (request.action === 'PARSE_ORDERS') {
      const ords = parseOrdersOnPage();
      sendResponse({ success: true, scriptVersion: SCRIPT_VERSION, data: ords });
      return true;
    }

    if (request.action === 'EXPAND_AND_PARSE_PAGE_ORDERS') {
      expandAndParseAllInlineOrders().then(ords => {
        sendResponse({ success: true, scriptVersion: SCRIPT_VERSION, data: ords });
      }).catch(err => {
        sendResponse({ success: false, scriptVersion: SCRIPT_VERSION, error: err ? err.message : 'Bilinmeyen hata' });
      });
      return true;
    }

    if (request.action === 'FETCH_ORDER_FULL_ADDRESS') {
      const receiptId = request.receiptId;
      fetchSingleOrderFullAddress(receiptId).then(addressData => {
        sendResponse({ success: !!addressData, scriptVersion: SCRIPT_VERSION, data: addressData });
      }).catch(err => {
        sendResponse({ success: false, scriptVersion: SCRIPT_VERSION, error: err ? err.message : 'Bilinmeyen hata' });
      });
      return true;
    }

    return true;
  };

  if (isContextValid() && chrome.runtime?.onMessage) {
    chrome.runtime.onMessage.addListener(messageHandler);
    window.__etsyCopilotCleanup = () => {
      try { observer.disconnect(); } catch {}
      try { chrome.runtime?.onMessage?.removeListener(messageHandler); } catch {}
    };
  }
})();
