import { Component, OnInit, OnDestroy, HostListener, inject, ViewChild, ElementRef, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { DomSanitizer, SafeUrl } from '@angular/platform-browser';
import { Router, ActivatedRoute } from '@angular/router';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService, EtsyTaxonomyItemDto, EtsyReturnPolicyDto, VariationPricingAgentResultDto, AgentCarrierQuoteSummaryDto, CompetitorListingInventoryDto } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { ExtensionBridgeService } from '../../core/services/extension-bridge.service';
import { EtsyListingAiService, CategoryAiSuggestion, TaxonomyCandidateItem, AiDescriptionContext, AiShippingContext, DescriptionVariantsResult } from '../../core/services/etsy-listing-ai.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';
import { PricingCompetitorsDrawerComponent } from './components/pricing-competitors-drawer.component';
import { buildTaxonomyShortlist, taxonomyLeafName } from '../../core/utils/taxonomy-shortlist';
import { extractClientValidationErrors, FieldValidationError } from '../../core/utils/validation-error.util';
import { buildEtsyVariationBookmarkletUrl, parseEtsyVariationsFromClipboard, parseInlineVariationOptionPrice, resolveVariationOptionPairPrice } from '../../core/utils/etsy-variation-parser';
import { extractEtsyListingRef } from '../../core/utils/etsy-listing-url.util';
import { calculateEtsyNetProfit, calculateVariationNetProfits, EtsyNetProfitBreakdown, VariationNetProfitRow, VariationNetProfitSummary } from '../../core/utils/etsy-fee-calculator';
import { buildEtsyDescriptionTemplate } from '../../core/utils/etsy-description-template.util';
import { Observable, of } from 'rxjs';
import { catchError, map, switchMap, tap } from 'rxjs/operators';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

export interface GalleryImage {
  id: string;
  url: string;
  isCover: boolean;
  name: string;
}

export interface CreatorTemplate {
  id: string;
  name: string;
  category: string;
  defaultPrice: number;
  sampleTitle: string;
  sampleTags: string[];
  sampleDesc: string;
  materials: string;
}

export interface FastCreatorDraft {
  listingType: 'physical' | 'digital';
  priceUsd: number | null;
  priceTry: number;
  quantity: number | null;
  title: string;
  selectedCategory: string;
  selectedTaxonomyId: number;
  selectedCategoryCombo: string;
  selectedDepartment: string;
  shippingProfile: string;
  selectedShippingProfileId: number | null;
  readinessState: string;
  selectedReadinessStateId: number | null;
  selectedReturnPolicyId?: number | null;
  tags: string[];
  description: string;
  materials: string;
  galleryImages: GalleryImage[];
  enableVariations: boolean;
  varType1: string;
  varValues1: string;
  enableVar2: boolean;
  varType2: string;
  varValues2: string;
  customVariationPricing: boolean;
  variationRows: VariationRow[];
  excludedVariationKeys?: string[];
  selectedTemplateId: string;
  isLivePublish: boolean;
  savedAt: string;
  originalNotesBackup?: string;
  estimatedShippingCostUsd?: number | null;
  includeOffsiteAdsFee?: boolean;
}

export interface VariationRow {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
  imageRank?: number;
  agentNetProfitUsd?: number;
  agentNetMarginPct?: number;
  agentTierLabel?: string;
  agentShippingUsd?: number;
}

@Component({
  selector: 'app-fast-creator',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective, AiLogoComponent, PricingCompetitorsDrawerComponent],
  providers: [provideTranslocoScope('listings')],
  templateUrl: './fast-creator.component.html',
  styleUrls: ['./fast-creator.component.css']
})
export class FastCreatorComponent implements OnInit, OnDestroy {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  extensionBridge = inject(ExtensionBridgeService);
  listingAiService = inject(EtsyListingAiService);
  router = inject(Router);
  route = inject(ActivatedRoute);

  @ViewChild('fileInput') fileInputRef?: ElementRef<HTMLInputElement>;

  // --- TEMPLATES ---
  templates: CreatorTemplate[] = [];

  selectedTemplateId = '';

  // --- COLUMN 1: ÜRÜN & SEO BİLGİLERİ ---
  listingType: 'physical' | 'digital' = 'physical';
  priceUsd: number | null = null;
  priceTry = 0;
  estimatedShippingCostUsd: number | null = null;
  includeOffsiteAdsFee = false;
  quantity: number | null = 15;
  title = '';
  selectedCategory = '';
  selectedTaxonomyId = 0;
  categoryAiResult: CategoryAiSuggestion | null = null;
  categoryAlternatives: TaxonomyCandidateItem[] = [];
  selectedCategoryCombo = '';
  categoriesList: EtsyTaxonomyItemDto[] = [];
  filteredCategories: EtsyTaxonomyItemDto[] = [];
  categorySearchTerm = '';
  selectedDepartment = '';
  departmentsList: Array<{ name: string; count: number; icon: string }> = [];
  isLoadingCategories = false;
  shippingProfile = '';
  shippingProfilesList: Array<{
    id: number;
    title: string;
    minDays?: number;
    maxDays?: number;
    processingDaysDisplay?: string;
    minDeliveryDays?: number;
    maxDeliveryDays?: number;
    deliveryDaysDisplay?: string;
    hasFreeShipping?: boolean;
    originCountryIso?: string;
    mailClass?: string;
    destinationsSummary?: string;
  }> = [];
  selectedShippingProfileId: number | null = null;
  isLoadingShippingProfiles = false;
  readinessStatesList: Array<{ id: number; title: string; minDays?: number; maxDays?: number; state?: string; unit?: string }> = [];
  selectedReadinessStateId: number | null = null;
  isLoadingReadinessStates = false;
  readinessState = '';
  returnPoliciesList: EtsyReturnPolicyDto[] = [];
  selectedReturnPolicyId: number | null = null;
  isLoadingReturnPolicies = false;
  isReturnPolicyModalOpen = false;
  newPolicyAcceptsReturns = true;
  newPolicyAcceptsExchanges = true;
  newPolicyDeadline: number | null = 30;
  readonly returnPolicyDeadlineOptions: number[] = [7, 14, 21, 30, 45, 60, 90];
  isCreatingReturnPolicy = false;
  returnPolicyModalError = '';
  tags: string[] = [];
  newTagInput = '';
  description = '';
  materials = '';

  // --- AI GENERATION LOADING STATES ---
  isGeneratingTitle = false;
  isGeneratingCategory = false;
  isGeneratingTags = false;
  isGeneratingDesc = false;
  isGeneratingAll = false;

  // --- COLUMN 2: GÖRSELLER & AI MOTORU (0/10) ---
  galleryImages: GalleryImage[] = [];

  // AI Image Studio
  aiPrompt = '';
  lastGeneratedImage: string | null = null;
  isGeneratingAiImage = false;
  isStudioPickerOpen = false;

  // Studio gallery sample pool
  studioPool: string[] = [];

  // --- COLUMN 3: VARYASYONLAR & KONTROL ---
  enableVariations = false;
  showVariationImportBox = false;
  variationPasteInput = '';
  extensionListingUrlInput = '';
  isFetchingVariationsViaExtension = false;
  clonedSourceListingUrl = '';
  variationImportWarning = '';
  private readonly sanitizer = inject(DomSanitizer);
  readonly bookmarkletSafeUrl: SafeUrl = this.sanitizer.bypassSecurityTrustUrl(buildEtsyVariationBookmarkletUrl());
  varType1 = 'Boyut / Size';
  varValues1 = '';
  enableVar2 = false;
  varType2 = 'Renk / Color';
  varValues2 = '';
  customVariationPricing = false;
  variationRows: VariationRow[] = [];
  excludedVariationKeys: string[] = [];
  selectedProfitVariationKey = '__base__';
  isGeneratingVariations = false;
  variationAiAnalysis: {
    provider: string;
    model: string;
    usedAi: boolean;
    summary: string;
    warning?: string | null;
  } | null = null;

  // --- AI PRICING, COMPETITOR & SHIPPING AGENT ---
  agentTargetCountry = 'US';
  agentBaseWeightGrams = 250;
  agentLengthCm = 18;
  agentWidthCm = 14;
  agentHeightCm = 8;
  agentProductionCostUsd = 0;
  agentTargetMarginPct = 30;
  showAgentSettings = false;
  isRunningPricingAgent = false;
  pricingAgentResult: VariationPricingAgentResultDto | null = null;
  pricingAgentError = '';

  // --- RIGHT-SIDE SHIPPING & PRICING DRAWER ---
  isShippingDrawerOpen = false;
  isCompetitorPanelOpen = false;
  agentCustomSearchKeyword = '';
  isLoadingCarrierQuotes = false;
  carrierQuotesError = '';
  drawerCarrierQuotes: AgentCarrierQuoteSummaryDto[] = [];
  selectedCarrierKey = '';
  selectedCarrierName = '';

  // Publish Status & Checklist
  isLivePublish = false;
  isPreviewModalOpen = false;
  isGeneratingAi = false;
  isSavingDraft = false;
  successMessage = '';
  validationErrorSummary = '';
  validationFieldErrors: FieldValidationError[] = [];
  clonedNotice = '';
  hasRestoredDraft = false;
  draftSavedAt = '';
  private readonly DRAFT_STORAGE_KEY = 'etsy_fast_creator_draft_v1';
  private autoSaveTimer: any = null;

  // Orijinal satıcı notları yedeği
  originalNotesBackup = '';
  hasBackupNotes = false;

