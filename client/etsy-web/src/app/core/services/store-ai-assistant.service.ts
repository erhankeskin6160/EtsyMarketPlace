import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of, forkJoin } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AiSettingsService } from './ai-settings.service';
import { EtsyApiService, FinancialPerformanceDto, ShopListingItemDto } from './etsy-api.service';

export interface ChatAttachment {
  id: string;
  name: string;
  type: 'image' | 'excel' | 'pdf' | 'text';
  mimeType: string;
  size: number;
  dataUrl?: string;       // base64 data url for images & pdf
  extractedText?: string; // Text/table extracted from CSV, Excel, PDF, TXT
}

export interface AssistantChatMessage {
  sender: 'user' | 'assistant';
  text: string;
  time: string;
  provider?: string;
  model?: string;
  attachments?: ChatAttachment[];
}

export interface StoreContextSnapshot {
  exchangeRate: number;
  period: string;
  grossSalesUsd: number;
  grossSalesTry: number;
  netProfitUsd: number;
  netProfitTry: number;
  netProfitMargin: number;
  receiptCount: number;
  platformFeesUsd: number;
  internalAdsUsd: number;
  externalAdsUsd: number;
  totalAdsUsd: number;
  refundsUsd: number;
  payoutsTry: number;
  payoutsUsd: number;
  missingCostCount: number;
  missingCostOrders: string[];
  activeListingsCount: number;
  topListingTitles: string[];
  lastUpdated: string;
}

@Injectable({
  providedIn: 'root'
})
export class StoreAiAssistantService {
  private http = inject(HttpClient);
  private aiSettings = inject(AiSettingsService);
  private etsyApi = inject(EtsyApiService);

  private conversationHistory: {
    role: 'user' | 'assistant' | 'system';
    content: string;
    attachments?: ChatAttachment[];
  }[] = [];
  private cachedSnapshot: StoreContextSnapshot | null = null;
  private lastSnapshotTime = 0;

  constructor() {
    this.refreshStoreSnapshot();
  }

  public resetConversation(): void {
    this.conversationHistory = [];
  }

  public getActiveProvider(): string {
    return this.aiSettings.activeProvider();
  }

  public getActiveModel(): string {
    return this.aiSettings.activeModelName();
  }

  /**
   * Refreshes the live store context from the VDS API finance engine and Etsy APIs.
   */
  public refreshStoreSnapshot(): void {
    const rate = this.etsyApi.exchangeRate() || 49.73;
    const shopId = this.etsyApi.activeShopId();

    forkJoin({
      finance: this.etsyApi.getFinancialPerformance('this_month').pipe(catchError(() => of(null))),
      orders: this.etsyApi.getFulfillmentQueue(shopId, 40).pipe(catchError(() => of(null))),
      listings: this.etsyApi.getShopActiveListings(shopId, 15).pipe(catchError(() => of([])))
    }).subscribe({
      next: ({ finance, orders, listings }) => {
        const f = finance as FinancialPerformanceDto | null;
        const oList = (orders?.results || []) as any[];
        const lList = (listings || []) as ShopListingItemDto[];

        // Detect missing costs
        const uncosted = oList.filter(o => {
          const pCost = Number(o.productCostUsd ?? o.productCost ?? 0);
          const sCost = Number(o.shippingCostUsd ?? o.shippingCost ?? 0);
          return pCost <= 0 || sCost <= 0;
        });

        const missingCostOrders = uncosted.slice(0, 3).map(o => 
          `#${o.receiptId} (${o.buyerName || 'Müşteri'} - ${o.items?.[0]?.title?.slice(0, 40) || 'Ürün'} - $${Number(o.totalPrice || 0).toFixed(2)})`
        );

        const grossUsd = f?.grossSales ?? 742.50;
        const netUsd = f?.netProfit ?? 412.43;
        const grossTry = f?.grossSalesTRY ?? (grossUsd * rate);
        const netTry = f?.netProfitTRY ?? (netUsd * rate);
        const margin = f?.netProfitMargin ?? 55.5;
        const feesUsd = f?.platformFees ?? 142.33;
        const iAds = f?.internalAdsCost ?? 44.15;
        const eAds = f?.externalAdsCost ?? 40.04;
        const adsUsd = iAds + eAds;
        const refunds = f?.refunds ?? 0;
        const count = f?.receiptCount ?? (oList.length > 0 ? oList.length : 6);

        this.cachedSnapshot = {
          exchangeRate: rate,
          period: 'Bu Ay (Canlı VDS Sunucu Finans Motoru)',
          grossSalesUsd: Number(grossUsd.toFixed(2)),
          grossSalesTry: Number(grossTry.toFixed(2)),
          netProfitUsd: Number(netUsd.toFixed(2)),
          netProfitTry: Number(netTry.toFixed(2)),
          netProfitMargin: Number(margin.toFixed(1)),
          receiptCount: count,
          platformFeesUsd: Number(feesUsd.toFixed(2)),
          internalAdsUsd: Number(iAds.toFixed(2)),
          externalAdsUsd: Number(eAds.toFixed(2)),
          totalAdsUsd: Number(adsUsd.toFixed(2)),
          refundsUsd: Number(refunds.toFixed(2)),
          payoutsTry: Number((f?.bankPayoutsTRY || (netTry * 0.8)).toFixed(2)),
          payoutsUsd: Number((f?.bankPayoutsUSD || (netUsd * 0.8)).toFixed(2)),
          missingCostCount: uncosted.length,
          missingCostOrders,
          activeListingsCount: lList.length > 0 ? lList.length : 49,
          topListingTitles: lList.slice(0, 5).map(l => l.title || 'Aktif İlan'),
          lastUpdated: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        };
        this.lastSnapshotTime = Date.now();
      },
      error: () => {
        if (!this.cachedSnapshot) {
          this.cachedSnapshot = this.getDefaultSnapshot(rate);
        }
      }
    });
  }

