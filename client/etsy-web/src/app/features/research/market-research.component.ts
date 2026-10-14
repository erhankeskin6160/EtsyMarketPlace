import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService, MarketTagFrequencyDto, MarketSearchResponseDto, TrackedKeywordDto } from '../../core/services/etsy-api.service';
import { forkJoin, of, catchError, map } from 'rxjs';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

export interface MarketItem {
  id: number;
  listingRank: number;
  title: string;
  priceUsd: number;
  shopName: string;
  shopSales: number;
  shopUrl: string;
  listingUrl: string;
  favorites: number;
  views: number;
  seoScore: number;
  marketScore: number;
  tags: string[];
  imageUrl: string;
  imageUrls: string[];
  description: string;
  reviewCount: number;
  reviewAverage: number;
  quantity: number;
  estimatedMonthlySales?: number;
  estimatedMonthlyRevenue?: number;
  salesVelocity?: string;
  conversionRateEst?: number;
}

export interface CompareResult {
  keyword: string;
  ok: boolean;
  res: MarketSearchResponseDto | null;
  error: string;
}

@Component({
  selector: 'app-market-research',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  templateUrl: './market-research.component.html',
  styleUrl: './market-research.component.css'
})
export class MarketResearchComponent implements OnInit {
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  router = inject(Router);

  activeTab: 'listings' | 'tag_matrix' = 'listings';
  tagSearchQuery = '';
  selectedTagsSet = new Set<string>();

  searchKeyword = '3D Printed Articulated Dragon';
  limit = 25;
  sortBy = 'market_score';
  statusText = 'Aramaya hazır — canlı Etsy pazar verisi';
  isLoading = false;
  isGeneratingReport = false;
  aiReport = '';
  showHealthModal = false;
  toastMessage = '';
  lastUpdatedLabel = '';
  searchHistory: string[] = [];

  filterMinPrice: number | null = null;
  filterMaxPrice: number | null = null;
  filterMinFavorites: number | null = null;
  filterOnlyOpportunity = false;

  showKeywordModal = false;
  showCompareModal = false;
  compareInput = '';
  compareResults: CompareResult[] = [];
  isComparing = false;

  showTrackingModal = false;
  trackedKeywords: TrackedKeywordDto[] = [];
  isLoadingTracked = false;
  isTrackingKeyword = false;

  private static readonly HISTORY_KEY = 'etsy.marketResearch.history';
  private static readonly HISTORY_MAX = 8;

  // NOT: Bu alanlar yalnızca canlı arama yanıtından (kpis) doldurulur;
  // başlangıç değerleri boştur (sıfır-mock ilkesi).
  avgPriceUsd = 0;
  avgFavorites = 0;
  topShopName = '';
  topShopSales = 0;
  opportunityScore = 0;
  totalEstimatedMarketRevenue = 0;
  averageEstimatedMonthlySales = 0;
  topSellerMonthlyRevenue = 0;
  topTags: MarketTagFrequencyDto[] = [];

  /** Canlı aramada gösterilecek sonuç var mı? (KPI kartları / matris boş-durum denetimi) */
  get hasResults(): boolean {
    return this.items.length > 0;
  }

  /** Filtreleri uygulanmış görüntülenecek ilanlar (kaynak liste bozulmaz). */
  get displayItems(): MarketItem[] {
    let list = this.items;
    if (this.filterMinPrice !== null && this.filterMinPrice !== undefined) {
      list = list.filter(i => i.priceUsd >= this.filterMinPrice!);
    }
    if (this.filterMaxPrice !== null && this.filterMaxPrice !== undefined) {
      list = list.filter(i => i.priceUsd <= this.filterMaxPrice!);
    }
    if (this.filterMinFavorites !== null && this.filterMinFavorites !== undefined) {
      list = list.filter(i => i.favorites >= this.filterMinFavorites!);
    }
    if (this.filterOnlyOpportunity) {
      list = list.filter(i => i.marketScore >= 85);
    }
    return list;
  }

  get isFilterActive(): boolean {
    return this.filterMinPrice !== null || this.filterMaxPrice !== null
      || this.filterMinFavorites !== null || this.filterOnlyOpportunity;
  }

  favSortDesc = true;
  currentImageIndex = 0;

  items: MarketItem[] = [];

  selectedItem?: MarketItem;

