import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AiSettingsService } from './ai-settings.service';

export interface CompleteListingAiResult {
  title: string;
  category: string;
  tags: string[];
  description: string;
  materials?: string;
  isLive: boolean;
  provider: string;
  summaryMessage: string;
}

export interface AiFieldResult<T> {
  value: T;
  isLive: boolean;
  provider: string;
  message: string;
}

@Injectable({
  providedIn: 'root'
})
export class EtsyListingAiService {
  private http = inject(HttpClient);
  private aiSettings = inject(AiSettingsService);

  /**
   * Generates a 140-character Etsy-optimized SEO title.
   */
  public suggestTitle(userInput: string, currentCategory?: string): Observable<AiFieldResult<string>> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;

    // Check if live AI is available
    if (provider === 'Gemini' && settings.geminiApiKey && settings.geminiApiKey.trim().length > 10) {
      return this.callGeminiForTitle(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', currentCategory).pipe(
        map(title => ({
          value: this.normalizeTitleLength(title),
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini ile '${cleanInput || 'Ürün'}' için 140 karakter SEO başlığı optimize edildi!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini API hatası, Akıllı Kural Motoru devrede:', err);
          const fallback = this.generateRuleBasedTitle(cleanInput, currentCategory);
          return of({
            value: fallback,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: `⚡ Akıllı Kural Motoru ile '${cleanInput || 'Ürün'}' için 140 karakter SEO başlığı oluşturuldu.`
          });
        })
      );
    }

    // Offline / Fallback
    const title = this.generateRuleBasedTitle(cleanInput, currentCategory);
    return of({
      value: title,
      isLive: false,
      provider: provider === 'Offline' ? 'Offline Kural Motoru' : `${provider} (Offline Kural)`,
      message: `⚡ Akıllı Kural Motoru ile '${cleanInput || 'Ürün'}' için 140 karakter SEO başlığı optimize edildi.`
    });
  }

  /**
   * Suggests the best matching Etsy Taxonomy category.
   */
  public suggestCategory(userInput: string): Observable<AiFieldResult<string>> {
    const cleanInput = (userInput || '').trim();
    const cat = this.detectCategoryFromKeywords(cleanInput);
    return of({
      value: cat,
      isLive: false,
      provider: 'Etsy Taxonomy Motoru',
      message: `✨ '${cleanInput || 'Ürün'}' için en uygun Etsy Taxonomy kategorisi belirlendi.`
    });
  }

  /**
   * Generates 13 high-converting Etsy search tags (max 20 chars each).
   */
  public suggestTags(userInput: string, currentCategory?: string): Observable<AiFieldResult<string[]>> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;

    if (provider === 'Gemini' && settings.geminiApiKey && settings.geminiApiKey.trim().length > 10) {
      return this.callGeminiForTags(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash').pipe(
        map(tags => ({
          value: this.normalizeTagsList(tags),
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini 13 altın arama etiketini tam doldurdu!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini tag hatası, kural motoruna geçildi:', err);
          const tags = this.generateRuleBasedTags(cleanInput);
          return of({
            value: tags,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: `⚡ Akıllı Kural Motoru ile 13 arama etiketi üretildi.`
          });
        })
      );
    }

    const tags = this.generateRuleBasedTags(cleanInput);
    return of({
      value: tags,
      isLive: false,
      provider: provider === 'Offline' ? 'Offline Kural Motoru' : `${provider} (Offline Kural)`,
      message: `⚡ Akıllı Kural Motoru 13 altın etiketi tam doldurdu!`
    });
  }

  /**
   * Generates a persuasive, structured product description.
   */
  public suggestDescription(userInput: string, materials?: string): Observable<AiFieldResult<string>> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;

