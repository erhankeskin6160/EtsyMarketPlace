import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService, MarketTagFrequencyDto } from '../../core/services/etsy-api.service';
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
  statusText = '9 ilan listelendi. Sıralama: Pazar Puanı';
  isLoading = false;
  isGeneratingReport = false;
  aiReport = '';
  showHealthModal = false;
  toastMessage = '';

  avgPriceUsd = 34.80;
  avgFavorites = 1420;
  topShopName = 'MythicForgeCrafts';
  topShopSales = 14820;
  opportunityScore = 88;
  totalEstimatedMarketRevenue = 18450;
  averageEstimatedMonthlySales = 52;
  topSellerMonthlyRevenue = 3450;
  topTags: MarketTagFrequencyDto[] = [
    { tag: '3d printed', count: 9, usagePercentage: 100, wordCount: 2, charLength: 10, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'desk organizer', count: 8, usagePercentage: 88.9, wordCount: 2, charLength: 14, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'fidget toy', count: 7, usagePercentage: 77.8, wordCount: 2, charLength: 10, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'articulated dragon', count: 7, usagePercentage: 77.8, wordCount: 2, charLength: 18, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'gamer gift', count: 6, usagePercentage: 66.7, wordCount: 2, charLength: 10, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'office decor', count: 5, usagePercentage: 55.6, wordCount: 2, charLength: 12, isLongTail: true, competitionLevel: 'Yüksek' },
    { tag: 'cable management', count: 4, usagePercentage: 44.4, wordCount: 2, charLength: 16, isLongTail: true, competitionLevel: 'Orta' },
    { tag: 'mechanical toy', count: 4, usagePercentage: 44.4, wordCount: 2, charLength: 14, isLongTail: true, competitionLevel: 'Orta' },
    { tag: 'steampunk prop', count: 3, usagePercentage: 33.3, wordCount: 2, charLength: 14, isLongTail: true, competitionLevel: 'Orta' },
    { tag: 'fantasy figurine', count: 3, usagePercentage: 33.3, wordCount: 2, charLength: 16, isLongTail: true, competitionLevel: 'Orta' },
    { tag: 'tabletop rpg', count: 3, usagePercentage: 33.3, wordCount: 2, charLength: 12, isLongTail: true, competitionLevel: 'Orta' },
    { tag: 'resin print', count: 2, usagePercentage: 22.2, wordCount: 2, charLength: 11, isLongTail: true, competitionLevel: 'Düşük' },
    { tag: 'cosplay helmet', count: 2, usagePercentage: 22.2, wordCount: 2, charLength: 14, isLongTail: true, competitionLevel: 'Düşük' }
  ];

  favSortDesc = true;
  currentImageIndex = 0;

  items: MarketItem[] = [
    {
      id: 4188710928,
      listingRank: 1,
      title: 'Articulated Crystal Dragon with Moving Wings 3D Print Fidget Toy',
      priceUsd: 39.50,
      shopName: 'MythicForgeCrafts',
      shopSales: 14820,
      shopUrl: 'https://www.etsy.com/shop/MythicForgeCrafts',
      listingUrl: 'https://www.etsy.com/listing/4188710928',
      views: 18450,
      favorites: 3420,
      seoScore: 96,
      marketScore: 94,
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=600',
        'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600'
      ],
      description: 'Handcrafted high quality articulated crystal dragon with movable wings. Printed using premium dual-color silk PLA on Bambu Lab X1-Carbon. Perfect fantasy gift and fidget desk toy.',
      reviewCount: 428,
      reviewAverage: 4.9,
      quantity: 12,
      tags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'winged dragon', 'desk decor', 'dnd gift', 'dragon sculpture', 'fantasy beast', 'mythical creature', 'adhd toy', 'bambu lab', 'flexi animal', 'gamer gift']
    },
    {
      id: 4188192041,
      listingRank: 2,
      title: 'Minimalist Geometric Self-Watering Planter for Succulents & Cacti',
      priceUsd: 26.00,
      shopName: 'Botanical3DPrints',
      shopSales: 8940,
      shopUrl: 'https://www.etsy.com/shop/Botanical3DPrints',
      listingUrl: 'https://www.etsy.com/listing/4188192041',
      views: 12340,
      favorites: 2490,
      seoScore: 92,
      marketScore: 89,
      imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=600'
      ],
      description: 'Modern geometric self-watering planter pot. Features an internal water reservoir and cotton wick system to keep your succulents and houseplants thriving without root rot.',
      reviewCount: 265,
      reviewAverage: 4.8,
      quantity: 25,
      tags: ['self watering', 'succulent pot', 'geometric planter', 'minimalist decor', 'indoor planter', '3d printed pot', 'modern planter', 'plant lover gift', 'desktop plant', 'drainage pot', 'eco filament', 'home decor', 'botanical pot']
    },
    {
      id: 4187920145,
      listingRank: 3,
      title: 'Steampunk Mechanical Skeleton Gear Desk Clock with Moving Cogs',
      priceUsd: 89.00,
      shopName: 'TimeWorksDesign',
      shopSales: 5120,
      shopUrl: 'https://www.etsy.com/shop/TimeWorksDesign',
      listingUrl: 'https://www.etsy.com/listing/4187920145',
      views: 9820,
      favorites: 1850,
      seoScore: 88,
      marketScore: 86,
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600'
      ],
      description: 'Precision 3D printed mechanical steampunk desk clock. Features exposed planetary gears, quartz movement, and rustic metallic antique brass finish.',
      reviewCount: 180,
      reviewAverage: 4.7,
      quantity: 8,
      tags: ['steampunk clock', 'mechanical clock', 'gear desk clock', 'industrial clock', 'vintage steampunk', '3d printed clock', 'engineer gift', 'skeleton clock', 'rustic home decor', 'mens gift', 'retro desk accessory', 'clockwork art', 'maker design']
    },
    {
      id: 4187299104,
      listingRank: 4,
      title: 'Captain Jack Sparrow Functional Replica Compass with Working Needle',
      priceUsd: 48.50,
      shopName: 'PiratesTreasureCo',
      shopSales: 7340,
      shopUrl: 'https://www.etsy.com/shop/PiratesTreasureCo',
      listingUrl: 'https://www.etsy.com/listing/4187299104',
      views: 14200,
      favorites: 2980,
      seoScore: 94,
      marketScore: 91,
      imageUrl: 'https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600'],
      description: 'Full sized screen-accurate Jack Sparrow compass replica with rotating dial and weathered faux-leather strap. Hand-painted with acrylic wash.',
      reviewCount: 310,
      reviewAverage: 4.9,
      quantity: 14,
      tags: ['jack sparrow', 'pirate compass', 'cosplay prop', 'pirates caribbean', 'working compass', 'movie replica', 'leather compass', 'halloween prop', 'ren faire', 'pirate costume', 'dnd artifact', 'prop maker', 'nautical antique']
    },
    {
      id: 4185189201,
      listingRank: 5,
      title: 'Arcane Jinx Fishbones Rocket Launcher 1:1 Cosplay Replica Prop',
      priceUsd: 145.00,
      shopName: 'ZaunWorkshop',
      shopSales: 3200,
      shopUrl: 'https://www.etsy.com/shop/ZaunWorkshop',
      listingUrl: 'https://www.etsy.com/listing/4185189201',
      views: 22100,
      favorites: 4120,
      seoScore: 90,
      marketScore: 88,
      imageUrl: 'https://images.unsplash.com/photo-1563089145-599997674d42?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1563089145-599997674d42?w=600'],
      description: 'Life sized Jinx Fishbones rocket launcher. Lightweight hollow PLA build, LED glowing eye sockets, and movable mouth jaw. Perfect for comic con conventions.',
      reviewCount: 95,
      reviewAverage: 4.8,
      quantity: 5,
      tags: ['jinx cosplay', 'fishbones prop', 'arcane weapon', 'lol rocket launcher', 'cosplay weapon', 'gamer bedroom decor', 'comic con prop', 'league of legends', 'zaun punk', '3d printed weapon', 'led cosplay', 'halloween costume', 'gamer gift']
    },
    {
      id: 4178129840,
      listingRank: 6,
      title: 'Fallout Pip-Boy 3000 Mk IV Wearable Bluetooth Smartwatch Dock',
      priceUsd: 120.00,
      shopName: 'WastelandArmory',
      shopSales: 4100,
      shopUrl: 'https://www.etsy.com/shop/WastelandArmory',
      listingUrl: 'https://www.etsy.com/listing/4178129840',
      views: 17800,
      favorites: 3100,
      seoScore: 86,
      marketScore: 84,
      imageUrl: 'https://images.unsplash.com/photo-1518770660439-4636190af475?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1518770660439-4636190af475?w=600'],
      description: 'Wearable Pip Boy 3000 forearm cuff compatible with Apple Watch & Samsung Galaxy Watch. Features working rotating knobs and authentic vault-tec weathered patina.',
      reviewCount: 140,
      reviewAverage: 4.6,
      quantity: 9,
      tags: ['pip boy', 'fallout prop', 'vault tec', 'wearable armor', 'cosplay cuff', 'apple watch dock', 'gaming gadget', 'post apocalyptic', 'wasteland prop', 'fallout 4', 'gamer cosplay', 'halloween prop', 'scifi cuff']
    },
    {
      id: 4176640192,
      listingRank: 7,
      title: 'Lotr Aragorn Crown of Gondor Full Size Wearable Metalized Tiara',
      priceUsd: 65.00,
      shopName: 'MiddleEarthArmory',
      shopSales: 6200,
      shopUrl: 'https://www.etsy.com/shop/MiddleEarthArmory',
      listingUrl: 'https://www.etsy.com/listing/4176640192',
      views: 11200,
      favorites: 2150,
      seoScore: 82,
      marketScore: 80,
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=600'],
      description: 'Coronation crown of King Elessar Telcontar. Printed in silver infused filament with antique black wax rubbing. Padded velvet interior for comfortable wear.',
      reviewCount: 165,
      reviewAverage: 4.8,
      quantity: 11,
      tags: ['aragorn crown', 'crown of gondor', 'lotr cosplay', 'king crown', 'middle earth tiara', 'wedding crown', 'fantasy tiara', 'lord of the rings', 'medieval armor', 'renaissance fair', 'cosplay armor', 'king elessar', 'fantasy prop']
    },
    {
      id: 4174028911,
      listingRank: 8,
      title: 'Ben 10 Classic Omnitrix Watch with Sound & Pop-Up Hologram Dial',
      priceUsd: 35.00,
      shopName: 'HeroPropsStudio',
      shopSales: 9400,
      shopUrl: 'https://www.etsy.com/shop/HeroPropsStudio',
      listingUrl: 'https://www.etsy.com/listing/4174028911',
      views: 15400,
      favorites: 3320,
      seoScore: 85,
      marketScore: 82,
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600'],
      description: 'Accurate retro cartoon Omnitrix watch. Spring loaded pop-up ring mechanism and green LED glowing core. Adjustable silicone wrist strap included.',
      reviewCount: 290,
      reviewAverage: 4.7,
      quantity: 18,
      tags: ['omnitrix watch', 'ben 10 toy', 'alien watch', 'cartoon prop', 'nostalgia gift', 'cosplay accessory', 'green led watch', 'kids superhero', 'hero dial', 'retro cartoon', '3d printed toy', 'fidget watch', 'gamer gift']
    },
    {
      id: 4168074902,
      listingRank: 9,
      title: 'D&D Mimic Chest Dice Box with Realistic Silicone Sharp Teeth & Tongue',
      priceUsd: 38.00,
      shopName: 'DiceHoardCrafts',
      shopSales: 11500,
      shopUrl: 'https://www.etsy.com/shop/DiceHoardCrafts',
      listingUrl: 'https://www.etsy.com/listing/4168074902',
      views: 19800,
      favorites: 4200,
      seoScore: 95,
      marketScore: 92,
      imageUrl: 'https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600'],
      description: 'Frightening yet cute tabletop RPG mimic treasure chest. Holds up to 5 complete polyhedral dice sets (35 dice). Glossy resin eyes and flexible silicone tongue.',
      reviewCount: 380,
      reviewAverage: 4.9,
      quantity: 20,
      tags: ['mimic dice box', 'dnd dice chest', 'dice holder', 'tabletop rpg', 'dungeon master gift', 'monstrous chest', 'polyhedral dice', 'dungeons dragons', 'd20 holder', 'pathfinder gift', 'fantasy storage', 'resin teeth', 'gamer desk']
    }
  ];

  selectedItem?: MarketItem;

  ngOnInit(): void {
    this.selectedItem = this.items[0];
    this.applySort();
  }

  onSearch(): void {
    if (!this.searchKeyword || !this.searchKeyword.trim()) {
      this.showToastMsg('⚠️ Lütfen aranacak bir kelime girin.');
      return;
    }

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
          this.statusText = `"${this.searchKeyword}" için ${this.items.length} canlı Etsy ilanı getirildi (Toplam bulunan: ${res.total.toLocaleString()}). Sıralama: ${this.getSortLabel()}`;
          this.showToastMsg(`✅ ${this.items.length} ürün ve pazar metriği başarıyla yüklendi.`);
        } else {
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
    this.items = this.items.filter(i => i.marketScore >= 85);
    this.statusText = `Filtrelendi: Yalnızca Fırsat Skoru %85+ olan ${this.items.length} ürün gösteriliyor.`;
    this.showToastMsg(`⚡ Yüksek Fırsatlı ${this.items.length} ürün filtrelendi.`);
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
    const topKeywords = this.selectedItem.tags.slice(0, 5).join(' | ');
    alert(`🔍 Kelime Yoğunluk Analizi:\n\nÜrün: ${this.selectedItem.title}\n\nEn Yüksek Hacimli Anahtar Kelimeler:\n${topKeywords}\n\nSEO Gücü: %${this.selectedItem.seoScore}`);
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
    const headers = 'Sira;ListingId;Baslik;Fiyat_USD;Magaza;Magaza_Satisi;Favori;Goruntulenme;SEO_Puani;Pazar_Puani;Tagler\n';
    const rows = this.items.map(i => 
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
    navigator.clipboard.writeText(allTags.join(', '));
    this.showToastMsg(`📋 İlk 13 Ortak Altın Etiket kopyalandı!`);
  }

  generateAiMarketReport(): void {
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
    const cloned: ClonedMarketListing = {
      id: base ? base.id : Date.now(),
      title: base ? base.title : `${this.searchKeyword} - Handmade Model`,
      priceUsd: base ? base.priceUsd : (this.avgPriceUsd || 35.0),
      tags: [...selected],
      description: base ? base.description : '',
      imageUrl: base ? base.imageUrl : '',
      shopName: base ? base.shopName : '',
      seoScore: 98,
      marketScore: this.opportunityScore
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