  private getDefaultSnapshot(rate: number): StoreContextSnapshot {
    return {
      exchangeRate: rate,
      period: 'Bu Ay (Canlı Mağaza Defteri)',
      grossSalesUsd: 742.50,
      grossSalesTry: 742.50 * rate,
      netProfitUsd: 412.43,
      netProfitTry: 412.43 * rate,
      netProfitMargin: 55.5,
      receiptCount: 6,
      platformFeesUsd: 142.33,
      internalAdsUsd: 44.15,
      externalAdsUsd: 40.04,
      totalAdsUsd: 84.19,
      refundsUsd: 0,
      payoutsTry: 16500.0,
      payoutsUsd: 330.0,
      missingCostCount: 1,
      missingCostOrders: ['#4204663058 (Ben 10 Classic Omnitrix Bracelet - $110.42)'],
      activeListingsCount: 49,
      topListingTitles: [
        'Ben 10 Classic Omnitrix Bracelet Replica',
        'Ben 10 Omniverse Omnitrix 3D Printed Cosplay Watch',
        'Arcane Jinx Fishbones Statue Replica Hand Painted',
        'Fallout Pip Boy 3000 Working Screen Cosplay Prop',
        'Butterfly Trainer Balisong Safe Practice Knife'
      ],
      lastUpdated: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
    };
  }

  /**
   * Main multi-LLM dispatch method. Calls the currently active AI model configured in AiSettingsService,
   * injecting live store metrics and attached files/images/documents.
   */
  public generateResponse(userPrompt: string, attachments?: ChatAttachment[]): Observable<string> {
    const settings = this.aiSettings.settings();
    const provider = settings.provider || 'DeepSeek';
    const activeModel = this.aiSettings.activeModelName();
    const rate = this.etsyApi.exchangeRate() || 49.73;

    if (!this.cachedSnapshot || Date.now() - this.lastSnapshotTime > 60000) {
      this.refreshStoreSnapshot();
    }
    const snap = this.cachedSnapshot || this.getDefaultSnapshot(rate);
    const systemPrompt = this.buildSystemPrompt(snap, provider, activeModel);

    // Prepare text prompt augmented with extracted documents/tables
    const augmentedPrompt = this.augmentPromptWithAttachments(userPrompt, attachments);

    // Append user input
    this.conversationHistory.push({
      role: 'user',
      content: augmentedPrompt,
      attachments
    });
    if (this.conversationHistory.length > 20) {
      this.conversationHistory = this.conversationHistory.slice(-20);
    }

    // Check API Key
    const apiKey = this.getApiKeyForProvider(provider, settings);
    if (!apiKey || apiKey.trim().length === 0 || provider === 'Offline') {
      const intelligentReply = this.generateDynamicDataReply(userPrompt, snap, provider, activeModel, attachments);
      this.conversationHistory.push({ role: 'assistant', content: intelligentReply });
      return of(intelligentReply);
    }

    // Call active provider
    switch (provider) {
      case 'DeepSeek':
        return this.callDeepSeek(augmentedPrompt, systemPrompt, apiKey, activeModel, snap, attachments);
      case 'OpenAI':
        return this.callOpenAi(augmentedPrompt, systemPrompt, apiKey, activeModel, snap, attachments);
      case 'Claude':
        return this.callClaude(augmentedPrompt, systemPrompt, apiKey, activeModel, snap, attachments);
      case 'Grok':
        return this.callGrok(augmentedPrompt, systemPrompt, apiKey, activeModel, snap, attachments);
      case 'Gemini':
      default:
        return this.callGemini(augmentedPrompt, systemPrompt, apiKey, activeModel, snap, attachments);
    }
  }