  ngOnInit(): void {
    const cloned = this.aiService.clonedListing();
    if (cloned) {
      this.selectedTemplateId = '';
      this.title = cloned.title;
      this.priceUsd = cloned.priceUsd;
      this.tags = [...cloned.tags];
      if (cloned.description) this.description = cloned.description;
      if (cloned.category) this.selectedCategory = cloned.category;
      if (cloned.taxonomyId && cloned.taxonomyId > 0) { this.selectedTaxonomyId = cloned.taxonomyId; }
      if (cloned.materials && cloned.materials.length > 0) {
        this.materials = cloned.materials.join(', ');
      }

      const allUrls = (cloned.imageUrls && cloned.imageUrls.length > 0)
        ? cloned.imageUrls.filter(Boolean).slice(0, 10)
        : (cloned.imageUrl ? [cloned.imageUrl] : []);

      if (allUrls.length > 0) {
        this.galleryImages = allUrls.map((url, idx) => ({
          id: `cloned-${idx + 1}`,
          url,
          isCover: idx === 0,
          name: idx === 0
            ? this.transloco.translate('listings.fastCreator.messages.clonedCoverName')
            : this.transloco.translate('listings.fastCreator.messages.clonedImageName', { index: idx + 1 })
        }));
      }

      if (cloned.sourceListingUrl) {
        this.clonedSourceListingUrl = cloned.sourceListingUrl;
        this.extensionListingUrlInput = cloned.sourceListingUrl;
      }

      if (cloned.hasVariations) {
        this.enableVariations = true;
        this.excludedVariationKeys = [];
        this.variationAiAnalysis = null;
        if (cloned.varType1) this.varType1 = cloned.varType1;
        if (cloned.varValues1) this.varValues1 = cloned.varValues1;
        if (cloned.enableVar2) {
          this.enableVar2 = true;
          if (cloned.varType2) this.varType2 = cloned.varType2;
          if (cloned.varValues2) this.varValues2 = cloned.varValues2;
        }
        if (cloned.variationRows && cloned.variationRows.length > 0) {
          this.variationRows = cloned.variationRows.map(r => ({ ...r }));
          this.customVariationPricing = true;

          if (!this.varValues1) {
            const g1 = new Set<string>();
            const g2 = new Set<string>();
            for (const row of this.variationRows) {
              const parts = row.key.split('/').map(p => p.trim()).filter(Boolean);
              if (parts[0]) g1.add(parts[0]);
              if (parts[1]) g2.add(parts[1]);
            }
            if (g1.size > 0) this.varValues1 = Array.from(g1).join(', ');
            if (g2.size > 0) {
              this.enableVar2 = true;
              this.varValues2 = Array.from(g2).join(', ');
            }
          }
        } else {
          this.rebuildVariationGrid();
        }
      } else if (cloned.variationWarning) {
        this.variationImportWarning = cloned.variationWarning;
        this.showVariationImportBox = true;
      }

      const varCount = (this.variationRows && this.variationRows.length > 0)
        ? this.transloco.translate('listings.fastCreator.messages.clonedVariationPart', { count: this.variationRows.length })
        : '';
      this.clonedNotice = this.transloco.translate('listings.fastCreator.messages.clonedNotice', {
        id: cloned.id,
        title: cloned.title.slice(0, 48),
        shop: cloned.shopName,
        images: allUrls.length,
        tags: this.tags.length,
        variations: varCount
      });
      this.updatePriceTry();
      this.saveDraftToStorage();
    }

    // QueryParams aktarımı (Dış Pazarlar, Viral 3D vb.)
    this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      if (params['title']) {
        this.selectedTemplateId = '';
        this.title = params['title'];
      }
      if (params['price']) {
        this.priceUsd = Number(params['price']) || this.priceUsd;
      }
      if (params['category']) {
        this.selectedCategory = params['category'];
      }
      if (params['description']) {
        this.description = params['description'];
      }
      if (params['tags']) {
        const rawTags = params['tags'];
        this.tags = typeof rawTags === 'string' ? rawTags.split(',').map((t: string) => t.trim()).filter(Boolean) : rawTags;
      }
      if (params['imageUrl']) {
        this.galleryImages = [{
          id: 'imported-1',
          url: params['imageUrl'],
          isCover: true,
          name: this.transloco.translate('listings.fastCreator.messages.importedImageName')
        }];
      }
      if (params['title'] || params['category']) {
        this.clonedNotice = this.transloco.translate('listings.fastCreator.messages.importedNotice', {
          title: this.title.slice(0, 48),
          category: this.selectedCategory
        });
        this.updatePriceTry();
      }
    });

    if (!cloned) {
      const hasRouteParams = !!(this.route.snapshot.queryParams['title'] || this.route.snapshot.queryParams['category']);
      if (!hasRouteParams) {
        this.loadDraftFromStorage();
      }
    }

    // Auto-save timer: arka planda her 4 saniyede bir formu otomatik yedekler
    this.autoSaveTimer = setInterval(() => {
      this.saveDraftToStorage();
    }, 4000);

    this.rebuildVariationGrid();
    this.loadLogisticsData();
  }

  loadLogisticsData(): void {
    this.loadShippingProfiles();
    this.loadReadinessStates();
    this.loadReturnPolicies();
    this.loadCategories();
  }

  refreshLogistics(): void {
    this.loadLogisticsData();
  }

  @HostListener('window:copilot_draft_updated')
  onCopilotDraftUpdated(): void {
    this.loadDraftFromStorage();
    this.clonedNotice = this.transloco.translate('listings.fastCreator.messages.copilotDraftRestored');
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.isCompetitorPanelOpen) {
      this.closeCompetitorPanel();
      return;
    }
    if (this.isShippingDrawerOpen) {
      this.closeShippingDrawer();
    }
  }

  @HostListener('window:beforeunload')
  onBeforeUnload(): void {
    this.saveDraftToStorage();
  }

  ngOnDestroy(): void {
    if (this.autoSaveTimer) {
      clearInterval(this.autoSaveTimer);
      this.autoSaveTimer = null;
    }
    this.saveDraftToStorage();
  }

  saveDraftToStorage(): void {
    if (!this.hasAnyDraftContent()) {
      return;
    }

    try {
      const now = new Date();
      const timeStr = now.toLocaleTimeString([], { hour: '2-digit', minute: '2-digit' });

      const draft: FastCreatorDraft = {
        listingType: this.listingType,
        priceUsd: this.priceUsd,
        priceTry: this.priceTry,
        quantity: this.quantity,
        title: this.title,
        selectedCategory: this.selectedCategory,
        selectedTaxonomyId: this.selectedTaxonomyId,
        selectedCategoryCombo: this.selectedCategoryCombo,
        selectedDepartment: this.selectedDepartment,
        shippingProfile: this.shippingProfile,
        selectedShippingProfileId: this.selectedShippingProfileId,
        readinessState: this.readinessState,
        selectedReadinessStateId: this.selectedReadinessStateId,
        selectedReturnPolicyId: this.selectedReturnPolicyId,
        tags: this.tags,
        description: this.description,
        materials: this.materials,
        galleryImages: this.galleryImages,
        enableVariations: this.enableVariations,
        varType1: this.varType1,
        varValues1: this.varValues1,
        enableVar2: this.enableVar2,
        varType2: this.varType2,
        varValues2: this.varValues2,
        customVariationPricing: this.customVariationPricing,
        variationRows: this.variationRows,
        excludedVariationKeys: this.excludedVariationKeys,
        selectedTemplateId: this.selectedTemplateId,
        isLivePublish: this.isLivePublish,
        savedAt: timeStr,
        originalNotesBackup: this.originalNotesBackup,
        estimatedShippingCostUsd: this.estimatedShippingCostUsd,
        includeOffsiteAdsFee: this.includeOffsiteAdsFee,
      };

      try {
        localStorage.setItem(this.DRAFT_STORAGE_KEY, JSON.stringify(draft));
        this.draftSavedAt = timeStr;
      } catch (storageError) {
        // QuotaExceeded: base64 buyuk resimleri bosaltarak metin ve diger verileri koru
        const safeImages = this.galleryImages.map(img => ({
          ...img,
          url: img.url.startsWith('data:') ? '' : img.url
        }));
        const slimDraft = { ...draft, galleryImages: safeImages };
        localStorage.setItem(this.DRAFT_STORAGE_KEY, JSON.stringify(slimDraft));
        this.draftSavedAt = timeStr;
      }
    } catch (e) {
      console.warn('FastCreator draft autosave failed:', e);
    }
  }

  loadDraftFromStorage(): boolean {
    try {
      const raw = localStorage.getItem(this.DRAFT_STORAGE_KEY);
      if (!raw) return false;

      const draft: FastCreatorDraft = JSON.parse(raw);
      if (!draft) return false;

      const hasContent = !!(
        draft.title?.trim() ||
        (draft.priceUsd !== null && draft.priceUsd !== undefined && draft.priceUsd > 0) ||
        (draft.tags && draft.tags.length > 0) ||
        draft.description?.trim() ||
        draft.materials?.trim() ||
        (draft.galleryImages && draft.galleryImages.length > 0) ||
        (draft.enableVariations && draft.variationRows && draft.variationRows.length > 0)
      );

      if (!hasContent) return false;

      this.listingType = draft.listingType || 'physical';
      this.priceUsd = draft.priceUsd ?? null;
      this.priceTry = draft.priceTry || 0;
      this.quantity = draft.quantity ?? 15;
      this.title = draft.title || '';
      this.selectedCategory = draft.selectedCategory || '';
      this.selectedTaxonomyId = draft.selectedTaxonomyId || 0;
      this.selectedCategoryCombo = draft.selectedCategoryCombo || '';
      this.selectedDepartment = draft.selectedDepartment || '';
      this.shippingProfile = draft.shippingProfile || '';
      this.selectedShippingProfileId = draft.selectedShippingProfileId ?? null;
      this.readinessState = draft.readinessState || '';
      this.selectedReadinessStateId = draft.selectedReadinessStateId ?? null;
      this.selectedReturnPolicyId = draft.selectedReturnPolicyId ?? null;
      this.tags = Array.isArray(draft.tags) ? [...draft.tags] : [];
      this.description = draft.description || '';
      this.materials = draft.materials || '';
      this.galleryImages = Array.isArray(draft.galleryImages) ? [...draft.galleryImages] : [];
      this.enableVariations = !!draft.enableVariations;
      this.varType1 = draft.varType1 || 'Boyut / Size';
      this.varValues1 = draft.varValues1 || '';
      this.enableVar2 = !!draft.enableVar2;
      this.varType2 = draft.varType2 || 'Renk / Color';
      this.varValues2 = draft.varValues2 || '';
      this.customVariationPricing = !!draft.customVariationPricing;
      this.variationRows = Array.isArray(draft.variationRows) ? [...draft.variationRows] : [];
      this.excludedVariationKeys = Array.isArray(draft.excludedVariationKeys) ? [...draft.excludedVariationKeys] : [];
      this.selectedTemplateId = draft.selectedTemplateId || '';
      this.isLivePublish = !!draft.isLivePublish;
      this.estimatedShippingCostUsd = draft.estimatedShippingCostUsd ?? null;
      this.includeOffsiteAdsFee = !!draft.includeOffsiteAdsFee;
      this.draftSavedAt = draft.savedAt || '';
      if (draft.originalNotesBackup) {
        this.originalNotesBackup = draft.originalNotesBackup;
        this.hasBackupNotes = true;
      }
      this.hasRestoredDraft = true;

      this.updatePriceTry();
      this.selectedProfitVariationKey = '__base__';
      if (this.enableVariations) {
        if (this.variationRows.length === 0) {
          this.rebuildVariationGrid();
        } else if (!this.customVariationPricing) {
          const activePrices = this.variationRows
            .filter(r => r.active !== false && Number(r.price) > 0)
            .map(r => Number(r.price));
          if (new Set(activePrices).size > 1) {
            this.customVariationPricing = true;
          }
        }
      }

      return true;
    } catch (e) {
      console.warn('FastCreator load draft error:', e);
      return false;
    }
  }

  clearDraftFromStorage(): void {
    try {
      localStorage.removeItem(this.DRAFT_STORAGE_KEY);
      this.hasRestoredDraft = false;
      this.draftSavedAt = '';
    } catch (e) {
      console.warn('FastCreator clear draft error:', e);
    }
  }

  private hasAnyDraftContent(): boolean {
    return !!(
      this.title?.trim() ||
      (this.priceUsd !== null && this.priceUsd !== undefined && this.priceUsd > 0) ||
      this.tags.length > 0 ||
      this.description?.trim() ||
      this.materials?.trim() ||
      this.galleryImages.length > 0 ||
      (this.enableVariations && this.variationRows.length > 0)
    );
  }

  private readonly TR_TO_EN_TAXONOMY_MAP: Record<string, string[]> = {
    'lamba': ['lamp', 'lighting', 'lights'],
    'ışık': ['light', 'lighting'],
    'abajur': ['lamp', 'lighting'],
    'gece lambası': ['night lights', 'lighting'],
    'ejderha': ['dragon', 'monsters & fantasy', 'fantasy', 'figurines', 'sculpture'],
    'figür': ['figurines', 'sculpture', 'collectibles'],
    'biblo': ['figurines', 'knick knacks', 'sculpture'],
    'heykel': ['sculpture', 'statues', 'figurines', 'art objects'],
    'yüzük': ['rings', 'solitaire rings', 'band rings'],
    'kolye': ['necklaces', 'pendants', 'pendant necklaces'],
    'küpe': ['earrings', 'drop & dangle earrings', 'stud earrings'],
    'bileklik': ['bracelets', 'charm bracelets'],
    'takı': ['jewelry'],
    'çanta': ['bags & purses', 'handbags', 'shoulder bags', 'tote bags', 'crossbody bags', 'backpacks'],
    'omuz çantası': ['shoulder bags'],
    'sırt çantası': ['backpacks'],
    'el çantası': ['top handle bags', 'clutches & evening bags'],
    'bez çanta': ['tote bags'],
    'cüzdan': ['wallets', 'money clips', 'wallets & money clips'],
    'tişört': ['t-shirts', 'tops & tees'],
    'gömlek': ['dress shirts', 'button downs', 'oxfords'],
    'elbise': ['dresses'],
    'sweatshirt': ['sweatshirts', 'hoodies & sweatshirts'],
    'kapüşonlu': ['hoodies', 'hoodies & sweatshirts'],
    'şapka': ['hats & caps', 'hats & head coverings'],
    'bere': ['beanies', 'hats & caps'],
    'ayakkabı': ['shoes', 'boots', 'sneakers'],
    'terlik': ['slippers'],
    'kupa': ['mugs', 'drinkware'],
    'bardak': ['drinkware', 'tumblers', 'glassware'],
    'mum': ['candles', 'candleholders'],
    'mumluk': ['candleholders'],
    'vazo': ['vases', 'vessels'],
    'saksı': ['planters & pots', 'planters'],
    'yastık': ['pillows', 'cushions'],
    'kırlent': ['pillow covers', 'pillows'],
    'tablo': ['paintings', 'prints', 'canvas'],
    'resim': ['paintings', 'prints', 'drawing & illustration'],
    'duvar': ['wall decor', 'wall hangings'],
    'poster': ['posters', 'music & movie posters', 'prints'],
    'anahtarlık': ['keychains', 'keychains & lanyards'],
    'oyuncak': ['toys', 'games', 'puzzles'],
    'bebek': ['dolls', 'baby'],
    'çocuk': ['kids', 'child'],
    'evcil': ['pet supplies', 'pet'],
    'köpek': ['dog', 'pet supplies'],
    'kedi': ['cat', 'pet supplies'],
    'düğün': ['weddings', 'bridal'],
    'kırtasiye': ['paper & party supplies', 'stationery'],
    'defter': ['journals & notebooks', 'blank books'],
    'kart': ['greeting cards', 'invitations'],
    'dijital': ['digital', 'digital prints', 'craft machine files'],
    '3d': ['3d printer files', 'figurines', 'sculpture', 'monsters & fantasy'],
    'stl': ['3d printer files', 'digital']
  };

  private readonly DEPT_ICONS: Record<string, string> = {
    'Home & Living': '🏠',
    'Craft Supplies & Tools': '🎨',
    'Clothing': '👕',
    'Toys & Games': '🧸',
    'Bath & Beauty': '🧼',
    'Shoes': '👟',
    'Paper & Party Supplies': '🎉',
    'Weddings': '💍',
    'Electronics & Accessories': '🔌',
    'Books, Movies & Music': '📚',
    'Accessories': '🧢',
    'Art & Collectibles': '🖼️',
    'Jewelry': '💎',
    'Pet Supplies': '🐾',
    'Bags & Purses': '👜'
  };

  getDepartmentIcon(deptName: string): string {
    return this.DEPT_ICONS[deptName] || '📁';
  }

  extractDepartments(): void {
    const counts = new Map<string, number>();
    for (const cat of this.categoriesList) {
      const dept = cat.path.split(' > ')[0]?.trim();
      if (dept) {
        counts.set(dept, (counts.get(dept) || 0) + 1);
      }
    }
    this.departmentsList = Array.from(counts.entries())
      .map(([name, count]) => ({
        name,
        count,
        icon: this.getDepartmentIcon(name)
      }))
      .sort((a, b) => b.count - a.count);
  }

  clearCategorySearch(): void {
    this.categorySearchTerm = '';
    this.filterCategories();
  }

  expandSearchTerms(rawQ: string): string[] {
    if (!rawQ) return [];
    const lower = rawQ.toLowerCase().trim();
    const terms = new Set<string>();
    terms.add(lower);

    if (this.TR_TO_EN_TAXONOMY_MAP[lower]) {
      this.TR_TO_EN_TAXONOMY_MAP[lower].forEach(t => terms.add(t.toLowerCase()));
    }

    const words = lower.split(/\s+/);
    for (const word of words) {
      if (word.length >= 2) {
        terms.add(word);
        if (this.TR_TO_EN_TAXONOMY_MAP[word]) {
          this.TR_TO_EN_TAXONOMY_MAP[word].forEach(t => terms.add(t.toLowerCase()));
        }
      }
    }

    return Array.from(terms);
  }

  loadCategories(): void {
    this.isLoadingCategories = true;
    this.etsyApi.getSellerTaxonomy().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isLoadingCategories = false;
        this.categoriesList = res?.results || [];
        this.extractDepartments();

        if (this.selectedTaxonomyId > 0) {
          const match = this.categoriesList.find(c => c.id === this.selectedTaxonomyId);
          if (match) {
            this.selectedCategory = match.path;
            this.selectedCategoryCombo = `${match.id}|${match.path}`;
            const dept = match.path.split(' > ')[0]?.trim();
            if (dept) this.selectedDepartment = dept;
          }
        } else if (this.selectedCategory) {
          const norm = (s: string) => (s || '').toLowerCase().replace(/\s*>\s*/g, ' > ').trim();
          const selNorm = norm(this.selectedCategory);
          const leaf = taxonomyLeafName(this.selectedCategory);
          const match = this.categoriesList.find(c => norm(c.path) === selNorm || norm(c.name) === selNorm || (leaf && taxonomyLeafName(c.path) === leaf));
          if (match) {
            this.selectedTaxonomyId = match.id;
            this.selectedCategory = match.path;
            this.selectedCategoryCombo = `${match.id}|${match.path}`;
            const dept = match.path.split(' > ')[0]?.trim();
            if (dept) this.selectedDepartment = dept;
          }
        }
        this.filterCategories();
      },
      error: () => {
        this.isLoadingCategories = false;
      }
    });
  }

  filterCategories(): void {
    const rawQ = (this.categorySearchTerm || '').trim().toLowerCase();
    const queryTerms = this.expandSearchTerms(rawQ);
    const dept = this.selectedDepartment;

    let filtered = this.categoriesList;

    // 1. Bölüm filtresi
    if (dept) {
      filtered = filtered.filter(c => c.path.startsWith(dept + ' > ') || c.path === dept);
    }

    // 2. Arama terimleri filtresi (TR & EN)
    if (queryTerms.length > 0) {
      filtered = filtered.filter(c => {
        const p = c.path.toLowerCase();
        const n = c.name.toLowerCase();
        return queryTerms.some(term => p.includes(term) || n.includes(term));
      });
    }

    // 3. Gösterim listesi (bölüm seçiliyse veya arama yapılıyorsa ilk 300, yoksa ilk 250)
    let baseList = filtered.slice(0, 300);

    // Aktif seçili olan kategori listede yoksa en başa ekle (kullanıcı seçimini kaybetmesin)
    if (this.selectedTaxonomyId > 0) {
      const selectedItem: EtsyTaxonomyItemDto | undefined =
        this.categoriesList.find(c => c.id === this.selectedTaxonomyId)
        ?? (this.selectedCategory
          ? { id: this.selectedTaxonomyId, path: this.selectedCategory, name: this.selectedCategory.split('>').pop()?.trim() || this.selectedCategory }
          : undefined);
      if (selectedItem && !baseList.some(c => c.id === this.selectedTaxonomyId)) {
        baseList.unshift(selectedItem);
      }
      // Combo değeri, listede render edilen option değeriyle birebir aynı olmalı (aksi halde select boş görünür)
      if (selectedItem) {
        this.selectedCategoryCombo = `${selectedItem.id}|${selectedItem.path}`;
      }
    }

    this.filteredCategories = baseList;
  }

  trackByTaxonomyId(_: number, cat: EtsyTaxonomyItemDto): number {
    return cat.id;
  }

  selectCategoryItem(cat: EtsyTaxonomyItemDto): void {
    this.selectedTaxonomyId = cat.id;
    this.selectedCategory = cat.path;
    this.selectedCategoryCombo = `${cat.id}|${cat.path}`;
    this.showToast(this.transloco.translate('listings.fastCreator.messages.categorySelected', { id: cat.id, path: cat.path }));
  }

  loadShippingProfiles(): void {
    this.isLoadingShippingProfiles = true;
    this.etsyApi.getShippingProfiles().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isLoadingShippingProfiles = false;
        const results = res?.results || (Array.isArray(res) ? res : []);
        if (results.length > 0) {
          this.shippingProfilesList = results
            .map((p: any) => {
              const id = Number(p.shippingProfileId ?? p.shipping_profile_id ?? p.id ?? 0);
              return {
                id,
                title: p.title || this.transloco.translate('listings.fastCreator.messages.profileFallback', { id }),
                minDays: p.minProcessingDays ?? p.min_processing_days ?? undefined,
                maxDays: p.maxProcessingDays ?? p.max_processing_days ?? undefined,
                processingDaysDisplay: p.processingDaysDisplay ?? p.processing_days_display ?? p.processing_days_display_label ?? undefined,
                minDeliveryDays: p.minDeliveryDays ?? p.min_delivery_days ?? undefined,
                maxDeliveryDays: p.maxDeliveryDays ?? p.max_delivery_days ?? undefined,
                deliveryDaysDisplay: p.deliveryDaysDisplay ?? p.delivery_days_display ?? undefined,
                hasFreeShipping: !!(p.hasFreeShipping ?? p.has_free_shipping),
                originCountryIso: p.originCountryIso ?? p.origin_country_iso ?? undefined,
                mailClass: p.mailClass ?? p.mail_class ?? undefined,
                destinationsSummary: p.destinationsSummary ?? p.destinations_summary ?? undefined
              };
            })
            .filter((p: { id: number }) => p.id > 0);
          if (this.shippingProfilesList.length > 0 &&
              (!this.selectedShippingProfileId || !this.shippingProfilesList.some(p => p.id === Number(this.selectedShippingProfileId)))) {
            this.selectedShippingProfileId = this.shippingProfilesList[0].id;
          }
        }
      },
      error: () => {
        this.isLoadingShippingProfiles = false;
      }
    });
  }

  loadReadinessStates(): void {
    this.isLoadingReadinessStates = true;
    this.etsyApi.getReadinessStates().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isLoadingReadinessStates = false;
        const results = res?.results || (Array.isArray(res) ? res : []);
        if (results.length > 0) {
          this.readinessStatesList = results
            .map((r: any) => {
              const id = Number(r.readinessStateId ?? r.readiness_state_id ?? r.id ?? 0);
              return {
                id,
                title: r.title || this.transloco.translate('listings.fastCreator.messages.readinessFallback', { id }),
                minDays: r.minProcessingTime ?? r.min_processing_time ?? undefined,
                maxDays: r.maxProcessingTime ?? r.max_processing_time ?? undefined,
                state: r.readinessState ?? r.readiness_state ?? undefined,
                unit: r.processingTimeUnit ?? r.processing_time_unit ?? undefined
              };
            })
            .filter((r: { id: number }) => r.id > 0);
          if (this.readinessStatesList.length > 0 &&
              (!this.selectedReadinessStateId || !this.readinessStatesList.some(r => r.id === Number(this.selectedReadinessStateId)))) {
            this.selectedReadinessStateId = this.readinessStatesList[0].id;
          }
        }
      },
      error: () => {
        this.isLoadingReadinessStates = false;
      }
    });
  }

  loadReturnPolicies(): void {
    this.isLoadingReturnPolicies = true;
    this.etsyApi.getReturnPolicies().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isLoadingReturnPolicies = false;
        const results = res?.results || (Array.isArray(res) ? res : []);
        this.returnPoliciesList = results
          .map((p: any) => ({
            returnPolicyId: Number(p.returnPolicyId ?? p.return_policy_id ?? 0),
            acceptsReturns: !!(p.acceptsReturns ?? p.accepts_returns),
            acceptsExchanges: !!(p.acceptsExchanges ?? p.accepts_exchanges),
            returnDeadline: p.returnDeadline ?? p.return_deadline ?? null,
            title: p.title || this.transloco.translate('listings.fastCreator.messages.returnPolicyFallback', { id: p.returnPolicyId ?? p.return_policy_id })
          }))
          .filter(p => p.returnPolicyId > 0);

        if (this.returnPoliciesList.length > 0 &&
            (!this.selectedReturnPolicyId || !this.returnPoliciesList.some(p => p.returnPolicyId === this.selectedReturnPolicyId))) {
          this.selectedReturnPolicyId = this.returnPoliciesList[0].returnPolicyId;
        }
      },
      error: () => {
        this.isLoadingReturnPolicies = false;
      }
    });
  }

  openReturnPolicyModal(): void {
    this.newPolicyAcceptsReturns = true;
    this.newPolicyAcceptsExchanges = true;
    this.newPolicyDeadline = 30;
    this.returnPolicyModalError = '';
    this.isCreatingReturnPolicy = false;
    this.isReturnPolicyModalOpen = true;
  }

  closeReturnPolicyModal(): void {
    if (this.isCreatingReturnPolicy) return;
    this.isReturnPolicyModalOpen = false;
  }

  get newPolicyPreviewTitle(): string {
    const days = (this.newPolicyAcceptsReturns || this.newPolicyAcceptsExchanges) && this.newPolicyDeadline
      ? this.transloco.translate('listings.fastCreator.messages.rpPreviewDays', { days: this.newPolicyDeadline })
      : '';
    if (this.newPolicyAcceptsReturns && this.newPolicyAcceptsExchanges) {
      return this.transloco.translate('listings.fastCreator.messages.rpPreviewBoth', { days });
    }
    if (this.newPolicyAcceptsReturns) {
      return this.transloco.translate('listings.fastCreator.messages.rpPreviewReturns', { days });
    }
    if (this.newPolicyAcceptsExchanges) {
      return this.transloco.translate('listings.fastCreator.messages.rpPreviewExchanges', { days });
    }
    return this.transloco.translate('listings.fastCreator.messages.rpPreviewNone');
  }

  submitCreateReturnPolicy(): void {
    const requiresDeadline = this.newPolicyAcceptsReturns || this.newPolicyAcceptsExchanges;
    if (requiresDeadline && (!this.newPolicyDeadline || this.newPolicyDeadline <= 0)) {
      this.returnPolicyModalError = this.transloco.translate('listings.fastCreator.messages.rpSelectDeadlineError');
      return;
    }

    this.isCreatingReturnPolicy = true;
    this.returnPolicyModalError = '';

    this.etsyApi.createReturnPolicy({
      acceptsReturns: this.newPolicyAcceptsReturns,
      acceptsExchanges: this.newPolicyAcceptsExchanges,
      returnDeadline: requiresDeadline ? Number(this.newPolicyDeadline) : null
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (created) => {
        this.isCreatingReturnPolicy = false;
        const createdId = Number((created as any)?.returnPolicyId ?? (created as any)?.return_policy_id ?? 0);
        if (created && createdId > 0) {
          const normalized: EtsyReturnPolicyDto = {
            returnPolicyId: createdId,
            acceptsReturns: !!(created as any).acceptsReturns,
            acceptsExchanges: !!(created as any).acceptsExchanges,
            returnDeadline: (created as any).returnDeadline ?? null,
            title: created.title || this.newPolicyPreviewTitle
          };
          const existingIdx = this.returnPoliciesList.findIndex(p => p.returnPolicyId === createdId);
          if (existingIdx >= 0) {
            this.returnPoliciesList[existingIdx] = normalized;
          } else {
            this.returnPoliciesList = [normalized, ...this.returnPoliciesList];
          }
          this.selectedReturnPolicyId = createdId;
          this.saveDraftToStorage();
          this.isReturnPolicyModalOpen = false;
          this.showToast(this.transloco.translate('listings.fastCreator.messages.rpCreatedToast', { title: normalized.title, id: createdId }));
        } else {
          this.isReturnPolicyModalOpen = false;
          this.loadReturnPolicies();
        }
      },
      error: (err) => {
        this.isCreatingReturnPolicy = false;
        const parsed = extractClientValidationErrors(err, this.transloco.translate('listings.fastCreator.messages.rpCreateError'));
        this.returnPolicyModalError = parsed.summary;
      }
    });
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  get variationProfitSummary(): VariationNetProfitSummary {
    return calculateVariationNetProfits(
      this.variationRows || [],
      Number(this.priceUsd) || 0,
      Number(this.estimatedShippingCostUsd) || 0,
      this.usdTryRate,
      this.includeOffsiteAdsFee
    );
  }

  get variationProfitRows(): VariationNetProfitRow[] {
    return this.variationProfitSummary.rows;
  }

  get netProfitBreakdown(): EtsyNetProfitBreakdown {
    if (this.hasActiveVariations && this.selectedProfitVariationKey !== '__base__') {
      const selected = this.variationProfitRows.find(r => r.key === this.selectedProfitVariationKey);
      if (selected) {
        return selected.breakdown;
      }
    }
    return calculateEtsyNetProfit({
      salePriceUsd: this.effectivePrice,
      shippingCostUsd: this.estimatedShippingCostUsd ?? 0,
      usdTryRate: this.usdTryRate,
      includeOffsiteAds: this.includeOffsiteAdsFee
    });
  }

  getRowNetProfit(row: VariationRow): EtsyNetProfitBreakdown {
    const rowPrice = Number(row.price) > 0 ? Number(row.price) : (Number(this.priceUsd) || 0);
    return calculateEtsyNetProfit({
      salePriceUsd: rowPrice,
      shippingCostUsd: this.estimatedShippingCostUsd ?? 0,
      usdTryRate: this.usdTryRate,
      includeOffsiteAds: this.includeOffsiteAdsFee
    });
  }

  selectProfitVariation(key: string): void {
    this.selectedProfitVariationKey = key || '__base__';
  }

  // --- TEMPLATE ACTIONS ---
  applyTemplateById(id: string): void {
    const tmpl = this.templates.find(t => t.id === id);
    if (!tmpl) return;
    this.selectedTemplateId = tmpl.id;
    this.title = tmpl.sampleTitle;
    this.priceUsd = tmpl.defaultPrice;
    this.updatePriceTry();
    this.tags = [...tmpl.sampleTags];
    this.description = tmpl.sampleDesc;
    this.selectedCategory = tmpl.category;
    this.materials = tmpl.materials;
    this.rebuildVariationGrid();
  }

  saveCurrentAsTemplate(): void {
    const name = prompt(
      this.transloco.translate('listings.fastCreator.messages.promptTemplateName'),
      this.transloco.translate('listings.fastCreator.messages.defaultTemplateName', { num: this.templates.length + 1 })
    );
    if (!name) return;
    const newId = 'custom_' + Date.now();
    this.templates.push({
      id: newId,
      name: '📁 ' + name,
      category: this.selectedCategory,
      defaultPrice: this.priceUsd || 0,
      sampleTitle: this.title,
      sampleTags: [...this.tags],
      sampleDesc: this.description,
      materials: this.materials
    });
    this.selectedTemplateId = newId;
    this.showToast(this.transloco.translate('listings.fastCreator.messages.templateSaved'));
  }

  deleteCurrentTemplate(): void {
    if (this.templates.length === 0) {
      alert(this.transloco.translate('listings.fastCreator.messages.noTemplateToDelete'));
      return;
    }
    if (confirm(this.transloco.translate('listings.fastCreator.messages.confirmDeleteTemplate'))) {
      this.templates = this.templates.filter(t => t.id !== this.selectedTemplateId);
      this.selectedTemplateId = '';
      this.resetForm();
      this.showToast(this.transloco.translate('listings.fastCreator.messages.templateDeleted'));
    }
  }

  // --- PRICING & LOGISTICS ---
  onPriceChange(): void {
    this.updatePriceTry();
    this.propagateBasePriceToVariations();
  }

  onPriceTryChange(): void {
    if (this.usdTryRate > 0) {
      this.priceUsd = Number((this.priceTry / this.usdTryRate).toFixed(2));
      this.propagateBasePriceToVariations();
    }
  }

  private propagateBasePriceToVariations(): void {
    if (this.enableVariations && this.variationRows.length > 0) {
      const base = Number(this.priceUsd) || 0;
      if (!this.customVariationPricing || this.variationRows.every(r => (Number(r.price) || 0) <= 0)) {
        this.variationRows.forEach(r => {
          r.price = base;
        });
      }
    }
    this.rebuildVariationGrid();
  }

  updatePriceTry(): void {
    this.priceTry = Number(((this.priceUsd || 0) * this.usdTryRate).toFixed(2));
  }

  clearCloned(): void {
    this.aiService.clonedListing.set(null);
    this.clonedNotice = '';
    this.resetForm();
  }

  resetForm(): void {
    this.clearValidationErrors();
    this.clearDraftFromStorage();
    this.hasRestoredDraft = false;
    this.draftSavedAt = '';
    this.originalNotesBackup = '';
    this.hasBackupNotes = false;

    // 1. Ürün & SEO Bilgileri
    this.title = '';
    this.priceUsd = null;
    this.priceTry = 0;
    this.quantity = null;
    this.tags = [];
    this.newTagInput = '';
    this.description = '';
    this.materials = '';

    // 2. Görseller & AI Motoru
    this.galleryImages = [];
    this.lastGeneratedImage = null;
    this.aiPrompt = '';
    this.isGeneratingAiImage = false;
    if (this.fileInputRef?.nativeElement) {
      this.fileInputRef.nativeElement.value = '';
    }

    // 3. Varyasyonlar & Kontrol
    this.enableVariations = false;
    this.varValues1 = '';
    this.enableVar2 = false;
    this.varValues2 = '';
    this.customVariationPricing = false;
    this.variationRows = [];
    this.excludedVariationKeys = [];
    this.variationAiAnalysis = null;
    this.selectedProfitVariationKey = '__base__';
    this.pricingAgentResult = null;
    this.pricingAgentError = '';
    this.isCompetitorPanelOpen = false;
    this.agentCustomSearchKeyword = '';
    this.drawerCarrierQuotes = [];
    this.carrierQuotesError = '';
    this.selectedCarrierKey = '';
    this.selectedCarrierName = '';

    // 4. Şablon & Klonlanan
    this.selectedTemplateId = '';
    this.clonedNotice = '';

    this.showToast(this.transloco.translate('listings.fastCreator.messages.formReset'));
  }

  // --- TAGS ---
  addTag(): void {
    const clean = this.newTagInput.trim().toLowerCase();
    if (!clean || clean.length > 20 || this.tags.length >= 13) return;
    if (!this.tags.includes(clean)) {
      this.tags.push(clean);
    }
    this.newTagInput = '';
  }

  removeTag(index: number): void {
    this.tags.splice(index, 1);
  }

  trimTags(): void {
    this.tags = this.tags.map(t => t.slice(0, 20).trim().toLowerCase()).filter(t => t.length > 0);
    this.showToast(this.transloco.translate('listings.fastCreator.messages.tagsTrimmed'));
  }

  // --- AI SUGGESTIONS ---
  suggestAiTitle(): void {
    const input = this.title.trim() || this.description.trim();
    if (!input) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterBasicInfoForAi'));
      return;
    }
    this.isGeneratingTitle = true;
    this.listingAiService.suggestTitle(input, this.selectedCategory).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isGeneratingTitle = false;
        this.title = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingTitle = false;
        this.showToast(err?.message || this.transloco.translate('listings.fastCreator.messages.aiTitleError'));
      }
    });
  }

  /**
   * AI yanıtını canlı Etsy taksonomisine karşı KATI doğrular.
   * - ID gerçek listede var ve yaprak adı AI'nın path'iyle tutarlıysa → kabul
   * - Aksi halde path / yaprak adı eşleşmesi (önce AI'ya gönderilen adaylar içinde)
   * - AI'ya gönderilen aday listesindeki ID → kabul
   * - Hiçbiri tutmazsa: kısa listenin 1. adayı "düşük güven" uyarısıyla (başlık kelimeleriyle körlemesine arama YOK)
   */
  groundTaxonomyRecommendation(res: CategoryAiSuggestion, candidates: EtsyTaxonomyItemDto[] = []): CategoryAiSuggestion {
    const list = this.categoriesList;
    if (!list || list.length === 0) return res;

    const resolve = (id: number, path: string): EtsyTaxonomyItemDto | undefined => {
      const norm = (s: string) => (s || '').toLowerCase().replace(/\s*>\s*/g, ' > ').trim();
      const cleanPath = norm(path);
      const leaf = taxonomyLeafName(path);
      const byId = id > 0 ? list.find(c => c.id === id) : undefined;

      // 1. ID canlı aday listesindeyse (AI'ya verdiğimiz resmi liste) ve ID veritabanında varsa → en güvenilir eşleşme
      if (byId && candidates.some(c => c.id === byId.id)) {
        return byId;
      }
      // 2. ID + path / yaprak adı tutarlı
      if (byId && (!cleanPath || norm(byId.path) === cleanPath || taxonomyLeafName(byId.path) === leaf)) {
        return byId;
      }
      // 3. Tam path eşleşmesi (normalize edilmiş)
      if (cleanPath) {
        const exact = list.find(c => norm(c.path) === cleanPath);
        if (exact) return exact;
        // 4. Yaprak adı eşleşmesi — önce AI'ya gönderilen adaylar içinde, sonra tüm listede
        if (leaf) {
          const leafMatch = candidates.find(c => taxonomyLeafName(c.path) === leaf)
            ?? list.find(c => norm(c.name) === leaf || taxonomyLeafName(c.path) === leaf);
          if (leafMatch) return leafMatch;
        }
      }
      // 5. Herhangi bir geçerli ID eşleşmesi
      if (byId) return byId;

      return undefined;
    };

    const match = resolve(res.taxonomyId, res.categoryPath);
    if (match) {
      res.taxonomyId = match.id;
      res.categoryPath = match.path;
    } else if (candidates.length > 0) {
      const fallback = candidates[0];
      const aiSaid = res.categoryPath || (res.taxonomyId ? `#${res.taxonomyId}` : this.transloco.translate('listings.fastCreator.messages.invalidAiResponse'));
      res.taxonomyId = fallback.id;
      res.categoryPath = fallback.path;
      res.confidenceScore = Math.min(res.confidenceScore || 40, 40);
      res.reasoning = this.transloco.translate('listings.fastCreator.messages.lowConfidenceReasoning', {
        aiSaid,
        original: res.reasoning || ''
      }).trim();
      res.message = this.transloco.translate('listings.fastCreator.messages.lowConfidenceMessage', {
        id: fallback.id,
        path: fallback.path
      });
    } else {
      // Doğrulanamadı ve aday yok → seçim yapılmasın
      res.taxonomyId = 0;
    }

    // Alternatifleri de aynı katı kuralla doğrula; doğrulanamayanları ve tekrarları at
    const seen = new Set<number>([res.taxonomyId]);
    res.alternatives = (res.alternatives || [])
      .map(alt => {
        const m = resolve(alt.taxonomyId, alt.categoryPath);
        return m ? { taxonomyId: m.id, categoryPath: m.path, confidenceScore: alt.confidenceScore } : null;
      })
      .filter((alt): alt is TaxonomyCandidateItem => !!alt && !seen.has(alt.taxonomyId) && !!seen.add(alt.taxonomyId));

    // Alternatif yoksa kısa listeden tamamla
    if (res.alternatives.length === 0 && candidates.length > 0) {
      res.alternatives = candidates
        .filter(c => !seen.has(c.id))
        .slice(0, 3)
        .map((c, i) => ({ taxonomyId: c.id, categoryPath: c.path, confidenceScore: 60 - i * 5 }));
    }

    return res;
  }

  /** Canlı Etsy taksonomisi henüz yüklenmediyse önce yükler (AI'ya gerçek aday listesi verebilmek için). */
  private ensureTaxonomyLoaded(): Observable<EtsyTaxonomyItemDto[]> {
    if (this.categoriesList.length > 0) return of(this.categoriesList);
    return this.etsyApi.getSellerTaxonomy().pipe(
      map(res => res?.results || []),
      tap(results => {
        if (results.length > 0 && this.categoriesList.length === 0) {
          this.categoriesList = results;
          this.extractDepartments();
          this.filterCategories();
        }
      }),
      catchError(() => of([] as EtsyTaxonomyItemDto[]))
    );
  }

  suggestAiCategory(): void {
    const title = this.title.trim();
    const desc = this.description.trim();
    const images = this.galleryImages.map(img => img.url).filter(u => !!u);

    if (!title && !desc && images.length === 0) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterInputForCategory'));
      return;
    }

    this.isGeneratingCategory = true;
    let candidates: EtsyTaxonomyItemDto[] = [];

    this.ensureTaxonomyLoaded().pipe(
      switchMap(taxonomy => {
        // Canlı Etsy listesinden ürüne en alakalı gerçek kategoriler → AI yalnız bunlardan seçer
        candidates = buildTaxonomyShortlist(taxonomy, title, desc, this.TR_TO_EN_TAXONOMY_MAP, 80);
        return this.listingAiService.suggestCategoryMultimodal(title, desc, images, candidates);
      })
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isGeneratingCategory = false;
        const grounded = this.groundTaxonomyRecommendation(res, candidates);

        if (!grounded.taxonomyId) {
          this.categoryAiResult = grounded;
          this.categoryAlternatives = grounded.alternatives || [];
          this.showToast(this.transloco.translate('listings.fastCreator.messages.invalidCategoryReturned'));
          return;
        }

        this.selectedTaxonomyId = grounded.taxonomyId;
        this.selectedCategory = grounded.categoryPath;
        this.categoryAiResult = grounded;
        this.categoryAlternatives = grounded.alternatives || [];
        this.selectedCategoryCombo = `${grounded.taxonomyId}|${grounded.categoryPath}`;

        // İlgili bölümü otomatik seç
        const dept = grounded.categoryPath.split(' > ')[0]?.trim();
        if (dept && this.departmentsList.some(d => d.name === dept)) {
          this.selectedDepartment = dept;
        }

        this.filterCategories();
        this.showToast(grounded.message || this.transloco.translate('listings.fastCreator.messages.aiCategoryDetected', { id: grounded.taxonomyId, path: grounded.categoryPath }));
      },
      error: (err) => {
        this.isGeneratingCategory = false;
        this.showToast(err?.message || this.transloco.translate('listings.fastCreator.messages.aiCategoryError'));
      }
    });
  }

  onTaxonomyComboChange(val: string): void {
    if (!val) return;
    if (val.includes('|')) {
      const parts = val.split('|');
      const id = parseInt(parts[0], 10);
      const path = parts.slice(1).join('|');
      if (id > 0) this.selectedTaxonomyId = id;
      if (path) {
        this.selectedCategory = path;
        const dept = path.split(' > ')[0]?.trim();
        if (dept && !this.selectedDepartment) {
          this.selectedDepartment = dept;
        }
      }
    } else {
      this.selectedCategory = val;
    }
  }

  selectAlternativeCategory(alt: TaxonomyCandidateItem): void {
    this.selectedTaxonomyId = alt.taxonomyId;
    this.selectedCategory = alt.categoryPath;
    this.selectedCategoryCombo = `${alt.taxonomyId}|${alt.categoryPath}`;

    const dept = alt.categoryPath.split(' > ')[0]?.trim();
    if (dept && this.departmentsList.some(d => d.name === dept)) {
      this.selectedDepartment = dept;
    }

    this.filterCategories();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.categoryUpdated', { id: alt.taxonomyId, path: alt.categoryPath }));
  }

  suggestAiTags(): void {
    const input = this.title.trim() || this.description.trim();
    if (!input) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterBasicInfoForAi'));
      return;
    }
    this.isGeneratingTags = true;
    this.listingAiService.suggestTags(input, this.selectedCategory).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isGeneratingTags = false;
        this.tags = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingTags = false;
        this.showToast(err?.message || this.transloco.translate('listings.fastCreator.messages.aiTagsError'));
      }
    });
  }

  // --- AI AÇIKLAMA MOTORU (ÖN BİLGİ & BOYUT/RENK KORUMA) ---

  restoreOriginalNotes(): void {
    if (!this.originalNotesBackup) return;
    this.description = this.originalNotesBackup;
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.originalNotesRestored'));
  }

  applyDescriptionTemplate(): void {
    const currentDesc = this.description.trim();
    if (currentDesc && !this.originalNotesBackup) {
      this.originalNotesBackup = currentDesc;
      this.hasBackupNotes = true;
    }

    this.description = buildEtsyDescriptionTemplate({
      title: this.title,
      tags: this.tags,
      materials: this.materials,
      listingType: this.listingType
    });

    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.descTemplateApplied'));
  }

  suggestAiDescription(): void {
    const descText = this.description.trim();
    const titleText = this.title.trim();

    if (!descText && !titleText) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterInfoForDesc'));
      return;
    }

    // Kullanıcının yazdığı orijinal notları yedekle
    if (descText && !this.originalNotesBackup) {
      this.originalNotesBackup = descText;
      this.hasBackupNotes = true;
    }

    const variationsSummary = this.getVariationsSummary();
    const shippingInfo = this.buildShippingAiContext();

    const context: AiDescriptionContext = {
      productTitle: titleText || descText,
      preliminaryNotes: descText,
      materials: this.materials.trim(),
      category: this.selectedCategory,
      tags: this.tags,
      variationsSummary,
      shippingInfo
    };

    this.isGeneratingDesc = true;
    this.listingAiService.suggestDescription(context).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isGeneratingDesc = false;
        this.description = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingDesc = false;
        this.showToast(err?.message || this.transloco.translate('listings.fastCreator.messages.aiDescError'));
      }
    });
  }

  private buildShippingAiContext(): AiShippingContext {
    if (this.listingType === 'digital') {
      return { isDigital: true };
    }

    const profile =
      this.shippingProfilesList.find(p => p.id === Number(this.selectedShippingProfileId))
      ?? this.shippingProfilesList[0];
    const readiness =
      this.readinessStatesList.find(r => r.id === Number(this.selectedReadinessStateId))
      ?? this.readinessStatesList[0];
    const returnPolicy =
      this.returnPoliciesList.find(rp => rp.returnPolicyId === Number(this.selectedReturnPolicyId))
      ?? this.returnPoliciesList[0];

    let minProcessingDays: number | null = null;
    let maxProcessingDays: number | null = null;
    let processingTimeUnit: string = readiness?.unit || 'business_days';

    if (readiness && ((readiness.minDays ?? 0) > 0 || (readiness.maxDays ?? 0) > 0)) {
      minProcessingDays = readiness.minDays ?? null;
      maxProcessingDays = readiness.maxDays ?? null;
    } else if (profile && ((profile.minDays ?? 0) > 0 || (profile.maxDays ?? 0) > 0)) {
      minProcessingDays = profile.minDays ?? null;
      maxProcessingDays = profile.maxDays ?? null;
    } else {
      const readinessMatch = readiness?.title?.match(/(\d+)\s*[-–]\s*(\d+)/);
      const displayMatch = profile?.processingDaysDisplay?.match(/(\d+)\s*[-–]\s*(\d+)/);
      const titleMatch = profile?.title?.match(/(\d+)\s*[-–]\s*(\d+)\s*(?:iş günü|gün|business\s*days?|days?|hafta|weeks?)/i);
      const matched = readinessMatch || displayMatch || titleMatch;
      if (matched) {
        minProcessingDays = Number(matched[1]);
        maxProcessingDays = Number(matched[2]);
        if (/hafta|week/i.test(matched[0]) || (readiness?.title && /hafta|week/i.test(readiness.title))) {
          processingTimeUnit = 'weeks';
        }
      }
    }

    let returnPolicySummary: string | undefined;
    if (returnPolicy) {
      const deadlineText = returnPolicy.returnDeadline && returnPolicy.returnDeadline > 0
        ? ` within ${returnPolicy.returnDeadline} days`
        : '';
      if (returnPolicy.acceptsReturns && returnPolicy.acceptsExchanges) {
        returnPolicySummary = `Returns and exchanges accepted${deadlineText}`;
      } else if (returnPolicy.acceptsReturns) {
        returnPolicySummary = `Returns accepted${deadlineText}`;
      } else if (returnPolicy.acceptsExchanges) {
        returnPolicySummary = `Exchanges accepted${deadlineText}`;
      } else {
        returnPolicySummary = 'Returns and exchanges are not accepted';
      }
    }

    return {
      isDigital: false,
      profileId: profile?.id ?? this.selectedShippingProfileId,
      profileTitle: profile?.title,
      minProcessingDays,
      maxProcessingDays,
      processingDaysDisplay: profile?.processingDaysDisplay,
      readinessState: readiness?.state,
      readinessTitle: readiness?.title,
      minDeliveryDays: profile?.minDeliveryDays ?? null,
      maxDeliveryDays: profile?.maxDeliveryDays ?? null,
      processingTimeUnit,
      hasFreeShipping: profile?.hasFreeShipping ?? false,
      originCountryIso: profile?.originCountryIso,
      mailClass: profile?.mailClass,
      destinationsSummary: profile?.destinationsSummary,
      returnPolicySummary
    };
  }

  private getVariationsSummary(): string {
    if (!this.enableVariations) return '';
    const parts: string[] = [];
    if (this.varType1 && this.varValues1) {
      parts.push(`${this.varType1}: ${this.varValues1}`);
    }
    if (this.enableVar2 && this.varType2 && this.varValues2) {
      parts.push(`${this.varType2}: ${this.varValues2}`);
    }
    return parts.join(' | ');
  }

  generateWithAi(): void {
    const titleInput = this.title.trim();
    const descInput = this.description.trim();
    const input = titleInput || descInput;

    if (!input) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterTitleForFullAi'));
      return;
    }

    // Kullanıcının açıklamadaki ham notlarını koru ve yedekle
    if (descInput && !this.originalNotesBackup) {
      this.originalNotesBackup = descInput;
      this.hasBackupNotes = true;
    }

    const sellerNotes = descInput || this.originalNotesBackup || '';
    const variationsSummary = this.getVariationsSummary();
    const shippingInfo = this.buildShippingAiContext();

    this.isGeneratingAll = true;
    this.listingAiService.generateCompleteListing(input, this.materials, {
      preliminaryNotes: sellerNotes,
      variationsSummary,
      category: this.selectedCategory,
      shippingInfo
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isGeneratingAll = false;
        this.title = res.title;
        this.selectedCategory = res.category;
        this.tags = res.tags;
        this.description = res.description;
        if (res.materials) this.materials = res.materials;

        // Üretilen başlık ve açıklamayla resmi Etsy taksonomi eşleştirmesini tetikle
        this.suggestAiCategory();

        this.showToast(res.summaryMessage);
      },
      error: (err) => {
        this.isGeneratingAll = false;
        this.showToast(err?.message || this.transloco.translate('listings.fastCreator.messages.fullAiError'));
      }
    });
  }

  // --- GALLERY IMAGE OPERATIONS (0/10) ---
  onImgError(event: any): void {
    event.target.src = 'data:image/svg+xml;utf8,<svg xmlns="http://www.w3.org/2000/svg" width="400" height="400" viewBox="0 0 400 400"><rect width="400" height="400" fill="%231e293b"/><text x="50%" y="50%" fill="%2394a3b8" font-family="sans-serif" font-size="20" text-anchor="middle" dominant-baseline="middle">🎨 3D Tasarım Görseli</text></svg>';
  }

  onFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (!input.files || input.files.length === 0) return;

    const files = Array.from(input.files);
    for (const file of files) {
      if (this.galleryImages.length >= 10) {
        alert(this.transloco.translate('listings.fastCreator.messages.maxImagesAlert'));
        break;
      }
      const reader = new FileReader();
      reader.onload = (e: ProgressEvent<FileReader>) => {
        if (e.target?.result && this.galleryImages.length < 10) {
          const isFirst = this.galleryImages.length === 0;
          this.galleryImages.push({
            id: 'local_' + Date.now() + '_' + Math.random().toString(36).substr(2, 4),
            url: e.target.result as string,
            isCover: isFirst,
            name: file.name
          });
          this.updateCoverStatus();
        }
      };
      reader.readAsDataURL(file);
    }
    input.value = '';
  }

  removeImage(index: number): void {
    this.galleryImages.splice(index, 1);
    this.updateCoverStatus();
  }

  moveImageLeft(index: number): void {
    if (index <= 0) return;
    const temp = this.galleryImages[index];
    this.galleryImages[index] = this.galleryImages[index - 1];
    this.galleryImages[index - 1] = temp;
    this.updateCoverStatus();
  }

  moveImageRight(index: number): void {
    if (index >= this.galleryImages.length - 1) return;
    const temp = this.galleryImages[index];
    this.galleryImages[index] = this.galleryImages[index + 1];
    this.galleryImages[index + 1] = temp;
    this.updateCoverStatus();
  }

  setAsCover(index: number): void {
    if (index <= 0 || index >= this.galleryImages.length) return;
    const target = this.galleryImages.splice(index, 1)[0];
    this.galleryImages.unshift(target);
    this.updateCoverStatus();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.coverUpdated'));
  }

  updateCoverStatus(): void {
    this.galleryImages.forEach((img, idx) => {
      img.isCover = (idx === 0);
    });
  }

  openStudioGalleryPicker(): void {
    this.isStudioPickerOpen = true;
  }

  closeStudioGalleryPicker(): void {
    this.isStudioPickerOpen = false;
  }

  pickFromStudio(url: string): void {
    if (this.galleryImages.length >= 10) {
      alert(this.transloco.translate('listings.fastCreator.messages.maxImagesStudioAlert'));
      return;
    }
    this.galleryImages.push({
      id: 'studio_' + Date.now(),
      url: url,
      isCover: this.galleryImages.length === 0,
      name: this.transloco.translate('listings.fastCreator.messages.studioImageName')
    });
    this.updateCoverStatus();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.studioImageAdded'));
  }

  // --- AI IMAGE STUDIO ACTIONS ---
  applyPromptPreset(presetName: string, styleKeyword: string): void {
    const prod = this.title ? this.title.slice(0, 50).trim() : 'Etsy handcrafted product';
    this.aiPrompt = `Professional commercial product photography of ${prod}, ${styleKeyword}, high detail, studio lighting, 8k render, etsy showcase`;
  }

  fillPromptFromTitle(): void {
    if (!this.title.trim()) {
      alert(this.transloco.translate('listings.fastCreator.messages.enterTitleFirstAlert'));
      return;
    }
    this.aiPrompt = `Professional commercial product photography of ${this.title.trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase`;
  }

  generateAiImage(): void {
    if (!this.aiPrompt.trim()) {
      this.fillPromptFromTitle();
    }
    this.isGeneratingAiImage = false;
    this.showToast(this.transloco.translate('listings.fastCreator.messages.aiImageNotConnected'));
  }

  addGeneratedToGallery(): void {
    if (!this.lastGeneratedImage) return;
    if (this.galleryImages.length >= 10) {
      alert(this.transloco.translate('listings.fastCreator.messages.maxImagesAlert'));
      return;
    }
    this.galleryImages.push({
      id: 'ai_gen_' + Date.now(),
      url: this.lastGeneratedImage,
      isCover: this.galleryImages.length === 0,
      name: this.transloco.translate('listings.fastCreator.messages.aiStudioImageName')
    });
    this.updateCoverStatus();
    this.lastGeneratedImage = null;
    this.showToast(this.transloco.translate('listings.fastCreator.messages.generatedImageAdded'));
  }

  // --- VARIATIONS & PRICING LOCK LOGIC ---
  get hasActiveVariations(): boolean {
    return this.enableVariations && Array.isArray(this.variationRows) && this.variationRows.length > 0;
  }

  get hasPricingVariations(): boolean {
    return this.enableVariations && (
      this.variationRows.length > 0 ||
      this.varOptions1.length > 0 ||
      (this.enableVar2 && this.varOptions2.length > 0)
    );
  }

  get varOptions1(): string[] {
    return this.parseVariationOptionGroup(this.varValues1).map(o => o.cleanValue);
  }

  get varOptions2(): string[] {
    return this.parseVariationOptionGroup(this.varValues2).map(o => o.cleanValue);
  }

  get isVariationPricingActive(): boolean {
    return this.hasActiveVariations && this.customVariationPricing;
  }

  get minVariationPrice(): number {
    if (!this.variationRows || this.variationRows.length === 0) return Number(this.priceUsd) || 0;
    const active = this.variationRows.filter(r => r.active !== false && Number(r.price) > 0);
    if (active.length > 0) return Math.min(...active.map(r => Number(r.price)));
    const anyPositive = this.variationRows.filter(r => Number(r.price) > 0);
    return anyPositive.length > 0 ? Math.min(...anyPositive.map(r => Number(r.price))) : (Number(this.priceUsd) || 0);
  }

  get maxVariationPrice(): number {
    if (!this.variationRows || this.variationRows.length === 0) return Number(this.priceUsd) || 0;
    const active = this.variationRows.filter(r => r.active !== false && Number(r.price) > 0);
    if (active.length > 0) return Math.max(...active.map(r => Number(r.price)));
    const anyPositive = this.variationRows.filter(r => Number(r.price) > 0);
    return anyPositive.length > 0 ? Math.max(...anyPositive.map(r => Number(r.price))) : (Number(this.priceUsd) || 0);
  }

  get effectivePrice(): number {
    return this.isVariationPricingActive ? this.minVariationPrice : (Number(this.priceUsd) || 0);
  }

  get effectiveQuantity(): number {
    return this.isVariationPricingActive ? this.totalVariationStock : (Number(this.quantity) || 0);
  }

  get totalVariationStock(): number {
    if (!this.variationRows || this.variationRows.length === 0) return Number(this.quantity) || 0;
    return this.variationRows
      .filter(r => r.active !== false)
      .reduce((sum, r) => sum + (Number(r.quantity) || 0), 0);
  }

  onVariationRowChange(): void {
    if (this.variationRows && this.variationRows.length > 0) {
      for (const row of this.variationRows) {
        if (row.agentShippingUsd !== undefined || row.agentNetMarginPct !== undefined) {
          const breakdown = calculateEtsyNetProfit({
            salePriceUsd: Number(row.price) || 0,
            shippingCostUsd: row.agentShippingUsd ?? (this.estimatedShippingCostUsd ?? 0),
            usdTryRate: this.usdTryRate,
            includeOffsiteAds: this.includeOffsiteAdsFee
          });
          const net = breakdown.netRemainingUsd - (this.agentProductionCostUsd || 0);
          row.agentNetProfitUsd = Math.round(net * 100) / 100;
          row.agentNetMarginPct = row.price > 0 ? Math.round((net / row.price) * 1000) / 10 : 0;
        }
      }
    }

    if (this.isVariationPricingActive) {
      const minP = this.minVariationPrice;
      if (minP > 0) {
        this.priceUsd = minP;
        this.updatePriceTry();
      }
      const totalQ = this.totalVariationStock;
      if (totalQ > 0) {
        this.quantity = totalQ;
      }
    }
    this.saveDraftToStorage();
  }

  onVariationPricingToggle(): void {
    if (this.isVariationPricingActive) {
      this.onVariationRowChange();
    } else if (this.enableVariations && this.variationRows.length > 0) {
      const base = Number(this.priceUsd) || 0;
      if (base > 0) {
        this.variationRows.forEach(r => {
          r.price = base;
        });
      }
      this.saveDraftToStorage();
    }
  }

  onVariationsToggled(): void {
    if (this.enableVariations) {
      this.customVariationPricing = true;
    } else {
      this.selectedProfitVariationKey = '__base__';
      if (this.variationAiAnalysis && !this.variationAiAnalysis.warning) {
        this.variationAiAnalysis = null;
      }
    }
    this.rebuildVariationGrid();
    if (this.isVariationPricingActive) {
      this.onVariationRowChange();
    }
    this.saveDraftToStorage();
  }

  setVarType(groupIndex: number, typeName: string): void {
    if (groupIndex === 1) {
      this.varType1 = typeName;
    } else {
      this.varType2 = typeName;
    }
    this.rebuildVariationGrid();
  }

  onVariationPaste(event: ClipboardEvent): void {
    const pasted = event.clipboardData?.getData('text') || '';
    if (pasted.trim()) {
      setTimeout(() => this.applyPastedVariations(pasted, true), 0);
    }
  }

  applyPastedVariations(rawInput?: string, silentOnMiss = false): void {
    const input = (rawInput ?? this.variationPasteInput).trim();
    const urlRef = extractEtsyListingRef(input);
    if (urlRef) {
      this.extensionListingUrlInput = urlRef.listingUrl;
      this.variationPasteInput = '';
      void this.fetchVariationsViaExtension(urlRef.listingUrl);
      return;
    }

    const parsed = parseEtsyVariationsFromClipboard(input, this.priceUsd || 0, this.usdTryRate);
    if (!parsed.hasVariations) {
      if (!silentOnMiss) {
        this.showToast(this.transloco.translate('listings.fastCreator.messages.noVariationDetected'));
      }
      return;
    }

    this.enableVariations = true;
    this.varType1 = parsed.varType1;
    this.varValues1 = parsed.varValues1;
    this.enableVar2 = parsed.enableVar2;
    this.varType2 = parsed.varType2;
    this.varValues2 = parsed.varValues2;
    this.variationRows = parsed.variationRows.map(r => ({ ...r }));
    this.excludedVariationKeys = [];
    this.variationAiAnalysis = null;
    this.customVariationPricing = true;
    this.selectedProfitVariationKey = '__base__';
    this.variationImportWarning = '';
    this.showVariationImportBox = false;
    this.variationPasteInput = '';

    this.onVariationRowChange();
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.pastedVariationsSuccess', {
      count: parsed.variationRows.length,
      format: parsed.sourceFormat.toUpperCase()
    }));
  }

  async fetchVariationsViaExtension(rawInput?: string): Promise<void> {
    const targetInput = (rawInput ?? this.extensionListingUrlInput ?? this.clonedSourceListingUrl).trim();
    const ref = extractEtsyListingRef(targetInput);
    if (!ref) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.invalidEtsyUrl'));
      return;
    }

    if (this.isFetchingVariationsViaExtension) return;
    this.isFetchingVariationsViaExtension = true;
    this.variationImportWarning = '';

    try {
      const bridgeReady = await this.extensionBridge.checkBridgeAvailable(600);
      if (!bridgeReady) {
        this.variationImportWarning = this.transloco.translate('listings.fastCreator.messages.extBridgeWarning');
        this.showToast(this.transloco.translate('listings.fastCreator.messages.extBridgeToast'));
        return;
      }

      const inv = await this.extensionBridge.fetchListingVariationsViaExtension(ref.listingId, ref.listingUrl);
      if (!inv || !inv.hasVariations) {
        this.variationImportWarning = this.transloco.translate('listings.fastCreator.messages.extNoVariationsWarning');
        this.showToast(this.transloco.translate('listings.fastCreator.messages.extNoVariationsToast'));
        return;
      }

      this.applyExtensionInventoryToForm(inv);
    } finally {
      this.isFetchingVariationsViaExtension = false;
    }
  }

  private applyExtensionInventoryToForm(inv: CompetitorListingInventoryDto): void {
    this.enableVariations = true;
    this.excludedVariationKeys = [];
    this.variationAiAnalysis = null;
    this.varType1 = inv.varType1 || this.varType1;
    this.varValues1 = inv.varValues1 || '';
    this.enableVar2 = Boolean(inv.enableVar2 && inv.varType2 && inv.varValues2);
    this.varType2 = this.enableVar2 ? (inv.varType2 || 'Renk / Color') : this.varType2;
    this.varValues2 = this.enableVar2 ? (inv.varValues2 || '') : '';

    if (inv.offerings && inv.offerings.length > 0) {
      this.variationRows = inv.offerings.map(o => ({
        key: o.key,
        price: Number(o.price) > 0 ? Number(o.price) : (this.priceUsd || 0),
        quantity: Number(o.quantity) > 0 ? Number(o.quantity) : 15,
        active: o.active !== false
      }));
      this.customVariationPricing = true;
    } else {
      this.customVariationPricing = true;
      this.rebuildVariationGrid();
    }

    this.selectedProfitVariationKey = '__base__';
    this.variationImportWarning = '';
    this.showVariationImportBox = false;
    this.onVariationRowChange();
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.extVariationsSuccess', { count: this.variationRows.length }));
  }

  private parseVariationOptionGroup(raw: string) {
    const parsed = (raw || '')
      .split(',')
      .map(s => s.trim())
      .filter(s => s.length > 0)
      .map(s => parseInlineVariationOptionPrice(s, this.usdTryRate))
      .filter(o => o.cleanValue.length > 0);

    const unique: typeof parsed = [];
    const seen = new Set<string>();
    for (const o of parsed) {
      const k = o.cleanValue.toLowerCase();
      if (!seen.has(k)) {
        seen.add(k);
        unique.push(o);
      }
    }
    return unique;
  }

  rebuildVariationGrid(): void {
    if (!this.enableVariations) {
      this.variationRows = [];
      this.selectedProfitVariationKey = '__base__';
      return;
    }

    const basePrice = Number(this.priceUsd) || 0;
    const opts1 = this.parseVariationOptionGroup(this.varValues1);
    const opts2 = this.enableVar2 ? this.parseVariationOptionGroup(this.varValues2) : [];

    if (opts1.length === 0 && opts2.length === 0) {
      this.variationRows = [];
      this.selectedProfitVariationKey = '__base__';
      return;
    }

    const hasAnyInlinePrice =
      opts1.some(o => o.priceUsd !== undefined || o.deltaUsd !== undefined) ||
      opts2.some(o => o.priceUsd !== undefined || o.deltaUsd !== undefined);
    if (hasAnyInlinePrice) {
      this.customVariationPricing = true;
    }

    const allCombinations: Array<{ key: string; inlinePrice?: number; active: boolean }> = [];
    if (opts1.length > 0 && this.enableVar2 && opts2.length > 0) {
      for (const o1 of opts1) {
        for (const o2 of opts2) {
          allCombinations.push({
            key: `${o1.cleanValue} / ${o2.cleanValue}`,
            inlinePrice: resolveVariationOptionPairPrice(basePrice, o1, o2),
            active: o1.active && o2.active
          });
        }
      }
    } else if (opts1.length > 0) {
      for (const o1 of opts1) {
        allCombinations.push({
          key: o1.cleanValue,
          inlinePrice: resolveVariationOptionPairPrice(basePrice, o1),
          active: o1.active
        });
      }
    } else {
      for (const o2 of opts2) {
        allCombinations.push({
          key: o2.cleanValue,
          inlinePrice: resolveVariationOptionPairPrice(basePrice, o2),
          active: o2.active
        });
      }
    }

    const validKeySet = new Set(allCombinations.map(c => c.key.trim().toLowerCase()));
    this.excludedVariationKeys = (this.excludedVariationKeys || [])
      .map(k => k.trim().toLowerCase())
      .filter((k, idx, arr) => k.length > 0 && validKeySet.has(k) && arr.indexOf(k) === idx);
    const excludedSet = new Set(this.excludedVariationKeys);

    const combinations = allCombinations.filter(c => !excludedSet.has(c.key.trim().toLowerCase()));
    const existing = new Map(this.variationRows.map(r => [r.key, r]));
    const defaultPerRowQty = Math.max(5, Math.floor((Number(this.quantity) || 15) / Math.max(1, combinations.length)));

    this.variationRows = combinations.map(combo => {
      const prev = existing.get(combo.key);
      if (prev) {
        let nextPrice = Number(prev.price) || 0;
        if (combo.inlinePrice !== undefined) {
          nextPrice = combo.inlinePrice;
        } else if ((!this.customVariationPricing || nextPrice <= 0) && basePrice > 0) {
          nextPrice = basePrice;
        }
        return {
          ...prev,
          price: nextPrice,
          active: combo.active ? prev.active : false
        };
      }

      const initialPrice = combo.inlinePrice !== undefined ? combo.inlinePrice : basePrice;
      return {
        key: combo.key,
        price: initialPrice,
        quantity: combo.active ? defaultPerRowQty : 0,
        active: combo.active
      };
    });

    if (
      this.selectedProfitVariationKey !== '__base__' &&
      !this.variationRows.some(r => r.key === this.selectedProfitVariationKey)
    ) {
      this.selectedProfitVariationKey = '__base__';
    }

    if (this.isVariationPricingActive) {
      this.onVariationRowChange();
    }
  }

  dismissVariationAiAnalysis(): void {
    this.variationAiAnalysis = null;
  }

  removeVariationGroup(groupIndex: 1 | 2): void {
    const preservedBase = this.effectivePrice || Number(this.priceUsd) || 0;

    if (groupIndex === 1) {
      const hasGroup2 = this.enableVar2 && this.varOptions2.length > 0;
      if (hasGroup2) {
        this.varType1 = this.varType2.trim() || 'Color';
        this.varValues1 = this.varValues2;
        this.enableVar2 = false;
        this.varType2 = 'Renk / Color';
        this.varValues2 = '';
        this.excludedVariationKeys = [];
        if (preservedBase > 0 && !(Number(this.priceUsd) > 0)) {
          this.priceUsd = preservedBase;
          this.updatePriceTry();
        }
        this.variationRows = [];
        this.rebuildVariationGrid();
        this.saveDraftToStorage();
        this.showToast(this.transloco.translate('listings.fastCreator.messages.group1RemovedPromoted'));
      } else {
        if (preservedBase > 0 && !(Number(this.priceUsd) > 0)) {
          this.priceUsd = preservedBase;
          this.updatePriceTry();
        }
        this.varValues1 = '';
        this.enableVar2 = false;
        this.varValues2 = '';
        this.variationRows = [];
        this.excludedVariationKeys = [];
        this.enableVariations = false;
        this.customVariationPricing = false;
        this.variationAiAnalysis = null;
        this.selectedProfitVariationKey = '__base__';
        this.saveDraftToStorage();
        this.showToast(this.transloco.translate('listings.fastCreator.messages.group1Removed'));
      }
      return;
    }

    this.enableVar2 = false;
    this.varValues2 = '';
    this.excludedVariationKeys = [];
    if (this.varOptions1.length === 0) {
      if (preservedBase > 0 && !(Number(this.priceUsd) > 0)) {
        this.priceUsd = preservedBase;
        this.updatePriceTry();
      }
      this.enableVariations = false;
      this.customVariationPricing = false;
      this.variationRows = [];
      this.variationAiAnalysis = null;
      this.selectedProfitVariationKey = '__base__';
    } else {
      this.rebuildVariationGrid();
    }
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.group2Removed'));
  }

  removeVariationOption(groupIndex: 1 | 2, optionIndex: number): void {
    const options = groupIndex === 1 ? this.varOptions1 : this.varOptions2;
    const targetOption = options[optionIndex];
    if (!targetOption) return;

    const targetNorm = targetOption.trim().toLowerCase();
    const raw = groupIndex === 1 ? this.varValues1 : this.varValues2;
    const parts = (raw || '')
      .split(',')
      .map(s => s.trim())
      .filter(s => s.length > 0);

    const nextParts = parts.filter(part => {
      const clean = parseInlineVariationOptionPrice(part, this.usdTryRate).cleanValue.trim().toLowerCase();
      return clean !== targetNorm;
    });

    if (nextParts.length === 0) {
      this.removeVariationGroup(groupIndex);
      return;
    }

    if (groupIndex === 1) {
      this.varValues1 = nextParts.join(', ');
    } else {
      this.varValues2 = nextParts.join(', ');
    }
    this.rebuildVariationGrid();
    this.saveDraftToStorage();
  }

  private splitTwoGroupKey(key: string): string[] {
    const clean = (key || '').trim();
    if (!clean) return [];
    const spaced = clean.split(' / ').map(p => p.trim()).filter(Boolean);
    if (spaced.length >= 2) return spaced;
    return clean.split('/').map(p => p.trim()).filter(Boolean);
  }

  removeVariationRow(rowIndex: number): void {
    if (!this.variationRows || rowIndex < 0 || rowIndex >= this.variationRows.length) return;

    const removed = this.variationRows[rowIndex];
    const preservedBase = this.effectivePrice || Number(this.priceUsd) || 0;
    const removedNorm = (removed.key || '').trim().toLowerCase();
    const hasTwoGroups = this.enableVar2 && this.varOptions1.length > 0 && this.varOptions2.length > 0;

    if (!hasTwoGroups) {
      const targetGroup: 1 | 2 = this.varOptions1.length > 0 ? 1 : 2;
      const raw = targetGroup === 1 ? this.varValues1 : this.varValues2;
      const parts = (raw || '').split(',').map(s => s.trim()).filter(s => s.length > 0);
      const nextParts = parts.filter(part => {
        const parsed = parseInlineVariationOptionPrice(part, this.usdTryRate);
        return parsed.cleanValue.trim().toLowerCase() !== removedNorm && part.toLowerCase() !== removedNorm;
      });

      if (nextParts.length === 0) {
        this.removeVariationGroup(targetGroup);
        return;
      }

      if (targetGroup === 1) {
        this.varValues1 = nextParts.join(', ');
      } else {
        this.varValues2 = nextParts.join(', ');
      }
      this.variationRows.splice(rowIndex, 1);
      if (this.selectedProfitVariationKey === removed.key) {
        this.selectedProfitVariationKey = '__base__';
      }
      this.onVariationRowChange();
      this.saveDraftToStorage();
      return;
    }

    if (removedNorm && !this.excludedVariationKeys.includes(removedNorm)) {
      this.excludedVariationKeys = [...this.excludedVariationKeys, removedNorm];
    }
    this.variationRows.splice(rowIndex, 1);

    if (this.selectedProfitVariationKey === removed.key) {
      this.selectedProfitVariationKey = '__base__';
    }

    if (this.variationRows.length === 0) {
      if (preservedBase > 0 && !(Number(this.priceUsd) > 0)) {
        this.priceUsd = preservedBase;
        this.updatePriceTry();
      }
      this.enableVariations = false;
      this.enableVar2 = false;
      this.varValues1 = '';
      this.varValues2 = '';
      this.excludedVariationKeys = [];
      this.customVariationPricing = false;
      this.variationAiAnalysis = null;
      this.selectedProfitVariationKey = '__base__';
      this.saveDraftToStorage();
      return;
    }

    const usedG1 = new Set<string>();
    const usedG2 = new Set<string>();
    for (const row of this.variationRows) {
      const segs = this.splitTwoGroupKey(row.key || '');
      if (segs[0]) usedG1.add(segs[0].toLowerCase());
      if (segs[1]) usedG2.add(segs[1].toLowerCase());
    }

    const filterGroupText = (raw: string, usedSet: Set<string>): string =>
      (raw || '')
        .split(',')
        .map(s => s.trim())
        .filter(s => {
          if (!s) return false;
          const clean = parseInlineVariationOptionPrice(s, this.usdTryRate).cleanValue.trim().toLowerCase();
          return usedSet.has(clean);
        })
        .join(', ');

    this.varValues1 = filterGroupText(this.varValues1, usedG1);
    this.varValues2 = filterGroupText(this.varValues2, usedG2);

    const remainingOpts1 = this.varOptions1;
    const remainingOpts2 = this.varOptions2;
    if (remainingOpts1.length === 0 && remainingOpts2.length > 0) {
      this.varType1 = this.varType2.trim() || 'Color';
      this.varValues1 = this.varValues2;
      this.enableVar2 = false;
      this.varType2 = 'Renk / Color';
      this.varValues2 = '';
      this.excludedVariationKeys = [];
      this.variationRows = this.variationRows.map(r => {
        const parts = this.splitTwoGroupKey(r.key || '');
        return { ...r, key: parts[1] || parts[0] || r.key };
      });
    } else if (remainingOpts2.length === 0) {
      this.enableVar2 = false;
      this.varValues2 = '';
      this.excludedVariationKeys = [];
      this.variationRows = this.variationRows.map(r => {
        const parts = this.splitTwoGroupKey(r.key || '');
        return { ...r, key: parts[0] || r.key };
      });
    } else {
      const opt1Set = new Set(remainingOpts1.map(o => o.toLowerCase()));
      const opt2Set = new Set(remainingOpts2.map(o => o.toLowerCase()));
      this.excludedVariationKeys = this.excludedVariationKeys.filter(k => {
        const segs = this.splitTwoGroupKey(k);
        return segs.length === 2 && opt1Set.has(segs[0]) && opt2Set.has(segs[1]);
      });
    }

    this.onVariationRowChange();
    this.saveDraftToStorage();
  }

  syncBasePriceToVariations(): void {
    let p = Number(this.priceUsd) || 0;
    if (p <= 0) {
      const firstPositive = this.variationRows.find(r => (Number(r.price) || 0) > 0);
      if (firstPositive) {
        p = Number(firstPositive.price);
      }
    }
    if (p <= 0) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterBasePriceFirst'));
      return;
    }
    this.variationRows.forEach(row => {
      row.price = p;
    });
    this.onVariationRowChange();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.pricesSynced', { price: p.toFixed(2) }));
  }

  getSelectedImageForRank(rank?: number): GalleryImage | undefined {
    if (!rank || rank < 1 || rank > this.galleryImages.length) return undefined;
    return this.galleryImages[rank - 1];
  }

  autoAssignVariationImages(): void {
    if (!this.variationRows || this.variationRows.length === 0) return;
    if (!this.galleryImages || this.galleryImages.length === 0) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.uploadImageFirst'));
      return;
    }

    let assigned = 0;
    this.variationRows.forEach((r, idx) => {
      if (idx < this.galleryImages.length) {
        r.imageRank = idx + 1;
        assigned++;
      }
    });

    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.imagesAutoAssigned', { count: assigned }));
  }

  clearVariationImages(): void {
    if (!this.variationRows || this.variationRows.length === 0) return;
    this.variationRows.forEach(r => {
      r.imageRank = undefined;
    });
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.variationImagesCleared'));
  }

  suggestAiVariations(): void {
    const cleanTitle = (this.title || '').trim();
    const cleanDesc = (this.description || '').trim();
    if (!cleanTitle && !cleanDesc) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterInfoForVariations'));
      return;
    }

    this.isGeneratingVariations = true;
    const s = this.aiService.settings();
    const activeApiKey = this.aiService.getActiveApiKey();

    this.etsyApi.suggestListingVariations({
      title: cleanTitle,
      description: cleanDesc,
      category: this.selectedCategory || '',
      tags: this.tags,
      materials: this.sanitizeMaterialsForPayload(this.materials),
      listingType: this.listingType,
      priceUsd: this.effectivePrice || Number(this.priceUsd) || 0,
      currentVarType1: this.enableVariations ? this.varType1 : '',
      currentVarValues1: this.enableVariations ? this.varValues1 : '',
      currentVarType2: this.enableVariations && this.enableVar2 ? this.varType2 : '',
      currentVarValues2: this.enableVariations && this.enableVar2 ? this.varValues2 : '',
      provider: s.provider,
      model: this.aiService.activeModelName(),
      apiKey: activeApiKey,
      strictNeverOffline: s.strictNeverOffline
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isGeneratingVariations = false;
        this.variationAiAnalysis = {
          provider: res.provider,
          model: res.model,
          usedAi: res.usedAi,
          summary: res.analysisSummary,
          warning: res.warningMessage
        };

        if (res.suggestedVariations && res.suggestedVariations.length > 0) {
          this.excludedVariationKeys = [];
          this.enableVariations = true;
          this.customVariationPricing = true;
          this.varType1 = res.suggestedVariations[0].name;
          this.varValues1 = res.suggestedVariations[0].values.join(', ');
          if (res.suggestedVariations.length > 1) {
            this.enableVar2 = true;
            this.varType2 = res.suggestedVariations[1].name;
            this.varValues2 = res.suggestedVariations[1].values.join(', ');
          } else {
            this.enableVar2 = false;
            this.varValues2 = '';
          }
          if (res.suggestedOfferings && res.suggestedOfferings.length > 0) {
            this.variationRows = res.suggestedOfferings.map(o => ({
              key: o.key,
              price: Number(o.price) > 0 ? Number(o.price) : (Number(this.priceUsd) || 0),
              quantity: Math.max(0, Number(o.quantity) || 0),
              active: o.active !== false
            }));
            this.selectedProfitVariationKey = '__base__';
            this.onVariationRowChange();
          } else {
            this.rebuildVariationGrid();
          }
          this.saveDraftToStorage();
          const groupLabel = res.suggestedVariations.map(v => v.name).join(' & ');
          if (res.warningMessage) {
            this.showToast(`⚠️ ${res.warningMessage}`);
          } else {
            this.showToast(this.transloco.translate('listings.fastCreator.messages.aiVariationsSuccess', { provider: res.provider, groups: groupLabel }));
          }
        } else {
          this.showToast(this.transloco.translate('listings.fastCreator.messages.noVariationsFound'));
        }
      },
      error: (err) => {
        this.isGeneratingVariations = false;
        const detail = err?.error?.message || err?.error?.detail || err?.message || this.transloco.translate('listings.fastCreator.messages.aiVariationsError');
        this.variationAiAnalysis = {
          provider: s.provider,
          model: this.aiService.activeModelName(),
          usedAi: false,
          summary: '',
          warning: detail
        };
        this.showToast(`❌ ${detail}`);
      }
    });
  }

  // --- RIGHT-SIDE SHIPPING & PRICING DRAWER ---
  openShippingDrawer(): void {
    this.isShippingDrawerOpen = true;
    if (this.drawerCarrierQuotes.length === 0 && !this.isLoadingCarrierQuotes) {
      this.fetchDrawerCarrierQuotes();
    }
  }

  closeShippingDrawer(): void {
    this.isCompetitorPanelOpen = false;
    this.isShippingDrawerOpen = false;
  }

  toggleShippingDrawer(): void {
    if (this.isShippingDrawerOpen) {
      this.closeShippingDrawer();
    } else {
      this.openShippingDrawer();
    }
  }

  get scannedCompetitorsCount(): number {
    const mb = this.pricingAgentResult?.marketBenchmark;
    if (!mb) return 0;
    return mb.scannedCompetitors?.length || mb.topCompetitors?.length || mb.competitorCount || 0;
  }

  openCompetitorPanel(): void {
    if (this.scannedCompetitorsCount > 0) {
      this.isShippingDrawerOpen = true;
      this.isCompetitorPanelOpen = true;
    }
  }

  closeCompetitorPanel(): void {
    this.isCompetitorPanelOpen = false;
  }

  toggleCompetitorPanel(): void {
    if (this.isCompetitorPanelOpen) {
      this.closeCompetitorPanel();
    } else {
      this.openCompetitorPanel();
    }
  }

  rescanPricingWithKeyword(keyword: string): void {
    const cleaned = (keyword || '').trim();
    if (!cleaned) return;
    this.agentCustomSearchKeyword = cleaned;
    this.runAiPricingAgent(cleaned);
  }

  get computedDesi(): number {
    const l = Math.max(1, Number(this.agentLengthCm) || 18);
    const w = Math.max(1, Number(this.agentWidthCm) || 14);
    const h = Math.max(1, Number(this.agentHeightCm) || 8);
    return Number(((l * w * h) / 5000).toFixed(2));
  }

  getCarrierLogo(carrierKey: string): string {
    const k = (carrierKey || '').toLowerCase();
    if (k.includes('aras')) return 'assets/shipping/aras_global.png';
    if (k.includes('navlungo')) return 'assets/shipping/navlungo.png';
    if (k.includes('shiptomore')) return 'assets/shipping/shiptomore.png';
    return 'assets/shipping/shipentegra.jpg';
  }

  fetchDrawerCarrierQuotes(): void {
    this.isLoadingCarrierQuotes = true;
    this.carrierQuotesError = '';
    const weightKg = Math.max(0.05, (Number(this.agentBaseWeightGrams) || 250) / 1000);

    this.etsyApi.getCarrierQuotes({
      countryCode: (this.agentTargetCountry || 'US').toUpperCase(),
      weightKg,
      lengthCm: Math.max(1, Number(this.agentLengthCm) || 18),
      widthCm: Math.max(1, Number(this.agentWidthCm) || 14),
      heightCm: Math.max(1, Number(this.agentHeightCm) || 8),
      usdTryRate: this.usdTryRate,
      declaredValueUsd: this.effectivePrice > 0 ? this.effectivePrice : 25,
      quantity: 1
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (resp) => {
        this.isLoadingCarrierQuotes = false;
        const rawQuotes = Array.isArray(resp?.quotes) ? resp.quotes : [];
        const bestByCarrier = new Map<string, AgentCarrierQuoteSummaryDto>();
        for (const q of rawQuotes) {
          const priceUsd = Number(q?.priceUsd || 0);
          if (priceUsd <= 0) continue;
          const key = String(q.carrierKey || q.carrierName || '').toLowerCase();
          const item: AgentCarrierQuoteSummaryDto = {
            carrierKey: q.carrierKey || key,
            carrierName: q.carrierName || key,
            serviceType: q.serviceType || q.serviceCode || 'Standard',
            priceUsd: Number(priceUsd.toFixed(2)),
            priceTry: Number((Number(q.priceTry) || priceUsd * this.usdTryRate).toFixed(2)),
            estimatedDays: q.estimatedDays || this.transloco.translate('listings.fastCreator.messages.defaultEstimatedDays'),
            isLive: Boolean(q.isLive)
          };
          const prev = bestByCarrier.get(key);
          if (!prev || item.priceUsd < prev.priceUsd) {
            bestByCarrier.set(key, item);
          }
        }

        this.drawerCarrierQuotes = Array.from(bestByCarrier.values()).sort((a, b) => a.priceUsd - b.priceUsd);
        if (this.drawerCarrierQuotes.length === 0) {
          this.carrierQuotesError = resp?.warningMessage || this.transloco.translate('listings.fastCreator.messages.noCarrierQuotes');
        } else if (!this.selectedCarrierKey) {
          const cheapest = this.drawerCarrierQuotes[0];
          this.selectedCarrierKey = cheapest.carrierKey;
          this.selectedCarrierName = cheapest.carrierName;
          if (!this.estimatedShippingCostUsd) {
            this.estimatedShippingCostUsd = cheapest.priceUsd;
            this.onVariationRowChange();
            this.saveDraftToStorage();
          }
        }
      },
      error: (err) => {
        this.isLoadingCarrierQuotes = false;
        this.carrierQuotesError = err?.error?.message || err?.message || this.transloco.translate('listings.fastCreator.messages.carrierQuotesError');
      }
    });
  }

  applyCarrierQuoteToListing(quote: AgentCarrierQuoteSummaryDto): void {
    this.selectedCarrierKey = quote.carrierKey;
    this.selectedCarrierName = quote.carrierName;
    this.estimatedShippingCostUsd = Number(quote.priceUsd.toFixed(2));
    this.onVariationRowChange();
    this.saveDraftToStorage();
    this.showToast(this.transloco.translate('listings.fastCreator.messages.carrierApplied', {
      carrier: quote.carrierName,
      price: quote.priceUsd.toFixed(2)
    }));
  }

  runAiPricingAgent(customKeywordOverride?: string): void {
    const cleanTitle = (this.title || '').trim() || (this.description || '').trim();
    if (!cleanTitle) {
      this.showToast(this.transloco.translate('listings.fastCreator.messages.enterInfoForAgent'));
      return;
    }

    const customKeyword = (customKeywordOverride || '').trim() || undefined;
    const hasUserVariations = this.hasPricingVariations;
    if (hasUserVariations) {
      this.rebuildVariationGrid();
    }
    const effectiveHasVariations = this.enableVariations && this.variationRows.length > 0;

    const s = this.aiService.settings();
    const provider = s.provider;
    const apiKey = this.aiService.getActiveApiKey();

    this.isRunningPricingAgent = true;
    this.pricingAgentError = '';

    this.etsyApi.runVariationPricingAgent({
      shopId: this.etsyApi.activeShopId(),
      title: cleanTitle,
      description: this.description || '',
      category: this.selectedCategory || '',
      materials: this.sanitizeMaterialsForPayload(this.materials),
      tags: this.tags,
      customSearchKeyword: customKeyword,
      enableVariations: effectiveHasVariations,
      variationType1: effectiveHasVariations ? this.varType1 : '',
      variationValues1: effectiveHasVariations ? this.varValues1 : '',
      enableVariation2: effectiveHasVariations ? this.enableVar2 : false,
      variationType2: effectiveHasVariations ? this.varType2 : '',
      variationValues2: effectiveHasVariations ? this.varValues2 : '',
      existingVariations: effectiveHasVariations
        ? this.variationRows.map(r => ({
            key: r.key,
            currentPrice: Number(r.price) || 0,
            quantity: Number(r.quantity) || 1,
            active: r.active !== false
          }))
        : [],
      targetCountryCode: this.agentTargetCountry || 'US',
      baseWeightGrams: Math.max(50, Number(this.agentBaseWeightGrams) || 250),
      lengthCm: Math.max(1, Number(this.agentLengthCm) || 18),
      widthCm: Math.max(1, Number(this.agentWidthCm) || 14),
      heightCm: Math.max(1, Number(this.agentHeightCm) || 8),
      productionCostUsd: Math.max(0, Number(this.agentProductionCostUsd) || 0),
      targetNetMarginPercent: Math.max(5, Number(this.agentTargetMarginPct) || 30),
      includeOffsiteAds: this.includeOffsiteAdsFee,
      usdTryRate: this.usdTryRate,
      provider,
      model: this.aiService.activeModelName(),
      apiKey
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isRunningPricingAgent = false;
        this.pricingAgentResult = res;
        this.agentCustomSearchKeyword = res.searchKeywordUsed || customKeyword || '';

        // 1. Kargo modülü maliyetini ve taşıyıcı tekliflerini senkronize et
        this.estimatedShippingCostUsd = Number((res.recommendedBaseShippingCostUsd || 0).toFixed(2));
        if (res.shippingBenchmark) {
          this.selectedCarrierKey = res.shippingBenchmark.selectedCarrierKey || '';
          this.selectedCarrierName = res.shippingBenchmark.selectedCarrierName || '';
          if (Array.isArray(res.shippingBenchmark.carrierQuotes) && res.shippingBenchmark.carrierQuotes.length > 0) {
            this.drawerCarrierQuotes = [...res.shippingBenchmark.carrierQuotes].sort((a, b) => a.priceUsd - b.priceUsd);
          }
        }

        // 2. Tekil ürün modu: Varyasyonlar kapalıysa ASLA varyasyon açma veya üretme
        if (!effectiveHasVariations || res.isSingleProductPricing || !res.pricedVariations || res.pricedVariations.length === 0) {
          if (res.recommendedBasePriceUsd > 0) {
            this.priceUsd = Number(res.recommendedBasePriceUsd.toFixed(2));
            this.updatePriceTry();
          }
          this.saveDraftToStorage();
          this.showToast(
            this.transloco.translate('listings.fastCreator.messages.agentSingleSuccess', {
              price: res.recommendedBasePriceUsd.toFixed(2),
              carrier: res.shippingBenchmark.selectedCarrierName,
              shipping: res.recommendedBaseShippingCostUsd.toFixed(2),
              margin: res.averageNetMarginPercent.toFixed(1)
            })
          );
          return;
        }

        // 3. Varyasyonlu mod: Yalnızca kullanıcının mevcut varyasyon satırlarını fiyatlandır
        this.customVariationPricing = true;
        const pricedMap = new Map(
          res.pricedVariations.map(p => [p.key.trim().toLowerCase(), p])
        );

        this.variationRows.forEach(row => {
          const match = pricedMap.get(row.key.trim().toLowerCase());
          if (match) {
            row.price = match.recommendedPriceUsd;
            row.agentNetProfitUsd = match.netProfitUsd;
            row.agentNetMarginPct = match.netMarginPercent;
            row.agentTierLabel = match.pricingTierLabel;
            row.agentShippingUsd = match.estimatedShippingCostUsd;
          }
        });

        this.onVariationRowChange();
        this.saveDraftToStorage();
        this.showToast(
          this.transloco.translate('listings.fastCreator.messages.agentVariationSuccess', {
            count: res.pricedVariations.length,
            carrier: res.shippingBenchmark.selectedCarrierName,
            shipping: res.recommendedBaseShippingCostUsd.toFixed(2),
            price: res.recommendedBasePriceUsd.toFixed(2)
          })
        );
      },
      error: (err) => {
        this.isRunningPricingAgent = false;
        this.pricingAgentError = err?.error?.message || err?.message || this.transloco.translate('listings.fastCreator.messages.agentError');
        this.showToast(`❌ ${this.pricingAgentError}`);
      }
    });
  }

  // --- CHECKLIST GETTERS ---
  get isTitleReady(): boolean {
    return this.title.trim().length > 0 && this.title.length <= 140;
  }
  get isPriceReady(): boolean {
    return this.effectivePrice > 0 && this.effectiveQuantity > 0;
  }
  get isImagesReady(): boolean {
    return this.galleryImages.length > 0;
  }
  get isShippingReady(): boolean {
    return this.listingType === 'digital' || !!this.selectedShippingProfileId || this.shippingProfilesList.length > 0;
  }
  get isReadinessReady(): boolean {
    return this.listingType === 'digital' || !!this.selectedReadinessStateId || this.readinessStatesList.length > 0;
  }
  get isReturnPolicyReady(): boolean {
    return this.listingType === 'digital' || !!this.selectedReturnPolicyId || this.returnPoliciesList.length > 0;
  }
  get isDescReady(): boolean {
    return this.description.trim().length > 20;
  }
  get isTagsReady(): boolean {
    return this.tags.length > 0;
  }

  // --- PUBLISH & PREVIEW ---
  createdListingResult: {
    listingId: number;
    url: string;
    state?: string;
    uploadedImages?: number;
    message?: string;
    title?: string;
    price?: number;
    quantity?: number;
    variationImagesCount?: number;
  } | null = null;
  isPublishSuccessModalOpen = false;
  isActivatingListing = false;

  openPreviewModal(): void {
    this.isPreviewModalOpen = true;
  }

  closePreviewModal(): void {
    this.isPreviewModalOpen = false;
  }

  closeSuccessModal(): void {
    this.isPublishSuccessModalOpen = false;
    this.resetForm();
  }

  activateListingNow(listingId: number | undefined): void {
    if (!listingId) return;
    this.isActivatingListing = true;
    this.etsyApi.updateListingState(listingId, 'active').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isActivatingListing = false;
        if (this.createdListingResult) {
          this.createdListingResult.state = 'active';
        }
        this.showToast(this.transloco.translate('listings.fastCreator.messages.listingActivated', { id: listingId }));
      },
      error: (err) => {
        this.isActivatingListing = false;
        this.applyClientValidationError(err, this.transloco.translate('listings.fastCreator.messages.activateError'));
      }
    });
  }

  publishListingToEtsy(): void {
    this.clearValidationErrors();
    if (!this.isTitleReady) {
      this.validationErrorSummary = this.transloco.translate('listings.fastCreator.banners.validationDefault');
      this.validationFieldErrors = [
        {
          field: 'Title',
          label: this.transloco.translate('listings.fastCreator.messages.titleFieldLabel'),
          messages: [this.transloco.translate('listings.fastCreator.messages.titleRequiredMsg')]
        }
      ];
      return;
    }

    if (this.enableVariations) {
      this.rebuildVariationGrid();
    }

    this.isSavingDraft = true;

    // Prepare gallery images
    const imagesPayload = this.galleryImages.map((img, idx) => ({
      dataUrl: img.url.startsWith('data:') ? img.url : undefined,
      url: !img.url.startsWith('data:') ? img.url : undefined,
      rank: idx + 1
    }));

    const effectivePrice = this.effectivePrice > 0 ? this.effectivePrice : 39.90;
    const effectiveQuantity = this.effectiveQuantity > 0 ? this.effectiveQuantity : 15;

    // Prepare variation groups (yalnızca boş olmayan gruplar gönderilir)
    const rawGroup1Vals = this.parseVariationOptionGroup(this.varValues1).map(o => o.cleanValue);
    const rawGroup2Vals = this.enableVar2 ? this.parseVariationOptionGroup(this.varValues2).map(o => o.cleanValue) : [];
    const builtGroups = this.enableVariations && this.variationRows.length > 0
      ? [
          ...(rawGroup1Vals.length > 0 ? [{ name: this.varType1, values: rawGroup1Vals }] : []),
          ...(rawGroup2Vals.length > 0 ? [{ name: this.varType2, values: rawGroup2Vals }] : [])
        ]
      : [];
    const variationGroupsPayload = builtGroups.length > 0 ? builtGroups : undefined;

    // Prepare variations (iki grupta hariç tutulan kombinasyonlar Etsy'de pasif olarak işaretlenir)
    let variationsPayload = this.enableVariations && this.variationRows.length > 0
      ? this.variationRows.map(r => ({
          key: r.key,
          price: r.price,
          quantity: r.quantity,
          active: r.active,
          imageRank: r.imageRank
        }))
      : undefined;

    if (variationsPayload && builtGroups.length === 2 && this.excludedVariationKeys.length > 0) {
      const existingLower = new Set(variationsPayload.map(v => v.key.trim().toLowerCase()));
      const excludedSet = new Set(this.excludedVariationKeys.map(k => k.trim().toLowerCase()));
      const fallbackInactiveQty = Math.max(1, Number(variationsPayload[0]?.quantity) || 1);
      for (const v1 of builtGroups[0].values) {
        for (const v2 of builtGroups[1].values) {
          const comboKey = `${v1} / ${v2}`;
          const lower = comboKey.toLowerCase();
          if (excludedSet.has(lower) && !existingLower.has(lower)) {
            variationsPayload.push({
              key: comboKey,
              price: effectivePrice,
              quantity: fallbackInactiveQty,
              active: false,
              imageRank: undefined
            });
          }
        }
      }
    }

    // Clean tags (Etsy max 13, <=20 chars)
    const validTags = this.tags
      .map(t => t.trim())
      .filter(t => t.length > 0 && t.length <= 20)
      .slice(0, 13);

    // Shipping profile ID parsing
    let shippingProfileId: number | null = this.selectedShippingProfileId;
    if (!shippingProfileId && this.shippingProfile && !isNaN(Number(this.shippingProfile))) {
      shippingProfileId = Number(this.shippingProfile);
    }
    if (!shippingProfileId && this.shippingProfilesList.length > 0) {
      shippingProfileId = this.shippingProfilesList[0].id;
    }

    // Readiness state ID parsing
    let readinessStateId: number | null = this.selectedReadinessStateId;
    if (!readinessStateId && this.readinessStatesList.length > 0) {
      readinessStateId = this.readinessStatesList[0].id;
    }

    // Return policy ID parsing
    let returnPolicyId: number | null = this.listingType === 'digital' ? null : this.selectedReturnPolicyId;
    if (this.listingType !== 'digital' && !returnPolicyId && this.returnPoliciesList.length > 0) {
      returnPolicyId = this.returnPoliciesList[0].returnPolicyId;
    }

    const payload = {
      title: this.title.trim(),
      description: this.description.trim() || 'Handmade custom design artisan product.',
      price: effectivePrice,
      quantity: effectiveQuantity,
      taxonomyId: this.selectedTaxonomyId > 0 ? this.selectedTaxonomyId : 1042,
      shippingProfileId,
      readinessStateId,
      returnPolicyId,
      isDigital: this.listingType === 'digital',
      tags: validTags,
      materials: this.sanitizeMaterialsForPayload(this.materials),
      state: (this.isLivePublish ? 'active' : 'draft') as 'draft' | 'active',
      images: imagesPayload,
      variations: variationsPayload,
      variationGroups: variationGroupsPayload
    };

    this.etsyApi.createListing(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isSavingDraft = false;
        if (res && res.success) {
          this.clearDraftFromStorage();
          this.hasRestoredDraft = false;
          this.createdListingResult = {
            ...res,
            title: this.title,
            price: effectivePrice,
            quantity: effectiveQuantity,
            uploadedImages: this.galleryImages.length
          };
          this.isPublishSuccessModalOpen = true;
          this.resetForm();
          const varCount = (res as any).variationsCount || 0;
          const varText = varCount > 0 ? this.transloco.translate('listings.fastCreator.messages.publishVarPart', { count: varCount }) : '';

          if ((res as any).variationError) {
            alert(this.transloco.translate('listings.fastCreator.messages.publishVarErrorAlert', { id: res.listingId, error: (res as any).variationError }));
          }

          const stateLabel = res.state === 'active'
            ? this.transloco.translate('listings.fastCreator.messages.publishStateLive')
            : this.transloco.translate('listings.fastCreator.messages.publishStateDraft');
          this.showToast(this.transloco.translate('listings.fastCreator.messages.publishSuccess', { state: stateLabel, id: res.listingId, varText }));
        } else {
          this.showToast(this.transloco.translate('listings.fastCreator.messages.publishUnverified'));
        }
      },
      error: (err) => {
        this.isSavingDraft = false;
        this.applyClientValidationError(err, this.transloco.translate('listings.fastCreator.messages.publishError'));
      }
    });
  }

  clearValidationErrors(): void {
    this.validationErrorSummary = '';
    this.validationFieldErrors = [];
  }

  private applyClientValidationError(err: unknown, fallbackMessage: string): void {
    const parsed = extractClientValidationErrors(err, fallbackMessage);
    this.validationErrorSummary = parsed.summary;
    this.validationFieldErrors = parsed.fieldErrors;
  }

  private sanitizeMaterialsForPayload(raw: string): string[] {
    if (!raw) return [];
    return raw
      .split(/[,;\n]/)
      .map(m => m.trim())
      .filter(m => m.length > 0)
      .map(m => {
        return m
          .replace(/[&]/g, ' and ')
          .replace(/[\/\\|_+()[\]{}*•"':;.,!?\-–—]/g, ' ')
          .replace(/\s+/g, ' ')
          .trim();
      })
      .filter(m => m.length > 0)
      .slice(0, 13);
  }

  showToast(msg: string): void {
    this.successMessage = msg;
    setTimeout(() => {
      if (this.successMessage === msg) this.successMessage = '';
    }, 4500);
  }
}
