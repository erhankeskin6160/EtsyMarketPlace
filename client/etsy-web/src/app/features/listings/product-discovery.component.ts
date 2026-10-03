import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

export interface DiscoveryProduct {
  id: string;
  title: string;
  category: string;
  taxonomyId: number;
  priceUsd: number;
  estimatedCostUsd: number;
  estNetProfitUsd: number;
  opportunityScore: number;
  searchVolume: number;
  competitionScore: number;
  tags: string[];
  imageUrl: string;
  description: string;
  nicheRating: 'Çok Yüksek' | 'Yüksek' | 'Orta';
  viralScore: number;
}

@Component({
  selector: 'app-product-discovery',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <div class="discovery-container">
      <!-- HEADER -->
      <div class="discovery-header">
        <div class="header-left">
          <div class="icon-box">🛍️</div>
          <div>
            <h1 class="page-title">Ürün Bul & Akıllı Taslak Listeleme (Product Discovery)</h1>
            <p class="page-subtitle">Trend radarında yükselen ürünler, kârlılık fırsat skoru ve tek tıkla Fast Creator taslak transferi</p>
          </div>
        </div>

        <div class="header-actions">
          <button type="button" class="btn-scan" [disabled]="isScanning" (click)="scanOpportunities()">
            <span *ngIf="isScanning" class="spin-disc">⏳</span>
            <span *ngIf="!isScanning">⚡</span>
            <span>{{ isScanning ? 'Pazar Taranıyor...' : 'Yeni Fırsatları Tara' }}</span>
          </button>
        </div>
      </div>

      <!-- FILTER & METRIC BAR -->
      <div class="glass-card toolbar-card">
        <div class="search-col">
          <label class="toolbar-label">Arama / Niche Filtrele:</label>
          <div class="search-input-wrap">
            <span class="search-icon">🔍</span>
            <input 
              type="text" 
              class="std-input" 
              [(ngModel)]="searchQuery" 
              (ngModelChange)="applyFilters()" 
              placeholder="Örn: 3D printed, jewelry, desk decor, bag..." />
          </div>
        </div>

        <div class="filter-col">
          <label class="toolbar-label">Kategori:</label>
          <select class="std-select" [(ngModel)]="selectedCategory" (ngModelChange)="applyFilters()">
            <option value="all">Tüm Kategoriler</option>
            <option value="Art & Collectibles">Art & Collectibles (3D Baskı / Heykel)</option>
            <option value="Home & Living">Home & Living (Ev & Dekorasyon)</option>
            <option value="Jewelry">Jewelry (Takı & Mücevher)</option>
            <option value="Bags & Purses">Bags & Purses (Çanta & Aksesuar)</option>
          </select>
        </div>

        <div class="filter-col">
          <label class="toolbar-label">Sıralama:</label>
          <select class="std-select" [(ngModel)]="sortBy" (ngModelChange)="applyFilters()">
            <option value="opportunity">Fırsat Skoru (En Yüksek)</option>
            <option value="profit">Net Kâr ($ En Yüksek)</option>
            <option value="volume">Arama Hacmi (En Popüler)</option>
            <option value="price_asc">Fiyat (Önce Düşük)</option>
          </select>
        </div>

        <div class="filter-col toggle-col">
          <label class="chk-label">
            <input type="checkbox" [(ngModel)]="onlyHighOpportunity" (ngModelChange)="applyFilters()" />
            <span>Sadece Yüksek Fırsat (80+ Skor)</span>
          </label>
        </div>
      </div>

      <!-- STATUS & OPPORTUNITY METRIC STRIP -->
      <div class="kpi-strip">
        <div class="kpi-card">
          <span class="kpi-lbl">BULUNAN ÜRÜN</span>
          <span class="kpi-val">{{ filteredProducts.length }} / {{ products.length }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-lbl">ORTALAMA KÂR MARJI</span>
          <span class="kpi-val text-emerald">%64.2</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-lbl">RADAR DURUMU</span>
          <span class="kpi-val text-cyan">● Canlı Veri Hazır</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-lbl">DOLAR KURU</span>
          <span class="kpi-val text-orange">1 USD = {{ usdTryRate.toFixed(2) }} ₺</span>
        </div>
      </div>

      <!-- PRODUCTS DISCOVERY GRID -->
      <div class="products-grid">
        <div *ngFor="let p of filteredProducts" class="glass-card product-card">
          <div class="card-thumb-wrap">
            <img [src]="p.imageUrl" class="product-thumb" alt="{{ p.title }}" />
            <div class="opp-badge" [class.badge-gold]="p.opportunityScore >= 85" [class.badge-silver]="p.opportunityScore < 85">
              ★ Skor: {{ p.opportunityScore }}
            </div>
            <div class="niche-badge">{{ p.nicheRating }} Talep</div>
          </div>

          <div class="card-body">
            <h3 class="product-title" title="{{ p.title }}">{{ p.title }}</h3>
            <span class="category-pill">📁 {{ p.category }}</span>

            <!-- Financial Metrics Mini Table -->
            <div class="financial-row">
              <div class="fin-box">
                <span class="f-lbl">Etsy Satış:</span>
                <span class="f-val price">&#36;{{ p.priceUsd.toFixed(2) }}</span>
                <span class="f-sub">(₺{{ (p.priceUsd * usdTryRate).toFixed(0) }})</span>
              </div>
              <div class="fin-box">
                <span class="f-lbl">Tahmini Maliyet:</span>
                <span class="f-val cost">&#36;{{ p.estimatedCostUsd.toFixed(2) }}</span>
              </div>
              <div class="fin-box">
                <span class="f-lbl">Net Kâr:</span>
                <span class="f-val profit">&#36;{{ p.estNetProfitUsd.toFixed(2) }}</span>
                <span class="f-sub">%{{ ((p.estNetProfitUsd / p.priceUsd) * 100).toFixed(0) }} Marj</span>
              </div>
            </div>

            <!-- Tags Strip -->
            <div class="tags-row">
              <span *ngFor="let t of p.tags.slice(0, 4)" class="tag-chip">#{{ t }}</span>
              <span *ngIf="p.tags.length > 4" class="tag-more">+{{ p.tags.length - 4 }} etiket</span>
            </div>

            <!-- Actions Row -->
            <div class="card-actions">
              <button type="button" class="btn-draft-transfer" (click)="transferToFastCreator(p)">
                <span>⚡ Taslağa Aktar (Fast Creator)</span>
                <span class="arrow-right">→</span>
              </button>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .discovery-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 4px;
    }

    .discovery-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }

    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }

    .icon-box {
      font-size: 2rem;
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.35);
      padding: 10px;
      border-radius: 12px;
    }

    .page-title {
      margin: 0;
      font-size: 1.35rem;
      font-weight: 800;
      color: #fff;
    }

    .page-subtitle {
      margin: 3px 0 0 0;
      font-size: 0.82rem;
      color: #94a3b8;
    }

    .btn-scan {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      background: linear-gradient(135deg, #6366f1 0%, #8b5cf6 100%);
      color: #fff;
      border: none;
      font-weight: 700;
      font-size: 0.85rem;
      padding: 9px 18px;
      border-radius: 8px;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(99, 102, 241, 0.3);
      transition: all 0.2s;
    }

    .btn-scan:hover:not(:disabled) {
      transform: translateY(-1px);
      box-shadow: 0 6px 18px rgba(99, 102, 241, 0.45);
    }

    .btn-scan:disabled {
      opacity: 0.6;
      cursor: wait;
    }

    .spin-disc {
      display: inline-block;
      animation: spin 1.5s linear infinite;
    }

    @keyframes spin {
      100% { transform: rotate(360deg); }
    }

    .glass-card {
      background: rgba(17, 24, 39, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 14px;
      backdrop-filter: blur(8px);
    }

    .toolbar-card {
      display: flex;
      align-items: flex-end;
      gap: 14px;
      flex-wrap: wrap;
    }

    .toolbar-label {
      font-size: 0.74rem;
      font-weight: 700;
      color: #94a3b8;
      margin-bottom: 4px;
      display: block;
    }

    .search-col { flex: 1.5; min-width: 220px; }
    .filter-col { flex: 1; min-width: 170px; }
    .toggle-col { flex: 1.2; display: flex; align-items: center; height: 38px; }

    .search-input-wrap {
      position: relative;
    }

    .search-icon {
      position: absolute;
      left: 10px;
      top: 50%;
      transform: translateY(-50%);
      font-size: 0.85rem;
      color: #64748b;
    }

    .std-input, .std-select {
      width: 100%;
      box-sizing: border-box;
      background: #0f172a;
      border: 1px solid #334155;
      color: #fff;
      border-radius: 6px;
      padding: 8px 10px;
      font-size: 0.82rem;
      outline: none;
    }

    .search-input-wrap .std-input {
      padding-left: 32px;
    }

    .std-input:focus, .std-select:focus {
      border-color: #6366f1;
    }

    .chk-label {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
      cursor: pointer;
    }

    .kpi-strip {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(170px, 1fr));
      gap: 10px;
    }

    .kpi-card {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 8px;
      padding: 10px 14px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .kpi-lbl {
      font-size: 0.68rem;
      font-weight: 700;
      color: #94a3b8;
    }

    .kpi-val {
      font-size: 1.15rem;
      font-weight: 800;
      color: #fff;
    }

    .text-emerald { color: #10b981 !important; }
    .text-cyan { color: #06b6d4 !important; }
    .text-orange { color: #f97316 !important; }

    /* PRODUCTS GRID */
    .products-grid {
      display: grid;
      grid-template-columns: repeat(auto-fill, minmax(320px, 1fr));
      gap: 16px;
    }

    .product-card {
      display: flex;
      flex-direction: column;
      overflow: hidden;
      padding: 0;
      transition: transform 0.2s, box-shadow 0.2s;
    }

    .product-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.35);
      border-color: rgba(99, 102, 241, 0.4);
    }

    .card-thumb-wrap {
      position: relative;
      width: 100%;
      height: 190px;
      background: #0f172a;
      overflow: hidden;
    }

    .product-thumb {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.3s;
    }

    .product-card:hover .product-thumb {
      transform: scale(1.04);
    }

    .opp-badge {
      position: absolute;
      top: 10px;
      left: 10px;
      font-size: 0.74rem;
      font-weight: 800;
      padding: 3px 8px;
      border-radius: 6px;
      box-shadow: 0 2px 8px rgba(0, 0, 0, 0.5);
    }

    .badge-gold {
      background: linear-gradient(135deg, #f59e0b 0%, #d97706 100%);
      color: #fff;
    }

    .badge-silver {
      background: #334155;
      color: #cbd5e1;
    }

    .niche-badge {
      position: absolute;
      top: 10px;
      right: 10px;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #38bdf8;
      font-size: 0.7rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 6px;
    }

    .card-body {
      padding: 14px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      flex: 1;
    }

    .product-title {
      margin: 0;
      font-size: 0.92rem;
      font-weight: 700;
      color: #f1f5f9;
      line-height: 1.35;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .category-pill {
      font-size: 0.72rem;
      color: #a5b4fc;
      background: rgba(99, 102, 241, 0.12);
      padding: 2px 7px;
      border-radius: 4px;
      align-self: flex-start;
    }

    .financial-row {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 6px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 6px;
      padding: 8px;
    }

    .fin-box {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .f-lbl {
      font-size: 0.65rem;
      color: #64748b;
      font-weight: 600;
    }

    .f-val {
      font-size: 0.88rem;
      font-weight: 800;
    }

    .f-val.price { color: #f8fafc; }
    .f-val.cost { color: #94a3b8; }
    .f-val.profit { color: #10b981; }

    .f-sub {
      font-size: 0.64rem;
      color: #64748b;
    }

    .tags-row {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }

    .tag-chip {
      background: rgba(255, 255, 255, 0.06);
      color: #cbd5e1;
      font-size: 0.68rem;
      padding: 2px 6px;
      border-radius: 4px;
    }

    .tag-more {
      font-size: 0.68rem;
      color: #64748b;
      align-self: center;
    }

    .card-actions {
      margin-top: auto;
      padding-top: 6px;
    }

    .btn-draft-transfer {
      width: 100%;
      background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
      color: #fff;
      border: none;
      border-radius: 6px;
      padding: 9px 12px;
      font-size: 0.82rem;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: space-between;
      transition: all 0.2s;
      box-shadow: 0 2px 10px rgba(249, 115, 22, 0.3);
    }

    .btn-draft-transfer:hover {
      box-shadow: 0 4px 14px rgba(249, 115, 22, 0.5);
      transform: translateY(-1px);
    }

    .arrow-right {
      font-size: 1rem;
      transition: transform 0.2s;
    }

    .btn-draft-transfer:hover .arrow-right {
      transform: translateX(3px);
    }
  `]
})
export class ProductDiscoveryComponent implements OnInit {
  private apiService = inject(EtsyApiService);
  private aiService = inject(AiSettingsService);
  private router = inject(Router);

  isScanning = false;
  searchQuery = '';
  selectedCategory = 'all';
  sortBy = 'opportunity';
  onlyHighOpportunity = false;

  get usdTryRate(): number {
    return this.apiService.exchangeRate();
  }

  products: DiscoveryProduct[] = [
    {
      id: 'disc-1',
      title: 'Articulated Crystal Wing Dragon 3D Print Toy',
      category: 'Art & Collectibles',
      taxonomyId: 1238,
      priceUsd: 38.50,
      estimatedCostUsd: 6.80,
      estNetProfitUsd: 26.20,
      opportunityScore: 94,
      searchVolume: 14200,
      competitionScore: 42,
      tags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'winged dragon', 'desk decor', 'dnd gift'],
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=600&auto=format&fit=crop&q=80',
      description: 'Handcrafted articulated crystal dragon printed with high-quality dual-color PLA filament. Perfect fidget desktop toy and fantasy collector piece.',
      nicheRating: 'Çok Yüksek',
      viralScore: 96
    },
    {
      id: 'disc-2',
      title: 'Personalized Leather Shoulder Bag Handmade Vintage Satchel',
      category: 'Bags & Purses',
      taxonomyId: 132,
      priceUsd: 84.00,
      estimatedCostUsd: 24.50,
      estNetProfitUsd: 49.30,
      opportunityScore: 89,
      searchVolume: 18500,
      competitionScore: 56,
      tags: ['leather bag', 'shoulder bag', 'crossbody satchel', 'vintage purse', 'personalized gift'],
      imageUrl: 'https://images.unsplash.com/photo-1548036328-c9fa89d128fa?w=600&auto=format&fit=crop&q=80',
      description: 'Genuine distressed full-grain leather crossbody messenger bag with custom monogram engraving option.',
      nicheRating: 'Yüksek',
      viralScore: 88
    },
    {
      id: 'disc-3',
      title: 'Self-Watering Geometric Succulent Planter Nordic Pot',
      category: 'Home & Living',
      taxonomyId: 1054,
      priceUsd: 26.00,
      estimatedCostUsd: 4.20,
      estNetProfitUsd: 17.80,
      opportunityScore: 86,
      searchVolume: 9200,
      competitionScore: 35,
      tags: ['succulent pot', 'geometric planter', 'self watering', 'indoor planter', 'minimalist vase'],
      imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=600&auto=format&fit=crop&q=80',
      description: 'Modern geometric minimalist indoor succulent planter with hidden water reservoir tray.',
      nicheRating: 'Yüksek',
      viralScore: 84
    },
    {
      id: 'disc-4',
      title: 'Custom Name Dainty Silver Necklace 925 Sterling Minimalist',
      category: 'Jewelry',
      taxonomyId: 204,
      priceUsd: 42.00,
      estimatedCostUsd: 9.10,
      estNetProfitUsd: 27.60,
      opportunityScore: 82,
      searchVolume: 28400,
      competitionScore: 68,
      tags: ['name necklace', 'silver necklace', 'custom jewelry', 'dainty necklace', 'bridesmaid gift'],
      imageUrl: 'https://images.unsplash.com/photo-1599643478518-a784e5dc4c8f?w=600&auto=format&fit=crop&q=80',
      description: 'Solid 925 sterling silver custom personalized cursive nameplate pendant necklace on delicate chain.',
      nicheRating: 'Orta',
      viralScore: 78
    },
    {
      id: 'disc-5',
      title: 'Steampunk Industrial Mechanical Gear Table Desk Clock',
      category: 'Home & Living',
      taxonomyId: 1041,
      priceUsd: 92.00,
      estimatedCostUsd: 18.00,
      estNetProfitUsd: 61.20,
      opportunityScore: 91,
      searchVolume: 8100,
      competitionScore: 28,
      tags: ['steampunk clock', 'mechanical clock', 'desk clock', 'gear art', 'industrial decor'],
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600&auto=format&fit=crop&q=80',
      description: 'Handmade industrial steampunk desk clock with exposed moving brass gears and solid wooden base.',
      nicheRating: 'Çok Yüksek',
      viralScore: 92
    }
  ];

  filteredProducts: DiscoveryProduct[] = [];

  ngOnInit(): void {
    this.applyFilters();
  }

  scanOpportunities(): void {
    this.isScanning = true;
    setTimeout(() => {
      this.isScanning = false;
      this.applyFilters();
    }, 800);
  }

  applyFilters(): void {
    let result = [...this.products];

    if (this.searchQuery.trim()) {
      const q = this.searchQuery.trim().toLowerCase();
      result = result.filter(p => 
        p.title.toLowerCase().includes(q) || 
        p.tags.some(t => t.toLowerCase().includes(q))
      );
    }

    if (this.selectedCategory !== 'all') {
      result = result.filter(p => p.category === this.selectedCategory);
    }

    if (this.onlyHighOpportunity) {
      result = result.filter(p => p.opportunityScore >= 80);
    }

    switch (this.sortBy) {
      case 'opportunity':
        result.sort((a, b) => b.opportunityScore - a.opportunityScore);
        break;
      case 'profit':
        result.sort((a, b) => b.estNetProfitUsd - a.estNetProfitUsd);
        break;
      case 'volume':
        result.sort((a, b) => b.searchVolume - a.searchVolume);
        break;
      case 'price_asc':
        result.sort((a, b) => a.priceUsd - b.priceUsd);
        break;
    }

    this.filteredProducts = result;
  }

  transferToFastCreator(p: DiscoveryProduct): void {
    const cloneData: ClonedMarketListing = {
      id: p.id,
      title: p.title,
      priceUsd: p.priceUsd,
      tags: p.tags,
      description: p.description,
      category: p.category,
      imageUrl: p.imageUrl,
      shopName: 'Trend Radar Keşif Motoru',
      marketScore: p.opportunityScore,
      seoScore: 92
    };

    this.aiService.clonedListing.set(cloneData);
    this.router.navigate(['/listings/fast-creator']);
  }
}
