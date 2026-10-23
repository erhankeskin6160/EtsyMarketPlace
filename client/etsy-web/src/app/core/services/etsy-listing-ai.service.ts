import { Injectable, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of, throwError, forkJoin } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AiSettingsService } from './ai-settings.service';
import { EtsyApiService } from './etsy-api.service';

export interface AiDescriptionContext {
  productTitle?: string;
  preliminaryNotes?: string;
  materials?: string;
  category?: string;
  tags?: string[];
  variationsSummary?: string;
  toneStyle?: 'artisan' | 'specs' | 'gift';
  emojiStyle?: 'vibrant' | 'minimal';
}

export interface DescriptionVariantsResult {
  variantA: string;
  variantB: string;
  isLive: boolean;
  provider: string;
  message: string;
}

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
  private etsyApi = inject(EtsyApiService);

  /**
   * Generates a 140-character Etsy-optimized SEO title.
   */
  public suggestTitle(userInput: string, currentCategory?: string): Observable<AiFieldResult<string>> {
    const cleanInput = (userInput || '').trim();
    const settings = this.aiSettings.settings();
    const provider = settings.provider;
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const activeApiKey = this.getActiveApiKey(settings);
    const hasLiveKey = provider !== 'Offline' && !!activeApiKey && activeApiKey.length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik şablon üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      const activeModel = this.getActiveModel(settings);
      let callObs: Observable<string>;
      if (provider === 'DeepSeek') {
        callObs = this.callDeepSeekForTitle(cleanInput, activeApiKey, activeModel, currentCategory);
      } else {
        callObs = this.callGeminiForTitle(cleanInput, activeApiKey, activeModel, currentCategory);
      }

      return callObs.pipe(
        map(title => ({
          value: this.normalizeTitleLength(title),
          isLive: true,
          provider: `${provider} (${activeModel})`,
          message: `✨ ${provider} ile '${cleanInput || 'Ürün'}' için canlı 140 karakter SEO başlığı optimize edildi!`
        })),
        catchError(err => {
          console.warn(`[EtsyListingAiService] Canlı ${provider} API hatası:`, err);
          if (isStrict) {
            const detail = this.formatAiErrorMessage(provider, activeModel, err);
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider} - ${activeModel}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
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
      const activeModel = settings.deepSeekModel || 'DeepSeek-V4-Flash';
      return this.callDeepSeekForCategory(cleanTitle, cleanDesc, settings.deepSeekApiKey.trim(), activeModel, inputSources).pipe(
        catchError(err => {
          console.warn('[EtsyListingAiService] Canlı DeepSeek kategori hatası:', err);
          if (isStrict) {
            const detail = this.formatAiErrorMessage('DeepSeek', activeModel, err);
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

    const activeApiKey = this.getActiveApiKey(settings);
    const hasLiveKey = provider !== 'Offline' && !!activeApiKey && activeApiKey.length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik etiket üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      const activeModel = this.getActiveModel(settings);
      let callObs: Observable<string[]>;
      if (provider === 'DeepSeek') {
        callObs = this.callDeepSeekForTags(cleanInput, activeApiKey, activeModel);
      } else {
        callObs = this.callGeminiForTags(cleanInput, activeApiKey, activeModel);
      }

      return callObs.pipe(
        map(tags => ({
          value: this.normalizeTagsList(tags),
          isLive: true,
          provider: `${provider} (${activeModel})`,
          message: `✨ ${provider} 13 altın arama etiketini canlı olarak tam doldurdu!`
        })),
        catchError(err => {
          console.warn(`[EtsyListingAiService] Canlı ${provider} tag hatası:`, err);
          if (isStrict) {
            const detail = this.formatAiErrorMessage(provider, activeModel, err);
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider} - ${activeModel}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
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
  /**
   * Generates a persuasive, structured product description honoring seller specs, dual units, and tone.
   */
  public suggestDescription(inputOrContext: string | AiDescriptionContext, materials?: string): Observable<AiFieldResult<string>> {
    const ctx: AiDescriptionContext = typeof inputOrContext === 'string'
      ? { productTitle: inputOrContext, preliminaryNotes: inputOrContext, materials }
      : { ...inputOrContext, materials: inputOrContext.materials || materials };

    const settings = this.aiSettings.settings();
    const provider = settings.provider;
    const isStrict = settings.strictNeverOffline;
    const isSilentFallback = settings.allowSilentOfflineFallback;

    const activeApiKey = this.getActiveApiKey(settings);
    const hasLiveKey = provider !== 'Offline' && !!activeApiKey && activeApiKey.length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik açıklama üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      const activeModel = this.getActiveModel(settings);
      let callObs: Observable<string>;
      if (provider === 'DeepSeek') {
        callObs = this.callDeepSeekForDescription(ctx, activeApiKey, activeModel);
      } else {
        callObs = this.callGeminiForDescription(ctx, activeApiKey, activeModel);
      }

      return callObs.pipe(
        map(desc => ({
          value: desc,
          isLive: true,
          provider: `${provider} (${activeModel})`,
          message: `✨ ${provider} satıcı ön bilgilerinizi ve ölçülerinizi koruyarak profesyonel açıklamayı canlı üretti!`
        })),
        catchError(err => {
          console.warn(`[EtsyListingAiService] Canlı ${provider} açıklama hatası:`, err);
          if (isStrict) {
            const detail = this.formatAiErrorMessage(provider, activeModel, err);
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider} - ${activeModel}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
          }
          const desc = this.generateRuleBasedDescription(ctx.productTitle || '', ctx.materials, ctx.preliminaryNotes);
          const msg = isSilentFallback
            ? `⚡ Akıllı Kural Motoru (Canlı AI yanıt veremedi, offline açıklama oluşturuldu).`
            : `⚡ Akıllı Kural Motoru ile ön bilgileriniz korunarak zengin ürün açıklaması hazırlandı.`;
          return of({
            value: desc,
            isLive: false,
            provider: 'Offline Kural Motoru',
            message: msg
          });
        })
      );
    }

    const desc = this.generateRuleBasedDescription(ctx.productTitle || '', ctx.materials, ctx.preliminaryNotes);
    return of({
      value: desc,
      isLive: false,
      provider: provider === 'Offline' ? 'Offline Kural Motoru' : `${provider} (Offline Kural)`,
      message: `⚡ Akıllı Kural Motoru ile ön bilgileriniz korunarak zengin ürün açıklaması hazırlandı.`
    });
  }

  /**
   * Generates both Variant A (Artisan Story) and Variant B (Specs Heavy) for A/B comparison.
   */
  public suggestDescriptionVariants(inputOrContext: string | AiDescriptionContext, materials?: string): Observable<DescriptionVariantsResult> {
    const ctx: AiDescriptionContext = typeof inputOrContext === 'string'
      ? { productTitle: inputOrContext, preliminaryNotes: inputOrContext, materials }
      : { ...inputOrContext, materials: inputOrContext.materials || materials };

    const ctxA: AiDescriptionContext = { ...ctx, toneStyle: 'artisan' };
    const ctxB: AiDescriptionContext = { ...ctx, toneStyle: 'specs' };

    return forkJoin({
      a: this.suggestDescription(ctxA),
      b: this.suggestDescription(ctxB)
    }).pipe(
      map(({ a, b }) => ({
        variantA: a.value,
        variantB: b.value,
        isLive: a.isLive || b.isLive,
        provider: a.provider,
        message: '✨ Hem Hikaye (A) hem de Madde Madde (B) açıklama varyantları başarıyla hazırlandı!'
      }))
    );
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

    const activeApiKey = this.getActiveApiKey(settings);
    const hasLiveKey = provider !== 'Offline' && !!activeApiKey && activeApiKey.length > 10;

    if (isStrict && !hasLiveKey) {
      return throwError(() => new Error(`⚠️ Canlı AI Zorunlu aktif fakat ${provider} API anahtarı tanımlanmamış. 'Asla Offline Kural Motoruna Düşme' seçili olduğundan sentetik listeleme üretilmedi. Lütfen üst menüdeki AI Ayarlarından API anahtarınızı kaydedin.`));
    }

    if (hasLiveKey) {
      const activeModel = this.getActiveModel(settings);
      let callObs: Observable<any>;
      if (provider === 'DeepSeek') {
        callObs = this.callDeepSeekForComplete(cleanInput, activeApiKey, activeModel, materials);
      } else {
        callObs = this.callGeminiForComplete(cleanInput, activeApiKey, activeModel, materials);
      }

      return callObs.pipe(
        map(res => ({
          title: this.normalizeTitleLength(res.title),
          category: res.category || this.detectCategoryFromKeywords(cleanInput),
          tags: this.normalizeTagsList(res.tags),
          description: res.description,
          materials: res.materials || materials || 'Handcrafted, Premium Materials',
          isLive: true,
          provider: `${provider} (${activeModel})`,
          summaryMessage: `🎉 ${provider} ile '${cleanInput || 'Ürün'}' için tüm Etsy listelemesi (Canlı Başlık, Kategori, 13 Tag, Açıklama) hazırlandı!`
        })),
        catchError(err => {
          console.warn(`[EtsyListingAiService] Canlı ${provider} tam paket hatası:`, err);
          if (isStrict) {
            const detail = this.formatAiErrorMessage(provider, activeModel, err);
            return throwError(() => new Error(`❌ Canlı AI API Hatası (${provider} - ${activeModel}): ${detail}. 'Canlı AI Zorunlu' seçili olduğundan offline kural motoruna geçilmedi.`));
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

  private formatAiErrorMessage(provider: string, model: string, err: any): string {
    const rawDetail = err?.error?.error?.message || err?.error?.message || err?.message || 'Model yanıt vermedi';
    if (provider === 'DeepSeek') {
      const lower = String(rawDetail).toLowerCase();
      const currentKey = this.getActiveApiKey(this.aiSettings.settings());
      const keySuffix = currentKey ? `(Mevcut kayıtlı anahtar: ...${currentKey.slice(-4)})` : '(Kayıtlı anahtar boş)';
      if (err?.status === 401 || lower.includes('authentication') || lower.includes('invalid') || lower.includes('api key')) {
        return `Yetkilendirme Hatası (401): DeepSeek'e gönderilen API anahtarı ${keySuffix} geçersiz bulundu! Lütfen AI Ayarları ⚙️ menüsünü açıp platform.deepseek.com/api_keys adresindeki yeni ve tam 'sk-...' anahtarınızı yapıştırıp Kaydet'e basınız.`;
      }
      if (err?.status === 402 || lower.includes('insufficient') || lower.includes('balance')) {
        return `Bakiye Hatası (402): DeepSeek hesabınızda yeterli bakiye (kredi) bulunmuyor. platform.deepseek.com adresinden bakiye yükleyiniz.`;
      }
    }
    return rawDetail;
  }

  private getActiveApiKey(settings: any): string {
    switch (settings?.provider) {
      case 'DeepSeek':
        return (settings?.deepSeekApiKey || '').trim();
      case 'OpenAI':
        return (settings?.openAiApiKey || '').trim();
      case 'Claude':
        return (settings?.claudeApiKey || '').trim();
      case 'Grok':
        return (settings?.grokApiKey || '').trim();
      case 'Gemini':
      default:
        return (settings?.geminiApiKey || '').trim();
    }
  }

  private getActiveModel(settings: any): string {
    switch (settings?.provider) {
      case 'DeepSeek':
        return (settings?.deepSeekModel || 'DeepSeek-V4-Flash').trim();
      case 'OpenAI':
        return (settings?.openAiModel || 'gpt-4o').trim();
      case 'Claude':
        return (settings?.claudeModel || 'claude-3-7-sonnet').trim();
      case 'Grok':
        return (settings?.grokModel || 'grok-3').trim();
      case 'Gemini':
      default:
        return (settings?.geminiModel || 'gemini-2.5-flash').trim();
    }
  }

  private normalizeDeepSeekModelName(model?: string): string {
    const m = (model || '').toLowerCase().trim();
    if (m.includes('reasoner') || m.includes('r1') || m.includes('pro')) {
      return 'deepseek-reasoner';
    }
    return 'deepseek-chat';
  }

  private parseTagsJsonResponse(raw: string, fallbackInput: string): string[] {
    if (!raw) return this.generateRuleBasedTags(fallbackInput);
    let clean = raw.trim();
    if (clean.startsWith('```json')) clean = clean.substring(7);
    else if (clean.startsWith('```')) clean = clean.substring(3);
    if (clean.endsWith('```')) clean = clean.substring(0, clean.length - 3);
    clean = clean.trim();

    const firstBracket = clean.indexOf('[');
    const lastBracket = clean.lastIndexOf(']');
    if (firstBracket >= 0 && lastBracket > firstBracket) {
      clean = clean.substring(firstBracket, lastBracket + 1);
    }

    try {
      const parsed = JSON.parse(clean);
      if (Array.isArray(parsed)) {
        return parsed.map((s: any) => String(s).toLowerCase().trim());
      }
      if (parsed && Array.isArray(parsed.tags)) {
        return parsed.tags.map((s: any) => String(s).toLowerCase().trim());
      }
    } catch {
      // ignore
    }
    return this.generateRuleBasedTags(fallbackInput);
  }

  private parseCompleteListingJsonResponse(raw: string, fallbackInput: string, materials?: string): any {
    if (!raw) return this.generateRuleBasedComplete(fallbackInput, materials);
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
      return this.generateRuleBasedComplete(fallbackInput, materials);
    }
  }

  private callDeepSeekForTitle(input: string, apiKey: string, model: string, category?: string): Observable<string> {
    const normalizedModel = this.normalizeDeepSeekModelName(model);
    const url = 'https://api.deepseek.com/chat/completions';
    const systemInstruction = `You are a world-class Etsy SEO specialist. Return ONLY a single line containing an optimized English Etsy title up to 140 characters.
CRITICAL LANGUAGE RULE: The seller's raw input may be in Turkish (e.g. 'El yapımı kadın çantası'), English, or rough notes. You MUST understand the Turkish product concept, materials, and purpose, but your generated title MUST ALWAYS BE 100% IN ENGLISH. NEVER output Turkish in the title.
The first 55 characters MUST contain the most critical search keywords for mobile visibility.
Separate keyword phrases with commas or pipes.
Do NOT include quotes, explanations, markdown, or greetings. Output ONLY the raw English title string.`;

    const userPrompt = `Product input from seller: "${input || 'Handmade Artisan Gift'}". Category: "${category || ''}". Generate a high-converting 140-character English Etsy title:`;

    const body: any = {
      model: normalizedModel,
      messages: [
        { role: 'system', content: systemInstruction },
        { role: 'user', content: userPrompt }
      ],
      max_tokens: 500
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.35;
    }

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'Hızlı Ürün Listeleme (Başlık Üretimi)', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const text = res?.choices?.[0]?.message?.content?.trim() || '';
        return text.replace(/^["'`]+|["'`]+$/g, '').trim();
      })
    );
  }

  private callDeepSeekForTags(input: string, apiKey: string, model: string): Observable<string[]> {
    const normalizedModel = this.normalizeDeepSeekModelName(model);
    const url = 'https://api.deepseek.com/chat/completions';
    const systemInstruction = `You are an Etsy SEO expert. Generate exactly 13 unique, high-search-volume buyer tags for this product.
CRITICAL LANGUAGE RULE: The seller's input may be in Turkish, English, or rough notes. You must understand the Turkish meaning and generate all 13 tags 100% in ENGLISH for Etsy US and international shoppers. NEVER output Turkish tags.
CRITICAL ETSY RULES:
- Each tag must be maximum 20 characters long.
- Use only lowercase English letters, numbers, and single spaces. No punctuation, no symbols, no hashtags.
- Output ONLY a valid JSON array of 13 English strings, e.g. ["tag one", "tag two", ...]`;

    const userPrompt = `Product: "${input || 'Handcrafted Gift'}". Output 13 English tags JSON array:`;

    const body: any = {
      model: normalizedModel,
      messages: [
        { role: 'system', content: systemInstruction },
        { role: 'user', content: userPrompt }
      ],
      max_tokens: 1000
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.3;
    }

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'Hızlı Ürün Listeleme (13 Etiket Üretimi)', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const raw = res?.choices?.[0]?.message?.content || '[]';
        return this.parseTagsJsonResponse(raw, input);
      })
    );
  }

  private buildDescriptionPrompts(ctx: AiDescriptionContext): { systemInstruction: string; userPrompt: string } {
    const tone = ctx.toneStyle || 'artisan';
    const isMinimalEmoji = ctx.emojiStyle === 'minimal';
    const materials = ctx.materials || 'Handcrafted, Eco-Friendly, Premium Materials';

    const systemInstruction = `You are an elite Etsy listing copywriter and SEO architect specializing in high-converting descriptions for the Etsy global marketplace.

CRITICAL LANGUAGE RULE:
The seller's input or notes may be in Turkish or rough shorthand (e.g. 'Boyut: 20x15cm, Renk: Buz Mavisi LED, Ahsap meşe taban'). You must understand all Turkish details, but the entire final output MUST BE 100% IN NATURAL, ELEGANT, HIGH-CONVERTING ENGLISH. NEVER output Turkish sentences.

STRICT SPECIFICATION & REFERENCE COMPLIANCE RULES:
1. MANDATORY SELLER MEASUREMENTS (DUAL UNITS):
   - You MUST extract and preserve ALL seller-provided measurements, dimensions, heights, widths, and weights.
   - For every metric measurement (cm/mm/kg), you MUST provide the metric value followed by the approximate imperial inches/lbs equivalent in parentheses: e.g. "Height: 25 cm (~9.8 inches)", "Width: 15 cm (~5.9 inches)".
   - NEVER invent contradictory measurements or discard the seller's numbers!
2. EXACT COLORS, LIGHTING & FINISHES:
   - Accurately translate and prominently showcase the seller's exact color shades, LED lighting colors (e.g. Ice Blue LED, Warm Amber), wood stains, and stone types in the Key Features section.
3. FIRST 160 CHARACTERS SEO HOOK:
   - The opening 1-2 sentences must immediately state the primary product concept and keywords naturally for Etsy and Google Search snippet indexing.
4. ORDER & PERSONALIZATION GUIDANCE:
   - If variations or personalization details are mentioned, include a clear 3-step '✍️ How to Order / Personalize' section.
5. TRUST & SHIPPING GUARANTEE:
   - Include secure tracked express shipping and 100% satisfaction / safe delivery guarantee.
6. FORMAT & TONE:
   - Tone: ${tone === 'specs' ? 'Specs-First & Clear Bullet Points (focus on dimensions, technical features, package contents)' : (tone === 'gift' ? 'Gift & Emotional Occasions (focus on gift-giving, celebrations, keepsakes)' : 'Artisan Story & Premium Craftsmanship (focus on handmade uniqueness, aesthetics, and story)')}.
   - Emoji style: ${isMinimalEmoji ? 'Minimalist — use clean bullet points (•) and no emoji clutter' : 'Vibrant & Modern — use clean, tasteful emojis (🌟, 📏, ✨, 🎁, 🧵, 🚚)'}.`;

    const userPrompt = `PRODUCT TITLE: "${ctx.productTitle || 'Handcrafted Artisan Product'}"
CATEGORY: "${ctx.category || 'Handmade Crafts'}"
MATERIALS: "${materials}"
${ctx.variationsSummary ? `AVAILABLE OPTIONS & VARIATIONS: "${ctx.variationsSummary}"\n` : ''}${ctx.tags && ctx.tags.length > 0 ? `SEO KEYWORDS: "${ctx.tags.join(', ')}"\n` : ''}
CRITICAL SELLER PRELIMINARY NOTES & REFERENCE SPECIFICATIONS:
"""
${ctx.preliminaryNotes || 'No specific preliminary notes provided. Generate a complete, compelling product description.'}
"""`;

    return { systemInstruction, userPrompt };
  }

  private callDeepSeekForDescription(ctx: AiDescriptionContext, apiKey: string, model: string): Observable<string> {
    const normalizedModel = this.normalizeDeepSeekModelName(model);
    const url = 'https://api.deepseek.com/chat/completions';
    const { systemInstruction, userPrompt } = this.buildDescriptionPrompts(ctx);

    const body: any = {
      model: normalizedModel,
      messages: [
        { role: 'system', content: systemInstruction },
        { role: 'user', content: userPrompt }
      ],
      max_tokens: 2500
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.5;
    }

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'Hızlı Ürün Listeleme (Açıklama Üretimi)', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        return res?.choices?.[0]?.message?.content?.trim() || this.generateRuleBasedDescription(ctx.productTitle || '', ctx.materials, ctx.preliminaryNotes);
      })
    );
  }

  private callDeepSeekForComplete(input: string, apiKey: string, model: string, materials?: string): Observable<any> {
    const normalizedModel = this.normalizeDeepSeekModelName(model);
    const url = 'https://api.deepseek.com/chat/completions';
    const systemInstruction = `You are an elite Etsy listing architect. Return ONLY a valid JSON object.
CRITICAL LANGUAGE RULE: The seller's input may be in Turkish (e.g. 'El yapımı kadın çantası'). You must understand the Turkish product concept, but ALL output fields MUST BE 100% IN ENGLISH for the global Etsy marketplace. NEVER output Turkish text.
Fields schema:
{
  "title": "English SEO title up to 140 chars, first 55 chars mobile-optimized",
  "category": "Matching Etsy Taxonomy breadcrumb e.g. Bags & Purses > Handbags > Shoulder Bags",
  "tags": ["array of exactly 13 lowercase English tags each max 20 chars"],
  "description": "Engaging formatted product description in English with emojis and bullet points",
  "materials": "Comma-separated list of materials used in English"
}`;

    const body: any = {
      model: normalizedModel,
      messages: [
        { role: 'system', content: systemInstruction },
        { role: 'user', content: `Optimize this listing for: "${input || 'Handmade Artisan Item'}". Materials: "${materials || ''}"` }
      ],
      max_tokens: 3500
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.4;
    }

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'Hızlı Ürün Listeleme (Tam Paket)', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const raw = res?.choices?.[0]?.message?.content || '{}';
        return this.parseCompleteListingJsonResponse(raw, input, materials);
      })
    );
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
CRITICAL LANGUAGE RULE: The seller's raw input may be in Turkish (e.g. 'El yapımı kadın çantası'), English, or rough notes. You MUST understand the Turkish product concept, materials, and purpose, but your generated title MUST ALWAYS BE 100% IN ENGLISH. NEVER output Turkish in the title.
The first 55 characters MUST contain the most critical search keywords for mobile visibility. 
Separate keyword phrases with commas or pipes. 
Do NOT include quotes, explanations, markdown, or greetings. Output ONLY the raw English title string.`;

    const userPrompt = `Product input from seller: "${input || 'Handmade Artisan Gift'}". Category: "${category || ''}". Generate a high-converting 140-character English Etsy title:`;

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
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', activeModel, 'Hızlı Ürün Listeleme (Başlık Üretimi)', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
        const text = res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || '';
        return text.replace(/^["'`]+|["'`]+$/g, '').trim();
      })
    );
  }

  private callGeminiForTags(input: string, apiKey: string, model: string): Observable<string[]> {
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are an Etsy SEO expert. Generate exactly 13 unique, high-search-volume buyer tags for this product.
CRITICAL LANGUAGE RULE: The seller's input may be in Turkish, English, or rough notes. You must understand the Turkish meaning and generate all 13 tags 100% in ENGLISH for Etsy US and international shoppers. NEVER output Turkish tags.
CRITICAL ETSY RULES:
- Each tag must be maximum 20 characters long.
- Use only lowercase English letters, numbers, and single spaces. No punctuation, no symbols, no hashtags.
- Output ONLY a valid JSON array of 13 English strings, e.g. ["tag one", "tag two", ...]`;

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
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', activeModel, 'Hızlı Ürün Listeleme (13 Etiket Üretimi)', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
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

  private callGeminiForDescription(ctx: AiDescriptionContext, apiKey: string, model: string): Observable<string> {
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const { systemInstruction, userPrompt } = this.buildDescriptionPrompts(ctx);

    const body = {
      systemInstruction: { parts: [{ text: systemInstruction }] },
      contents: [{ role: 'user', parts: [{ text: userPrompt }] }],
      generationConfig: {
        temperature: 0.5,
        maxOutputTokens: 2500
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', activeModel, 'Hızlı Ürün Listeleme (Açıklama Üretimi)', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
        return res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim() || this.generateRuleBasedDescription(ctx.productTitle || '', ctx.materials, ctx.preliminaryNotes);
      })
    );
  }

  private callGeminiForComplete(input: string, apiKey: string, model: string, materials?: string): Observable<any> {
    const activeModel = this.resolveGeminiModel(model);
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(activeModel)}:generateContent?key=${encodeURIComponent(apiKey)}`;
    const systemInstruction = `You are an elite Etsy listing architect. Return ONLY a valid JSON object.
CRITICAL LANGUAGE RULE: The seller's input may be in Turkish (e.g. 'El yapımı kadın çantası'). You must understand the Turkish product concept, but ALL output fields MUST BE 100% IN ENGLISH for the global Etsy marketplace. NEVER output Turkish text.
Fields schema:
{
  "title": "English SEO title up to 140 chars, first 55 chars mobile-optimized",
  "category": "Matching Etsy Taxonomy breadcrumb e.g. Bags & Purses > Handbags > Shoulder Bags",
  "tags": ["array of exactly 13 lowercase English tags each max 20 chars"],
  "description": "Engaging formatted product description in English with emojis and bullet points",
  "materials": "Comma-separated list of materials used in English"
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
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', activeModel, 'Hızlı Ürün Listeleme (Tam Paket)', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
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
- Etsy taxonomy requires selecting an official leaf category node from one of the 15 official Etsy root departments:
  1. Accessories
  2. Art & Collectibles
  3. Bags & Purses
  4. Bath & Beauty
  5. Books, Movies & Music
  6. Clothing
  7. Craft Supplies & Tools
  8. Electronics & Accessories
  9. Home & Living
  10. Jewelry
  11. Paper & Party Supplies
  12. Pet Supplies
  13. Shoes
  14. Toys & Games
  15. Weddings

COMMON VERIFIED OFFICIAL ETSY LEAF CATEGORIES:
* Art & Collectibles / 3D Models / Statues:
  - 130: Art & Collectibles > Sculpture > Figurines
  - 69: Art & Collectibles > Collectibles > Figurines & Knick Knacks
  - 128: Art & Collectibles > Sculpture
  - 129: Art & Collectibles > Sculpture > Art Objects
  - 119: Art & Collectibles > Prints
  - 2078: Art & Collectibles > Prints > Digital Prints
* Craft Supplies & Tools / 3D Blanks & Files:
  - 6785: Craft Supplies & Tools > Blanks > Figurines > Monsters & Fantasy
  - 6782: Craft Supplies & Tools > Blanks > Figurines > Animal & Natural Shapes
  - 6446: Craft Supplies & Tools > Blanks > Figurines
  - 12380: Craft Supplies & Tools > Patterns & How To > Craft Machine Files > 3D Printer Files
* Home & Living:
  - 1041: Home & Living > Lighting > Lamps
  - 1042: Home & Living > Lighting > Night Lights
  - 1054: Home & Living > Home Decor > Wall Decor
  - 1063: Home & Living > Home Decor > Candleholders
  - 943: Home & Living > Kitchen & Dining > Drinkware > Mugs
  - 992: Home & Living > Outdoor & Gardening > Planters & Pots
* Bags & Purses:
  - 132: Bags & Purses > Handbags > Shoulder Bags
  - 134: Bags & Purses > Handbags > Tote Bags
  - 133: Bags & Purses > Handbags > Crossbody Bags
  - 136: Bags & Purses > Backpacks
  - 197: Bags & Purses > Wallets & Money Clips > Wallets
* Jewelry:
  - 204: Jewelry > Necklaces
  - 211: Jewelry > Necklaces > Pendants
  - 220: Jewelry > Rings
  - 187: Jewelry > Earrings
  - 172: Jewelry > Bracelets
* Clothing:
  - 449: Clothing > Men's Clothing > Shirts & Tees > T-shirts
  - 559: Clothing > Women's Clothing > Tops & Tees > T-shirts
  - 505: Clothing > Women's Clothing > Dresses
  - 506: Clothing > Women's Clothing > Hoodies & Sweatshirts
* Accessories:
  - 25: Accessories > Hats & Head Coverings > Hats & Caps
  - 165: Accessories > Keychains & Lanyards > Keychains
* Electronics & Accessories:
  - 2079: Electronics & Accessories > Audio > Headphone & Headset Stands
  - 651: Electronics & Accessories > Cases & Covers > Phone Cases

CRITICAL INSTRUCTIONS:
1. Examine the actual physical item. If it is a 3D dragon, figurine or monster statue, categorize under Art & Collectibles > Sculpture > Figurines (130) or Craft Supplies & Tools > Blanks > Figurines > Monsters & Fantasy (6785).
2. If it is a lamp/night light, categorize under Home & Living > Lighting > Lamps (1041) or Night Lights (1042).
3. If it is a handbag/purse, do NOT classify as 3D print or decor; categorize under Bags & Purses (e.g. 132, 134).
4. If an image is provided, carefully inspect the visual shape, form, textures, and function.
5. Provide a clear 'reasoning' in Turkish explaining why this Etsy taxonomy was selected.
6. Suggest 2-3 realistic alternatives in 'alternatives' array with verified IDs.

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
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', model, 'Kategori & Taksonomi Tespiti', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
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
    const normalizedModel = this.normalizeDeepSeekModelName(model);
    const url = 'https://api.deepseek.com/chat/completions';
    const userPromptText = this.buildTaxonomyUserPrompt(title, desc, false);

    const body: any = {
      model: normalizedModel,
      messages: [
        { role: 'system', content: this.ETSY_TAXONOMY_SYSTEM_PROMPT },
        { role: 'user', content: userPromptText }
      ],
      max_tokens: 1000
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.2;
    }

    const headers = {
      'Authorization': `Bearer ${apiKey}`,
      'Content-Type': 'application/json'
    };

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'Kategori & Taksonomi Tespiti', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
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
        taxonomyId: 130,
        categoryPath: 'Art & Collectibles > Sculpture > Figurines',
        confidenceScore: 92,
        reasoning: '3D baskı, figür veya minyatür terimleri Etsy Sculptures & Figurines dalıyla eşleşti.',
        providerUsed: 'Yerel Kural Motoru',
        isLive: false,
        message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #130 Art & Collectibles > Sculpture > Figurines`,
        inputSources: sources,
        alternatives: [
          { taxonomyId: 69, categoryPath: 'Art & Collectibles > Collectibles > Figurines & Knick Knacks', confidenceScore: 85 },
          { taxonomyId: 6785, categoryPath: 'Craft Supplies & Tools > Blanks > Figurines > Monsters & Fantasy', confidenceScore: 80 },
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
      taxonomyId: 130,
      categoryPath: 'Art & Collectibles > Sculptures > Busts & Statues',
      confidenceScore: 75,
      reasoning: 'Genel el sanatı ve tasarım ürün dalı seçildi.',
      providerUsed: 'Yerel Kural Motoru',
      isLive: false,
      message: `⚡ Yerel Taksonomi Motoru ${sourcesText}: #130 Art & Collectibles > Sculpture > Figurines`,
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

  private generateRuleBasedDescription(userInput: string, materials?: string, preliminaryNotes?: string): string {
    const combined = `${userInput || ''} ${preliminaryNotes || ''}`.trim();
    const lower = combined.toLowerCase();

    // Extract seller dimension notes if present
    let customSizeLine = '';
    const sizeMatch = combined.match(/(?:boyut|ölçü|ebat|size|dimension|height|width|cm|mm|inch)[^.\n,]*/i);
    if (sizeMatch && sizeMatch[0]) {
      customSizeLine = `\n• 📏 Sizing & Custom Dimensions: ${sizeMatch[0].trim()}`;
    }

    // Extract seller color notes if present
    let customColorLine = '';
    const colorMatch = combined.match(/(?:renk|color|led|işık|shade)[^.\n,]*/i);
    if (colorMatch && colorMatch[0]) {
      customColorLine = `\n• 🎨 Color & Aesthetic Details: ${colorMatch[0].trim()}`;
    }

    if (lower.includes('çanta') || lower.includes('bag') || lower.includes('purse') || lower.includes('tote')) {
      return `✨ Artisan Handcrafted Women's Handbag — Timeless Elegance & Daily Practicality!\n\n` +
        `Expertly designed for daily versatility and refined style, this handcrafted bag combines premium artisan stitching with lightweight durability. The spacious interior and comfortable shoulder strap make it the perfect everyday companion from office to weekend outings.\n\n` +
        `🌿 Key Highlights & Features:` +
        customSizeLine + customColorLine +
        `\n• 100% Handcrafted with high-density durable reinforced stitching` +
        `\n• Adjustable and detachable comfortable shoulder strap` +
        `\n• Secure zippered inner compartments for phone, wallet, keys, and cosmetics` +
        `\n• Materials: ${materials || 'Genuine Artisan Leather / Premium Cotton Canvas & Brass Hardware'}` +
        `\n• Dimensions: Approx. 11.0" x 7.8" x 3.2" (28 x 20 x 8 cm) — Ideal compact daily size\n\n` +
        `🎁 Gift-Ready & Sustainable Packaging:\n` +
        `Shipped in an eco-friendly protective dust bag with optional premium gift wrapping. A wonderful keepsake gift for her, birthdays, bridesmaids, or Mother's Day!`;
    }

    if (lower.includes('kolye') || lower.includes('takı') || lower.includes('necklace') || lower.includes('jewelry') || lower.includes('yüzük')) {
      return `💎 Dainty 925 Sterling Silver Pendant Necklace — Minimalist Everyday Sparkle!\n\n` +
        `Handcrafted with exquisite precision and finished with anti-tarnish protective plating for everlasting shine and comfort on sensitive skin.\n\n` +
        `✨ Specifications & Details:` +
        customSizeLine + customColorLine +
        `\n• Material: ${materials || '925 Solid Sterling Silver, Hypoallergenic & Nickel-Free'}` +
        `\n• Chain Length: 18 inches + 2-inch extender (45 cm + 5 cm adjustable)` +
        `\n• Includes complimentary signature velvet jewelry pouch and microfiber polishing cloth!`;
    }

    return `🌟 Unique Handcrafted Artisan Creation — Made with Passion & Precision!\n\n` +
      `Add a touch of elegance to your home or delight someone special with this custom handcrafted masterpiece. Each piece is individually crafted for long-lasting beauty and aesthetic charm.\n\n` +
      `✨ Details & Specifications:` +
      customSizeLine + customColorLine +
      `\n• Materials: ${materials || 'Premium Eco-Friendly, Handpicked High-Quality Materials'}` +
      `\n• Fast, secure tracked shipping in protective gift-ready packaging` +
      `\n• Feel free to message our workshop for custom personalization requests!`;
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

  private trackAiUsage(
    provider: string,
    model: string,
    moduleName: string,
    promptTokens: number,
    completionTokens: number,
    totalTokens?: number,
    status: string = 'Başarılı'
  ): void {
    const prompt = Number(promptTokens) || 0;
    const comp = Number(completionTokens) || 0;
    const total = Number(totalTokens) || (prompt + comp);

    if (total <= 0 && prompt <= 0 && comp <= 0) return;

    let costUsd = 0;
    const pLower = (provider || '').toLowerCase();
    const mLower = (model || '').toLowerCase();

    if (pLower.includes('deepseek')) {
      const isReasoner = mLower.includes('reasoner') || mLower.includes('r1');
      const inRate = isReasoner ? 0.55 : 0.14;
      const outRate = isReasoner ? 2.19 : 0.28;
      costUsd = ((prompt * inRate) + (comp * outRate)) / 1_000_000;
    } else if (pLower.includes('gemini') || pLower.includes('google')) {
      costUsd = ((prompt * 0.15) + (comp * 0.60)) / 1_000_000;
    } else if (pLower.includes('openai')) {
      costUsd = ((prompt * 2.50) + (comp * 10.00)) / 1_000_000;
    }

    const payload = {
      timestamp: new Date().toISOString(),
      moduleName: moduleName || 'Hızlı Ürün Listeleme',
      provider: provider,
      modelName: model,
      promptTokens: prompt,
      completionTokens: comp,
      totalTokens: total,
      estimatedCostUsd: Number(costUsd.toFixed(6)),
      estimatedCostTry: Number((costUsd * 40.0).toFixed(4)),
      status: status,
      note: 'Web Studio Canlı AI İşlemi'
    };

    this.etsyApi.recordAiUsage(payload).subscribe({
      next: () => {},
      error: (err) => console.warn('[EtsyListingAiService] AI kullanım kaydı gönderilemedi:', err)
    });
  }
}

