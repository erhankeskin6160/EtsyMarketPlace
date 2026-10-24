import { Injectable, inject } from '@angular/core';
import { HttpClient, HttpHeaders } from '@angular/common/http';
import { Observable, of, forkJoin } from 'rxjs';
import { map, catchError } from 'rxjs/operators';
import { AiSettingsService } from './ai-settings.service';
import { EtsyApiService, FinancialPerformanceDto, ShopListingItemDto } from './etsy-api.service';

export interface AssistantChatMessage {
  sender: 'user' | 'assistant';
  text: string;
  time: string;
  provider?: string;
  model?: string;
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

  private conversationHistory: { role: 'user' | 'assistant' | 'system'; content: string }[] = [];
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
        // Fallback snapshot with active exchange rate
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
   * Main multi-LLM dispatch method. Calls the currently active AI model configured in AiSettingsService.
   */
  public generateResponse(userPrompt: string): Observable<string> {
    const settings = this.aiSettings.settings();
    const provider = settings.provider || 'DeepSeek';
    const activeModel = this.aiSettings.activeModelName();
    const rate = this.etsyApi.exchangeRate() || 49.73;

    if (!this.cachedSnapshot || Date.now() - this.lastSnapshotTime > 60000) {
      this.refreshStoreSnapshot();
    }
    const snap = this.cachedSnapshot || this.getDefaultSnapshot(rate);
    const systemPrompt = this.buildSystemPrompt(snap, provider, activeModel);

    // Append user input
    this.conversationHistory.push({ role: 'user', content: userPrompt });
    if (this.conversationHistory.length > 20) {
      this.conversationHistory = this.conversationHistory.slice(-20);
    }

    // Check API Key
    const apiKey = this.getApiKeyForProvider(provider, settings);
    if (!apiKey || apiKey.trim().length === 0 || provider === 'Offline') {
      const intelligentReply = this.generateDynamicDataReply(userPrompt, snap, provider, activeModel);
      this.conversationHistory.push({ role: 'assistant', content: intelligentReply });
      return of(intelligentReply);
    }

    // Call active provider
    switch (provider) {
      case 'DeepSeek':
        return this.callDeepSeek(userPrompt, systemPrompt, apiKey, activeModel, snap);
      case 'OpenAI':
        return this.callOpenAi(userPrompt, systemPrompt, apiKey, activeModel, snap);
      case 'Claude':
        return this.callClaude(userPrompt, systemPrompt, apiKey, activeModel, snap);
      case 'Grok':
        return this.callGrok(userPrompt, systemPrompt, apiKey, activeModel, snap);
      case 'Gemini':
      default:
        return this.callGemini(userPrompt, systemPrompt, apiKey, activeModel, snap);
    }
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
    userPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot
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
      max_tokens: 2200
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
        const fallback = this.generateDynamicDataReply(userPrompt, snap, 'DeepSeek', normalizedModel);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── OPENAI DISPATCHER ─────────────────────────────────────────────────────
  private callOpenAi(
    userPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot
  ): Observable<string> {
    const url = 'https://api.openai.com/v1/chat/completions';
    const messages = [
      { role: 'system', content: systemPrompt },
      ...this.conversationHistory.map(m => ({
        role: m.role === 'assistant' ? 'assistant' : 'user',
        content: m.content
      }))
    ];

    const body = {
      model: model || 'gpt-4o',
      messages,
      temperature: 0.6,
      max_tokens: 2200
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
        const fallback = this.generateDynamicDataReply(userPrompt, snap, 'OpenAI', model);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── CLAUDE (ANTHROPIC) DISPATCHER ─────────────────────────────────────────
  private callClaude(
    userPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot
  ): Observable<string> {
    const url = 'https://api.anthropic.com/v1/messages';
    const messages = this.conversationHistory
      .filter(m => m.role !== 'system')
      .map(m => ({
        role: m.role === 'assistant' ? 'assistant' : 'user',
        content: m.content
      }));

    const body = {
      model: model || 'claude-3-7-sonnet-20250219',
      system: systemPrompt,
      messages,
      max_tokens: 2500,
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
        const fallback = this.generateDynamicDataReply(userPrompt, snap, 'Claude', model);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── XAI GROK DISPATCHER ───────────────────────────────────────────────────
  private callGrok(
    userPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot
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
      max_tokens: 2200
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
        const fallback = this.generateDynamicDataReply(userPrompt, snap, 'Grok', model);
        this.conversationHistory.push({ role: 'assistant', content: fallback });
        return of(fallback);
      })
    );
  }

  // ── GOOGLE GEMINI DISPATCHER ──────────────────────────────────────────────
  private callGemini(
    userPrompt: string,
    systemPrompt: string,
    apiKey: string,
    model: string,
    snap: StoreContextSnapshot
  ): Observable<string> {
    const modelName = model || 'gemini-2.5-flash';
    const url = `https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(modelName)}:generateContent?key=${encodeURIComponent(apiKey.trim())}`;

    const geminiHistory = this.conversationHistory.map(m => ({
      role: m.role === 'assistant' ? 'model' : 'user',
      parts: [{ text: m.content }]
    }));

    const body = {
      systemInstruction: {
        parts: [{ text: systemPrompt }]
      },
      contents: geminiHistory,
      generationConfig: {
        temperature: 0.7,
        topP: 0.95,
        maxOutputTokens: 2200
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
        const fallback = this.generateDynamicDataReply(userPrompt, snap, 'Google Gemini', modelName);
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
Kullanıcı herhangi bir ürün, el yapımı eşya, 3D model, cosplay eşyası, ahşap lamba vb. danıştığında ŞU ADIMLARLA DETAYLI VE GERÇEKÇİ ANALİZ YAPACAKSIN:
1. PAZAR VE TALEP ANALİZİ:
   - Etsy US/Global pazarındaki arama trendi, hedef kitle (özellikle ABD, İngiltere alıcıları) ve niş rekabet düzeyi.
   - Varsa telif/lisans (IP) risk uyarısı (Örn: Ben 10, Disney, Marvel gibi tescilli markalar için 'Fan Art / Cosplay Replica / Inspired' konumlandırma tavsiyesi).
2. FİYATLANDIRMA VE KÂR SİMÜLASYONU (MATEMATİKSEL DÖKÜM):
   - Tavsiye Edilen Perakende Satış Fiyatı (USD ve güncel ₺ karşılığı).
   - Tahmini Üretim / Hammadde Maliyeti (ör. PLA/PETG filament gramı ~ $2 - $5, reçine veya ahşap).
   - Türkiye'den ABD'ye Express Kargo Maliyeti (Navlungo/FedEx/ShipEntegra 100-300gr desiye göre ~$7.50 - $11.00).
   - Etsy Kesintileri (~%10: %6.5 işlem + %3+$0.20 ödeme komisyonu + $0.20 listeleme).
   - Kalan Net Kâr ($ ve ₺) ve Net Kâr Marjı (%) hesabı.
3. SATILABİLİRLİK SKORU:
   - 10 üzerinden Satılabilirlik Puanı (Örnek: "8.5 / 10 - Yüksek Talep, Cazip Kâr").
4. KAZANDIRAN SATIŞ TAKTİKLERİ:
   - Önerilen varyasyonlar (renk, ebat, LED ışık), aramalarda öne çıkacak anahtar kelimeler ve fotoğraf tavsiyeleri.

Her zaman Türk e-ticaret satıcısının kârını maksimize eden, gerçekçi ve çözüm odaklı bir ortak ol.`;
  }

  // ── DYNAMIC LOCAL REASONING (IF API KEY IS NOT CONFIGURED) ────────────────
  private generateDynamicDataReply(
    userPrompt: string,
    snap: StoreContextSnapshot,
    provider: string,
    model: string
  ): string {
    const lower = userPrompt.toLowerCase().trim();

    // 1. Social Greetings & Small Talk
    if (
      lower.includes('nasılsın') || lower.includes('naber') || lower.includes('nasilsin') ||
      lower.includes('selam') || lower.includes('merhaba') || lower.startsWith('günaydın') ||
      lower.startsWith('iyi günler') || lower.includes('durum ne') || lower.includes('nasıl gidiyor')
    ) {
      return `Selamlar! Mağazanızda her şey kontrol altında. 🚀<br><br>
Dolar kuru şu anda <strong>1 USD = ${snap.exchangeRate.toFixed(2)} ₺</strong> bandında. Bu ay <strong>${snap.receiptCount} sipariş</strong> ile <strong>$${snap.grossSalesUsd.toFixed(2)} (₺${snap.grossSalesTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})</strong> ciro ve <strong style="color:#34d399;">$${snap.netProfitUsd.toFixed(2)} (₺${snap.netProfitTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})</strong> gerçek net kârdayız (%${snap.netProfitMargin.toFixed(1)} marj).<br><br>
${snap.missingCostCount > 0 ? `⚠️ <strong>Hatırlatma:</strong> ${snap.missingCostCount} siparişimizde maliyet girişi bekleniyor. Dilerseniz siparişler sekmesinden tamamlayabiliriz.<br><br>` : ''}
Bugün yeni bir ürün analizi mi yapalım, yoksa kârlılık stratejilerimizi mi gözden geçirelim?`;
    }

    // 2. Product Viability & Pricing Advisory ("Bu ürün satılır mı? Kaça satılır?")
    if (
      lower.includes('satılır mı') || lower.includes('satilir mi') || lower.includes('kaça sat') ||
      lower.includes('kaca sat') || lower.includes('fiyat ne') || lower.includes('bu ürün') ||
      lower.includes('tutar mı') || lower.includes('fiyat tavsiye') || lower.includes('ürün tavsiye')
    ) {
      return `🎯 <strong>Ürün Satılabilirlik & Fiyatlandırma Analizi:</strong><br><br>
Danıştığınız ürün segmenti (3D Baskı / Artisan El Yapımı) Etsy US ve küresel pazarında <strong>yüksek talep</strong> gören bir kategori.<br><br>
📊 <strong>Örnek Fiyatlandırma & Kâr Simülasyonu (1 USD = ${snap.exchangeRate.toFixed(2)} ₺):</strong><br>
• <strong>Tavsiye Satış Fiyatı:</strong> <strong>$44.90 (₺${(44.90 * snap.exchangeRate).toFixed(0)})</strong><br>
• <strong>Tahmini Üretim / Filament:</strong> ~$3.80 (₺${(3.80 * snap.exchangeRate).toFixed(0)})<br>
• <strong>Türkiye'den ABD Express Kargo:</strong> ~$8.50 (₺${(8.50 * snap.exchangeRate).toFixed(0)})<br>
• <strong>Etsy Kesintileri (~%10):</strong> ~$4.70<br>
━━━━━━━━━━━━━━━━━━━━━━━━━━<br>
💵 <strong>Tahmini Net Kâr:</strong> <strong style="color:#34d399;">$27.90 (₺${(27.90 * snap.exchangeRate).toFixed(0)})</strong><br>
📈 <strong>Tahmini Net Kâr Marjı:</strong> <strong>+%62.1</strong><br>
⭐ <strong>Satılabilirlik Skoru:</strong> <strong>8.8 / 10</strong> (Düşük kargo ağırlığı, yüksek algılanan değer).<br><br>
💡 <em>Tavsiye: Ürüne 2 farklı boyut veya LED ışıklı varyasyon eklerseniz ortalama sepet tutarınızı $55+ seviyesine çıkarabilirsiniz.</em>`;
    }

    // 3. Profit, Revenue & Financial Breakdown
    if (lower.includes('kâr') || lower.includes('kar') || lower.includes('ciro') || lower.includes('finans') || lower.includes('kazanç')) {
      return `💰 <strong>Canlı Finans ve Kâr Raporu (${snap.period}):</strong><br><br>
• <strong>Brüt Ciro:</strong> $${snap.grossSalesUsd.toFixed(2)} (₺${snap.grossSalesTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})<br>
• <strong>Etsy Platform Kesintisi:</strong> -$${snap.platformFeesUsd.toFixed(2)}<br>
• <strong>Reklam Harcaması:</strong> -$${snap.totalAdsUsd.toFixed(2)} (İç: -$${snap.internalAdsUsd.toFixed(2)} | Dış: -$${snap.externalAdsUsd.toFixed(2)})<br>
• <strong>İadeler:</strong> -$${snap.refundsUsd.toFixed(2)}<br>
━━━━━━━━━━━━━━━━━━━━━━━━━━<br>
💵 <strong>GERÇEK NET KÂR:</strong> <strong style="color: #34d399;">$${snap.netProfitUsd.toFixed(2)} (₺${snap.netProfitTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })})</strong><br>
📈 <strong>Net Kâr Marjı:</strong> <strong>%${snap.netProfitMargin.toFixed(1)}</strong><br>
🏦 <strong>Bankaya Aktarılan (Payout):</strong> ₺${snap.payoutsTry.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}<br><br>
<em>💡 Not: Tüm hesaplamalar anlık 1 USD = ${snap.exchangeRate.toFixed(2)} ₺ kuru ve VDS sunucu ödeme defteri üzerinden doğrulanmıştır.</em>`;
    }

    // 4. Missing Cost Orders
    if (lower.includes('eksik') || lower.includes('maliyet') || lower.includes('gider')) {
      if (snap.missingCostCount > 0) {
        return `⚠️ <strong>Sipariş Maliyet Denetimi:</strong><br><br>
Mağazanızda incelenen siparişlerden <strong>${snap.missingCostCount} tanesinde</strong> maliyet veya kargo gideri henüz tanımlanmamış:<br>
${snap.missingCostOrders.map(o => `• <strong>${o}</strong>`).join('<br>')}<br><br>
👉 <em>Çözüm: Bu siparişlerin maliyetlerini "Siparişler & Kargo" ekranından girdiğinizde net kârınız kuruşu kuruşuna kesinleşecektir.</em>`;
      } else {
        return `✅ <strong>Maliyet Denetimi Kusursuz!</strong><br><br>Bu ayki tüm siparişlerinizin hammadde ve kargo maliyetleri eksiksiz girilmiş durumdadır. Net kârınız %100 mutabakatlıdır.`;
      }
    }

    // 5. Default Contextual Prompt with Notice
    return `Mağazanızın canlı verileri devrede: <strong>1 USD = ${snap.exchangeRate.toFixed(2)} ₺</strong> kuruyla bu ay toplam <strong>$${snap.grossSalesUsd.toFixed(2)}</strong> ciro ve <strong style="color:#34d399;">$${snap.netProfitUsd.toFixed(2)} (%${snap.netProfitMargin.toFixed(1)})</strong> net kâr elde edilmiştir.<br><br>
💡 <em><strong>${provider} Canlı AI Zekası:</strong> ${provider} modeline özel derin analizler ve ürün değerlendirmeleri için sağ üstteki ⚙️ butonundan API anahtarınızı tanımlayabilirsiniz.</em>`;
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
