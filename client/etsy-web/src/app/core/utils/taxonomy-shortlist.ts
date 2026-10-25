/**
 * Etsy Seller Taxonomy kısa liste (shortlist) motoru.
 *
 * Amaç: AI'ya ~3000 düğümlük canlı Etsy kategori ağacının tamamını göndermek yerine,
 * ürün başlığı/açıklamasıyla en alakalı gerçek kategorileri seçip prompt'a "yalnızca bu
 * listeden seç" kısıtıyla eklemek. Böylece AI ezberden/uydurma taxonomy_id döndürmez.
 *
 * Saf fonksiyonlardır (Angular bağımlılığı yok) — birim testi: taxonomy-shortlist.spec.ts
 */

export interface TaxonomyNodeLike {
  id: number;
  name: string;
  path: string;
}

const STOPWORDS = new Set<string>([
  // EN
  'a', 'an', 'and', 'or', 'the', 'of', 'for', 'with', 'without', 'to', 'in', 'on', 'by', 'from', 'at', 'as',
  'is', 'it', 'this', 'that', 'your', 'you', 'my', 'our', 'set', 'pack', 'piece', 'pcs', 'gift', 'gifts',
  'idea', 'ideas', 'best', 'new', 'custom', 'customized', 'personalized', 'personalised', 'handmade',
  'unique', 'cute', 'perfect', 'great', 'premium', 'quality', 'style', 'design', 'modern', 'color',
  'colour', 'colors', 'colours', 'changing', 'size', 'large', 'small', 'mini', 'big', 'made', 'item',
  'him', 'her', 'men', 'women', 'birthday', 'christmas', 'present', 'other', 'more',
  // TR
  've', 'ile', 'için', 'bir', 'bu', 'şu', 'da', 'de', 'çok', 'en', 'olarak', 'hediye', 'hediyelik',
  'özel', 'yeni', 'el', 'yapımı', 'renk', 'renkli', 'adet', 'ürün', 'ürünü'
]);

/** Basit İngilizce tekilleştirme: lamps→lamp, stands→stand, accessories→accessory, boxes→box. */
export function singularizeToken(token: string): string {
  if (token.length <= 3) return token;
  if (token.endsWith('ies') && token.length > 4) return token.slice(0, -3) + 'y';
  if (/(ss|sh|ch|x|z)es$/.test(token)) return token.slice(0, -2);
  if (token.endsWith('s') && !token.endsWith('ss') && !token.endsWith('us') && !token.endsWith('is')) {
    return token.slice(0, -1);
  }
  return token;
}

/** Metni küçük harfli, stopword'süz, tekilleştirilmiş token listesine çevirir. */
export function tokenizeTaxonomyText(text: string): string[] {
  if (!text) return [];
  return text
    .replace(/İ/g, 'i')
    .replace(/I/g, 'ı')
    .toLowerCase()
    .replace(/[^a-z0-9çğıöşüâîû]+/gi, ' ')
    .split(/\s+/)
    .filter(t => t.length >= 2 && !STOPWORDS.has(t))
    .map(singularizeToken);
}

function expandWithSynonyms(text: string, synonyms: Record<string, string[]>): string[] {
  const lower = (text || '').replace(/İ/g, 'i').replace(/I/g, 'ı').toLowerCase();
  const extra: string[] = [];
  for (const [trKey, enTerms] of Object.entries(synonyms)) {
    // Çok kelimeli anahtarlar (örn. "gece lambası") ifade olarak, tek kelimeliler kelime başı olarak aranır
    // (sondaki sınır yok: Türkçe ekler — "çantası", "kupalar" — da eşleşsin)
    const hit = trKey.includes(' ')
      ? lower.includes(trKey)
      : new RegExp(`(^|[^a-zçğıöşü])${trKey.replace(/[.*+?^${}()|[\]\\]/g, '\\$&')}`).test(lower);
    if (hit) extra.push(...enTerms);
  }
  return extra.flatMap(tokenizeTaxonomyText);
}

/**
 * Canlı taksonomi listesinden ürüne en alakalı adayları puanlayarak döndürür.
 * Puan: yaprak adında eşleşme 3, yalnız path'te eşleşme 1; başlık token'ları ×2 ağırlıklı.
 */
export function buildTaxonomyShortlist<T extends TaxonomyNodeLike>(
  nodes: T[],
  title: string,
  description: string,
  synonyms: Record<string, string[]> = {},
  limit = 80
): T[] {
  if (!nodes || nodes.length === 0) return [];

  const weights = new Map<string, number>();
  const addTokens = (tokens: string[], weight: number) => {
    for (const t of tokens) weights.set(t, Math.max(weights.get(t) || 0, weight));
  };
  const desc = (description || '').slice(0, 600);
  addTokens(tokenizeTaxonomyText(desc), 1);
  addTokens(expandWithSynonyms(desc, synonyms), 1);
  addTokens(tokenizeTaxonomyText(title), 2);
  addTokens(expandWithSynonyms(title, synonyms), 2);

  if (weights.size === 0) return [];

  const scored: { node: T; score: number; depth: number }[] = [];
  for (const node of nodes) {
    const leafTokens = new Set(tokenizeTaxonomyText(node.name || node.path.split('>').pop() || ''));
    const pathTokens = new Set(tokenizeTaxonomyText(node.path));
    let score = 0;
    weights.forEach((w, token) => {
      if (leafTokens.has(token)) score += 3 * w;
      else if (pathTokens.has(token)) score += w;
    });
    if (score > 0) {
      scored.push({ node, score, depth: node.path.split('>').length });
    }
  }

  scored.sort((a, b) => b.score - a.score || b.depth - a.depth || a.node.path.length - b.node.path.length);
  return scored.slice(0, limit).map(s => s.node);
}

/** Path'in yaprak adını (son segment) normalize ederek döndürür. */
export function taxonomyLeafName(path: string): string {
  return (path || '').split('>').pop()?.trim().toLowerCase() || '';
}