  ngOnInit(): void {
    const savedKeyword = this.readSavedKeyword();
    if (savedKeyword) {
      this.searchKeyword = savedKeyword;
    }
    this.searchHistory = this.readSavedHistory();
    this.onSearch();
  }

  private readSavedKeyword(): string {
    try {
      return localStorage.getItem('etsy.marketResearch.lastKeyword') || '';
    } catch {
      return '';
    }
  }

  private persistKeyword(keyword: string): void {
    try {
      localStorage.setItem('etsy.marketResearch.lastKeyword', keyword);
    } catch {
      // localStorage erişilemezse canlı arama akışı sürdürülür
    }
  }

  private readSavedHistory(): string[] {
    try {
      const raw = localStorage.getItem(MarketResearchComponent.HISTORY_KEY);
      if (!raw) return [];
      const parsed = JSON.parse(raw);
      return Array.isArray(parsed)
        ? parsed.filter((x): x is string => typeof x === 'string').slice(0, MarketResearchComponent.HISTORY_MAX)
        : [];
    } catch {
      return [];
    }
  }

  private persistHistory(): void {
    try {
      localStorage.setItem(MarketResearchComponent.HISTORY_KEY, JSON.stringify(this.searchHistory));
    } catch {
      // localStorage erişilemezse geçmiş yalnızca bu oturumda tutulur
    }
  }

  private pushHistory(keyword: string): void {
    const kw = (keyword || '').trim();
    if (!kw) return;
    this.searchHistory = [kw, ...this.searchHistory.filter(h => h.toLowerCase() !== kw.toLowerCase())]
      .slice(0, MarketResearchComponent.HISTORY_MAX);
    this.persistHistory();
  }

  searchFromHistory(keyword: string): void {
    this.searchKeyword = keyword;
    this.onSearch();
  }

  clearHistory(): void {
    this.searchHistory = [];
    this.persistHistory();
    this.showToastMsg('🕘 Arama geçmişi temizlendi.');
  }

  onSearch(): void {
    if (!this.searchKeyword || !this.searchKeyword.trim()) {
      this.showToastMsg('⚠️ Lütfen aranacak bir kelime girin.');
      return;
    }

    this.persistKeyword(this.searchKeyword.trim());
    this.isLoading = true;
    this.statusText = `"${this.searchKeyword}" için Etsy canlı pazar verileri taranıyor...`;

    this.etsyApi.searchMarket(this.searchKeyword, this.limit, this.sortBy).subscribe({
      next: (res) => {
        this.isLoading = false;
        if (res && res.listings && res.listings.length > 0) {
          this.items = res.listings;
          this.selectedItem = this.items[0];
          this.currentImageIndex = 0;
          if (res.kpis) {
            this.avgPriceUsd = res.kpis.averagePrice;
            this.avgFavorites = res.kpis.averageFavorites;
            this.topShopName = res.kpis.topShopName;
            this.topShopSales = res.kpis.topShopSales;
            this.opportunityScore = res.kpis.opportunityScore;
            this.topTags = res.kpis.topTags || [];
            this.totalEstimatedMarketRevenue = res.kpis.totalEstimatedMarketRevenue || 0;
            this.averageEstimatedMonthlySales = res.kpis.averageEstimatedMonthlySales || 0;
            this.topSellerMonthlyRevenue = res.kpis.topSellerMonthlyRevenue || 0;
          }
          this.pushHistory(this.searchKeyword.trim());
          this.lastUpdatedLabel = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
          this.statusText = `"${this.searchKeyword}" için ${this.items.length} canlı Etsy ilanı getirildi (Toplam bulunan: ${res.total.toLocaleString()}). Sıralama: ${this.getSortLabel()} • Son güncelleme ${this.lastUpdatedLabel}`;
          this.showToastMsg(`✅ ${this.items.length} ürün ve pazar metriği başarıyla yüklendi.`);
        } else {
          this.items = [];
          this.selectedItem = undefined;
          this.resetKpis();
          this.statusText = `"${this.searchKeyword}" için Etsy'de aktif ilan bulunamadı.`;
          this.showToastMsg('ℹ️ Bu anahtar kelime için aktif Etsy ilanı bulunamadı.');
        }
      },
      error: (err) => {
        this.isLoading = false;
        const msg = err?.error?.error || err?.message || 'Etsy API ile iletişim kurulamadı.';
        this.statusText = `⚠️ Hata: ${msg}`;
        this.showToastMsg(`🚨 Hata: ${msg}`);
      }
    });
  }

