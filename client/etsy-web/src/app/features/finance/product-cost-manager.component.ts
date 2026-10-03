import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

export interface ListingProductCost {
  listingId: string;
  title: string;
  sku: string;
  salePriceUsd: number;
  materialCostUsd: number;
  packagingCostUsd: number;
  shippingCostUsd: number;
  lastUpdated: string;
}

const STORAGE_KEY = 'etsy_product_costs_v1';

@Component({
  selector: 'app-product-cost-manager',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="cost-mgr-container">
      <!-- HEADER -->
      <div class="cost-header">
        <div class="header-left">
          <div class="icon-box">🏷️</div>
          <div>
            <h1 class="page-title">Ürün Maliyetleri & Kârlılık Yöneticisi</h1>
            <p class="page-subtitle">İlan bazında hammadde, ambalaj ve kargo maliyetlerini yöneterek gerçek net kârı ve bilançoyu hesaplayın</p>
          </div>
        </div>

        <div class="header-actions">
          <button type="button" class="btn-back" (click)="returnToAccounting()">
            ← Muhasebeye Dön
          </button>
          <button type="button" class="btn-save-all" (click)="saveAll()">
            💾 Tüm Maliyetleri Kaydet
          </button>
        </div>
      </div>

      <!-- KPI METRIC CARDS -->
      <div class="kpi-grid">
        <div class="glass-card kpi-card">
          <span class="kpi-lbl">TOPLAM KAYITLI İLAN</span>
          <span class="kpi-val">{{ items.length }} İlan</span>
          <span class="kpi-sub">{{ completedCount }} ilanın maliyeti tam</span>
        </div>
        <div class="glass-card kpi-card">
          <span class="kpi-lbl">ORTALAMA HAMMADDE MALİYETİ</span>
          <span class="kpi-val text-amber">&#36;{{ avgMaterialCost.toFixed(2) }}</span>
          <span class="kpi-sub">≈ ₺{{ (avgMaterialCost * usdTryRate).toFixed(2) }}</span>
        </div>
        <div class="glass-card kpi-card">
          <span class="kpi-lbl">ORTALAMA NET KÂR MARJI</span>
          <span class="kpi-val text-emerald">%{{ avgMargin.toFixed(1) }}</span>
          <span class="kpi-sub">Komisyon ve kargo sonrası</span>
        </div>
        <div class="glass-card kpi-card">
          <span class="kpi-lbl">EKSİK MALİYET ALARMI</span>
          <span class="kpi-val" [class.text-rose]="missingCount > 0" [class.text-emerald]="missingCount === 0">
            {{ missingCount }} İlan
          </span>
          <span class="kpi-sub">{{ missingCount > 0 ? 'Kırmızı alarm: kâr hesaplanamıyor' : 'Tüm maliyetler girilmiş' }}</span>
        </div>
      </div>

      <!-- TOOLBAR & SEARCH -->
      <div class="glass-card toolbar-card">
        <div class="search-wrap">
          <span class="search-icon">🔍</span>
          <input 
            type="text" 
            class="std-input" 
            [(ngModel)]="searchQuery" 
            placeholder="İlan başlığı veya SKU kodu ile ara..." />
        </div>

        <div class="filter-wrap">
          <button 
            type="button" 
            class="filter-pill" 
            [class.active]="filterMode === 'all'" 
            (click)="filterMode = 'all'">
            Tümü ({{ items.length }})
          </button>
          <button 
            type="button" 
            class="filter-pill" 
            [class.active]="filterMode === 'missing'" 
            (click)="filterMode = 'missing'">
            ⚠️ Sadece Eksikler ({{ missingCount }})
          </button>
          <button 
            type="button" 
            class="filter-pill" 
            [class.active]="filterMode === 'completed'" 
            (click)="filterMode = 'completed'">
            ✓ Tamamlananlar ({{ completedCount }})
          </button>
        </div>
      </div>

      <!-- TABLE CARD -->
      <div class="glass-card table-card">
        <div class="table-wrap">
          <table class="cost-table">
            <thead>
              <tr>
                <th style="width: 32%;">İlan Başlığı & SKU</th>
                <th style="width: 10%;">Etsy Satış ($)</th>
                <th style="width: 14%;">Hammadde ($)</th>
                <th style="width: 12%;">Ambalaj ($)</th>
                <th style="width: 14%;">Kargo Maliyeti ($)</th>
                <th style="width: 10%;">Tahmini Kâr ($)</th>
                <th style="width: 8%;">Durum</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of filteredItems" [class.row-missing]="isItemCostMissing(item)">
                <td>
                  <div class="title-cell">
                    <span class="prod-title" title="{{ item.title }}">{{ item.title }}</span>
                    <span class="sku-tag">SKU: {{ item.sku || 'SKU-YOK' }} | #{{ item.listingId }}</span>
                  </div>
                </td>
                <td>
                  <span class="price-val">&#36;{{ item.salePriceUsd.toFixed(2) }}</span>
                </td>
                <td>
                  <div class="input-cell-wrap">
                    <span class="cell-curr">&#36;</span>
                    <input 
                      type="number" 
                      step="0.1" 
                      min="0" 
                      class="cell-input" 
                      [(ngModel)]="item.materialCostUsd" 
                      (ngModelChange)="onCostChange(item)" />
                  </div>
                </td>
                <td>
                  <div class="input-cell-wrap">
                    <span class="cell-curr">&#36;</span>
                    <input 
                      type="number" 
                      step="0.05" 
                      min="0" 
                      class="cell-input" 
                      [(ngModel)]="item.packagingCostUsd" 
                      (ngModelChange)="onCostChange(item)" />
                  </div>
                </td>
                <td>
                  <div class="input-cell-wrap">
                    <span class="cell-curr">&#36;</span>
                    <input 
                      type="number" 
                      step="0.1" 
                      min="0" 
                      class="cell-input" 
                      [(ngModel)]="item.shippingCostUsd" 
                      (ngModelChange)="onCostChange(item)" />
                  </div>
                </td>
                <td>
                  <div class="profit-cell">
                    <span class="p-usd" [class.text-emerald]="calcNetProfit(item) > 0" [class.text-rose]="calcNetProfit(item) <= 0">
                      &#36;{{ calcNetProfit(item).toFixed(2) }}
                    </span>
                    <span class="p-pct">
                      %{{ calcMarginPercent(item).toFixed(0) }}
                    </span>
                  </div>
                </td>
                <td>
                  <span class="status-badge" [class.badge-ok]="!isItemCostMissing(item)" [class.badge-warn]="isItemCostMissing(item)">
                    {{ isItemCostMissing(item) ? 'Eksik' : 'Kayıtlı' }}
                  </span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- TOAST MESSAGE -->
      <div *ngIf="toastMessage" class="toast-popup">
        {{ toastMessage }}
      </div>
    </div>
  `,
  styles: [`
    .cost-mgr-container {
      display: flex;
      flex-direction: column;
      gap: 16px;
      padding: 4px;
    }

    .cost-header {
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
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
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

    .header-actions {
      display: flex;
      align-items: center;
      gap: 10px;
    }

    .btn-back {
      background: #1e293b;
      border: 1px solid #334155;
      color: #cbd5e1;
      padding: 9px 16px;
      font-weight: 600;
      font-size: 0.82rem;
      border-radius: 8px;
      cursor: pointer;
      transition: all 0.2s;
    }

    .btn-back:hover {
      background: #334155;
      color: #fff;
    }

    .btn-save-all {
      background: linear-gradient(135deg, #10b981 0%, #059669 100%);
      color: #fff;
      border: none;
      font-weight: 700;
      font-size: 0.85rem;
      padding: 9px 18px;
      border-radius: 8px;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
      transition: all 0.2s;
    }

    .btn-save-all:hover {
      transform: translateY(-1px);
      box-shadow: 0 6px 18px rgba(16, 185, 129, 0.5);
    }

    .glass-card {
      background: rgba(17, 24, 39, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 14px;
      backdrop-filter: blur(8px);
    }

    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 12px;
    }

    .kpi-card {
      display: flex;
      flex-direction: column;
      gap: 3px;
    }

    .kpi-lbl {
      font-size: 0.7rem;
      font-weight: 700;
      color: #94a3b8;
    }

    .kpi-val {
      font-size: 1.3rem;
      font-weight: 800;
      color: #fff;
    }

    .kpi-sub {
      font-size: 0.72rem;
      color: #64748b;
    }

    .text-emerald { color: #10b981 !important; }
    .text-amber { color: #f59e0b !important; }
    .text-rose { color: #f43f5e !important; }

    .toolbar-card {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
      flex-wrap: wrap;
    }

    .search-wrap {
      position: relative;
      flex: 1;
      min-width: 250px;
    }

    .search-icon {
      position: absolute;
      left: 10px;
      top: 50%;
      transform: translateY(-50%);
      color: #64748b;
    }

    .std-input {
      width: 100%;
      box-sizing: border-box;
      background: #0f172a;
      border: 1px solid #334155;
      color: #fff;
      border-radius: 6px;
      padding: 8px 10px 8px 32px;
      font-size: 0.82rem;
      outline: none;
    }

    .std-input:focus {
      border-color: #10b981;
    }

    .filter-wrap {
      display: flex;
      gap: 8px;
    }

    .filter-pill {
      background: #1e293b;
      border: 1px solid #334155;
      color: #94a3b8;
      padding: 6px 12px;
      border-radius: 20px;
      font-size: 0.76rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
    }

    .filter-pill.active {
      background: rgba(16, 185, 129, 0.2);
      border-color: #10b981;
      color: #34d399;
    }

    .table-card {
      padding: 0;
      overflow: hidden;
    }

    .table-wrap {
      overflow-x: auto;
    }

    .cost-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.82rem;
    }

    .cost-table th {
      background: rgba(15, 23, 42, 0.9);
      color: #94a3b8;
      font-weight: 700;
      padding: 12px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      white-space: nowrap;
    }

    .cost-table td {
      padding: 10px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      color: #e2e8f0;
      vertical-align: middle;
    }

    .cost-table tr:hover {
      background: rgba(255, 255, 255, 0.02);
    }

    .cost-table tr.row-missing {
      background: rgba(244, 63, 94, 0.04);
    }

    .title-cell {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }

    .prod-title {
      font-weight: 600;
      color: #f1f5f9;
      display: -webkit-box;
      -webkit-line-clamp: 1;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }

    .sku-tag {
      font-size: 0.7rem;
      color: #64748b;
      font-family: monospace;
    }

    .price-val {
      font-weight: 700;
      color: #fff;
    }

    .input-cell-wrap {
      display: inline-flex;
      align-items: center;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 2px 6px;
      width: 90px;
    }

    .input-cell-wrap:focus-within {
      border-color: #10b981;
    }

    .cell-curr {
      font-size: 0.74rem;
      color: #64748b;
      margin-right: 2px;
    }

    .cell-input {
      width: 100%;
      background: transparent;
      border: none;
      color: #fff;
      font-size: 0.8rem;
      font-weight: 600;
      outline: none;
    }

    .profit-cell {
      display: flex;
      flex-direction: column;
      gap: 1px;
    }

    .p-usd {
      font-weight: 800;
      font-size: 0.85rem;
    }

    .p-pct {
      font-size: 0.68rem;
      color: #64748b;
    }

    .status-badge {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: 4px;
    }

    .badge-ok {
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      border: 1px solid rgba(16, 185, 129, 0.3);
    }

    .badge-warn {
      background: rgba(244, 63, 94, 0.15);
      color: #fb7185;
      border: 1px solid rgba(244, 63, 94, 0.3);
    }

    .toast-popup {
      position: fixed;
      bottom: 24px;
      right: 24px;
      background: #065f46;
      border: 1px solid #10b981;
      color: #ecfdf5;
      padding: 12px 20px;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.5);
      z-index: 1000;
      animation: fadeIn 0.2s ease;
    }

    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(8px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class ProductCostManagerComponent implements OnInit {
  private apiService = inject(EtsyApiService);
  private router = inject(Router);

  searchQuery = '';
  filterMode: 'all' | 'missing' | 'completed' = 'all';
  toastMessage = '';

  get usdTryRate(): number {
    return this.apiService.exchangeRate();
  }

  items: ListingProductCost[] = [];

  ngOnInit(): void {
    this.loadCosts();
  }

  private loadCosts(): void {
    const saved = localStorage.getItem(STORAGE_KEY);
    if (saved) {
      try {
        this.items = JSON.parse(saved);
        return;
      } catch {}
    }

    // Default seed listings
    this.items = [
      {
        listingId: '1849204811',
        title: 'Articulated Crystal Wing Dragon 3D Print Toy',
        sku: 'DRG-CRY-01',
        salePriceUsd: 38.50,
        materialCostUsd: 4.80,
        packagingCostUsd: 1.20,
        shippingCostUsd: 9.80,
        lastUpdated: new Date().toISOString()
      },
      {
        listingId: '1849204812',
        title: 'Personalized Leather Shoulder Bag Handmade Vintage Satchel',
        sku: 'BAG-LTH-02',
        salePriceUsd: 84.00,
        materialCostUsd: 22.00,
        packagingCostUsd: 2.50,
        shippingCostUsd: 12.50,
        lastUpdated: new Date().toISOString()
      },
      {
        listingId: '1849204813',
        title: 'Self-Watering Geometric Succulent Planter Nordic Pot',
        sku: 'PLT-GEO-03',
        salePriceUsd: 26.00,
        materialCostUsd: 3.10,
        packagingCostUsd: 1.00,
        shippingCostUsd: 9.80,
        lastUpdated: new Date().toISOString()
      },
      {
        listingId: '1849204814',
        title: 'Custom Name Dainty Silver Necklace 925 Sterling',
        sku: 'JWL-SLV-04',
        salePriceUsd: 42.00,
        materialCostUsd: 0, // Eksik
        packagingCostUsd: 0,
        shippingCostUsd: 0,
        lastUpdated: new Date().toISOString()
      },
      {
        listingId: '1849204815',
        title: 'Steampunk Mechanical Skeleton Gear Desk Clock',
        sku: 'CLK-STP-05',
        salePriceUsd: 92.00,
        materialCostUsd: 15.00,
        packagingCostUsd: 3.00,
        shippingCostUsd: 14.50,
        lastUpdated: new Date().toISOString()
      }
    ];
  }

  isItemCostMissing(item: ListingProductCost): boolean {
    return (item.materialCostUsd || 0) <= 0 || (item.shippingCostUsd || 0) <= 0;
  }

  get completedCount(): number {
    return this.items.filter(i => !this.isItemCostMissing(i)).length;
  }

  get missingCount(): number {
    return this.items.filter(i => this.isItemCostMissing(i)).length;
  }

  get avgMaterialCost(): number {
    const list = this.items.filter(i => (i.materialCostUsd || 0) > 0);
    if (list.length === 0) return 0;
    const sum = list.reduce((acc, curr) => acc + curr.materialCostUsd, 0);
    return sum / list.length;
  }

  get avgMargin(): number {
    const list = this.items.filter(i => !this.isItemCostMissing(i));
    if (list.length === 0) return 0;
    const sumMargin = list.reduce((acc, curr) => acc + this.calcMarginPercent(curr), 0);
    return sumMargin / list.length;
  }

  calcNetProfit(item: ListingProductCost): number {
    const etsyFees = (item.salePriceUsd * 0.095) + 0.45; // ~9.5% fees + fixed
    const totalCosts = (item.materialCostUsd || 0) + (item.packagingCostUsd || 0) + (item.shippingCostUsd || 0) + etsyFees;
    return Number((item.salePriceUsd - totalCosts).toFixed(2));
  }

  calcMarginPercent(item: ListingProductCost): number {
    if (item.salePriceUsd <= 0) return 0;
    const profit = this.calcNetProfit(item);
    return Number(((profit / item.salePriceUsd) * 100).toFixed(1));
  }

  onCostChange(item: ListingProductCost): void {
    item.lastUpdated = new Date().toISOString();
  }

  saveAll(): void {
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(this.items));
      this.toastMessage = '✅ Tüm ürün maliyetleri başarıyla kaydedildi!';
      setTimeout(() => this.toastMessage = '', 3500);
    } catch (e) {
      alert('Maliyetler kaydedilirken hata oluştu.');
    }
  }

  returnToAccounting(): void {
    this.router.navigate(['/finance/accounting']);
  }

  get filteredItems(): ListingProductCost[] {
    return this.items.filter(item => {
      if (this.filterMode === 'missing' && !this.isItemCostMissing(item)) return false;
      if (this.filterMode === 'completed' && this.isItemCostMissing(item)) return false;

      if (this.searchQuery.trim()) {
        const q = this.searchQuery.trim().toLowerCase();
        return item.title.toLowerCase().includes(q) || item.sku.toLowerCase().includes(q);
      }
      return true;
    });
  }
}
