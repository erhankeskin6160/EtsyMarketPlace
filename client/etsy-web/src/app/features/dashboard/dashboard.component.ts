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
      <!-- HERO KPI STRIP (MATCHING WINFORMS DESKTOP) -->
      <section class="kpi-grid">
        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">💳 BU AYKI BRÜT CİRO</span>
            <span class="kpi-tag text-emerald">+12.4%</span>
          </div>
          <div class="kpi-value text-emerald">
            &#36;{{ grossSalesUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ grossSalesUsd * apiService.exchangeRate() | number:'1.0-0' }})</span>
          </div>
          <div class="kpi-sub">Etsy Mağaza Satış Geliri</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">💰 GERÇEK NET KÂR</span>
            <span class="kpi-tag text-cyan">%38.5 Marj</span>
          </div>
          <div class="kpi-value text-cyan">
            &#36;{{ netProfitUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ netProfitUsd * apiService.exchangeRate() | number:'1.0-0' }})</span>
          </div>
          <div class="kpi-sub">Maliyet, Komisyonlar Düşülmüş</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">📦 TOPLAM SİPARİŞ</span>
            <span class="kpi-tag text-purple">1 Sipariş</span>
          </div>
          <div class="kpi-value text-purple">1 Sipariş</div>
          <div class="kpi-sub">Dönem İçi Başarılı Satış</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">🏷️ AKTİF İLAN SAYISI</span>
            <span class="kpi-tag text-orange">Portföy</span>
          </div>
          <div class="kpi-value text-orange">46 Aktif İlan</div>
          <div class="kpi-sub">Mağaza Portföyü</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">📢 ETSY KESİNTİLERİ / REKLAM</span>
            <span class="kpi-tag text-danger">Giderler</span>
          </div>
          <div class="kpi-value text-danger">
            -&#36;{{ etsyFeesUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ etsyFeesUsd * apiService.exchangeRate() | number:'1.0-0' }})</span>
          </div>
          <div class="kpi-sub">Reklam: -$5.47, Komisyon: -$5.99</div>
        </div>
      </section>

      <!-- QUICK ACTION HUB -->
      <section class="action-hub-grid">
        <a routerLink="/finance/accounting" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-orange">FİNANS MODÜLÜ</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">💳 Finans & Muhasebe</h4>
          <p class="action-desc">Ödeme defteri, komisyonlar ve banka transferleri dökümü</p>
        </a>

        <a routerLink="/listings/fast-creator" class="glass-card action-card">
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
          <p class="action-desc">AI ile stüdyo kalitesinde ürün fotoğrafları oluştur</p>
        </a>

        <a routerLink="/analytics/ai-audit" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-emerald">MAĞAZA DENETİMİ</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">🏬 Mağaza Git & AI Denetim</h4>
          <p class="action-desc">Siparişler, SEO skoru ve AI mağaza denetim raporu</p>
        </a>
      </section>

      <!-- MIDDLE: RECENT ORDERS & GEMINI SPARK STORE COPILOT -->
      <section class="middle-grid">
        <!-- Live Order Stream Matching WinForms Image 2 -->
        <div class="glass-card orders-card">
          <div class="card-header-flex">
            <div>
              <h3 class="section-title">📦 Son Siparişler Canlı Satış Akışı</h3>
              <span class="section-sub">Toplam 1 sipariş listelendi</span>
            </div>
            <a routerLink="/orders" class="link-view-all">Tüm Siparişler (1) →</a>
          </div>

          <div class="table-container">
            <table class="desktop-orders-table">
              <thead>
                <tr>
                  <th>Tarih</th>
                  <th>Sipariş No</th>
                  <th>Satılan Ürün</th>
                  <th>Adet</th>
                  <th>Tutar ($)</th>
                  <th>Net Kâr ($ / ₺)</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let o of liveOrders">
                  <td class="text-muted">{{ o.date }}</td>
                  <td class="font-mono text-cyan">#{{ o.receiptId }}</td>
                  <td class="font-semibold">{{ o.title }}</td>
                  <td>{{ o.quantity }}</td>
                  <td class="text-emerald font-bold">&#36;{{ o.totalUsd | number:'1.2-2' }}</td>
                  <td class="text-cyan font-bold">+&#36;{{ o.netProfitUsd | number:'1.2-2' }} <span class="text-xs text-muted">(₺{{ o.netProfitUsd * apiService.exchangeRate() | number:'1.0-0' }})</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- Akıllı Mağaza Asistanı (Store Copilot) Matching WinForms Image 2 -->
        <div class="glass-card copilot-card">
          <div class="copilot-header">
            <div class="spark-icon">💡</div>
            <div>
              <h3 class="section-title">Akıllı Mağaza Asistanı (Store Copilot)</h3>
              <span class="section-sub">Canlı Satış & Büyüme Analitiği</span>
            </div>
          </div>

          <div class="copilot-insights">
            <div class="insight-item">
              <span class="insight-label text-emerald">🌟 En Çok Ciro Getiren Ürün</span>
              <p class="insight-body">
                <b>'3D Printed Butterfly Trainer | Colorful Safe Knife'</b> bu ay toplam <b>&#36;35,91</b> ciro sağlayarak mağazanızın yıldız ürünü oldu.
              </p>
            </div>

            <div class="insight-item">
              <span class="insight-label text-cyan">📢 Reklam / Komisyon Durumu</span>
              <p class="insight-body">
                Reklam harcaması cironuzun %15,4'i (&#36;5,47), Etsy komisyonları %16,8'i (&#36;5,99) seviyesindedir. Toplam net kâr marjınız <b>%38,5</b>.
              </p>
            </div>

            <div class="insight-item">
              <span class="insight-label text-purple">🚀 Büyüme / SEO Tavsiyesi</span>
              <p class="insight-body">
                Ürün başlıklarında ve ilk 3 etiketinde en çok aranan uzun kuyruklu anahtar kelimeleri kullanarak organik trafiğinizi <b>%25</b> artırabilirsiniz.
              </p>
              <a routerLink="/research/market" class="btn-micro-action">🔍 Pazar Araştırması</a>
            </div>
          </div>
        </div>
      </section>

      <!-- BOTTOM: CHART SECTION MATCHING WINFORMS IMAGE 2 -->
      <section class="glass-card chart-section">
        <div class="card-header-flex">
          <div>
            <h3 class="section-title">📈 BU AYIN GÜNLÜK GELİR VE NET KÂR TRENDİ ($)</h3>
            <span class="section-sub">Ekim 2026 Gerçekleşen Satış ve Kâr Seyri</span>
          </div>
          <div class="chart-badges">
            <span class="legend-badge legend-revenue">● Brüt Satış ($)</span>
            <span class="legend-badge legend-profit">● Net Kâr ($)</span>
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

    .table-container {
      overflow-x: auto;
      margin-top: 10px;
    }
    .desktop-orders-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
    }
    .desktop-orders-table th {
      text-align: left;
      padding: 10px;
      color: var(--text-muted);
      border-bottom: 1px solid var(--border-color);
      font-weight: 600;
    }
    .desktop-orders-table td {
      padding: 12px 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }
    .kpi-sub-try {
      font-size: 0.92rem;
      font-weight: 500;
      color: #94a3b8;
    }
    .copilot-insights {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .insight-item {
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 8px;
      padding: 12px;
    }
    .insight-label {
      font-size: 0.75rem;
      font-weight: 700;
      display: block;
      margin-bottom: 4px;
    }
    .insight-body {
      font-size: 0.8rem;
      color: #cbd5e1;
      line-height: 1.45;
      margin: 0;
    }
    .btn-micro-action {
      display: inline-block;
      margin-top: 8px;
      font-size: 0.75rem;
      color: #38bdf8;
      text-decoration: none;
      font-weight: 600;
    }
    .btn-micro-action:hover {
      text-decoration: underline;
    }

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
    .legend-profit { color: #f97316; }
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

  // Exact desktop live numbers from WinForms Image 2
  grossSalesUsd = 35.57;
  netProfitUsd = 13.69;
  etsyFeesUsd = 11.46;

  liveOrders = [
    {
      date: '01.10.2026',
      receiptId: '4188710928',
      title: '3D Printed Butterfly Trainer | Colorful Safe Knife | Cosplay Display Prop',
      quantity: 1,
      totalUsd: 35.91,
      netProfitUsd: 14.62
    }
  ];

  ngOnInit(): void {
    this.initChart();
  }

  initChart(): void {
    const ctx = this.chartCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    const days = ['01.10.2026', '02.10.2026', '03.10.2026'];
    const revenueData = [35.91, 0, 0];
    const profitData = [14.62, 0, 0];

    this.chartInstance = new Chart(ctx, {
      type: 'line',
      data: {
        labels: days,
        datasets: [
          {
            label: 'Brüt Satış ($)',
            data: revenueData,
            borderColor: '#3b82f6',
            backgroundColor: 'rgba(59, 130, 246, 0.15)',
            fill: false,
            tension: 0.1,
            borderWidth: 2,
            pointRadius: 6,
            pointBackgroundColor: '#3b82f6'
          },
          {
            label: 'Net Kâr ($)',
            data: profitData,
            borderColor: '#f97316',
            backgroundColor: 'rgba(249, 115, 22, 0.15)',
            fill: false,
            tension: 0.1,
            borderWidth: 2,
            pointRadius: 6,
            pointBackgroundColor: '#f97316'
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
            },
            min: 0,
            max: 40
          }
        }
      }
    });
  }
}