    if (provider === 'Gemini' && settings.geminiApiKey && settings.geminiApiKey.trim().length > 10) {
      return this.callGeminiForDescription(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', materials).pipe(
        map(desc => ({
          value: desc,
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini ikna edici ürün açıklamasını üretti!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini açıklama hatası, kural motoruna geçildi:', err);
          const desc = this.generateRuleBasedDescription(cleanInput, materials);
          return of({
            value: desc,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: `⚡ Akıllı Kural Motoru ile zengin ürün açıklaması hazırlandı.`
          });
        })
      );
    }

    const desc = this.generateRuleBasedDescription(cleanInput, materials);
    return of({
      value: desc,
      isLive: false,
      provider: provider === 'Offline' ? 'Offline Kural Motoru' : `${provider} (Offline Kural)`,
      message: `⚡ Akıllı Kural Motoru ile zengin ürün açıklaması hazırlandı.`
    });
  }

  /**
   * Generates all listing fields (Title, Category, Tags, Description) in a single unified execution.
   */
  public generateCompleteListing(userInput: string, materials?: string): Observable<CompleteListingAiResult> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;

    if (provider === 'Gemini' && settings.geminiApiKey && settings.geminiApiKey.trim().length > 10) {
      return this.callGeminiForComplete(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', materials).pipe(
        map(res => ({
          title: this.normalizeTitleLength(res.title),
          category: res.category || this.detectCategoryFromKeywords(cleanInput),
          tags: this.normalizeTagsList(res.tags),
          description: res.description,
          materials: res.materials || materials || 'Handcrafted, Premium Materials',
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          summaryMessage: `🎉 Google Gemini ile '${cleanInput || 'Ürün'}' için tüm Etsy listelemesi (Başlık, Kategori, 13 Tag, Açıklama) hazırlandı!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini tam paket hatası, kural motoruna geçildi:', err);
          return of(this.generateRuleBasedComplete(cleanInput, materials));
        })
      );
    }

    return of(this.generateRuleBasedComplete(cleanInput, materials));
  }

  // =========================================================================
  // LIVE GEMINI API REST CALLS
  // =========================================================================

  private callGeminiForTitle(input: string, apiKey: string, model: string, category?: string): Observable<string> {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are a world-class Etsy SEO specialist. Return ONLY a single line containing an optimized English Etsy title up to 140 characters. 
The first 55 characters MUST contain the most critical search keywords for mobile visibility. 
Separate keyword phrases with commas or pipes. 
Do NOT include quotes, explanations, markdown, or greetings. Output ONLY the raw title string.`;

    const userPrompt = `Product input from seller: "${input || 'Handmade Artisan Gift'}". Category: "${category || ''}". Generate a high-converting 140-character Etsy title:`;

    const body = {
      systemInstruction: { parts: [{ text: systemInstruction }] },
      contents: [{ role: 'user', parts: [{ text: userPrompt }] }],
      generationConfig: {
        temperature: 0.35,
        maxOutputTokens: 120
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        const text = res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || '';
        return text.replace(/^["'`]+|["'`]+$/g, '').trim();
      })
    );
  }

  private callGeminiForTags(input: string, apiKey: string, model: string): Observable<string[]> {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are an Etsy SEO expert. Generate exactly 13 unique, high-search-volume buyer tags for this product.
CRITICAL ETSY RULES:
- Each tag must be maximum 20 characters long.
- Use only lowercase letters, numbers, and single spaces. No punctuation, no symbols, no hashtags.
- Output ONLY a valid JSON array of 13 strings, e.g. ["tag one", "tag two", ...]`;

    const body = {
      systemInstruction: { parts: [{ text: systemInstruction }] },
      contents: [{ role: 'user', parts: [{ text: `Product: "${input || 'Handcrafted Gift'}"` }] }],
      generationConfig: {
        temperature: 0.3,
        responseMimeType: 'application/json'
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        const raw = res?.candidates?.[0]?.content?.parts?.[0]?.text || '[]';
        try {
          const parsed = JSON.parse(raw);
          if (Array.isArray(parsed)) return parsed.map((s: any) => String(s).toLowerCase().trim());
        } catch {
          // ignore
        }
        return this.generateRuleBasedTags(input);
      })
    );
  }

  private callGeminiForDescription(input: string, apiKey: string, model: string, materials?: string): Observable<string> {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are a top Etsy copywriter. Write a persuasive, beautifully structured product description.
Include:
- Catchy hook & opening summary
- Highlighted Key Features (bullet points)
- Materials & Specifications (${materials || 'Handcrafted, Eco-friendly'})
- Care & Packaging / Gift wrapping options
- Friendly shop closing.
Format with clean emojis and line breaks.`;

    const body = {
      systemInstruction: { parts: [{ text: systemInstruction }] },
      contents: [{ role: 'user', parts: [{ text: `Product: "${input || 'Handcrafted Artisan Product'}". Materials: "${materials || ''}"` }] }],
      generationConfig: {
        temperature: 0.5,
        maxOutputTokens: 800
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || this.generateRuleBasedDescription(input, materials))
    );
  }

  private callGeminiForComplete(input: string, apiKey: string, model: string, materials?: string): Observable<any> {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are an elite Etsy listing architect. Return ONLY a valid JSON object with the following fields:
{
  "title": "SEO title up to 140 chars, first 55 chars mobile-optimized",
  "category": "Matching Etsy Taxonomy breadcrumb e.g. Bags & Purses > Handbags > Shoulder Bags",
  "tags": ["array of exactly 13 lowercase tags each max 20 chars"],
  "description": "Engaging formatted product description with emojis and bullet points",
  "materials": "Comma-separated list of materials used"
}`;

    const body = {
      systemInstruction: { parts: [{ text: systemInstruction }] },
      contents: [{ role: 'user', parts: [{ text: `Optimize this listing for: "${input || 'Handmade Artisan Item'}". Materials: "${materials || ''}"` }] }],
      generationConfig: {
        temperature: 0.4,
        responseMimeType: 'application/json'
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        const raw = res?.candidates?.[0]?.content?.parts?.[0]?.text || '{}';
        return JSON.parse(raw);
      })
    );
  }

  // =========================================================================
  // INTELLIGENT OFFLINE ETSY SEO RULE ENGINE
  // =========================================================================

  private detectCategoryFromKeywords(text: string): string {
    const lower = text.toLowerCase();

    // 1. Bags & Purses
    if (lower.includes('çanta') || lower.includes('bag') || lower.includes('purse') || lower.includes('tote') || lower.includes('cüzdan') || lower.includes('crossbody')) {
      return 'Bags & Purses > Handbags > Shoulder Bags';
    }
    // 2. Jewelry
    if (lower.includes('kolye') || lower.includes('necklace') || lower.includes('takı') || lower.includes('jewelry') || lower.includes('yüzük') || lower.includes('ring') || lower.includes('bileklik') || lower.includes('küpe') || lower.includes('silver') || lower.includes('gümüş')) {
      return 'Jewelry > Necklaces > Pendants';
    }
    // 3. 3D Print / Figurines (Only if explicitly 3D / printed / dragon / figurine)
    if (lower.includes('3d') || lower.includes('baskı') || lower.includes('print') || lower.includes('figür') || lower.includes('ejderha') || lower.includes('fidget') || lower.includes('balisong')) {
      return 'Art & Collectibles > Sculptures > 3D Printed Figurines';
    }
    // 4. Wood & Home Decor
    if (lower.includes('ahşap') || lower.includes('wood') || lower.includes('lamba') || lower.includes('lamp') || lower.includes('tablo') || lower.includes('dekor') || lower.includes('mobilya') || lower.includes('mum') || lower.includes('candle')) {
      return 'Home & Living > Lighting > Lamps';
    }
    // 5. Ceramics & Pottery
    if (lower.includes('seramik') || lower.includes('ceramic') || lower.includes('kupa') || lower.includes('mug') || lower.includes('fincan') || lower.includes('saksı') || lower.includes('porselen')) {
      return 'Home & Living > Kitchen & Dining > Drinkware > Mugs';
    }
    // 6. Clothing & Apparel
    if (lower.includes('tişört') || lower.includes('t-shirt') || lower.includes('hoodie') || lower.includes('sweatshirt') || lower.includes('giyim') || lower.includes('shirt')) {
      return 'Clothing > Unisex Adult Clothing > Tops & Tees > T-shirts';
    }

    return 'Art & Collectibles > Fine Art Ceramics & Crafts';
  }

  private generateRuleBasedTitle(userInput: string, category?: string): string {
    const lower = (userInput || '').toLowerCase().trim();

    // 1. Kadın Çantası / Handbag / Leather Bag / Tote
    if (lower.includes('çanta') || lower.includes('bag') || lower.includes('purse') || lower.includes('tote')) {
      return 'Handmade Women Handbag, Genuine Leather Shoulder Bag, Artisan Crafted Boho Tote Purse, Minimalist Casual Crossbody, Elegant Gift for Her';
    }

    // 2. Takı / Kolye / Gümüş / Jewelry
    if (lower.includes('kolye') || lower.includes('necklace') || lower.includes('takı') || lower.includes('jewelry') || lower.includes('gümüş') || lower.includes('silver') || lower.includes('bileklik') || lower.includes('yüzük')) {
      return 'Handmade 925 Sterling Silver Pendant Necklace, Dainty Personalized Nameplate Charm, Minimalist Celestial Choker, Birthday Gift for Her';
    }

    // 3. 3D Baskı / Figür / Model
    if (lower.includes('3d') || lower.includes('baskı') || lower.includes('figür') || lower.includes('ejderha') || lower.includes('dragon') || lower.includes('fidget')) {
      return 'Articulated Crystal Dragon 3D Printed Fidget Toy, Flexible Dragon Desk Pet, Fantasy Mythical Beast Figurine Birthday Gift';
    }

    // 4. Ahşap / Lamba / Ev Dekoru
    if (lower.includes('ahşap') || lower.includes('wood') || lower.includes('lamba') || lower.includes('lamp') || lower.includes('ışık')) {
      return 'Handcrafted Wooden Ambient Desk Lamp, Modern Geometric Table Night Light, Rustic Walnut Wood Light, Unique Housewarming Birthday Gift';
    }

    // 5. Seramik / Kupa / Mutfak
    if (lower.includes('seramik') || lower.includes('ceramic') || lower.includes('kupa') || lower.includes('mug') || lower.includes('fincan')) {
      return 'Handmade Ceramic Coffee Mug, Artisan Pottery Speckled Tea Cup, Rustic Stoneware Mug, Aesthetic Kitchenware, Unique Housewarming Gift';
    }

    // 6. Giyim / Tişört
    if (lower.includes('tişört') || lower.includes('t-shirt') || lower.includes('hoodie') || lower.includes('sweatshirt')) {
      return 'Comfortable Vintage Graphic T-Shirt, Premium Cotton Aesthetic Tee, Minimalist Retro Streetwear Top, Unisex Casual Everyday Shirt Gift';
    }

    // 7. Akıllı fallback (Kullanıcının girdiği kelimeleri alıp 140 karaktere tamamlayan şablon)
    if (userInput && userInput.trim().length > 2) {
      const clean = userInput.trim().replace(/[^\w\sğüşıöçĞÜŞİÖÇ-]/gi, '');
      const candidate = `Handmade ${clean} Artisan Design, Custom Personalized Gift for Her or Him, Aesthetic Minimalist Decor, Premium Unique Keepsake`;
      return this.normalizeTitleLength(candidate);
    }

    return 'Handmade Artisan Crafted Gift, Custom Personalized Keepsake Design, Minimalist Aesthetic Decor, Unique Birthday Present for Her';
  }

  private generateRuleBasedTags(userInput: string): string[] {
    const lower = (userInput || '').toLowerCase().trim();

    // 1. Kadın Çantası & Cüzdan
    if (lower.includes('çanta') || lower.includes('bag') || lower.includes('purse') || lower.includes('tote')) {
      return [
        'handmade handbag',
        'women leather bag',
        'shoulder tote bag',
        'artisan crafted bag',
        'boho chic purse',
        'gift for her',
        'minimalist handbag',
        'everyday crossbody',
        'custom women purse',
        'casual tote purse',
        'unique gift for mom',
        'brown leather purse',
        'fashion accessory'
      ];
    }

    // 2. Takı & Kolye
    if (lower.includes('kolye') || lower.includes('necklace') || lower.includes('takı') || lower.includes('jewelry') || lower.includes('gümüş') || lower.includes('silver')) {
      return [
        'silver necklace',
        'handmade jewelry',
        'dainty pendant',
        'gift for her',
        '925 sterling silver',
        'minimalist necklace',
        'personalized gift',
        'celestial jewelry',
        'bridesmaid gift',
        'birthday gift mom',
        'custom charm pendant',
        'boho choker',
        'everyday necklace'
      ];
    }

    // 3. 3D Baskı / Figür
    if (lower.includes('3d') || lower.includes('baskı') || lower.includes('figür') || lower.includes('ejderha') || lower.includes('fidget')) {
      return [
        'crystal dragon',
        '3d printed dragon',
        'articulated dragon',
        'fidget dragon toy',
        'desk pet figurine',
        'fantasy room decor',
        'sensory fidget toy',
        'flexi dragon 3d',
        'bambu lab print',
        'dnd mythical gift',
        'birthday gift boy',
        'unique desk decor',
        'dragon sculpture'
      ];
    }

    // 4. Ahşap & Lamba
    if (lower.includes('ahşap') || lower.includes('wood') || lower.includes('lamba') || lower.includes('lamp')) {
      return [
        'wooden desk lamp',
        'table night light',
        'rustic wood lamp',
        'modern home decor',
        'geometric lamp',
        'housewarming gift',
        'artisan wood craft',
        'ambient room light',
        'minimalist decor',
        'bedside night lamp',
        'unique wood gift',
        'handmade table lamp',
        'cozy home light'
      ];
    }

    // 5. Seramik & Kupa
    if (lower.includes('seramik') || lower.includes('ceramic') || lower.includes('kupa') || lower.includes('mug')) {
      return [
        'ceramic coffee mug',
        'artisan pottery cup',
        'handmade stoneware',
        'aesthetic tea mug',
        'speckled mug',
        'kitchen aesthetic',
        'housewarming gift',
        'pottery coffee cup',
        'boho kitchenware',
        'cozy morning mug',
        'gift for coffee lover',
        'unique ceramic cup',
        'tableware gift'
      ];
    }

    // Genel fallback
    return [
      'handmade artisan gift',
      'personalized present',
      'custom order gift',
      'aesthetic room decor',
      'unique handcrafted',
      'gift for her',
      'gift for him',
      'birthday gift idea',
      'minimalist design',
      'premium craft item',
      'keepsake souvenir',
      'trend etsy item',
      'handcrafted delight'
    ];
  }

  private generateRuleBasedDescription(userInput: string, materials?: string): string {
    const lower = (userInput || '').toLowerCase().trim();

    if (lower.includes('çanta') || lower.includes('bag') || lower.includes('purse')) {
      return `✨ Kusursuz El İşçiliği Kadın Çantası — Zarafet ve Fonksiyonellik Bir Arada!\n\n` +
        `Usta eller tarafından özenle tasarlanan bu şık el yapımı çanta, günlük kullanımınızda hem zarafeti hem de maksimum konforu sunar. Dayanıklı dikiş yapısı ve geniş iç hacmi ile tüm ihtiyaçlarınızı zahmetsizce taşır.\n\n` +
        `🌿 Öne Çıkan Özellikler:\n` +
        `• %100 El Yapımı ve Birinci Sınıf Dikiş Kalitesi\n` +
        `• Ayarlanabilir ve Çıkarılabilir Omuz Askısı\n` +
        `• Cüzdan, telefon ve makyaj çantası için fermuarlı güvenli iç cepler\n` +
        `• Malzeme: ${materials || 'Hakiki Deri / Kaliteli Kanvas Kumaş ve Pirinç Aksesuar'}\n` +
        `• Boyut: ~28 cm x 20 cm x 8 cm (İdeal günlük taşıma ölçüsü)\n\n` +
        `🎁 Hediye & Paketleme:\n` +
        `Tüm ürünlerimiz çevre dostu özel koruyucu toz torbası ve zarif hediye paketi seçeneğiyle kargolanır. Kendiniz veya sevdikleriniz için unutulmaz bir hediye!`;
    }

    if (lower.includes('kolye') || lower.includes('takı') || lower.includes('necklace') || lower.includes('jewelry')) {
      return `💎 Zarif 925 Ayar Gümüş Kolye — Zamansız Şıklık ve Işıltı!\n\n` +
        `Her bir takımız en ince ayrıntısına kadar el işçiliğiyle üretilmiş olup kararmaya karşı özel koruyucu rodyum/altın kaplama ile tamamlanmıştır.\n\n` +
        `✨ Özellikler:\n` +
        `• Malzeme: ${materials || '925 Ayar Gerçek Gümüş, Antialerjik & Nikelsiz'}\n` +
        `• Zincir Uzunluğu: 45 cm + 5 cm uzatma payı\n` +
        `• Özel kadife takı kutusu ve gümüş temizleme bezi hediye!`;
    }

    return `🌟 Özel Tasarım El Yapımı Ürün — Sanat ve Kalite Bir Arada!\n\n` +
      `Atölyemizde büyük bir tutku ve titizlikle üretilen bu benzersiz ürün, evinize şıklık katmak veya sevdiklerinize anlamlı bir hediye sunmak için tasarlandı.\n\n` +
      `✨ Detaylar:\n` +
      `• Malzeme: ${materials || 'Çevre Dostu, Yüksek Kaliteli Birinci Sınıf Malzemeler'}\n` +
      `• Özenli işçilik ve uzun ömürlü kullanım garantisi\n` +
      `• Hızlı ve güvenli kargo, özenli koruyucu ambalaj`;
  }

  private generateRuleBasedComplete(userInput: string, materials?: string): CompleteListingAiResult {
    const cleanInput = (userInput || '').trim();
    const title = this.generateRuleBasedTitle(cleanInput);
    const category = this.detectCategoryFromKeywords(cleanInput);
    const tags = this.generateRuleBasedTags(cleanInput);
    const description = this.generateRuleBasedDescription(cleanInput, materials);

    return {
      title,
      category,
      tags,
      description,
      materials: materials || (title.includes('Bag') ? 'Hakiki Deri, Kanvas Kumaş, Pirinç Toka' : 'Premium Handcrafted Materials'),
      isLive: false,
      provider: 'Offline Kural Motoru',
      summaryMessage: `⚡ Akıllı Kural Motoru ile '${cleanInput || 'Ürün'}' için tüm Etsy listelemesi (Başlık, Kategori, 13 Tag, Açıklama) hazırlandı!`
    };
  }

  private normalizeTitleLength(title: string): string {
    if (!title) return '';
    const clean = title.replace(/\s+/g, ' ').trim();
    if (clean.length <= 140) return clean;
    // Trim cleanly at word boundary
    const sub = clean.slice(0, 140);
    const lastComma = sub.lastIndexOf(',');
    const lastPipe = sub.lastIndexOf('|');
    const lastSpace = sub.lastIndexOf(' ');
    const cutPoint = Math.max(lastComma, lastPipe, lastSpace);
    return cutPoint > 100 ? sub.slice(0, cutPoint).trim() : sub.trim();
  }

  private normalizeTagsList(tags: any[]): string[] {
    if (!Array.isArray(tags)) return [];
    const cleanList = tags
      .map(t => String(t).toLowerCase().replace(/[^a-z0-9\s]/g, '').trim())
      .filter(t => t.length > 0 && t.length <= 20);

    const unique = Array.from(new Set(cleanList));
    if (unique.length >= 13) return unique.slice(0, 13);

    // If less than 13, pad with generic high-ranking terms
    const padPool = ['handmade gift', 'gift for her', 'unique keepsake', 'artisan crafted', 'personalized gift', 'trend etsy item', 'birthday present'];
    for (const p of padPool) {
      if (unique.length >= 13) break;
      if (!unique.includes(p)) unique.push(p);
    }
    return unique.slice(0, 13);
  }
}
