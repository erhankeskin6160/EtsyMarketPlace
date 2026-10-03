import { Component, OnInit, OnDestroy, ElementRef, ViewChild, inject } from '@angular/core';
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
      <section class="kpi-grid" [class.has-popover-open]="isExpensesPopoverOpen">
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

        <div class="glass-card kpi-card kpi-card-expenses"
             [class.hover-active]="isHoveringExpenses"
             [class.popover-open]="isExpensesPopoverOpen"
             (mouseenter)="onExpensesMouseEnter()"
             (mouseleave)="onExpensesMouseLeave()"
             (click)="toggleExpensesPopover()"
             title="Maliyet detayını görmek için üzerinde birkaç saniye bekleyin veya tıklayın">
          <div class="kpi-header">
            <span class="kpi-label">📢 ETSY KESİNTİLERİ / REKLAM</span>
            <div class="kpi-header-right">
              <!-- Animated mini circular countdown / loading badge -->
              <span class="hover-timer-badge" *ngIf="isHoveringExpenses && !isExpensesPopoverOpen">
                <span class="pulse-dot"></span>
                <span>Detay...</span>
              </span>
              <span class="kpi-tag text-danger" *ngIf="!isHoveringExpenses || isExpensesPopoverOpen">Giderler</span>
            </div>
          </div>
          <div class="kpi-value text-danger">
            -&#36;{{ etsyFeesUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ (etsyFeesUsd * apiService.exchangeRate()) | number:'1.0-0' }})</span>
          </div>
          <div class="kpi-sub">
            Reklam: -$5.47, Komisyon: -$5.99 
            <span class="kpi-hint-text">💡 (Detay için durun)</span>
          </div>

          <!-- DETAILED EXPENSES POPOVER (MATCHING WINFORMS ANIMATED TOOLTIP) -->
          <div class="expenses-popover-box" 
               *ngIf="isExpensesPopoverOpen"
               (click)="$event.stopPropagation()"
               (mouseenter)="onPopoverMouseEnter()"
               (mouseleave)="onPopoverMouseLeave()">
            
            <!-- Popover Header -->
            <div class="popover-header">
              <div class="popover-title-row">
                <span class="popover-icon">📢</span>
                <div>
                  <h4 class="popover-title">Etsy Kesintileri & Reklam Harcamaları Analizi</h4>
                  <span class="popover-subtitle">
                    Toplam Etsy Kesintisi: -₺{{ (etsyFeesUsd * apiService.exchangeRate()) | number:'1.2-2' }} (-&#36;{{ etsyFeesUsd | number:'1.2-2' }}) | Cironun %32.2'si
                  </span>
                </div>
              </div>
              <button type="button" class="btn-close-popover" (click)="closeExpensesPopover()" title="Kapat">✕</button>
            </div>

            <!-- 3 KPI Cards Cluster -->
            <div class="popover-kpi-row">
              <div class="mini-kpi-card red">
                <span class="mini-kpi-label">📢 İç Reklam (Etsy Ads)</span>
                <span class="mini-kpi-try">-₺268.00</span>
                <span class="mini-kpi-usd">-$5.47 (%47.7)</span>
              </div>
              <div class="mini-kpi-card orange">
                <span class="mini-kpi-label">🌐 Dış Reklam (Offsite)</span>
                <span class="mini-kpi-try">-₺0.00</span>
                <span class="mini-kpi-usd">-$0.00 (%0.0)</span>
              </div>
              <div class="mini-kpi-card purple">
                <span class="mini-kpi-label">📋 Etsy Komisyon & Harç</span>
                <span class="mini-kpi-try">-₺294.00</span>
                <span class="mini-kpi-usd">-$5.99 (%52.3)</span>
              </div>
            </div>

            <!-- Breakdown Table -->
            <div class="popover-table-wrap">
              <table class="popover-breakdown-table">
                <thead>
                  <tr>
                    <th>Tür</th>
                    <th>Kalem Adı</th>
                    <th>Oran / Tür</th>
                    <th>Tutar (TL / USD)</th>
                    <th>Açıklama / Muhasebe Mantığı</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let item of expensesBreakdown" [class.total-row]="item.isTotal">
                    <td>
                      <span class="type-pill" [ngClass]="item.typeClass">{{ item.type }}</span>
                    </td>
                    <td class="name-cell">{{ item.name }}</td>
                    <td class="rate-cell">{{ item.rate }}</td>
                    <td class="amount-cell">{{ item.amountText }}</td>
                    <td class="desc-cell">{{ item.desc }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <!-- Popover Footer Action -->
            <div class="popover-footer">
              <span class="footer-note">💡 Masaüstü canlı ödeme defteri ve Finans raporu ile %100 senkronizedir.</span>
              <a routerLink="/finance/accounting" class="btn-goto-accounting" (click)="closeExpensesPopover()">
                💳 Finans & Muhasebe Detay Tablosu →
              </a>
            </div>

          </div>
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
      position: relative;
      z-index: 10;
    }
    .kpi-grid.has-popover-open {
      z-index: 20000;
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

    /* EXPENSES CARD & HOVER POPOVER */
    .kpi-card-expenses {
      position: relative;
      cursor: pointer;
      transition: all 0.25s ease;
      z-index: 15;
    }
    .kpi-card-expenses:hover, .kpi-card-expenses.hover-active {
      border-color: rgba(239, 68, 68, 0.45);
      box-shadow: 0 4px 20px rgba(239, 68, 68, 0.15);
    }
    .kpi-card-expenses.popover-open {
      z-index: 20001;
    }
    .kpi-header-right {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .hover-timer-badge {
      display: inline-flex;
      align-items: center;
      gap: 5px;
      background: rgba(239, 68, 68, 0.2);
      border: 1px solid rgba(239, 68, 68, 0.5);
      color: #f87171;
      font-size: 0.68rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: 4px;
      animation: pulse 1s infinite;
    }
    .pulse-dot {
      width: 6px;
      height: 6px;
      border-radius: 50%;
      background: #ef4444;
      animation: blink 0.8s infinite alternate;
    }
    @keyframes blink {
      0% { opacity: 0.3; transform: scale(0.8); }
      100% { opacity: 1; transform: scale(1.2); }
    }
    .kpi-hint-text {
      color: #38bdf8;
      font-weight: 600;
      font-size: 0.68rem;
      margin-left: 4px;
    }

    /* THE POPOVER BOX */
    .expenses-popover-box {
      position: absolute;
      top: calc(100% + 10px);
      right: 0;
      width: 650px;
      max-width: 90vw;
      background: linear-gradient(135deg, rgba(17, 24, 39, 0.98) 0%, rgba(15, 23, 42, 0.99) 100%);
      border: 1px solid rgba(239, 68, 68, 0.4);
      border-radius: 12px;
      box-shadow: 0 16px 40px rgba(0, 0, 0, 0.8), 0 0 25px rgba(239, 68, 68, 0.15);
      backdrop-filter: blur(16px);
      z-index: 20002;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      cursor: default;
      animation: popoverFadeIn 0.2s ease-out;
    }
    @keyframes popoverFadeIn {
      from { opacity: 0; transform: translateY(-8px) scale(0.98); }
      to { opacity: 1; transform: translateY(0) scale(1); }
    }

    .popover-header {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 10px;
    }
    .popover-title-row {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .popover-icon {
      font-size: 1.4rem;
    }
    .popover-title {
      margin: 0;
      font-size: 0.92rem;
      font-weight: 800;
      color: #ffffff;
      line-height: 1.2;
    }
    .popover-subtitle {
      font-size: 0.72rem;
      color: #f87171;
      font-weight: 600;
      display: block;
      margin-top: 2px;
    }
    .btn-close-popover {
      background: rgba(255, 255, 255, 0.06);
      border: none;
      color: #94a3b8;
      width: 24px;
      height: 24px;
      border-radius: 4px;
      cursor: pointer;
      font-size: 0.8rem;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.15s;
    }
    .btn-close-popover:hover {
      background: rgba(239, 68, 68, 0.2);
      color: #ef4444;
    }

    /* 3 MINI KPI CARDS */
    .popover-kpi-row {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 10px;
    }
    .mini-kpi-card {
      border-radius: 8px;
      padding: 8px 10px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .mini-kpi-card.red {
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.3);
    }
    .mini-kpi-card.orange {
      background: rgba(249, 115, 22, 0.12);
      border: 1px solid rgba(249, 115, 22, 0.3);
    }
    .mini-kpi-card.purple {
      background: rgba(99, 102, 241, 0.12);
      border: 1px solid rgba(99, 102, 241, 0.3);
    }
    .mini-kpi-label {
      font-size: 0.68rem;
      font-weight: 700;
      color: #cbd5e1;
    }
    .mini-kpi-try {
      font-size: 0.95rem;
      font-weight: 800;
      color: #ffffff;
    }
    .mini-kpi-card.red .mini-kpi-try { color: #f87171; }
    .mini-kpi-card.orange .mini-kpi-try { color: #fb923c; }
    .mini-kpi-card.purple .mini-kpi-try { color: #a5b4fc; }
    .mini-kpi-usd {
      font-size: 0.68rem;
      color: #94a3b8;
    }

    /* BREAKDOWN TABLE */
    .popover-table-wrap {
      max-height: 250px;
      overflow-y: auto;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
    }
    .popover-breakdown-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.72rem;
      text-align: left;
    }
    .popover-breakdown-table th {
      background: #1e293b;
      color: #94a3b8;
      padding: 6px 10px;
      font-weight: 700;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      position: sticky;
      top: 0;
      z-index: 2;
    }
    .popover-breakdown-table td {
      padding: 5px 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      color: #cbd5e1;
    }
    .popover-breakdown-table tr:hover:not(.total-row) {
      background: rgba(255, 255, 255, 0.03);
    }
    .popover-breakdown-table .total-row {
      background: rgba(239, 68, 68, 0.15);
      border-top: 2px solid rgba(239, 68, 68, 0.4);
      font-weight: 800;
    }
    .popover-breakdown-table .total-row td {
      color: #ffffff;
      padding: 7px 10px;
    }
    .type-pill {
      font-size: 0.64rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      letter-spacing: 0.03em;
    }
    .badge-ad { background: rgba(239, 68, 68, 0.2); color: #f87171; }
    .badge-fee { background: rgba(99, 102, 241, 0.2); color: #a5b4fc; }
    .badge-refund { background: rgba(234, 179, 8, 0.2); color: #fde047; }
    .badge-total { background: #ef4444; color: #ffffff; }

    .name-cell { font-weight: 600; color: #f1f5f9; }
    .rate-cell { color: #94a3b8; }
    .amount-cell { font-weight: 700; color: #f87171; white-space: nowrap; }
    .desc-cell { color: #94a3b8; font-size: 0.68rem; line-height: 1.3; }

    /* FOOTER */
    .popover-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding-top: 10px;
      gap: 12px;
    }
    .footer-note {
      font-size: 0.68rem;
      color: #94a3b8;
    }
    .btn-goto-accounting {
      background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
      color: #ffffff;
      text-decoration: none;
      font-size: 0.75rem;
      font-weight: 700;
      padding: 6px 12px;
      border-radius: 6px;
      white-space: nowrap;
      transition: all 0.15s;
      box-shadow: 0 2px 8px rgba(249, 115, 22, 0.35);
    }
    .btn-goto-accounting:hover {
      filter: brightness(1.1);
      transform: translateY(-1px);
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
export class DashboardComponent implements OnInit, OnDestroy {
  apiService = inject(EtsyApiService);

  @ViewChild('trendChart', { static: true }) chartCanvas!: ElementRef<HTMLCanvasElement>;
  chartInstance?: Chart;

  // Exact desktop live numbers from WinForms Image 2
  grossSalesUsd = 35.57;
  netProfitUsd = 13.69;
  etsyFeesUsd = 11.46;

  // EXPENSES HOVER TOOLTIP POPOVER
  isHoveringExpenses = false;
  isExpensesPopoverOpen = false;
  private hoverTimer: any = null;
  private leaveTimer: any = null;

  expensesBreakdown = [
    { type: 'Reklam', typeClass: 'badge-ad', name: 'İç Reklam (Etsy Ads)', rate: 'Tıklama', amountText: '-₺268.00 (-$5.47)', desc: 'Etsy platform içi arama sponsorlu reklam harcaması', isTotal: false },
    { type: 'Reklam', typeClass: 'badge-ad', name: 'Dış Reklam (Offsite Ads)', rate: '%15', amountText: '-₺0.00 (-$0.00)', desc: 'Google & sosyal medya dış reklam satış komisyonu', isTotal: false },
    { type: 'Kesinti', typeClass: 'badge-fee', name: 'İşlem Komisyonu (Transaction Fee)', rate: '%6.5', amountText: '-₺113.80 (-$2.32)', desc: 'Ürün ve kargo tutarı üzerinden Etsy standart komisyonu', isTotal: false },
    { type: 'Kesinti', typeClass: 'badge-fee', name: 'Ödeme İşleme Ücreti (Payment Processing)', rate: '%6.5+3TL', amountText: '-₺116.80 (-$2.38)', desc: 'Etsy Payments güvenli ödeme tahsilat masrafı', isTotal: false },
    { type: 'Kesinti', typeClass: 'badge-fee', name: 'Yasal İşletim & KDV (Regulatory & VAT)', rate: '%1.5 + KDV', amountText: '-₺43.40 (-$0.88)', desc: 'Türkiye yasal işletim payı ve komisyon KDV\'si', isTotal: false },
    { type: 'Kesinti', typeClass: 'badge-fee', name: 'İlan Listeleme Ücreti (Listing Fees)', rate: '$0.20', amountText: '-₺20.00 (-$0.41)', desc: 'Ürün listeleme ve 4 aylık otomatik yenileme bedelleri', isTotal: false },
    { type: 'İade', typeClass: 'badge-refund', name: 'İptal ve İadeler (Refunds)', rate: '0 Adet', amountText: '-₺0.00 (-$0.00)', desc: 'Müşterilere iade edilen sipariş ve kargo tutarları', isTotal: false },
    { type: 'Toplam', typeClass: 'badge-total', name: 'TOPLAM GİDER & KESİNTİ', rate: 'Tümü', amountText: '-₺562.00 (-$11.46)', desc: 'Brüt cirodan düşülen tüm Etsy kesintileri ve reklam harcaması', isTotal: true }
  ];

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

  // --- HOVER TIMING & POPOVER LOGIC ---
  onExpensesMouseEnter(): void {
    if (this.leaveTimer) {
      clearTimeout(this.leaveTimer);
      this.leaveTimer = null;
    }
    this.isHoveringExpenses = true;
    if (!this.isExpensesPopoverOpen) {
      this.hoverTimer = setTimeout(() => {
        this.isExpensesPopoverOpen = true;
      }, 1200); // 1.2s delay as requested ("bir kaç saniye durunca")
    }
  }

  onExpensesMouseLeave(): void {
    if (this.hoverTimer) {
      clearTimeout(this.hoverTimer);
      this.hoverTimer = null;
    }
    this.isHoveringExpenses = false;
    this.leaveTimer = setTimeout(() => {
      this.isExpensesPopoverOpen = false;
    }, 350); // graceful leave buffer
  }

  onPopoverMouseEnter(): void {
    if (this.leaveTimer) {
      clearTimeout(this.leaveTimer);
      this.leaveTimer = null;
    }
    this.isHoveringExpenses = true;
    this.isExpensesPopoverOpen = true;
  }

  onPopoverMouseLeave(): void {
    this.onExpensesMouseLeave();
  }

  toggleExpensesPopover(): void {
    if (this.hoverTimer) {
      clearTimeout(this.hoverTimer);
      this.hoverTimer = null;
    }
    this.isExpensesPopoverOpen = !this.isExpensesPopoverOpen;
  }

  closeExpensesPopover(): void {
    if (this.hoverTimer) {
      clearTimeout(this.hoverTimer);
      this.hoverTimer = null;
    }
    this.isExpensesPopoverOpen = false;
    this.isHoveringExpenses = false;
  }

  ngOnDestroy(): void {
    if (this.hoverTimer) clearTimeout(this.hoverTimer);
    if (this.leaveTimer) clearTimeout(this.leaveTimer);
    if (this.chartInstance) this.chartInstance.destroy();
  }
}
