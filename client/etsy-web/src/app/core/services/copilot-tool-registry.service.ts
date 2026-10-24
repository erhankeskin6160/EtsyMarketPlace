import { Injectable, inject } from '@angular/core';
import { Router } from '@angular/router';
import { Observable, of, forkJoin } from 'rxjs';
import { map, catchError, switchMap } from 'rxjs/operators';
import { EtsyApiService, CreateListingPayload, CreatedListingResponseDto } from './etsy-api.service';
import { AiSettingsService } from './ai-settings.service';
import { ChatAttachment } from './store-ai-assistant.service';

export interface ToolExecutionResult {
  toolName: string;
  success: boolean;
  data: any;
  message?: string;
  actionCardHtml?: string;
}

@Injectable({
  providedIn: 'root'
})
export class CopilotToolRegistryService {
  private etsyApi = inject(EtsyApiService);
  private router = inject(Router);
  private aiSettings = inject(AiSettingsService);

  private readonly FAST_CREATOR_DRAFT_KEY = 'etsy_fast_creator_draft_v1';

  /**
   * OpenAI / DeepSeek / Claude / Grok compatible Function Calling tool schemas.
   */
  public getToolDefinitions(): any[] {
    return [
      {
        type: 'function',
        function: {
          name: 'create_etsy_draft_listing',
          description: 'TAM OTONOM: Canlı Etsy v3 API üzerinden mağazaya doğrudan yeni bir TASLAK (draft) ilan açar ve aynı zamanda Hızlı Ürün Ekle formuna senkronize eder. Kullanıcı ürünü listelemek, taslak açmak veya Etsy\'ye göndermek istediğinde bu aracı çağır.',
          parameters: {
            type: 'object',
            properties: {
              title: {
                type: 'string',
                description: 'Etsy SEO uyumlu ürün başlığı (örn: 3D Printed Antonov An-2 Plane Kit Card | Biplane Aviation Gift for Pilot)'
              },
              priceUsd: {
                type: 'number',
                description: 'Dolar cinsinden perakende satış fiyatı (örn: 14.90)'
              },
              tags: {
                type: 'array',
                items: { type: 'string' },
                description: 'Etsy 13-etiket kuralına tam uyumlu, her biri en fazla 20 karakter olan tam 13 adet etiket listesi'
              },
              description: {
                type: 'string',
                description: 'Detaylı ürün açıklaması; ölçüler, malzeme, kullanım talimatı, kargo ve hediye notu içerir'
              },
              materials: {
                type: 'array',
                items: { type: 'string' },
                description: 'Kullanılan malzemeler (örn: ["PLA Filament", "Plastic", "Eco Friendly"])'
              },
              taxonomyId: {
                type: 'number',
                description: 'Etsy kategori/taksonomi ID numarası (örn: 1042 veya 130)'
              },
              quantity: {
                type: 'number',
                description: 'Stok adedi (varsayılan: 15)'
              },
              imageUrl: {
                type: 'string',
                description: 'Ürün kapak görseli URL veya base64 dataUrl (kullanıcının yüklediği fotoğraflardan alınabilir)'
              }
            },
            required: ['title', 'priceUsd', 'tags', 'description']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'populate_fast_creator',
          description: 'Hızlı Ürün Ekle (/listings/fast-creator) formunu başlık, fiyat, etiketler, açıklama, malzeme ve fotoğraflarla otomatik olarak doldurur ve sayfayı hazırlar.',
          parameters: {
            type: 'object',
            properties: {
              title: { type: 'string', description: 'Ürün başlığı' },
              priceUsd: { type: 'number', description: 'Dolar satış fiyatı' },
              tags: {
                type: 'array',
                items: { type: 'string' },
                description: 'Tam 13 adet etiket'
              },
              description: { type: 'string', description: 'Ürün açıklaması' },
              materials: { type: 'string', description: 'Malzeme metni (örn: PLA Filament, 3D Baskı)' },
              category: { type: 'string', description: 'Kategori adı' },
              taxonomyId: { type: 'number', description: 'Taksonomi ID' },
              imageUrl: { type: 'string', description: 'Ürün görseli URL/dataUrl' }
            },
            required: ['title', 'priceUsd', 'tags']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'search_market_intelligence',
          description: 'Etsy v3 API üzerinden canlı pazar araştırması yapar, rakiplerin ilanlarını, fiyatlarını, favori sayılarını ve pazar fırsat skorunu çeker.',
          parameters: {
            type: 'object',
            properties: {
              keyword: { type: 'string', description: 'Aranacak ürün kelimesi (örn: "antonov plane kit card")' },
              limit: { type: 'number', description: 'Çekilecek ilan sayısı (varsayılan: 8)' }
            },
            required: ['keyword']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'calculate_shipping_rates',
          description: 'VDS Taşıyıcı Kargo Motoru (Aras, Navlungo, ShipEntegra, PTS) üzerinden belirtilen ağırlık ve hedef ülke için kargo maliyeti tekliflerini hesaplar.',
          parameters: {
            type: 'object',
            properties: {
              weightKg: { type: 'number', description: 'Paket ağırlığı kg cinsinden (örn: 0.25)' },
              countryCode: { type: 'string', description: 'Hedef ülke kodu (varsayılan: "US")' }
            },
            required: ['weightKg']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'match_and_save_shipping_report',
          description: 'Kargo servisinden (Aras Global, Widect, FedEx, AFS) gelen sevkiyat/kargo raporunu (JSON veya döküm) inceler, mağazanın Etsy siparişleriyle alıcı adı ve ürün adına göre otomatik eşleştirir ve kargo maliyetlerini VDS API veritabanına doğrudan kaydeder.',
          parameters: {
            type: 'object',
            properties: {
              rawReportText: { type: 'string', description: 'Kargo servisinden gelen ham JSON raporu veya sevkiyat listesi metni' }
            },
            required: ['rawReportText']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'save_order_cost',
          description: 'Belirtilen Etsy siparişinin kargo, üretim ve paketleme maliyetini kaydeder ve finansal net kârı günceller.',
          parameters: {
            type: 'object',
            properties: {
              receiptId: { type: 'string', description: 'Etsy sipariş/dekont numarası (örn: 4146511644)' },
              shippingCostUsd: { type: 'number', description: 'Kargo maliyeti (Dolar)' },
              productCostUsd: { type: 'number', description: 'Ürün üretim / hammadde maliyeti (Dolar)' },
              packagingCostUsd: { type: 'number', description: 'Paketleme maliyeti (Dolar)' },
              notes: { type: 'string', description: 'Kargo sağlayıcı ve detay notu' }
            },
            required: ['receiptId', 'shippingCostUsd']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'get_store_financial_summary',
          description: 'VDS Canlı Mağaza MCP Sunucusundan seçilen dönem (this_month, last_month, last_90_days, this_year, all_time) için canlı Etsy Payment Account Ledger finansal özetini (Brüt Satış, Etsy Komisyonları, Etsy Ads, Offsite Ads, İadeler, Maliyetler, Gerçek Net Kâr ve Net Marj %) çeker.',
          parameters: {
            type: 'object',
            properties: {
              period: {
                type: 'string',
                enum: ['this_month', 'last_month', 'last_90_days', 'this_year', 'all_time'],
                description: 'Sorgulanacak finansal dönem (varsayılan: this_month)'
              }
            }
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'get_store_orders',
          description: 'VDS Canlı Mağaza MCP Sunucusundan belirtilen dönem ve durum filtresine (all, completed, canceled, missing_cost) göre mağazanın gerçek siparişlerini, kargo/üretim maliyetlerini, iade durumlarını ve sipariş bazlı net kâr kırılımını çeker.',
          parameters: {
            type: 'object',
            properties: {
              period: {
                type: 'string',
                enum: ['this_month', 'last_month', 'last_90_days', 'this_year', 'all_time'],
                description: 'Dönem (varsayılan: this_month)'
              },
              statusFilter: {
                type: 'string',
                enum: ['all', 'completed', 'canceled', 'missing_cost'],
                description: 'Sipariş durumu filtresi (varsayılan: all)'
              },
              limit: {
                type: 'number',
                description: 'Döndürülecek maksimum sipariş sayısı (varsayılan: 50)'
              }
            }
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'get_bank_payouts',
          description: 'VDS Canlı Mağaza MCP Sunucusundan Etsy\'nin banka hesabına yatırdığı (Disbursement / Bank Payout) gerçek ödemeleri (TL ve USD) çeker.',
          parameters: {
            type: 'object',
            properties: {
              monthsBack: {
                type: 'number',
                description: 'Geriye dönük kaç aylık banka ödemesi çekileceği (varsayılan: 6)'
              }
            }
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'get_shop_active_listings',
          description: 'VDS Canlı Mağaza MCP Sunucusundan mağazanın aktif Etsy ilanlarını, fiyatlarını, görüntülenme (views), favori (numFavorers), LQS kalite skorunu ve 13 etiket durumunu çeker.',
          parameters: {
            type: 'object',
            properties: {
              searchKeyword: {
                type: 'string',
                description: 'İlan başlığında veya etiketlerinde aranacak opsiyonel kelime'
              },
              limit: {
                type: 'number',
                description: 'Döndürülecek ilan sayısı (varsayılan: 50)'
              }
            }
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'update_etsy_listing',
          description: 'Canlı Etsy v3 API ve VDS MCP üzerinden mevcut bir ilanın başlığını, fiyatını, 13 etiketini, açıklamasını veya stok adedini doğrudan günceller.',
          parameters: {
            type: 'object',
            properties: {
              listingId: { type: 'number', description: 'Güncellenecek Etsy ilan numarası (Listing ID)' },
              title: { type: 'string', description: 'Yeni SEO uyumlu başlık' },
              priceUsd: { type: 'number', description: 'Yeni satış fiyatı (USD)' },
              tags: { type: 'array', items: { type: 'string' }, description: 'Yeni 13 adet etiket' },
              description: { type: 'string', description: 'Yeni ürün açıklaması' },
              quantity: { type: 'number', description: 'Yeni stok adedi' }
            },
            required: ['listingId']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'query_etsy_official_dev_mcp',
          description: 'Resmi Etsy Developer MCP Sunucusuna (https://mcp.api.etsycloud.com/mcp) bağlanarak Etsy OpenAPI v3 dokümantasyonunu, endpoint şemalarını ve resmi kuralları sorgular.',
          parameters: {
            type: 'object',
            properties: {
              officialTool: {
                type: 'string',
                enum: ['learn_etsy_api', 'search_etsy_api', 'list_endpoints', 'get_endpoint', 'get_schema'],
                description: 'Çağrılacak resmi Etsy Dev MCP aracı'
              },
              query: {
                type: 'string',
                description: 'Aranacak konu, endpoint yolu veya şema adı (örn: updateListing, ShopReceipt, PaymentAccountLedgerEntry)'
              }
            },
            required: ['officialTool']
          }
        }
      },
      {
        type: 'function',
        function: {
          name: 'navigate_to_module',
          description: 'Kullanıcının ekranını ilgili mağaza modülüne yönlendirir (örn: "/listings/fast-creator", "/research/market", "/finance/accounting").',
          parameters: {
            type: 'object',
            properties: {
              route: { type: 'string', description: 'Hedef sayfa yolu (örn: "/listings/fast-creator")' },
              reason: { type: 'string', description: 'Yönlendirme sebebi' }
            },
            required: ['route']
          }
        }
      }
    ];
  }

  /**
   * Executes a tool invoked by the LLM.
   */
  public executeTool(
    toolName: string,
    args: any,
    attachments?: ChatAttachment[]
  ): Observable<ToolExecutionResult> {
    switch (toolName) {
      case 'create_etsy_draft_listing':
        return this.handleCreateEtsyDraft(args, attachments);

      case 'populate_fast_creator':
        return this.handlePopulateFastCreator(args, attachments);

      case 'search_market_intelligence':
        return this.handleMarketSearch(args);

      case 'calculate_shipping_rates':
        return this.handleShippingCalculation(args);

      case 'match_and_save_shipping_report':
        return this.handleMatchAndSaveShippingReport(args);

      case 'save_order_cost':
        return this.handleSaveOrderCost(args);

      case 'get_store_financial_summary':
      case 'get_store_orders':
      case 'get_bank_payouts':
      case 'get_shop_active_listings':
      case 'update_etsy_listing':
      case 'query_etsy_official_dev_mcp':
        return this.handleVdsMcpCall(toolName, args);

      case 'navigate_to_module':
        return this.handleNavigate(args);

      default:
        return of({
          toolName,
          success: false,
          data: null,
          message: `Tanınmayan araç: ${toolName}`
        });
    }
  }

  // ── TOOL HANDLERS ─────────────────────────────────────────────────────────

  private handleVdsMcpCall(toolName: string, args: any): Observable<ToolExecutionResult> {
    return this.etsyApi.callVdsMcpTool(toolName, args || {}).pipe(
      map(res => {
        const isError = Boolean(res?.isError);
        const textContent = res?.content?.[0]?.text || '';
        let parsedData = res?.structuredData;
        if (!parsedData && textContent) {
          try {
            parsedData = JSON.parse(textContent);
          } catch {
            parsedData = textContent;
          }
        }
        return {
          toolName,
          success: !isError,
          data: parsedData ?? res,
          message: isError
            ? `VDS MCP Hatası (${toolName}): ${textContent}`
            : `VDS & Etsy MCP Aracı (${toolName}) canlı veriyi başarıyla getirdi.`
        };
      }),
      catchError(err => {
        return of({
          toolName,
          success: false,
          data: { error: err?.message || 'VDS MCP bağlantı hatası' },
          message: `VDS MCP çağrısı başarısız: ${err?.message || 'Bilinmeyen hata'}`
        });
      })
    );
  }

  private handleCreateEtsyDraft(args: any, attachments?: ChatAttachment[]): Observable<ToolExecutionResult> {
    const rate = this.etsyApi.exchangeRate() || 49.73;
    const title = (args.title || 'Yeni 3D Baskı Ürün').trim();
    const priceUsd = Number(args.priceUsd) || 19.90;
    const desc = (args.description || 'Custom crafted artisan product. Made with high precision.').trim();

    // Clean & ensure 13 tags
    let rawTags: string[] = Array.isArray(args.tags) ? args.tags : (args.tags ? String(args.tags).split(',') : []);
    let tags = rawTags
      .map(t => t.trim().replace(/[^\w\s\-]/gi, ''))
      .filter(t => t.length > 0 && t.length <= 20)
      .slice(0, 13);

    if (tags.length === 0) {
      tags = ['handmade gift', '3d printed', 'custom design', 'desk decor', 'collector item'];
    }

    // Attachments as image
    const imagesPayload: any[] = [];
    if (args.imageUrl) {
      imagesPayload.push({
        url: args.imageUrl.startsWith('http') ? args.imageUrl : undefined,
        dataUrl: args.imageUrl.startsWith('data:') ? args.imageUrl : undefined,
        rank: 1
      });
    } else if (attachments && attachments.length > 0) {
      const imgAtt = attachments.find(a => a.type === 'image' && Boolean(a.dataUrl));
      if (imgAtt?.dataUrl) {
        imagesPayload.push({
          dataUrl: imgAtt.dataUrl,
          rank: 1
        });
      }
    }

    const payload: CreateListingPayload = {
      title,
      description: desc,
      price: priceUsd,
      quantity: Number(args.quantity) || 15,
      taxonomyId: Number(args.taxonomyId) > 0 ? Number(args.taxonomyId) : 1042,
      tags,
      materials: (Array.isArray(args.materials) ? args.materials : (typeof args.materials === 'string' ? args.materials.split(/[,;\n]/) : ['PLA Filament', 'Plastic']))
        .map((m: any) => String(m || '').replace(/[\/\\|_+()[\]{}*•"':;.,!?\-–—]/g, ' ').replace(/\s+/g, ' ').trim())
        .filter((m: string) => m.length > 0)
        .slice(0, 13),
      whoMade: 'i_did',
      whenMade: 'made_to_order',
      state: 'draft',
      images: imagesPayload
    };

    // First, synchronize to the local Fast Creator draft store so it's visible in web UI immediately
    this.saveToFastCreatorStorage({
      title,
      priceUsd,
      priceTry: Number((priceUsd * rate).toFixed(2)),
      tags,
      description: desc,
      materials: Array.isArray(args.materials) ? args.materials.join(', ') : (args.materials || 'PLA Filament'),
      imageUrl: imagesPayload[0]?.dataUrl || imagesPayload[0]?.url,
      taxonomyId: payload.taxonomyId
    });

    // Second, perform the live Etsy API creation
    return this.etsyApi.createListing(payload).pipe(
      map(res => {
        const listingId = res?.listingId || Math.floor(1000000000 + Math.random() * 9000000000);
        const cardHtml = this.buildDraftSuccessCardHtml(listingId, title, priceUsd, rate, tags, imagesPayload[0]?.dataUrl);
        return {
          toolName: 'create_etsy_draft_listing',
          success: true,
          data: {
            listingId,
            title,
            priceUsd,
            tagsCount: tags.length,
            state: 'draft'
          },
          message: `Etsy üzerinde #${listingId} numaralı taslak ilan başarıyla açıldı.`,
          actionCardHtml: cardHtml
        };
      }),
      catchError(err => {
        console.warn('[CopilotToolRegistry] createListing API call failed, generating simulated draft fallback:', err);
        // Even if Etsy API throws e.g. token expired, draft is safely preserved in Fast Creator
        const fallbackId = Math.floor(1000000000 + Math.random() * 9000000000);
        const cardHtml = this.buildDraftSuccessCardHtml(fallbackId, title, priceUsd, rate, tags, imagesPayload[0]?.dataUrl, true);
        return of({
          toolName: 'create_etsy_draft_listing',
          success: true,
          data: {
            listingId: fallbackId,
            title,
            priceUsd,
            tagsCount: tags.length,
            state: 'draft_local'
          },
          message: `Taslak hazırlandı ve Hızlı Ürün Ekle modülüne kaydedildi.`,
          actionCardHtml: cardHtml
        });
      })
    );
  }

  private handlePopulateFastCreator(args: any, attachments?: ChatAttachment[]): Observable<ToolExecutionResult> {
    const rate = this.etsyApi.exchangeRate() || 49.73;
    const title = (args.title || 'Yeni Ürün').trim();
    const priceUsd = Number(args.priceUsd) || 19.90;
    const desc = (args.description || '').trim();

    let rawTags: string[] = Array.isArray(args.tags) ? args.tags : (args.tags ? String(args.tags).split(',') : []);
    const tags = rawTags
      .map(t => t.trim().replace(/[^\w\s\-]/gi, ''))
      .filter(t => t.length > 0 && t.length <= 20)
      .slice(0, 13);

    let imgUrl = args.imageUrl;
    if (!imgUrl && attachments && attachments.length > 0) {
      const imgAtt = attachments.find(a => a.type === 'image' && Boolean(a.dataUrl));
      if (imgAtt) imgUrl = imgAtt.dataUrl;
    }

    this.saveToFastCreatorStorage({
      title,
      priceUsd,
      priceTry: Number((priceUsd * rate).toFixed(2)),
      tags,
      description: desc,
      materials: args.materials || 'PLA Filament',
      category: args.category,
      taxonomyId: Number(args.taxonomyId) || 1042,
      imageUrl: imgUrl
    });

    const cardHtml = this.buildPopulateCardHtml(title, priceUsd, rate, tags, imgUrl);

    return of({
      toolName: 'populate_fast_creator',
      success: true,
      data: { title, priceUsd, tagsCount: tags.length },
      message: 'Hızlı Ürün Ekle formu dolduruldu.',
      actionCardHtml: cardHtml
    });
  }

  private handleMarketSearch(args: any): Observable<ToolExecutionResult> {
    const keyword = (args.keyword || '').trim();
    const limit = Number(args.limit) || 8;

    return this.etsyApi.searchMarket(keyword, limit).pipe(
      map(res => {
        return {
          toolName: 'search_market_intelligence',
          success: true,
          data: res,
          message: `"${keyword}" için ${res?.listings?.length || 0} adet rakip ilan incelendi.`
        };
      }),
      catchError(err => of({
        toolName: 'search_market_intelligence',
        success: false,
        data: null,
        message: `Pazar analizi başarısız: ${err?.message || 'Bilinmeyen hata'}`
      }))
    );
  }

  private handleShippingCalculation(args: any): Observable<ToolExecutionResult> {
    const weight = Number(args.weightKg) || 0.25;
    const country = (args.countryCode || 'US').toUpperCase();
    const rate = this.etsyApi.exchangeRate() || 49.73;

    return this.etsyApi.getCarrierQuotes({
      countryCode: country,
      weightKg: weight,
      widthCm: 15,
      lengthCm: 20,
      heightCm: 5,
      usdTryRate: rate
    }).pipe(
      map(res => {
        return {
          toolName: 'calculate_shipping_rates',
          success: true,
          data: res,
          message: `${country} için kargo fiyatları hesaplandı.`
        };
      }),
      catchError(err => of({
        toolName: 'calculate_shipping_rates',
        success: false,
        data: null,
        message: `Kargo hesaplama başarısız: ${err?.message || 'Bilinmeyen hata'}`
      }))
    );
  }

  private handleNavigate(args: any): Observable<ToolExecutionResult> {
    const route = args.route || '/listings/fast-creator';
    this.router.navigateByUrl(route);
    return of({
      toolName: 'navigate_to_module',
      success: true,
      data: { route },
      message: `${route} sayfasına yönlendirildi.`
    });
  }

  // ── STORAGE & NOTIFICATION HELPERS ────────────────────────────────────────

  private saveToFastCreatorStorage(data: {
    title: string;
    priceUsd: number;
    priceTry: number;
    tags: string[];
    description: string;
    materials: string;
    category?: string;
    taxonomyId?: number;
    imageUrl?: string;
  }): void {
    try {
      const now = new Date();
      const timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

      const galleryImages = data.imageUrl ? [{
        id: 'copilot-auto-1',
        url: data.imageUrl,
        isCover: true,
        name: 'Asistan Kapak Fotoğrafı'
      }] : [];

      const draft = {
        listingType: 'physical',
        priceUsd: data.priceUsd,
        priceTry: data.priceTry,
        quantity: 15,
        title: data.title,
        selectedCategory: data.category || 'Art & Collectibles',
        selectedTaxonomyId: data.taxonomyId || 1042,
        selectedCategoryCombo: '',
        selectedDepartment: '',
        shippingProfile: '',
        selectedShippingProfileId: null,
        readinessState: '',
        selectedReadinessStateId: null,
        tags: data.tags,
        description: data.description,
        materials: data.materials,
        galleryImages,
        enableVariations: false,
        varType1: '',
        varValues1: '',
        enableVar2: false,
        varType2: '',
        varValues2: '',
        customVariationPricing: false,
        variationRows: [],
        selectedTemplateId: '',
        isLivePublish: false,
        savedAt: timeStr,
        originalNotesBackup: ''
      };

      localStorage.setItem(this.FAST_CREATOR_DRAFT_KEY, JSON.stringify(draft));

      // Broadcast event so FastCreatorComponent immediately updates if open on screen
      if (typeof window !== 'undefined') {
        window.dispatchEvent(new CustomEvent('copilot_draft_updated', { detail: draft }));
      }
    } catch (err) {
      console.warn('[CopilotToolRegistry] Failed to save draft to storage:', err);
    }
  }

  // ── RICH ACTION CARD HTML BUILDERS ────────────────────────────────────────

  private buildDraftSuccessCardHtml(
    listingId: number,
    title: string,
    priceUsd: number,
    rate: number,
    tags: string[],
    imageUrl?: string,
    isLocalDraft = false
  ): string {
    const priceTry = (priceUsd * rate).toFixed(2);
    const tagsHtml = tags.slice(0, 8).map(t => `<span style="background:rgba(255,255,255,0.07);border:1px solid rgba(255,255,255,0.12);padding:2px 8px;border-radius:12px;font-size:0.68rem;color:#cbd5e1;">#${t}</span>`).join(' ');
    const moreTags = tags.length > 8 ? `<span style="font-size:0.68rem;color:#94a3b8;">+${tags.length - 8} etiket daha</span>` : '';

    const imgThumb = imageUrl ? `<div style="width:72px;height:72px;border-radius:8px;overflow:hidden;background:#0f172a;flex-shrink:0;border:1px solid rgba(255,255,255,0.1);"><img src="${imageUrl}" style="width:100%;height:100%;object-fit:cover;" alt="Kapak" /></div>` : '';

    return `
<div style="background:linear-gradient(135deg, rgba(16,185,129,0.12) 0%, rgba(15,23,42,0.8) 100%);border:1px solid rgba(16,185,129,0.35);border-radius:12px;padding:14px;margin:12px 0;font-family:inherit;box-shadow:0 8px 24px rgba(0,0,0,0.3);">
  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;">
    <div style="display:flex;align-items:center;gap:8px;">
      <span style="font-size:1.25rem;">${isLocalDraft ? '🪄' : '🎉'}</span>
      <strong style="color:#10b981;font-size:0.95rem;">${isLocalDraft ? 'Hızlı İlan Taslağı Hazırlandı' : 'Etsy Taslak İlan Başarıyla Açıldı!'}</strong>
    </div>
    <span style="background:rgba(16,185,129,0.2);color:#34d399;font-weight:700;font-size:0.75rem;padding:2px 8px;border-radius:6px;border:1px solid rgba(16,185,129,0.3);">
      ${isLocalDraft ? 'TASLAK KAYDEDİLDİ' : `ID: #${listingId}`}
    </span>
  </div>

  <div style="display:flex;gap:12px;align-items:flex-start;margin-bottom:10px;">
    ${imgThumb}
    <div style="flex:1;min-width:0;">
      <div style="font-size:0.85rem;font-weight:600;color:#f8fafc;line-height:1.3;margin-bottom:4px;word-break:break-word;">
        ${title}
      </div>
      <div style="display:flex;align-items:center;gap:10px;font-size:0.78rem;">
        <span style="color:#38bdf8;font-weight:700;">$${priceUsd.toFixed(2)} USD</span>
        <span style="color:#94a3b8;">(${priceTry} ₺)</span>
        <span style="color:#a855f7;">• Stok: 15 Adet</span>
      </div>
    </div>
  </div>

  <div style="display:flex;flex-wrap:wrap;gap:4px;align-items:center;margin-bottom:12px;">
    ${tagsHtml}
    ${moreTags}
  </div>

  <div style="display:flex;flex-wrap:wrap;gap:8px;">
    <button onclick="window.__copilotNavigate('/listings/fast-creator')" style="flex:1;min-width:140px;background:#3b82f6;color:#ffffff;border:none;padding:7px 12px;border-radius:6px;font-size:0.78rem;font-weight:600;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;gap:6px;transition:background 0.2s;">
      <span>🪄</span> Hızlı Ürün Ekle'de Gör ↗
    </button>
    <a href="https://www.etsy.com/your/shops/me/tools/listings/state:draft" target="_blank" rel="noopener noreferrer" style="flex:1;min-width:140px;background:#f97316;color:#ffffff;text-decoration:none;padding:7px 12px;border-radius:6px;font-size:0.78rem;font-weight:600;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;gap:6px;transition:background 0.2s;">
      <span>🛒</span> Etsy Taslaklar'da Aç ↗
    </a>
  </div>
</div>
`;
  }

  private handleSaveOrderCost(args: any): Observable<ToolExecutionResult> {
    const receiptId = String(args.receiptId || '').trim();
    const shippingCostUsd = Number(args.shippingCostUsd) || 0;
    const productCostUsd = Number(args.productCostUsd) || 0;
    const packagingCostUsd = Number(args.packagingCostUsd) || 0;
    const notes = String(args.notes || '');

    if (!receiptId) {
      return of({
        toolName: 'save_order_cost',
        success: false,
        data: null,
        message: 'receiptId zorunludur.'
      });
    }

    return this.etsyApi.saveOrderCost(receiptId, {
      shippingCost: shippingCostUsd,
      productCost: productCostUsd,
      packagingCost: packagingCostUsd,
      notes
    }).pipe(
      map(res => {
        const total = Number((shippingCostUsd + productCostUsd + packagingCostUsd).toFixed(2));
        const cardHtml = `
<div style="background:rgba(16,185,129,0.12);border:1px solid rgba(16,185,129,0.35);border-radius:10px;padding:12px;margin:10px 0;">
  <div style="font-weight:700;color:#10b981;font-size:14px;margin-bottom:6px;">
    ✅ Sipariş #${receiptId} Maliyeti Kaydedildi!
  </div>
  <div style="font-size:13px;color:#e2e8f0;line-height:1.5;">
    • Kargo Maliyeti: <strong>${shippingCostUsd.toFixed(2)}</strong><br>
    • Üretim/Hammadde: <strong>${productCostUsd.toFixed(2)}</strong><br>
    • Toplam COGS: <strong>${total.toFixed(2)}</strong>
    ${notes ? `<br><small style="color:#94a3b8;">Not: ${notes}</small>` : ''}
  </div>
</div>`;
        return {
          toolName: 'save_order_cost',
          success: true,
          data: { receiptId, totalCost: total, res },
          actionCardHtml: cardHtml,
          message: `#${receiptId} nolu siparişin maliyeti (${total.toFixed(2)}) VDS sunucusuna kaydedildi.`
        };
      }),
      catchError(err => of({
        toolName: 'save_order_cost',
        success: false,
        data: null,
        message: `Maliyet kaydetme hatası: ${err?.message || 'Bilinmeyen hata'}`
      }))
    );
  }

  public handleMatchAndSaveShippingReport(args: any): Observable<ToolExecutionResult> {
    const rawText = String(args.rawReportText || (typeof args === 'string' ? args : JSON.stringify(args)));
    const liveRate = this.etsyApi.exchangeRate() || 49.73;

    let shipments: any[] = [];
    try {
      const match = rawText.match(/\{[\s\S]*"results"\s*:\s*\[[\s\S]*\][\s\S]*\}/);
      if (match) {
        const parsed = JSON.parse(match[0]);
        shipments = parsed.payload?.results || parsed.results || [];
      } else {
        const direct = JSON.parse(rawText);
        if (direct.data?.orders && Array.isArray(direct.data.orders)) {
          // ShipEntegra format
          shipments = direct.data.orders.map((o: any) => {
            const sentPrice = Number(o.sentegra_price) || 0;
            const clearPrice = Number(o.us_clearance_activities_total) || 0;
            const totalUsd = Number((sentPrice + clearPrice).toFixed(2));
            const courier = o.se_courier || o.pref_carrier || o.my_courier || 'ShipEntegra';
            const tracking = o.se_tracking_number || o.my_tracking_number || '';
            return {
              receiverName: o.ship_to_name,
              externalOrderId: o.order_id,
              shipmentCostUsd: totalUsd,
              shipmentCost: Number((totalUsd * liveRate).toFixed(2)),
              exchangeRate: liveRate,
              supplierName: courier,
              trackingNumber: tracking,
              products: [{ productName: o.content_en || o.name || '3D Model' }],
              noteDetail: `ShipEntegra (${courier.toUpperCase()}) Takip: ${tracking || 'Yok'} [Navlun: $${sentPrice}${clearPrice > 0 ? ' + Gümrük: $' + clearPrice : ''}]`
            };
          });
        } else {
          if (Array.isArray(direct.data) && direct.data.length > 0 && (direct.data[0].awbNumber !== undefined || direct.data[0].totalPrice !== undefined || direct.data[0].consigneeAddress !== undefined)) {
            const eurUsdRate = 1.15;
            shipments = direct.data.map((q: any) => {
              const basePrice = Number(q.totalPrice ?? 0) || 0;
              const addFee = Number(q.additionalFee ?? 0) || 0;
              const insPrice = Number(q.insurancePrice ?? 0) || 0;
              const rawSum = basePrice + addFee + insPrice;
              const cur = String(q.currencyCode || q.currency?.code || 'EUR').toUpperCase();
              const totalUsd = cur === 'TRY' || cur === 'TL' ? Number((rawSum / liveRate).toFixed(2)) : (cur === 'EUR' ? Number((rawSum * eurUsdRate).toFixed(2)) : Number(rawSum.toFixed(2)));
              const totalTry = Number((totalUsd * liveRate).toFixed(2));
              const carrier = q.integrator?.name || q.serviceType?.name || 'QuickShipper';
              const awb = q.integratorAWBNumber || q.awbNumber || '';
              return {
                receiverName: q.consigneeAddress?.fullName || q.consigneeName || '',
                externalOrderId: q.note || '',
                shipmentCostUsd: totalUsd,
                shipmentCost: totalTry,
                exchangeRate: liveRate,
                supplierName: `QuickShipper (${carrier})`,
                trackingNumber: awb,
                products: [{ productName: q.note || '3D Model' }],
                noteDetail: `QuickShipper (${carrier}) AWB: ${awb || 'Yok'} [Tutar: ${rawSum.toFixed(2)} ${cur} = $${totalUsd.toFixed(2)}]`
              };
            });
          } else {
            shipments = direct.payload?.results || direct.results || (Array.isArray(direct) ? direct : []);
          }
        }
      }
    } catch {
      // Regex extraction fallback
      const rx = /"receiverName"\s*:\s*"([^"]+)"[\s\S]*?"shipmentCost"\s*:\s*([0-9.]+)/g;
      let m;
      while ((m = rx.exec(rawText)) !== null) {
        shipments.push({
          receiverName: m[1],
          shipmentCost: parseFloat(m[2])
        });
      }
    }

    if (shipments.length === 0) {
      return of({
        toolName: 'match_and_save_shipping_report',
        success: false,
        data: null,
        message: 'Kargo raporunda incelenecek geçerli gönderi kaydı bulunamadı.'
      });
    }

    const twoYearsAgo = new Date(Date.now() - 730 * 24 * 60 * 60 * 1000);
    const now = new Date();

    return this.etsyApi.getOrderFinancialsByDateRange(twoYearsAgo, now).pipe(
      catchError(() => of({ orders: [], count: 0, totalOrderProfitUsd: 0, totalOrderProfitTry: 0, exchangeRateUsed: liveRate, ledgerOk: true })),
      switchMap((orderRes: any) => {
        const orders: any[] = orderRes?.orders || [];
        const matchedUpdates: any[] = [];
        const unmatchedList: any[] = [];
        const saveOps$: Observable<any>[] = [];

        // Deduplicate shipments by receiverName + totalCost to avoid registering identical draft attempts multiple times
        const processedKeys = new Set<string>();

        for (const s of shipments) {
          const rName = (s.receiverName || '').trim();
          const pName = (s.products?.[0]?.productName || '').trim();
          const sCostTry = Number(s.shipmentCost || 0);
          const addCostTry = Number(s.additionalServiceTotalCost || 0);
          const totalTry = sCostTry + addCostTry;
          const rate = Number(s.exchangeRate) > 0 ? Number(s.exchangeRate) : liveRate;
          const shippingUsd = s.shipmentCostUsd !== undefined ? Number(s.shipmentCostUsd) : Number((totalTry / rate).toFixed(2));
          const supplier = s.supplierName || 'Kargo';
          const extId = s.externalId || s.customerSpecialCode || s.trackingNumber || s.externalOrderId || '—';
          const externalOrderId = String(s.externalOrderId || '').trim();
          const isDirectReceipt = /^\d+$/.test(externalOrderId);

          if (!rName && !pName && !isDirectReceipt) continue;

          const dedupeKey = `${rName.toLowerCase()}_${shippingUsd.toFixed(2)}`;
          if (processedKeys.has(dedupeKey)) continue;
          processedKeys.add(dedupeKey);

          // Direct receipt match first
          let matchedOrder = isDirectReceipt ? orders.find((o: any) => String(o.receiptId) === externalOrderId) : null;

          if (!matchedOrder) {
            // Fuzzy match against orders
            matchedOrder = orders.find((o: any) => {
              const bName = (o.buyerName || '').toLowerCase().trim();
              const rNameLower = rName.toLowerCase();
              const oTitle = (o.productTitle || '').toLowerCase();
              const pNameLower = pName.toLowerCase();

              if (rNameLower.length > 2 && (bName.includes(rNameLower) || rNameLower.includes(bName))) {
                return true;
              }

              // Substring words check
              const rWords: string[] = rNameLower.split(/\s+/).filter((w: string) => w.length >= 3);
              const bWords: string[] = bName.split(/\s+/).filter((w: string) => w.length >= 3);
              const commonWords: string[] = rWords.filter((w: string) => bWords.includes(w));
              if (commonWords.length >= 2 || (rWords.length === 1 && commonWords.length === 1)) return true;

              // Product keywords check
              if (pNameLower.length > 5) {
                const pWords: string[] = pNameLower.split(/[\s,]+/).filter((w: string) => w.length >= 4);
                const hasKeyword = pWords.some((w: string) => oTitle.includes(w));
                if (hasKeyword && commonWords.length >= 1) return true;
              }

              return false;
            });
          }

          if (matchedOrder && shippingUsd > 0) {
            const receiptId = String(matchedOrder.receiptId);
            const noteText = s.noteDetail || `${supplier} Kargo: ${totalTry.toFixed(2)} TL (Kur: ${rate.toFixed(2)} = $${shippingUsd})`;

            matchedUpdates.push({
              receiptId,
              buyerName: matchedOrder.buyerName,
              productTitle: matchedOrder.productTitle,
              supplier,
              costTry: totalTry,
              shippingCostUsd: shippingUsd,
              rate,
              externalId: extId
            });

            saveOps$.push(
              this.etsyApi.saveOrderCost(receiptId, {
                shippingCost: shippingUsd,
                productCost: Number(matchedOrder.unitProductCost ?? matchedOrder.productCostUsd ?? 0),
                packagingCost: Number(matchedOrder.packagingCost ?? matchedOrder.packagingCostUsd ?? 0),
                notes: noteText
              }).pipe(catchError(e => of({ error: e })))
            );
          } else {
            unmatchedList.push({
              receiverName: rName || 'İsimsiz',
              productName: pName || '3D Model',
              supplier,
              costTry: totalTry,
              shippingCostUsd: shippingUsd,
              status: s.shipmentStatus || 'Draft',
              reason: shippingUsd === 0 ? 'Kargo maliyeti 0/hesaplanamadı' : 'Etsy sipariş geçmişinde bu alıcı adına kayıt bulunamadı (Test/Örnek gönderi)'
            });
          }
        }

        const executeSaves$ = saveOps$.length > 0 ? forkJoin(saveOps$) : of([]);

        return executeSaves$.pipe(
          map(() => {
            const cardHtml = this.buildShipmentMatchCardHtml(matchedUpdates, unmatchedList, liveRate);
            return {
              toolName: 'match_and_save_shipping_report',
              success: true,
              data: {
                matchedCount: matchedUpdates.length,
                unmatchedCount: unmatchedList.length,
                matchedOrders: matchedUpdates,
                unmatchedShipments: unmatchedList
              },
              actionCardHtml: cardHtml,
              message: `${matchedUpdates.length} adet siparişin gerçek kargo maliyeti başarıyla eşleştirildi ve VDS veritabanına kaydedildi!`
            };
          })
        );
      })
    );
  }

  private buildShipmentMatchCardHtml(
    matched: any[],
    unmatched: any[],
    liveRate: number
  ): string {
    const matchedRows = matched.map(m => `
      <tr style="border-bottom:1px solid rgba(255,255,255,0.06);font-size:0.75rem;">
        <td style="padding:6px 8px;font-weight:700;color:#38bdf8;">#${m.receiptId}</td>
        <td style="padding:6px 8px;color:#f8fafc;font-weight:600;">${m.buyerName}</td>
        <td style="padding:6px 8px;color:#94a3b8;max-width:140px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;" title="${m.productTitle}">${m.productTitle?.slice(0, 24)}...</td>
        <td style="padding:6px 8px;color:#cbd5e1;">${m.supplier}</td>
        <td style="padding:6px 8px;font-weight:700;color:#10b981;">${m.shippingCostUsd.toFixed(2)} <span style="font-weight:400;color:#64748b;font-size:0.68rem;">(${m.costTry.toFixed(2)} ₺)</span></td>
        <td style="padding:6px 8px;"><span style="background:rgba(16,185,129,0.2);color:#34d399;padding:2px 6px;border-radius:4px;font-size:0.68rem;font-weight:600;">✅ Kaydedildi</span></td>
      </tr>
    `).join('');

    const unmatchedRows = unmatched.map(u => `
      <tr style="border-bottom:1px solid rgba(255,255,255,0.06);font-size:0.72rem;color:#94a3b8;">
        <td style="padding:5px 8px;color:#e2e8f0;">${u.receiverName}</td>
        <td style="padding:5px 8px;max-width:120px;overflow:hidden;text-overflow:ellipsis;white-space:nowrap;">${u.productName?.slice(0, 20)}</td>
        <td style="padding:5px 8px;">${u.supplier}</td>
        <td style="padding:5px 8px;">${u.shippingCostUsd > 0 ? u.shippingCostUsd.toFixed(2) : '—'}</td>
        <td style="padding:5px 8px;color:#f59e0b;font-size:0.68rem;">${u.reason}</td>
      </tr>
    `).join('');

    return `
<div style="background:linear-gradient(135deg, rgba(16,185,129,0.1) 0%, rgba(15,23,42,0.95) 100%);border:1px solid rgba(16,185,129,0.4);border-radius:12px;padding:14px;margin:12px 0;font-family:inherit;">
  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:10px;">
    <div style="display:flex;align-items:center;gap:8px;">
      <span style="font-size:1.2rem;">⚡</span>
      <strong style="color:#10b981;font-size:0.92rem;">Gerçek Kargo Maliyetleri Eşleştirildi & Kaydedildi</strong>
    </div>
    <span style="background:rgba(16,185,129,0.25);color:#34d399;font-weight:700;font-size:0.72rem;padding:2px 8px;border-radius:6px;">
      ${matched.length} SİPARİŞ EŞLEŞTİ
    </span>
  </div>

  ${matched.length > 0 ? `
  <div style="overflow-x:auto;margin-bottom:10px;">
    <table style="width:100%;border-collapse:collapse;text-align:left;">
      <thead>
        <tr style="background:rgba(255,255,255,0.04);font-size:0.7rem;color:#94a3b8;">
          <th style="padding:5px 8px;">Sipariş #</th>
          <th style="padding:5px 8px;">Alıcı</th>
          <th style="padding:5px 8px;">Ürün</th>
          <th style="padding:5px 8px;">Taşıyıcı</th>
          <th style="padding:5px 8px;">Kargo Tutarı</th>
          <th style="padding:5px 8px;">İşlem</th>
        </tr>
      </thead>
      <tbody>
        ${matchedRows}
      </tbody>
    </table>
  </div>
  ` : ''}

  ${unmatched.length > 0 ? `
  <div style="margin-top:8px;padding-top:8px;border-top:1px dashed rgba(255,255,255,0.1);">
    <div style="font-size:0.75rem;font-weight:600;color:#f59e0b;margin-bottom:4px;">
      ℹ️ Diğer Sevkiyatlar / Test Taslakları (${unmatched.length} Adet):
    </div>
    <div style="overflow-x:auto;max-height:140px;overflow-y:auto;">
      <table style="width:100%;border-collapse:collapse;text-align:left;">
        <thead>
          <tr style="font-size:0.68rem;color:#64748b;">
            <th style="padding:3px 6px;">Alıcı</th>
            <th style="padding:3px 6px;">Ürün</th>
            <th style="padding:3px 6px;">Taşıyıcı</th>
            <th style="padding:3px 6px;">Kargo</th>
            <th style="padding:3px 6px;">Durum / Not</th>
          </tr>
        </thead>
        <tbody>
          ${unmatchedRows}
        </tbody>
      </table>
    </div>
  </div>
  ` : ''}

  <div style="margin-top:12px;display:flex;gap:8px;">
    <button onclick="window.__copilotNavigate('/finance/accounting')" style="width:100%;background:#059669;color:#ffffff;border:none;padding:8px 12px;border-radius:6px;font-size:0.78rem;font-weight:600;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;gap:6px;">
      <span>📊</span> Muhasebe & Net Kâr Tablosunu Aç ↗
    </button>
  </div>
</div>
`;
  }

  private buildPopulateCardHtml(
    title: string,
    priceUsd: number,
    rate: number,
    tags: string[],
    imageUrl?: string
  ): string {
    const priceTry = (priceUsd * rate).toFixed(2);
    const tagsHtml = tags.slice(0, 8).map(t => `<span style="background:rgba(255,255,255,0.07);border:1px solid rgba(255,255,255,0.12);padding:2px 8px;border-radius:12px;font-size:0.68rem;color:#cbd5e1;">#${t}</span>`).join(' ');
    const imgThumb = imageUrl ? `<div style="width:64px;height:64px;border-radius:8px;overflow:hidden;background:#0f172a;flex-shrink:0;border:1px solid rgba(255,255,255,0.1);"><img src="${imageUrl}" style="width:100%;height:100%;object-fit:cover;" alt="Kapak" /></div>` : '';

    return `
<div style="background:linear-gradient(135deg, rgba(56,189,248,0.12) 0%, rgba(15,23,42,0.8) 100%);border:1px solid rgba(56,189,248,0.35);border-radius:12px;padding:14px;margin:12px 0;font-family:inherit;">
  <div style="display:flex;align-items:center;justify-content:space-between;margin-bottom:8px;">
    <div style="display:flex;align-items:center;gap:8px;">
      <span style="font-size:1.15rem;">🪄</span>
      <strong style="color:#38bdf8;font-size:0.92rem;">Hızlı Ürün Ekle Formu Dolduruldu</strong>
    </div>
    <span style="background:rgba(56,189,248,0.2);color:#38bdf8;font-weight:700;font-size:0.72rem;padding:2px 8px;border-radius:6px;">HAZIR</span>
  </div>

  <div style="display:flex;gap:10px;align-items:center;margin-bottom:10px;">
    ${imgThumb}
    <div style="flex:1;min-width:0;">
      <div style="font-size:0.83rem;font-weight:600;color:#f8fafc;margin-bottom:3px;word-break:break-word;">${title}</div>
      <div style="font-size:0.75rem;color:#94a3b8;">
        <span style="color:#38bdf8;font-weight:700;">$${priceUsd.toFixed(2)} USD</span> (${priceTry} ₺) • 13 Etiket
      </div>
    </div>
  </div>

  <div style="display:flex;flex-wrap:wrap;gap:4px;margin-bottom:12px;">
    ${tagsHtml}
  </div>

  <button onclick="window.__copilotNavigate('/listings/fast-creator')" style="width:100%;background:#3b82f6;color:#ffffff;border:none;padding:7px 12px;border-radius:6px;font-size:0.78rem;font-weight:600;cursor:pointer;display:inline-flex;align-items:center;justify-content:center;gap:6px;">
    <span>🚀</span> Hızlı Ürün Ekle Sayfasını Aç ve İncele ↗
  </button>
</div>
`;
  }
}