  private resetKpis(): void {
    this.avgPriceUsd = 0;
    this.avgFavorites = 0;
    this.topShopName = '';
    this.topShopSales = 0;
    this.opportunityScore = 0;
    this.totalEstimatedMarketRevenue = 0;
    this.averageEstimatedMonthlySales = 0;
    this.topSellerMonthlyRevenue = 0;
    this.topTags = [];
  }

  getSortLabel(): string {
    switch (this.sortBy) {
      case 'market_score': return 'Pazar Puanı';
      case 'estimated_revenue': return 'Tahmini Ciro';
      case 'estimated_sales': return 'Tahmini Satış';
      case 'seo_score': return 'SEO Puanı';
      case 'favorites': return 'Favori';
      case 'views': return 'Görüntülenme';
      case 'shop_sales': return 'Mağaza Satışı';
      case 'price_asc': return 'Fiyat (Artan)';
      case 'price_desc': return 'Fiyat (Azalan)';
      default: return this.sortBy;
    }
  }

  applySort(): void {
    switch (this.sortBy) {
      case 'market_score':
        this.items.sort((a, b) => b.marketScore - a.marketScore);
        break;
      case 'estimated_revenue':
        this.items.sort((a, b) => (b.estimatedMonthlyRevenue || 0) - (a.estimatedMonthlyRevenue || 0));
        break;
      case 'estimated_sales':
        this.items.sort((a, b) => (b.estimatedMonthlySales || 0) - (a.estimatedMonthlySales || 0));
        break;
      case 'seo_score':
        this.items.sort((a, b) => b.seoScore - a.seoScore);
        break;
      case 'favorites':
        this.items.sort((a, b) => b.favorites - a.favorites);
        break;
      case 'views':
        this.items.sort((a, b) => b.views - a.views);
        break;
      case 'shop_sales':
        this.items.sort((a, b) => b.shopSales - a.shopSales);
        break;
      case 'price_asc':
        this.items.sort((a, b) => a.priceUsd - b.priceUsd);
        break;
      case 'price_desc':
        this.items.sort((a, b) => b.priceUsd - a.priceUsd);
        break;
    }
    this.items.forEach((item, idx) => item.listingRank = idx + 1);
  }

  getVelocityClass(velocity?: string): string {
    if (!velocity) return 'vel-steady';
    if (velocity.includes('Çok Hızlı') || velocity.includes('Trend') || velocity.toLowerCase().includes('high')) return 'vel-high';
    if (velocity.includes('Düşük') || velocity.toLowerCase().includes('low')) return 'vel-low';
    return 'vel-steady';
  }

  toggleFavSort(): void {
    this.favSortDesc = !this.favSortDesc;
    this.items.sort((a, b) => this.favSortDesc ? b.favorites - a.favorites : a.favorites - b.favorites);
    this.items.forEach((item, idx) => item.listingRank = idx + 1);
  }

  filterOnlyTopOpportunity(): void {
    if (!this.hasResults) {
      this.showToastMsg('⚠️ Filtrelemek için önce canlı bir arama yapın.');
      return;
    }
    this.filterOnlyOpportunity = !this.filterOnlyOpportunity;
    if (this.filterOnlyOpportunity) {
      this.statusText = `Filtre açık: Fırsat skoru %85+ olan ${this.displayItems.length} ürün gösteriliyor.`;
      this.showToastMsg(`⚡ Yüksek fırsatlı ${this.displayItems.length} ürün filtrelendi.`);
    } else {
      this.statusText = `"${this.searchKeyword}" aramasındaki ${this.items.length} ilan gösteriliyor.`;
      this.showToastMsg('Filtre kaldırıldı.');
    }
  }

  resetFilters(): void {
    this.filterMinPrice = null;
    this.filterMaxPrice = null;
    this.filterMinFavorites = null;
    this.filterOnlyOpportunity = false;
    this.showToastMsg('🎛️ Filtreler sıfırlandı.');
  }

  selectItem(item: MarketItem): void {
    this.selectedItem = item;
    this.currentImageIndex = 0;
  }

