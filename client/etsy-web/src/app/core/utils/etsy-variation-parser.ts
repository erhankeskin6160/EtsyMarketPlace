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
export function parseEtsyVariationsFromClipboard(rawInput: string, fallbackBasePrice = 0): ParsedVariationResult {
  const trimmed = (rawInput || '').trim();
  if (!trimmed) {
    return emptyResult();
  }

  // 1. JSON (Bookmarklet veya API formatı)
  if (trimmed.startsWith('{') || trimmed.startsWith('[')) {
    const jsonRes = tryParseBookmarkletJson(trimmed, fallbackBasePrice);
    if (jsonRes.hasVariations) return jsonRes;
  }

  // 2. HTML (<select id="variation-selector-...">)
  if (/<select\b[^>]*variation-selector-/i.test(trimmed) || /<option\b/i.test(trimmed)) {
    const htmlRes = tryParseEtsyHtml(trimmed, fallbackBasePrice);
    if (htmlRes.hasVariations) return htmlRes;
  }

  // 3. Etsy sayfasından Ctrl+A -> Ctrl+C ile kopyalanan düz metin
  const textRes = tryParseCopiedEtsyText(trimmed, fallbackBasePrice);
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
    const basePrice = m ? parseFloat(m[1].replace(',', '.')) : 0;
    const payload = JSON.stringify({ etsyMarketPlaceVariations: true, basePrice, groups });
    navigator.clipboard?.writeText(payload).then(
      () => alert('✅ ' + groups.length + ' varyasyon grubu panoya kopyalandı! Şimdi Fast Creator kutusuna Ctrl+V ile yapıştırın.'),
      () => prompt('Aşağıdaki varyasyon verisini kopyalayın (Ctrl+C):', payload)
    );
  })();`;
  return 'javascript:' + encodeURIComponent(fn.replace(/\s+/g, ' ').trim());
}

function tryParseBookmarkletJson(raw: string, fallbackBasePrice: number): ParsedVariationResult {
  try {
    const data = JSON.parse(raw);
    const groupsRaw = Array.isArray(data?.groups) ? data.groups : (Array.isArray(data) ? data : null);
    if (!groupsRaw || groupsRaw.length === 0) return emptyResult();

    const basePrice = typeof data?.basePrice === 'number' && data.basePrice > 0 ? data.basePrice : fallbackBasePrice;
    const groups: InternalGroup[] = [];

    for (const g of groupsRaw) {
      const name = cleanGroupLabel(String(g?.name || `Variation ${groups.length + 1}`));
      const rawOpts: string[] = Array.isArray(g?.options)
        ? g.options.map((x: any) => String(x))
        : (Array.isArray(g?.values) ? g.values.map((x: any) => String(x)) : []);

      const options = parseOptionStringList(rawOpts);
      if (options.length > 0) {
        groups.push({ name, options });
      }
    }

    return buildResultFromGroups(groups, basePrice, 'json');
  } catch {
    return emptyResult();
  }
}

function tryParseEtsyHtml(html: string, fallbackBasePrice: number): ParsedVariationResult {
  const ldMatch = html.match(/"(?:lowPrice|price)"\s*:\s*"?(\d+(?:[.,]\d{1,2})?)"?/i);
  const basePrice = ldMatch ? parseFloat(ldMatch[1].replace(',', '.')) : fallbackBasePrice;

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

    const options = parseOptionStringList(rawOpts);
    if (options.length > 0) {
      groups.push({ idx, group: { name, options } });
    }
  }

  if (groups.length === 0) return emptyResult();
  groups.sort((a, b) => a.idx - b.idx);
  return buildResultFromGroups(groups.map(g => g.group), basePrice, 'html');
}

function tryParseCopiedEtsyText(text: string, fallbackBasePrice: number): ParsedVariationResult {
  const lines = text
    .split(/\r?\n/)
    .map(l => l.replace(/\s+/g, ' ').trim())
    .filter(l => l.length > 0);

  if (lines.length < 2) return emptyResult();

  // Sayfa içi baz fiyat tespiti ("Now US$7.50+", "Şimdi 7,50 ABD doları+")
  let detectedBasePrice = fallbackBasePrice;
  for (const line of lines) {
    const bp = line.match(/(?:Now|Şimdi)\s*(?:US\s*\$|\$|USD)?\s*(\d+(?:[.,]\d{1,2})?)/i);
    if (bp) {
      detectedBasePrice = parseFloat(bp[1].replace(',', '.'));
      break;
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

      const parsedOpts = parseOptionStringList(rawOpts);
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

function parseOptionStringList(rawOpts: string[]): InternalOption[] {
  const options: InternalOption[] = [];
  const seen = new Set<string>();

  for (const rawItem of rawOpts) {
    let text = rawItem.replace(/\s+/g, ' ').trim();
    if (!text || PLACEHOLDER_TEXTS.has(text.toLowerCase())) continue;

    let active = true;
    const soldOutRegex = /\(\s*(?:sold\s*out|out\s*of\s*stock|tükendi|stokta\s*yok)\s*\)/i;
    if (soldOutRegex.test(text)) {
      active = false;
      text = text.replace(soldOutRegex, '').trim();
    }

    let priceUsd: number | undefined;
    let deltaUsd: number | undefined;

    const priceRegex = /\(\s*(\+\s*)?(?:US\s*\$|\$|USD|EUR|€|GBP|£|TRY|₺)?\s*(\d+(?:[.,]\d{1,2})?)\s*(?:USD|EUR|GBP|TRY|ABD\s*doları)?\s*\)\s*$/i;
    const priceMatch = text.match(priceRegex);
    if (priceMatch) {
      const val = parseFloat(priceMatch[2].replace(',', '.'));
      if (!isNaN(val)) {
        if (priceMatch[1]) {
          deltaUsd = val;
        } else {
          priceUsd = val;
        }
      }
      text = text.slice(0, priceMatch.index).trim();
    }

    if (!text || seen.has(text.toLowerCase())) continue;
    seen.add(text.toLowerCase());
    options.push({ value: text, priceUsd, deltaUsd, active });
  }

  return options;
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
      const price = o1.priceUsd ?? (o1.deltaUsd !== undefined ? basePrice + o1.deltaUsd : basePrice);
      rows.push({
        key: o1.value,
        price: Number(price.toFixed(2)),
        quantity: o1.active ? 10 : 0,
        active: o1.active
      });
    }
  } else {
    for (const o1 of g1.options) {
      for (const o2 of g2.options) {
        let price = basePrice;
        if (o2.priceUsd !== undefined && o1.priceUsd === undefined) {
          price = o2.priceUsd + (o1.deltaUsd ?? 0);
        } else if (o1.priceUsd !== undefined && o2.priceUsd === undefined) {
          price = o1.priceUsd + (o2.deltaUsd ?? 0);
        } else if (o1.priceUsd !== undefined && o2.priceUsd !== undefined) {
          price = Math.max(o1.priceUsd, o2.priceUsd);
        } else {
          price = basePrice + (o1.deltaUsd ?? 0) + (o2.deltaUsd ?? 0);
        }

        const active = o1.active && o2.active;
        rows.push({
          key: `${o1.value} / ${o2.value}`,
          price: Number(price.toFixed(2)),
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
