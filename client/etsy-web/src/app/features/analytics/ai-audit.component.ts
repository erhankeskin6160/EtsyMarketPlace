import { Component, OnInit, inject, signal, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import {
  EtsyApiService,
  ShopListingItemDto,
  SavedListingAuditDto,
  OptimizeListingResponseDto,
  ListingImageDetailDto,
  SyncListingImageItemDto
} from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

type FilterType = 'all' | 'lowSeo' | 'missingTags' | 'hasAudit' | 'zeroFav';

export interface EditableListingImage {
  listingImageId: number | null;
  url: string;
  fullUrl: string;
  dataUrl?: string | null;
  fileName?: string | null;
  isNew?: boolean;
}

@Component({
  selector: 'app-ai-audit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './ai-audit.component.html',
  styleUrls: ['./ai-audit.component.css']
})
export class AiAuditComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private router = inject(Router);
  etsyApi = inject(EtsyApiService);
  aiSettings = inject(AiSettingsService);
  readonly Math = Math;

  // Listing Image Management State
  listingImages: EditableListingImage[] = [];
  private originalImagesSignature = '';
  activeImageIndex = 0;
  isLoadingImages = false;
  isSyncingImages = false;
  isImageDragOver = false;
  lightboxImageUrl: string | null = null;
  readonly maxListingImages = 10;

  // Data & State
  listings: ShopListingItemDto[] = [];
  filteredListings: ShopListingItemDto[] = [];
  selectedListing: ShopListingItemDto | null = null;
  isLoading = false;
  isAuditingSelected = false;
  isBatchAuditing = false;
  batchProgress = { current: 0, total: 0 };
  isUpdatingEtsy = false;
  toastMessage: string | null = null;
  toastType: 'success' | 'error' | 'info' = 'info';

  // Filters & Search
  activeFilter: FilterType = 'all';
  searchQuery = '';
  fetchLimit = 25;

  // Pagination
  currentPage = 1;
  pageSize = 15;

  // AI Editor / After Fields
  afterTitle = '';
  titleSuggestions: string[] = [];
  afterTags: string[] = [];
  afterMaterials: string[] = [];
  newMaterialInput = '';
  afterDescription = '';
  aiCritique = '';
  riskWarnings: string[] = [];
  checklist: string[] = [];
  aiScoreAfter: number | null = null;
  aiModelUsed = '';
  aiProviderUsed = '';
  newTagInput = '';
  private batchCancelRequested = false;

  // AI Settings Dialog
  showSettingsModal = false;
  get selectedAiModel(): string {
    return this.aiSettings.activeBadgeTextClean();
  }

  getActiveApiKey(): string {
    const s = this.aiSettings.settings();
    switch (s.provider) {
      case 'Gemini': return (s.geminiApiKey || '').trim();
      case 'DeepSeek': return (s.deepSeekApiKey || '').trim();
      case 'OpenAI': return (s.openAiApiKey || '').trim();
      case 'Claude': return (s.claudeApiKey || '').trim();
      case 'Grok': return (s.grokApiKey || '').trim();
      default: return '';
    }
  }

  openGlobalAiModelHub(): void {
    this.showSettingsModal = false;
    this.aiSettings.openAiSettingsModal();
  }
  focusKeywords = '';
  targetBuyerPersona = 'Hediye arayanlar, düğün ve özel gün alıcıları, estetik ev dekorasyonu sevenler';
  aiTone = 'Profesyonel & İkna Edici';

  // KPIs
  get totalCount(): number {
    return this.listings.length;
  }

  get averageSeoScore(): number {
    if (this.listings.length === 0) return 0;
    const sum = this.listings.reduce((acc, l) => acc + (l.seoScore || 0), 0);
    return Math.round(sum / this.listings.length);
  }

  get criticalCount(): number {
    return this.listings.filter(l => (l.seoScore || 0) < 60).length;
  }

  get optimizedCount(): number {
    return this.listings.filter(l => l.hasSavedAudit).length;
  }

  get lowSeoCount(): number {
    return this.criticalCount;
  }

  get missingTagCount(): number {
    return this.listings.filter(l => (l.tags?.length || 0) < 13).length;
  }

  get zeroFavCount(): number {
    return this.listings.filter(l => ((l.numFavorers != null ? l.numFavorers : l.favorites) || 0) === 0).length;
  }

  // Pagination Computed
  get totalPages(): number {
    return Math.max(1, Math.ceil(this.filteredListings.length / this.pageSize));
  }

  get paginatedListings(): ShopListingItemDto[] {
    const startIndex = (this.currentPage - 1) * this.pageSize;
    return this.filteredListings.slice(startIndex, startIndex + this.pageSize);
  }

  get startIndexDisplay(): number {
    if (this.filteredListings.length === 0) return 0;
    return (this.currentPage - 1) * this.pageSize + 1;
  }

  get endIndexDisplay(): number {
    return Math.min(this.currentPage * this.pageSize, this.filteredListings.length);
  }

  ngOnInit(): void {
    this.loadListings();
  }

  loadListings(): void {
    this.isLoading = true;
    this.showToast('Canlı Etsy aktif ilanları yükleniyor...', 'info');

    this.etsyApi.getShopActiveListings(undefined, this.fetchLimit).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        this.isLoading = false;
        this.listings = data || [];
        this.applyFilter();

        if (this.listings.length > 0) {
          this.selectListing(this.listings[0]);
          this.showToast(`${this.listings.length} adet canlı Etsy ilanı başarıyla çekildi.`, 'success');
        } else {
          this.selectedListing = null;
          this.showToast('Aktif ilan bulunamadı veya mağaza boş.', 'info');
        }
      },
      error: (err) => {
        this.isLoading = false;
        console.error('Listings fetch error:', err);
        const errDetail = err?.error?.error || err?.message || 'Bağlantı hatası';
        this.showToast(`İlanlar yüklenemedi: ${errDetail}`, 'error');
      }
    });
  }

  selectListing(listing: ShopListingItemDto): void {
    this.selectedListing = listing;
    this.newTagInput = '';
    this.loadListingImages(listing);

    if (listing.savedAudit || (listing.hasSavedAudit && listing.resultJson)) {
      this.populateFromAudit(listing.savedAudit || {
        resultJson: listing.resultJson,
        optimizedSeoScore: listing.aiScore,
        status: listing.status,
        title: listing.title
      });
    } else {
      // Initialize with current listing content as baseline
      this.afterTitle = listing.title;
      this.titleSuggestions = [];
      this.afterTags = [...(listing.tags || [])];
      this.afterMaterials = [...(listing.materials || [])];
      this.afterDescription = listing.description;
      this.aiCritique = listing.riskWarnings && listing.riskWarnings.length > 0
        ? 'Mevcut listing algoritmik olarak taranmış ve potansiyel marka/telif riskleri tespit edilmiştir. "AI ile Puanla" butonuna basarak yapay zeka ile tam SEO optimizasyonu ve temiz başlık/etiket seti üretebilirsiniz.'
        : '';
      this.riskWarnings = [...(listing.riskWarnings || [])];
      this.checklist = [];
      this.aiScoreAfter = null;
      this.aiModelUsed = this.aiSettings.activeModelName();
      this.aiProviderUsed = `${this.aiSettings.activeProvider()} (Canlı API)`;
    }
  }

  populateFromAudit(audit: any): void {
    if (!audit) return;

    let parsedResult: any = null;
    const rawJson = audit.resultJson || audit.ResultJson;
    if (rawJson) {
      try {
        parsedResult = typeof rawJson === 'string' ? JSON.parse(rawJson) : rawJson;
      } catch (e) {
        console.warn('Audit resultJson parse error:', e);
      }
    }

    const titleSuggestions: string[] = parsedResult?.TitleSuggestions || parsedResult?.titleSuggestions || [];
    const tagSuggestions: string[] = parsedResult?.TagSuggestions || parsedResult?.tagSuggestions || [];
    const materialSuggestions: string[] = parsedResult?.MaterialSuggestions || parsedResult?.materialSuggestions || [];
    const descDraft: string = parsedResult?.DescriptionDraft || parsedResult?.descriptionDraft || '';
    const risks: string[] = parsedResult?.RiskWarnings || parsedResult?.riskWarnings || [];
    const actions: string[] = parsedResult?.ActionChecklist || parsedResult?.actionChecklist || [];
    const critique: string = parsedResult?.SeoCritique || parsedResult?.seoCritique || '';
    const scoreAfter: number = audit.seoScoreAfter || audit.optimizedSeoScore || parsedResult?.OptimizedSeoScore || 0;

    this.titleSuggestions = titleSuggestions;
    this.afterTitle = audit.optimizedTitle || titleSuggestions[0] || audit.title || '';
    this.afterMaterials = materialSuggestions.length > 0
      ? [...materialSuggestions]
      : [...(this.selectedListing?.materials || [])];

    if (audit.optimizedTagsJson) {
      try {
        this.afterTags = JSON.parse(audit.optimizedTagsJson);
      } catch {
        this.afterTags = [];
      }
    } else if (audit.optimizedTags && Array.isArray(audit.optimizedTags)) {
      this.afterTags = audit.optimizedTags;
    } else if (tagSuggestions.length > 0) {
      this.afterTags = tagSuggestions;
    } else {
      this.afterTags = [];
    }

    this.afterDescription = audit.optimizedDescription || descDraft || '';
    this.aiScoreAfter = scoreAfter > 0 ? scoreAfter : null;
    this.aiModelUsed = audit.aiModel || audit.model || parsedResult?.ExecutedModel || this.selectedAiModel;
    this.aiProviderUsed = audit.provider || parsedResult?.ExecutedProvider || `${this.aiSettings.activeProvider()} (Canlı API)`;

    if (audit.riskWarningsJson) {
      try {
        this.riskWarnings = JSON.parse(audit.riskWarningsJson);
      } catch {
        this.riskWarnings = [];
      }
    } else if (risks.length > 0) {
      this.riskWarnings = risks;
    } else {
      this.riskWarnings = [];
    }

    if (audit.checklistJson) {
      try {
        this.checklist = JSON.parse(audit.checklistJson);
      } catch {
        this.checklist = [];
      }
    } else if (actions.length > 0) {
      this.checklist = actions;
    } else {
      this.checklist = [];
    }

    this.aiCritique = critique || 'Önceki yapay zeka denetim kaydından başarıyla yüklendi.';
  }

  setFilter(filter: FilterType): void {
    this.activeFilter = filter;
    this.currentPage = 1;
    this.applyFilter();
  }

  onSearchChange(): void {
    this.currentPage = 1;
    this.applyFilter();
  }

  applyFilter(): void {
    let result = [...this.listings];

    // Filter Chips
    if (this.activeFilter === 'lowSeo') {
      result = result.filter(l => (l.seoScore || 0) < 60);
    } else if (this.activeFilter === 'missingTags') {
      result = result.filter(l => (l.tags?.length || 0) < 13);
    } else if (this.activeFilter === 'hasAudit') {
      result = result.filter(l => l.hasSavedAudit);
    } else if (this.activeFilter === 'zeroFav') {
      result = result.filter(l => ((l.numFavorers != null ? l.numFavorers : l.favorites) || 0) === 0);
    }

    // Search query
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.trim().toLowerCase();
      result = result.filter(l =>
        l.title.toLowerCase().includes(q) ||
        l.listingId.toString().includes(q) ||
        (l.tags && l.tags.some(t => t.toLowerCase().includes(q)))
      );
    }

    this.filteredListings = result;

    // Maintain selection or select first
    if (this.selectedListing && !this.filteredListings.some(l => l.listingId === this.selectedListing?.listingId)) {
      if (this.filteredListings.length > 0) {
        this.selectListing(this.filteredListings[0]);
      } else {
        this.selectedListing = null;
      }
    }
  }

  // ── AI Optimization Actions ───────────────────────────────────────────────
  runAiOptimizationForSelected(): void {
    if (!this.selectedListing) return;

    const settings = this.aiSettings.settings();
    const isStrict = settings.strictNeverOffline;
    const apiKey = this.getActiveApiKey();

    if (isStrict && !apiKey && settings.provider !== 'Offline') {
      this.showToast(`⚠️ Canlı AI Zorunlu aktif fakat ${settings.provider} API anahtarı boş. Lütfen üst menüdeki AI Model Merkezinden anahtarınızı kaydedin.`, 'error');
      return;
    }

    this.isAuditingSelected = true;
    this.showToast(`${this.selectedListing.listingId} nolu ilan ${this.aiSettings.activeBadgeTextClean()} ile optimize ediliyor...`, 'info');

    const payload = {
      shopId: this.etsyApi.activeShopId(),
      title: this.selectedListing.title,
      tags: this.selectedListing.tags,
      description: this.selectedListing.description,
      focusKeywords: this.focusKeywords,
      targetBuyerPersona: this.targetBuyerPersona,
      tone: this.aiTone,
      model: this.aiSettings.activeModelName(),
      apiKey: apiKey,
      provider: settings.provider
    };

    this.etsyApi.optimizeListingWithAi(this.selectedListing.listingId, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: OptimizeListingResponseDto) => {
        this.isAuditingSelected = false;
        if (res.success) {
          this.afterTitle = res.optimizedTitle;
          this.titleSuggestions = res.titleSuggestions && res.titleSuggestions.length > 0
            ? res.titleSuggestions
            : (res.optimizedTitle ? [res.optimizedTitle] : []);
          this.afterTags = res.optimizedTags || [];
          this.afterMaterials = res.materialSuggestions && res.materialSuggestions.length > 0
            ? [...res.materialSuggestions]
            : [...(this.selectedListing?.materials || [])];
          this.afterDescription = res.optimizedDescription;
          this.aiCritique = res.critique || 'Optimizasyon başarıyla tamamlandı.';
          this.riskWarnings = res.riskWarnings || [];
          this.checklist = res.checklist || [];
          this.aiScoreAfter = res.seoScoreAfter;
          this.aiModelUsed = res.aiModel || res.model || this.selectedAiModel;
          this.aiProviderUsed = res.provider || `${this.aiSettings.activeProvider()} (Canlı API)`;

          // Update in-memory item
          if (this.selectedListing) {
            this.selectedListing.aiScore = res.seoScoreAfter;
            this.selectedListing.hasSavedAudit = true;
            this.selectedListing.status = res.riskWarnings && res.riskWarnings.length > 0 ? '⚠️ AI: Risk Var' : '✨ AI: Hazır';
            this.selectedListing.riskWarnings = res.riskWarnings || [];
            this.selectedListing.savedAudit = {
              id: 0,
              shopId: this.etsyApi.activeShopId(),
              listingId: this.selectedListing.listingId.toString(),
              originalTitle: this.selectedListing.title,
              originalTagsJson: JSON.stringify(this.selectedListing.tags || []),
              originalDescription: this.selectedListing.description,
              optimizedTitle: res.optimizedTitle,
              optimizedTagsJson: JSON.stringify(res.optimizedTags || []),
              optimizedDescription: res.optimizedDescription,
              aiModel: res.aiModel,
              seoScoreBefore: res.seoScoreBefore,
              seoScoreAfter: res.seoScoreAfter,
              riskWarningsJson: JSON.stringify(res.riskWarnings || []),
              checklistJson: JSON.stringify(res.checklist || []),
              isAppliedToEtsy: false,
              createdAt: new Date().toISOString()
            };
          }

          this.showToast(`Listing ${res.listingId} [${this.aiProviderUsed}] ile başarıyla optimize edildi! Skor: ${res.seoScoreAfter}/100`, 'success');
        } else {
          this.showToast(`AI optimizasyon tamamlanamadı: ${res.message || 'Bilinmeyen hata'}`, 'error');
        }
      },
      error: (err) => {
        this.isAuditingSelected = false;
        console.error('AI optimize error:', err);
        const msg = err?.error?.error || err?.message || 'AI servisi yanıt vermedi';
        this.showToast(`AI Optimizasyon Hatası: ${msg}`, 'error');
      }
    });
  }

  runBatchAiAudit(): void {
    if (this.filteredListings.length === 0) return;
    if (this.isBatchAuditing) return;

    const targets = this.filteredListings.filter(l => !l.hasSavedAudit || (l.seoScore || 0) < 70);
    if (targets.length === 0) {
      this.showToast('Filtredeki tüm ilanlar zaten optimize edilmiş durumda.', 'info');
      return;
    }

    const settings = this.aiSettings.settings();
    const isStrict = settings.strictNeverOffline;
    const apiKey = this.getActiveApiKey();

    if (isStrict && !apiKey && settings.provider !== 'Offline') {
      this.showToast(`⚠️ Canlı AI Zorunlu aktif fakat ${settings.provider} API anahtarı boş. Lütfen üst menüdeki AI Model Merkezinden anahtarınızı kaydedin.`, 'error');
      return;
    }

    if (!confirm(`${targets.length} adet ilan sırayla ${this.aiSettings.activeBadgeTextClean()} ile denetlenecek ve optimize edilecek. Devam edilsin mi?`)) {
      return;
    }

    this.batchCancelRequested = false;
    this.isBatchAuditing = true;
    this.batchProgress = { current: 0, total: targets.length };

    const processNext = (index: number) => {
      if (this.batchCancelRequested) {
        this.isBatchAuditing = false;
        this.showToast(`Toplu denetim durduruldu (${this.batchProgress.current}/${targets.length}).`, 'info');
        return;
      }
      if (index >= targets.length) {
        this.isBatchAuditing = false;
        this.showToast(`${targets.length} ilan başarıyla toplu AI denetiminden geçirildi!`, 'success');
        return;
      }

      const item = targets[index];
      this.batchProgress.current = index + 1;

      const payload = {
        shopId: this.etsyApi.activeShopId(),
        title: item.title,
        tags: item.tags,
        description: item.description,
        focusKeywords: this.focusKeywords,
        targetBuyerPersona: this.targetBuyerPersona,
        tone: this.aiTone,
        model: this.aiSettings.activeModelName(),
        apiKey: apiKey,
        provider: settings.provider
      };

      this.etsyApi.optimizeListingWithAi(item.listingId, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res) => {
          if (res.success) {
            item.aiScore = res.seoScoreAfter;
            item.hasSavedAudit = true;
            item.status = res.riskWarnings && res.riskWarnings.length > 0 ? '⚠️ AI: Risk Var' : '✨ AI: Hazır';
            item.riskWarnings = res.riskWarnings || [];
            if (this.selectedListing?.listingId === item.listingId) {
              this.populateFromAudit({
                id: 0,
                shopId: this.etsyApi.activeShopId(),
                listingId: item.listingId.toString(),
                originalTitle: item.title,
                originalTagsJson: JSON.stringify(item.tags || []),
                originalDescription: item.description,
                optimizedTitle: res.optimizedTitle,
                optimizedTagsJson: JSON.stringify(res.optimizedTags || []),
                optimizedDescription: res.optimizedDescription,
                aiModel: res.aiModel,
                seoScoreBefore: res.seoScoreBefore,
                seoScoreAfter: res.seoScoreAfter,
                riskWarningsJson: JSON.stringify(res.riskWarnings || []),
                checklistJson: JSON.stringify(res.checklist || []),
                isAppliedToEtsy: false,
                createdAt: new Date().toISOString()
              });
            }
          }
          setTimeout(() => processNext(index + 1), 800);
        },
        error: (err) => {
          console.warn(`Batch item error on ${item.listingId}:`, err);
          setTimeout(() => processNext(index + 1), 800);
        }
      });
    };

    processNext(0);
  }

  cancelBatchAudit(): void {
    this.batchCancelRequested = true;
  }

  // ── Etsy Direct Update ───────────────────────────────────────────────────
  updateListingOnEtsy(): void {
    if (!this.selectedListing) return;

    // Validation matching Etsy API constraints
    if (!this.afterTitle || this.afterTitle.trim().length === 0) {
      this.showToast('Başlık boş olamaz!', 'error');
      return;
    }

    if (this.afterTitle.length > 140) {
      this.showToast(`Başlık 140 karakterden uzun olamaz! (Mevcut: ${this.afterTitle.length})`, 'error');
      return;
    }

    if ((this.afterTitle.match(/&/g) || []).length > 1) {
      this.showToast("Etsy API başlıkta birden fazla '&' karakterini reddeder! Lütfen 'and' veya virgül kullanın.", 'error');
      return;
    }

    if (this.afterTags.length > 13) {
      this.showToast(`Etsy en fazla 13 etikete izin verir! (Mevcut: ${this.afterTags.length})`, 'error');
      return;
    }

    const invalidTag = this.afterTags.find(t => t.length > 20);
    if (invalidTag) {
      this.showToast(`Etiketler en fazla 20 karakter olabilir: "${invalidTag}" (${invalidTag.length} karakter)`, 'error');
      return;
    }

    const tagWarning = this.afterTags.length < 13
      ? `\n⚠️ UYARI: Etiket sayısı 13'ün altında (${this.afterTags.length}/13). Etsy SEO için 13 etiket önerilir.\n`
      : '';

    const confirmMsg = `DİKKAT: Listing #${this.selectedListing.listingId} doğrudan canlı Etsy mağazanıza gönderilecektir.\n${tagWarning}\nYeni Başlık: ${this.afterTitle.substring(0, 60)}...\nEtiket Sayısı: ${this.afterTags.length}/13\nMalzeme Sayısı: ${this.afterMaterials.length}\n\nOnaylıyor musunuz?`;
    if (!confirm(confirmMsg)) {
      return;
    }

    this.isUpdatingEtsy = true;
    this.showToast('Değişiklikler canlı Etsy mağazasına yükleniyor...', 'info');

    const payload = {
      title: this.afterTitle.trim(),
      description: this.afterDescription.trim(),
      tags: this.afterTags,
      materials: this.afterMaterials
    };

    this.etsyApi.updateEtsyListing(this.selectedListing.listingId, payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isUpdatingEtsy = false;
        this.showToast(`🎉 İlan #${this.selectedListing?.listingId} başarıyla Etsy'de güncellendi!`, 'success');

        if (this.selectedListing) {
          this.selectedListing.title = this.afterTitle;
          this.selectedListing.tags = [...this.afterTags];
          this.selectedListing.materials = [...this.afterMaterials];
          this.selectedListing.description = this.afterDescription;
          this.selectedListing.seoScore = this.aiScoreAfter || 95;
          if (this.selectedListing.savedAudit) {
            this.selectedListing.savedAudit.isAppliedToEtsy = true;
            this.selectedListing.savedAudit.appliedAt = new Date().toISOString();
          }
        }
      },
      error: (err) => {
        this.isUpdatingEtsy = false;
        console.error('Update Etsy listing error:', err);
        const msg = err?.error?.error || err?.message || 'Etsy API güncelleme isteğini reddetti';
        this.showToast(`Etsy Güncelleme Hatası: ${msg}`, 'error');
      }
    });
  }

  // ── Helper Actions ────────────────────────────────────────────────────────
  selectTitleSuggestion(title: string): void {
    this.afterTitle = title;
    this.showToast('Alternatif başlık seçildi.', 'info');
  }

  addMaterial(): void {
    const mat = this.newMaterialInput.trim();
    if (!mat) return;
    if (this.afterMaterials.length >= 13) {
      this.showToast('En fazla 13 malzeme ekleyebilirsiniz!', 'error');
      return;
    }
    if (mat.length > 45) {
      this.showToast(`Malzeme 45 karakteri aşamaz! (${mat.length} karakter)`, 'error');
      return;
    }
    if (!this.afterMaterials.some(m => m.toLowerCase() === mat.toLowerCase())) {
      this.afterMaterials.push(mat);
    }
    this.newMaterialInput = '';
  }

  removeMaterial(index: number): void {
    this.afterMaterials.splice(index, 1);
  }

  resetToOriginal(): void {
    if (!this.selectedListing) return;
    this.afterTitle = this.selectedListing.title;
    this.afterTags = [...(this.selectedListing.tags || [])];
    this.afterMaterials = [...(this.selectedListing.materials || [])];
    this.afterDescription = this.selectedListing.description;
    this.showToast('Editör alanları mevcut Etsy ilanına sıfırlandı.', 'info');
  }

  formatDescriptionTemplate(): void {
    if (!this.selectedListing) return;

    const baseDesc = (this.afterDescription || this.selectedListing.description || '').trim();

    const formatted = `✦ HIGHLIGHTS ✦
${baseDesc}

✦ DIMENSIONS & MATERIALS ✦
- Crafted with durable, high-quality materials for long-lasting display and daily use.
- Please check the listing photos and variations for exact sizing and color options.

✦ PERSONALIZATION & CUSTOM ORDERS ✦
- Looking for a custom size, color, or gift note? Leave a note at checkout or message us before ordering.

✦ SHIPPING & PACKAGING ✦
- Carefully packed with protective materials and dispatched within 1-3 business days.
- All orders ship with international tracking for safe delivery.

✦ CARE INSTRUCTIONS ✦
- Wipe gently with a soft, dry or slightly damp cloth. Avoid harsh chemicals.`;

    this.afterDescription = formatted;
    this.showToast('İngilizce profesyonel Etsy paragraf şablonu uygulandı.', 'success');
  }

  copySuggestionToClipboard(): void {
    if (!this.afterTitle) {
      this.showToast('Kopyalanacak optimize içerik bulunamadı.', 'info');
      return;
    }

    const textToCopy = `=== BAŞLIK (TITLE) ===\n${this.afterTitle}\n\n=== ETİKETLER (TAGS - ${this.afterTags.length}/13) ===\n${this.afterTags.join(', ')}\n\n=== AÇIKLAMA (DESCRIPTION) ===\n${this.afterDescription}`;

    navigator.clipboard.writeText(textToCopy).then(() => {
      this.showToast('📋 AI Başlık, Etiketler ve Açıklama panoya kopyalandı!', 'success');
    }).catch(() => {
      this.showToast('Panoya kopyalama başarısız oldu.', 'error');
    });
  }

  addTag(): void {
    const tag = this.newTagInput.trim().toLowerCase();
    if (!tag) return;

    if (this.afterTags.length >= 13) {
      this.showToast('En fazla 13 etiket ekleyebilirsiniz!', 'error');
      return;
    }

    if (tag.length > 20) {
      this.showToast(`Etiket 20 karakteri aşamaz! ("${tag}" - ${tag.length} karakter)`, 'error');
      return;
    }

    if (this.afterTags.includes(tag)) {
      this.showToast('Bu etiket zaten ekli!', 'info');
      return;
    }

    this.afterTags.push(tag);
    this.newTagInput = '';
  }

  removeTag(index: number): void {
    this.afterTags.splice(index, 1);
  }

  // ── Pagination Actions ────────────────────────────────────────────────────
  firstPage(): void {
    this.currentPage = 1;
  }

  prevPage(): void {
    if (this.currentPage > 1) this.currentPage--;
  }

  nextPage(): void {
    if (this.currentPage < this.totalPages) this.currentPage++;
  }

  lastPage(): void {
    this.currentPage = this.totalPages;
  }

  // ── Listing Image Management ──────────────────────────────────────────────
  get activePreviewUrl(): string {
    const active = this.listingImages[this.activeImageIndex] || this.listingImages[0];
    return active?.url || this.selectedListing?.primaryImageUrl || this.selectedListing?.thumbnail || '';
  }

  get activeFullPreviewUrl(): string {
    const active = this.listingImages[this.activeImageIndex] || this.listingImages[0];
    return active?.fullUrl || active?.url || this.activePreviewUrl;
  }

  get pendingImageChangesCount(): number {
    if (!this.originalImagesSignature) return 0;
    const current = this.buildImagesSignature(this.listingImages);
    if (current === this.originalImagesSignature) return 0;

    const origIds: (number | null)[] = JSON.parse(this.originalImagesSignature);
    let changes = 0;
    const newCount = this.listingImages.filter(i => i.isNew).length;
    changes += newCount;

    const currentIds = new Set(this.listingImages.filter(i => i.listingImageId).map(i => i.listingImageId));
    for (const id of origIds) {
      if (id && !currentIds.has(id)) changes++;
    }

    // Check rank changes for remaining existing images
    this.listingImages.forEach((img, idx) => {
      if (!img.isNew && img.listingImageId && origIds[idx] !== img.listingImageId) {
        changes++;
      }
    });

    return Math.max(changes, 1);
  }

  private buildImagesSignature(imgs: EditableListingImage[]): string {
    return JSON.stringify(imgs.map(i => (i.isNew ? `new:${i.fileName || i.url.slice(0, 32)}` : i.listingImageId)));
  }

  loadListingImages(listing: ShopListingItemDto): void {
    this.isLoadingImages = true;
    this.activeImageIndex = 0;
    const fallbackUrls = (listing.images && listing.images.length > 0)
      ? listing.images
      : (listing.primaryImageUrl ? [listing.primaryImageUrl] : []);

    this.listingImages = fallbackUrls.map(u => ({
      listingImageId: null,
      url: u,
      fullUrl: u,
      isNew: false
    }));
    this.originalImagesSignature = '';

    this.etsyApi.getListingImages(listing.listingId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (imgs: ListingImageDetailDto[]) => {
        if (this.selectedListing?.listingId !== listing.listingId) return;
        this.isLoadingImages = false;
        if (imgs && imgs.length > 0) {
          this.listingImages = imgs.map(i => ({
            listingImageId: i.listingImageId,
            url: i.url570xN || i.urlFullxfull || i.url75x75,
            fullUrl: i.urlFullxfull || i.url570xN,
            isNew: false
          }));
          this.originalImagesSignature = this.buildImagesSignature(this.listingImages);
        } else {
          this.originalImagesSignature = this.buildImagesSignature(this.listingImages);
        }
      },
      error: () => {
        this.isLoadingImages = false;
        this.originalImagesSignature = this.buildImagesSignature(this.listingImages);
      }
    });
  }

  selectImageIndex(index: number): void {
    if (index >= 0 && index < this.listingImages.length) {
      this.activeImageIndex = index;
    }
  }

  moveImage(index: number, delta: number, event?: Event): void {
    event?.stopPropagation();
    const target = index + delta;
    if (target < 0 || target >= this.listingImages.length) return;
    const copy = [...this.listingImages];
    const [moved] = copy.splice(index, 1);
    copy.splice(target, 0, moved);
    this.listingImages = copy;
    if (this.activeImageIndex === index) {
      this.activeImageIndex = target;
    } else if (this.activeImageIndex === target) {
      this.activeImageIndex = index;
    }
  }

  setAsCover(index: number, event?: Event): void {
    event?.stopPropagation();
    if (index <= 0 || index >= this.listingImages.length) return;
    const copy = [...this.listingImages];
    const [moved] = copy.splice(index, 1);
    copy.unshift(moved);
    this.listingImages = copy;
    this.activeImageIndex = 0;
  }

  removeImage(index: number, event?: Event): void {
    event?.stopPropagation();
    if (this.listingImages.length <= 1) {
      this.showToast('Etsy ilanında en az 1 görsel bulunmalıdır.', 'error');
      return;
    }
    this.listingImages = this.listingImages.filter((_, i) => i !== index);
    if (this.activeImageIndex >= this.listingImages.length) {
      this.activeImageIndex = Math.max(0, this.listingImages.length - 1);
    }
  }

  onImageFilesSelected(event: Event): void {
    const input = event.target as HTMLInputElement;
    if (input.files && input.files.length > 0) {
      this.processImageFiles(Array.from(input.files));
      input.value = '';
    }
  }

  onImageDragOver(event: DragEvent): void {
    event.preventDefault();
    this.isImageDragOver = true;
  }

  onImageDragLeave(event: DragEvent): void {
    event.preventDefault();
    this.isImageDragOver = false;
  }

  onImageDrop(event: DragEvent): void {
    event.preventDefault();
    this.isImageDragOver = false;
    if (event.dataTransfer?.files && event.dataTransfer.files.length > 0) {
      this.processImageFiles(Array.from(event.dataTransfer.files));
    }
  }

  private processImageFiles(files: File[]): void {
    const remaining = this.maxListingImages - this.listingImages.length;
    if (remaining <= 0) {
      this.showToast(`En fazla ${this.maxListingImages} görsel ekleyebilirsiniz.`, 'error');
      return;
    }

    const validFiles = files
      .filter(f => f.type.startsWith('image/'))
      .slice(0, remaining);

    if (validFiles.length === 0) {
      this.showToast('Lütfen geçerli bir görsel dosyası (JPG, PNG, WEBP) seçin.', 'error');
      return;
    }

    for (const file of validFiles) {
      const reader = new FileReader();
      reader.onload = () => {
        const dataUrl = reader.result as string;
        if (!dataUrl) return;
        this.listingImages = [
          ...this.listingImages,
          {
            listingImageId: null,
            url: dataUrl,
            fullUrl: dataUrl,
            dataUrl,
            fileName: file.name,
            isNew: true
          }
        ];
        this.activeImageIndex = this.listingImages.length - 1;
      };
      reader.readAsDataURL(file);
    }
  }

  syncImagesToEtsy(): void {
    if (!this.selectedListing || this.isSyncingImages) return;
    if (this.listingImages.length === 0) {
      this.showToast('Etsy ilanında en az 1 görsel bulunmalıdır.', 'error');
      return;
    }

    const items: SyncListingImageItemDto[] = this.listingImages.map((img, idx) => ({
      listingImageId: img.isNew ? null : img.listingImageId,
      dataUrl: img.isNew ? img.dataUrl : null,
      fileName: img.isNew ? img.fileName : null,
      rank: idx + 1
    }));

    this.isSyncingImages = true;
    const targetListing = this.selectedListing;
    this.showToast(`#${targetListing.listingId} görselleri Anti-Ban EXIF/pHash temizliği ile Etsy'ye yükleniyor...`, 'info');

    this.etsyApi.syncListingImages(targetListing.listingId, items).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isSyncingImages = false;
        if (res?.images && res.images.length > 0) {
          this.listingImages = res.images.map(i => ({
            listingImageId: i.listingImageId,
            url: i.url570xN || i.urlFullxfull || i.url75x75,
            fullUrl: i.urlFullxfull || i.url570xN,
            isNew: false
          }));
          this.originalImagesSignature = this.buildImagesSignature(this.listingImages);
          this.activeImageIndex = 0;

          const newCover = this.listingImages[0]?.url;
          if (newCover) {
            targetListing.primaryImageUrl = newCover;
            targetListing.thumbnail = newCover;
            targetListing.images = this.listingImages.map(i => i.url);
          }
        }
        this.showToast(
          `✅ Görseller Etsy'de güncellendi! (${res.uploadedCount} yüklendi, ${res.reorderedCount} sıralandı, ${res.deletedCount} silindi)`,
          'success'
        );
      },
      error: (err) => {
        this.isSyncingImages = false;
        const msg = err?.error?.message || err?.error?.error || err?.message || 'Görseller güncellenemedi.';
        this.showToast(`❌ Görsel güncelleme hatası: ${msg}`, 'error');
      }
    });
  }

  openImageInAiStudio(): void {
    if (!this.selectedListing) return;
    const url = this.activeFullPreviewUrl;
    if (!url || url.startsWith('data:')) {
      this.showToast('AI Stüdyoya aktarmak için önce görseli Etsy\'ye yükleyin veya mevcut bir Etsy görseli seçin.', 'info');
      return;
    }
    this.router.navigate(['/ai-studio'], {
      queryParams: {
        imageUrl: url,
        title: this.selectedListing.title,
        listingId: this.selectedListing.listingId
      }
    });
  }

  openLightbox(url: string, event?: Event): void {
    event?.stopPropagation();
    if (url) this.lightboxImageUrl = url;
  }

  closeLightbox(): void {
    this.lightboxImageUrl = null;
  }

  // ── Image Error Fallback ──────────────────────────────────────────────────
  onImgError(event: Event): void {
    const target = event.target as HTMLElement;
    if (target) {
      target.style.display = 'none';
      if (target.parentElement) {
        const placeholder = document.createElement('div');
        placeholder.className = 'thumb-placeholder';
        placeholder.textContent = '🖼️';
        target.parentElement.appendChild(placeholder);
      }
    }
  }

  // ── Toast Helper ──────────────────────────────────────────────────────────
  showToast(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.toastMessage = message;
    this.toastType = type;
    setTimeout(() => {
      if (this.toastMessage === message) {
        this.toastMessage = null;
      }
    }, 4500);
  }
}
