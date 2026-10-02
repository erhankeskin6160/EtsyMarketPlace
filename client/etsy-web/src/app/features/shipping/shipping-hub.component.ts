import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

export interface CarrierRate {
  id: string;
  name: string;
  logo: string;
  eta: string;
  costUsd: number;
  fuelSurchargeUsd: number;
  totalUsd: number;
  isBestValue: boolean;
  trackingType: string;
}

@Component({
  selector: 'app-shipping-hub',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="shipping-view">
      <!-- HEADER -->
      <div class="shipping-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="1" y="3" width="15" height="13"></rect>
              <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
              <circle cx="5.5" cy="18.5" r="2.5"></circle>
              <circle cx="18.5" cy="18.5" r="2.5"></circle>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Kargo Entegrasyonları Hub'ı & Navlun Karşılaştırma</h1>
            <p class="page-subtitle">Aras Global, ShipEntegra, Navlungo ve Shiptomore canlı kargo fiyat karşılaştırma motoru</p>
          </div>
        </div>

        <div class="header-right">
          <button class="btn-primary-ship" (click)="calculateRates()">
            ⚡ Canlı Fiyatları Sorgula
          </button>
        </div>
      </div>

      <!-- PARCEL CALCULATOR BAR -->
      <div class="glass-card calc-card">
        <div class="calc-row">
          <div class="field-col">
            <label class="field-label">Varış Ülkesi:</label>
            <select [(ngModel)]="destinationCountry" class="field-select" (change)="calculateRates()">
              <option value="US">🇺🇸 Amerika Birleşik Devletleri (ABD)</option>
              <option value="GB">🇬🇧 Birleşik Krallık (İngiltere)</option>
              <option value="DE">🇩🇪 Almanya</option>
              <option value="CA">🇨🇦 Kanada</option>
              <option value="FR">🇫🇷 Fransa</option>
              <option value="AU">🇦🇺 Avustralya</option>
            </select>
          </div>

          <div class="field-col">
            <label class="field-label">Paket Ağırlığı (Gram):</label>
            <input type="number" [(ngModel)]="weightGrams" class="field-input" (input)="calculateRates()" />
          </div>

          <div class="field-col">
            <label class="field-label">Ebatlar (cm): En x Boy x Yükseklik</label>
            <div class="dims-group">
              <input type="number" [(ngModel)]="dimWidth" class="field-input-dim" (input)="calculateRates()" placeholder="En" />
              <span>×</span>
              <input type="number" [(ngModel)]="dimLength" class="field-input-dim" (input)="calculateRates()" placeholder="Boy" />
              <span>×</span>
              <input type="number" [(ngModel)]="dimHeight" class="field-input-dim" (input)="calculateRates()" placeholder="Yük" />
            </div>
          </div>

          <div class="field-col desi-col">
            <label class="field-label">Hesaplanan Desi:</label>
            <span class="desi-value">{{ desi | number:'1.2-2' }} Desi</span>
          </div>
        </div>
      </div>

      <!-- CARRIER CARDS COMPARISON -->
      <div class="carriers-grid">
        <div *ngFor="let c of carriers" class="glass-card carrier-card" [class.best-card]="c.isBestValue">
          <div class="carrier-badge-row">
            <span *ngIf="c.isBestValue" class="badge-best">🌟 EN KÂRLI TAŞIYICI</span>
            <span class="carrier-eta">⏱️ {{ c.eta }}</span>
          </div>

          <div class="carrier-brand-row">
            <span class="carrier-logo">{{ c.logo }}</span>
            <div class="carrier-info">
              <h3 class="carrier-name">{{ c.name }}</h3>
              <span class="carrier-type">{{ c.trackingType }}</span>
            </div>
          </div>

          <div class="price-box">
            <div class="price-usd">&#36;{{ c.totalUsd | number:'1.2-2' }}</div>
            <div class="price-try">₺{{ c.totalUsd * etsyApi.exchangeRate() | number:'1.0-0' }} TRY</div>
            <span class="tax-info">Yakıt & Harçlar Dahil</span>
          </div>

          <button class="btn-create-label" (click)="createLabel(c)">
            🏷️ Bu Taşıyıcı ile Etiket Oluştur
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .shipping-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .shipping-header {
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
      background: linear-gradient(135deg, rgba(249, 115, 22, 0.2), rgba(234, 88, 12, 0.2));
      border: 1px solid rgba(249, 115, 22, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #fb923c;
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
    .btn-primary-ship {
      background: linear-gradient(135deg, #f97316, #ea580c);
      border: none;
      color: #fff;
      padding: 11px 22px;
      border-radius: 10px;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(249, 115, 22, 0.3);
      transition: all 0.2s;
    }
    .btn-primary-ship:hover {
      transform: translateY(-1px);
    }
    .calc-card {
      padding: 18px 22px;
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
    }
    .calc-row {
      display: flex;
      gap: 20px;
      align-items: flex-end;
    }
    .field-col {
      display: flex;
      flex-direction: column;
      gap: 6px;
      flex: 1;
    }
    .field-label {
      font-size: 0.78rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .field-select, .field-input {
      background: #090d16;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 10px 12px;
      color: #fff;
      font-size: 0.88rem;
      outline: none;
    }
    .dims-group {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .field-input-dim {
      width: 100%;
      background: #090d16;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 10px;
      color: #fff;
      font-size: 0.88rem;
      outline: none;
      text-align: center;
    }
    .desi-col {
      flex: 0.7;
    }
    .desi-value {
      font-size: 1.15rem;
      font-weight: 800;
      color: #38bdf8;
      padding: 9px 12px;
      background: rgba(6, 182, 212, 0.1);
      border: 1px solid rgba(6, 182, 212, 0.25);
      border-radius: 8px;
      text-align: center;
    }

    .carriers-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 20px;
    }
    .carrier-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 22px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      transition: all 0.2s;
    }
    .carrier-card:hover {
      border-color: rgba(255, 255, 255, 0.2);
      transform: translateY(-2px);
    }
    .best-card {
      border: 2px solid #10b981;
      background: rgba(16, 185, 129, 0.05);
      box-shadow: 0 0 20px rgba(16, 185, 129, 0.15);
    }
    .carrier-badge-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      min-height: 24px;
    }
    .badge-best {
      font-size: 0.68rem;
      font-weight: 800;
      background: #10b981;
      color: #000;
      padding: 3px 8px;
      border-radius: 6px;
      letter-spacing: 0.03em;
    }
    .carrier-eta {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .carrier-brand-row {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .carrier-logo {
      font-size: 2rem;
    }
    .carrier-name {
      font-size: 1.1rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .carrier-type {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .price-box {
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 10px;
      padding: 14px;
      text-align: center;
    }
    .price-usd {
      font-size: 1.6rem;
      font-weight: 900;
      color: #38bdf8;
    }
    .price-try {
      font-size: 0.88rem;
      color: #94a3b8;
      font-weight: 600;
      margin-top: 2px;
    }
    .tax-info {
      font-size: 0.68rem;
      color: #64748b;
      margin-top: 4px;
      display: block;
    }
    .btn-create-label {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #fff;
      padding: 10px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.82rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-create-label:hover {
      background: #2563eb;
      border-color: #2563eb;
    }
  `]
})
export class ShippingHubComponent implements OnInit {
  destinationCountry = 'US';
  weightGrams = 250;
  dimWidth = 15;
  dimLength = 20;
  dimHeight = 10;
  desi = 0.6;

  carriers: CarrierRate[] = [
    {
      id: 'aras',
      name: 'Aras Global Kargo',
      logo: '🚚',
      eta: '3-5 İş Günü',
      costUsd: 9.80,
      fuelSurchargeUsd: 1.10,
      totalUsd: 10.90,
      isBestValue: true,
      trackingType: 'USPS Son Mil Teslimat'
    },
    {
      id: 'shipentegra',
      name: 'ShipEntegra Kargo',
      logo: '📦',
      eta: '3-6 İş Günü',
      costUsd: 11.20,
      fuelSurchargeUsd: 1.30,
      totalUsd: 12.50,
      isBestValue: false,
      trackingType: 'FedEx / UPS Express'
    },
    {
      id: 'navlungo',
      name: 'Navlungo Kargo',
      logo: '🚢',
      eta: '4-7 İş Günü',
      costUsd: 11.80,
      fuelSurchargeUsd: 1.40,
      totalUsd: 13.20,
      isBestValue: false,
      trackingType: 'DHL eCommerce'
    },
    {
      id: 'shiptomore',
      name: 'Shiptomore Kargo',
      logo: '✈️',
      eta: '2-4 İş Günü',
      costUsd: 13.50,
      fuelSurchargeUsd: 1.50,
      totalUsd: 15.00,
      isBestValue: false,
      trackingType: 'TNT / FedEx Priority'
    }
  ];

  constructor(public etsyApi: EtsyApiService) {}

  ngOnInit(): void {
    this.calculateRates();
  }

  calculateRates(): void {
    this.desi = (this.dimWidth * this.dimLength * this.dimHeight) / 5000;
    const baseMult = this.destinationCountry === 'US' ? 1.0 : this.destinationCountry === 'GB' ? 0.95 : 1.15;
    
    this.carriers[0].totalUsd = Number((10.90 * baseMult).toFixed(2));
    this.carriers[1].totalUsd = Number((12.50 * baseMult).toFixed(2));
    this.carriers[2].totalUsd = Number((13.20 * baseMult).toFixed(2));
    this.carriers[3].totalUsd = Number((15.00 * baseMult).toFixed(2));

    let minPrice = Infinity;
    let bestIdx = 0;
    this.carriers.forEach((c, i) => {
      c.isBestValue = false;
      if (c.totalUsd < minPrice) {
        minPrice = c.totalUsd;
        bestIdx = i;
      }
    });
    this.carriers[bestIdx].isBestValue = true;
  }

  createLabel(c: CarrierRate): void {
    alert(`"${c.name}" için barkodlu uluslararası kargo etiketi oluşturuldu!\nTakip Kodu: TR${Date.now()}US`);
  }
}
