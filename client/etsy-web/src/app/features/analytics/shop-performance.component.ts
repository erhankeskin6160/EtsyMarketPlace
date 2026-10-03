import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { RouterModule, Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface PerformanceSnapshot {
  period: string;
  orderCount: number;
  unitsSold: number;
  grossRevenue: number;
  avgOrderValue: number;
  conversionRate: number;
}

interface ProductTraffic {
  listingId: string;
  title: string;
  views: number;
  favorites: number;
  orders: number;
  revenue: number;
  conversionRate: number;
}

@Component({
  selector: 'app-shop-performance',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="perf-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">🏬</div>
          <div>
            <h1 class="page-title">Mağazam Performansı & Ziyaretçi Analitiği</h1>
            <p class="page-subtitle">Ziyaretçi trafiği, görüntülenme, favori sayıları ve ürün bazlı dönüşüm oranı (CR %) istatistikleri</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-sync" (click)="refreshPerformance()">
            ⚡ Canlı Metrikleri Yenile
          </button>
        </div>
      </div>

      <!-- DISCONNECTED / EXPIRED API WARNING BANNER -->
      <div *ngIf="etsyApi.tokenStatus() !== 'connected'" class="alert-banner">
        <div class="alert-icon">⚠️</div>
        <div class="alert-body">
          <span class="alert-title">Etsy v3 API Bağlantısı Aktif Değil (Token Süresi Dolmuş veya Bağlı Değil)</span>
          <p class="alert-desc">
            Sistemde <strong>asla uydurma veri gösterilmez</strong>. Mağazanıza ait gerçek ciro ve sipariş verilerini çekmek için lütfen Etsy API ayarlarından token yetkilendirmesini tamamlayın.
          </p>
        </div>
        <button class="btn-alert-action" (click)="router.navigate(['/settings/etsy-api'])">
          Etsy ile Yetkilendir
        </button>
      </div>

      <!-- TOAST MESSAGE -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">AYLIK BRÜT CİRO</span>
          <span class="kpi-val text-green">\${{ totalGrossRevenue | number:'1.2-2' }}</span>
          <span class="kpi-sub">₺{{ (totalGrossRevenue * etsyApi.exchangeRate()) | number:'1.0-0' }} TRY</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM SİPARİŞ</span>
          <span class="kpi-val text-blue">{{ totalOrders }} Sipariş</span>
          <span class="kpi-sub">{{ totalUnitsSold }} Adet Satılan Ürün</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">ORTALAMA SEPET (AOV)</span>
          <span class="kpi-val text-purple">\${{ avgOrderValue | number:'1.2-2' }}</span>
          <span class="kpi-sub">Sipariş Başı Gelir</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">ORTALAMA DÖNÜŞÜM ORANI</span>
          <span class="kpi-val text-orange">%{{ avgConversionRate }}</span>
          <span class="kpi-sub">Etsy Kategori Ortalaması: %2.1</span>
        </div>
      </div>

      <!-- REVENUE & ORDERS MONTHLY TIMELINE -->
      <div class="timeline-section glass-card">
        <div class="section-header">
          <h2 class="section-title">Aylık Büyüme & Satış Trendi</h2>
          <span class="currency-tag">USD ($) Bazlı</span>
        </div>

        <div *ngIf="monthlyHistory.length > 0" class="bars-container">
          <div *ngFor="let m of monthlyHistory" class="month-col">
            <div class="bar-track">
              <div class="bar-fill" [style.height.%]="(m.grossRevenue / maxRevenue) * 100"></div>
            </div>
            <span class="bar-amount">\${{ m.grossRevenue | number:'1.0-0' }}</span>
            <span class="bar-label">{{ m.period }}</span>
          </div>
        </div>

        <div *ngIf="monthlyHistory.length === 0" class="empty-state-box">
          <span class="empty-icon">📊</span>
          <span class="empty-title">Aylık Satış Trendi Verisi Bulunamadı</span>
          <p class="empty-desc">
            VDS veri tabanında bu mağazaya ait henüz geçmiş satış kaydı bulunmuyor. Etsy API bağlantısı sağlandığında ve senkronize edildikçe aylar otomatik listelenecektir.
          </p>
        </div>
      </div>

      <!-- TOP LISTING TRAFFIC & PERFORMANCE TABLE -->
      <div class="traffic-section glass-card">
        <div class="section-header">
          <h2 class="section-title">En Çok Satan & Trafik Çeken İlanlar</h2>
          <span class="badge-active">{{ topProducts.length > 0 ? (topProducts.length + ' İlan') : 'Canlı Veri Yok' }}</span>
        </div>

        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>İlan Adı</th>
                <th>Listing ID</th>
                <th>Görüntülenme</th>
                <th>Favori</th>
                <th>Sipariş</th>
                <th>Toplam Ciro</th>
                <th>Dönüşüm Oranı (CR)</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of topProducts">
                <td class="cell-title"><b>{{ p.title }}</b></td>
                <td><code>#{{ p.listingId }}</code></td>
                <td>{{ p.views | number }}</td>
                <td class="text-purple">{{ p.favorites | number }}</td>
                <td><b>{{ p.orders }}</b></td>
                <td class="text-green"><b>\${{ p.revenue | number:'1.2-2' }}</b></td>
                <td>
                  <div class="cr-wrap">
                    <span class="cr-pill" [ngClass]="p.conversionRate >= 3.0 ? 'high' : 'normal'">
                      %{{ p.conversionRate }}
                    </span>
                  </div>
                </td>
              </tr>
              <tr *ngIf="topProducts.length === 0">
                <td colspan="7" class="empty-table-cell">
                  <div class="empty-state-box">
                    <span class="empty-icon">🏬</span>
                    <span class="empty-title">Canlı Mağaza Ürün Verisi Bulunamadı</span>
                    <p class="empty-desc">
                      Etsy API bağlantısı aktif olmadığında veya henüz sipariş/trafik senkronizasyonu yapılmadığında <strong>asla sahte ürün uydurulmaz</strong>.
                      Gerçek mağazanızdaki ilanların görüntülenme ve dönüşüm verilerini görmek için lütfen Etsy API ayarlarından bağlantınızı yenileyin.
                    </p>
                    <button class="btn-goto-api" (click)="router.navigate(['/settings/etsy-api'])">
                      🔗 Etsy API Ayarlarına Git
                    </button>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .perf-container {
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
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.3);
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
    .btn-sync {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      border: none;
      padding: 9px 18px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-sync:hover { opacity: 0.9; transform: translateY(-1px); }

    .alert-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.4);
      border-radius: 12px;
      padding: 16px 20px;
      gap: 16px;
    }
    .alert-icon { font-size: 1.6rem; }
    .alert-body { flex: 1; }
    .alert-title { font-weight: 800; color: #f87171; font-size: 0.95rem; }
    .alert-desc { margin: 4px 0 0; font-size: 0.82rem; color: #fecaca; line-height: 1.4; }
    .btn-alert-action {
      background: #ef4444;
      color: #fff;
      border: none;
      padding: 8px 16px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
      transition: background 0.2s;
    }
    .btn-alert-action:hover { background: #dc2626; }

    .toast-box {
      background: rgba(16, 185, 129, 0.2);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      padding: 12px 18px;
      border-radius: 8px;
      font-weight: 600;
    }

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
    .text-green { color: #34d399; }
    .text-blue { color: #38bdf8; }
    .text-purple { color: #c084fc; }
    .text-orange { color: #f97316; }

    .glass-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
    }
    .section-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 20px;
    }
    .section-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }
    .currency-tag { font-size: 0.75rem; color: #64748b; font-weight: 600; }
    .badge-active {
      font-size: 0.75rem;
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      padding: 4px 10px;
      border-radius: 6px;
      font-weight: 700;
    }

    .bars-container {
      display: flex;
      justify-content: space-between;
      align-items: flex-end;
      height: 180px;
      padding: 0 10px;
    }
    .month-col {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 8px;
      height: 100%;
      justify-content: flex-end;
    }
    .bar-track {
      width: 38px;
      height: 120px;
      background: rgba(255, 255, 255, 0.04);
      border-radius: 6px;
      display: flex;
      align-items: flex-end;
      overflow: hidden;
    }
    .bar-fill {
      width: 100%;
      background: linear-gradient(180deg, #10b981, #059669);
      border-radius: 6px 6px 0 0;
      transition: height 0.6s ease;
    }
    .bar-amount { font-size: 0.72rem; font-weight: 700; color: #f1f5f9; }
    .bar-label { font-size: 0.75rem; color: #94a3b8; }

    .empty-state-box {
      display: flex;
      flex-direction: column;
      align-items: center;
      text-align: center;
      padding: 36px 20px;
      gap: 10px;
    }
    .empty-icon { font-size: 2.4rem; }
    .empty-title { font-weight: 700; font-size: 1.05rem; color: #f1f5f9; }
    .empty-desc { max-width: 500px; font-size: 0.82rem; color: #94a3b8; line-height: 1.5; margin: 0; }
    .btn-goto-api {
      margin-top: 8px;
      background: rgba(14, 165, 233, 0.15);
      border: 1px solid rgba(14, 165, 233, 0.4);
      color: #38bdf8;
      padding: 8px 18px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.8rem;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-goto-api:hover { background: rgba(14, 165, 233, 0.25); }
    .empty-table-cell { padding: 0 !important; }

    .table-responsive { overflow-x: auto; }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
      text-align: left;
    }
    .data-table th {
      padding: 10px 14px;
      color: #94a3b8;
      font-weight: 600;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .data-table td {
      padding: 12px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      color: #cbd5e1;
    }
    .cell-title { max-width: 280px; color: #f1f5f9; }
    .cr-pill {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.75rem;
    }
    .cr-pill.high { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .cr-pill.normal { background: rgba(56, 189, 248, 0.15); color: #38bdf8; }
  `]
})
export class ShopPerformanceComponent implements OnInit {
  etsyApi = inject(EtsyApiService);
  router = inject(Router);

  // Initialized to 0 - Zero fake data policy!
  totalGrossRevenue = 0;
  totalOrders = 0;
  totalUnitsSold = 0;
  avgOrderValue = 0;
  avgConversionRate = 0;
  maxRevenue = 5000;
  toastMessage = '';

  monthlyHistory: PerformanceSnapshot[] = [];
  topProducts: ProductTraffic[] = [];

  ngOnInit(): void {
    this.refreshPerformance();
  }

  refreshPerformance(): void {
    // 1. Fetch real financial performance from VDS SQLite
    this.etsyApi.getFinancialPerformance('this_month').subscribe({
      next: (perf: any) => {
        if (perf) {
          const gross = Number(perf.grossSalesUsd ?? perf.grossSales ?? 0);
          const orders = Number(perf.orderCount ?? 0);
          if (gross > 0) {
            this.totalGrossRevenue = gross;
            this.totalOrders = orders;
            this.totalUnitsSold = orders;
            this.avgOrderValue = orders > 0 ? (gross / orders) : 0;
          }
        }
      }
    });

    // 2. Fetch real monthly history from VDS SQLite
    this.etsyApi.getShopPerformanceHistory().subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.monthlyHistory = data.map((d: any) => ({
            period: d.period || d.snapshot_period || 'Ay',
            orderCount: d.orderCount || d.order_count || 0,
            unitsSold: d.unitsSold || d.units_sold || 0,
            grossRevenue: d.grossRevenue || d.gross_revenue || 0,
            avgOrderValue: d.avgOrderValue || d.avg_order_value || 0,
            conversionRate: d.conversionRate || d.conversion_rate || 0
          }));
          const max = Math.max(...this.monthlyHistory.map(m => m.grossRevenue));
          if (max > 0) this.maxRevenue = max * 1.2;
        }
      }
    });

    this.etsyApi.verifyApiConnection();
    this.toastMessage = '⚡ Mağaza verileri VDS sunucusundan sorgulandı.';
    setTimeout(() => this.toastMessage = '', 3500);
  }
}
