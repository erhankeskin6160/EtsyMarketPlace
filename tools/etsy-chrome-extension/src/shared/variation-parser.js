// Etsy Copilot v2.7.0 — Etsy İlan Varyasyon Ayrıştırıcı (HTML + DOM Grupları)

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

function decodeBasicHtml(str) {
  return String(str || '')
    .replace(/&amp;/g, '&')
    .replace(/&lt;/g, '<')
    .replace(/&gt;/g, '>')
    .replace(/&quot;/g, '"')
    .replace(/&#39;/g, "'")
    .replace(/&#x27;/gi, "'")
    .replace(/&nbsp;/g, ' ');
}

function stripTags(html) {
  return decodeBasicHtml(String(html || '').replace(/<[^>]+>/g, ' '))
    .replace(/\s+/g, ' ')
    .trim();
}

export function cleanGroupLabel(raw) {
  let text = String(raw || '').replace(/\s+/g, ' ').trim();
  text = text.replace(/^(?:Choose\s+your\s+|Select\s+(?:a|your)\s+|Renginizi\s+|Tarzınızı\s+|Boyutunuzu\s+)/i, '').trim();
  if (/seçin$/i.test(text)) {
    text = text.replace(/seçin$/i, '').trim();
  }
  text = text.replace(/[:*]+$/, '').trim();
  if (!text) return 'Option';
  return text.charAt(0).toUpperCase() + text.slice(1);
}

function parseOptionStringList(rawOpts) {
  const options = [];
  const seen = new Set();

  for (const rawItem of rawOpts || []) {
    let text = String(rawItem || '').replace(/\s+/g, ' ').trim();
    if (!text || PLACEHOLDER_TEXTS.has(text.toLowerCase())) continue;

    let active = true;
    const soldOutRegex = /\(\s*(?:sold\s*out|out\s*of\s*stock|tükendi|stokta\s*yok)\s*\)/i;
    if (soldOutRegex.test(text)) {
      active = false;
      text = text.replace(soldOutRegex, '').trim();
    }

    let priceUsd;
    let deltaUsd;

    const priceRegex = /\(\s*(\+\s*)?(?:US\s*\$|\$|USD|EUR|€|GBP|£|TRY|₺)?\s*(\d+(?:[.,]\d{1,2})?)\s*(?:USD|EUR|GBP|TRY|ABD\s*doları)?\s*\)\s*$/i;
    const priceMatch = text.match(priceRegex);
    if (priceMatch) {
      const val = parseFloat(priceMatch[2].replace(',', '.'));
      if (!Number.isNaN(val)) {
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

export function buildInventoryFromExtractedGroups(listingId, rawGroups, basePrice = 0, source = 'chrome_extension') {
  const normalizedGroups = [];
  for (const g of rawGroups || []) {
    const name = cleanGroupLabel(g?.name || `Variation ${normalizedGroups.length + 1}`);
    const options = parseOptionStringList(g?.options || []);
    if (options.length > 0) {
      normalizedGroups.push({ name, options });
    }
  }

  if (normalizedGroups.length === 0) {
    return {
      listingId: Number(listingId) || 0,
      hasVariations: false,
      groups: [],
      offerings: [],
      source
    };
  }

  const g1 = normalizedGroups[0];
  const g2 = normalizedGroups.length > 1 ? normalizedGroups[1] : null;
  const offerings = [];
  const safeBase = Number(basePrice) > 0 ? Number(basePrice) : 0;

  if (!g2) {
    for (const o1 of g1.options) {
      const price = o1.priceUsd ?? (o1.deltaUsd !== undefined ? safeBase + o1.deltaUsd : safeBase);
      offerings.push({
        key: o1.value,
        price: Number(price.toFixed(2)),
        quantity: o1.active ? 10 : 0,
        active: o1.active
      });
    }
  } else {
    for (const o1 of g1.options) {
      for (const o2 of g2.options) {
        let price = safeBase;
        if (o2.priceUsd !== undefined && o1.priceUsd === undefined) {
          price = o2.priceUsd + (o1.deltaUsd ?? 0);
        } else if (o1.priceUsd !== undefined && o2.priceUsd === undefined) {
          price = o1.priceUsd + (o2.deltaUsd ?? 0);
        } else if (o1.priceUsd !== undefined && o2.priceUsd !== undefined) {
          price = Math.max(o1.priceUsd, o2.priceUsd);
        } else {
          price = safeBase + (o1.deltaUsd ?? 0) + (o2.deltaUsd ?? 0);
        }

        const active = o1.active && o2.active;
        offerings.push({
          key: `${o1.value} / ${o2.value}`,
          price: Number(price.toFixed(2)),
          quantity: active ? 10 : 0,
          active
        });
      }
    }
  }

  return {
    listingId: Number(listingId) || 0,
    hasVariations: true,
    varType1: g1.name,
    varValues1: g1.options.map((o) => o.value).join(', '),
    enableVar2: Boolean(g2),
    varType2: g2 ? g2.name : undefined,
    varValues2: g2 ? g2.options.map((o) => o.value).join(', ') : undefined,
    groups: normalizedGroups.map((g) => ({
      name: g.name,
      values: g.options.map((o) => o.value)
    })),
    offerings,
    source
  };
}

export function parseVariationsFromListingHtml(listingId, html) {
  const rawHtml = String(html || '');
  if (!rawHtml) {
    return buildInventoryFromExtractedGroups(listingId, [], 0, 'chrome_extension');
  }

  const ldMatch = rawHtml.match(/"(?:lowPrice|price)"\s*:\s*"?(\d+(?:[.,]\d{1,2})?)"?/i);
  const basePrice = ldMatch ? parseFloat(ldMatch[1].replace(',', '.')) : 0;

  const selectRegex = /<select\b[^>]*\bid=["']variation-selector-(\d+)["'][^>]*>([\s\S]*?)<\/select>/gi;
  const optionRegex = /<option\b[^>]*>([\s\S]*?)<\/option>/gi;
  const extracted = [];

  let selMatch;
  while ((selMatch = selectRegex.exec(rawHtml)) !== null) {
    const idx = parseInt(selMatch[1], 10) || extracted.length;
    const body = selMatch[2];
    const lblRegex = new RegExp(`<label\\b[^>]*\\bfor=["']variation-selector-${idx}["'][^>]*>([\\s\\S]*?)<\\/label>`, 'i');
    const lblMatch = rawHtml.match(lblRegex);
    const name = lblMatch ? stripTags(lblMatch[1]) : `Variation ${idx + 1}`;

    const options = [];
    let optMatch;
    optionRegex.lastIndex = 0;
    while ((optMatch = optionRegex.exec(body)) !== null) {
      options.push(stripTags(optMatch[1]));
    }

    if (options.length > 0) {
      extracted.push({ idx, name, options });
    }
  }

  extracted.sort((a, b) => a.idx - b.idx);
  return buildInventoryFromExtractedGroups(listingId, extracted, basePrice, 'chrome_extension');
}