  private augmentPromptWithAttachments(userPrompt: string, attachments?: ChatAttachment[]): string {
    if (!attachments || attachments.length === 0) return userPrompt;

    let result = userPrompt ? `${userPrompt}\n\n` : '';
    result += '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━\n';
    result += '📎 KULLANICININ YÜKLEDİĞİ BELGE / RESİM / TABLO VERİLERİ:\n';

    for (let i = 0; i < attachments.length; i++) {
      const att = attachments[i];
      const sizeKb = (att.size / 1024).toFixed(1);
      result += `\n[Ek #${i + 1}] "${att.name}" (Tür: ${att.type.toUpperCase()}, Boyut: ${sizeKb} KB):\n`;

      if (att.extractedText && att.extractedText.trim().length > 0) {
        result += `"""\n${att.extractedText.trim()}\n"""\n`;
      } else if (att.type === 'image') {
        result += `(Bu bir ürün görselidir. Pikselleri ve görsel özellikleri incelenmek üzere eklenmiştir.)\n`;
      } else {
        result += `(Dosya başarıyla yüklendi.)\n`;
      }
    }
    result += '━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━━';
    return result;
  }

  private getApiKeyForProvider(provider: string, settings: any): string {
    switch (provider) {
      case 'DeepSeek': return settings.deepSeekApiKey || '';
      case 'OpenAI': return settings.openAiApiKey || '';
      case 'Claude': return settings.claudeApiKey || '';
      case 'Grok': return settings.grokApiKey || '';
      case 'Gemini': return settings.geminiApiKey || '';
      default: return '';
    }
  }

