// Etsy Copilot v2.0.0 — Evrensel Adres, Para Birimi ve Ülke Ayrıştırıcı (Content Script + Node Test Uyumlu)
// Sıfır Mock İlkesi: Hiçbir sabit/uydurma adres veya bakiye üretmez.

(function (root) {
  'use strict';

  const ISO_COUNTRY_NAMES = Object.freeze({
    US: 'United States', USA: 'United States',
    CA: 'Canada', CAN: 'Canada',
    GB: 'United Kingdom', UK: 'United Kingdom', GBR: 'United Kingdom',
    AU: 'Australia', AUS: 'Australia',
    DE: 'Germany', DEU: 'Germany',
    FR: 'France', FRA: 'France',
    NL: 'Netherlands', NLD: 'Netherlands',
    IT: 'Italy', ITA: 'Italy',
    ES: 'Spain', ESP: 'Spain',
    TR: 'Turkey', TUR: 'Turkey',
    AT: 'Austria', BEL: 'Belgium', BE: 'Belgium',
    CH: 'Switzerland', SE: 'Sweden', NO: 'Norway',
    DK: 'Denmark', FI: 'Finland', PL: 'Poland',
    IE: 'Ireland', NZ: 'New Zealand', JP: 'Japan',
    PT: 'Portugal', GR: 'Greece', CZ: 'Czech Republic',
    HU: 'Hungary', RO: 'Romania', BG: 'Bulgaria',
    HR: 'Croatia', SK: 'Slovakia', SI: 'Slovenia',
    EE: 'Estonia', LV: 'Latvia', LT: 'Lithuania',
    CY: 'Cyprus', MT: 'Malta', IL: 'Israel',
    AE: 'United Arab Emirates', SA: 'Saudi Arabia',
    SG: 'Singapore', KR: 'South Korea', MX: 'Mexico', BR: 'Brazil'
  });

  // Etsy v2/v3 standart country_id eşlemesi (Etsy.Context yüklenmemişse yedek tablo)
  const ETSY_COUNTRY_ID_MAP = Object.freeze({
    209: 'United States',
    79: 'Canada',
    105: 'United Kingdom',
    61: 'Australia',
    91: 'Germany',
    103: 'France',
    150: 'Netherlands',
    128: 'Italy',
    99: 'Spain',
    200: 'Turkey',
    63: 'Austria',
    68: 'Belgium',
    194: 'Switzerland',
    193: 'Sweden',
    162: 'Norway',
    93: 'Denmark',
    102: 'Finland',
    174: 'Poland',
    123: 'Ireland',
    158: 'New Zealand',
    131: 'Japan',
    175: 'Portugal',
    112: 'Greece'
  });

  // Çok dilli (İngilizce + Google Çeviri Türkçe + Almanca/Fransızca) ülke isimleri -> Kanonik İngilizce ülke adı
  const COUNTRY_ALIASES = new Map([
    ['united states', 'United States'], ['united states of america', 'United States'], ['usa', 'United States'],
    ['america', 'United States'], ['amerika birleşik devletleri', 'United States'], ['abd', 'United States'],
    ['birleşik devletler', 'United States'],
    ['canada', 'Canada'], ['kanada', 'Canada'],
    ['united kingdom', 'United Kingdom'], ['great britain', 'United Kingdom'], ['uk', 'United Kingdom'],
    ['birleşik krallık', 'United Kingdom'], ['ingiltere', 'United Kingdom'],
    ['australia', 'Australia'], ['avustralya', 'Australia'],
    ['germany', 'Germany'], ['deutschland', 'Germany'], ['almanya', 'Germany'],
    ['france', 'France'], ['fransa', 'France'],
    ['netherlands', 'Netherlands'], ['the netherlands', 'Netherlands'], ['hollanda', 'Netherlands'],
    ['italy', 'Italy'], ['italia', 'Italy'], ['italya', 'Italy'],
    ['spain', 'Spain'], ['españa', 'Spain'], ['ispanya', 'Spain'],
    ['turkey', 'Turkey'], ['türkiye', 'Turkey'], ['turkiye', 'Turkey'],
    ['austria', 'Austria'], ['österreich', 'Austria'], ['avusturya', 'Austria'],
    ['belgium', 'Belgium'], ['belçika', 'Belgium'],
    ['switzerland', 'Switzerland'], ['isviçre', 'Switzerland'],
    ['sweden', 'Sweden'], ['isveç', 'Sweden'],
    ['norway', 'Norway'], ['norveç', 'Norway'],
    ['denmark', 'Denmark'], ['danimarka', 'Denmark'],
    ['finland', 'Finland'], ['finlandiya', 'Finland'],
    ['poland', 'Poland'], ['polonya', 'Poland'],
    ['ireland', 'Ireland'], ['irlanda', 'Ireland'],
    ['new zealand', 'New Zealand'], ['yeni zelanda', 'New Zealand'],
    ['japan', 'Japan'], ['japonya', 'Japan'],
    ['portugal', 'Portugal'], ['portekiz', 'Portugal'],
    ['greece', 'Greece'], ['yunanistan', 'Greece'],
    ['czech republic', 'Czech Republic'], ['czechia', 'Czech Republic'], ['çekya', 'Czech Republic'],
    ['hungary', 'Hungary'], ['macaristan', 'Hungary'],
    ['romania', 'Romania'], ['romanya', 'Romania'],
    ['bulgaria', 'Bulgaria'], ['bulgaristan', 'Bulgaria'],
    ['croatia', 'Croatia'], ['hırvatistan', 'Croatia'],
    ['slovakia', 'Slovakia'], ['slovakya', 'Slovakia'],
    ['slovenia', 'Slovenia'], ['slovenya', 'Slovenia'],
    ['estonia', 'Estonia'], ['estonya', 'Estonia'],
    ['latvia', 'Latvia'], ['letonya', 'Latvia'],
    ['lithuania', 'Lithuania'], ['litvanya', 'Lithuania'],
    ['cyprus', 'Cyprus'], ['kıbrıs', 'Cyprus'],
    ['malta', 'Malta']
  ]);

  const US_STATE_CODES = new Set([
    'AL', 'AK', 'AZ', 'AR', 'CA', 'CO', 'CT', 'DE', 'FL', 'GA',
    'HI', 'ID', 'IL', 'IN', 'IA', 'KS', 'KY', 'LA', 'ME', 'MD',
    'MA', 'MI', 'MN', 'MS', 'MO', 'MT', 'NE', 'NV', 'NH', 'NJ',
    'NM', 'NY', 'NC', 'ND', 'OH', 'OK', 'OR', 'PA', 'RI', 'SC',
    'SD', 'TN', 'TX', 'UT', 'VT', 'VA', 'WA', 'WV', 'WI', 'WY',
    'DC', 'PR', 'VI', 'GU', 'AS', 'MP', 'AA', 'AE', 'AP'
  ]);

  const CA_PROVINCE_CODES = new Set([
    'AB', 'BC', 'MB', 'NB', 'NL', 'NS', 'NT', 'NU', 'ON', 'PE', 'QC', 'SK', 'YT'
  ]);

  // Adres bloğunun bittiğini gösteren kesin rozet / UI sınırları (İngilizce + Google Çeviri Türkçe)
  const NOISE_OR_STOP_LINE_PATTERNS = [
    /\bUSPS\s*Verified\b/i,
    /\bUSPS\s*(?:tarafından\s*)?doğrulandı\b/i,
    /\bOrder\s+in\s+reserve\b/i,
    /\bSipariş\s+rezervde\b/i,
    /\bRezervdeki\s+sipariş\b/i,
    /\bShip\s+with\s+DDP\b/i,
    /\bStandard\s+Shipping\b/i,
    /\bStandart\s+Kargo\b/i,
    /\bExpress\s+Shipping\b/i,
    /\bPriority\s+Mail\b/i,
    /\bNo\s+tracking\b/i,
    /\bTakip\s+(?:numarası\s+)?yok\b/i,
    /\bComplete\s+order\b/i,
    /\bSiparişi\s+tamamla\b/i,
    /\bMore\s+actions\b/i,
    /\bDiğer\s+işlemler\b/i,
    /\bDaha\s+fazla\s+işlem\b/i,
    /\bSelect\s+all\b/i,
    /\bTümünü\s+seç\b/i,
    /\bShop\s+coupon\b/i,
    /\bMağaza\s+kuponu\b/i,
    /\(\s*\d+\s*%\s*(?:off|indirim)\s*\)/i,
    /#\s*\d{9,12}\b/,
    /\(\s*Etsy\s*\)/i,
    /\bOrdered\s+[A-Za-z]{3,9}\b/i,
    /\btarihinde\s+sipariş\s+edildi\b/i,
    /\bShip\s+by\b/i,
    /\bkargoya\s+verin\b/i,
    /\bVAT\s+Collected\b/i,
    /\bIOSS\s+number\b/i,
    /\bMake\s+sure\s+to\s+write\b/i,
    /\bSelected\s+by\s+buyer\b/i,
    /\bAlıcı\s+tarafından\s+seçildi\b/i,
    /\bMarked\s+as\s+gift\b/i,
    /\bHediye\s+olarak\s+işaretlendi\b/i,
    /\bPre-transit\b/i,
    /\bIn\s+transit\b/i,
    /\bDelivered\b/i,
    /\bTeslim\s+edildi\b/i,
    /\bUpdate\s+progress\b/i,
    /\bAdd\s+a\s+private\s+note\b/i,
    /\bOnly\s+you\s+can\s+see\s+this\s+note\b/i,
    /\bNo\s+messages\s+about\s+this\s+order\b/i
  ];

  const HEADING_PREFIX_PATTERNS = /^(?:Ship\s*to|Shipped\s*to|Shipping\s*address|Delivery\s*address|Gönderim\s*adresi|Teslimat\s*adresi|Şuraya\s*gönder(?:in|ildi)?|Gönderilecek\s*adres|Gönderildiği\s*yer|Kargo\s*adresi|Alıcı\s*adresi|Caret\s*(?:down|up)|Shipping\s*destination|Copy(?:\s+address)?|Adresi\s*kopyala)\s*:?$/i;

  function resolveCountryName(input, dynamicCountryMap) {
    if (input == null || input === '') return '';
    if (typeof input === 'number' || /^\d+$/.test(String(input).trim())) {
      const id = Number(input);
      if (dynamicCountryMap && dynamicCountryMap[id]) return dynamicCountryMap[id];
      return ETSY_COUNTRY_ID_MAP[id] || '';
    }
    const raw = String(input).trim();
    const upper = raw.toUpperCase();
    if (ISO_COUNTRY_NAMES[upper]) return ISO_COUNTRY_NAMES[upper];
    const alias = COUNTRY_ALIASES.get(raw.toLowerCase());
    if (alias) return alias;
    return '';
  }

  function isInvalidPersonName(name) {
    if (!name) return true;
    const t = String(name).replace(/[▾▼⌄⌃]/g, '').replace(/\s{2,}/g, ' ').trim();
    if (t.length < 2 || t.length > 80) return true;
    if (/^\d+$/.test(t) || /[\r\n]/.test(t)) return true;
    if (/\b\d{9,12}\b/.test(t) || /\(\s*Etsy\s*\)/i.test(t)) return true;
    if (/(?:ABD\s*\$|US\s*\$|\$|₺|€|£)\s*[\d,.]+/i.test(t)) return true;
    if (/^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}$/i.test(t)) return true;
    if (HEADING_PREFIX_PATTERNS.test(t)) return true;
    if (NOISE_OR_STOP_LINE_PATTERNS.some((p) => p.test(t))) return true;
    if (resolveCountryName(t)) return true;
    if (/^(?:Gizlilik\s*Ayarlar[ıi]|Yard[ıi]m\s*Al[ıi]n|Mağaza\s*Müdürü|Siparişler|İlanlar|Mesajlar|Finans|Ayarlar|Quantity\s*\d*|Adet\s*\d*|SKU\b.*|Personalization\b.*|Height\b.*|Size\b.*|Color\b.*|Order\s*history|Message\s*buyer|Add\s*a\s*private\s*note|Print\s*shipping\s*label|day|days|ago|today|yesterday|overdue|\d+\s*days?\s*ago)$/i.test(t)) {
      return true;
    }
    return false;
  }

  function looksLikeStreetOrCityLine(line) {
    if (!line) return false;
    const t = String(line).trim();
    if (resolveCountryName(t)) return true;
    if (/^(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-.]/i.test(t)) return true;
    if (/^[+\d()\s.-]{7,22}$/.test(t) && t.replace(/\D/g, '').length >= 7) return true;
    if (/^(?:P\.?\s*O\.?\s*Box|PSC\s+\d|CMR\s+\d|UNIT\s+\d|APT\.?\s+\w|STE\.?\s+\w|SUITE\s+\w)/i.test(t)) return true;
    if (/^\d{1,6}\s+[A-Za-z\u00C0-\u017F]/.test(t)) return true;
    if (/^[A-Za-z\u00C0-\u017F \t.'-]+,\s*[A-Z]{2}(?:\s+\d{5}(?:-\d{4})?)?$/i.test(t)) return true;
    if (/^[A-Za-z\u00C0-\u017F \t.'-]+?\s+[A-Z]{2}\s+\d{5}(?:-\d{4})?$/i.test(t)) return true;
    if (/^(?:[A-Z]{1,2}-)?\d{4,5}\s+[A-Za-z\u00C0-\u017F\s.-]+$/i.test(t)) return true;
    return false;
  }

  /**
   * "1,250.50", "110,42", "28,479.09", "-50.35" gibi hem ABD hem Avrupa/Google-Çeviri sayı formatlarını çözer.
   */
  function parseFlexibleMoneyNumber(raw) {
    if (raw == null) return null;
    if (typeof raw === 'number') return Number.isFinite(raw) ? raw : null;
    let s = String(raw).replace(/[^\d.,+-]/g, '').trim();
    if (!s) return null;

    const lastComma = s.lastIndexOf(',');
    const lastDot = s.lastIndexOf('.');

    if (lastComma !== -1 && lastDot !== -1) {
      if (lastComma > lastDot) {
        // Avrupa formatı: 1.250,42 -> 1250.42
        s = s.replace(/\./g, '').replace(',', '.');
      } else {
        // ABD formatı: 1,250.42 -> 1250.42
        s = s.replace(/,/g, '');
      }
    } else if (lastComma !== -1) {
      // Yalnızca virgül var: virgülden sonra tam 2 hane varsa ondalıktır ("110,42"), 3 hane varsa binliktir ("1,250")
      const decimals = s.length - lastComma - 1;
      if (decimals === 2 || decimals === 1) {
        s = s.replace(',', '.');
      } else {
        s = s.replace(/,/g, '');
      }
    }

    const num = Number.parseFloat(s);
    return Number.isFinite(num) ? Number(num.toFixed(2)) : null;
  }

  /**
   * Bir metin veya adres kutusundan gelen ham satır dizisini yapılandırılmış adres alanlarına çevirir.
   */
  function parseAddressLines(rawLines, fullContextText = '') {
    const empty = {
      recipientName: '',
      streetAddress: '',
      secondAddress: '',
      city: '',
      state: '',
      postalCode: '',
      country: '',
      phone: '',
      iossNumber: '',
      rawAddressText: '',
      isFullAddress: false
    };

    if (!Array.isArray(rawLines) || rawLines.length === 0) return empty;

    // 1. Gürültü ve durdurma sınırlarını temizle
    const filtered = [];
    for (const raw of rawLines) {
      const line = String(raw || '').replace(/[▾▼⌄⌃]/g, '').replace(/\s{2,}/g, ' ').trim();
      if (!line) continue;
      if (/^[▾▼⌄⌃\s\-_–—✓✔]+$/.test(line)) continue;
      if (HEADING_PREFIX_PATTERNS.test(line)) continue;
      if (NOISE_OR_STOP_LINE_PATTERNS.some((p) => p.test(line))) break;
      if (/^(?=.*\d)(?=.*[A-F])[A-F0-9]{12,35}$/i.test(line)) continue;
      filtered.push(line);
      if (filtered.length >= 7) break;
    }

    if (filtered.length === 0) return empty;

    // 2. İlk satır alıcı adı mı yoksa doğrudan sokak/şehir satırı mı?
    let recipientName = '';
    let idx = 0;
    while (idx < filtered.length) {
      const cand = filtered[idx];
      if (isInvalidPersonName(cand)) {
        idx++;
        continue;
      }
      if (looksLikeStreetOrCityLine(cand)) {
        break;
      }
      recipientName = cand;
      idx++;
      break;
    }

    const remaining = filtered.slice(idx);

    // IOSS numarası (IMxxxxxxxxxx)
    let iossNumber = '';
    const iossMatch = `${fullContextText} ${filtered.join(' ')}`.match(/\b(IM\d{10})\b/i);
    if (iossMatch) iossNumber = iossMatch[1].toUpperCase();

    // 3. Telefon satırı
    let phone = '';
    const phoneIdx = remaining.findIndex((l) => {
      if (/^(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-.]?\s*(\+?[\d\s\-().]{7,22})$/i.test(l)) return true;
      if (/^[+\d()\s.-]{7,22}$/.test(l) && !/^\d{5}(?:-\d{4})?$/.test(l)) {
        const d = l.replace(/\D/g, '');
        return d.length >= 7 && d.length <= 15;
      }
      return false;
    });
    if (phoneIdx !== -1) {
      const rawPhone = remaining[phoneIdx];
      const m = rawPhone.match(/(?:Tel|Phone|Mobile|Telefon|Cep)\s*[:\-.]?\s*(.+)/i);
      phone = (m ? m[1] : rawPhone).trim();
      remaining.splice(phoneIdx, 1);
    }

    // 4. Tek satırlık ülke adı
    let country = '';
    for (let i = remaining.length - 1; i >= 0; i--) {
      const resolved = resolveCountryName(remaining[i]);
      if (resolved) {
        country = resolved;
        remaining.splice(i, 1);
        break;
      }
    }

    let city = '';
    let state = '';
    let postalCode = '';

    // 5. Kapalı özet satırı: "Koblenz, Germany" veya "Toronto, Canada"
    for (let i = 0; i < remaining.length; i++) {
      const parts = remaining[i].split(',').map((p) => p.trim()).filter(Boolean);
      if (parts.length === 2) {
        const maybeCountry = resolveCountryName(parts[1]);
        if (maybeCountry) {
          city = parts[0];
          if (!country) country = maybeCountry;
          remaining.splice(i, 1);
          break;
        }
      }
    }

    // 6. Şehir / Eyalet / Posta Kodu kalıpları (US, Askeri APO/FPO, Kanada, UK, AB/Almanya)
    const usCommaRe = /^([A-Za-z\u00C0-\u017F \t.'-]+),\s*([A-Za-z]{2})(?:\s+(\d{5}(?:-\d{4})?))?$/;
    const usSpaceZipRe = /^([A-Za-z\u00C0-\u017F \t.'-]+?)\s+([A-Za-z]{2})\s+(\d{5}(?:-\d{4})?)$/;
    const caRe = /^([A-Za-z\u00C0-\u017F \t.'-]+),\s*([A-Za-z]{2})\s+([A-Za-z]\d[A-Za-z]\s*\d[A-Za-z]\d)$/;
    const euRe = /^((?:[A-Za-z]{1,2}-)?\d{4,5})\s+([A-Za-z\u00C0-\u017F\s.'-]+)$/;
    const ukRe = /^(.*?)\s*\b([A-Za-z]{1,2}\d[A-Za-z\d]?\s*\d[A-Za-z]{2})\b$/;

    let matchedLocIdx = -1;
    for (let i = remaining.length - 1; i >= 0; i--) {
      const line = remaining[i];
      let m = line.match(caRe);
      if (m && CA_PROVINCE_CODES.has(m[2].toUpperCase())) {
        city = m[1].trim();
        state = m[2].toUpperCase();
        postalCode = m[3].toUpperCase();
        if (!country) country = 'Canada';
        matchedLocIdx = i;
        break;
      }

      m = line.match(usCommaRe) || line.match(usSpaceZipRe);
      if (m) {
        const code = m[2].toUpperCase();
        if (US_STATE_CODES.has(code) || CA_PROVINCE_CODES.has(code)) {
          city = m[1].trim();
          state = code;
          postalCode = m[3] ? m[3].trim() : '';
          if (!country) {
            country = US_STATE_CODES.has(code) ? 'United States' : 'Canada';
          }
          matchedLocIdx = i;
          break;
        }
      }

      m = line.match(euRe);
      if (m && !/\b(?:Box|Apt|Unit|Ste|Suite|Rd|St|Ave|Blvd|Ln|Dr|Ct|Way)\b/i.test(line)) {
        postalCode = m[1].trim();
        city = m[2].trim();
        matchedLocIdx = i;
        break;
      }

      m = line.match(ukRe);
      if (m) {
        if (m[1].trim()) city = m[1].trim();
        postalCode = m[2].toUpperCase();
        if (!country) country = 'United Kingdom';
        matchedLocIdx = i;
        break;
      }
    }

    if (matchedLocIdx !== -1) {
      remaining.splice(matchedLocIdx, 1);
    }

    // 7. Geriye kalan satırlar: Cadde 1 ve Cadde 2 (Daire / Bina)
    // Kapalı uluslararası özet koruması: Yalnızca tek satır kaldıysa, rakam/sokak belirteci yoksa ve şehir henüz boşsa bu satır özet şehirdir (sokak değil!)
    let streetAddress = '';
    let secondAddress = '';
    if (
      remaining.length === 1 &&
      !city &&
      !postalCode &&
      !/\d/.test(remaining[0]) &&
      !/\b(?:Box|PO\s*Box|Street|St|Road|Rd|Avenue|Ave|Boulevard|Blvd|Lane|Ln|Drive|Dr|Court|Ct|Way|Str|Straße|Strasse|Rue|Via|Calle|Sokak|Cadde|Mah)\b/i.test(remaining[0])
    ) {
      const summaryParts = remaining[0].split(',').map((p) => p.trim()).filter(Boolean);
      city = summaryParts[0] || remaining[0];
      if (summaryParts.length >= 2 && !state) {
        state = summaryParts[1];
      }
    } else if (remaining.length > 0) {
      streetAddress = remaining[0];
      if (remaining.length > 1) {
        secondAddress = remaining.slice(1).join(', ');
      }
    }

    if (!country && state) {
      if (US_STATE_CODES.has(state)) country = 'United States';
      else if (CA_PROVINCE_CODES.has(state)) country = 'Canada';
    }

    const isFullAddress = Boolean(
      streetAddress &&
      (city || state || postalCode) &&
      streetAddress.toLowerCase() !== city.toLowerCase() &&
      !resolveCountryName(streetAddress)
    );

    const rawParts = [
      recipientName,
      streetAddress,
      secondAddress,
      [postalCode, city, state].filter(Boolean).join(' '),
      country,
      phone ? `Tel: ${phone}` : '',
      iossNumber ? `IOSS: ${iossNumber}` : ''
    ].filter(Boolean);

    return {
      recipientName,
      streetAddress,
      secondAddress,
      city,
      state,
      postalCode,
      country,
      phone,
      iossNumber,
      rawAddressText: rawParts.join('\n'),
      isFullAddress
    };
  }

  const api = Object.freeze({
    resolveCountryName,
    isInvalidPersonName,
    looksLikeStreetOrCityLine,
    parseFlexibleMoneyNumber,
    parseAddressLines
  });

  root.EtsyCopilotAddressParser = api;
  if (typeof module !== 'undefined' && module.exports) {
    module.exports = api;
  }
})(typeof globalThis !== 'undefined' ? globalThis : window);
