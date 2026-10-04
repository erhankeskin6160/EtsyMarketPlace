import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService, ShopListingItemDto } from '../../core/services/etsy-api.service';

@Component({
  selector: 'app-profit-calculator',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="calc-view">
      <!-- HEADER -->
      <div class="calc-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="4" y="2" width="16" height="20" rx="2"></rect>
              <line x1="8" y1="6" x2="16" y2="6"></line>
              <line x1="16" y1="14" x2="16" y2="18"></line>
              <path d="M16 10h.01"></path>
              <path d="M12 10h.01"></path>
              <path d="M8 10h.01"></path>
              <path d="M12 14h.01"></path>
              <path d="M8 14h.01"></path>
              <path d="M12 18h.01"></path>
              <path d="M8 18h.01"></path>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Etsy Kâr Hesaplayıcı & Maliyet Simülatörü</h1>
            <p class="page-subtitle">Komisyonlar, işlem ücretleri, kargo navlunu ve offsite reklamlar dahil net kâr analizi</p>
          </div>
        </div>

        <div class="header-rate-pill">
          <span class="live-dot"></span>
          <span>Canlı Kur: 1 USD = {{ usdTryRate }} ₺</span>
        </div>
      </div>

      <!-- KPI METRIC CARDS -->
      <div class="kpi-grid">
        <div class="kpi-card profit-card">
          <span class="kpi-label">GERÇEK NET KÂR</span>
          <div class="kpi-value-row">
            <span class="kpi-val">&#36;{{ netProfitUsd.toFixed(2) }}</span>
            <span class="kpi-try">₺{{ netProfitTry.toFixed(2) }}</span>
          </div>
          <span class="kpi-sub" [class.success]="marginPercent >= 30">
            Kâr Marjı: <strong>%{{ marginPercent.toFixed(1) }}</strong>
          </span>
        </div>

        <div class="kpi-card">
          <span class="kpi-label">YATIRIM GETİRİSİ (ROI)</span>
          <span class="kpi-val roi-val">%{{ roiPercent.toFixed(1) }}</span>
          <span class="kpi-sub">Maliyet başına kâr oranı</span>
        </div>

        <div class="kpi-card">
          <span class="kpi-label">TOPLAM MALİYET & GİDER</span>
          <div class="kpi-value-row">
            <span class="kpi-val expense-val">&#36;{{ totalExpensesUsd.toFixed(2) }}</span>
            <span class="kpi-try">₺{{ (totalExpensesUsd * usdTryRate).toFixed(2) }}</span>
          </div>
          <span class="kpi-sub">Üretim + Kargo + Kesintiler</span>
        </div>

        <div class="kpi-card">
          <span class="kpi-label">ETSY TOPLAM KESİNTİSİ</span>
          <div class="kpi-value-row">
            <span class="kpi-val fee-val">&#36;{{ totalEtsyFeesUsd.toFixed(2) }}</span>
            <span class="kpi-try">₺{{ (totalEtsyFeesUsd * usdTryRate).toFixed(2) }}</span>
          </div>
          <span class="kpi-sub">%6.5 Komisyon + %3 İşlem + Listeleme</span>
        </div>
      </div>

      <!-- VISUAL SPLIT BAR -->
      <div class="split-bar-card">
        <div class="split-bar-header">
          <span class="card-label">Ciro Dağılım Çubuğu (Gelirin Nereye Gittiği)</span>
          <span class="total-revenue-label">Toplam Gelir: &#36;{{ totalRevenueUsd.toFixed(2) }}</span>
        </div>

        <div class="split-bar-wrapper">
          <div class="bar-segment profit-seg" [style.width.%]="profitRatio" title="Net Kâr: %{{ profitRatio.toFixed(1) }}">
            <span *ngIf="profitRatio > 12">Net Kâr (%{{ profitRatio.toFixed(0) }})</span>
          </div>
          <div class="bar-segment fee-seg" [style.width.%]="feeRatio" title="Etsy Kesintileri: %{{ feeRatio.toFixed(1) }}">
            <span *ngIf="feeRatio > 10">Etsy (%{{ feeRatio.toFixed(0) }})</span>
          </div>
          <div class="bar-segment material-seg" [style.width.%]="materialRatio" title="Üretim/Malzeme: %{{ materialRatio.toFixed(1) }}">
            <span *ngIf="materialRatio > 10">Üretim (%{{ materialRatio.toFixed(0) }})</span>
          </div>
          <div class="bar-segment shipping-seg" [style.width.%]="shippingRatio" title="Kargo Navlunu: %{{ shippingRatio.toFixed(1) }}">
            <span *ngIf="shippingRatio > 10">Kargo (%{{ shippingRatio.toFixed(0) }})</span>
          </div>
          <div class="bar-segment ad-seg" [style.width.%]="adRatio" title="Reklam: %{{ adRatio.toFixed(1) }}">
            <span *ngIf="adRatio > 8">Reklam</span>
          </div>
        </div>

        <div class="split-legend">
          <div class="legend-item"><span class="dot profit-dot"></span> Net Kâr (&#36;{{ netProfitUsd.toFixed(2) }})</div>
          <div class="legend-item"><span class="dot fee-dot"></span> Etsy Komisyonları (&#36;{{ totalEtsyFeesUsd.toFixed(2) }})</div>
          <div class="legend-item"><span class="dot mat-dot"></span> Üretim Maliyeti (&#36;{{ materialCostUsd.toFixed(2) }})</div>
          <div class="legend-item"><span class="dot ship-dot"></span> Kargo Navlunu (&#36;{{ shippingCostUsd.toFixed(2) }})</div>
          <div class="legend-item" *ngIf="adRatio > 0"><span class="dot ad-dot"></span> Reklam Gideri (&#36;{{ adCostUsd.toFixed(2) }})</div>
        </div>
      </div>

      <!-- MAIN INPUT WORKSPACE -->
      <div class="calc-inputs-grid">
        
        <!-- COLUMN 1: SALE PRICE & REVENUE -->
        <div class="glass-card">
          <span class="card-label">1. Satış & Gelir Kalemleri</span>

          <div class="form-group" *ngIf="activeListings.length > 0">
            <label>🛍️ Canlı Mağaza İlanından Seç</label>
            <select [(ngModel)]="selectedListingId" (change)="onListingSelect()" class="form-select highlight-select">
              <option [ngValue]="null">-- Canlı İlan İçe Aktar (Opsiyonel) --</option>
              <option *ngFor="let l of activeListings" [ngValue]="l.listingId">
                {{ l.title | slice:0:45 }}... (&#36;{{ l.price }})
              </option>
            </select>
          </div>

          <div class="form-group">
            <label>Ürün Adı / Başlık</label>
            <input type="text" [(ngModel)]="productName" placeholder="Örn: 3D Kristal Ejderha" class="form-input" />
          </div>

          <div class="form-row-2">
            <div class="form-group">
              <label>Satış Fiyatı (&#36; USD)</label>
              <input type="number" step="0.5" [(ngModel)]="salePriceUsd" (ngModelChange)="recalc()" class="form-input highlight" />
            </div>
            <div class="form-group">
              <label>TL Karşılığı (₺ TRY)</label>
              <input type="text" [value]="'₺' + (salePriceUsd * usdTryRate).toFixed(2)" readonly class="form-input readonly" />
            </div>
          </div>

          <div class="form-row-2">
            <div class="form-group">
              <label>Alıcıdan Alınan Kargo (&#36; USD)</label>
              <input type="number" step="0.5" [(ngModel)]="buyerShippingUsd" (ngModelChange)="recalc()" class="form-input" />
            </div>
            <div class="form-group">
              <label>Hediye Paketi Ücreti (&#36; USD)</label>
              <input type="number" step="0.5" [(ngModel)]="giftWrapUsd" (ngModelChange)="recalc()" class="form-input" />
            </div>
          </div>
        </div>

        <!-- COLUMN 2: COSTS & PRODUCTION -->
        <div class="glass-card">
          <span class="card-label">2. Üretim & Kargo Maliyetleri</span>

          <div class="form-row-2">
            <div class="form-group">
              <label>Ürün / Filament / Malzeme (&#36; USD)</label>
              <input type="number" step="0.5" [(ngModel)]="materialCostUsd" (ngModelChange)="recalc()" class="form-input highlight-cost" />
            </div>
            <div class="form-group">
              <label>TL Karşılığı (₺ TRY)</label>
              <input type="text" [value]="'₺' + (materialCostUsd * usdTryRate).toFixed(2)" readonly class="form-input readonly" />
            </div>
          </div>

          <div class="form-row-2">
            <div class="form-group">
              <label>Kargo Gönderim Maliyeti (&#36; USD)</label>
              <input type="number" step="0.5" [(ngModel)]="shippingCostUsd" (ngModelChange)="recalc()" class="form-input highlight-cost" />
            </div>
            <div class="form-group">
              <label>Paketleme & Koli (&#36; USD)</label>
              <input type="number" step="0.2" [(ngModel)]="packagingCostUsd" (ngModelChange)="recalc()" class="form-input" />
            </div>
          </div>

          <!-- QUICK CARRIER CARDS -->
          <div class="carrier-pills">
            <span class="carrier-pill-hint">Hızlı Kargo Seçimi:</span>
            <button class="c-pill" (click)="setCarrierCost(9.95, 'Shiptomore')">Shiptomore (&#36;9.95)</button>
            <button class="c-pill" (click)="setCarrierCost(10.90, 'ShipEntegra')">ShipEntegra (&#36;10.90)</button>
            <button class="c-pill" (click)="setCarrierCost(11.50, 'Aras Global')">Aras Global (&#36;11.50)</button>
            <button class="c-pill" (click)="setCarrierCost(12.20, 'Navlungo')">Navlungo (&#36;12.20)</button>
          </div>
        </div>

        <!-- COLUMN 3: ETSY FEES & ADVERTISING -->
        <div class="glass-card">
          <span class="card-label">3. Etsy Komisyonları & Reklam</span>

          <div class="fee-detail-list">
            <div class="fee-row">
              <span>Etsy İşlem Komisyonu (%6.5)</span>
              <span>-&#36;{{ (totalRevenueUsd * 0.065).toFixed(2) }}</span>
            </div>
            <div class="fee-row">
              <span>Ödeme İşleme Ücreti (%3 + &#36;0.25)</span>
              <span>-&#36;{{ (totalRevenueUsd * 0.03 + 0.25).toFixed(2) }}</span>
            </div>
            <div class="fee-row">
              <span>İlan Listeleme Ücreti (Listing Fee)</span>
              <span>-&#36;0.20</span>
            </div>
            <div class="fee-row" *ngIf="useCurrencyConversion">
              <span>Döviz Çevrim Farkı (%2.5)</span>
              <span>-&#36;{{ (totalRevenueUsd * 0.025).toFixed(2) }}</span>
            </div>
            <div class="fee-row" *ngIf="offsiteAdsPercent > 0">
              <span>Dış Reklam (Offsite Ads %{{ offsiteAdsPercent }})</span>
              <span>-&#36;{{ (totalRevenueUsd * (offsiteAdsPercent / 100)).toFixed(2) }}</span>
            </div>
          </div>

          <div class="form-group checkbox-group">
            <label class="custom-chk">
              <input type="checkbox" [(ngModel)]="useCurrencyConversion" (ngModelChange)="recalc()" />
              <span>Para Birimi Dönüştürme Farkı (%2.5) Ekle</span>
            </label>
          </div>

          <div class="form-group">
            <label>Offsite Ads (Dış Reklam)</label>
            <select [(ngModel)]="offsiteAdsPercent" (ngModelChange)="recalc()" class="form-select">
              <option [ngValue]="0">Yok (%0)</option>
              <option [ngValue]="12">Etsy İsteğe Bağlı (%12)</option>
              <option [ngValue]="15">Etsy Zorunlu Katılım (%15)</option>
            </select>
          </div>

          <button class="btn-save-costs" (click)="saveToCostManager()">
            💾 Bu Maliyeti Maliyet Yöneticisine Kaydet
          </button>
          <div *ngIf="saveToast" class="cost-saved-toast">
            {{ saveToast }}
          </div>
        </div>

      </div>
    </div>
  `,
  styles: [`
    .calc-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .calc-header {
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
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.2), rgba(99, 102, 241, 0.2));
      border: 1px solid rgba(16, 185, 129, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #34d399;
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
    .header-rate-pill {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 8px 16px;
      background: rgba(30, 41, 59, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 20px;
      font-size: 0.85rem;
      color: #f1f5f9;
      font-weight: 600;
    }
    .live-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10b981;
      box-shadow: 0 0 8px #10b981;
    }

    /* KPI CARDS */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    .kpi-card {
      padding: 18px;
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      backdrop-filter: blur(10px);
    }
    .profit-card {
      border-color: rgba(16, 185, 129, 0.4);
      background: rgba(16, 185, 129, 0.08);
    }
    .kpi-label {
      font-size: 0.75rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.04em;
    }
    .kpi-value-row {
      display: flex;
      align-items: baseline;
      gap: 8px;
      margin-top: 4px;
    }
    .kpi-val {
      font-size: 1.45rem;
      font-weight: 700;
      color: #34d399;
    }
    .kpi-try {
      font-size: 0.85rem;
      color: #94a3b8;
    }
    .roi-val { color: #818cf8; }
    .expense-val { color: #f87171; }
    .fee-val { color: #f59e0b; }
    .kpi-sub {
      font-size: 0.75rem;
      color: #64748b;
    }
    .kpi-sub.success { color: #34d399; }

    /* SPLIT BAR */
    .split-bar-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 16px 20px;
    }
    .split-bar-header {
      display: flex;
      justify-content: space-between;
      margin-bottom: 12px;
    }
    .card-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .total-revenue-label {
      font-size: 0.85rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .split-bar-wrapper {
      width: 100%;
      height: 28px;
      background: rgba(15, 23, 42, 0.8);
      border-radius: 8px;
      overflow: hidden;
      display: flex;
    }
    .bar-segment {
      height: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 0.72rem;
      font-weight: 700;
      color: #fff;
      transition: width 0.3s ease;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .profit-seg { background: #10b981; }
    .fee-seg { background: #f59e0b; }
    .material-seg { background: #3b82f6; }
    .shipping-seg { background: #8b5cf6; }
    .ad-seg { background: #ec4899; }

    .split-legend {
      display: flex;
      gap: 18px;
      margin-top: 12px;
      flex-wrap: wrap;
    }
    .legend-item {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.78rem;
      color: #cbd5e1;
    }
    .dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
    }
    .profit-dot { background: #10b981; }
    .fee-dot { background: #f59e0b; }
    .mat-dot { background: #3b82f6; }
    .ship-dot { background: #8b5cf6; }
    .ad-dot { background: #ec4899; }

    /* INPUTS GRID */
    .calc-inputs-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 16px;
    }
    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .form-group label {
      display: block;
      font-size: 0.75rem;
      color: #cbd5e1;
      margin-bottom: 4px;
      font-weight: 600;
    }
    .form-row-2 {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    .form-input, .form-select {
      width: 100%;
      padding: 8px 12px;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 6px;
      color: #fff;
      font-size: 0.85rem;
      outline: none;
      box-sizing: border-box;
    }
    .form-input.highlight {
      border-color: rgba(16, 185, 129, 0.4);
      background: rgba(16, 185, 129, 0.08);
      color: #34d399;
      font-weight: 700;
    }
    .form-input.highlight-cost {
      border-color: rgba(59, 130, 246, 0.4);
    }
    .form-input.readonly {
      background: rgba(255, 255, 255, 0.03);
      color: #94a3b8;
    }

    .carrier-pills {
      margin-top: 6px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .carrier-pill-hint {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .c-pill {
      padding: 6px 10px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 6px;
      color: #cbd5e1;
      font-size: 0.75rem;
      cursor: pointer;
      text-align: left;
      transition: all 0.2s;
    }
    .c-pill:hover {
      background: rgba(99, 102, 241, 0.2);
      border-color: #818cf8;
      color: #fff;
    }

    .fee-detail-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 10px;
      background: rgba(15, 23, 42, 0.5);
      border-radius: 8px;
    }
    .fee-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.78rem;
      color: #cbd5e1;
    }
    .custom-chk {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.78rem;
      color: #cbd5e1;
      cursor: pointer;
    }

    .highlight-select {
      border-color: rgba(59, 130, 246, 0.4);
      background: rgba(30, 58, 138, 0.25);
      color: #93c5fd;
      font-weight: 600;
    }
    .btn-save-costs {
      margin-top: 8px;
      padding: 10px;
      background: linear-gradient(135deg, #10b981, #059669);
      border: none;
      border-radius: 6px;
      color: #fff;
      font-weight: 700;
      font-size: 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-save-costs:hover {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .cost-saved-toast {
      padding: 8px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
      animation: fadeIn 0.2s ease;
    }

    @media (max-width: 1200px) {
      .kpi-grid { grid-template-columns: repeat(2, 1fr); }
      .calc-inputs-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class ProfitCalculatorComponent implements OnInit {
  productName = '3D Kristal Ejderha (Bambu PLA)';
  salePriceUsd = 49.50;
  buyerShippingUsd = 0;
  giftWrapUsd = 0;

  activeListings: ShopListingItemDto[] = [];
  selectedListingId: number | null = null;
  saveToast: string | null = null;

  materialCostUsd = 8.50;
  shippingCostUsd = 10.90;
  packagingCostUsd = 1.00;
  adCostUsd = 0;

  useCurrencyConversion = true;
  offsiteAdsPercent = 0;

  netProfitUsd = 0;
  netProfitTry = 0;
  marginPercent = 0;
  roiPercent = 0;
  totalExpensesUsd = 0;
  totalEtsyFeesUsd = 0;
  totalRevenueUsd = 0;

  profitRatio = 0;
  feeRatio = 0;
  materialRatio = 0;
  shippingRatio = 0;
  adRatio = 0;

  constructor(public etsyApi: EtsyApiService) {}

  ngOnInit(): void {
    this.recalc();
    this.loadListings();
  }

  loadListings(): void {
    this.etsyApi.getShopActiveListings(undefined, 50).subscribe({
      next: (listings) => {
        this.activeListings = listings || [];
      },
      error: () => {}
    });
  }

  onListingSelect(): void {
    const found = this.activeListings.find(l => l.listingId === Number(this.selectedListingId));
    if (found) {
      this.productName = found.title;
      this.salePriceUsd = found.price || this.salePriceUsd;

      try {
        const savedCosts: any[] = JSON.parse(localStorage.getItem('etsy_product_costs_v1') || '[]');
        const existing = savedCosts.find((c: any) => c.listingId === found.listingId || c.listingId === found.listingId.toString());
        if (existing) {
          this.materialCostUsd = existing.productCostUsd || existing.costUsd || this.materialCostUsd;
          this.shippingCostUsd = existing.shippingCostUsd || this.shippingCostUsd;
          this.packagingCostUsd = existing.packagingCostUsd || this.packagingCostUsd;
        }
      } catch {}

      this.recalc();
    }
  }

  saveToCostManager(): void {
    const listingId = this.selectedListingId || Date.now();
    try {
      const savedCosts: any[] = JSON.parse(localStorage.getItem('etsy_product_costs_v1') || '[]');
      const idx = savedCosts.findIndex((c: any) => c.listingId === listingId || c.listingId === listingId.toString());
      const itemToSave = {
        listingId: listingId,
        productName: this.productName,
        salePriceUsd: this.salePriceUsd,
        productCostUsd: this.materialCostUsd,
        shippingCostUsd: this.shippingCostUsd,
        packagingCostUsd: this.packagingCostUsd,
        netProfitUsd: this.netProfitUsd,
        marginPercent: this.marginPercent,
        updatedAt: new Date().toISOString()
      };

      if (idx >= 0) {
        savedCosts[idx] = { ...savedCosts[idx], ...itemToSave };
      } else {
        savedCosts.push(itemToSave);
      }

      localStorage.setItem('etsy_product_costs_v1', JSON.stringify(savedCosts));
      this.saveToast = `✓ "${this.productName.substring(0, 30)}..." maliyetleri başarıyla Ürün Maliyet Yöneticisine kaydedildi!`;
      setTimeout(() => this.saveToast = null, 4000);
    } catch {
      this.saveToast = '⚠️ Kaydedilirken bir hata oluştu.';
      setTimeout(() => this.saveToast = null, 4000);
    }
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  setCarrierCost(costUsd: number, name: string): void {
    this.shippingCostUsd = costUsd;
    this.recalc();
  }

  recalc(): void {
    this.totalRevenueUsd = this.salePriceUsd + this.buyerShippingUsd + this.giftWrapUsd;
    
    // Etsy transaction fee 6.5% on total revenue
    const txnFee = this.totalRevenueUsd * 0.065;
    // Payment processing fee 3% + $0.25
    const procFee = (this.totalRevenueUsd * 0.03) + 0.25;
    // Listing fee $0.20
    const listingFee = 0.20;
    // Currency conversion 2.5%
    const convFee = this.useCurrencyConversion ? (this.totalRevenueUsd * 0.025) : 0;
    // Offsite ads
    const offsiteFee = this.totalRevenueUsd * (this.offsiteAdsPercent / 100);

    this.totalEtsyFeesUsd = txnFee + procFee + listingFee + convFee + offsiteFee;

    const directCosts = this.materialCostUsd + this.shippingCostUsd + this.packagingCostUsd + this.adCostUsd;
    this.totalExpensesUsd = this.totalEtsyFeesUsd + directCosts;

    this.netProfitUsd = Number((this.totalRevenueUsd - this.totalExpensesUsd).toFixed(2));
    this.netProfitTry = Number((this.netProfitUsd * this.usdTryRate).toFixed(2));

    this.marginPercent = this.totalRevenueUsd > 0 
      ? Number(((this.netProfitUsd / this.totalRevenueUsd) * 100).toFixed(1))
      : 0;

    this.roiPercent = this.totalExpensesUsd > 0
      ? Number(((this.netProfitUsd / this.totalExpensesUsd) * 100).toFixed(1))
      : 0;

    // Ratios for split bar
    if (this.totalRevenueUsd > 0) {
      this.profitRatio = Math.max(0, (this.netProfitUsd / this.totalRevenueUsd) * 100);
      this.feeRatio = (this.totalEtsyFeesUsd / this.totalRevenueUsd) * 100;
      this.materialRatio = (this.materialCostUsd / this.totalRevenueUsd) * 100;
      this.shippingRatio = (this.shippingCostUsd / this.totalRevenueUsd) * 100;
      this.adRatio = (this.adCostUsd / this.totalRevenueUsd) * 100;
    }
  }
}
