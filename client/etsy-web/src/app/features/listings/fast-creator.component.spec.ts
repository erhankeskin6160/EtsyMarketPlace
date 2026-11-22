import { ComponentFixture, TestBed } from '@angular/core/testing';
import { Injectable, signal } from '@angular/core';
import { ActivatedRoute, Router } from '@angular/router';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { FastCreatorComponent } from './fast-creator.component';

@Injectable()
class StubTranslocoLoader implements TranslocoLoader {
  getTranslation(): Observable<Translation> {
    return of({});
  }
}
import {
  CreateListingPayload,
  EtsyApiService,
  SuggestListingVariationsRequestDto,
  SuggestListingVariationsResultDto,
  VariationPricingAgentRequestDto,
  VariationPricingAgentResultDto
} from '../../core/services/etsy-api.service';
import { AiOptimizationSettings, AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { ExtensionBridgeService } from '../../core/services/extension-bridge.service';
import { EtsyListingAiService } from '../../core/services/etsy-listing-ai.service';

describe('FastCreatorComponent (Pricing Agent, AI Variations & Removal Regressions)', () => {
  let fixture: ComponentFixture<FastCreatorComponent>;
  let component: FastCreatorComponent;

  let settingsSignal = signal<AiOptimizationSettings>({
    provider: 'DeepSeek',
    geminiApiKey: 'gm-should-not-be-used',
    geminiModel: 'gemini-2.5-flash',
    geminiImageModel: 'gemini-2.5-flash-image',
    openAiApiKey: '',
    openAiModel: 'gpt-4o',
    openAiImageModel: 'gpt-image-2.5-flare',
    claudeApiKey: '',
    claudeModel: 'claude-3-7-sonnet-20250219',
    deepSeekApiKey: 'sk-ds-test-key',
    deepSeekModel: 'DeepSeek-V4-Flash',
    evrenApiKey: '',
    evrenModel: 'deepseek-v4.1-flash',
    grokApiKey: '',
    grokModel: 'grok-3',
    photoRoomApiKey: '',
    bflApiKey: '',
    ideogramApiKey: '',
    allowSilentOfflineFallback: false,
    strictNeverOffline: true,
    customModel: ''
  });

  let runVariationPricingAgentSpy: jasmine.Spy;
  let suggestListingVariationsSpy: jasmine.Spy;
  let createListingSpy: jasmine.Spy;

  function buildSingleProductPricingResult(priceUsd = 29.9): VariationPricingAgentResultDto {
    return {
      isSingleProductPricing: true,
      usedAi: true,
      providerUsed: 'DeepSeek (DeepSeek-V4-Flash)',
      searchKeywordUsed: 'wooden candle holder',
      marketBenchmark: {
        competitorCount: 12,
        minPriceUsd: 18,
        medianPriceUsd: 28,
        averagePriceUsd: 29,
        p75PriceUsd: 35,
        maxPriceUsd: 48,
        freeShippingRatioPct: 50,
        usedLiveMarketData: true,
        topCompetitors: []
      },
      shippingBenchmark: {
        targetCountryCode: 'US',
        selectedCarrierKey: 'shipentegra',
        selectedCarrierName: 'ShipEntegra',
        selectedServiceName: 'Eco Express',
        baseShippingCostUsd: 9.5,
        baseShippingCostTry: 475,
        calculatedDesi: 0.4,
        billableWeightKg: 0.4,
        estimatedDays: '3-6 İş Günü',
        hsCodeUsed: '4420909900',
        carrierQuotes: []
      },
      recommendedBasePriceUsd: priceUsd,
      recommendedBaseShippingCostUsd: 9.5,
      averageNetMarginPercent: 31.2,
      recommendedBreakEvenPriceUsd: 14.2,
      recommendedEtsyFeesUsd: 5.37,
      recommendedNetProfitUsd: 9.33,
      recommendedPricingTierLabel: 'Hedef Net Marj',
      recommendedPricingReasoning: 'Tekil ürün için zararsız hedef marj.',
      strategySummary: 'Tekil ürün fiyatı kargo ve kesintilere göre optimize edildi.',
      suggestedVarType1: '',
      suggestedVarValues1: '',
      suggestedEnableVar2: false,
      suggestedVarType2: '',
      suggestedVarValues2: '',
      agentSteps: [],
      pricedVariations: []
    };
  }

  beforeEach(async () => {
    localStorage.clear();
    settingsSignal = signal<AiOptimizationSettings>({
      provider: 'DeepSeek',
      geminiApiKey: 'gm-should-not-be-used',
      geminiModel: 'gemini-2.5-flash',
      geminiImageModel: 'gemini-2.5-flash-image',
      openAiApiKey: '',
      openAiModel: 'gpt-4o',
      openAiImageModel: 'gpt-image-2.5-flare',
      claudeApiKey: '',
      claudeModel: 'claude-3-7-sonnet-20250219',
      deepSeekApiKey: 'sk-ds-test-key',
      deepSeekModel: 'DeepSeek-V4-Flash',
      evrenApiKey: '',
      evrenModel: 'deepseek-v4.1-flash',
      grokApiKey: '',
      grokModel: 'grok-3',
      photoRoomApiKey: '',
      bflApiKey: '',
      ideogramApiKey: '',
      allowSilentOfflineFallback: false,
      strictNeverOffline: true,
      customModel: ''
    });

    runVariationPricingAgentSpy = jasmine.createSpy('runVariationPricingAgent');
    suggestListingVariationsSpy = jasmine.createSpy('suggestListingVariations');
    createListingSpy = jasmine.createSpy('createListing').and.returnValue(of({
      success: true,
      listingId: 999111,
      url: 'https://www.etsy.com/listing/999111',
      state: 'draft'
    }));

    const etsyApiStub = {
      activeShopId: signal('53236321'),
      exchangeRate: signal(50),
      getSellerTaxonomy: () => of({ results: [] }),
      getShippingProfiles: () => of({ results: [] }),
      getReadinessStates: () => of({ results: [] }),
      getReturnPolicies: () => of({ results: [] }),
      createReturnPolicy: jasmine.createSpy('createReturnPolicy').and.returnValue(of({})),
      getCarrierQuotes: jasmine.createSpy('getCarrierQuotes').and.returnValue(of({ quotes: [] })),
      runVariationPricingAgent: runVariationPricingAgentSpy,
      suggestListingVariations: suggestListingVariationsSpy,
      createListing: createListingSpy,
      updateListingState: jasmine.createSpy('updateListingState').and.returnValue(of({}))
    };

    const aiServiceStub = {
      settings: settingsSignal,
      activeModelName: () => {
        const s = settingsSignal();
        return s.provider === 'DeepSeek' ? s.deepSeekModel : s.geminiModel;
      },
      activeProvider: () => settingsSignal().provider,
      activeBadgeTextClean: () => `${settingsSignal().provider} (${settingsSignal().deepSeekModel})`,
      activeBadgeText: () => settingsSignal().provider,
      activeBadgeClass: () => 'badge-deepseek',
      clonedListing: signal<ClonedMarketListing | null>(null),
      openAiSettingsModal: jasmine.createSpy('openAiSettingsModal'),
      getActiveApiKey: (override?: string) => {
        const s = settingsSignal();
        const p = override ?? s.provider;
        if (p === 'DeepSeek') return (s.deepSeekApiKey || '').trim();
        if (p === 'OpenAI') return (s.openAiApiKey || '').trim();
        if (p === 'Gemini') return (s.geminiApiKey || '').trim();
        if (p === 'Claude') return (s.claudeApiKey || '').trim();
        if (p === 'Grok') return (s.grokApiKey || '').trim();
        return '';
      }
    };

    const extensionBridgeStub = {
      isBridgeDetected: () => false,
      checkBridgeAvailable: async () => false,
      fetchListingVariationsViaExtension: async () => null
    };

    const listingAiServiceStub = {
      suggestTitle: jasmine.createSpy('suggestTitle').and.returnValue(of({ value: '', isLive: false, provider: '', message: '' })),
      suggestCategoryMultimodal: jasmine.createSpy('suggestCategoryMultimodal').and.returnValue(of({})),
      suggestTags: jasmine.createSpy('suggestTags').and.returnValue(of({ value: [], isLive: false, provider: '', message: '' })),
      suggestDescription: jasmine.createSpy('suggestDescription').and.returnValue(of({ value: {}, isLive: false, provider: '', message: '' })),
      generateCompleteListing: jasmine.createSpy('generateCompleteListing').and.returnValue(of({}))
    };

    await TestBed.configureTestingModule({
      imports: [FastCreatorComponent],
      providers: [
        provideTransloco({
          config: {
            availableLangs: ['en', 'tr'],
            defaultLang: 'en',
            reRenderOnLangChange: true,
            missingHandler: { logMissingKey: false }
          },
          loader: StubTranslocoLoader
        }),
        { provide: EtsyApiService, useValue: etsyApiStub },
        { provide: AiSettingsService, useValue: aiServiceStub },
        { provide: ExtensionBridgeService, useValue: extensionBridgeStub },
        { provide: EtsyListingAiService, useValue: listingAiServiceStub },
        { provide: Router, useValue: { navigate: jasmine.createSpy('navigate') } },
        { provide: ActivatedRoute, useValue: { queryParams: of({}), snapshot: { queryParams: {} } } }
      ]
    }).compileComponents();

    fixture = TestBed.createComponent(FastCreatorComponent);
    component = fixture.componentInstance;
    fixture.detectChanges();
  });

  afterEach(() => {
    fixture?.destroy();
    localStorage.clear();
  });

  it('runAiPricingAgent keeps single-product mode and never adds variations when enableVariations is false', () => {
    runVariationPricingAgentSpy.and.returnValue(of(buildSingleProductPricingResult(29.9)));

    component.title = 'Minimalist Wooden Candle Holder';
    component.enableVariations = false;
    component.varValues1 = 'Old Value Should Be Ignored';
    component.enableVar2 = true;
    component.varValues2 = 'Red, Blue';

    component.runAiPricingAgent();

    expect(runVariationPricingAgentSpy).toHaveBeenCalledTimes(1);
    const reqArg = runVariationPricingAgentSpy.calls.mostRecent().args[0] as VariationPricingAgentRequestDto;
    expect(reqArg.enableVariations).toBeFalse();
    expect(reqArg.variationValues1).toBe('');
    expect(reqArg.existingVariations).toEqual([]);

    expect(component.enableVariations).toBeFalse();
    expect(component.variationRows.length).toBe(0);
    expect(component.priceUsd).toBe(29.9);
  });

  it('hasPricingVariations ignores whitespace/comma-only input and detects Group 2-only variations', () => {
    component.enableVariations = true;
    component.variationRows = [];
    component.varValues1 = ' , , ';
    component.enableVar2 = false;
    component.varValues2 = '';
    expect(component.hasPricingVariations).toBeFalse();

    component.enableVar2 = true;
    component.varValues2 = 'Black, White';
    expect(component.hasPricingVariations).toBeTrue();
  });

  it('suggestAiVariations sends product metadata + active DeepSeek key and renders Turkish AI summary', () => {
    const mockResult: SuggestListingVariationsResultDto = {
      usedAi: true,
      provider: 'DeepSeek',
      model: 'DeepSeek-V4-Flash',
      analysisSummary: 'Başlıktaki seramik kupa kapasitesi (11oz/15oz) ve açıklamadaki renkler analiz edildi.',
      warningMessage: null,
      suggestedVariations: [
        { name: 'Capacity', values: ['11 oz', '15 oz'], reason: 'Kupa hacim seçeneği' },
        { name: 'Glaze Color', values: ['Sage Green', 'Warm Cream'], reason: 'Sır rengi' }
      ],
      suggestedOfferings: [
        { key: '11 oz / Sage Green', price: 24.9, quantity: 5, active: true },
        { key: '11 oz / Warm Cream', price: 24.9, quantity: 5, active: true },
        { key: '15 oz / Sage Green', price: 28.9, quantity: 5, active: true },
        { key: '15 oz / Warm Cream', price: 28.9, quantity: 5, active: true }
      ]
    };
    suggestListingVariationsSpy.and.returnValue(of(mockResult));

    component.title = 'Botanical Handmade Ceramic Coffee Mug';
    component.description = '11oz and 15oz stoneware tea cup in sage green and warm cream.';
    component.selectedCategory = 'Home & Living > Drinkware > Mugs';
    component.tags = ['ceramic mug', 'handmade mug'];
    component.materials = 'Stoneware Clay, Lead-Free Glaze';
    component.priceUsd = 24.9;

    component.suggestAiVariations();

    expect(suggestListingVariationsSpy).toHaveBeenCalledTimes(1);
    const reqArg = suggestListingVariationsSpy.calls.mostRecent().args[0] as SuggestListingVariationsRequestDto;
    expect(reqArg.title).toContain('Ceramic Coffee Mug');
    expect(reqArg.description).toContain('11oz and 15oz');
    expect(reqArg.category).toContain('Mugs');
    expect(reqArg.tags).toEqual(['ceramic mug', 'handmade mug']);
    expect(reqArg.materials).toEqual(['Stoneware Clay', 'Lead Free Glaze']);
    expect(reqArg.provider).toBe('DeepSeek');
    expect(reqArg.apiKey).toBe('sk-ds-test-key');

    expect(component.enableVariations).toBeTrue();
    expect(component.enableVar2).toBeTrue();
    expect(component.varType1).toBe('Capacity');
    expect(component.varValues1).toBe('11 oz, 15 oz');
    expect(component.varType2).toBe('Glaze Color');
    expect(component.variationRows.length).toBe(4);
    expect(component.variationAiAnalysis?.usedAi).toBeTrue();
    expect(component.variationAiAnalysis?.summary).toContain('11oz/15oz');
  });

  it('removeVariationGroup(1) promotes Group 2 to Group 1 when Group 2 has options', () => {
    component.enableVariations = true;
    component.varType1 = 'Size';
    component.varValues1 = 'Small, Large';
    component.enableVar2 = true;
    component.varType2 = 'Finish';
    component.varValues2 = 'Matte Black, Warm White';
    component.rebuildVariationGrid();

    component.removeVariationGroup(1);

    expect(component.enableVariations).toBeTrue();
    expect(component.varType1).toBe('Finish');
    expect(component.varValues1).toBe('Matte Black, Warm White');
    expect(component.enableVar2).toBeFalse();
    expect(component.varValues2).toBe('');
    expect(component.variationRows.map(r => r.key)).toEqual(['Matte Black', 'Warm White']);
  });

  it('removeVariationGroup(1) regression: does NOT promote empty Group 2 even with default varType2', () => {
    component.enableVariations = true;
    component.varType1 = 'Boyut / Size';
    component.varValues1 = 'Small, Large';
    component.enableVar2 = true;
    component.varType2 = 'Renk / Color';
    component.varValues2 = '';
    component.priceUsd = 27.5;
    component.rebuildVariationGrid();

    component.removeVariationGroup(1);

    expect(component.enableVariations).toBeFalse();
    expect(component.enableVar2).toBeFalse();
    expect(component.varValues1).toBe('');
    expect(component.variationRows.length).toBe(0);
    expect(component.priceUsd).toBe(27.5);
  });

  it('removeVariationOption regression: removes target option cleanly when raw input has duplicates', () => {
    component.enableVariations = true;
    component.varType1 = 'Size';
    component.varValues1 = 'Small, small, Large';
    component.rebuildVariationGrid();

    // varOptions1 deduplicates to ['Small', 'Large']; index 1 is 'Large'
    expect(component.varOptions1).toEqual(['Small', 'Large']);
    component.removeVariationOption(1, 1);

    expect(component.varOptions1).toEqual(['Small']);
    expect(component.variationRows.map(r => r.key)).toEqual(['Small']);

    // Removing the last remaining option removes Group 1 cleanly
    component.removeVariationOption(1, 0);
    expect(component.enableVariations).toBeFalse();
    expect(component.variationRows.length).toBe(0);
  });

  it('removeVariationRow handles 1-group & 2-group exclusions and marks excluded 2-group rows active: false on publish', () => {
    // 1-group removal
    component.enableVariations = true;
    component.varType1 = 'Size';
    component.varValues1 = 'Small, Medium, Large';
    component.enableVar2 = false;
    component.rebuildVariationGrid();
    expect(component.variationRows.map(r => r.key)).toEqual(['Small', 'Medium', 'Large']);

    component.removeVariationRow(0);
    expect(component.varOptions1).toEqual(['Medium', 'Large']);
    expect(component.variationRows.map(r => r.key)).toEqual(['Medium', 'Large']);

    // 2-group removal + persistence across rebuildVariationGrid + publish payload
    component.title = 'Handcrafted Oak Lamp';
    component.description = 'Minimalist oak table lamp in two sizes and finishes.';
    component.priceUsd = 45;
    component.varType1 = 'Size';
    component.varValues1 = 'Small, Large';
    component.enableVar2 = true;
    component.varType2 = 'Color';
    component.varValues2 = 'Black, White';
    component.excludedVariationKeys = [];
    component.rebuildVariationGrid();
    expect(component.variationRows.length).toBe(4);

    // Remove first row ('Small / Black')
    component.removeVariationRow(0);
    expect(component.variationRows.map(r => r.key)).toEqual([
      'Small / White',
      'Large / Black',
      'Large / White'
    ]);
    expect(component.excludedVariationKeys).toEqual(['small / black']);

    // Exclusion survives rebuildVariationGrid()
    component.rebuildVariationGrid();
    expect(component.variationRows.length).toBe(3);

    // Publish syncs excluded 2-group combination as active: false for Etsy compliance
    component.publishListingToEtsy();
    expect(createListingSpy).toHaveBeenCalledTimes(1);
    const payload = createListingSpy.calls.mostRecent().args[0] as CreateListingPayload;
    const inactiveExcluded = payload.variations?.find(v => v.key === 'Small / Black');
    const activeRemaining = payload.variations?.filter(v => v.active !== false) ?? [];

    expect(inactiveExcluded).toBeDefined();
    expect(inactiveExcluded?.active).toBeFalse();
    expect(activeRemaining.length).toBe(3);
  });

  it('onVariationsToggled clears stale non-warning AI analysis when unchecked while keeping error warnings', () => {
    component.enableVariations = true;
    component.variationAiAnalysis = {
      provider: 'DeepSeek',
      model: 'DeepSeek-V4-Flash',
      usedAi: true,
      summary: 'Ürüne özel öneri',
      warning: null
    };

    component.enableVariations = false;
    component.onVariationsToggled();
    expect(component.variationAiAnalysis).toBeNull();

    component.enableVariations = true;
    component.variationAiAnalysis = {
      provider: 'DeepSeek',
      model: 'DeepSeek-V4-Flash',
      usedAi: false,
      summary: '',
      warning: 'API anahtarı bulunamadı'
    };

    component.enableVariations = false;
    component.onVariationsToggled();
    expect(component.variationAiAnalysis?.warning).toBe('API anahtarı bulunamadı');
  });

  it('publishListingToEtsy matches active row quantity on excluded 2-group inactive rows', () => {
    component.title = 'Handcrafted Oak Lamp';
    component.description = 'Minimalist oak table lamp in two sizes and finishes.';
    component.priceUsd = 45;
    component.enableVariations = true;
    component.varType1 = 'Size';
    component.varValues1 = 'Small, Large';
    component.enableVar2 = true;
    component.varType2 = 'Color';
    component.varValues2 = 'Black, White';
    component.excludedVariationKeys = [];
    component.rebuildVariationGrid();

    component.variationRows.forEach(r => (r.quantity = 5));
    component.removeVariationRow(0); // Exclude 'Small / Black'

    component.publishListingToEtsy();
    expect(createListingSpy).toHaveBeenCalled();
    const payload = createListingSpy.calls.mostRecent().args[0] as CreateListingPayload;
    const inactiveExcluded = payload.variations?.find(v => v.key === 'Small / Black');

    expect(inactiveExcluded?.active).toBeFalse();
    expect(inactiveExcluded?.quantity).toBe(5);
  });

  it('manages side competitor flyout panel state and Escape close order cleanly', () => {
    component.isShippingDrawerOpen = true;
    component.pricingAgentResult = {
      ...buildSingleProductPricingResult(29.9),
      searchKeywordUsed: 'thinking alien',
      marketBenchmark: {
        sourceModule: 'Pazar Araştırması Modülü (Etsy API v3)',
        competitorCount: 2,
        minPriceUsd: 18,
        medianPriceUsd: 24,
        averagePriceUsd: 24,
        p75PriceUsd: 29,
        maxPriceUsd: 30,
        freeShippingRatioPct: 50,
        usedLiveMarketData: true,
        topCompetitors: [],
        scannedCompetitors: [
          {
            listingId: 1,
            title: 'Thinking Alien Resin Figurine',
            priceUsd: 24.9,
            shopName: 'AlienShop',
            shopSales: 300,
            hasFreeShipping: true,
            estimatedMonthlySales: 20
          }
        ]
      }
    };

    expect(component.scannedCompetitorsCount).toBe(1);
    component.toggleCompetitorPanel();
    expect(component.isCompetitorPanelOpen).toBeTrue();

    // First Escape closes only the competitor flyout panel
    component.onEscapeKey();
    expect(component.isCompetitorPanelOpen).toBeFalse();
    expect(component.isShippingDrawerOpen).toBeTrue();

    // Second Escape closes the main shipping drawer
    component.onEscapeKey();
    expect(component.isShippingDrawerOpen).toBeFalse();

    // Closing the main shipping drawer also closes an open competitor panel
    component.isShippingDrawerOpen = true;
    component.isCompetitorPanelOpen = true;
    component.closeShippingDrawer();
    expect(component.isShippingDrawerOpen).toBeFalse();
    expect(component.isCompetitorPanelOpen).toBeFalse();
  });

  it('rescanPricingWithKeyword forwards customSearchKeyword to pricing agent request', () => {
    runVariationPricingAgentSpy.and.returnValue(of({
      ...buildSingleProductPricingResult(32.9),
      searchKeywordUsed: 'thinking alien figurine'
    }));

    component.title = 'Thinking Alien Desk Statue';
    component.enableVariations = false;

    component.rescanPricingWithKeyword('  thinking alien figurine  ');

    expect(runVariationPricingAgentSpy).toHaveBeenCalledTimes(1);
    const reqArg = runVariationPricingAgentSpy.calls.mostRecent().args[0] as VariationPricingAgentRequestDto;
    expect(reqArg.customSearchKeyword).toBe('thinking alien figurine');
    expect(component.agentCustomSearchKeyword).toBe('thinking alien figurine');
  });
});
