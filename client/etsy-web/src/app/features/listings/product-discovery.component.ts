import { Component, OnInit, inject, signal, computed, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService, MarketListingItemDto, CreateListingPayload } from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { buildEtsyDescriptionTemplate } from '../../core/utils/etsy-description-template.util';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

export interface DiscoveryItem {
  id: number;
  listingRank: number;
  title: string;
  priceUsd: number;
  currency: string;
  shopName: string;
  shopSales: number;
  shopUrl: string;
  listingUrl: string;
  favorites: number;
  views: number;
  seoScore: number;
  marketScore: number;
  tags: string[];
  materials: string[];
  imageUrl: string;
  imageUrls: string[];
  description: string;
  reviewCount: number;
  reviewAverage: number;
  quantity: number;
  taxonomyId?: number;
  isTargetListing?: boolean;
  isOpportunityListing?: boolean;
  isBestseller?: boolean;
}

@Component({
  selector: 'app-product-discovery',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('listings')],
  templateUrl: './product-discovery.component.html',
  styleUrls: ['./product-discovery.component.css']
})
export class ProductDiscoveryComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  router = inject(Router);

  // Search & Link Inputs
  keywordInput = '3d printed fidget toy, desk sculpture';
  limitInput = 30;
  listingLinkInput = '';

  // State flags
  isLoading = false;
  isImporting = false;
  isGeneratingFullAi = false;
  isGeneratingDesc = false;
  isSavingDraft = false;
  statusText = '';
  toastMessage = '';

  // Main Data
  items: DiscoveryItem[] = [];
  selectedItem: DiscoveryItem | null = null;
  currentImageIndex = 0;

  // Active Tab
  activeTab: 'seo' | 'tags' | 'shipping' | 'prompts' | 'lqs' = 'seo';

  // Draft Editor State
  draftTitle = '';
  draftDescription = '';
  draftTags: string[] = [];
  newTagInput = '';
  draftMaterials = '';
  draftPriceUsd = 19.90;
  draftQuantity = 20;
  draftShippingProfileId: number | null = null;
  draftIsDigital = false;
  draftWhoMade = 'i_did';
  draftWhenMade = 'made_to_order';
  draftTaxonomyId = 0;

  // Shipping profiles from Etsy API
  shippingProfiles: any[] = [];

  // AI & LQS Audit Data
  targetLqsScore = 85;
  targetLqsGrade = 'A';
  lqsHighlights: string[] = [];
  imagePrompts: string[] = [];

  // Modal states
  showSuccessModal = false;
  createdListingId: number | null = null;
  createdListingUrl: string | null = null;
  zoomedImageUrl: string | null = null;

  activeAiModel = computed(() => this.aiService.activeModelName());

  getActiveApiKey(): string {
    const s = this.aiService.settings();
    switch (s.provider) {
      case 'OpenAI': return s.openAiApiKey || '';
      case 'Claude': return s.claudeApiKey || '';
      case 'DeepSeek': return s.deepSeekApiKey || '';
      case 'Grok': return s.grokApiKey || '';
      default: return s.geminiApiKey || '';
    }
  }

  ngOnInit(): void {
    this.loadShippingProfiles();
  }

  loadShippingProfiles(): void {
    this.etsyApi.getShippingProfiles().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        const results = res?.results || (Array.isArray(res) ? res : []);
        if (results.length > 0) {
          this.shippingProfiles = results
            .map((p: any) => {
              const id = Number(p.shippingProfileId ?? p.shipping_profile_id ?? p.id ?? 0);
              return {
                ...p,
                shippingProfileId: id,
                shipping_profile_id: id,
                title: p.title || this.transloco.translate('listings.discovery.profileFallback', { id })
              };
            })
            .filter((p: any) => p.shippingProfileId > 0);
          if (this.shippingProfiles.length > 0 && !this.draftShippingProfileId) {
            this.draftShippingProfileId = this.shippingProfiles[0].shippingProfileId;
          }
        }
      },
      error: () => {}
    });
  }

  // --- ETSY'DE ARA ---
  searchEtsy(): void {
    const kw = (this.keywordInput || '').trim();
    if (!kw) {
      this.showToast(this.transloco.translate('listings.discovery.toastEnterKeyword'));
      return;
    }

    this.isLoading = true;
    this.statusText = this.transloco.translate('listings.discovery.statusSearching', { keyword: kw });

    this.etsyApi.searchMarket(kw, this.limitInput, 'market_score').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res && res.listings && res.listings.length > 0) {
          this.items = res.listings.map(l => this.mapToDiscoveryItem(l));
          this.statusText = this.transloco.translate('listings.discovery.statusSearchFound', { keyword: kw, count: this.items.length });
          this.selectItem(this.items[0]);
          this.showToast(this.transloco.translate('listings.discovery.toastSearchLoaded', { count: this.items.length }));
        } else {
          this.items = [];
          this.selectedItem = null;
          this.statusText = this.transloco.translate('listings.discovery.statusSearchEmpty', { keyword: kw });
          this.showToast(this.transloco.translate('listings.discovery.toastSearchEmpty'));
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.statusText = this.transloco.translate('listings.discovery.statusSearchError', { error: err?.message || this.transloco.translate('listings.discovery.defaultSearchError') });
        this.showToast(this.transloco.translate('listings.discovery.toastSearchError'));
      }
    });
  }

  // --- LİNKTEN AL (Target Product Link / ID) ---
  importListingLink(): void {
    const link = (this.listingLinkInput || '').trim();
    if (!link) {
      this.showToast(this.transloco.translate('listings.discovery.toastEnterLink'));
      return;
    }

    this.isImporting = true;
    this.statusText = this.transloco.translate('listings.discovery.statusImportingLink');

    this.etsyApi.searchMarket(link, this.limitInput, 'market_score').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isImporting = false;
        if (res && res.listings && res.listings.length > 0) {
          this.items = res.listings.map(l => this.mapToDiscoveryItem(l));
          const target = this.items.find(i => i.isTargetListing) ?? this.items[0];
          this.selectItem(target);
          if (res.targetListingId) {
            this.statusText = this.transloco.translate('listings.discovery.statusLinkImported', {
              id: res.targetListingId,
              competitorCount: Math.max(0, this.items.length - 1)
            });
          }
          this.showToast(this.transloco.translate('listings.discovery.toastLinkImported'));
        } else {
          this.statusText = this.transloco.translate('listings.discovery.statusLinkNotFound');
          this.showToast(this.transloco.translate('listings.discovery.toastLinkNotFound'));
        }
      },
      error: (err) => {
        this.isImporting = false;
        this.statusText = this.transloco.translate('listings.discovery.statusSearchError', { error: err?.message || this.transloco.translate('listings.discovery.defaultImportError') });
        this.showToast(this.transloco.translate('listings.discovery.toastImportError'));
      }
    });
  }

  // --- SEÇİLİ ÜRÜNÜ DÜZENLEYİCİYE AKTAR ---
  selectItem(item: DiscoveryItem): void {
    this.selectedItem = item;
    this.currentImageIndex = 0;

    // Fill Draft Fields from Item
    this.draftTitle = item.title;
    this.draftDescription = item.description || '';
    this.draftTags = [...(item.tags || [])].slice(0, 13);
    this.draftMaterials = (item.materials || []).join(', ');
    this.draftPriceUsd = item.priceUsd > 0 ? item.priceUsd : 19.90;
    this.draftQuantity = item.quantity > 0 ? item.quantity : 20;
    this.draftTaxonomyId = item.taxonomyId || 0;

    // Generate basic initial prompts
    this.generateLocalImagePrompts();
    this.recalculateLqsChecks();
  }

  // --- 🚀 1-TIK FULL AI LISTING ---
  generateFullAutoPilotListing(): void {
    if (!this.selectedItem && !this.draftTitle) {
      this.showToast(this.transloco.translate('listings.discovery.toastSelectProductFirst'));
      return;
    }

    this.isGeneratingFullAi = true;
    this.statusText = this.transloco.translate('listings.discovery.statusFullAiRunning');

    const cmd = {
      listingId: this.selectedItem?.id || 0,
      title: this.draftTitle || this.selectedItem?.title || '',
      tags: this.draftTags.length > 0 ? this.draftTags : (this.selectedItem?.tags || []),
      description: this.draftDescription || this.selectedItem?.description || '',
      materials: this.draftMaterials ? this.draftMaterials.split(',').map(m => m.trim()) : (this.selectedItem?.materials || []),
      priceUsd: this.draftPriceUsd,
      currentLqsScore: this.selectedItem?.seoScore || 65,
      provider: this.aiService.settings().provider,
      apiKey: this.getActiveApiKey(),
      model: this.aiService.activeModelName()
    };

    this.etsyApi.improveListingLqs(cmd).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isGeneratingFullAi = false;
        if (res) {
          if (res.optimizedTitle) this.draftTitle = res.optimizedTitle;
          if (res.optimizedTags && res.optimizedTags.length > 0) this.draftTags = res.optimizedTags.slice(0, 13);
          if (res.optimizedDescription) this.draftDescription = res.optimizedDescription;
          if (res.suggestedMaterials && res.suggestedMaterials.length > 0) {
            this.draftMaterials = res.suggestedMaterials.join(', ');
          }
          this.targetLqsScore = res.targetLqsScore || 100;
          this.targetLqsGrade = res.targetLqsGrade || 'A+';
          this.lqsHighlights = res.improvementHighlights || [];
          this.generateLocalImagePrompts();
          this.recalculateLqsChecks();
          this.activeTab = 'seo';
          this.statusText = this.transloco.translate('listings.discovery.statusFullAiSuccess');
          this.showToast(this.transloco.translate('listings.discovery.toastFullAiSuccess'));
        }
      },
      error: () => {
        this.isGeneratingFullAi = false;
        // Fallback: Yerel akıllı şablon ve optimizasyon motorunu devreye sok
        this.applyLocalOptimizationFallback();
        this.statusText = this.transloco.translate('listings.discovery.statusFullAiFallback');
        this.showToast(this.transloco.translate('listings.discovery.toastFullAiFallback'));
      }
    });
  }

  // --- ŞABLONLA PARAGRAFLA ---
  applyDescriptionTemplate(): void {
    this.draftDescription = buildEtsyDescriptionTemplate({
      title: this.draftTitle || this.selectedItem?.title,
      tags: this.draftTags,
      materials: this.draftMaterials,
      listingType: 'physical'
    });
    this.recalculateLqsChecks();
    this.showToast(this.transloco.translate('listings.discovery.toastTemplateApplied'));
  }

  // --- AI AÇIKLAMA YENİLE ---
  generateDescriptionWithAi(): void {
    this.isGeneratingDesc = true;
    this.statusText = this.transloco.translate('listings.discovery.statusAiDescRunning');

    const cmd = {
      listingId: this.selectedItem?.id || 0,
      title: this.draftTitle,
      tags: this.draftTags,
      description: this.draftDescription,
      materials: this.draftMaterials ? this.draftMaterials.split(',').map(m => m.trim()) : [],
      priceUsd: this.draftPriceUsd,
      currentLqsScore: 70,
      provider: this.aiService.settings().provider,
      apiKey: this.getActiveApiKey(),
      model: this.aiService.activeModelName()
    };

    this.etsyApi.improveListingLqs(cmd).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isGeneratingDesc = false;
        if (res && res.optimizedDescription) {
          this.draftDescription = res.optimizedDescription;
          this.showToast(this.transloco.translate('listings.discovery.toastAiDescSuccess'));
        } else {
          this.applyDescriptionTemplate();
        }
      },
      error: () => {
        this.isGeneratingDesc = false;
        this.applyDescriptionTemplate();
      }
    });
  }

  // --- RAKİP ETİKETLERİNİ KOPYALA ---
  copyCompetitorTags(): void {
    if (!this.selectedItem || !this.selectedItem.tags || this.selectedItem.tags.length === 0) {
      this.showToast(this.transloco.translate('listings.discovery.toastNoCompetitorTags'));
      return;
    }

    const cleaned = this.selectedItem.tags
      .map(t => t.trim().toLowerCase())
      .filter(t => t.length > 0 && t.length <= 20)
      .slice(0, 13);

    this.draftTags = cleaned;
    this.recalculateLqsChecks();
    this.showToast(this.transloco.translate('listings.discovery.toastCopiedCompetitorTags', { count: cleaned.length }));
  }

  // --- ETİKET YÖNETİMİ ---
  addTag(): void {
    const val = (this.newTagInput || '').trim().toLowerCase();
    if (!val) return;
    if (val.length > 20) {
      this.showToast(this.transloco.translate('listings.discovery.toastTagMaxLength'));
      return;
    }
    if (this.draftTags.length >= 13) {
      this.showToast(this.transloco.translate('listings.discovery.toastTagMaxCount'));
      return;
    }
    if (this.draftTags.includes(val)) {
      this.showToast(this.transloco.translate('listings.discovery.toastTagDuplicate'));
      return;
    }

    this.draftTags.push(val);
    this.newTagInput = '';
    this.recalculateLqsChecks();
  }

  removeTag(index: number): void {
    this.draftTags.splice(index, 1);
    this.recalculateLqsChecks();
  }

  // --- 💾 ETSY TASLAK EKLE (Direct Draft to Shop) ---
  saveDraftToEtsy(): void {
    if (!this.draftTitle || this.draftTitle.trim().length < 5) {
      this.showToast(this.transloco.translate('listings.discovery.toastMinTitle'));
      return;
    }
    if (this.draftPriceUsd <= 0) {
      this.showToast(this.transloco.translate('listings.discovery.toastInvalidPrice'));
      return;
    }

    this.isSavingDraft = true;
    this.statusText = this.transloco.translate('listings.discovery.statusSavingDraft');

    // Toplanan görseller
    const imagePayloads = (this.selectedItem?.imageUrls || [])
      .slice(0, 10)
      .map((url, rank) => ({ url, rank: rank + 1 }));

    const payload: CreateListingPayload = {
      title: this.draftTitle.trim().slice(0, 140),
      description: this.draftDescription.trim(),
      price: this.draftPriceUsd,
      quantity: this.draftQuantity,
      taxonomyId: this.draftTaxonomyId > 0 ? this.draftTaxonomyId : 1, // Fallback craft supplies
      shippingProfileId: this.draftShippingProfileId,
      isDigital: this.draftIsDigital,
      tags: this.draftTags,
      materials: this.draftMaterials ? this.draftMaterials.split(',').map(m => m.trim()) : [],
      whoMade: this.draftWhoMade,
      whenMade: this.draftWhenMade,
      state: 'draft',
      images: imagePayloads
    };

    this.etsyApi.createListing(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isSavingDraft = false;
        if (res && res.listingId) {
          this.createdListingId = res.listingId;
          this.createdListingUrl = res.url || `https://www.etsy.com/your/shops/me/listing-editor/edit/${res.listingId}`;
          this.showSuccessModal = true;
          this.statusText = this.transloco.translate('listings.discovery.statusDraftSaved', { id: res.listingId });
          this.showToast(this.transloco.translate('listings.discovery.toastDraftSaved', { id: res.listingId }));
        } else {
          this.showToast(this.transloco.translate('listings.discovery.toastDraftNoId'));
        }
      },
      error: (err) => {
        this.isSavingDraft = false;
        const msg = err?.error?.message || err?.message || this.transloco.translate('listings.discovery.defaultDraftError');
        this.statusText = this.transloco.translate('listings.discovery.statusDraftError', { error: msg });
        this.showToast(this.transloco.translate('listings.discovery.toastDraftError', { error: msg }));
      }
    });
  }

  // --- ⚡ HIZLI ÜRÜN EKLE'YE AKTAR (Bridge to Fast Creator) ---
  transferToFastCreator(): void {
    if (!this.draftTitle) {
      this.showToast(this.transloco.translate('listings.discovery.toastNoTitleToTransfer'));
      return;
    }

    this.aiService.clonedListing.set({
      id: this.selectedItem?.id || 0,
      title: this.draftTitle,
      priceUsd: this.draftPriceUsd,
      tags: [...this.draftTags],
      description: this.draftDescription,
      taxonomyId: this.draftTaxonomyId,
      materials: this.draftMaterials ? this.draftMaterials.split(',').map(m => m.trim()) : [],
      imageUrl: this.selectedItem?.imageUrls[0] || this.selectedItem?.imageUrl || '',
      imageUrls: this.selectedItem?.imageUrls || []
    });

    this.showToast(this.transloco.translate('listings.discovery.toastTransferring'));
    this.router.navigate(['/listings/fast-creator']);
  }

  // --- IMAGE SLIDER & ZOOM ---
  prevImage(): void {
    const urls = this.selectedItem?.imageUrls || [];
    if (urls.length <= 1) return;
    this.currentImageIndex = (this.currentImageIndex - 1 + urls.length) % urls.length;
  }

  nextImage(): void {
    const urls = this.selectedItem?.imageUrls || [];
    if (urls.length <= 1) return;
    this.currentImageIndex = (this.currentImageIndex + 1) % urls.length;
  }

  openZoom(url: string): void {
    this.zoomedImageUrl = url;
  }

  closeZoom(): void {
    this.zoomedImageUrl = null;
  }

  copyToClipboard(text: string, label?: string): void {
    if (!text) return;
    const resolvedLabel = label || this.transloco.translate('listings.discovery.defaultCopyLabel');
    navigator.clipboard.writeText(text).then(() => {
      this.showToast(this.transloco.translate('listings.discovery.toastCopiedClipboard', { label: resolvedLabel }));
    });
  }

  private mapToDiscoveryItem(item: MarketListingItemDto): DiscoveryItem {
    return {
      id: item.id,
      listingRank: item.listingRank,
      title: item.title,
      priceUsd: item.priceUsd,
      currency: item.currency || 'USD',
      shopName: item.shopName,
      shopSales: item.shopSales,
      shopUrl: item.shopUrl,
      listingUrl: item.listingUrl,
      favorites: item.favorites,
      views: item.views,
      seoScore: item.seoScore || 70,
      marketScore: item.marketScore || 70,
      tags: item.tags || [],
      materials: item.materials || [],
      imageUrl: item.imageUrl || '',
      imageUrls: item.imageUrls && item.imageUrls.length > 0 ? item.imageUrls : (item.imageUrl ? [item.imageUrl] : []),
      description: item.description || '',
      reviewCount: item.reviewCount || 0,
      reviewAverage: item.reviewAverage || 5.0,
      quantity: item.quantity || 10,
      taxonomyId: item.taxonomyId,
      isTargetListing: item.isTargetListing,
      isOpportunityListing: item.isOpportunityListing,
      isBestseller: item.isBestseller
    };
  }

  private generateLocalImagePrompts(): void {
    const title = this.draftTitle || this.selectedItem?.title || 'Artisan product';
    const primary = this.draftTags[0] || 'artisan craft';
    const mat = this.draftMaterials || 'matte finish resin, precision craft';

    this.imagePrompts = [
      `Studio product photograph of a ${primary}, ${title}, crafted from ${mat}, clean neutral beige background, soft directional studio lighting, commercial eCommerce photography, 8k resolution, Hasselblad style --ar 4:3 --v 6.0`,
      `Cozy lifestyle setting featuring ${title}, resting on a modern wooden desk with subtle soft plant shadows, warm ambient light, depth of field, premium Etsy product visual --ar 4:3 --style raw`,
      `Close-up macro detail of ${title}, highlighting the intricate texture, seamless edges, premium ${mat}, crisp sharp focus, cinematic lighting --ar 1:1`
    ];
  }

  private recalculateLqsChecks(): void {
    const titleLen = (this.draftTitle || '').length;
    const tagCount = this.draftTags.length;
    const descLen = (this.draftDescription || '').length;
    const hasImages = (this.selectedItem?.imageUrls || []).length > 0;

    let score = 50;
    if (titleLen >= 80 && titleLen <= 140) score += 20;
    else if (titleLen >= 50) score += 10;

    if (tagCount === 13) score += 20;
    else if (tagCount >= 10) score += 12;

    if (descLen >= 500) score += 10;
    if (hasImages) score += 10;

    this.targetLqsScore = Math.min(100, score);
    this.targetLqsGrade = this.targetLqsScore >= 95 ? 'A+' : (this.targetLqsScore >= 80 ? 'A' : 'B');
  }

  private applyLocalOptimizationFallback(): void {
    const title = this.selectedItem?.title || this.draftTitle;
    if (title) {
      // 125-140 char title
      const clean = title.replace(/[^\w\s-]/gi, ' ').replace(/\s+/g, ' ').trim();
      const tagsPart = this.draftTags.slice(0, 3).join(' | ');
      const candidate = `${clean} | ${tagsPart}`;
      this.draftTitle = candidate.slice(0, 138);
    }

    if (this.draftTags.length < 13 && this.selectedItem?.tags) {
      this.copyCompetitorTags();
    }

    this.applyDescriptionTemplate();
  }

  showToast(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) {
        this.toastMessage = '';
      }
    }, 4500);
  }
}
