import { Component, OnInit, inject, ViewChild, ElementRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, ActivatedRoute } from '@angular/router';
import { EtsyApiService, EtsyTaxonomyItemDto } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { EtsyListingAiService, CategoryAiSuggestion, TaxonomyCandidateItem } from '../../core/services/etsy-listing-ai.service';
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
export class FastCreatorComponent implements OnInit {
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

  // Publish Status & Checklist
  isLivePublish = false;
  isPreviewModalOpen = false;
  isGeneratingAi = false;
  isSavingDraft = false;
  successMessage = '';
  clonedNotice = '';

  ngOnInit(): void {
    const cloned = this.aiService.clonedListing();
    if (cloned) {
      this.selectedTemplateId = '';
      this.title = cloned.title;
      this.priceUsd = cloned.priceUsd;
      this.tags = [...cloned.tags];
      if (cloned.description) this.description = cloned.description;
      if (cloned.category) this.selectedCategory = cloned.category;
      if (cloned.imageUrl) {
        this.galleryImages = [{
          id: 'cloned-1',
          url: cloned.imageUrl,
          isCover: true,
          name: 'Klonlanan Kapak Görseli'
        }];
      }
      this.clonedNotice = `Klonlanan Ürün #${cloned.id}: "${cloned.title.slice(0, 48)}..." (${cloned.shopName}) - Fiyat: $${cloned.priceUsd}`;
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

  loadCategories(): void {
    this.isLoadingCategories = true;
    this.etsyApi.getSellerTaxonomy().subscribe({
      next: (res) => {
        this.isLoadingCategories = false;
        this.categoriesList = res?.results || [];
        this.filterCategories();
        if (this.selectedTaxonomyId > 0 && !this.selectedCategory) {
          const match = this.categoriesList.find(c => c.id === this.selectedTaxonomyId);
          if (match) {
            this.selectedCategory = match.path;
            this.selectedCategoryCombo = `${match.id}|${match.path}`;
          }
        }
      },
      error: () => {
        this.isLoadingCategories = false;
      }
    });
  }

  filterCategories(): void {
    const q = (this.categorySearchTerm || '').trim().toLowerCase();
    if (!q) {
      this.filteredCategories = this.categoriesList.slice(0, 80);
    } else {
      this.filteredCategories = this.categoriesList
        .filter(c => c.name.toLowerCase().includes(q) || c.path.toLowerCase().includes(q))
        .slice(0, 150);
    }
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
        this.selectedTaxonomyId = res.taxonomyId;
        this.selectedCategory = res.categoryPath;
        this.categoryAiResult = res;
        this.categoryAlternatives = res.alternatives || [];
        if (this.categoriesList.length > 0) {
          const match = this.categoriesList.find(c => c.id === res.taxonomyId);
          if (match) {
            this.selectedCategory = match.path;
          }
        }
        this.selectedCategoryCombo = `${this.selectedTaxonomyId}|${this.selectedCategory}`;
        this.showToast(res.message);
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
      const path = parts[1];
      if (id > 0) this.selectedTaxonomyId = id;
      if (path) this.selectedCategory = path;
    } else {
      this.selectedCategory = val;
    }
  }

  selectAlternativeCategory(alt: TaxonomyCandidateItem): void {
    this.selectedTaxonomyId = alt.taxonomyId;
    this.selectedCategory = alt.categoryPath;
    this.selectedCategoryCombo = `${alt.taxonomyId}|${alt.categoryPath}`;
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

  suggestAiDescription(): void {
    const input = this.description.trim() || this.title.trim();
    if (!input) {
      this.showToast('⚠️ Lütfen önce ürün başlığı veya açıklama alanına bir ürün adı yazın.');
      return;
    }
    this.isGeneratingDesc = true;
    this.listingAiService.suggestDescription(input, this.materials).subscribe({
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

  generateWithAi(): void {
    const input = this.title.trim() || this.description.trim();
    if (!input) {
      this.showToast('⚠️ Lütfen önce sol panelde ürün başlığı kutusuna birkaç kelime girin (Örn: El Yapımı Kadın Çantası).');
      return;
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

        // If user already has images in gallery, also refine category using vision
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

  openPreviewModal(): void {
    this.isPreviewModalOpen = true;
  }

  closePreviewModal(): void {
    this.isPreviewModalOpen = false;
  }

  closeSuccessModal(): void {
    this.isPublishSuccessModalOpen = false;
  }

  publishListingToEtsy(): void {
    if (!this.isTitleReady) {
      alert('Lütfen geçerli bir ürün başlığı giriniz (1-140 karakter).');
      return;
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
          this.createdListingResult = res;
          this.isPublishSuccessModalOpen = true;
          const varText = (res as any).variationsCount > 0 ? ` + ${(res as any).variationsCount} varyasyon` : '';
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
