import { Component, OnInit, OnDestroy, HostListener, inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { EtsyApiService, EtsyTaxonomyItemDto } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { EtsyListingAiService, CategoryAiSuggestion, TaxonomyCandidateItem, AiDescriptionContext, DescriptionVariantsResult } from '../../core/services/etsy-listing-ai.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

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
  selectedTemplateId: string;
  isLivePublish: boolean;
  savedAt: string;
  originalNotesBackup?: string;
}

export interface VariationRow {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
}

@Component({
  selector: 'app-fast-creator',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  templateUrl: './fast-creator.component.html',
  styleUrls: ['./fast-creator.component.css']
})
export class FastCreatorComponent implements OnInit, OnDestroy {
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
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
  shippingProfilesList: Array<{ id: number; title: string; minDays?: number; maxDays?: number }> = [];
  selectedShippingProfileId: number | null = null;
  isLoadingShippingProfiles = false;
  readinessStatesList: Array<{ id: number; title: string; minDays?: number; maxDays?: number; state?: string }> = [];
  selectedReadinessStateId: number | null = null;
  isLoadingReadinessStates = false;
  readinessState = '';
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
  varType1 = 'Boyut / Size';
  varValues1 = 'Small, Medium, Large';
  enableVar2 = false;
  varType2 = 'Renk / Color';
  varValues2 = 'Siyah, Beyaz, Altın';
  customVariationPricing = false;
  variationRows: VariationRow[] = [];
  isGeneratingVariations = false;

