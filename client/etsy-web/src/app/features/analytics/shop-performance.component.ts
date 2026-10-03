import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
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
  imports: [CommonModule, FormsModule],
  template: `
    <div class="perf-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">🏬</div>
          <div>
            <h1 class="page-title">Mağazam Performansı & Ziyaretçi Analitiği</h1>
            <p class="page-subtitle">Ziyaretçi trafiği, görüntülenme, favori sayıları ve ürün bazlı dönüşüm oranı (Conversion Rate %) istatistikleri</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-sync" (click)="refreshPerformance()">
            ⚡ Canlı Etsy Metriklerini Senkronize Et
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">AYLIK BRÜT CİRO</span>
          <span class="kpi-val text-green">\${{ totalGrossRevenue | number:'1.2-2' }}</span>
          <span class="kpi-sub">Geçen Aya Göre: +%18.4</span>
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
          <h2 class="section-title">Aylık Büyüme & Satış Trendi (Son 6 Ay)</h2>
          <span class="currency-tag">USD ($) Bazlı</span>
        </div>

        <div class="bars-container">
          <div *ngFor="let m of monthlyHistory" class="month-col">
            <div class="bar-track">
              <div class="bar-fill" [style.height.%]="(m.grossRevenue / maxRevenue) * 100"></div>
            </div>
            <span class="bar-amount">\${{ m.grossRevenue | number:'1.0-0' }}</span>
            <span class="bar-label">{{ m.period }}</span>
          </div>
        </div>
      </div>

      <!-- TOP LISTING TRAFFIC & PERFORMANCE TABLE -->
      <div class="traffic-section glass-card">
        <div class="section-header">
          <h2 class="section-title">En Çok Satan & Trafik Çeken İlk 10 İlan</h2>
          <span class="badge-active">Canlı Analiz</span>
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
      padding: 10px 20px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
      transition: all 0.2s;
    }
    .btn-sync:hover { filter: brightness(1.1); transform: translateY(-1px); }

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

    .timeline-section, .traffic-section {
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
    .currency-tag { font-size: 0.75rem; color: #94a3b8; }
    .badge-active {
      font-size: 0.72rem;
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 700;
    }

    .bars-container {
      display: flex;
      justify-content: space-around;
      align-items: flex-end;
      height: 180px;
      padding-top: 20px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
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

  totalGrossRevenue = 6420.50;
  totalOrders = 186;
  totalUnitsSold = 214;
  avgOrderValue = 34.52;
  avgConversionRate = 3.2;
  maxRevenue = 8000;

  monthlyHistory: PerformanceSnapshot[] = [
    { period: 'Mayıs', orderCount: 22, unitsSold: 26, grossRevenue: 840, avgOrderValue: 38.18, conversionRate: 2.8 },
    { period: 'Haziran', orderCount: 28, unitsSold: 31, grossRevenue: 1020, avgOrderValue: 36.42, conversionRate: 3.1 },
    { period: 'Temmuz', orderCount: 34, unitsSold: 39, grossRevenue: 1250, avgOrderValue: 36.76, conversionRate: 3.4 },
    { period: 'Ağustos', orderCount: 30, unitsSold: 35, grossRevenue: 1100, avgOrderValue: 36.66, conversionRate: 2.9 },
    { period: 'Eylül', orderCount: 42, unitsSold: 49, grossRevenue: 1480, avgOrderValue: 35.23, conversionRate: 3.5 },
    { period: 'Ekim (Canlı)', orderCount: 30, unitsSold: 34, grossRevenue: 730, avgOrderValue: 24.33, conversionRate: 3.2 }
  ];

  topProducts: ProductTraffic[] = [
    {
      listingId: '189204859',
      title: 'Articulated Crystal Dragon 3D Printed Figurine',
      views: 4820,
      favorites: 612,
      orders: 78,
      revenue: 1950.00,
      conversionRate: 4.1
    },
    {
      listingId: '190412851',
      title: 'Handmade Turkish Ceramic Coffee Mug Aesthetic Kitchen',
      views: 3140,
      favorites: 390,
      orders: 45,
      revenue: 1440.00,
      conversionRate: 3.6
    },
    {
      listingId: '191054332',
      title: 'Dainty Gold Stacking Ring 14k Gold Minimalist Band',
      views: 2800,
      favorites: 420,
      orders: 38,
      revenue: 1064.00,
      conversionRate: 3.2
    },
    {
      listingId: '192451890',
      title: 'Custom Wooden Name Puzzle for Toddlers & Kids Gift',
      views: 2100,
      favorites: 280,
      orders: 25,
      revenue: 875.00,
      conversionRate: 2.8
    }
  ];

  ngOnInit(): void {
    this.refreshPerformance();
  }

  refreshPerformance(): void {
    this.etsyApi.getShopPerformanceHistory().subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          // Map historical performance data
        }
      }
    });
  }
}
