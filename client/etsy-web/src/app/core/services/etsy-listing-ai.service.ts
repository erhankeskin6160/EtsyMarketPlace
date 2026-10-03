import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError } from 'rxjs';
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

export interface TaxonomyCandidateItem {
  taxonomyId: number;
  categoryPath: string;
  confidenceScore: number;
}

export interface CategoryAiSuggestion {
  taxonomyId: number;
  categoryPath: string;
  confidenceScore: number;
  reasoning: string;
  providerUsed: string;
  isLive: boolean;
  message: string;
  inputSources: {
    hasTitle: boolean;
    hasImage: boolean;
    hasDescription: boolean;
  };
  alternatives: TaxonomyCandidateItem[];
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
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const hasLiveKey = provider === 'Gemini' && !!settings.geminiApiKey && settings.geminiApiKey.trim().length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik şablon üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      return this.callGeminiForTitle(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', currentCategory).pipe(
        map(title => ({
          value: this.normalizeTitleLength(title),
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini ile '${cleanInput || 'Ürün'}' için canlı 140 karakter SEO başlığı optimize edildi!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini API hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Model yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
          }
          const fallback = this.generateRuleBasedTitle(cleanInput, currentCategory);
          const msg = isSilentFallback
            ? `⚡ Akıllı Kural Motoru (Canlı AI yanıt veremedi, offline kural motoruna geçildi).`
            : `⚡ Akıllı Kural Motoru ile '${cleanInput || 'Ürün'}' için 140 karakter SEO başlığı oluşturuldu.`;
          return of({
            value: fallback,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: msg
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
   * Multimodal Etsy Taxonomy Category determination grounded in official Etsy API documentation:
   * https://developers.etsy.com/documentation/reference#operation/getSellerTaxonomyNodes
   * Analyzes:
   * - Product Title
   * - Product Images (Base64 inlineData or URL)
   * - Product Description
   * Or any combination / all 3 simultaneously.
   */
  public suggestCategoryMultimodal(
    title?: string,
    description?: string,
    images?: string[]
  ): Observable<CategoryAiSuggestion> {
    const cleanTitle = (title || '').trim();
    const cleanDesc = (description || '').trim();
    const validImages = (images || []).filter(img => typeof img === 'string' && img.trim().length > 0);
    const coverImage = validImages.length > 0 ? validImages[0] : null;

    const inputSources = {
      hasTitle: cleanTitle.length > 0,
      hasImage: !!coverImage,
      hasDescription: cleanDesc.length > 0
    };

    if (!inputSources.hasTitle && !inputSources.hasImage && !inputSources.hasDescription) {
      return throwError(() => new Error('⚠️ Lütfen kategori belirlemek için en az bir ürün başlığı, ürün görseli veya ürün açıklaması giriniz.'));
    }

    const settings = this.aiSettings.settings();
    const provider = settings.provider;
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    // 1. Google Gemini Live Vision & Text
    if (provider === 'Gemini' && settings.geminiApiKey && settings.geminiApiKey.trim().length > 10) {
      const activeModel = this.resolveGeminiModel(settings.geminiModel || 'gemini-2.5-flash');
      return this.callGeminiForCategoryMultimodal(cleanTitle, cleanDesc, coverImage, settings.geminiApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini kategori hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Gemini yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı Gemini Kategori Hatası: ${detail}. 'Canlı AI Zorunlu' seçili olduğundan çevrimdışı motora geçilmedi.`));
          }
          return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, isSilentFallback));
        })
      );
    }

    // 2. OpenAI GPT-4o / GPT-4o-mini Vision & Text
    if (provider === 'OpenAI' && settings.openAiApiKey && settings.openAiApiKey.trim().length > 10) {
      const activeModel = settings.openAiModel || 'gpt-4o';
      return this.callOpenAiForCategoryMultimodal(cleanTitle, cleanDesc, coverImage, settings.openAiApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı OpenAI kategori hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'OpenAI yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı OpenAI Kategori Hatası: ${detail}. 'Canlı AI Zorunlu' seçili olduğundan çevrimdışı motora geçilmedi.`));
          }
          return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, isSilentFallback));
        })
      );
    }

    // 3. Anthropic Claude 3.5 / 3.7 Vision & Text
    if (provider === 'Claude' && settings.claudeApiKey && settings.claudeApiKey.trim().length > 10) {
      const activeModel = settings.claudeModel || 'claude-3-7-sonnet';
      return this.callClaudeForCategoryMultimodal(cleanTitle, cleanDesc, coverImage, settings.claudeApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Claude kategori hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Claude yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı Claude Kategori Hatası: ${detail}. 'Canlı AI Zorunlu' seçili olduğundan çevrimdışı motora geçilmedi.`));
          }
          return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, isSilentFallback));
        })
      );
    }

    // 4. xAI Grok Vision & Text
    if (provider === 'Grok' && settings.grokApiKey && settings.grokApiKey.trim().length > 10) {
      const activeModel = settings.grokModel || 'grok-3';
      return this.callGrokForCategoryMultimodal(cleanTitle, cleanDesc, coverImage, settings.grokApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Grok kategori hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Grok yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı Grok Kategori Hatası: ${detail}. 'Canlı AI Zorunlu' seçili olduğundan çevrimdışı motora geçilmedi.`));
          }
          return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, isSilentFallback));
        })
      );
    }

    // 5. DeepSeek (DeepSeek Chat / Reasoner)
    if (provider === 'DeepSeek' && settings.deepSeekApiKey && settings.deepSeekApiKey.trim().length > 10) {
      const activeModel = settings.deepSeekModel || 'deepseek-reasoner';
      return this.callDeepSeekForCategory(cleanTitle, cleanDesc, settings.deepSeekApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı DeepSeek kategori hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'DeepSeek yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı DeepSeek Kategori Hatası: ${detail}. 'Canlı AI Zorunlu' seçili olduğundan çevrimdışı motora geçilmedi.`));
          }
          return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, isSilentFallback));
        })
      );
    }

    if (isStrict && provider !== 'Offline') {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı giriniz.`));
    }

    // Offline Fallback
    return of(this.suggestOfflineCategoryDetailed(cleanTitle, cleanDesc, inputSources, false));
  }

  /**
   * Backwards-compatible legacy method.
   */
  public suggestCategory(userInput: string): Observable<AiFieldResult<string>> {
    return this.suggestCategoryMultimodal(userInput).pipe(
      map(res => ({
        value: `${res.taxonomyId} - ${res.categoryPath}`,
        isLive: res.isLive,
        provider: res.providerUsed,
        message: res.message
      }))
    );
  }

  /**
   * Generates 13 high-converting Etsy search tags (max 20 chars each).
   */
  public suggestTags(userInput: string, currentCategory?: string): Observable<AiFieldResult<string[]>> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const hasLiveKey = provider === 'Gemini' && !!settings.geminiApiKey && settings.geminiApiKey.trim().length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik etiket üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      return this.callGeminiForTags(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash').pipe(
        map(tags => ({
          value: this.normalizeTagsList(tags),
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini 13 altın arama etiketini canlı olarak tam doldurdu!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini tag hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Model yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
          }
          const tags = this.generateRuleBasedTags(cleanInput);
          const msg = isSilentFallback
            ? `⚡ Akıllı Kural Motoru (Canlı AI yanıt veremedi, 13 arama etiketi offline oluşturuldu).`
            : `⚡ Akıllı Kural Motoru ile 13 arama etiketi üretildi.`;
          return of({
            value: tags,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: msg
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
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const hasLiveKey = provider === 'Gemini' && !!settings.geminiApiKey && settings.geminiApiKey.trim().length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik açıklama üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      return this.callGeminiForDescription(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', materials).pipe(
        map(desc => ({
          value: desc,
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          message: `✨ Google Gemini ikna edici ürün açıklamasını canlı üretti!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini açıklama hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Model yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
          }
          const desc = this.generateRuleBasedDescription(cleanInput, materials);
          const msg = isSilentFallback
            ? `⚡ Akıllı Kural Motoru (Canlı AI yanıt veremedi, offline açıklama oluşturuldu).`
            : `⚡ Akıllı Kural Motoru ile zengin ürün açıklaması hazırlandı.`;
          return of({
            value: desc,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: msg
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
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const hasLiveKey = provider === 'Gemini' && !!settings.geminiApiKey && settings.geminiApiKey.trim().length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik listeleme üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      return this.callGeminiForComplete(cleanInput, settings.geminiApiKey.trim(), settings.geminiModel || 'gemini-2.5-flash', materials).pipe(
        map(res => ({
          title: this.normalizeTitleLength(res.title),
          category: res.category || this.detectCategoryFromKeywords(cleanInput),
          tags: this.normalizeTagsList(res.tags),
          description: res.description,
          materials: res.materials || materials || 'Handcrafted, Premium Materials',
          isLive: true,
          provider: `Gemini (${settings.geminiModel || 'gemini-2.5-flash'})`,
          summaryMessage: `🎉 Google Gemini ile '${cleanInput || 'Ürün'}' için tüm Etsy listelemesi (Canlı Başlık, Kategori, 13 Tag, Açıklama) hazırlandı!`
        })),
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı Gemini tam paket hatası:', err);
          if (isStrict) {
            const detail = err?.error?.error?.message || err?.message || 'Model yanıt vermedi';
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider} - ${settings.geminiModel || 'gemini-2.5-flash'}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
          }
          const ruleRes = this.generateRuleBasedComplete(cleanInput, materials);
          if (isSilentFallback) {
            ruleRes.summaryMessage = `⚡ Akıllı Kural Motoru (Canlı AI yanıt veremedi, offline şablon listelemesi oluşturuldu).`;
          }
          return of(ruleRes);
        })
      );
    }

    return of(this.generateRuleBasedComplete(cleanInput, materials));
  }

  private resolveGeminiModel(model: string): string {
    const m = (model || '').trim();
    if (m === 'gemini-2.5-pro' || !m) {
      return 'gemini-2.5-flash';
    }
    return m;
  }

  private callGeminiForTitle(input: string, apiKey: string, model: string, category?: string): Observable<string> {
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
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
        maxOutputTokens: 1000
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
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
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
        maxOutputTokens: 1500,
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
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
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
        maxOutputTokens: 2500
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || this.generateRuleBasedDescription(input, materials))
    );
  }

  private callGeminiForComplete(input: string, apiKey: string, model: string, materials?: string): Observable<any> {
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
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
        maxOutputTokens: 3500,
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

  private readonly ETSY_TAXONOMY_SYSTEM_PROMPT = `You are an expert official Etsy Taxonomy and Category Specialist with deep mastery of the Etsy Seller Taxonomy Tree (https://developers.etsy.com/documentation/reference#operation/getSellerTaxonomyNodes).

Your task is to accurately determine the single best official Etsy taxonomy category for a product, by analyzing whatever product data the seller provides:
1. Product Title (SEO keywords, physical item type)
2. Product Image (visual shape, materials, aesthetics, functional purpose)
3. Product Description (features, materials, size, intended use)

OFFICIAL ETSY TAXONOMY RULES:
- Etsy listing taxonomy requires selecting a specific leaf category node from one of the official Etsy root departments:
  * Bags & Purses:
    - 132: Bags & Purses > Handbags > Shoulder Bags
    - 134: Bags & Purses > Handbags > Tote Bags
    - 133: Bags & Purses > Handbags > Crossbody Bags
    - 138: Bags & Purses > Handbags > Clutches & Evening Bags
    - 140: Bags & Purses > Backpacks
    - 142: Bags & Purses > Wallets & Money Clips
  * Jewelry:
    - 204: Jewelry > Necklaces
    - 211: Jewelry > Necklaces > Pendants
    - 220: Jewelry > Rings
    - 187: Jewelry > Earrings
    - 172: Jewelry > Bracelets
  * Clothing:
    - 270: Clothing > Unisex Adult Clothing > Tops & Tees > T-shirts
    - 283: Clothing > Women's Clothing > Dresses
    - 288: Clothing > Unisex Adult Clothing > Hoodies & Sweatshirts
  * Home & Living:
    - 1041: Home & Living > Lighting > Lamps
    - 1042: Home & Living > Lighting > Night Lights
    - 1054: Home & Living > Home Decor > Wall Decor
    - 1063: Home & Living > Home Decor > Candleholders
    - 943: Home & Living > Kitchen & Dining > Drinkware > Mugs
    - 992: Home & Living > Outdoor & Gardening > Planters & Pots
  * Art & Collectibles:
    - 1239: Art & Collectibles > Sculptures > Busts & Statues
    - 1238: Art & Collectibles > Sculptures > Figurines
    - 1215: Art & Collectibles > Prints > Digital Prints
  * Craft Supplies & Tools:
    - 68: Craft Supplies & Tools > Digital
    - 590: Craft Supplies & Tools > Patterns & How To
  * Accessories:
    - 22: Accessories > Hats & Caps
    - 62: Accessories > Keychains
  * Electronics & Accessories:
    - 2079: Electronics & Accessories > Audio > Headphone & Headset Stands
    - 651: Electronics & Accessories > Cases & Covers > Phone Cases

CRITICAL INSTRUCTIONS:
1. Examine the actual physical item. If it is a handbag or purse (e.g. "El yapımı kadın çantası" or bag image), do NOT classify it as 3D print or home decor. Categorize it under Bags & Purses (e.g., taxonomy_id 132: Bags & Purses > Handbags > Shoulder Bags).
2. If an image is provided, carefully inspect the visual textures, stitching, hardware, materials, and form factor.
3. Provide a clear 'reasoning' in Turkish explaining how the visual cues, title, or description led to this taxonomy choice.
4. Suggest 2-3 realistic alternatives in 'alternatives' array.

You MUST respond ONLY with a single valid JSON object matching this schema:
{
  "taxonomy_id": <number>,
  "category_path": "<Department > Subcategory > Specific Leaf>",
  "confidence_score": <number between 1 and 100>,
  "reasoning": "<Concise explanation in Turkish citing title/image/description clues>",
  "alternatives": [
    {"taxonomy_id": <number>, "category_path": "<Path>", "confidence_score": <number>}
  ]
}`;

  private buildTaxonomyUserPrompt(title?: string, desc?: string, hasImage?: boolean): string {
    let p = 'Lütfen aşağıdaki ürün bilgilerini Etsy resmi dökümantasyonundaki Seller Taxonomy ağacına göre sınıflandırın:\n';
    if (title && title.trim()) {
      p += `Ürün Başlığı: "${title.trim()}"\n`;
    }
    if (desc && desc.trim()) {
      p += `Ürün Açıklaması: "${desc.trim().slice(0, 800)}"\n`;
    }
    if (hasImage) {
      p += `[Görsel Sağlandı]: Ürünün fiziksel fotoğrafı iletilmiştir. Görseldeki şekli, malzemeyi, dikiş/tasarım detaylarını ve kullanım amacını analiz ederek en uygun yaprak kategoriyi seçiniz.\n`;
    }
    return p;
  }

  private formatInputSourcesDescription(sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }): string {
    const list: string[] = [];
    if (sources.hasTitle) list.push('✍️ Başlık');
    if (sources.hasImage) list.push('📷 Görsel');
    if (sources.hasDescription) list.push('📝 Açıklama');
    return list.length > 0 ? `(${list.join(' + ')})` : '';
  }

  private parseTaxonomyJsonResponse(raw: string): any {
    if (!raw) return {};
    let clean = raw.trim();
    if (clean.startsWith('```json')) clean = clean.substring(7);
    else if (clean.startsWith('```')) clean = clean.substring(3);
    if (clean.endsWith('```')) clean = clean.substring(0, clean.length - 3);
    clean = clean.trim();

    const firstBrace = clean.indexOf('{');
    const lastBrace = clean.lastIndexOf('}');
    if (firstBrace >= 0 && lastBrace > firstBrace) {
      clean = clean.substring(firstBrace, lastBrace + 1);
    }

    try {
      return JSON.parse(clean);
    } catch {
      return {};
    }
  }

  private callGeminiForCategoryMultimodal(
    title: string,
    desc: string,
    coverImage: string | null,
    apiKey: string,
    model: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }
  ): Observable<CategoryAiSuggestion> {
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(model)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, sources.hasImage);

    const parts: any[] = [{ text: userPromptText }];
    if (coverImage && coverImage.startsWith('data:')) {
      const match = coverImage.match(/^data:(image\/[a-zA-Z+]+);base64,(.+)$/);
      if (match) {
        parts.push({
          inlineData: {
            mimeType: match[1],
            data: match[2]
          }
        });
      }
    }

    const body = {
      systemInstruction: { parts: [{ text: this.ETSY_TAXONOMY_SYSTEM_PROMPT }] },
      contents: [{ role: 'user', parts }],
      generationConfig: {
        temperature: 0.2,
        maxOutputTokens: 1200,
        responseMimeType: 'application/json'
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        const rawText = res?.candidates?.[0]?.content?.parts?.[0]?.text || '{}';
        const parsed = this.parseTaxonomyJsonResponse(rawText);
        const sourcesText = this.formatInputSourcesDescription(sources);
        return {
          taxonomyId: parsed.taxonomy_id || 132,
          categoryPath: parsed.category_path || 'Bags & Purses > Handbags > Shoulder Bags',
          confidenceScore: parsed.confidence_score || 95,
          reasoning: parsed.reasoning || 'Ürün başlığı ve görsel analizi doğrultusunda belirlendi.',
          providerUsed: `Google Gemini (${model})`,
          isLive: true,
          message: `✨ Google Gemini (${model}) ${sourcesText} inceleyerek Etsy kategorisini belirledi: #${parsed.taxonomy_id} ${parsed.category_path}`,
          inputSources: sources,
          alternatives: (parsed.alternatives || []).map((a: any) => ({
            taxonomyId: Number(a.taxonomy_id) || 0,
            categoryPath: String(a.category_path || ''),
            confidenceScore: Number(a.confidence_score) || 80
          }))
        };
      })
    );
  }

  private callOpenAiForCategoryMultimodal(
    title: string,
    desc: string,
    coverImage: string | null,
    apiKey: string,
    model: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }
  ): Observable<CategoryAiSuggestion> {
    const url = 'https://api.openai.com/v1/chat/completions';
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, sources.hasImage);
    const userContent: any[] = [{ type: 'text', text: userPromptText }];

    if (coverImage) {
      userContent.push({
        type: 'image_url',
        image_url: { url: coverImage, detail: 'auto' }
      });
    }

    const body = {
      model: model || 'gpt-4o',
      messages: [
        { role: 'system', content: this.ETSY_TAXONOMY_SYSTEM_PROMPT },
        { role: 'user', content: userContent }
      ],
      temperature: 0.2,
      max_tokens: 1000,
      response_format: { type: 'json_object' }
    };

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        const rawText = res?.choices?.[0]?.message?.content || '{}';
        const parsed = this.parseTaxonomyJsonResponse(rawText);
        const sourcesText = this.formatInputSourcesDescription(sources);
        return {
          taxonomyId: parsed.taxonomy_id || 132,
          categoryPath: parsed.category_path || 'Bags & Purses > Handbags > Shoulder Bags',
          confidenceScore: parsed.confidence_score || 95,
          reasoning: parsed.reasoning || 'OpenAI analizi doğrultusunda belirlendi.',
          providerUsed: `OpenAI (${model})`,
          isLive: true,
          message: `✨ OpenAI (${model}) ${sourcesText} inceleyerek Etsy kategorisini belirledi: #${parsed.taxonomy_id} ${parsed.category_path}`,
          inputSources: sources,
          alternatives: (parsed.alternatives || []).map((a: any) => ({
            taxonomyId: Number(a.taxonomy_id) || 0,
            categoryPath: String(a.category_path || ''),
            confidenceScore: Number(a.confidence_score) || 80
          }))
        };
      })
    );
  }

  private callClaudeForCategoryMultimodal(
    title: string,
    desc: string,
    coverImage: string | null,
    apiKey: string,
    model: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }
  ): Observable<CategoryAiSuggestion> {
    const url = 'https://api.anthropic.com/v1/messages';
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, sources.hasImage);
    const userContent: any[] = [];

    if (coverImage && coverImage.startsWith('data:')) {
      const match = coverImage.match(/^data:(image\/[a-zA-Z+]+);base64,(.+)$/);
      if (match) {
        userContent.push({
          type: 'image',
          source: {
            type: 'base64',
            media_type: match[1],
            data: match[2]
          }
        });
      }
    }
    userContent.push({ type: 'text', text: userPromptText });

    const body = {
      model: model || 'claude-3-7-sonnet-20250219',
      max_tokens: 1000,
      temperature: 0.2,
      system: this.ETSY_TAXONOMY_SYSTEM_PROMPT,
      messages: [{ role: 'user', content: userContent }]
    };

    const headers = {
      'x-api-key': apiKey,
      'anthropic-version': '2023-06-01',
      'Content-Type': 'application/json',
      'anthropic-dangerous-direct-browser-access': 'true'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        const rawText = res?.content?.[0]?.text || '{}';
        const parsed = this.parseTaxonomyJsonResponse(rawText);
        const sourcesText = this.formatInputSourcesDescription(sources);
        return {
          taxonomyId: parsed.taxonomy_id || 132,
          categoryPath: parsed.category_path || 'Bags & Purses > Handbags > Shoulder Bags',
          confidenceScore: parsed.confidence_score || 95,
          reasoning: parsed.reasoning || 'Claude Vision analizi doğrultusunda belirlendi.',
          providerUsed: `Claude (${model})`,
          isLive: true,
          message: `✨ Anthropic Claude ${sourcesText} inceleyerek Etsy kategorisini belirledi: #${parsed.taxonomy_id} ${parsed.category_path}`,
          inputSources: sources,
          alternatives: (parsed.alternatives || []).map((a: any) => ({
            taxonomyId: Number(a.taxonomy_id) || 0,
            categoryPath: String(a.category_path || ''),
            confidenceScore: Number(a.confidence_score) || 80
          }))
        };
      })
    );
  }

  private callGrokForCategoryMultimodal(
    title: string,
    desc: string,
    coverImage: string | null,
    apiKey: string,
    model: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }
  ): Observable<CategoryAiSuggestion> {
    const url = 'https://api.x.ai/v1/chat/completions';
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, sources.hasImage);
    const userContent: any[] = [{ type: 'text', text: userPromptText }];

    if (coverImage) {
      userContent.push({
        type: 'image_url',
        image_url: { url: coverImage, detail: 'auto' }
      });
    }

    const body = {
      model: model || 'grok-3',
      messages: [
        { role: 'system', content: this.ETSY_TAXONOMY_SYSTEM_PROMPT },
        { role: 'user', content: userContent }
      ],
      temperature: 0.2,
      max_tokens: 1000
    };

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        const rawText = res?.choices?.[0]?.message?.content || '{}';
        const parsed = this.parseTaxonomyJsonResponse(rawText);
        const sourcesText = this.formatInputSourcesDescription(sources);
        return {
          taxonomyId: parsed.taxonomy_id || 132,
          categoryPath: parsed.category_path || 'Bags & Purses > Handbags > Shoulder Bags',
          confidenceScore: parsed.confidence_score || 95,
          reasoning: parsed.reasoning || 'xAI Grok analizi doğrultusunda belirlendi.',
          providerUsed: `xAI Grok (${model})`,
          isLive: true,
          message: `✨ xAI Grok (${model}) ${sourcesText} inceleyerek Etsy kategorisini belirledi: #${parsed.taxonomy_id} ${parsed.category_path}`,
          inputSources: sources,
          alternatives: (parsed.alternatives || []).map((a: any) => ({
            taxonomyId: Number(a.taxonomy_id) || 0,
            categoryPath: String(a.category_path || ''),
            confidenceScore: Number(a.confidence_score) || 80
          }))
        };
      })
    );
  }

  private callDeepSeekForCategory(
    title: string,
    desc: string,
    apiKey: string,
    model: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean }
  ): Observable<CategoryAiSuggestion> {
    const url = 'https://api.deepseek.com/chat/completions';
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, false);

    const body = {
      model: model || 'deepseek-reasoner',
      messages: [
        { role: 'system', content: this.ETSY_TAXONOMY_SYSTEM_PROMPT },
        { role: 'user', content: userPromptText }
      ],
      temperature: 0.2,
      max_tokens: 1000
    };

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        const rawText = res?.choices?.[0]?.message?.content || '{}';
        const parsed = this.parseTaxonomyJsonResponse(rawText);
        const sourcesText = this.formatInputSourcesDescription(sources);
        return {
          taxonomyId: parsed.taxonomy_id || 132,
          categoryPath: parsed.category_path || 'Bags & Purses > Handbags > Shoulder Bags',
          confidenceScore: parsed.confidence_score || 95,
          reasoning: parsed.reasoning || 'DeepSeek derin mantık analizi ile belirlendi.',
          providerUsed: `DeepSeek (${model})`,
          isLive: true,
          message: `✨ DeepSeek (${model}) ${sourcesText} inceleyerek Etsy kategorisini belirledi: #${parsed.taxonomy_id} ${parsed.category_path}`,
          inputSources: sources,
          alternatives: (parsed.alternatives || []).map((a: any) => ({
            taxonomyId: Number(a.taxonomy_id) || 0,
            categoryPath: String(a.category_path || ''),
            confidenceScore: Number(a.confidence_score) || 80
          }))
        };
      })
    );
  }

  private suggestOfflineCategoryDetailed(
    title: string,
    desc: string,
    sources: { hasTitle: boolean; hasImage: boolean; hasDescription: boolean },
    isFallbackFromError: boolean
  ): CategoryAiSuggestion {
    const text = `${title} ${desc}`.toLowerCase();
    const sourcesText = this.formatInputSourcesDescription(sources);

    // 1. Bags & Purses
    if (text.includes('çanta') || text.includes('bag') || text.includes('purse') || text.includes('omuz çantası') || text.includes('handbag')) {
      return {
        taxonomyId: 132,
        categoryPath: 'Bags & Purses > Handbags > Shoulder Bags',
        confidenceScore: 92,
        reasoning: 'Metindeki çanta, omuz çantası veya el yapımı bag anahtar kelimeleri doğrudan Etsy Bags & Purses kategorisiyle eşleşmektedir.',
        providerUsed: isFallbackFromError ? 'Yerel Kural Motoru (Fallback)' : 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #132 Bags & Purses > Handbags > Shoulder Bags`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 134, categoryPath: 'Bags & Purses > Handbags > Tote Bags', confidenceScore: 85 },
          { taxonomyId: 133, categoryPath: 'Bags & Purses > Handbags > Crossbody Bags', confidenceScore: 80 }
        ]
      };
    }

    if (text.includes('tote') || text.includes('bez çanta') || text.includes('kanvas')) {
      return {
        taxonomyId: 134,
        categoryPath: 'Bags & Purses > Handbags > Tote Bags',
        confidenceScore: 93,
        reasoning: 'Bez çanta / tote terimi Etsy Tote Bags yaprak kategorisiyle tam uyumludur.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #134 Bags & Purses > Handbags > Tote Bags`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 132, categoryPath: 'Bags & Purses > Handbags > Shoulder Bags', confidenceScore: 82 }
        ]
      };
    }

    // 2. Jewelry
    if (text.includes('kolye') || text.includes('necklace') || text.includes('takı') || text.includes('jewelry') || text.includes('pendant')) {
      return {
        taxonomyId: 204,
        categoryPath: 'Jewelry > Necklaces',
        confidenceScore: 94,
        reasoning: 'Takı ve kolye terimleri Etsy Jewelry > Necklaces departmanı ile eşleşti.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #204 Jewelry > Necklaces`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 211, categoryPath: 'Jewelry > Necklaces > Pendants', confidenceScore: 88 },
          { taxonomyId: 220, categoryPath: 'Jewelry > Rings', confidenceScore: 70 }
        ]
      };
    }

    // 3. 3D Print / Figurines
    if (text.includes('3d') || text.includes('baskı') || text.includes('print') || text.includes('figür') || text.includes('ejderha') || text.includes('dragon') || text.includes('fidget')) {
      return {
        taxonomyId: 1238,
        categoryPath: 'Art & Collectibles > Sculptures > Figurines',
        confidenceScore: 92,
        reasoning: '3D baskı, figür veya minyatür terimleri Etsy Sculptures & Figurines dalıyla eşleşti.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #1238 Art & Collectibles > Sculptures > Figurines`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 1239, categoryPath: 'Art & Collectibles > Sculptures > Busts & Statues', confidenceScore: 85 },
          { taxonomyId: 68, categoryPath: 'Craft Supplies & Tools > Digital', confidenceScore: 75 }
        ]
      };
    }

    // 4. Home & Living - Lighting / Decor
    if (text.includes('lamba') || text.includes('lamp') || text.includes('ışık') || text.includes('gece lambası')) {
      return {
        taxonomyId: 1041,
        categoryPath: 'Home & Living > Lighting > Lamps',
        confidenceScore: 90,
        reasoning: 'Aydınlatma ve dekoratif masa lambası terimleri Etsy Lighting dalıyla eşleşti.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #1041 Home & Living > Lighting > Lamps`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 1042, categoryPath: 'Home & Living > Lighting > Night Lights', confidenceScore: 86 }
        ]
      };
    }

    // 5. Ceramics / Mugs
    if (text.includes('seramik') || text.includes('kupa') || text.includes('mug') || text.includes('fincan')) {
      return {
        taxonomyId: 943,
        categoryPath: 'Home & Living > Kitchen & Dining > Drinkware > Mugs',
        confidenceScore: 95,
        reasoning: 'Mutfak ve içecek kupası terimleri Mugs yaprak kategorisiyle tam örtüşmektedir.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #943 Home & Living > Kitchen & Dining > Drinkware > Mugs`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 992, categoryPath: 'Home & Living > Outdoor & Gardening > Planters & Pots', confidenceScore: 70 }
        ]
      };
    }

    // Default Fallback
    return {
      taxonomyId: 1239,
      categoryPath: 'Art & Collectibles > Sculptures > Busts & Statues',
      confidenceScore: 75,
      reasoning: 'Genel el sanatı ve tasarım ürün dalı seçildi.',
      providerUsed: 'Yerel Kural Motoru',
      isLive: false,
      message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #1239 Art & Collectibles`,
      inputSources: sources,
      alternatives: [
        { taxonomyId: 1054, categoryPath: 'Home & Living > Home Decor > Wall Decor', confidenceScore: 70 }
      ]
    };
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