  // Publish Status & Checklist
  isLivePublish = false;
  isPreviewModalOpen = false;
  isGeneratingAi = false;
  isSavingDraft = false;
  successMessage = '';
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
          name: idx === 0 ? 'Klonlanan Kapak Görseli' : `Klonlanan Görsel #${idx + 1}`
        }));
      }

      if (cloned.hasVariations) {
        this.enableVariations = true;
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
        } else {
          this.rebuildVariationGrid();
        }
      }

      const varCount = (this.variationRows && this.variationRows.length > 0) ? ` & ${this.variationRows.length} Varyasyon` : '';
      this.clonedNotice = `Klonlanan Ürün #${cloned.id}: "${cloned.title.slice(0, 48)}..." (${cloned.shopName}) — ${allUrls.length} Fotoğraf, ${this.tags.length} Etiket${varCount} Aktarıldı`;
      this.updatePriceTry();
    }

    // QueryParams aktarımı (Dış Pazarlar, Viral 3D vb.)
    this.route.queryParams.subscribe(params => {
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
          name: 'Aktarılan Ürün Görseli'
        }];
      }
      if (params['title'] || params['category']) {
        this.clonedNotice = `Aktarılan Ürün: "${this.title.slice(0, 48)}..." - Kategori: ${this.selectedCategory}`;
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
    this.loadCategories();
  }

  refreshLogistics(): void {
    this.loadLogisticsData();
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
        selectedTemplateId: this.selectedTemplateId,
        isLivePublish: this.isLivePublish,
        savedAt: timeStr,
        originalNotesBackup: this.originalNotesBackup,
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
      this.selectedTemplateId = draft.selectedTemplateId || '';
      this.isLivePublish = !!draft.isLivePublish;
      this.draftSavedAt = draft.savedAt || '';
      if (draft.originalNotesBackup) {
        this.originalNotesBackup = draft.originalNotesBackup;
        this.hasBackupNotes = true;
      }
      this.hasRestoredDraft = true;

      this.updatePriceTry();
      if (this.enableVariations && this.variationRows.length === 0) {
        this.rebuildVariationGrid();
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
    this.etsyApi.getSellerTaxonomy().subscribe({
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
          const match = this.categoriesList.find(c => c.path.toLowerCase() === this.selectedCategory.toLowerCase() || c.name.toLowerCase() === this.selectedCategory.toLowerCase());
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
      const selectedItem = this.categoriesList.find(c => c.id === this.selectedTaxonomyId);
      if (selectedItem && !baseList.some(c => c.id === this.selectedTaxonomyId)) {
        baseList.unshift(selectedItem);
      }
    }

    this.filteredCategories = baseList;
  }

  selectCategoryItem(cat: EtsyTaxonomyItemDto): void {
    this.selectedTaxonomyId = cat.id;
    this.selectedCategory = cat.path;
    this.selectedCategoryCombo = `${cat.id}|${cat.path}`;
    this.showToast(`📌 Kategori seçildi: #${cat.id} - ${cat.path}`);
  }

  loadShippingProfiles(): void {
    this.isLoadingShippingProfiles = true;
    this.etsyApi.getShippingProfiles().subscribe({
      next: (res) => {
        this.isLoadingShippingProfiles = false;
        const results = res?.results || (Array.isArray(res) ? res : []);
        if (results.length > 0) {
          this.shippingProfilesList = results.map((p: any) => ({
            id: p.shipping_profile_id,
            title: p.title || `Profil #${p.shipping_profile_id}`,
            minDays: p.min_processing_days,
            maxDays: p.max_processing_days
          }));
          if (!this.selectedShippingProfileId) {
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
    this.etsyApi.getReadinessStates().subscribe({
      next: (res) => {
        this.isLoadingReadinessStates = false;
        const results = res?.results || (Array.isArray(res) ? res : []);
        if (results.length > 0) {
          this.readinessStatesList = results.map((r: any) => ({
            id: r.readiness_state_id,
            title: r.title || `Hazırlık Durumu #${r.readiness_state_id}`,
            minDays: r.min_processing_time,
            maxDays: r.max_processing_time,
            state: r.readiness_state
          }));
          if (!this.selectedReadinessStateId) {
            this.selectedReadinessStateId = this.readinessStatesList[0].id;
          }
        }
      },
      error: () => {
        this.isLoadingReadinessStates = false;
      }
    });
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
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
    const name = prompt('Yeni şablon adını girin:', 'Özel Şablon ' + (this.templates.length + 1));
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
    this.showToast('✅ Şablon başarıyla kaydedildi!');
  }

  deleteCurrentTemplate(): void {
    if (this.templates.length === 0) {
      alert('Silinecek şablon yok.');
      return;
    }
    if (confirm('Seçili şablonu silmek istediğinize emin misiniz?')) {
      this.templates = this.templates.filter(t => t.id !== this.selectedTemplateId);
      this.selectedTemplateId = '';
      this.resetForm();
      this.showToast('🗑️ Şablon silindi.');
    }
  }

  // --- PRICING & LOGISTICS ---
  onPriceChange(): void {
    this.updatePriceTry();
    this.rebuildVariationGrid();
  }

  onPriceTryChange(): void {
    if (this.usdTryRate > 0) {
      this.priceUsd = Number((this.priceTry / this.usdTryRate).toFixed(2));
      this.rebuildVariationGrid();
    }
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

    // 4. Şablon & Klonlanan
    this.selectedTemplateId = '';
    this.clonedNotice = '';

    this.showToast('🔄 Tüm form ve girdi alanları temizlendi!');
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
    this.showToast('🧹 Etiketler 20 karaktere göre formatlandı.');
  }

  // --- AI SUGGESTIONS ---
  suggestAiTitle(): void {
    const input = this.title.trim() || this.description.trim();
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı veya açıklama alanına ürünle ilgili temel bilgileri yazın.');
      return;
    }
    this.isGeneratingTitle = true;
    this.listingAiService.suggestTitle(input, this.selectedCategory).subscribe({
      next: res => {
        this.isGeneratingTitle = false;
        this.title = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingTitle = false;
        this.showToast(err?.message || '❌ Canlı AI başlık önerisi oluşturulamadı.');
      }
    });
  }

  groundTaxonomyRecommendation(res: CategoryAiSuggestion): CategoryAiSuggestion {
    if (!this.categoriesList || this.categoriesList.length === 0) return res;

    // 1. Doğrudan ID eşleşmesi
    let match = this.categoriesList.find(c => c.id === res.taxonomyId);
    if (match) {
      res.taxonomyId = match.id;
      res.categoryPath = match.path;
    } else if (res.categoryPath) {
      // 2. Kategori yolu (path) tam veya yaprak adıyla eşleşme
      const cleanPath = res.categoryPath.toLowerCase().trim();
      const leafName = cleanPath.split('>').pop()?.trim() || cleanPath;

      match = this.categoriesList.find(c => c.path.toLowerCase() === cleanPath);
      if (!match) {
        match = this.categoriesList.find(c => c.name.toLowerCase() === leafName || c.path.toLowerCase().endsWith('> ' + leafName));
      }
    }

    // 3. Eğer AI bilinmeyen bir ID döndürdüyse, ürün başlığı ve açıklamasındaki anahtar kelimelerden tespit et
    if (!match) {
      const searchTerms = this.expandSearchTerms(`${this.title} ${this.description}`);
      for (const term of searchTerms) {
        match = this.categoriesList.find(c => c.name.toLowerCase() === term || c.path.toLowerCase().includes(term));
        if (match) break;
      }
    }

    if (match) {
      res.taxonomyId = match.id;
      res.categoryPath = match.path;
    }

    // Alternatifleri de resmi taksonomi listesiyle doğrula
    if (res.alternatives && res.alternatives.length > 0) {
      res.alternatives = res.alternatives.map(alt => {
        const altMatch = this.categoriesList.find(c => c.id === alt.taxonomyId) ||
                         this.categoriesList.find(c => c.path.toLowerCase() === alt.categoryPath.toLowerCase()) ||
                         this.categoriesList.find(c => c.name.toLowerCase() === alt.categoryPath.split('>').pop()?.trim().toLowerCase());
        if (altMatch) {
          return {
            taxonomyId: altMatch.id,
            categoryPath: altMatch.path,
            confidenceScore: alt.confidenceScore
          };
        }
        return alt;
      }).filter(alt => alt.taxonomyId > 0);
    }

    return res;
  }

  suggestAiCategory(): void {
    const title = this.title.trim();
    const desc = this.description.trim();
    const images = this.galleryImages.map(img => img.url).filter(u => !!u);

    if (!title && !desc && images.length === 0) {
      this.showToast('⚠️ Lütfen AI kategori analizi için bir başlık yazın, açıklama ekleyin veya orta panelden ürün görseli yükleyin.');
      return;
    }

    this.isGeneratingCategory = true;
    this.listingAiService.suggestCategoryMultimodal(title, desc, images).subscribe({
      next: res => {
        this.isGeneratingCategory = false;
        const grounded = this.groundTaxonomyRecommendation(res);

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
        this.showToast(grounded.message || `✨ AI Kategoriyi belirledi: #${grounded.taxonomyId} ${grounded.categoryPath}`);
      },
      error: (err) => {
        this.isGeneratingCategory = false;
        this.showToast(err?.message || '❌ Kategori önerisi oluşturulamadı.');
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
    this.showToast(`📌 Kategori güncellendi: #${alt.taxonomyId} ${alt.categoryPath}`);
  }

  suggestAiTags(): void {
    const input = this.title.trim() || this.description.trim();
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı veya açıklama alanına ürünle ilgili temel bilgileri yazın.');
      return;
    }
    this.isGeneratingTags = true;
    this.listingAiService.suggestTags(input, this.selectedCategory).subscribe({
      next: res => {
        this.isGeneratingTags = false;
        this.tags = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingTags = false;
        this.showToast(err?.message || '❌ Canlı AI etiket önerisi oluşturulamadı.');
      }
    });
  }

  // --- AI AÇIKLAMA MOTORU (ÖN BİLGİ & BOYUT/RENK KORUMA) ---

  restoreOriginalNotes(): void {
    if (!this.originalNotesBackup) return;
    this.description = this.originalNotesBackup;
    this.showToast('↩️ Orijinal ham notlarınız geri yüklendi!');
  }

  suggestAiDescription(): void {
    const descText = this.description.trim();
    const titleText = this.title.trim();

    if (!descText && !titleText) {
      this.showToast('⚠️ Lütfen önce ürün başlığı veya açıklama alanına bir ürün adı/özelliği yazın.');
      return;
    }

    // Kullanıcının yazdığı orijinal notları yedekle
    if (descText && !this.originalNotesBackup) {
      this.originalNotesBackup = descText;
      this.hasBackupNotes = true;
    }

    const variationsSummary = this.getVariationsSummary();

    const context: AiDescriptionContext = {
      productTitle: titleText || descText,
      preliminaryNotes: descText,
      materials: this.materials.trim(),
      category: this.selectedCategory,
      tags: this.tags,
      variationsSummary
    };

    this.isGeneratingDesc = true;
    this.listingAiService.suggestDescription(context).subscribe({
      next: res => {
        this.isGeneratingDesc = false;
        this.description = res.value;
        this.showToast(res.message);
      },
      error: (err) => {
        this.isGeneratingDesc = false;
        this.showToast(err?.message || '❌ Canlı AI açıklama önerisi oluşturulamadı.');
      }
    });
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
      this.showToast('⚠️ Lütfen önce sol panelde ürün başlığı kutusuna birkaç kelime girin (Örn: El Yapımı Kadın Çantası).');
      return;
    }

    if (descInput && !this.originalNotesBackup) {
      this.originalNotesBackup = descInput;
      this.hasBackupNotes = true;
    }

    this.isGeneratingAll = true;
    this.listingAiService.generateCompleteListing(input, this.materials).subscribe({
      next: res => {
        this.isGeneratingAll = false;
        this.title = res.title;
        this.selectedCategory = res.category;
        this.tags = res.tags;
        this.description = res.description;
        if (res.materials) this.materials = res.materials;

        const images = this.galleryImages.map(img => img.url).filter(u => !!u);
        if (images.length > 0) {
          this.suggestAiCategory();
        }

        this.showToast(res.summaryMessage);
      },
      error: (err) => {
        this.isGeneratingAll = false;
        this.showToast(err?.message || '❌ Canlı AI listeleme önerisi oluşturulamadı.');
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
        alert('Etsy en fazla 10 ürün görseline izin verir.');
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
    this.showToast('⭐ #1 Kapak görseli güncellendi!');
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
      alert('Maksimum 10 görsel sınırına ulaşıldı.');
      return;
    }
    this.galleryImages.push({
      id: 'studio_' + Date.now(),
      url: url,
      isCover: this.galleryImages.length === 0,
      name: 'Stüdyo Görseli'
    });
    this.updateCoverStatus();
    this.showToast('🎨 Görsel stüdyodan galeriye eklendi!');
  }

  // --- AI IMAGE STUDIO ACTIONS ---
  applyPromptPreset(presetName: string, styleKeyword: string): void {
    const prod = this.title ? this.title.slice(0, 50).trim() : 'Etsy handcrafted product';
    this.aiPrompt = `Professional commercial product photography of ${prod}, ${styleKeyword}, high detail, studio lighting, 8k render, etsy showcase`;
  }

  fillPromptFromTitle(): void {
    if (!this.title.trim()) {
      alert('Önce sol panelden bir ürün başlığı girin.');
      return;
    }
    this.aiPrompt = `Professional commercial product photography of ${this.title.trim()}, isolated on clean studio lighting, high detail, 8k render, etsy showcase`;
  }

  generateAiImage(): void {
    if (!this.aiPrompt.trim()) {
      this.fillPromptFromTitle();
    }
    this.isGeneratingAiImage = false;
    this.showToast('AI görsel üretimi bu modülde bağlı değil. AI Studio üzerinden üretim yapın.');
  }

  addGeneratedToGallery(): void {
    if (!this.lastGeneratedImage) return;
    if (this.galleryImages.length >= 10) {
      alert('Etsy en fazla 10 ürün görseline izin verir.');
      return;
    }
    this.galleryImages.push({
      id: 'ai_gen_' + Date.now(),
      url: this.lastGeneratedImage,
      isCover: this.galleryImages.length === 0,
      name: 'AI Stüdyo Görseli'
    });
    this.updateCoverStatus();
    this.lastGeneratedImage = null;
    this.showToast('➕ Üretilen görsel 10\'lu galeriye eklendi!');
  }

  // --- VARIATIONS LOGIC ---
  onVariationsToggled(): void {
    this.rebuildVariationGrid();
  }

  rebuildVariationGrid(): void {
    if (!this.enableVariations) {
      this.variationRows = [];
      return;
    }

    const v1 = this.varValues1.split(',').map(s => s.trim()).filter(s => s.length > 0);
    if (v1.length === 0) {
      this.variationRows = [];
      return;
    }

    const combinations: string[] = [];
    if (this.enableVar2) {
      const v2 = this.varValues2.split(',').map(s => s.trim()).filter(s => s.length > 0);
      if (v2.length > 0) {
        for (const val1 of v1) {
          for (const val2 of v2) {
            combinations.push(`${val1} / ${val2}`);
          }
        }
      } else {
        combinations.push(...v1);
      }
    } else {
      combinations.push(...v1);
    }

    // Preserve existing prices if any
    const existing = new Map(this.variationRows.map(r => [r.key, r]));
    this.variationRows = combinations.map(key => {
      if (existing.has(key)) {
        return existing.get(key)!;
      }
      return {
        key: key,
        price: this.priceUsd || 0,
        quantity: Math.max(1, Math.floor((this.quantity || 10) / (combinations.length || 1))),
        active: true
      };
    });
  }

  syncBasePriceToVariations(): void {
    const p = this.priceUsd || 0;
    this.variationRows.forEach(row => {
      row.price = p;
    });
    this.showToast(`⚡ Tüm varyasyon fiyatları $${p.toFixed(2)} olarak eşitlendi!`);
  }

  suggestAiVariations(): void {
    const title = this.title.trim() || 'Handmade Product';
    this.isGeneratingVariations = true;
    const s = this.aiService.settings();
    const cmd = {
      listingId: 0,
      title,
      tags: this.tags,
      description: this.description,
      materials: this.materials ? this.materials.split(',').map(m => m.trim()) : [],
      priceUsd: this.priceUsd || 25,
      category: this.selectedCategory,
      currentLqsScore: 80,
      provider: s.provider,
      apiKey: (s as any)[`${s.provider.toLowerCase()}ApiKey`] || s.geminiApiKey || s.deepSeekApiKey || '',
      model: this.aiService.activeModelName()
    };

    this.etsyApi.improveListingLqs(cmd).subscribe({
      next: (res) => {
        this.isGeneratingVariations = false;
        if (res.suggestedVariations && res.suggestedVariations.length > 0) {
          this.enableVariations = true;
          this.varType1 = res.suggestedVariations[0].name;
          this.varValues1 = res.suggestedVariations[0].values.join(', ');
          if (res.suggestedVariations.length > 1) {
            this.enableVar2 = true;
            this.varType2 = res.suggestedVariations[1].name;
            this.varValues2 = res.suggestedVariations[1].values.join(', ');
          }
          this.rebuildVariationGrid();
          this.showToast(`✨ ${res.provider} ile varyasyonlar oluşturuldu (${res.suggestedVariations.map(v => v.name).join(' & ')})`);
        } else {
          this.showToast('Varyasyon önerisi bulunamadı.');
        }
      },
      error: () => {
        this.isGeneratingVariations = false;
        this.showToast('❌ AI varyasyon önerisi alınamadı.');
      }
    });
  }

  // --- CHECKLIST GETTERS ---
  get isTitleReady(): boolean {
    return this.title.trim().length > 0 && this.title.length <= 140;
  }
  get isPriceReady(): boolean {
    return (this.priceUsd ?? 0) > 0 && (this.quantity ?? 0) > 0;
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
    state: string;
    uploadedImages: number;
    message: string;
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
  }

  activateListingNow(listingId: number | undefined): void {
    if (!listingId) return;
    this.isActivatingListing = true;
    this.etsyApi.updateListingState(listingId, 'active').subscribe({
      next: (res) => {
        this.isActivatingListing = false;
        if (this.createdListingResult) {
          this.createdListingResult.state = 'active';
        }
        this.showToast(`🎉 İlan başarıyla CANLI satışa açıldı! (ID: #${listingId})`);
      },
      error: (err) => {
        this.isActivatingListing = false;
        const msg = err?.error?.error || err?.message || 'Etsy API aktivasyon hatası.';
        alert(`❌ Canlıya alma hatası:\n${msg}`);
      }
    });
  }

  publishListingToEtsy(): void {
    if (!this.isTitleReady) {
      alert('Lütfen geçerli bir ürün başlığı giriniz (1-140 karakter).');
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

    // Prepare variations
    const variationsPayload = this.enableVariations && this.variationRows.length > 0
      ? this.variationRows.map(r => ({
          key: r.key,
          price: r.price,
          quantity: r.quantity,
          active: r.active
        }))
      : undefined;

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

    // Prepare variation groups
    const variationGroupsPayload = this.enableVariations && this.variationRows.length > 0
      ? [
          {
            name: this.varType1,
            values: this.varValues1.split(',').map(s => s.trim()).filter(Boolean)
          },
          ...(this.enableVar2 ? [{
            name: this.varType2,
            values: this.varValues2.split(',').map(s => s.trim()).filter(Boolean)
          }] : [])
        ]
      : undefined;

    const payload = {
      title: this.title.trim(),
      description: this.description.trim() || 'Handmade custom design artisan product.',
      price: this.priceUsd || 39.90,
      quantity: this.quantity || 15,
      taxonomyId: this.selectedTaxonomyId > 0 ? this.selectedTaxonomyId : 1042,
      shippingProfileId,
      readinessStateId,
      isDigital: this.listingType === 'digital',
      tags: validTags,
      materials: this.sanitizeMaterialsForPayload(this.materials),
      state: (this.isLivePublish ? 'active' : 'draft') as 'draft' | 'active',
      images: imagesPayload,
      variations: variationsPayload,
      variationGroups: variationGroupsPayload
    };

    this.etsyApi.createListing(payload).subscribe({
      next: (res) => {
        this.isSavingDraft = false;
        if (res && res.success) {
          this.clearDraftFromStorage();
          this.hasRestoredDraft = false;
          this.createdListingResult = res;
          this.isPublishSuccessModalOpen = true;
          const varCount = (res as any).variationsCount || 0;
          const varText = varCount > 0 ? ` + ${varCount} varyasyon` : '';

          if ((res as any).variationError) {
            alert(`⚠️ İlan #${res.listingId} Etsy'ye aktarıldı, ANCAK varyasyonlar eklenemedi:\n${(res as any).variationError}`);
          }

          this.showToast(`🎉 Başarılı! İlan ${res.state === 'active' ? 'CANLI' : 'TASLAK'} olarak Etsy'ye aktarıldı! (ID: #${res.listingId}${varText})`);
        } else {
          this.showToast('İlan aktarıldı ancak sunucu yanıtı doğrulanamadı.');
        }
      },
      error: (err) => {
        this.isSavingDraft = false;
        const errDetail = err?.error?.error || err?.message || 'Etsy API bağlantı hatası.';
        alert(`❌ Etsy İlan Gönderim Hatası:\n${errDetail}`);
      }
    });
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
          .replace(/[\/\\|_+()[\]{}*•"':;.,!?]/g, ' ')
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
