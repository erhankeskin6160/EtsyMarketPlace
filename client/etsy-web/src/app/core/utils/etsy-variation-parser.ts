export interface ParsedInlineOptionPrice {
  cleanValue: string;
  priceUsd?: number;
  deltaUsd?: number;
  active: boolean;
}

export interface ParsedVariationRow {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
}

export interface ParsedVariationResult {
  hasVariations: boolean;
  varType1: string;
  varValues1: string;
  enableVar2: boolean;
  varType2: string;
  varValues2: string;
  variationRows: ParsedVariationRow[];
  sourceFormat: 'json' | 'html' | 'text' | 'none';
}

interface InternalOption {
  value: string;
  priceUsd?: number;
  deltaUsd?: number;
  active: boolean;
}

interface InternalGroup {
  name: string;
  options: InternalOption[];
}

const PLACEHOLDER_TEXTS = new Set([
  'select an option',
  'choose an option',
  'select a color',
  'select a size',
  'select a style',
  'bir seçenek seçin',
  'seçiniz',
  'bir renk seçin',
  'bir boyut seçin'
]);

const STOP_LINES = new Set([
  'quantity',
  'miktar',
  'buy it now',
  'şimdi satın al',
  'add to cart',
  'sepete ekle',
  'add to collection',
  'koleksiyona elde',
  'koleksiyona ekle',
  'personalization',
  'kişiselleştirme',
  'add your personalization',
  'highlights',
  'item details',
  'shipping and return policies'
]);

/**
 * Chrome eklentisi gerektirmeden;
 * 1) Bookmarklet JSON çıktısını,
 * 2) Etsy HTML (<select id="variation-selector-*">) kodunu veya
 * 3) Etsy ürün sayfasından Ctrl+A -> Ctrl+C ile kopyalanan düz metni
 * otomatik olarak varyasyon gruplarına ve fiyatlı kombinasyon tablosuna dönüştürür.
 */
export function parseEtsyVariationsFromClipboard(
  rawInput: string,
  fallbackBasePrice = 0,
  usdTryRate = 50
): ParsedVariationResult {
  const trimmed = (rawInput || '').trim();
  if (!trimmed) {
    return emptyResult();
  }

  // 1. JSON (Bookmarklet veya API formatı)
  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    const jsonRes = tryParseBookmarkletJson(trimmed, fallbackBasePrice, usdTryRate);
    if (jsonRes.hasVariations) return jsonRes;
  }

  // 2. HTML (<select id="variation-selector-...">)
  if (/<select\b[^>]*variation-selector-/i.test(trimmed) || /<option\b/i.test(trimmed)) {
    const htmlRes = tryParseEtsyHtml(trimmed, fallbackBasePrice, usdTryRate);
    if (htmlRes.hasVariations) return htmlRes;
  }

  // 3. Etsy sayfasından Ctrl+A -> Ctrl+C ile kopyalanan düz metin
  const textRes = tryParseCopiedEtsyText(trimmed, fallbackBasePrice, usdTryRate);
  if (textRes.hasVariations) return textRes;

  return emptyResult();
}

export function buildEtsyVariationBookmarkletUrl(): string {
  const fn = `(() => {
    const sels = Array.from(document.querySelectorAll('select[id^="variation-selector-"]'));
    if (!sels.length) { alert('Bu sayfada Etsy varyasyon seçimi bulunamadı.'); return; }
    const groups = sels.map((sel, idx) => {
      const lbl = document.querySelector('label[for="' + sel.id + '"]')?.innerText || ('Variation ' + (idx + 1));
      const cleanName = lbl.replace(/^(Choose your|Select (a|your)|Renginizi|Tarzınızı)\\s+/i, '').replace(/seçin$/i, '').replace(/[:*]/g, '').trim() || ('Variation ' + (idx + 1));
      const options = Array.from(sel.options).map(o => o.text.trim()).filter(t => t && !/^(select|choose|bir seçenek|seçiniz)/i.test(t));
      return { name: cleanName, options };
    }).filter(g => g.options.length > 0);
    const ld = document.querySelector('script[type="application/ld+json"]')?.textContent || '';
    const m = ld.match(/"(?:lowPrice|price)"\\s*:\\s*"?(\\d+(?:[.,]\\d{1,2})?)"?/i);
    const cm = ld.match(/"priceCurrency"\\s*:\\s*"([A-Z]{3})"/i);
    const basePrice = m ? parseFloat(m[1].replace(',', '.')) : 0;
    const currency = cm ? cm[1].toUpperCase() : 'USD';
    const payload = JSON.stringify({ etsyMarketPlaceVariations: true, basePrice, currency, groups });
    navigator.clipboard?.writeText(payload).then(
      () => alert('✅ ' + groups.length + ' varyasyon grubu panoya kopyalandı! Şimdi Fast Creator kutusuna Ctrl+V ile yapıştırın.'),
      () => prompt('Aşağıdaki varyasyon verisini kopyalayın (Ctrl+C):', payload)
    );
  })();`;
  return 'javascript:' + encodeURIComponent(fn.replace(/\s+/g, ' ').trim());
}

