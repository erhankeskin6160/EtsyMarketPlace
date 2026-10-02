import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

export interface MarketItem {
  id: number;
  title: string;
  priceUsd: number;
  views: number;
  favorites: number;
  shopName: string;
  tags: string[];
  imageUrl: string;
}

@Component({
  selector: 'app-market-research',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="market-view">
      <!-- HEADER -->
      <div class="market-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Pazar Araştırması & Anahtar Kelime Radarı</h1>
            <p class="page-subtitle">Arama hacmi, ortalama fiyatlar, rekabet skoru ve pazar fırsat analizi</p>
          </div>
        </div>

        <div class="header-right">
          <button class="btn-action btn-gemini" (click)="generateAiMarketReport()" [disabled]="isGeneratingReport">
            <span>{{ isGeneratingReport ? 'Yapay Zeka Analiz Ediyor...' : '✨ Gemini AI Pazar Raporu Çıkar' }}</span>
          </button>
        </div>
      </div>

      <!-- SEARCH BAR -->
      <div class="glass-card search-card">
        <div class="search-form-row">
          <div class="search-input-wrap">
            <span class="search-icon">🔍</span>
            <input 
              type="text" 
              [(ngModel)]="searchKeyword" 
              (keyup.enter)="onSearch()"
              placeholder="Etsy'de araştırılacak anahtar kelime (örn: 3d printed dragon, minimalist desk decor)..." 
              class="search-input" />
          </div>
          <div class="filter-wrap">
            <label class="filter-label">Limit:</label>
            <select [(ngModel)]="limit" class="filter-select">
              <option [value]="25">25 İlan</option>
              <option [value]="50">50 İlan</option>
              <option [value]="100">100 İlan</option>
            </select>
          </div>
          <div class="filter-wrap">
            <label class="filter-label">Sıralama:</label>
            <select [(ngModel)]="sortBy" class="filter-select" (change)="applySort()">
              <option value="relevance">En İlgili</option>
              <option value="price_asc">Fiyat (Önce En Düşük)</option>
              <option value="price_desc">Fiyat (Önce En Yüksek)</option>
              <option value="favorites">En Çok Beğenilen</option>
            </select>
          </div>
          <button class="btn-primary-search" (click)="onSearch()">
            Pazarı Tara
          </button>
        </div>
      </div>

      <!-- 4 KPI CARDS -->
      <div class="kpi-grid">
        <div class="glass-card kpi-card">
          <span class="kpi-label">ORTALAMA PİYASA FİYATI</span>
          <div class="kpi-val text-emerald">
            &#36;{{ avgPriceUsd | number:'1.2-2' }}
            <span class="sub-try">(₺{{ avgPriceUsd * etsyApi.exchangeRate() | number:'1.0-0' }})</span>
          </div>
          <span class="kpi-desc">Seçili kategorideki ortalama satış fiyatı</span>
        </div>

        <div class="glass-card kpi-card">
          <span class="kpi-label">ORTALAMA FAVORİ / İLAN</span>
          <div class="kpi-val text-cyan">
            {{ avgFavorites | number:'1.0-0' }} Fav
          </div>
          <span class="kpi-desc">Müşteri talep ve kaydetme yoğunluğu</span>
        </div>

        <div class="glass-card kpi-card">
          <span class="kpi-label">LİDER SATICI MAĞAZA</span>
          <div class="kpi-val text-purple">
            {{ topShopName }}
          </div>
          <span class="kpi-desc">Kategori hacminin %34'üne hakim</span>
        </div>

        <div class="glass-card kpi-card">
          <span class="kpi-label">FIRSAT SKORU (NİŞ DERECESİ)</span>
          <div class="kpi-val text-orange">
            %{{ opportunityScore }} / 100
          </div>
          <span class="kpi-desc">Düşük rekabet, yüksek talep seviyesi</span>
        </div>
      </div>

      <!-- AI REPORT CALLOUT -->
      <div *ngIf="aiReport" class="glass-card ai-report-card">
        <div class="ai-report-head">
          <span class="ai-badge">🤖 GEMINI SPARK PAZAR İSTİHBARATI</span>
          <button class="close-report" (click)="aiReport = ''">×</button>
        </div>
        <div class="ai-report-content" [innerHTML]="aiReport"></div>
      </div>

      <!-- LISTINGS GRID & TAG CLOUD -->
      <div class="content-split">
        <!-- Results Table -->
        <div class="glass-card table-card">
          <div class="table-head-row">
            <span class="table-title">Pazar İlanları ({{ items.length }} Sonuç)</span>
            <button class="btn-copy-tags" (click)="copyAllTopTags()">
              📋 İlk 13 Ortak Etiketi Kopyala
            </button>
          </div>

          <div class="table-wrap">
            <table class="market-table">
              <thead>
                <tr>
                  <th>Ürün</th>
                  <th>Fiyat ($ / ₺)</th>
                  <th>Favori</th>
                  <th>Görüntülenme</th>
                  <th>Mağaza</th>
                  <th>İşlem</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of items">
                  <td class="product-cell">
                    <img [src]="item.imageUrl" alt="Thumb" class="prod-thumb" />
                    <span class="prod-title">{{ item.title }}</span>
                  </td>
                  <td class="price-cell">
                    <b>&#36;{{ item.priceUsd | number:'1.2-2' }}</b>
                    <span class="try-sm">₺{{ item.priceUsd * etsyApi.exchangeRate() | number:'1.0-0' }}</span>
                  </td>
                  <td>❤️ {{ item.favorites }}</td>
                  <td>👁️ {{ item.views }}</td>
                  <td class="shop-name">{{ item.shopName }}</td>
                  <td>
                    <button class="btn-inspect" (click)="selectItem(item)">İncele</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Selected Item Tags & Detail -->
        <div class="glass-card detail-card" *ngIf="selectedItem">
          <h3 class="detail-title">İlan Detayı & Etiket Analizi</h3>
          <img [src]="selectedItem.imageUrl" alt="Preview" class="detail-big-img" />
          <h4 class="detail-prod-name">{{ selectedItem.title }}</h4>
          <div class="detail-pricing">
            <span>Satış Fiyatı: <b>&#36;{{ selectedItem.priceUsd | number:'1.2-2' }}</b></span>
            <span>(₺{{ selectedItem.priceUsd * etsyApi.exchangeRate() | number:'1.0-0' }})</span>
          </div>

          <div class="tags-section">
            <span class="tags-title">Kullanılan 13 Arama Etiketi:</span>
            <div class="tags-flex">
              <span *ngFor="let tag of selectedItem.tags" class="tag-pill">{{ tag }}</span>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .market-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .market-header {
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
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(6, 182, 212, 0.2), rgba(59, 130, 246, 0.2));
      border: 1px solid rgba(6, 182, 212, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #38bdf8;
    }
    .page-title {
      font-size: 1.45rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .page-subtitle {
      font-size: 0.8rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .btn-gemini {
      background: linear-gradient(135deg, #8b5cf6, #3b82f6);
      border: none;
      color: #fff;
      padding: 10px 18px;
      border-radius: 10px;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(139, 92, 246, 0.3);
      transition: all 0.2s;
    }
    .btn-gemini:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 20px rgba(139, 92, 246, 0.4);
    }
    .search-card {
      padding: 16px 20px;
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
    }
    .search-form-row {
      display: flex;
      gap: 14px;
      align-items: center;
    }
    .search-input-wrap {
      flex: 1;
      position: relative;
      display: flex;
      align-items: center;
    }
    .search-icon {
      position: absolute;
      left: 14px;
      font-size: 1rem;
      color: #94a3b8;
    }
    .search-input {
      width: 100%;
      background: #090d16;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 10px;
      padding: 12px 14px 12px 42px;
      color: #fff;
      font-size: 0.9rem;
      outline: none;
    }
    .search-input:focus {
      border-color: #38bdf8;
    }
    .filter-wrap {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .filter-label {
      font-size: 0.8rem;
      color: #94a3b8;
    }
    .filter-select {
      background: #090d16;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 10px 12px;
      color: #e2e8f0;
      font-size: 0.85rem;
      outline: none;
    }
    .btn-primary-search {
      background: #2563eb;
      border: none;
      color: #fff;
      padding: 11px 22px;
      border-radius: 10px;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
    }
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    .kpi-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .kpi-label {
      font-size: 0.72rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.05em;
    }
    .kpi-val {
      font-size: 1.55rem;
      font-weight: 800;
    }
    .sub-try {
      font-size: 0.95rem;
      color: #94a3b8;
      font-weight: 500;
    }
    .kpi-desc {
      font-size: 0.75rem;
      color: #64748b;
    }
    .text-emerald { color: #10b981; }
    .text-cyan { color: #06b6d4; }
    .text-purple { color: #a855f7; }
    .text-orange { color: #f97316; }

    .ai-report-card {
      background: rgba(88, 28, 135, 0.2);
      border: 1px solid rgba(168, 85, 247, 0.4);
      border-radius: 14px;
      padding: 18px;
    }
    .ai-report-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 10px;
    }
    .ai-badge {
      font-size: 0.75rem;
      font-weight: 700;
      color: #c084fc;
      letter-spacing: 0.05em;
    }
    .close-report {
      background: none;
      border: none;
      color: #94a3b8;
      font-size: 1.2rem;
      cursor: pointer;
    }
    .ai-report-content {
      font-size: 0.88rem;
      line-height: 1.6;
      color: #e2e8f0;
    }

    .content-split {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 20px;
    }
    .table-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
    }
    .table-head-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }
    .table-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
    }
    .btn-copy-tags {
      background: rgba(6, 182, 212, 0.15);
      border: 1px solid rgba(6, 182, 212, 0.4);
      color: #38bdf8;
      padding: 6px 12px;
      border-radius: 8px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
    }
    .table-wrap {
      overflow-x: auto;
    }
    .market-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
    }
    .market-table th {
      text-align: left;
      padding: 10px;
      color: #94a3b8;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .market-table td {
      padding: 12px 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    .product-cell {
      display: flex;
      align-items: center;
      gap: 12px;
      max-width: 320px;
    }
    .prod-thumb {
      width: 44px;
      height: 44px;
      border-radius: 8px;
      object-fit: cover;
    }
    .prod-title {
      font-weight: 600;
      color: #f1f5f9;
      line-height: 1.35;
    }
    .price-cell {
      display: flex;
      flex-direction: column;
    }
    .try-sm {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .shop-name {
      color: #94a3b8;
    }
    .btn-inspect {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #fff;
      padding: 5px 12px;
      border-radius: 6px;
      font-size: 0.75rem;
      cursor: pointer;
    }

    .detail-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .detail-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
    }
    .detail-big-img {
      width: 100%;
      height: 180px;
      border-radius: 10px;
      object-fit: cover;
    }
    .detail-prod-name {
      font-size: 0.88rem;
      font-weight: 600;
      margin: 0;
      line-height: 1.4;
    }
    .detail-pricing {
      display: flex;
      gap: 8px;
      font-size: 0.85rem;
      color: #38bdf8;
    }
    .tags-section {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .tags-title {
      font-size: 0.78rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .tags-flex {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .tag-pill {
      background: rgba(59, 130, 246, 0.12);
      border: 1px solid rgba(59, 130, 246, 0.25);
      color: #93c5fd;
      padding: 3px 8px;
      border-radius: 6px;
      font-size: 0.72rem;
    }
  `]
})
export class MarketResearchComponent implements OnInit {
  searchKeyword = '3D Printed Articulated Dragon';
  limit = 25;
  sortBy = 'relevance';
  isGeneratingReport = false;
  aiReport = '';

  avgPriceUsd = 34.80;
  avgFavorites = 1420;
  topShopName = 'MythicForgeCrafts';
  opportunityScore = 88;

  items: MarketItem[] = [
    {
      id: 1,
      title: 'Articulated Crystal Dragon with Moving Wings 3D Print',
      priceUsd: 39.50,
      views: 12450,
      favorites: 2840,
      shopName: 'MythicForgeCrafts',
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=300',
      tags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'winged dragon', 'desk decor', 'dnd gift', 'dragon sculpture', 'fantasy beast', 'mythical creature', 'adhd toy', 'bambu lab', 'flexi animal', 'gamer gift']
    },
    {
      id: 2,
      title: 'Minimalist Geometric Self-Watering Planter for Succulents',
      priceUsd: 26.00,
      views: 9340,
      favorites: 1890,
      shopName: 'Botanical3DPrints',
      imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=300',
      tags: ['self watering', 'succulent pot', 'geometric planter', 'minimalist decor', 'indoor planter', '3d printed pot', 'modern planter', 'plant lover gift', 'desktop plant', 'drainage pot', 'eco filament', 'home decor', 'botanical pot']
    },
    {
      id: 3,
      title: 'Steampunk Mechanical Skeleton Gear Desk Clock',
      priceUsd: 89.00,
      views: 8120,
      favorites: 1650,
      shopName: 'TimeWorksDesign',
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=300',
      tags: ['steampunk clock', 'mechanical clock', 'gear desk clock', 'industrial clock', 'vintage steampunk', '3d printed clock', 'engineer gift', 'skeleton clock', 'rustic home decor', 'mens gift', 'retro desk accessory', 'clockwork art', 'maker design']
    }
  ];

  selectedItem?: MarketItem;

  constructor(public etsyApi: EtsyApiService) {}

  ngOnInit(): void {
    this.selectedItem = this.items[0];
  }

  onSearch(): void {
    this.applySort();
  }

  applySort(): void {
    if (this.sortBy === 'price_asc') {
      this.items.sort((a, b) => a.priceUsd - b.priceUsd);
    } else if (this.sortBy === 'price_desc') {
      this.items.sort((a, b) => b.priceUsd - a.priceUsd);
    } else if (this.sortBy === 'favorites') {
      this.items.sort((a, b) => b.favorites - a.favorites);
    }
  }

  selectItem(item: MarketItem): void {
    this.selectedItem = item;
  }

  generateAiMarketReport(): void {
    this.isGeneratingReport = true;
    setTimeout(() => {
      this.isGeneratingReport = false;
      this.aiReport = `
        <b>Pazar Fırsat Özeti:</b> "<b>${this.searchKeyword}</b>" nişinde arz/talep dengesi oldukça elverişli (Fırsat Skoru: <b>%88</b>).<br/>
        • <b>Ortalama Fiyat:</b> &#36;${this.avgPriceUsd.toFixed(2)} (₺${(this.avgPriceUsd * this.etsyApi.exchangeRate()).toFixed(0)}). Tavsiye edilen giriş fiyatı: <b>&#36;34.90</b>.<br/>
        • <b>En Güçlü Rekabet Avantajı:</b> Alıcıların %68'i çift renkli ve ipek (silk) PLA filament kaplamaları tercih ediyor.<br/>
        • <b>Kritik 3 Etiket:</b> "<i>crystal dragon</i>", "<i>winged dragon</i>", "<i>flexi toy</i>".
      `;
    }, 1200);
  }

  copyAllTopTags(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.tags.join(', '));
      alert('13 adet etiket panoya kopyalandı:\n' + this.selectedItem.tags.join(', '));
    }
  }
}
