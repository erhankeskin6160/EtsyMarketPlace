import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

const COMPETITOR_STORAGE_KEY = 'etsy_competitor_shops_v1';

interface CompetitorShop {
  shopName: string;
  niche: string;
  sales30d: number;
  revenueEstUsd: number;
  activeListings: number;
  topItemTitle: string;
  topItemPrice: number;
  topItemTags: string[];
}

@Component({
  selector: 'app-competitor-spy',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="spy-view">
      <!-- HEADER -->
      <div class="spy-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"></path>
              <circle cx="12" cy="12" r="3"></circle>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Rakip & Trend Casusu (Competitor Intelligence)</h1>
            <p class="page-subtitle">Rakip mağaza satış hızı, tahmini cirolar ve ilk sayfa 13 etiket istihbaratı</p>
          </div>
        </div>

        <div class="header-right">
          <button class="btn-add-shop" (click)="addCompetitorShop()">
            + Yeni Rakip Mağaza Ekle
          </button>
        </div>
      </div>

      <!-- MAIN CONTENT: 2 COLUMNS -->
      <div class="spy-body">
        
        <!-- LEFT: COMPETITORS LIST -->
        <div class="competitor-list-col">
          <div class="glass-card">
            <span class="card-label">Takip Edilen Rakip Mağazalar ({{ shops.length }})</span>
            
            <div class="shops-list">
              <div *ngIf="shops.length === 0" class="empty-hint">
                Henüz rakip mağaza eklenmedi. Sağ üstteki "+ Yeni Rakip Mağaza Ekle" ile takip listenizi oluşturun —
                mağaza metrikleri canlı analiz veri kaynağı bağlandığında otomatik doldurulur.
              </div>
              <div 
                *ngFor="let s of shops" 
                class="shop-card"
                [class.selected]="selectedShop?.shopName === s.shopName"
                (click)="selectedShop = s">
                
                <div class="shop-card-head">
                  <div class="head-left">
                    <span class="shop-name">{{ s.shopName }}</span>
                    <span class="niche-badge">{{ s.niche }}</span>
                  </div>
                  <button type="button" class="btn-del-shop" (click)="removeCompetitorShop(s.shopName, $event)" title="Rakip Takibini Sil">
                    ✕
                  </button>
                </div>

                <div class="shop-metrics-row">
                  <div class="mini-stat">
                    <span class="s-label">Aylık Satış:</span>
                    <span class="s-val">{{ s.sales30d ? (s.sales30d | number) + ' adet' : '—' }}</span>
                  </div>
                  <div class="mini-stat">
                    <span class="s-label">Tahmini Ciro:</span>
                    <span class="s-val revenue">{{ s.revenueEstUsd ? ('$' + (s.revenueEstUsd | number)) : '—' }}</span>
                  </div>
                  <div class="mini-stat">
                    <span class="s-label">Aktif İlan:</span>
                    <span class="s-val">{{ s.activeListings ? s.activeListings : '—' }}</span>
                  </div>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- RIGHT: DETAIL & TAG SPY -->
        <div class="detail-col" *ngIf="selectedShop">
          
          <!-- TOP SELLER PRODUCT CARD -->
          <div class="glass-card">
            <span class="card-label">En Çok Satan Ürün Analizi</span>
            <ng-container *ngIf="selectedShop.topItemTitle; else noTopProduct">
            <h3 class="top-product-title">{{ selectedShop.topItemTitle }}</h3>
            <div class="top-product-meta">
              <span class="price-tag">&#36;{{ selectedShop.topItemPrice.toFixed(2) }}</span>
              <span class="try-equiv">(₺{{ (selectedShop.topItemPrice * usdTryRate).toFixed(2) }})</span>
            </div>
            </ng-container>
            <ng-template #noTopProduct>
              <p class="hint-text">Bu mağaza için ürün analizi henüz yok — canlı analiz veri kaynağı bağlandığında burada görünecek.</p>
            </ng-template>
          </div>

          <!-- TAG SPY BOX -->
          <div class="glass-card">
            <div class="card-header-row">
              <span class="card-label">Etiket Casusu (13 Arama Etiketi)</span>
              <button class="btn-copy-tags" (click)="copyTags(selectedShop.topItemTags)" [disabled]="selectedShop.topItemTags.length === 0">
                📋 13 Etiketi Kopyala
              </button>
            </div>
            <p class="hint-text">Bu rakibin Etsy arama algoritmasında ilk sayfaya çıkmasını sağlayan aktif 13 anahtar kelimesi:</p>

            <div class="tags-cloud">
              <span *ngFor="let tag of selectedShop.topItemTags" class="tag-badge">
                {{ tag }}
              </span>
              <span *ngIf="selectedShop.topItemTags.length === 0" class="hint-text">Etiket verisi henüz yok.</span>
            </div>

            <div *ngIf="copiedMessage" class="toast-success">
              ✓ {{ copiedMessage }}
            </div>
          </div>

        </div>

      </div>
    </div>
  `,
  styles: [`
    .spy-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .spy-header {
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
      background: linear-gradient(135deg, rgba(168, 85, 247, 0.2), rgba(99, 102, 241, 0.2));
      border: 1px solid rgba(168, 85, 247, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #c084fc;
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
    .btn-add-shop {
      padding: 9px 16px;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.82rem;
      cursor: pointer;
    }

    .spy-body {
      display: flex;
      gap: 20px;
    }
    .competitor-list-col { flex: 1.2; }
    .detail-col { flex: 1.1; display: flex; flex-direction: column; gap: 16px; }

    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 18px;
      backdrop-filter: blur(10px);
    }
    .card-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      display: block;
      margin-bottom: 12px;
    }
    .card-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }

    .shops-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .shop-card {
      padding: 14px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 10px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .shop-card:hover {
      background: rgba(15, 23, 42, 0.9);
      border-color: rgba(255, 255, 255, 0.15);
    }
    .shop-card.selected {
      background: rgba(99, 102, 241, 0.15);
      border-color: #818cf8;
      box-shadow: 0 0 16px rgba(99, 102, 241, 0.2);
    }
    .shop-card-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .head-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .btn-del-shop {
      background: transparent;
      border: none;
      color: #94a3b8;
      cursor: pointer;
      font-size: 0.85rem;
      padding: 2px 6px;
      border-radius: 4px;
      transition: all 0.2s;
    }
    .btn-del-shop:hover {
      background: rgba(239, 68, 68, 0.25);
      color: #f87171;
    }
    .shop-name {
      font-size: 0.92rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .niche-badge {
      font-size: 0.72rem;
      background: rgba(255, 255, 255, 0.08);
      padding: 2px 8px;
      border-radius: 4px;
      color: #94a3b8;
    }
    .shop-metrics-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.78rem;
    }
    .s-label { color: #94a3b8; margin-right: 4px; }
    .s-val { font-weight: 700; color: #f8fafc; }
    .s-val.revenue { color: #34d399; }

    .top-product-title {
      font-size: 0.95rem;
      font-weight: 600;
      color: #f8fafc;
      margin: 0 0 8px 0;
    }
    .price-tag {
      font-size: 1.25rem;
      font-weight: 700;
      color: #34d399;
    }
    .try-equiv {
      font-size: 0.85rem;
      color: #94a3b8;
      margin-left: 6px;
    }

    .btn-copy-tags {
      padding: 6px 12px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .hint-text {
      font-size: 0.75rem;
      color: #64748b;
      margin: 0 0 12px 0;
    }
    .tags-cloud {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .tag-badge {
      padding: 5px 10px;
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.3);
      border-radius: 6px;
      font-size: 0.78rem;
      color: #e2e8f0;
    }
    .toast-success {
      margin-top: 10px;
      color: #34d399;
      font-size: 0.8rem;
      font-weight: 600;
    }

    .empty-hint {
      padding: 14px;
      font-size: 0.82rem;
      color: #94a3b8;
      background: rgba(15, 23, 42, 0.4);
      border: 1px dashed rgba(255, 255, 255, 0.15);
      border-radius: 10px;
      line-height: 1.5;
    }
    .btn-copy-tags[disabled] {
      opacity: 0.45;
      cursor: not-allowed;
    }

    @media (max-width: 1024px) {
      .spy-body { flex-direction: column; }
    }
  `]
})
export class CompetitorSpyComponent implements OnInit {
  shops: CompetitorShop[] = [];
  selectedShop: CompetitorShop | null = null;
  copiedMessage = '';

  constructor(public etsyApi: EtsyApiService) {}

  ngOnInit(): void {
    this.loadShops();
  }

  private loadShops(): void {
    const saved = localStorage.getItem(COMPETITOR_STORAGE_KEY);
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed) && parsed.length > 0) {
          this.shops = parsed;
          this.selectedShop = this.shops[0];
          return;
        }
      } catch {}
    }

    // Kayıtlı rakip mağaza yoksa boş liste ile başla (sıfır-mock ilkesi: örnek mağaza gösterilmez)
    this.shops = [];
    this.selectedShop = null;
  }

  private saveShops(): void {
    try {
      localStorage.setItem(COMPETITOR_STORAGE_KEY, JSON.stringify(this.shops));
    } catch {}
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  copyTags(tags: string[]): void {
    navigator.clipboard.writeText(tags.join(', '));
    this.copiedMessage = '13 etiket panoya kopyalandı!';
    setTimeout(() => this.copiedMessage = '', 3000);
  }

  addCompetitorShop(): void {
    const name = prompt('Takip etmek istediğiniz Etsy mağaza adını giriniz:');
    if (name && name.trim()) {
      const cleanName = name.trim();
      if (this.shops.some(s => s.shopName.toLowerCase() === cleanName.toLowerCase())) {
        this.copiedMessage = `ℹ️ "${cleanName}" zaten takip listesinde.`;
        setTimeout(() => this.copiedMessage = '', 3000);
        return;
      }
      // Sıfır-mock ilkesi: uydurma metrik üretilmez; canlı analiz uçları bağlandığında doldurulur.
      const newShop: CompetitorShop = {
        shopName: cleanName,
        niche: '—',
        sales30d: 0,
        revenueEstUsd: 0,
        activeListings: 0,
        topItemTitle: '',
        topItemPrice: 0,
        topItemTags: []
      };
      this.shops.unshift(newShop);
      this.selectedShop = newShop;
      this.saveShops();
      this.copiedMessage = `✓ "${cleanName}" takip listesine eklendi — canlı metrikler veri kaynağı bağlandığında dolacak.`;
      setTimeout(() => this.copiedMessage = '', 3500);
    }
  }

  removeCompetitorShop(shopName: string, event: MouseEvent): void {
    event.stopPropagation();
    if (confirm(`"${shopName}" mağazasını takipten çıkarmak istediğinize emin misiniz?`)) {
      this.shops = this.shops.filter(s => s.shopName !== shopName);
      if (this.selectedShop?.shopName === shopName) {
        this.selectedShop = this.shops.length > 0 ? this.shops[0] : null;
      }
      this.saveShops();
      this.copiedMessage = `✓ "${shopName}" mağazası takipten çıkarıldı.`;
      setTimeout(() => this.copiedMessage = '', 3500);
    }
  }
}