export function parseInlineVariationOptionPrice(rawItem: string, usdTryRate = 50): ParsedInlineOptionPrice {
  let text = String(rawItem || '').replace(/\s+/g, ' ').trim();
  if (!text) {
    return { cleanValue: '', active: true };
  }

  let active = true;
  const soldOutRegex = /\(\s*(?:sold\s*out|out\s*of\s*stock|tükendi|stokta\s*yok)\s*\)/gi;
  if (soldOutRegex.test(text)) {
    active = false;
    text = text.replace(soldOutRegex, '').replace(/\s+/g, ' ').trim();
  }

  let priceUsd: number | undefined;
  let deltaUsd: number | undefined;

  const bracketRegex = /(?:\(\s*|\[\s*)(\+\s*)?(?:(US\s*\$|\$|USD|EUR|€|GBP|£|TRY|₺|TL)\s*)?(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)\s*(USD|EUR|GBP|TRY|TL|₺|\$|€|£|ABD\s*doları)?\s*(?:\)|\])\s*$/i;
  const sepRegex = /\s*[-–:]\s*(\+\s*)?(?:(US\s*\$|\$|USD|EUR|€|GBP|£|TRY|₺|TL)\s*)?(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)\s*(USD|EUR|GBP|TRY|TL|₺|\$|€|£|ABD\s*doları)?\s*$/i;

  const bracketMatch = text.match(bracketRegex);
  const sepMatch = !bracketMatch ? text.match(sepRegex) : null;
  const match = bracketMatch || sepMatch;

  if (match && match.index !== undefined) {
    const hasPlus = Boolean(match[1]);
    const prefixUnit = (match[2] || '').trim();
    const numStr = (match[3] || '').trim();
    const suffixUnit = (match[4] || '').trim();
    const hasCurrency = Boolean(prefixUnit || suffixUnit);

    const canAccept = bracketMatch ? (hasCurrency || hasPlus) : hasCurrency;
    if (canAccept) {
      const parsedAmount = parseLocalizedNumber(numStr);
      if (Number.isFinite(parsedAmount) && parsedAmount >= 0) {
        const usdAmount = convertParsedAmountToUsd(parsedAmount, `${prefixUnit} ${suffixUnit}`, usdTryRate);
        if (hasPlus) {
          deltaUsd = usdAmount;
        } else {
          priceUsd = usdAmount;
        }
        text = text.slice(0, match.index).replace(/\s+/g, ' ').trim();
      }
    }
  }

  return {
    cleanValue: text,
    priceUsd,
    deltaUsd,
    active
  };
}

function parseLocalizedNumber(rawNum: string): number {
  const s = String(rawNum || '').trim();
  if (!s) return Number.NaN;

  const hasComma = s.includes(',');
  const hasDot = s.includes('.');

  if (hasComma && hasDot) {
    if (s.lastIndexOf(',') > s.lastIndexOf('.')) {
      return Number.parseFloat(s.replace(/\./g, '').replace(',', '.'));
    }
    return Number.parseFloat(s.replace(/,/g, ''));
  }

  if (hasComma) {
    if (/^\d{1,3}(?:,\d{3})+$/.test(s)) {
      return Number.parseFloat(s.replace(/,/g, ''));
    }
    return Number.parseFloat(s.replace(',', '.'));
  }

  if (hasDot) {
    if (/^\d{1,3}(?:\.\d{3})+$/.test(s)) {
      return Number.parseFloat(s.replace(/\./g, ''));
    }
    return Number.parseFloat(s);
  }

  return Number.parseFloat(s);
}

