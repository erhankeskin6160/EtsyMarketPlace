import { Component, OnInit, ElementRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { DailyBrief, FinancialSummary } from '../../core/models/etsy.models';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="dashboard-root">
      <!-- HERO KPI STRIP -->
      <section class="kpi-grid">
        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">💳 CİRO & GELİR</span>
            <span class="kpi-tag text-emerald">+18.4%</span>
          </div>
          <div class="kpi-value text-emerald">
            {{ formatCurrency(summary.grossSales, summary.grossSales / apiService.exchangeRate()) }}
          </div>
          <div class="kpi-sub">
            Brüt Satış Cirosu (Eylül 2026)
          </div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">💰 GERÇEK NET KÂR</span>
            <span class="kpi-tag text-cyan">%43.8 Marj</span>
          </div>
          <div class="kpi-value text-cyan">
            {{ formatCurrency(summary.realNetProfit, summary.realNetProfit / apiService.exchangeRate()) }}
          </div>
          <div class="kpi-sub">
            Tüm Kesinti & Maliyet Sonrası Net
          </div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">🚚 NET BANKA TRANSFERİ</span>
            <span class="kpi-tag text-purple">4 Ödeme</span>
          </div>
          <div class="kpi-value text-purple">
            {{ formatCurrency(summary.bankPayoutsTotal, summary.bankPayoutsTotal / apiService.exchangeRate()) }}
          </div>
          <div class="kpi-sub">
            Hesaba Aktarılan Mevduat
          </div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">🏷️ AKTİF İLAN SAYISI</span>
            <span class="kpi-tag text-orange">Portföy</span>
          </div>
          <div class="kpi-value text-orange">
            84 İlan
          </div>
          <div class="kpi-sub">
            3 Yeni Taslak Beklemede
          </div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">📢 ETSY KESİNTİ & REKLAM</span>
            <span class="kpi-tag text-danger">Komisyon</span>
          </div>
          <div class="kpi-value text-danger">
            {{ formatCurrency(summary.etsyFees, summary.etsyFees / apiService.exchangeRate()) }}
          </div>
          <div class="kpi-sub">
            İşlem & Listing Kesintileri
          </div>
        </div>
      </section>

      <!-- QUICK ACTION HUB -->
      <section class="action-hub-grid">
        <a routerLink="/finance/accounting" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-orange">FİNANS & MUHASEBE</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">💳 Finans Modülü</h4>
          <p class="action-desc">Ödeme defteri, komisyonlar ve banka transferleri dökümü</p>
        </a>

        <a routerLink="/listings/creator" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-cyan">TREND & TASLAK</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">🛍️ AI Ürün Bul & Taslak</h4>
          <p class="action-desc">Trend ürün araştırması ve tek tıkla taslak listeleme</p>
        </a>

        <a routerLink="/ai-studio" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-purple">AI GÖRSEL STÜDYO</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">🎨 AI Görsel Studio</h4>
          <p class="action-desc">AI ile stüdyo kalitesinde ürün fotoğrafları oluşturma</p>
        </a>

        <a routerLink="/analytics/shop" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-emerald">MAĞAZA DENETİMİ</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">🏬 Mağazama Git & AI Denetim</h4>
          <p class="action-desc">Siparişler, SEO skoru ve AI mağaza denetim raporu</p>
        </a>
      </section>

      <!-- MIDDLE: RECENT ORDERS & GEMINI SPARK AI ADVISOR -->
      <section class="middle-grid">
        <!-- Live Order Stream -->
        <div class="glass-card orders-card">
          <div class="card-header-flex">
            <div>
              <h3 class="section-title">🚚 Canlı Sipariş Akışı & Maliyet Takibi</h3>
              <span class="section-sub">Son gelen Etsy siparişleri ve kargo takip durumları</span>
            </div>
            <a routerLink="/orders" class="link-view-all">Tümünü Gör (18) →</a>
          </div>

          <div class="order-list">
            <div class="order-item" *ngFor="let order of sampleOrders">
              <div class="order-id-col">
                <span class="order-num">#{{ order.id }}</span>
                <span class="order-date">{{ order.date }}</span>
              </div>
              <div class="order-buyer-col">
                <div class="order-buyer-name">{{ order.buyer }}</div>
                <div class="order-item-title">{{ order.item }}</div>
              </div>
              <div class="order-status-col">
                <span class="badge" [class.badge-success]="order.status === 'Kargolandı'" [class.badge-new]="order.status === 'Hazırlanıyor'">
                  {{ order.status }}
                </span>
              </div>
              <div class="order-total-col">
                <div class="order-amount">
                  {{ apiService.isTryCurrency() ? (order.usdTotal * apiService.exchangeRate() | number:'1.2-2') + ' ₺' : '$' + (order.usdTotal | number:'1.2-2') }}
                </div>
                <span class="order-carrier">{{ order.carrier }}</span>
              </div>
            </div>
          </div>
        </div>

        <!-- Gemini Spark AI Co-Pilot -->
        <div class="glass-card copilot-card">
          <div class="copilot-header">
            <div class="spark-icon">✨</div>
            <div>
              <h3 class="section-title">Gemini Spark AI Büyüme Asistanı</h3>
              <span class="section-sub">Akıllı mağaza içgörüleri & marj analizleri</span>
            </div>
          </div>

          <div class="insight-box">
            <div class="insight-badge">🚀 BÜYÜME TAVSİYESİ</div>
            <p class="insight-text">
              "Kişiselleştirilmiş 3D Baskılı Hediyelik" kategorisinde dönüşüm oranınız <b>%3.8</b> ile pazar ortalamasının üzerinde! 13 etiketten 4'ünde "Anneler Günü" güncellemesi yaparak arama görünürlüğünü %25 artırabilirsiniz.
            </p>
          </div>

          <div class="copilot-stats">
            <div class="copilot-stat">
              <span class="copilot-stat-label">Aylık AI Token Kotası</span>
              <span class="copilot-stat-val text-cyan">2.000.000</span>
            </div>
            <div class="copilot-stat">
              <span class="copilot-stat-label">Harcanan Token</span>
              <span class="copilot-stat-val text-orange">124.500 (%6.2)</span>
            </div>
          </div>

          <a routerLink="/finance/ai-analysis" class="btn btn-secondary w-full">
            🧠 Derinlemesine AI Finans Analizini Aç
          </a>
        </div>
      </section>

      <!-- BOTTOM: CHART SECTION -->
      <section class="glass-card chart-section">
        <div class="card-header-flex">
          <div>
            <h3 class="section-title">📊 30 Günlük Ciro & Net Kâr Trend Grafiği</h3>
            <span class="section-sub">Günlük brüt satış ve net kâr marjının karşılaştırmalı performansı</span>
          </div>
          <div class="chart-badges">
            <span class="legend-badge legend-revenue">● Brüt Satış</span>
            <span class="legend-badge legend-profit">● Net Kâr</span>
          </div>
        </div>
        <div class="chart-wrapper">
          <canvas #trendChart></canvas>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .dashboard-root {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }

    /* KPI GRID */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
    }
    .kpi-card {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      min-height: 110px;
    }
    .kpi-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .kpi-label {
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--text-muted);
      letter-spacing: 0.05em;
    }
    .kpi-tag {
      font-size: 0.72rem;
      font-weight: 700;
    }
    .kpi-value {
      font-size: 1.45rem;
      font-weight: 800;
      margin: 4px 0;
      letter-spacing: -0.02em;
    }
    .kpi-sub {
      font-size: 0.73rem;
      color: var(--text-muted);
    }

    /* ACTION HUB */
    .action-hub-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 16px;
    }
    .action-card {
      text-decoration: none;
      display: flex;
      flex-direction: column;
      gap: 6px;
      cursor: pointer;
    }
    .action-card:hover {
      transform: translateY(-2px);
    }
    .action-top {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .action-badge {
      font-size: 0.68rem;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 6px;
      letter-spacing: 0.05em;
    }
    .badge-orange { background: rgba(249, 115, 22, 0.15); color: #fb923c; border: 1px solid rgba(249, 115, 22, 0.3); }
    .badge-cyan { background: rgba(6, 182, 212, 0.15); color: #22d3ee; border: 1px solid rgba(6, 182, 212, 0.3); }
    .badge-purple { background: rgba(139, 92, 246, 0.15); color: #c084fc; border: 1px solid rgba(139, 92, 246, 0.3); }
    .badge-emerald { background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); }
    .action-arrow {
      color: var(--text-muted);
      font-size: 1.1rem;
    }
    .action-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #fff;
    }
    .action-desc {
      font-size: 0.78rem;
      color: var(--text-muted);
      line-height: 1.4;
    }

    /* MIDDLE GRID */
    .middle-grid {
      display: grid;
      grid-template-columns: 1.4fr 1fr;
      gap: 20px;
    }
    @media (max-width: 1024px) {
      .middle-grid { grid-template-columns: 1fr; }
    }
    .card-header-flex {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }
    .section-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #fff;
    }
    .section-sub {
      font-size: 0.75rem;
      color: var(--text-muted);
    }
    .link-view-all {
      font-size: 0.8rem;
      color: var(--etsy-orange);
      font-weight: 600;
      text-decoration: none;
    }

    .order-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .order-item {
      display: grid;
      grid-template-columns: 80px 1.5fr 110px 100px;
      align-items: center;
      gap: 12px;
      padding: 10px 12px;
      background: rgba(11, 15, 25, 0.6);
      border: 1px solid var(--border-color);
      border-radius: 8px;
    }
    .order-num { font-weight: 700; font-size: 0.82rem; color: #fff; display: block; }
    .order-date { font-size: 0.7rem; color: var(--text-muted); }
    .order-buyer-name { font-size: 0.82rem; font-weight: 600; color: #e2e8f0; }
    .order-item-title { font-size: 0.72rem; color: var(--text-muted); white-space: nowrap; overflow: hidden; text-overflow: ellipsis; max-width: 180px; }
    .order-amount { font-weight: 700; font-size: 0.85rem; color: #10b981; }
    .order-carrier { font-size: 0.68rem; color: var(--text-muted); display: block; }

    /* COPILOT CARD */
    .copilot-header {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-bottom: 16px;
    }
    .spark-icon {
      width: 36px;
      height: 36px;
      border-radius: 10px;
      background: linear-gradient(135deg, #06b6d4, #3b82f6);
      display: flex;
      align-items: center;
      justify-content: center;
      font-size: 1.2rem;
    }
    .insight-box {
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(59, 130, 246, 0.3);
      border-radius: 10px;
      padding: 14px;
      margin-bottom: 16px;
    }
    .insight-badge {
      font-size: 0.68rem;
      font-weight: 700;
      color: #38bdf8;
      margin-bottom: 6px;
    }
    .insight-text {
      font-size: 0.82rem;
      color: #cbd5e1;
      line-height: 1.5;
    }
    .copilot-stats {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
      margin-bottom: 16px;
    }
    .copilot-stat {
      background: #090d16;
      border: 1px solid var(--border-color);
      padding: 10px;
      border-radius: 8px;
    }
    .copilot-stat-label {
      font-size: 0.7rem;
      color: var(--text-muted);
      display: block;
    }
    .copilot-stat-val {
      font-size: 0.95rem;
      font-weight: 700;
      margin-top: 2px;
      display: block;
    }
    .w-full { width: 100%; text-align: center; }

    /* CHART SECTION */
    .chart-section {
      min-height: 320px;
    }
    .chart-badges {
      display: flex;
      gap: 12px;
      font-size: 0.78rem;
      font-weight: 600;
    }
    .legend-revenue { color: #3b82f6; }
    .legend-profit { color: #10b981; }
    .chart-wrapper {
      position: relative;
      height: 240px;
      width: 100%;
    }
  `]
})
export class DashboardComponent implements OnInit {
  apiService = inject(EtsyApiService);

  @ViewChild('trendChart', { static: true }) chartCanvas!: ElementRef<HTMLCanvasElement>;
  chartInstance?: Chart;

  summary: FinancialSummary = {
    period: 'Eylül 2026',
    grossSales: 45261.97,
    etsyFees: 10173.80,
    netRevenue: 29674.83,
    productCosts: 9835.33,
    realNetProfit: 19839.50,
    bankPayoutsTotal: 25701.31
  };

  sampleOrders = [
    { id: '34910294', date: 'Bugün 14:22', buyer: 'Sarah Jenkins (US)', item: 'Custom 3D Articulated Dragon', status: 'Hazırlanıyor', usdTotal: 48.50, carrier: 'Aras Global' },
    { id: '34908172', date: 'Dün 21:05', buyer: 'Michael Brown (UK)', item: 'Minimalist Desk Planter', status: 'Kargolandı', usdTotal: 34.00, carrier: 'ShipEntegra' },
    { id: '34899210', date: '30 Eyl 18:40', buyer: 'Emma Watson (CA)', item: 'Geometric Ring Dish - Gold', status: 'Kargolandı', usdTotal: 62.00, carrier: 'Navlungo' },
    { id: '34892115', date: '29 Eyl 11:15', buyer: 'David Miller (DE)', item: 'Vintage Industrial Lamp Base', status: 'Kargolandı', usdTotal: 85.00, carrier: 'Shiptomore' }
  ];

  ngOnInit(): void {
    this.apiService.getFinancialSummary().subscribe({
      next: data => {
        this.summary = data;
      },
      error: () => {
        // Keep initial values as demo fallback
      }
    });

    this.initChart();
  }

  formatCurrency(tryAmount: number, usdAmount: number): string {
    if (this.apiService.isTryCurrency()) {
      return (tryAmount).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 }) + ' ₺';
    } else {
      return '$' + (usdAmount).toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
    }
  }

  initChart(): void {
    const ctx = this.chartCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const days = ['1 Eyl', '5 Eyl', '10 Eyl', '15 Eyl', '20 Eyl', '25 Eyl', '30 Eyl'];
    const revenueData = [1200, 1850, 1600, 2400, 2100, 2900, 3100];
    const profitData = [550, 820, 710, 1100, 940, 1320, 1450];

    this.chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels: days,
        datasets: [
          {
            label: 'Brüt Satış',
            data: revenueData,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59, 130, 246, 0.1)',
            fill: true,
            tension: 0.35,
            borderWidth: 2.5
          },
          {
            label: 'Net Kâr',
            data: profitData,
            borderColor: '#10b981',
            backgroundColor: 'rgba(16, 185, 129, 0.1)',
            fill: true,
            tension: 0.35,
            borderWidth: 2.5
          }
        ]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: { display: false }
        },
        scales: {
          x: {
            grid: { color: 'rgba(255, 255, 255, 0.05)' },
            ticks: { color: '#94a3b8' }
          },
          y: {
            grid: { color: 'rgba(255, 255, 255, 0.05)' },
            ticks: {
              color: '#94a3b8',
              callback: (v) => '$' + v
            }
          }
        }
      }
    });
  }
}