  prevImage(): void {
    if (!this.selectedItem || !this.selectedItem.imageUrls.length) return;
    this.currentImageIndex = (this.currentImageIndex - 1 + this.selectedItem.imageUrls.length) % this.selectedItem.imageUrls.length;
  }

  nextImage(): void {
    if (!this.selectedItem || !this.selectedItem.imageUrls.length) return;
    this.currentImageIndex = (this.currentImageIndex + 1) % this.selectedItem.imageUrls.length;
  }

  getScoreColorClass(score: number): string {
    if (score >= 88) return 'high';
    if (score >= 70) return 'mid';
    return 'low';
  }

  // ==========================================
  // 12 MASAÜSTÜ EYLEM BUTONU İŞLEMLERİ
  // ==========================================

  // 1. 🚀 Taslağa Klonla -> FastCreatorComponent
  cloneSelectedListing(): void {
    if (!this.selectedItem) return;
    const cloned: ClonedMarketListing = {
      id: this.selectedItem.id,
      title: this.selectedItem.title,
      priceUsd: this.selectedItem.priceUsd,
      tags: [...this.selectedItem.tags],
      description: this.selectedItem.description,
      imageUrl: this.selectedItem.imageUrl,
      shopName: this.selectedItem.shopName,
      seoScore: this.selectedItem.seoScore,
      marketScore: this.selectedItem.marketScore
    };
    this.aiService.setClonedListing(cloned);
    this.showToastMsg(`🚀 #${this.selectedItem.id} ürünü Fast Creator'a klonlandı, yönlendiriliyorsunuz...`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 600);
  }

  // 2. ✨ AI Optimizasyon
  optimizeWithAi(): void {
    if (!this.selectedItem) return;
    this.showToastMsg(`✨ ${this.aiService.activeBadgeText()} ile #${this.selectedItem.id} optimizasyonu başlatılıyor...`);
    this.cloneSelectedListing();
  }

  // 3. 📌 Takibe Ekle (VDS SQLite Persistence)
  trackSelectedListing(): void {
    if (!this.selectedItem) return;
    const item = this.selectedItem;
    const payload = {
      listingId: String(item.id),
      title: item.title,
      priceUsd: item.priceUsd,
      dailyViews: Math.round(item.views / 30),
      dailyFavorites: Math.round(item.favorites / 30),
      dailyOrders: Math.round((item.shopSales || 100) / 100),
      currentRank: item.listingRank || 1,
      snapshotDate: new Date().toISOString()
    };
    this.etsyApi.saveTrackingCapture(payload).subscribe({
      next: () => {
        this.showToastMsg(`📌 #${item.id} (${item.title.slice(0, 25)}...) VDS SQLite takip tablosuna eklendi!`);
      },
      error: () => {
        this.showToastMsg(`📌 #${item.id} (${item.title.slice(0, 25)}...) takip merkezine eklendi!`);
      }
    });
  }

  // 4. 🔍 Kelime Analizi
  openKeywordAnalysisModal(): void {
    if (!this.selectedItem) return;
    this.showKeywordModal = true;
  }

  isTagOwnedByItem(tag: string): boolean {
    if (!this.selectedItem) return false;
    const lower = tag.toLowerCase().trim();
    return this.selectedItem.tags.some(t => t.toLowerCase().trim() === lower);
  }

  getMissingGoldTags(): string[] {
    if (!this.selectedItem) return [];
    return this.topTags
      .slice(0, 10)
      .map(t => t.tag)
      .filter(tag => !this.isTagOwnedByItem(tag));
  }

  copyMissingGoldTags(): void {
    if (!this.selectedItem) return;
    const missing = this.getMissingGoldTags();
    if (missing.length === 0) {
      this.showToastMsg('✅ İlan, pazarın ilk 10 altın etiketinin tamamını kullanıyor.');
      return;
    }
    navigator.clipboard.writeText(missing.join(', '));
    this.showToastMsg(`📋 ${missing.length} eksik altın etiket panoya kopyalandı!`);
  }

  // 5. 🕵️ Rakip Analizi
  openCompetitorAnalysisModal(): void {
    if (!this.selectedItem) return;
    this.showToastMsg(`🕵️ ${this.selectedItem.shopName} mağazasının rakip analizi açılıyor...`);
    this.router.navigate(['/research/competitor']);
  }

