import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { AccountingService } from '../../core/services/accounting.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { 
  PaymentLedgerEntry, 
  BankPayoutRecord, 
  FinancialKpiSummary, 
  ExpenseBreakdownItem 
} from '../../core/models/accounting.models';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="accounting-view">
      <!-- 1. TOP HEADER & CONTROLS -->
      <div class="accounting-header">
        <div class="header-title-box">
          <div class="icon-circle">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="2" y="4" width="20" height="16" rx="2"></rect>
              <line x1="2" y1="10" x2="22" y2="10"></line>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Finansal Muhasebe & Banka Defteri</h1>
            <p class="page-subtitle">Etsy Payment Account hareketleri, banka transferleri ve detaylı gider dökümü</p>
          </div>
        </div>

        <div class="header-actions">
          <!-- LIVE CURRENCY TOGGLE -->
          <div class="currency-toggle-box">
            <button 
              class="curr-btn" 
              [class.active]="!isTry" 
              (click)="setCurrency('USD')">
              $ USD
            </button>
            <button 
              class="curr-btn" 
              [class.active]="isTry" 
              (click)="setCurrency('TRY')">
              ₺ TRY
            </button>
          </div>

          <!-- CSV EXPORT BUTTON -->
          <button class="btn-csv-export" (click)="exportCsv()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            Muhasebe CSV İndir
          </button>
        </div>
      </div>

      <!-- 2. SIX FINANCIAL KPI CARDS -->
      <div class="kpi-grid">
        <div class="kpi-card gross">
          <span class="kpi-title">Brüt Satış Geliri</span>
          <span class="kpi-val">{{ formatValue(kpiSummary.grossSales) }}</span>
          <span class="kpi-sub">Sipariş cirosu</span>
        </div>

        <div class="kpi-card fees">
          <span class="kpi-title">Etsy Komisyon & Kesintiler</span>
          <span class="kpi-val fee-text">-{{ formatValue(kpiSummary.etsyFees) }}</span>
          <span class="kpi-sub">%6.5 komisyon + işlem ücreti</span>
        </div>

        <div class="kpi-card ads">
          <span class="kpi-title">Toplam Reklam Gideri</span>
          <span class="kpi-val ad-text">-{{ formatValue(kpiSummary.innerAds + kpiSummary.offsiteAds) }}</span>
          <span class="kpi-sub">Etsy Ads & Offsite Ads</span>
        </div>

        <div class="kpi-card costs">
          <span class="kpi-title">Ürün & Kargo Maliyetleri</span>
          <span class="kpi-val cost-text">-{{ formatValue(kpiSummary.productCosts + kpiSummary.shippingCosts) }}</span>
          <span class="kpi-sub">İmalat + Kargo Navlunu</span>
        </div>

        <div class="kpi-card profit">
          <span class="kpi-title">Gerçek Net Kâr</span>
          <span class="kpi-val profit-text">{{ formatValue(kpiSummary.realNetProfit) }}</span>
          <span class="kpi-sub highlight-sub">Net Kâr Marjı: <strong>%{{ kpiSummary.profitMarginPercent }}</strong></span>
        </div>

        <div class="kpi-card payouts">
          <span class="kpi-title">Bankaya Yatan Transfer</span>
          <span class="kpi-val payout-text">{{ formatValue(kpiSummary.bankPayoutsTotal) }}</span>
          <span class="kpi-sub">Hesaba geçen nakit</span>
        </div>
      </div>

      <!-- 3. NAVIGATION TABS -->
      <div class="tab-nav-bar">
        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'ledger'" 
          (click)="activeTab = 'ledger'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <line x1="8" y1="6" x2="21" y2="6"></line>
            <line x1="8" y1="12" x2="21" y2="12"></line>
            <line x1="8" y1="18" x2="21" y2="18"></line>
            <line x1="3" y1="6" x2="3.01" y2="6"></line>
            <line x1="3" y1="12" x2="3.01" y2="12"></line>
            <line x1="3" y1="18" x2="3.01" y2="18"></line>
          </svg>
          Ödeme Defteri Hareketleri ({{ ledgerEntries.length }})
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'payouts'" 
          (click)="activeTab = 'payouts'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <rect x="1" y="4" width="22" height="16" rx="2" ry="2"></rect>
            <line x1="1" y1="10" x2="23" y2="10"></line>
          </svg>
          Banka Transferleri (Payouts - {{ bankPayouts.length }})
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'expenses'" 
          (click)="activeTab = 'expenses'">
          <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
            <path d="M21.21 15.89A10 10 0 1 1 8 2.83"></path>
            <path d="M22 12A10 10 0 0 0 12 2v10z"></path>
          </svg>
          Gider Dağılımı ve Maliyet Analizi
        </button>
      </div>

      <!-- 4. TAB CONTENTS -->
      <div class="tab-content-area">

        <!-- TAB 1: LEDGER ENTRIES -->
        <div *ngIf="activeTab === 'ledger'" class="tab-pane">
          <!-- TABLE FILTER BAR -->
          <div class="table-filter-bar">
            <div class="type-filter-group">
              <button 
                class="type-btn" 
                [class.active]="ledgerFilter === 'all'" 
                (click)="ledgerFilter = 'all'">
                Tümü
              </button>
              <button 
                class="type-btn" 
                [class.active]="ledgerFilter === 'sale'" 
                (click)="ledgerFilter = 'sale'">
                Satışlar
              </button>
              <button 
                class="type-btn" 
                [class.active]="ledgerFilter === 'fee'" 
                (click)="ledgerFilter = 'fee'">
                Kesintiler
              </button>
              <button 
                class="type-btn" 
                [class.active]="ledgerFilter === 'ad'" 
                (click)="ledgerFilter = 'ad'">
                Reklamlar
              </button>
              <button 
                class="type-btn" 
                [class.active]="ledgerFilter === 'payout'" 
                (click)="ledgerFilter = 'payout'">
                Transferler
              </button>
            </div>

            <div class="search-box">
              <input 
                type="text" 
                [(ngModel)]="ledgerSearchQuery" 
                placeholder="Açıklama, sipariş veya işlem ara..." 
                class="table-search-input" />
            </div>
          </div>

          <!-- LEDGER TABLE -->
          <div class="glass-table-wrapper">
            <table class="glass-table">
              <thead>
                <tr>
                  <th>İşlem ID</th>
                  <th>Tarih</th>
                  <th>Tür</th>
                  <th>Açıklama</th>
                  <th>Sipariş No</th>
                  <th class="text-right">Brüt Tutar</th>
                  <th class="text-right">Kesinti</th>
                  <th class="text-right">Net Tutar</th>
                  <th class="text-right">Bakiye</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of filteredLedger">
                  <td class="text-mono">{{ item.id }}</td>
                  <td>{{ item.entryDate }}</td>
                  <td>
                    <span class="type-badge" [ngClass]="item.transactionType">
                      {{ item.typeDisplay }}
                    </span>
                  </td>
                  <td class="item-title">{{ item.title }}</td>
                  <td>
                    <span *ngIf="item.orderNumber" class="order-link">{{ item.orderNumber }}</span>
                    <span *ngIf="!item.orderNumber" class="text-muted">—</span>
                  </td>
                  <td class="text-right font-semibold">
                    {{ item.grossAmount > 0 ? formatValue(item.grossAmount) : '—' }}
                  </td>
                  <td class="text-right fee-col">
                    {{ item.feeAmount !== 0 ? formatValue(item.feeAmount) : '—' }}
                  </td>
                  <td class="text-right font-bold" [class.net-pos]="item.netAmount > 0" [class.net-neg]="item.netAmount < 0">
                    {{ item.netAmount > 0 ? '+' : '' }}{{ formatValue(item.netAmount) }}
                  </td>
                  <td class="text-right text-mono font-semibold">
                    {{ formatValue(item.runningBalance) }}
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- TAB 2: BANK PAYOUTS -->
        <div *ngIf="activeTab === 'payouts'" class="tab-pane">
          <div class="glass-table-wrapper">
            <table class="glass-table">
              <thead>
                <tr>
                  <th>Transfer Referans ID</th>
                  <th>Gönderim Tarihi</th>
                  <th>Banka & Hesap</th>
                  <th>Durum</th>
                  <th class="text-right">USD Tutarı</th>
                  <th class="text-right">TCMB Kuru</th>
                  <th class="text-right">Hesaba Geçen Tutar (TRY)</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let p of bankPayouts">
                  <td class="text-mono font-bold">{{ p.referenceNumber }}</td>
                  <td>{{ p.initiatedDate }}</td>
                  <td>
                    <div class="bank-cell">
                      <span class="bank-name">{{ p.bankName }}</span>
                      <span class="iban-text">{{ p.ibanEnding }}</span>
                    </div>
                  </td>
                  <td>
                    <span class="payout-status-badge completed">
                      ✓ Tamamlandı
                    </span>
                  </td>
                  <td class="text-right text-mono font-bold">&#36;{{ p.amountUsd.toFixed(2) }}</td>
                  <td class="text-right text-mono text-muted">₺{{ p.exchangeRate.toFixed(3) }}</td>
                  <td class="text-right text-mono font-bold highlight-try">₺{{ p.amountTry.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- TAB 3: EXPENSE BREAKDOWN -->
        <div *ngIf="activeTab === 'expenses'" class="tab-pane">
          <div class="expense-breakdown-container">
            <h2 class="sub-header">Maliyet ve Giderlerin Ciro İçindeki Dağılımı</h2>
            
            <div class="breakdown-list">
              <div *ngFor="let exp of expenseBreakdown" class="breakdown-item-card">
                <div class="breakdown-info">
                  <div class="color-dot" [style.background]="exp.color"></div>
                  <span class="exp-name">{{ exp.name }}</span>
                  <span class="exp-pct">{{ exp.percentage }}%</span>
                </div>

                <div class="progress-bar-bg">
                  <div class="progress-bar-fill" [style.width.%]="exp.percentage" [style.background]="exp.color"></div>
                </div>

                <div class="breakdown-amounts">
                  <span class="exp-usd">&#36;{{ exp.amountUsd.toLocaleString('en-US', { minimumFractionDigits: 2 }) }}</span>
                  <span class="exp-try">≈ ₺{{ exp.amountTry.toLocaleString('tr-TR', { minimumFractionDigits: 2 }) }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>
    </div>
  `,
  styles: [`
    .accounting-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
    }

    /* HEADER */
    .accounting-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .header-title-box {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .icon-circle {
      width: 48px;
      height: 48px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.2), rgba(6, 182, 212, 0.2));
      border: 1px solid rgba(16, 185, 129, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #34d399;
    }
    .page-title {
      font-size: 1.4rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 3px 0 0 0;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .currency-toggle-box {
      display: flex;
      background: rgba(30, 41, 59, 0.8);
      padding: 3px;
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .curr-btn {
      padding: 6px 14px;
      border: none;
      background: transparent;
      color: #94a3b8;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.82rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .curr-btn.active {
      background: #6366f1;
      color: #fff;
      box-shadow: 0 2px 8px rgba(99, 102, 241, 0.35);
    }
    .btn-csv-export {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 9px 16px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-csv-export:hover {
      background: rgba(16, 185, 129, 0.28);
      color: #fff;
    }

    /* 6 KPI CARDS */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 14px;
      margin-bottom: 24px;
    }
    .kpi-card {
      padding: 16px;
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      backdrop-filter: blur(10px);
    }
    .kpi-title {
      font-size: 0.75rem;
      color: #94a3b8;
      font-weight: 600;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .kpi-val {
      font-size: 1.25rem;
      font-weight: 700;
      color: #f8fafc;
      margin-top: 4px;
    }
    .kpi-sub {
      font-size: 0.72rem;
      color: #64748b;
    }
    .fee-text { color: #f59e0b; }
    .ad-text { color: #ec4899; }
    .cost-text { color: #60a5fa; }
    .profit-text { color: #10b981; font-size: 1.35rem; }
    .payout-text { color: #818cf8; }
    .highlight-sub { color: #34d399; }

    /* TAB NAVIGATION */
    .tab-nav-bar {
      display: flex;
      gap: 8px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      margin-bottom: 18px;
    }
    .nav-tab {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 12px 18px;
      background: transparent;
      border: none;
      border-bottom: 2px solid transparent;
      color: #94a3b8;
      font-size: 0.88rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .nav-tab:hover {
      color: #f1f5f9;
    }
    .nav-tab.active {
      color: #818cf8;
      border-bottom-color: #818cf8;
    }

    /* TABLE FILTER BAR */
    .table-filter-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 14px;
    }
    .type-filter-group {
      display: flex;
      gap: 6px;
    }
    .type-btn {
      padding: 6px 12px;
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 6px;
      color: #94a3b8;
      font-size: 0.78rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .type-btn.active {
      background: rgba(99, 102, 241, 0.2);
      border-color: rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
      font-weight: 600;
    }
    .search-box {
      width: 280px;
    }
    .table-search-input {
      width: 100%;
      padding: 8px 12px;
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      color: #fff;
      font-size: 0.82rem;
      outline: none;
      box-sizing: border-box;
    }

    /* GLASS TABLE */
    .glass-table-wrapper {
      background: rgba(30, 41, 59, 0.35);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      overflow: hidden;
      backdrop-filter: blur(12px);
    }
    .glass-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
    }
    .glass-table th {
      padding: 12px 16px;
      background: rgba(15, 23, 42, 0.6);
      color: #94a3b8;
      font-weight: 600;
      text-align: left;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      font-size: 0.75rem;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .glass-table td {
      padding: 12px 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      color: #cbd5e1;
    }
    .glass-table tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }
    .text-right { text-align: right; }
    .text-mono { font-family: monospace; }
    .font-semibold { font-weight: 600; }
    .font-bold { font-weight: 700; }
    .text-muted { color: #64748b; }
    .fee-col { color: #f59e0b; }
    .net-pos { color: #34d399; }
    .net-neg { color: #f87171; }
    .highlight-try { color: #34d399; font-size: 0.95rem; }
    .order-link {
      color: #818cf8;
      font-weight: 600;
    }
    .item-title {
      max-width: 280px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }

    /* BADGES */
    .type-badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .type-badge.sale { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .type-badge.fee { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
    .type-badge.ad { background: rgba(236, 72, 153, 0.15); color: #f472b6; }
    .type-badge.listing { background: rgba(148, 163, 184, 0.15); color: #cbd5e1; }
    .type-badge.payout { background: rgba(99, 102, 241, 0.15); color: #818cf8; }
    .type-badge.refund { background: rgba(239, 68, 68, 0.15); color: #f87171; }

    .payout-status-badge.completed {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
      padding: 3px 8px;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .bank-cell {
      display: flex;
      flex-direction: column;
    }
    .bank-name {
      font-weight: 600;
      color: #f1f5f9;
    }
    .iban-text {
      font-size: 0.72rem;
      color: #64748b;
    }

    /* EXPENSES BREAKDOWN */
    .expense-breakdown-container {
      background: rgba(30, 41, 59, 0.35);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 20px;
    }
    .sub-header {
      font-size: 1rem;
      font-weight: 700;
      margin: 0 0 16px 0;
      color: #f8fafc;
    }
    .breakdown-list {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .breakdown-item-card {
      display: flex;
      flex-direction: column;
      gap: 6px;
      padding: 12px 16px;
      background: rgba(15, 23, 42, 0.5);
      border-radius: 8px;
    }
    .breakdown-info {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .color-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }
    .exp-name {
      flex: 1;
      font-size: 0.85rem;
      font-weight: 600;
      color: #f1f5f9;
    }
    .exp-pct {
      font-size: 0.85rem;
      font-weight: 700;
      color: #cbd5e1;
    }
    .progress-bar-bg {
      width: 100%;
      height: 8px;
      background: rgba(255, 255, 255, 0.06);
      border-radius: 4px;
      overflow: hidden;
    }
    .progress-bar-fill {
      height: 100%;
      border-radius: 4px;
      transition: width 0.4s ease;
    }
    .breakdown-amounts {
      display: flex;
      justify-content: flex-end;
      gap: 12px;
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .exp-usd {
      font-weight: 700;
      color: #f8fafc;
    }

    @media (max-width: 1200px) {
      .kpi-grid {
        grid-template-columns: repeat(3, 1fr);
      }
    }
    @media (max-width: 768px) {
      .kpi-grid {
        grid-template-columns: 1fr;
      }
      .accounting-header {
        flex-direction: column;
        align-items: flex-start;
        gap: 14px;
      }
    }
  `]
})
export class AccountingComponent implements OnInit {
  kpiSummary: FinancialKpiSummary = {
    period: 'Eylül - Ekim 2026',
    currency: 'USD',
    grossSales: 45261.97,
    etsyFees: 10173.80,
    innerAds: 2450.00,
    offsiteAds: 1820.50,
    refunds: 840.25,
    netRevenue: 29674.83,
    productCosts: 9835.33,
    shippingCosts: 4890.10,
    realNetProfit: 19839.50,
    profitMarginPercent: 43.8,
    bankPayoutsTotal: 25701.31
  };

  ledgerEntries: PaymentLedgerEntry[] = [];
  bankPayouts: BankPayoutRecord[] = [];
  expenseBreakdown: ExpenseBreakdownItem[] = [];

  activeTab: 'ledger' | 'payouts' | 'expenses' = 'ledger';
  ledgerFilter: 'all' | 'sale' | 'fee' | 'ad' | 'payout' = 'all';
  ledgerSearchQuery: string = '';

  constructor(
    private accountingService: AccountingService,
    public etsyApi: EtsyApiService
  ) {}

  get isTry(): boolean {
    return this.etsyApi.isTryCurrency();
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  ngOnInit(): void {
    this.refreshData();
  }

  refreshData(): void {
    this.accountingService.getFinancialSummary().subscribe(kpi => {
      this.kpiSummary = kpi;
      this.expenseBreakdown = this.accountingService.getExpenseBreakdown(kpi, this.usdTryRate);
    });

    this.accountingService.getLedgerEntries(this.usdTryRate).subscribe(entries => {
      this.ledgerEntries = entries;
    });

    this.accountingService.getBankPayouts(this.usdTryRate).subscribe(payouts => {
      this.bankPayouts = payouts;
    });
  }

  setCurrency(curr: 'TRY' | 'USD'): void {
    this.etsyApi.setCurrency(curr);
  }

  get filteredLedger(): PaymentLedgerEntry[] {
    return this.ledgerEntries.filter(entry => {
      // Type filter
      if (this.ledgerFilter !== 'all' && entry.transactionType !== this.ledgerFilter) {
        return false;
      }
      // Query filter
      if (this.ledgerSearchQuery.trim()) {
        const q = this.ledgerSearchQuery.toLowerCase();
        const matchesTitle = entry.title.toLowerCase().includes(q);
        const matchesOrder = entry.orderNumber?.toLowerCase().includes(q);
        const matchesId = entry.id.toLowerCase().includes(q);
        if (!matchesTitle && !matchesOrder && !matchesId) return false;
      }
      return true;
    });
  }

  formatValue(usdVal: number): string {
    if (this.isTry) {
      return `₺${(usdVal * this.usdTryRate).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }
    return `$${usdVal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  exportCsv(): void {
    this.accountingService.exportLedgerToCsv(this.ledgerEntries, this.usdTryRate);
  }
}
