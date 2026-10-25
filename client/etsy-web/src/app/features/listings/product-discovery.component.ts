import { Component, OnInit, inject, signal, computed } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService, MarketListingItemDto, CreateListingPayload } from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';

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
  imports: [CommonModule, FormsModule],
  templateUrl: './product-discovery.component.html',
  styleUrls: ['./product-discovery.component.css']
})
export class ProductDiscoveryComponent implements OnInit {
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
  statusText = 'Aramaya hazır — Etsy ürün linki yapıştırın veya anahtar kelime ile arayın';
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
    this.etsyApi.getShippingProfiles().subscribe({
      next: (res) => {
        if (res && res.results && Array.isArray(res.results)) {
          this.shippingProfiles = res.results;
          if (this.shippingProfiles.length > 0 && !this.draftShippingProfileId) {
            this.draftShippingProfileId = this.shippingProfiles[0].shipping_profile_id;
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
      this.showToast('⚠️ Lütfen aranacak bir kelime veya niş girin.');
      return;
    }

    this.isLoading = true;
    this.statusText = `"${kw}" için Etsy pazarında canlı ürünler taranıyor...`;

    this.etsyApi.searchMarket(kw, this.limitInput, 'market_score').subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res && res.listings && res.listings.length > 0) {
          this.items = res.listings.map(l => this.mapToDiscoveryItem(l));
          this.statusText = `"${kw}" için ${this.items.length} canlı Etsy ilanı listelendi.`;
          this.selectItem(this.items[0]);
          this.showToast(`✅ ${this.items.length} rakip ürün yüklendi.`);
        } else {
          this.items = [];
          this.selectedItem = null;
          this.statusText = `"${kw}" için sonuç bulunamadı.`;
          this.showToast('ℹ️ Arama sonucunda ilan bulunamadı.');
        }
      },
      error: (err) => {
        this.isLoading = false;
        this.statusText = `⚠️ Hata: ${err?.message || 'Etsy araması başarısız'}`;
        this.showToast('🚨 Etsy API arama hatası oluştu.');
      }
    });
  }

  // --- LİNKTEN AL (Target Product Link / ID) ---
  importListingLink(): void {
    const link = (this.listingLinkInput || '').trim();
    if (!link) {
      this.showToast('⚠️ Lütfen geçerli bir Etsy listing linki veya ürün numarası girin.');
      return;
    }

    this.isImporting = true;
    this.statusText = '🔗 Etsy linki çözümleniyor, ürün detayları ve niş pazar verileri taranıyor...';

    this.etsyApi.searchMarket(link, this.limitInput, 'market_score').subscribe({
      next: (res) => {
        this.isImporting = false;
        if (res && res.listings && res.listings.length > 0) {
          this.items = res.listings.map(l => this.mapToDiscoveryItem(l));
          const target = this.items.find(i => i.isTargetListing) ?? this.items[0];
          this.selectItem(target);
          if (res.targetListingId) {
            this.statusText = `🎯 Hedef ürün (#${res.targetListingId}) ve ${Math.max(0, this.items.length - 1)} rakip pazar ilanı başarıyla yüklendi.`;
          }
          this.showToast('🎯 Hedef Etsy ürünü ve pazar verileri aktarıldı!');
        } else {
          this.statusText = '⚠️ Belirtilen linkten ilan detayları alınamadı.';
          this.showToast('⚠️ İlan bulunamadı. Linki kontrol edip tekrar deneyin.');
        }
      },
      error: (err) => {
        this.isImporting = false;
        this.statusText = `⚠️ Hata: ${err?.message || 'Linkten alma başarısız'}`;
        this.showToast('🚨 İlan linki alınırken hata oluştu.');
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
      this.showToast('⚠️ Lütfen önce tablodan bir ürün seçin veya başlık girin.');
      return;
    }

    this.isGeneratingFullAi = true;
    this.statusText = '🚀 1-Tık Full AI: 100/100 LQS SEO başlığı, 13 altın etiket ve satış odaklı açıklama üretiliyor...';

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

    this.etsyApi.improveListingLqs(cmd).subscribe({
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
          this.statusText = '✅ 1-Tık Full AI Listing başarıyla hazırlandı! İnceleyip "Etsy Taslak Ekle" ile gönderebilirsiniz.';
          this.showToast('🎉 1-Tık Full AI: İlan 100/100 LQS seviyesinde optimize edildi!');
        }
      },
      error: () => {
        this.isGeneratingFullAi = false;
        // Fallback: Yerel akıllı şablon ve optimizasyon motorunu devreye sok
        this.applyLocalOptimizationFallback();
        this.statusText = '✅ Akıllı SEO motoru ile başlık, 13 tag ve ferah açıklama şablonu oluşturuldu.';
        this.showToast('✨ Akıllı SEO motoru taslağı hazırladı!');
      }
    });
  }

  // --- ŞABLONLA PARAGRAFLA ---
  applyDescriptionTemplate(): void {
    const title = this.draftTitle || this.selectedItem?.title || 'Handcrafted Artisan Product';
    const primaryKeyword = this.draftTags[0] || 'Artisan Decor';
    const materials = this.draftMaterials || 'High Quality Materials, Precision Craft';

    const formatted = `✨ ${title.toUpperCase()} ✨

Discover the perfect blend of craftsmanship, premium quality, and timeless design with this unique ${primaryKeyword}. Each piece is meticulously produced to bring elegance and character to your space.

━━━━━━━━━━━━━━━━━━━━━━━━━
🌟 KEY HIGHLIGHTS & FEATURES
━━━━━━━━━━━━━━━━━━━━━━━━━
✔ Premium Craftsmanship: Made with care using top-tier ${materials}.
✔ High Attention to Detail: Smooth finish, vivid colors, and durable structure.
✔ Perfect Gift Idea: A thoughtful and memorable surprise for birthdays, holidays, or special celebrations.
✔ Ready to Enjoy: Safely packed and prepped for effortless display or daily use.

━━━━━━━━━━━━━━━━━━━━━━━━━
📏 SPECIFICATIONS & MATERIALS
━━━━━━━━━━━━━━━━━━━━━━━━━
• Primary Material: ${materials}
• Style: Modern Artisan / Handcrafted
• Condition: Brand New, Made to Order

━━━━━━━━━━━━━━━━━━━━━━━━━
📦 PACKAGING & SHIPPING
━━━━━━━━━━━━━━━━━━━━━━━━━
We take immense pride in securely wrapping each item in multi-layered protective packaging to ensure safe transit directly to your doorstep. Fast dispatch with tracking number provided immediately upon shipment.

━━━━━━━━━━━━━━━━━━━━━━━━━
💬 CUSTOMER CARE & QUESTIONS
━━━━━━━━━━━━━━━━━━━━━━━━━
Have custom color, sizing, or personalization requests? Feel free to reach out via Etsy Messages anytime — we are delighted to assist!`;

    this.draftDescription = formatted;
    this.recalculateLqsChecks();
    this.showToast('📐 Ferah Etsy paragraf şablonu açıklamaya uygulandı!');
  }

  // --- AI AÇIKLAMA YENİLE ---
  generateDescriptionWithAi(): void {
    this.isGeneratingDesc = true;
    this.statusText = '✨ Yapay zeka satış odaklı açıklama metnini yazıyor...';

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

    this.etsyApi.improveListingLqs(cmd).subscribe({
      next: (res) => {
        this.isGeneratingDesc = false;
        if (res && res.optimizedDescription) {
          this.draftDescription = res.optimizedDescription;
          this.showToast('✨ AI Açıklama başarıyla oluşturuldu!');
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
      this.showToast('⚠️ Seçili ürüne ait etiket bulunamadı.');
      return;
    }

    const cleaned = this.selectedItem.tags
      .map(t => t.trim().toLowerCase())
      .filter(t => t.length > 0 && t.length <= 20)
      .slice(0, 13);

    this.draftTags = cleaned;
    this.recalculateLqsChecks();
    this.showToast(`📋 Rakibin ${cleaned.length} etiketi kopyalandı.`);
  }

  // --- ETİKET YÖNETİMİ ---
  addTag(): void {
    const val = (this.newTagInput || '').trim().toLowerCase();
    if (!val) return;
    if (val.length > 20) {
      this.showToast('⚠️ Etsy etiketleri en fazla 20 karakter olabilir!');
      return;
    }
    if (this.draftTags.length >= 13) {
      this.showToast('⚠️ Etsy en fazla 13 etiket kabul eder!');
      return;
    }
    if (this.draftTags.includes(val)) {
      this.showToast('ℹ️ Bu etiket zaten ekli.');
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
      this.showToast('⚠️ Lütfen en az 5 karakterlik bir başlık girin.');
      return;
    }
    if (this.draftPriceUsd <= 0) {
      this.showToast('⚠️ Lütfen geçerli bir fiyat girin.');
      return;
    }

    this.isSavingDraft = true;
    this.statusText = '💾 Etsy mağazanıza taslak listing gönderiliyor...';

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

    this.etsyApi.createListing(payload).subscribe({
      next: (res) => {
        this.isSavingDraft = false;
        if (res && res.listingId) {
          this.createdListingId = res.listingId;
          this.createdListingUrl = res.url || `https://www.etsy.com/your/shops/me/listing-editor/edit/${res.listingId}`;
          this.showSuccessModal = true;
          this.statusText = `✅ Taslak #${res.listingId} Etsy mağazanıza başarıyla eklendi!`;
          this.showToast(`🎉 Taslak #${res.listingId} Etsy mağazanıza kaydedildi!`);
        } else {
          this.showToast('⚠️ Taslak oluşturuldu ancak ID alınamadı.');
        }
      },
      error: (err) => {
        this.isSavingDraft = false;
        const msg = err?.error?.message || err?.message || 'Etsy API taslak oluşturma hatası';
        this.statusText = `🚨 Hata: ${msg}`;
        this.showToast(`🚨 Taslak eklenemedi: ${msg}`);
      }
    });
  }

  // --- ⚡ HIZLI ÜRÜN EKLE'YE AKTAR (Bridge to Fast Creator) ---
  transferToFastCreator(): void {
    if (!this.draftTitle) {
      this.showToast('⚠️ Aktarılacak başlık bulunamadı.');
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

    this.showToast('⚡ İlan Hızlı Ürün Ekleme modülüne aktarılıyor...');
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

  copyToClipboard(text: string, label: string = 'Metin'): void {
    if (!text) return;
    navigator.clipboard.writeText(text).then(() => {
      this.showToast(`📋 ${label} panoya kopyalandı!`);
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
