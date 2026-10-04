import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface ViralModel {
  id: string;
  title: string;
  platform: 'MakerWorld' | 'Printables' | 'CrealityCloud';
  creator: string;
  downloads7d: number;
  likes: number;
  weightGrams: number;
  printHours: number;
  estCostUsd: number;
  etsyAvgPriceUsd: number;
  arbitrageMargin: number;
  isHotOpportunity: boolean;
  imageUrl: string;
  tags: string[];
}

@Component({
  selector: 'app-viral-3d',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="viral-view">
      <!-- HEADER -->
      <div class="viral-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 16V8a2 2 0 0 0-1-1.73l-7-4a2 2 0 0 0-2 0l-7 4A2 2 0 0 0 3 8v8a2 2 0 0 0 1 1.73l7 4a2 2 0 0 0 2 0l7-4A2 2 0 0 0 21 16z"></path>
              <polyline points="3.27 6.96 12 12.01 20.73 6.96"></polyline>
              <line x1="12" y1="22.08" x2="12" y2="12"></line>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Viral 3D Model Avcısı & Arbitraj Radarı</h1>
            <p class="page-subtitle">MakerWorld & Printables popüler modelleri, filament üretim maliyeti ve %200 - %500 Etsy arbitrajı</p>
          </div>
        </div>

        <div class="header-actions">
          <div class="arbitrage-badge">
            <span>🔥 Radar: {{ hotOpportunitiesCount }} Yüksek Fırsat Tespit Edildi</span>
          </div>
        </div>
      </div>

      <!-- PLATFORM & SEARCH FILTERS -->
      <div class="filter-strip">
        <div class="platform-tabs">
          <button class="p-tab" [class.active]="activePlatform === 'all'" (click)="activePlatform = 'all'">Tüm Platformlar ({{ models.length }})</button>
          <button class="p-tab" [class.active]="activePlatform === 'MakerWorld'" (click)="activePlatform = 'MakerWorld'">MakerWorld</button>
          <button class="p-tab" [class.active]="activePlatform === 'Printables'" (click)="activePlatform = 'Printables'">Printables</button>
          <button class="p-tab" [class.active]="activePlatform === 'CrealityCloud'" (click)="activePlatform = 'CrealityCloud'">CrealityCloud</button>
        </div>

        <div class="search-wrap">
          <input type="text" [(ngModel)]="searchQuery" placeholder="Model, kategori veya tasarımcı ara..." class="search-input" />
        </div>
      </div>

      <!-- MAIN GRID OF 3D MODELS -->
      <div class="models-grid">
        <div 
          *ngFor="let m of filteredModels" 
          class="model-card"
          [class.hot-card]="m.isHotOpportunity">
          
          <div class="model-thumb-wrap">
            <img [src]="m.imageUrl" class="model-img" alt="3D Model" />
            <span class="platform-tag" [ngClass]="m.platform.toLowerCase()">{{ m.platform }}</span>
            <span *ngIf="m.isHotOpportunity" class="hot-badge">★ %{{ m.arbitrageMargin }} KÂR</span>
          </div>

          <div class="model-info">
            <h3 class="model-title">{{ m.title }}</h3>
            <span class="model-creator">Tasarımcı: {{ m.creator }}</span>

            <div class="model-stats">
              <span>📥 {{ m.downloads7d | number }} İndirme (7g)</span>
              <span>❤️ {{ m.likes | number }} Beğeni</span>
            </div>

            <!-- ARBITRAGE FINANCIAL MATRIX -->
            <div class="arbitrage-matrix">
              <div class="matrix-item">
                <span class="m-label">Filament (Gram)</span>
                <span class="m-val">{{ m.weightGrams }}g ({{ m.printHours }}s)</span>
              </div>
              <div class="matrix-item">
                <span class="m-label">Üretim Maliyeti</span>
                <span class="m-val cost">&#36;{{ m.estCostUsd.toFixed(2) }}</span>
              </div>
              <div class="matrix-item">
                <span class="m-label">Etsy Piyasa Fiyatı</span>
                <span class="m-val price">&#36;{{ m.etsyAvgPriceUsd.toFixed(2) }}</span>
              </div>
            </div>

            <!-- NET PROFIT BADGE -->
            <div class="net-arbitrage-banner">
              <div>
                <span class="arb-label">Tahmini Net Kâr:</span>
                <span class="arb-val">&#36;{{ (m.etsyAvgPriceUsd - m.estCostUsd - (m.etsyAvgPriceUsd * 0.1)).toFixed(2) }}</span>
                <span class="arb-try">(₺{{ ((m.etsyAvgPriceUsd - m.estCostUsd - (m.etsyAvgPriceUsd * 0.1)) * usdTryRate).toFixed(2) }})</span>
              </div>
              <span class="margin-pill">+%{{ m.arbitrageMargin }}</span>
            </div>

            <button class="btn-create-listing" (click)="createListingFromModel(m)">
              ✨ Bu Modeli Doğrudan Etsy İlanına Dönüştür
            </button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .viral-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .viral-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(245, 158, 11, 0.2), rgba(239, 68, 68, 0.2));
      border: 1px solid rgba(245, 158, 11, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #f59e0b;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .arbitrage-badge {
      padding: 8px 16px;
      background: rgba(245, 158, 11, 0.15);
      border: 1px solid rgba(245, 158, 11, 0.35);
      color: #fbbf24;
      border-radius: 20px;
      font-weight: 700;
      font-size: 0.85rem;
    }

    .filter-strip {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: rgba(30, 41, 59, 0.4);
      padding: 8px 14px;
      border-radius: 10px;
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .platform-tabs {
      display: flex;
      gap: 6px;
    }
    .p-tab {
      padding: 6px 14px;
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 0.82rem;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .p-tab.active {
      background: #6366f1;
      color: #fff;
      font-weight: 600;
    }
    .search-input {
      width: 280px;
      padding: 7px 12px;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 6px;
      color: #fff;
      font-size: 0.82rem;
      outline: none;
    }

    .models-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 20px;
    }
    .model-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      transition: all 0.2s;
    }
    .model-card:hover {
      transform: translateY(-2px);
      border-color: rgba(255, 255, 255, 0.2);
    }
    .model-card.hot-card {
      border-color: rgba(245, 158, 11, 0.5);
      box-shadow: 0 0 20px rgba(245, 158, 11, 0.12);
    }
    .model-thumb-wrap {
      position: relative;
      height: 200px;
      background: #0f172a;
    }
    .model-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .platform-tag {
      position: absolute;
      top: 10px;
      left: 10px;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 0.72rem;
      font-weight: 700;
      background: rgba(15, 23, 42, 0.85);
      color: #f1f5f9;
      border: 1px solid rgba(255, 255, 255, 0.15);
    }
    .hot-badge {
      position: absolute;
      top: 10px;
      right: 10px;
      padding: 3px 8px;
      border-radius: 4px;
      font-size: 0.72rem;
      font-weight: 700;
      background: #f59e0b;
      color: #000;
    }

    .model-info {
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      flex: 1;
    }
    .model-title {
      font-size: 0.95rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
      line-height: 1.35;
    }
    .model-creator {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .model-stats {
      display: flex;
      justify-content: space-between;
      font-size: 0.75rem;
      color: #cbd5e1;
    }

    .arbitrage-matrix {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
      padding: 10px;
      background: rgba(15, 23, 42, 0.6);
      border-radius: 8px;
    }
    .matrix-item {
      display: flex;
      flex-direction: column;
    }
    .m-label { font-size: 0.68rem; color: #94a3b8; }
    .m-val { font-size: 0.85rem; font-weight: 700; color: #f1f5f9; margin-top: 2px; }
    .m-val.cost { color: #f87171; }
    .m-val.price { color: #34d399; }

    .net-arbitrage-banner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 12px;
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: 8px;
    }
    .arb-label { font-size: 0.72rem; color: #94a3b8; display: block; }
    .arb-val { font-size: 1rem; font-weight: 700; color: #34d399; }
    .arb-try { font-size: 0.75rem; color: #94a3b8; margin-left: 4px; }
    .margin-pill {
      background: #10b981;
      color: #000;
      padding: 2px 8px;
      border-radius: 6px;
      font-weight: 800;
      font-size: 0.78rem;
    }

    .btn-create-listing {
      padding: 10px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-weight: 700;
      font-size: 0.82rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-create-listing:hover {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }

    @media (max-width: 1200px) {
      .models-grid { grid-template-columns: repeat(2, 1fr); }
    }
    @media (max-width: 768px) {
      .models-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class Viral3DComponent implements OnInit {
  activePlatform: 'all' | 'MakerWorld' | 'Printables' | 'CrealityCloud' = 'all';
  searchQuery: string = '';

  models: ViralModel[] = [
    {
      id: 'vm-1',
      title: 'Articulated Crystal Dragon (Mafsallı Ejderha)',
      platform: 'MakerWorld',
      creator: 'Cinderwing3D',
      downloads7d: 14820,
      likes: 6420,
      weightGrams: 145,
      printHours: 5.5,
      estCostUsd: 4.80,
      etsyAvgPriceUsd: 38.50,
      arbitrageMargin: 420,
      isHotOpportunity: true,
      imageUrl: 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=600&auto=format&fit=crop&q=80',
      tags: ['crystal dragon', 'articulated dragon', 'fidget toy', 'bambu lab print']
    },
    {
      id: 'vm-2',
      title: 'Minimalist Geometrik Kendinden Drenajlı Saksı',
      platform: 'Printables',
      creator: 'DesignKreativ',
      downloads7d: 9430,
      likes: 4120,
      weightGrams: 90,
      printHours: 3.2,
      estCostUsd: 2.80,
      etsyAvgPriceUsd: 26.00,
      arbitrageMargin: 360,
      isHotOpportunity: true,
      imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=600&auto=format&fit=crop&q=80',
      tags: ['planter pot', 'geometric vase', 'indoor planter', 'succulent pot']
    },
    {
      id: 'vm-3',
      title: 'Steampunk Mekanik Dişli Masa Saati Gövdesi',
      platform: 'MakerWorld',
      creator: 'MechanicArt',
      downloads7d: 8200,
      likes: 3890,
      weightGrams: 280,
      printHours: 11.0,
      estCostUsd: 8.50,
      etsyAvgPriceUsd: 89.00,
      arbitrageMargin: 490,
      isHotOpportunity: true,
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600&auto=format&fit=crop&q=80',
      tags: ['steampunk clock', 'gear clock', 'desktop clock', 'mechanical art']
    },
    {
      id: 'vm-4',
      title: 'Modüler Bal Peteği Duvar Rafı & Düzenleyici',
      platform: 'CrealityCloud',
      creator: 'PolyGrid',
      downloads7d: 5410,
      likes: 2190,
      weightGrams: 110,
      printHours: 4.0,
      estCostUsd: 3.50,
      etsyAvgPriceUsd: 24.50,
      arbitrageMargin: 280,
      isHotOpportunity: false,
      imageUrl: 'https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=600&auto=format&fit=crop&q=80',
      tags: ['honeycomb shelf', 'modular organizer', 'wall decor', '3d storage']
    }
  ];

  constructor(
    public etsyApi: EtsyApiService,
    private router: Router
  ) {}

  ngOnInit(): void {}

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  get hotOpportunitiesCount(): number {
    return this.models.filter(m => m.isHotOpportunity).length;
  }

  get filteredModels(): ViralModel[] {
    return this.models.filter(m => {
      if (this.activePlatform !== 'all' && m.platform !== this.activePlatform) return false;
      if (this.searchQuery.trim()) {
        const q = this.searchQuery.toLowerCase();
        return m.title.toLowerCase().includes(q) || m.creator.toLowerCase().includes(q);
      }
      return true;
    });
  }

  createListingFromModel(model: ViralModel): void {
    this.router.navigate(['/listings/fast-creator'], {
      queryParams: {
        title: model.title,
        price: model.etsyAvgPriceUsd,
        tags: model.tags.join(','),
        imageUrl: model.imageUrl,
        category: 'Art & Collectibles > Sculptures'
      }
    });
  }
}
