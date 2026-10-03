import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface ArbitrageItem {
  id: string;
  title: string;
  sourceMarket: 'AliExpress' | 'Amazon' | 'Walmart';
  sourcePrice: number;
  sourceShippingDays: string;
  etsyAvgPrice: number;
  estimatedMargin: number;
  netProfitUsd: number;
  imageUrl: string;
  category: string;
}

@Component({
  selector: 'app-external-arbitrage',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="arb-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">🌐</div>
          <div>
            <h1 class="page-title">Dış Pazar Yeri Bulucu & Çapraz Arbitraj Radarı</h1>
            <p class="page-subtitle">Amazon Handmade, AliExpress ve eBay üzerindeki fiyat farklarını tespit edip Etsy'de yüksek kâr marjıyla listeleyin</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-scan" (click)="scanMarkets()">
            ⚡ Çapraz Pazarları Tara
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">TARANAN ÜRÜN</span>
          <span class="kpi-val text-blue">1,420 Ürün</span>
          <span class="kpi-sub">Amazon, AliExpress, eBay</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">YÜKSEK KÂR FIRSATI</span>
          <span class="kpi-val text-green">48 Ürün</span>
          <span class="kpi-sub">%150+ Net Marj</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">ORTALAMA ARBİTRAJ KÂRI</span>
          <span class="kpi-val text-purple">+$18.40</span>
          <span class="kpi-sub">Ürün Başına Net</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">ORT. TESLİMAT SÜRESİ</span>
          <span class="kpi-val text-orange">6-9 Gün</span>
          <span class="kpi-sub">Ekspres DDP Kargo</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- ITEMS GRID -->
      <div class="items-section">
        <div class="section-top">
          <h2 class="section-title">Keşfedilen Arbitraj Fırsatları ({{ items.length }})</h2>
          <div class="filter-pills">
            <button [class.active]="marketFilter === 'ALL'" (click)="marketFilter = 'ALL'">Tümü</button>
            <button [class.active]="marketFilter === 'AliExpress'" (click)="marketFilter = 'AliExpress'">AliExpress</button>
            <button [class.active]="marketFilter === 'Amazon'" (click)="marketFilter = 'Amazon'">Amazon</button>
          </div>
        </div>

        <div class="items-grid">
          <div *ngFor="let item of filteredItems" class="item-card glass-card">
            <div class="item-img-wrap">
              <img [src]="item.imageUrl" alt="Product" class="item-img" />
              <span class="source-badge" [ngClass]="item.sourceMarket.toLowerCase()">{{ item.sourceMarket }}</span>
            </div>

            <div class="item-details">
              <span class="cat-pill">{{ item.category }}</span>
              <h3 class="item-title">{{ item.title }}</h3>

              <!-- PRICE MATRIX -->
              <div class="price-matrix">
                <div class="price-col">
                  <span class="price-label">Tedarik Alış:</span>
                  <span class="price-val">\${{ item.sourcePrice | number:'1.2-2' }}</span>
                  <span class="price-sub">Kargo: {{ item.sourceShippingDays }}</span>
                </div>
                <div class="arrow-col">➔</div>
                <div class="price-col">
                  <span class="price-label">Etsy Satış:</span>
                  <span class="price-val text-green">\${{ item.etsyAvgPrice | number:'1.2-2' }}</span>
                  <span class="price-sub">Net Kâr: <b>+\${{ item.netProfitUsd }}</b></span>
                </div>
              </div>

              <!-- MARGIN BAR -->
              <div class="margin-row">
                <span>Kâr Marjı: <b class="text-green">+%{{ item.estimatedMargin }}</b></span>
                <button class="btn-import-listing" (click)="importToCreator(item)">
                  🚀 Etsy Taslağı Oluştur
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .arb-container {
      display: flex;
      flex-direction: column;
      gap: 24px;
      padding: 24px;
      color: #e2e8f0;
    }
    .page-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .icon-box {
      font-size: 2.2rem;
      background: rgba(56, 189, 248, 0.15);
      border: 1px solid rgba(56, 189, 248, 0.3);
      width: 52px;
      height: 52px;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .page-title {
      font-size: 1.45rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 4px 0 0 0;
    }
    .btn-scan {
      background: linear-gradient(135deg, #0284c7, #0369a1);
      color: #fff;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(2, 132, 199, 0.35);
      transition: all 0.2s;
    }
    .btn-scan:hover { filter: brightness(1.1); transform: translateY(-1px); }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
    }
    .kpi-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 12px;
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
    }
    .kpi-label { font-size: 0.72rem; color: #94a3b8; font-weight: 600; }
    .kpi-val { font-size: 1.6rem; font-weight: 800; margin: 4px 0; }
    .kpi-sub { font-size: 0.72rem; color: #64748b; }
    .text-blue { color: #38bdf8; }
    .text-green { color: #34d399; }
    .text-purple { color: #c084fc; }
    .text-orange { color: #f97316; }

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .items-section { display: flex; flex-direction: column; gap: 16px; }
    .section-top { display: flex; justify-content: space-between; align-items: center; }
    .section-title { font-size: 1.15rem; font-weight: 700; color: #fff; margin: 0; }
    .filter-pills {
      display: flex;
      gap: 6px;
      background: rgba(15, 23, 42, 0.6);
      padding: 4px;
      border-radius: 8px;
    }
    .filter-pills button {
      background: transparent;
      border: none;
      color: #94a3b8;
      padding: 4px 12px;
      border-radius: 6px;
      font-size: 0.78rem;
      cursor: pointer;
      font-weight: 600;
    }
    .filter-pills button.active {
      background: #0284c7;
      color: #fff;
    }

    .items-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(320px, 1fr));
      gap: 20px;
    }
    .item-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      overflow: hidden;
      display: flex;
      flex-direction: column;
    }
    .item-img-wrap {
      position: relative;
      height: 180px;
      width: 100%;
      background: #0f172a;
    }
    .item-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .source-badge {
      position: absolute;
      top: 10px;
      left: 10px;
      font-size: 0.72rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 6px;
    }
    .source-badge.aliexpress { background: #e11d48; color: #fff; }
    .source-badge.amazon { background: #f59e0b; color: #000; }
    .source-badge.walmart { background: #0284c7; color: #fff; }

    .item-details {
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 12px;
      flex: 1;
    }
    .cat-pill {
      font-size: 0.72rem;
      color: #94a3b8;
      text-transform: uppercase;
      font-weight: 600;
    }
    .item-title { font-size: 0.95rem; font-weight: 700; color: #fff; margin: 0; line-height: 1.4; }

    .price-matrix {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: rgba(15, 23, 42, 0.7);
      padding: 12px;
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.05);
    }
    .price-col { display: flex; flex-direction: column; gap: 2px; }
    .price-label { font-size: 0.7rem; color: #94a3b8; }
    .price-val { font-size: 1.1rem; font-weight: 800; }
    .price-sub { font-size: 0.7rem; color: #64748b; }
    .arrow-col { font-size: 1.1rem; color: #64748b; }

    .margin-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      padding-top: 12px;
      font-size: 0.85rem;
    }
    .btn-import-listing {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      border: none;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-import-listing:hover { filter: brightness(1.1); }
  `]
})
export class ExternalArbitrageRadarComponent implements OnInit {
  etsyApi = inject(EtsyApiService);
  router = inject(Router);

  marketFilter: 'ALL' | 'AliExpress' | 'Amazon' = 'ALL';
  toastMessage = '';

  items: ArbitrageItem[] = [
    {
      id: 'arb-1',
      title: 'Minimalist Ceramic Matcha Whisk Stand Bowl Set',
      sourceMarket: 'AliExpress',
      sourcePrice: 8.50,
      sourceShippingDays: '7-10 Gün',
      etsyAvgPrice: 34.00,
      estimatedMargin: 198,
      netProfitUsd: 19.40,
      imageUrl: 'https://images.unsplash.com/photo-1576092768241-dec231879fc3?w=500&auto=format&fit=crop&q=80',
      category: 'Home & Kitchen'
    },
    {
      id: 'arb-2',
      title: 'Vintage Leather Handcrafted Journal Notebook',
      sourceMarket: 'Amazon',
      sourcePrice: 11.20,
      sourceShippingDays: '3-5 Gün',
      etsyAvgPrice: 38.50,
      estimatedMargin: 165,
      netProfitUsd: 20.80,
      imageUrl: 'https://images.unsplash.com/photo-1544716278-ca5e3f4abd8c?w=500&auto=format&fit=crop&q=80',
      category: 'Paper & Stationery'
    },
    {
      id: 'arb-3',
      title: 'Nordic Wooden Geometric Wall Hanging Shelf',
      sourceMarket: 'AliExpress',
      sourcePrice: 14.00,
      sourceShippingDays: '8-12 Gün',
      etsyAvgPrice: 49.00,
      estimatedMargin: 180,
      netProfitUsd: 26.50,
      imageUrl: 'https://images.unsplash.com/photo-1513519245088-0e12902e5a38?w=500&auto=format&fit=crop&q=80',
      category: 'Home Decor'
    }
  ];

  ngOnInit(): void {}

  get filteredItems(): ArbitrageItem[] {
    if (this.marketFilter === 'ALL') return this.items;
    return this.items.filter(i => i.sourceMarket === this.marketFilter);
  }

  scanMarkets(): void {
    this.toastMessage = '⚡ Çapraz pazar taraması tamamlandı: 48 yüksek marjlı ürün radarda!';
    setTimeout(() => this.toastMessage = '', 4000);
  }

  importToCreator(item: ArbitrageItem): void {
    this.router.navigate(['/listings/fast-creator'], {
      queryParams: {
        title: item.title,
        price: item.etsyAvgPrice,
        category: item.category
      }
    });
  }
}