function convertParsedAmountToUsd(amount: number, unitText: string, usdTryRate: number): number {
  const isTryCurrency = /(?:^|\b)(?:tl|try)(?:\b|$)|₺/i.test(unitText || '');
  if (isTryCurrency) {
    const rate = Number(usdTryRate) > 0 ? Number(usdTryRate) : 50;
    return Number((amount / rate).toFixed(2));
  }
  return Number(amount.toFixed(2));
}

function tryParseBookmarkletJson(raw: string, fallbackBasePrice: number, usdTryRate: number): ParsedVariationResult {
  try {
    const data = JSON.parse(raw);
    const groupsRaw = Array.isArray(data?.groups) ? data.groups : (Array.isArray(data) ? data : null);
    if (!groupsRaw || groupsRaw.length === 0) return emptyResult();

    let basePrice = typeof data?.basePrice === 'number' && data.basePrice > 0 ? data.basePrice : fallbackBasePrice;
    if (typeof data?.basePrice === 'number' && data.basePrice > 0 && String(data?.currency || '').toUpperCase() === 'TRY') {
      basePrice = convertParsedAmountToUsd(data.basePrice, 'TRY', usdTryRate);
    }

    const groups: InternalGroup[] = [];

    for (const g of groupsRaw) {
      const name = cleanGroupLabel(String(g?.name || `Variation ${groups.length + 1}`));
      const rawOpts: string[] = Array.isArray(g?.options)
        ? g.options.map((x: any) => String(x))
        : (Array.isArray(g?.values) ? g.values.map((x: any) => String(x)) : []);

      const options = parseOptionStringList(rawOpts, usdTryRate);
      if (options.length > 0) {
        groups.push({ name, options });
      }
    }

    return buildResultFromGroups(groups, basePrice, 'json');
  } catch {
    return emptyResult();
  }
}