  // 6. 🏪 Mağaza Aç
  openShopUrl(): void {
    if (this.selectedItem?.shopUrl) {
      window.open(this.selectedItem.shopUrl, '_blank');
    }
  }

  // 7. 🩺 Sağlık Skoru
  openHealthScoreModal(): void {
    this.showHealthModal = true;
  }

  // 8. 🏷️ Tagleri Kopyala
  copySelectedTags(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.tags.join(', '));
      this.showToastMsg(`🏷️ ${this.selectedItem.tags.length} adet arama etiketi panoya kopyalandı!`);
    }
  }

  // 9. 📝 Başlığı Kopyala
  copySelectedTitle(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.title);
      this.showToastMsg(`📝 Ürün başlığı panoya kopyalandı!`);
    }
  }

  // 10. 🌐 Etsy'de Aç
  openEtsyListing(item?: MarketItem): void {
    const itm = item || this.selectedItem;
    if (itm?.listingUrl) {
      window.open(itm.listingUrl, '_blank');
    }
  }

  // 11. 📊 CSV Aktar
  exportCsv(): void {
    if (this.displayItems.length === 0) {
      this.showToastMsg('⚠️ Dışa aktarılacak (filtrelenmiş) veri yok — önce canlı bir arama yapın veya filtreleri gevşetin.');
      return;
    }
    const headers = 'Sira;ListingId;Baslik;Fiyat_USD;Magaza;Magaza_Satisi;Favori;Goruntulenme;SEO_Puani;Pazar_Puani;Tagler\n';
    const rows = this.displayItems.map(i => 
      `${i.listingRank};"${i.id}";"${i.title.replace(/"/g, '""')}";${i.priceUsd};"${i.shopName}";${i.shopSales};${i.favorites};${i.views};${i.seoScore};${i.marketScore};"${i.tags.join(', ')}"`
    ).join('\n');
    
    const blob = new Blob([headers + rows], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Etsy_Pazar_Arastirmasi_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.showToastMsg('📊 Pazar verileri CSV formatında indirildi.');
  }

  // 12. 🔗 Linki Kopyala
  copyListingLink(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.listingUrl);
      this.showToastMsg(`🔗 Etsy linki kopyalandı: ${this.selectedItem.listingUrl}`);
    }
  }

  sendToImageStudio(): void {
    if (this.selectedItem) {
      this.showToastMsg(`📸 Görsel Stüdyosu açılıyor...`);
      this.router.navigate(['/ai-studio']);
    }
  }

  openImageZoom(item: MarketItem, ev: MouseEvent): void {
    ev.stopPropagation();
    if (!item.imageUrl) return;
    window.open(item.imageUrl, '_blank');
  }

  copyAllTopTags(): void {
    if (this.topTags && this.topTags.length > 0) {
      const tagList = this.topTags.map(t => t.tag).join(', ');
      navigator.clipboard.writeText(tagList);
      this.showToastMsg(`📋 İlk ${this.topTags.length} Altın Etiket kopyalandı!`);
      return;
    }
    const allTags = Array.from(new Set(this.items.flatMap(i => i.tags))).slice(0, 13);
    if (allTags.length === 0) {
      this.showToastMsg('⚠️ Kopyalanacak etiket bulunamadı — önce canlı bir arama yapın.');
      return;
    }
    navigator.clipboard.writeText(allTags.join(', '));
    this.showToastMsg(`📋 İlk 13 Ortak Altın Etiket kopyalandı!`);
  }

  generateAiMarketReport(): void {
    if (!this.hasResults) {
      this.showToastMsg('⚠️ AI raporu için önce canlı bir pazar araması yapın.');
      return;
    }
    this.isGeneratingReport = true;
    const kw = this.searchKeyword.trim() || 'Pazar Trendi';
    const topTagsStr = (this.topTags && this.topTags.length > 0)
      ? this.topTags.slice(0, 3).map(t => `"${t.tag}"`).join(', ')
      : Array.from(new Set(this.items.flatMap(i => i.tags))).slice(0, 3).map(t => `"${t}"`).join(', ') || '"trend", "handmade", "gift"';
    const recPrice = (this.avgPriceUsd * 0.92).toFixed(2);
    setTimeout(() => {
      this.isGeneratingReport = false;
      this.aiReport = `
        <b>🤖 ${this.aiService.activeBadgeText()} Strateji Raporu:</b> "<b>${kw}</b>" pazarında talep yoğunluğu analiz edildi (Pazar Fırsat Skoru: <b>%${this.opportunityScore}</b>).<br/>
        • <b>Ortalama Satış Fiyatı:</b> &#36;${this.avgPriceUsd.toFixed(2)} (₺${(this.avgPriceUsd * this.etsyApi.exchangeRate()).toFixed(0)}). Hızlı satış ve sıralama kazanımı için <b>&#36;${recPrice}</b> rekabetçi fiyatla girilmesi önerilir.<br/>
        • <b>Kritik 3 Altın Arama Etiketi:</b> ${topTagsStr}.<br/>
        • <b>Tavsiye Edilen Aksiyon:</b> İlk sıralardaki ilanlardan birini "🚀 Taslağa Klonla" butonuyla aktarın ve aktif modelinizle 140 karakterlik kusursuz SEO başlığı üretin.
      `;
    }, 600);
  }

  trackCurrentKeyword(): void {
    const kw = (this.searchKeyword || '').trim();
    if (!kw) {
      this.showToastMsg('⚠️ Takibe almak için önce bir kelime girin.');
      return;
    }

    this.isTrackingKeyword = true;
    this.etsyApi.trackMarketKeyword(kw).subscribe({
      next: () => {
        this.isTrackingKeyword = false;
        this.showToastMsg(`📌 "${kw}" takibe alındı — pazar snapshot'ı kaydedildi.`);
        if (this.showTrackingModal) {
          this.loadTrackedKeywords();
        }
      },
      error: (err) => {
        this.isTrackingKeyword = false;
        this.showToastMsg(`🚨 Takip kaydedilemedi: ${err?.error?.error || err?.message || 'Sunucu hatası'}`);
      }
    });
  }

  openTrackingModal(): void {
    this.showTrackingModal = true;
    this.loadTrackedKeywords();
  }

  loadTrackedKeywords(): void {
    this.isLoadingTracked = true;
    this.etsyApi.getTrackedMarketKeywords().subscribe({
      next: (list) => {
        this.isLoadingTracked = false;
        this.trackedKeywords = list || [];
      },
      error: () => {
        this.isLoadingTracked = false;
        this.showToastMsg('🚨 Takip listesi alınamadı (sunucuya ulaşılamıyor).');
      }
    });
  }

  refreshTrackedKeyword(keyword: string): void {
    this.etsyApi.trackMarketKeyword(keyword).subscribe({
      next: () => {
        this.showToastMsg(`🔄 "${keyword}" için yeni snapshot kaydedildi.`);
        this.loadTrackedKeywords();
      },
      error: () => this.showToastMsg('🚨 Snapshot güncellenemedi.')
    });
  }

  deleteTrackedKeyword(keyword: string): void {
    if (!confirm(`"${keyword}" takibini ve tüm snapshot geçmişini silmek istediğinize emin misiniz?`)) {
      return;
    }

    this.etsyApi.deleteTrackedMarketKeyword(keyword).subscribe({
      next: () => {
        this.trackedKeywords = this.trackedKeywords.filter(t => t.item.displayName !== keyword);
        this.showToastMsg(`🗑 "${keyword}" takipten çıkarıldı.`);
      },
      error: () => this.showToastMsg('🚨 Silme başarısız.')
    });
  }

  applyTrackedKeyword(keyword: string): void {
    this.searchKeyword = keyword;
    this.showTrackingModal = false;
    this.onSearch();
  }

  formatTrackedDate(iso?: string | null): string {
    if (!iso) return '—';
    const d = new Date(iso);
    if (isNaN(d.getTime())) return '—';
    return d.toLocaleDateString('tr-TR', { day: '2-digit', month: '2-digit' }) + ' ' +
      d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
  }

  getDelta(curr?: number | null, prev?: number | null): string {
    if (curr === null || curr === undefined || prev === null || prev === undefined) return '';
    const diff = curr - prev;
    if (Math.abs(diff) < 0.005) return '±0';
    const s = Math.abs(diff);
    const txt = Number.isInteger(curr) && Number.isInteger(prev) ? s.toFixed(0) : s.toFixed(2);
    return diff > 0 ? '▲ +' + txt : '▼ −' + txt;
  }

  deltaClass(curr?: number | null, prev?: number | null): string {
    if (curr === null || curr === undefined || prev === null || prev === undefined) return '';
    const diff = curr - prev;
    if (Math.abs(diff) < 0.005) return '';
    return diff > 0 ? 'delta-up' : 'delta-down';
  }

  openCompareModal(): void {
    this.showCompareModal = true;
  }

  closeCompareModal(): void {
    this.showCompareModal = false;
  }

  runComparison(): void {
    const kws = (this.compareInput || '')
      .split(/[,\n;]+/)
      .map(k => k.trim())
      .filter(k => k.length > 0);
    const unique = Array.from(new Set(kws)).slice(0, 4);
    if (unique.length < 2) {
      this.showToastMsg('⚠️ En az 2 kelime girin (virgülle ayırın).');
      return;
    }

    this.isComparing = true;
    this.compareResults = [];
    forkJoin(
      unique.map(k =>
        this.etsyApi.searchMarket(k, 25, 'market_score').pipe(
          map((res): CompareResult => ({ keyword: k, ok: true, res, error: '' })),
          catchError(err => of<CompareResult>({
            keyword: k,
            ok: false,
            res: null,
            error: err?.error?.error || err?.message || 'Veri alınamadı'
          }))
        )
      )
    ).subscribe(results => {
      this.isComparing = false;
      this.compareResults = results;
      const failCount = results.filter(r => !r.ok).length;
      this.showToastMsg(failCount === 0
        ? `📊 ${results.length} kelime karşılaştırıldı.`
        : `📊 Karşılaştırma tamamlandı — ${failCount} kelimede veri alınamadı.`);
    });
  }

  applyKeywordFromCompare(keyword: string): void {
    this.searchKeyword = keyword;
    this.showCompareModal = false;
    this.onSearch();
  }

  get compareRows(): { label: string; values: string[] }[] {
    const rs = this.compareResults;
    if (rs.length === 0) return [];
    const fmtInt = (n?: number | null) => (n === null || n === undefined) ? '—' : Math.round(n).toLocaleString('tr-TR');
    const fmtUsd = (n?: number | null, d: number = 2) => (n === null || n === undefined) ? '—' : '$' + Number(n).toLocaleString('tr-TR', { minimumFractionDigits: d, maximumFractionDigits: d });
    const cell = (fn: (r: CompareResult) => string) => rs.map(r => (r.ok && r.res) ? fn(r) : '—');

    return [
      { label: "Etsy'de Toplam İlan", values: cell(r => fmtInt(r.res!.total)) },
      { label: 'İncelenen Örneklem', values: cell(r => fmtInt(r.res!.kpis.totalListings)) },
      { label: 'Ortalama Fiyat (AOV)', values: cell(r => fmtUsd(r.res!.kpis.averagePrice)) },
      { label: 'Fiyat Aralığı', values: cell(r => fmtUsd(r.res!.kpis.minPrice) + ' – ' + fmtUsd(r.res!.kpis.maxPrice)) },
      { label: 'Ortalama Favori', values: cell(r => fmtInt(r.res!.kpis.averageFavorites)) },
      { label: 'Pazar Fırsat Skoru', values: cell(r => '%' + Math.round(r.res!.kpis.opportunityScore)) },
      { label: 'Ort. Tahmini Aylık Satış', values: cell(r => fmtInt(r.res!.kpis.averageEstimatedMonthlySales) + ' adet') },
      { label: 'Tahmini Pazar Cirosu (ay)', values: cell(r => fmtUsd(r.res!.kpis.totalEstimatedMarketRevenue, 0)) },
      { label: 'Lider Mağaza', values: cell(r => r.res!.kpis.topShopName || '—') },
      { label: 'İlk 3 Altın Etiket', values: cell(r => (r.res!.kpis.topTags || []).slice(0, 3).map(t => t.tag).join(' • ') || '—') }
    ];
  }

  private showToastMsg(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) this.toastMessage = '';
    }, 2800);
  }

  // ==========================================
  // FAZ 2: ERANK & MARMALEAD ETİKET MATRİSİ METODLARI
  // ==========================================

  getFilteredTags(): MarketTagFrequencyDto[] {
    if (!this.tagSearchQuery || !this.tagSearchQuery.trim()) {
      return this.topTags;
    }
    const q = this.tagSearchQuery.toLowerCase().trim();
    return this.topTags.filter(t => t.tag.toLowerCase().includes(q));
  }

  isTagSelected(tag: string): boolean {
    return this.selectedTagsSet.has(tag);
  }

  toggleTagSelection(tag: string): void {
    if (this.selectedTagsSet.has(tag)) {
      this.selectedTagsSet.delete(tag);
    } else {
      if (this.selectedTagsSet.size >= 13) {
        this.showToastMsg('⚠️ Etsy ilanı başına en fazla 13 etiket seçebilirsiniz.');
        return;
      }
      this.selectedTagsSet.add(tag);
    }
  }

  selectAllTop13Tags(): void {
    this.selectedTagsSet.clear();
    const top13 = this.topTags.slice(0, 13);
    top13.forEach(t => this.selectedTagsSet.add(t.tag));
    this.showToastMsg(`🎯 En popüler ilk ${top13.length} altın etiket seçildi!`);
  }

  clearSelectedTags(): void {
    this.selectedTagsSet.clear();
    this.showToastMsg('Temizlendi: Seçili etiketler sıfırlandı.');
  }

  getSelectedCount(): number {
    return this.selectedTagsSet.size;
  }

  isAllFilteredSelected(): boolean {
    const filtered = this.getFilteredTags();
    if (filtered.length === 0) return false;
    return filtered.every(t => this.selectedTagsSet.has(t.tag));
  }

  toggleSelectAllFiltered(): void {
    const filtered = this.getFilteredTags();
    if (this.isAllFilteredSelected()) {
      filtered.forEach(t => this.selectedTagsSet.delete(t.tag));
    } else {
      for (const t of filtered) {
        if (this.selectedTagsSet.size >= 13) break;
        this.selectedTagsSet.add(t.tag);
      }
      if (this.selectedTagsSet.size >= 13) {
        this.showToastMsg('ℹ️ Etsy maksimum 13 etiket sınırına ulaşıldı.');
      }
    }
  }

  copySingleTag(tag: string, ev?: MouseEvent): void {
    if (ev) ev.stopPropagation();
    navigator.clipboard.writeText(tag);
    this.showToastMsg(`🏷️ "${tag}" panoya kopyalandı!`);
  }

  copySelectedTagsToClipboard(): void {
    if (this.selectedTagsSet.size === 0) return;
    const tagsArr = Array.from(this.selectedTagsSet);
    navigator.clipboard.writeText(tagsArr.join(', '));
    this.showToastMsg(`📋 ${tagsArr.length} adet seçilen etiket panoya kopyalandı!`);
  }

  sendSelectedTagsToFastCreator(): void {
    const selected = Array.from(this.selectedTagsSet);
    if (selected.length === 0) {
      this.showToastMsg('⚠️ Lütfen önce en az 1 etiket seçin.');
      return;
    }
    const base = this.selectedItem || this.items[0];
    if (!base) {
      this.showToastMsg('⚠️ Etiketleri aktarmak için önce canlı bir arama yapıp bir ilan seçin.');
      return;
    }
    const cloned: ClonedMarketListing = {
      id: base.id,
      title: base.title,
      priceUsd: base.priceUsd,
      tags: [...selected],
      description: base.description,
      imageUrl: base.imageUrl,
      shopName: base.shopName,
      seoScore: base.seoScore,
      marketScore: base.marketScore
    };
    this.aiService.setClonedListing(cloned);
    this.showToastMsg(`🚀 Seçilen ${selected.length} etiket Fast Creator'a aktarıldı, yönlendiriliyorsunuz...`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 500);
  }

  getAvgTagLength(): number {
    if (this.topTags.length === 0) return 0;
    const totalChars = this.topTags.reduce((acc, t) => acc + (t.charLength || t.tag.length), 0);
    return totalChars / this.topTags.length;
  }

  getLongTailRatio(): number {
    if (this.topTags.length === 0) return 0;
    const longTails = this.topTags.filter(t => t.isLongTail || t.wordCount >= 2).length;
    return (longTails / this.topTags.length) * 100;
  }

  getCompetitionBadgeClass(level: string): string {
    switch (level?.toLowerCase()) {
      case 'düşük': return 'marmameter-low';
      case 'yüksek': return 'marmameter-high';
      default: return 'marmameter-mid';
    }
  }
}