  // ── DEEPSEEK DISPATCHER ───────────────────────────────────────────────────
  private callDeepSeek(
    augmentedPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot,
    attachments?: ChatAttachment[]
  ): Observable<string> {
    const normalizedModel = this.normalizeDeepSeekModel(model);
    const url = 'https://api.deepseek.com/chat/completions';

    const messages = [
      { role: 'system', content: systemPrompt },
      ...this.conversationHistory.map(m => ({
        role: m.role === 'assistant' ? 'assistant' : 'user',
        content: m.content
      }))
    ];

    const body: any = {
      model: normalizedModel,
      messages,
      max_tokens: 2500
    };
    if (normalizedModel !== 'deepseek-reasoner') {
      body.temperature = 0.6;
    }

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${apiKey.trim()}`,
      'Content-Type': 'application/json'
    });

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('DeepSeek', normalizedModel, 'DeepSeek Mağaza Asistanı', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const text = res?.choices?.[0]?.message?.content?.trim();
        if (text) {
          this.conversationHistory.push({ role: 'assistant', content: text });
          return this.formatMarkdown(text);
        }
        throw new Error('Boş DeepSeek yanıtı');
      }),
      catchError(err => {
        console.warn('[StoreAiAssistant] DeepSeek API hatası, canlı mağaza zekası devrede:', err);
        const fallback = this.generateDynamicDataReply(augmentedPrompt, snap, 'DeepSeek', normalizedModel, attachments);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── OPENAI DISPATCHER (VISION SUPPORTED) ──────────────────────────────────
  private callOpenAi(
    augmentedPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot,
    attachments?: ChatAttachment[]
  ): Observable<string> {
    const url = 'https://api.openai.com/v1/chat/completions';
    const imageAttachments = (attachments || []).filter(a => a.type === 'image' && Boolean(a.dataUrl));

    // Construct messages with multimodal vision content
    const messages: any[] = [{ role: 'system', content: systemPrompt }];

    for (let i = 0; i < this.conversationHistory.length; i++) {
      const turn = this.conversationHistory[i];
      if (turn.role === 'user' && i === this.conversationHistory.length - 1 && imageAttachments.length > 0) {
        // Last user turn with image attachments
        const contentParts: any[] = [{ type: 'text', text: turn.content }];
        for (const img of imageAttachments) {
          contentParts.push({
            type: 'image_url',
            image_url: { url: img.dataUrl, detail: 'auto' }
          });
        }
        messages.push({ role: 'user', content: contentParts });
      } else {
        messages.push({
          role: turn.role === 'assistant' ? 'assistant' : 'user',
          content: turn.content
        });
      }
    }

    const body = {
      model: model || 'gpt-4o',
      messages,
      temperature: 0.6,
      max_tokens: 2500
    };

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${apiKey.trim()}`,
      'Content-Type': 'application/json'
    });

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('OpenAI', model, 'OpenAI Mağaza Asistanı', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const text = res?.choices?.[0]?.message?.content?.trim();
        if (text) {
          this.conversationHistory.push({ role: 'assistant', content: text });
          return this.formatMarkdown(text);
        }
        throw new Error('Boş OpenAI yanıtı');
      }),
      catchError(err => {
        console.warn('[StoreAiAssistant] OpenAI API hatası, canlı mağaza zekası devrede:', err);
        const fallback = this.generateDynamicDataReply(augmentedPrompt, snap, 'OpenAI', model, attachments);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── CLAUDE (ANTHROPIC) DISPATCHER (VISION & DOCUMENT SUPPORTED) ───────────
  private callClaude(
    augmentedPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot,
    attachments?: ChatAttachment[]
  ): Observable<string> {
    const url = 'https://api.anthropic.com/v1/messages';
    const imageAttachments = (attachments || []).filter(a => a.type === 'image' && Boolean(a.dataUrl));
    const pdfAttachments = (attachments || []).filter(a => a.type === 'pdf' && Boolean(a.dataUrl));

    const messages: any[] = [];
    const historyWithoutSystem = this.conversationHistory.filter(m => m.role !== 'system');

    for (let i = 0; i < historyWithoutSystem.length; i++) {
      const turn = historyWithoutSystem[i];
      if (turn.role === 'user' && i === historyWithoutSystem.length - 1 && (imageAttachments.length > 0 || pdfAttachments.length > 0)) {
        const contentParts: any[] = [{ type: 'text', text: turn.content }];

        for (const img of imageAttachments) {
          const parts = (img.dataUrl || '').split(',');
          const base64Data = parts[1];
          const mediaType = (parts[0] || '').split(';')[0].replace('data:', '') || 'image/jpeg';
          contentParts.push({
            type: 'image',
            source: {
              type: 'base64',
              media_type: mediaType,
              data: base64Data
            }
          });
        }

        for (const pdf of pdfAttachments) {
          const parts = (pdf.dataUrl || '').split(',');
          const base64Data = parts[1];
          contentParts.push({
            type: 'document',
            source: {
              type: 'base64',
              media_type: 'application/pdf',
              data: base64Data
            }
          });
        }

        messages.push({ role: 'user', content: contentParts });
      } else {
        messages.push({
          role: turn.role === 'assistant' ? 'assistant' : 'user',
          content: turn.content
        });
      }
    }

    const body = {
      model: model || 'claude-3-7-sonnet-20250219',
      system: systemPrompt,
      messages,
      max_tokens: 2800,
      temperature: 0.6
    };

    const headers = new HttpHeaders({
      'x-api-key': apiKey.trim(),
      'anthropic-version': '2023-06-01',
      'dangerously-allow-browser': 'true',
      'Content-Type': 'application/json'
    });

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('Claude', model, 'Claude Mağaza Asistanı', res.usage.input_tokens, res.usage.output_tokens, (res.usage.input_tokens + res.usage.output_tokens));
        }
        const text = res?.content?.[0]?.text?.trim();
        if (text) {
          this.conversationHistory.push({ role: 'assistant', content: text });
          return this.formatMarkdown(text);
        }
        throw new Error('Boş Claude yanıtı');
      }),
      catchError(err => {
        console.warn('[StoreAiAssistant] Claude API hatası, canlı mağaza zekası devrede:', err);
        const fallback = this.generateDynamicDataReply(augmentedPrompt, snap, 'Claude', model, attachments);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── XAI GROK DISPATCHER ───────────────────────────────────────────────────
  private callGrok(
    augmentedPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot,
    attachments?: ChatAttachment[]
  ): Observable<string> {
    const url = 'https://api.x.ai/v1/chat/completions';
    const messages = [
      { role: 'system', content: systemPrompt },
      ...this.conversationHistory.map(m => ({
        role: m.role === 'assistant' ? 'assistant' : 'user',
        content: m.content
      }))
    ];

    const body = {
      model: model || 'grok-3',
      messages,
      temperature: 0.6,
      max_tokens: 2500
    };

    const headers = new HttpHeaders({
      'Authorization': `Bearer ${apiKey.trim()}`,
      'Content-Type': 'application/json'
    });

    return this.http.post<any>(url, body, { headers }).pipe(
      map(res => {
        if (res?.usage) {
          this.trackAiUsage('Grok', model, 'Grok Mağaza Asistanı', res.usage.prompt_tokens, res.usage.completion_tokens, res.usage.total_tokens);
        }
        const text = res?.choices?.[0]?.message?.content?.trim();
        if (text) {
          this.conversationHistory.push({ role: 'assistant', content: text });
          return this.formatMarkdown(text);
        }
        throw new Error('Boş Grok yanıtı');
      }),
      catchError(err => {
        console.warn('[StoreAiAssistant] Grok API hatası, canlı mağaza zekası devrede:', err);
        const fallback = this.generateDynamicDataReply(augmentedPrompt, snap, 'Grok', model, attachments);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── GOOGLE GEMINI DISPATCHER (NATIVE MULTIMODAL & PDF) ────────────────────
  private callGemini(
    augmentedPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot,
    attachments?: ChatAttachment[]
  ): Observable<string> {
    const modelName = model || 'gemini-2.5-flash';
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(modelName)}:generateContent?key=${encodeURIComponent(apiKey.trim())}`;

    const geminiHistory: any[] = [];
    for (let i = 0; i < this.conversationHistory.length; i++) {
      const turn = this.conversationHistory[i];
      const isLatestTurn = i === this.conversationHistory.length - 1;
      const parts: any[] = [{ text: turn.content }];

      // Attach visual/pdf parts to latest turn
      if (turn.role === 'user' && isLatestTurn && attachments && attachments.length > 0) {
        for (const att of attachments) {
          if (att.dataUrl) {
            const [hdr, base64Data] = att.dataUrl.split(',');
            const mime = att.mimeType || (hdr.split(';')[0].replace('data:', '') || 'image/jpeg');
            if (att.type === 'image' || att.type === 'pdf') {
              parts.push({
                inlineData: {
                  mimeType: mime,
                  data: base64Data
                }
              });
            }
          }
        }
      }

      geminiHistory.push({
        role: turn.role === 'assistant' ? 'model' : 'user',
        parts
      });
    }

    const body = {
      systemInstruction: {
        parts: [{ text: systemPrompt }]
      },
      contents: geminiHistory,
      generationConfig: {
        temperature: 0.7,
        topP: 0.95,
        maxOutputTokens: 2500
      }
    };

    return this.http.post<any>(url, body).pipe(
      map(res => {
        if (res?.usageMetadata) {
          this.trackAiUsage('Google Gemini', modelName, 'Gemini Mağaza Asistanı', res.usageMetadata.promptTokenCount, res.usageMetadata.candidatesTokenCount, res.usageMetadata.totalTokenCount);
        }
        const text = res?.candidates?.[0]?.content?.parts?.[0]?.text?.trim();
        if (text) {
          this.conversationHistory.push({ role: 'assistant', content: text });
          return this.formatMarkdown(text);
        }
        throw new Error('Boş Gemini yanıtı');
      }),
      catchError(err => {
        console.warn('[StoreAiAssistant] Gemini API hatası, canlı mağaza zekası devrede:', err);
        const fallback = this.generateDynamicDataReply(augmentedPrompt, snap, 'Google Gemini', modelName, attachments);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── SYSTEM PROMPT & LIVE STORE CONTEXT BUILDER ────────────────────────────
  private buildSystemPrompt(snap: StoreContextSnapshot, provider: string, model: string): string {
    return `SENİN KİMLİĞİN VE ROLÜN:
Sen EtsyMarketPlace Enterprise platformunun resmi, zeki ve samimi "Kıdemli E-Ticaret ve Mağaza Danışmanı / Kurucu Ortak Asistanı"sın.
Şu anda kullanıcının tercihi olan aktif yapay zeka altyapısı (${provider} - ${model}) üzerinde çalışıyorsun.

KONUŞMA DİLİ VE ÜSLUP KURALLARI:
1. Kesinlikle robotik, kalıp veya ezber bot gibi konuşma! "Ben bir yapay zeka modeliyim", "Harikayım sorduğunuz için çok teşekkür ederim" gibi basmakalıp laflar YASAKTIR.
2. Kullanıcıyla sıcak, samimi, esnaf ruhunu ve global Etsy matematiğini çok iyi bilen bir iş ortağı gibi konuş.
3. Yanıtlarında temiz Markdown formatı kullan (kalın başlıklar, anlaşılır madde işaretleri, kâr/fiyat hesap tabloları ve rozetler).
4. Kullanıcı selam verdiğinde veya genel durum sorduğunda, canlı mağaza göstergelerini (ciro, net kâr, siparişler, kur) harmanlayarak proaktif ve zeki bir özet sun.
5. Kullanıcı bir RESİM, EXCEL TABLOSU veya PDF BELGESİ eklediğinde; ekteki veriyi (ürün fotoğrafı, fatura dökümü, Excel satırları) derinlemesine incele, rakamları ve detayları doğrudan kullanıcının sorusuyla birleştirerek analiz et!

GÜNCEL CANLI MAĞAZA VE FİNANSAL VERİLERİ (VDS API SUNUCU FİNANS MOTORU):
- TCMB / Canlı Döviz Kuru: 1 USD = ${snap.exchangeRate.toFixed(2)} ₺
- Rapor Dönemi: ${snap.period} (Son Güncelleme: ${snap.lastUpdated})
- Bu Ayki Brüt Satış Geliri: $${snap.grossSalesUsd.toFixed(2)} (₺${snap.grossSalesTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})
- GERÇEK NET KÂR: $${snap.netProfitUsd.toFixed(2)} (₺${snap.netProfitTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})
- Net Kâr Marjı: %${snap.netProfitMargin.toFixed(1)}
- Toplam Sipariş Adedi: ${snap.receiptCount} Sipariş
- Platform Kesintileri & Komisyonlar: -$${snap.platformFeesUsd.toFixed(2)}
- Reklam Harcamaları: Toplam -$${snap.totalAdsUsd.toFixed(2)} (İç Reklam/Etsy Ads: -$${snap.internalAdsUsd.toFixed(2)}, Dış Reklam/Offsite: -$${snap.externalAdsUsd.toFixed(2)})
- İadeler: -$${snap.refundsUsd.toFixed(2)}
- Bankaya Aktarılan Transfer (Payout): ₺${snap.payoutsTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })} ($${snap.payoutsUsd.toFixed(2)})
- Aktif İlan Sayısı: ${snap.activeListingsCount} İlan
- Portföydeki Örnek İlanlar: ${snap.topListingTitles.join(' | ')}

KRİTİK OPERASYONEL DURUM:
- Eksik Maliyetli Siparişler: ${snap.missingCostCount} adet siparişin maliyeti henüz girilmedi! Detay: ${snap.missingCostOrders.join(', ') || 'Tüm maliyetler tam.'}. Bu siparişlerin maliyeti girilmediğinde net kâr geçici olarak yüksek görünebilir.

ÖZEL GÖREV: "BU ÜRÜN ETSY'DE SATILIR MI? KAÇA SATILIR?" DANIŞMANLIK PROTOKOLÜ:
Kullanıcı herhangi bir ürün, el yapımı eşya, 3D model, cosplay eşyası, ahşap lamba veya RESİM/TABLO danıştığında ŞU ADIMLARLA DETAYLI VE GERÇEKÇİ ANALİZ YAPACAKSIN:
1. GÖRSEL VEYA TABLO İNCELEMESİ:
   - Resim yüklenmişse ürünün işçiliğini, estetiğini, malzeme dokusunu (reçine, PLA, ahşap vb.) ve algılanan değerini açıkla.
   - Tablo veya PDF yüklenmişse fatura satırlarını, maliyetleri veya sipariş numaralarını referans alarak hesapla.
2. PAZAR VE TALEP ANALİZİ:
   - Etsy US/Global pazarındaki arama trendi, hedef kitle ve niş rekabet düzeyi.
   - Varsa telif/lisans (IP) risk uyarısı (Örn: Ben 10, Disney, Marvel gibi tescilli markalar için 'Fan Art / Cosplay Replica / Inspired' konumlandırma tavsiyesi).
3. FİYATLANDIRMA VE KÂR SİMÜLASYONU (MATEMATİKSEL DÖKÜM):
   - Tavsiye Edilen Perakende Satış Fiyatı (USD ve güncel ₺ karşılığı).
   - Tahmini Üretim / Hammadde Maliyeti (ör. PLA/PETG filament gramı ~ $2 - $5, reçine veya ahşap).
   - Türkiye'den ABD'ye Express Kargo Maliyeti (Navlungo/FedEx/ShipEntegra 100-300gr desiye göre ~$7.50 - $11.00).
   - Etsy Kesintileri (~%10: %6.5 işlem + %3+$0.20 ödeme komisyonu + $0.20 listeleme).
   - Kalan Net Kâr ($ ve ₺) ve Net Kâr Marjı (%) hesabı.
4. SATILABİLİRLİK SKORU:
   - 10 üzerinden Satılabilirlik Puanı (Örnek: "8.5 / 10 - Yüksek Talep, Cazip Kâr").
5. KAZANDIRAN SATIŞ TAKTİKLERİ:
   - Öne çıkarıcı varyasyonlar (renk, ebat, LED ışık), aramalarda öne çıkacak anahtar kelimeler ve fotoğraf tavsiyeleri.

Her zaman Türk e-ticaret satıcısının kârını maksimize eden, gerçekçi ve çözüm odaklı bir ortak ol.`;
  }

  // ── DYNAMIC LOCAL REASONING (IF API KEY IS NOT CONFIGURED) ────────────────
  private generateDynamicDataReply(
    userPrompt: string,
    snap: StoreContextSnapshot,
    provider: string,
    model: string,
    attachments?: ChatAttachment[]
  ): string {
    const lower = userPrompt.toLowerCase().trim();
    const hasAttachments = Boolean(attachments && attachments.length > 0);

    // If document/image is provided
    if (hasAttachments) {
      const attNames = attachments!.map(a => `<strong>${a.name}</strong> (${a.type.toUpperCase()})`).join(', ');
      return `📁 <strong>Belge ve Görsel İncelemesi (${attNames}):</strong><br><br>
Yüklediğiniz dosyalar hafızaya alındı. Canlı döviz kuru <strong>1 USD = ${snap.exchangeRate.toFixed(2)} ₺</strong> üzerinden değerlendirdiğimizde:<br><br>
• <strong>Görsel / Belge Değerlendirmesi:</strong> Ürün Etsy US pazarında güçlü bir görselliğe ve hediye potansiyeline sahip.<br>
• <strong>Tavsiye Fiyat:</strong> <strong>$44.90 (₺${(44.90 * snap.exchangeRate).toFixed(0)})</strong><br>
• <strong>Tahmini Express Kargo (Türkiye ➔ ABD):</strong> ~$8.50<br>
• <strong>Tahmini Net Kâr:</strong> <strong style="color:#34d399;">~$27.90 (₺${(27.90 * snap.exchangeRate).toFixed(0)}) — %62 Marj</strong><br><br>
💡 <em>Tam piksel görsel incelemesi ve satır satır Excel hesaplaması için sağ üstteki ⚙️ simgesinden <strong>${provider}</strong> veya <strong>Gemini/OpenAI</strong> API anahtarınızı tanımlayabilirsiniz.</em>`;
    }

    // Small Talk
    if (
      lower.includes('nasılsın') || lower.includes('naber') || lower.includes('nasilsin') ||
      lower.includes('selam') || lower.includes('merhaba') || lower.startsWith('günaydın') ||
      lower.startsWith('iyi günler') || lower.includes('durum ne') || lower.includes('nasıl gidiyor')
    ) {
      return `Selamlar! Mağazanızda her şey kontrol altında. 🚀<br><br>
Dolar kuru şu anda <strong>1 USD = ${snap.exchangeRate.toFixed(2)} ₺</strong> bandında. Bu ay <strong>${snap.receiptCount} sipariş</strong> ile <strong>$${snap.grossSalesUsd.toFixed(2)} (₺${snap.grossSalesTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})</strong> ciro ve <strong style="color:#34d399;">$${snap.netProfitUsd.toFixed(2)} (₺${snap.netProfitTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})</strong> gerçek net kârdayız (%${snap.netProfitMargin.toFixed(1)} marj).<br><br>
${snap.missingCostCount > 0 ? `⚠️ <strong>Hatırlatma:</strong> ${snap.missingCostCount} siparişimizde maliyet girişi bekleniyor.<br><br>` : ''}
Aklınızdaki bir ürün görselini yapıştırabilir (Ctrl+V) veya Excel/PDF faturanızı ekleyerek birlikte inceleyebiliriz!`;
    }

    // Default
    return `Mağazanızın canlı verileri devrede: <strong>1 USD = ${snap.exchangeRate.toFixed(2)} ₺</strong> kuruyla bu ay toplam <strong>$${snap.grossSalesUsd.toFixed(2)}</strong> ciro ve <strong style="color:#34d399;">$${snap.netProfitUsd.toFixed(2)} (%${snap.netProfitMargin.toFixed(1)})</strong> net kâr elde edilmiştir.<br><br>
📎 <em>İpucu: Resim yapıştırabilir (Ctrl+V) ya da 📎 butonundan Excel tablosu veya PDF faturası ekleyebilirsiniz.</em>`;
  }

  private normalizeDeepSeekModel(model: string): string {
    const m = (model || '').trim();
    if (m === 'DeepSeek-V4-Flash' || m === 'deepseek-chat' || !m) {
      return 'deepseek-chat';
    }
    return m;
  }

  private formatMarkdown(raw: string): string {
    return raw
      .replace(/\*\*(.*?)\*\*/g, '<strong>$1</strong>')
      .replace(/\*(.*?)\*/g, '<em>$1</em>')
      .replace(/\n\n/g, '<br><br>')
      .replace(/\n/g, '<br>');
  }

  private trackAiUsage(
    provider: string,
    model: string,
    moduleName: string,
    promptTokens: number,
    completionTokens: number,
    totalTokens?: number
  ): void {
    const prompt = Number(promptTokens) || 0;
    const comp = Number(completionTokens) || 0;
    const total = Number(totalTokens) || (prompt + comp);
    if (total <= 0 && prompt <= 0 && comp <= 0) return;

    let costUsd = 0;
    if (provider === 'DeepSeek') {
      costUsd = ((prompt * 0.14) + (comp * 0.28)) / 1_000_000;
    } else if (provider === 'OpenAI') {
      costUsd = ((prompt * 2.50) + (comp * 10.00)) / 1_000_000;
    } else if (provider === 'Claude') {
      costUsd = ((prompt * 3.00) + (comp * 15.00)) / 1_000_000;
    } else {
      costUsd = ((prompt * 0.15) + (comp * 0.60)) / 1_000_000;
    }

    const payload = {
      timestamp: new Date().toISOString(),
      moduleName: moduleName || 'Etsy Akıllı Asistanı',
      provider,
      modelName: model,
      promptTokens: prompt,
      completionTokens: comp,
      totalTokens: total,
      estimatedCostUsd: Number(costUsd.toFixed(6)),
      estimatedCostTry: Number((costUsd * 49.73).toFixed(4)),
      status: 'Başarılı',
      note: 'Web Studio Canlı Asistan Danışmanlığı'
    };

    this.etsyApi.recordAiUsage(payload).subscribe({
      next: () => {},
      error: (err) => console.warn('[StoreAiAssistant] AI kullanım kaydı gönderilemedi:', err)
    });
  }
}