function tryParseEtsyHtml(html: string, fallbackBasePrice: number, usdTryRate: number): ParsedVariationResult {
  const ldMatch = html.match(/"(?:lowPrice|price)"\s*:\s*"?(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)"?/i);
  const currencyMatch = html.match(/"priceCurrency"\s*:\s*"([A-Z]{3})"/i);
  let basePrice = ldMatch ? parseLocalizedNumber(ldMatch[1]) : fallbackBasePrice;
  if (ldMatch && currencyMatch && currencyMatch[1].toUpperCase() === 'TRY') {
    basePrice = convertParsedAmountToUsd(basePrice, 'TRY', usdTryRate);
  }

  const selectRegex = /<select\b[^>]*\bid=["']variation-selector-(\d+)["'][^>]*>([\s\S]*?)<\/select>/gi;
  const optionRegex = /<option\b[^>]*>([\s\S]*?)<\/option>/gi;
  const groups: Array<{ idx: number; group: InternalGroup }> = [];

  let selMatch: RegExpExecArray | null;
  while ((selMatch = selectRegex.exec(html)) !== null) {
    const idx = parseInt(selMatch[1], 10) || groups.length;
    const body = selMatch[2];
    const lblRegex = new RegExp(`<label\\b[^>]*\\bfor=["']variation-selector-${idx}["'][^>]*>([\\s\\S]*?)<\\/label>`, 'i');
    const lblMatch = html.match(lblRegex);
    const name = lblMatch
      ? cleanGroupLabel(stripTags(decodeHtml(lblMatch[1])))
      : `Variation ${idx + 1}`;

    const rawOpts: string[] = [];
    let optMatch: RegExpExecArray | null;
    optionRegex.lastIndex = 0;
    while ((optMatch = optionRegex.exec(body)) !== null) {
      rawOpts.push(stripTags(decodeHtml(optMatch[1])));
    }

    const options = parseOptionStringList(rawOpts, usdTryRate);
    if (options.length > 0) {
      groups.push({ idx, group: { name, options } });
    }
  }

  if (groups.length === 0) return emptyResult();
  groups.sort((a, b) => a.idx - b.idx);
  return buildResultFromGroups(groups.map(g => g.group), basePrice, 'html');
}

function tryParseCopiedEtsyText(text: string, fallbackBasePrice: number, usdTryRate: number): ParsedVariationResult {
  const lines = text
    .split(/\r?\n/)
    .map(l => l.replace(/\s+/g, ' ').trim())
    .filter(l => l.length > 0);

  if (lines.length < 2) return emptyResult();

  // Sayfa içi baz fiyat tespiti ("Now US$7.50+", "Şimdi 7,50 ABD doları+", "Şimdi 450,00 TL+")
  let detectedBasePrice = fallbackBasePrice;
  for (const line of lines) {
    const bp = line.match(/(?:Now|Şimdi)\s*(?:(US\s*\$|\$|USD|TRY|₺|TL)\s*)?(\d{1,3}(?:[.,]\d{3})*(?:[.,]\d{1,2})?|\d+(?:[.,]\d{1,2})?)\s*(USD|TRY|TL|₺|\$|ABD\s*doları)?/i);
    if (bp) {
      const parsedBase = parseLocalizedNumber(bp[2]);
      if (Number.isFinite(parsedBase) && parsedBase > 0) {
        detectedBasePrice = convertParsedAmountToUsd(parsedBase, `${bp[1] || ''} ${bp[3] || ''}`, usdTryRate);
        break;
      }
    }
  }

  const groups: InternalGroup[] = [];
  const headerRegex = /^(?:Choose\s+your\s+|Select\s+(?:a|your)\s+|Renginizi\s+seçin|Tarzınızı\s+seçin|Boyutunuzu\s+seçin|(.+?)\s+seçin$)/i;

  let i = 0;
  while (i < lines.length) {
    const line = lines[i];
    const isHeader = headerRegex.test(line) ||
      (i + 1 < lines.length && PLACEHOLDER_TEXTS.has(lines[i + 1].toLowerCase()));

    if (isHeader && !PLACEHOLDER_TEXTS.has(line.toLowerCase()) && !STOP_LINES.has(line.toLowerCase())) {
      const groupName = cleanGroupLabel(line);
      let j = i + 1;
      if (j < lines.length && PLACEHOLDER_TEXTS.has(lines[j].toLowerCase())) {
        j++;
      }

      const rawOpts: string[] = [];
      while (j < lines.length) {
        const candidate = lines[j];
        const lower = candidate.toLowerCase();

        if (STOP_LINES.has(lower) || /^\d+$/.test(candidate)) {
          break;
        }
        if (headerRegex.test(candidate) || (j + 1 < lines.length && PLACEHOLDER_TEXTS.has(lines[j + 1].toLowerCase()))) {
          break;
        }
        if (!PLACEHOLDER_TEXTS.has(lower) && candidate.length <= 90) {
          rawOpts.push(candidate);
        }
        j++;
      }

      const parsedOpts = parseOptionStringList(rawOpts, usdTryRate);
      if (parsedOpts.length > 0) {
        groups.push({ name: groupName, options: parsedOpts });
        i = j;
        continue;
      }
    }
    i++;
  }

  return buildResultFromGroups(groups, detectedBasePrice, 'text');
}

function parseOptionStringList(rawOpts: string[], usdTryRate: number): InternalOption[] {
  const options: InternalOption[] = [];
  const seen = new Set<string>();

  for (const rawItem of rawOpts) {
    const rawTrimmed = String(rawItem || '').replace(/\s+/g, ' ').trim();
    if (!rawTrimmed || PLACEHOLDER_TEXTS.has(rawTrimmed.toLowerCase())) continue;

    const parsed = parseInlineVariationOptionPrice(rawTrimmed, usdTryRate);
    const text = parsed.cleanValue;

    if (!text || PLACEHOLDER_TEXTS.has(text.toLowerCase()) || seen.has(text.toLowerCase())) continue;
    seen.add(text.toLowerCase());
    options.push({
      value: text,
      priceUsd: parsed.priceUsd,
      deltaUsd: parsed.deltaUsd,
      active: parsed.active
    });
  }

  return options;
}

export function resolveVariationOptionPairPrice(
  basePrice: number,
  o1: { priceUsd?: number; deltaUsd?: number },
  o2?: { priceUsd?: number; deltaUsd?: number }
): number | undefined {
  const hasAny1 = o1.priceUsd !== undefined || o1.deltaUsd !== undefined;
  const hasAny2 = Boolean(o2 && (o2.priceUsd !== undefined || o2.deltaUsd !== undefined));
  if (!hasAny1 && !hasAny2) {
    return undefined;
  }

  let price = basePrice;
  if (!o2) {
    price = o1.priceUsd ?? (o1.deltaUsd !== undefined ? basePrice + o1.deltaUsd : basePrice);
  } else if (o2.priceUsd !== undefined && o1.priceUsd === undefined) {
    price = o2.priceUsd + (o1.deltaUsd ?? 0);
  } else if (o1.priceUsd !== undefined && o2.priceUsd === undefined) {
    price = o1.priceUsd + (o2.deltaUsd ?? 0);
  } else if (o1.priceUsd !== undefined && o2.priceUsd !== undefined) {
    price = Math.max(o1.priceUsd, o2.priceUsd);
  } else {
    price = basePrice + (o1.deltaUsd ?? 0) + (o2.deltaUsd ?? 0);
  }

  return Number(price.toFixed(2));
}

function buildResultFromGroups(
  groups: InternalGroup[],
  basePrice: number,
  sourceFormat: 'json' | 'html' | 'text'
): ParsedVariationResult {
  if (!groups || groups.length === 0) return emptyResult();

  const g1 = groups[0];
  const g2 = groups.length > 1 ? groups[1] : undefined;
  const rows: ParsedVariationRow[] = [];

  if (!g2) {
    for (const o1 of g1.options) {
      const price = resolveVariationOptionPairPrice(basePrice, o1) ?? Number(basePrice.toFixed(2));
      rows.push({
        key: o1.value,
        price,
        quantity: o1.active ? 10 : 0,
        active: o1.active
      });
    }
  } else {
    for (const o1 of g1.options) {
      for (const o2 of g2.options) {
        const price = resolveVariationOptionPairPrice(basePrice, o1, o2) ?? Number(basePrice.toFixed(2));
        const active = o1.active && o2.active;
        rows.push({
          key: `${o1.value} / ${o2.value}`,
          price,
          quantity: active ? 10 : 0,
          active
        });
      }
    }
  }

  return {
    hasVariations: true,
    varType1: g1.name,
    varValues1: g1.options.map(o => o.value).join(', '),
    enableVar2: !!g2,
    varType2: g2 ? g2.name : '',
    varValues2: g2 ? g2.options.map(o => o.value).join(', ') : '',
    variationRows: rows,
    sourceFormat
  };
}

function cleanGroupLabel(raw: string): string {
  let text = (raw || '').replace(/\s+/g, ' ').trim();
  text = text.replace(/^(?:Choose\s+your\s+|Select\s+(?:a|your)\s+|Renginizi\s+|Tarzınızı\s+|Boyutunuzu\s+)/i, '').trim();
  if (/seçin$/i.test(text)) {
    text = text.replace(/seçin$/i, '').trim();
  }
  text = text.replace(/[:*]+$/, '').trim();
  if (!text) return 'Option';
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function stripTags(html: string): string {
  return html.replace(/<[^>]+>/g, ' ').replace(/\s+/g, ' ').trim();
}

function decodeHtml(str: string): string {
  const txt = typeof document !== 'undefined' ? document.createElement('textarea') : null;
  if (txt) {
    txt.innerHTML = str;
    return txt.value;
  }
  return str
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'");
}

function emptyResult(): ParsedVariationResult {
  return {
    hasVariations: false,
    varType1: '',
    varValues1: '',
    enableVar2: false,
    varType2: '',
    varValues2: '',
    variationRows: [],
    sourceFormat: 'none'
  };
}
