import { Component, OnInit, OnDestroy, ElementRef, ViewChild, inject, DestroyRef } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { CommonModule } from '@angular/common';
import { RouterModule } from '@angular/router';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService, EtsyRateLimitSnapshot, FinancialPerformanceDto } from '../../core/services/etsy-api.service';
import { DailyBrief } from '../../core/models/etsy.models';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

@Component({
  selector: 'app-dashboard',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslocoDirective],
  providers: [provideTranslocoScope('dashboard')],
  template: `
    <div class="dashboard-root" *transloco="let t">
      <div *ngIf="rateLimitStatus && (rateLimitStatus.isDailyLimitExceeded || rateLimitStatus.dailyLimit)" style="margin-bottom: 12px; padding: 10px 14px; border-radius: 10px; display: flex; align-items: center; justify-content: space-between; gap: 12px; font-size: 0.85rem;" [style.background]="rateLimitStatus.isDailyLimitExceeded ? 'rgba(239, 68, 68, 0.14)' : 'rgba(16, 185, 129, 0.10)'" [style.border]="rateLimitStatus.isDailyLimitExceeded ? '1px solid rgba(239, 68, 68, 0.4)' : '1px solid rgba(16, 185, 129, 0.25)'">
        <span [ngClass]="rateLimitStatus.isDailyLimitExceeded ? 'text-danger' : 'text-emerald'">
          <ng-container *ngIf="rateLimitStatus.isDailyLimitExceeded">
            {{ t('dashboard.rateLimit.exceeded', { remaining: rateLimitStatus.remainingToday ?? 0, limit: rateLimitStatus.dailyLimit ?? 5000, retry: formatRetryAfter(rateLimitStatus.retryAfterSeconds) }) }}
          </ng-container>
          <ng-container *ngIf="!rateLimitStatus.isDailyLimitExceeded">
            {{ t('dashboard.rateLimit.available', { remaining: rateLimitStatus.remainingToday ?? '—', limit: rateLimitStatus.dailyLimit ?? '—', perSecond: rateLimitStatus.perSecondLimit ?? '—' }) }}
          </ng-container>
        </span>
      </div>
      <!-- HERO KPI STRIP (MATCHING WINFORMS DESKTOP) -->
      <section class="kpi-grid" [class.has-popover-open]="isExpensesPopoverOpen">
        <ng-template #noFinanceValue>—</ng-template>
        <ng-template #noFinanceInsight>{{ t('dashboard.kpi.noFinanceInsight') }}</ng-template>
        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">{{ t('dashboard.kpi.grossRevenueLabel') }}</span>
            <span class="kpi-tag text-emerald">{{ financialPerformance ? 'API' : t('common.noData') }}</span>
          </div>
          <div class="kpi-value text-emerald">
            <ng-container *ngIf="financialPerformance; else noFinanceValue">&#36;{{ grossSalesUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ grossSalesUsd * apiService.exchangeRate() | number:'1.0-0' }})</span></ng-container>
          </div>
          <div class="kpi-sub">{{ financialPerformance?.ledgerWarning ? financialPerformance?.ledgerWarning : t('dashboard.kpi.grossRevenueSub') }}</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">{{ netProfitUsd >= 0 ? t('dashboard.kpi.netProfitLabel') : t('dashboard.kpi.netLossLabel') }}</span>
            <span class="kpi-tag" [ngClass]="netProfitUsd >= 0 ? 'text-cyan' : 'text-danger'">{{ financialPerformance ? (financialPerformance.netProfitMargin / 100 | percent:'1.0-1') : t('common.noData') }}</span>
          </div>
          <div class="kpi-value" [ngClass]="netProfitUsd >= 0 ? 'text-cyan' : 'text-danger'">
            <ng-container *ngIf="financialPerformance; else noFinanceValue">{{ netProfitUsd < 0 ? '-' : '' }}&#36;{{ (netProfitUsd < 0 ? -netProfitUsd : netProfitUsd) | number:'1.2-2' }} <span class="kpi-sub-try">({{ netProfitUsd < 0 ? '-₺' : '₺' }}{{ (netProfitUsd < 0 ? -netProfitUsd : netProfitUsd) * apiService.exchangeRate() | number:'1.0-0' }})</span></ng-container>
          </div>
          <div class="kpi-sub">{{ t('dashboard.kpi.netProfitSub') }}</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">{{ t('dashboard.kpi.ordersLabel') }}</span>
            <span class="kpi-tag text-purple">{{ financialPerformance ? t('dashboard.kpi.liveApiTag') : t('common.noData') }}</span>
          </div>
          <div class="kpi-value text-purple">
            <ng-container *ngIf="financialPerformance; else noFinanceValue">{{ t('dashboard.kpi.ordersUnit', { count: financialPerformance.receiptCount ?? liveOrders.length }) }}</ng-container>
          </div>
          <div class="kpi-sub">{{ dailyBrief ? t('dashboard.kpi.openOrdersPending', { count: dailyBrief.unfulfilledCostAlertCount }) : t('dashboard.kpi.thisMonthOrders') }}</div>
        </div>

        <div class="glass-card kpi-card">
          <div class="kpi-header">
            <span class="kpi-label">{{ t('dashboard.kpi.activeListingsLabel') }}</span>
            <span class="kpi-tag text-orange">{{ t('dashboard.kpi.portfolioTag') }}</span>
          </div>
          <div class="kpi-value text-orange">{{ activeListingsText }}</div>
          <div class="kpi-sub">{{ activeListingsNote }}</div>
        </div>

        <div class="glass-card kpi-card kpi-card-expenses"
             [class.hover-active]="isHoveringExpenses"
             [class.popover-open]="isExpensesPopoverOpen"
             (mouseenter)="onExpensesMouseEnter()"
             (mouseleave)="onExpensesMouseLeave()"
             (click)="toggleExpensesPopover()"
             [title]="t('dashboard.kpi.deductionsTooltip')">
          <div class="kpi-header">
            <span class="kpi-label">{{ t('dashboard.kpi.deductionsLabel') }}</span>
            <div class="kpi-header-right">
              <!-- Animated mini circular countdown / loading badge -->
              <span class="hover-timer-badge" *ngIf="isHoveringExpenses && !isExpensesPopoverOpen">
                <span class="pulse-dot"></span>
                <span>{{ t('dashboard.kpi.detailsLoading') }}</span>
              </span>
              <span class="kpi-tag text-danger" *ngIf="!isHoveringExpenses || isExpensesPopoverOpen">{{ t('dashboard.kpi.expensesTag') }}</span>
            </div>
          </div>
          <div class="kpi-value text-danger">
            <ng-container *ngIf="financialPerformance; else noFinanceValue">-&#36;{{ etsyDeductionsUsd | number:'1.2-2' }} <span class="kpi-sub-try">(₺{{ (etsyDeductionsUsd * apiService.exchangeRate()) | number:'1.0-0' }})</span></ng-container>
          </div>
          <div class="kpi-sub">
            {{ t('dashboard.kpi.adsAndFeesSub', { ads: (adsTotalUsd | number:'1.2-2'), fees: (etsyFeesUsd | number:'1.2-2') }) }}
            <span class="kpi-hint-text">{{ t('dashboard.kpi.hoverHint') }}</span>
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
                  <h4 class="popover-title">{{ t('dashboard.popover.title') }}</h4>
                  <span class="popover-subtitle">
                    {{ t('dashboard.popover.subtitle', { tryAmount: ((etsyDeductionsUsd * apiService.exchangeRate()) | number:'1.2-2'), usdAmount: (etsyDeductionsUsd | number:'1.2-2') }) }}
                  </span>
                </div>
              </div>
              <button type="button" class="btn-close-popover" (click)="closeExpensesPopover()" [title]="t('common.close')">✕</button>
            </div>

            <!-- 3 KPI Cards Cluster -->
            <div class="popover-kpi-row">
              <div class="mini-kpi-card red">
                <span class="mini-kpi-label">{{ t('dashboard.popover.internalAds') }}</span>
                <span class="mini-kpi-try">&#36;{{ internalAdsUsd | number:'1.2-2' }}</span>
                <span class="mini-kpi-usd">₺{{ (internalAdsUsd * apiService.exchangeRate()) | number:'1.2-2' }}</span>
              </div>
              <div class="mini-kpi-card orange">
                <span class="mini-kpi-label">{{ t('dashboard.popover.externalAds') }}</span>
                <span class="mini-kpi-try">&#36;{{ externalAdsUsd | number:'1.2-2' }}</span>
                <span class="mini-kpi-usd">₺{{ (externalAdsUsd * apiService.exchangeRate()) | number:'1.2-2' }}</span>
              </div>
              <div class="mini-kpi-card purple">
                <span class="mini-kpi-label">{{ t('dashboard.popover.platformFees') }}</span>
                <span class="mini-kpi-try">&#36;{{ etsyFeesUsd | number:'1.2-2' }}</span>
                <span class="mini-kpi-usd">{{ t('dashboard.popover.platformFeesSub') }}</span>
              </div>
            </div>

            <!-- Breakdown Table -->
            <div class="popover-table-wrap">
              <table class="popover-breakdown-table">
                <thead>
                  <tr>
                    <th>{{ t('dashboard.popover.colType') }}</th>
                    <th>{{ t('dashboard.popover.colName') }}</th>
                    <th>{{ t('dashboard.popover.colRate') }}</th>
                    <th>{{ t('dashboard.popover.colAmount') }}</th>
                    <th>{{ t('dashboard.popover.colDesc') }}</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngIf="!financialPerformance"><td colspan="5">{{ t('dashboard.popover.emptyTable') }}</td></tr>
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
              <span class="footer-note">{{ t('dashboard.popover.footerNote') }}</span>
              <a routerLink="/finance/accounting" class="btn-goto-accounting" (click)="closeExpensesPopover()">
                {{ t('dashboard.popover.gotoAccounting') }}
              </a>
            </div>

          </div>
        </div>
      </section>

      <!-- QUICK ACTION HUB -->
      <section class="action-hub-grid">
        <a routerLink="/finance/accounting" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-orange">{{ t('dashboard.actions.financeBadge') }}</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">{{ t('dashboard.actions.financeTitle') }}</h4>
          <p class="action-desc">{{ t('dashboard.actions.financeDesc') }}</p>
        </a>

        <a routerLink="/listings/fast-creator" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-cyan">{{ t('dashboard.actions.creatorBadge') }}</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">{{ t('dashboard.actions.creatorTitle') }}</h4>
          <p class="action-desc">{{ t('dashboard.actions.creatorDesc') }}</p>
        </a>

        <a routerLink="/ai-studio" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-purple">{{ t('dashboard.actions.studioBadge') }}</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">{{ t('dashboard.actions.studioTitle') }}</h4>
          <p class="action-desc">{{ t('dashboard.actions.studioDesc') }}</p>
        </a>

        <a routerLink="/analytics/ai-audit" class="glass-card action-card">
          <div class="action-top">
            <span class="action-badge badge-emerald">{{ t('dashboard.actions.auditBadge') }}</span>
            <span class="action-arrow">↗</span>
          </div>
          <h4 class="action-title">{{ t('dashboard.actions.auditTitle') }}</h4>
          <p class="action-desc">{{ t('dashboard.actions.auditDesc') }}</p>
        </a>
      </section>

      <!-- MIDDLE: RECENT ORDERS & GEMINI SPARK STORE COPILOT -->
      <section class="middle-grid">
        <!-- Live Order Stream Matching WinForms Image 2 -->
        <div class="glass-card orders-card">
          <div class="card-header-flex">
            <div>
              <h3 class="section-title">{{ t('dashboard.recentOrders.title') }}</h3>
              <span class="section-sub">{{ t('dashboard.recentOrders.openSummary', { count: dailyBrief?.unfulfilledCostAlertCount ?? '—' }) }}</span>
            </div>
            <a routerLink="/orders" class="link-view-all">{{ t('dashboard.recentOrders.viewAll') }}</a>
          </div>

          <div class="table-container">
            <table class="desktop-orders-table">
              <thead>
                <tr>
                  <th>{{ t('dashboard.recentOrders.colDate') }}</th>
                  <th>{{ t('dashboard.recentOrders.colOrderNo') }}</th>
                  <th>{{ t('dashboard.recentOrders.colProduct') }}</th>
                  <th>{{ t('dashboard.recentOrders.colQty') }}</th>
                  <th>{{ t('dashboard.recentOrders.colAmount') }}</th>
                  <th>{{ t('dashboard.recentOrders.colNetProfit') }}</th>
                </tr>
                <tr *ngIf="liveOrders.length === 0"><td colspan="6">{{ recentOrdersError ? t('dashboard.recentOrders.loadError') : (recentOrdersWarning || t('dashboard.recentOrders.empty')) }}</td></tr>
              </thead>
              <tbody>
                <tr *ngFor="let o of liveOrders">
                  <td class="text-muted">{{ o.date }}</td>
                  <td class="font-mono text-cyan">#{{ o.receiptId }}</td>
                  <td class="font-semibold">{{ o.title }}</td>
                  <td>{{ o.quantity }}</td>
                  <td class="text-emerald font-bold">&#36;{{ o.totalUsd | number:'1.2-2' }}</td>
                  <td class="font-bold" [ngClass]="o.netProfitUsd >= 0 ? 'text-cyan' : 'text-danger'">{{ o.netProfitUsd >= 0 ? '+' : '-' }}&#36;{{ (o.netProfitUsd < 0 ? -o.netProfitUsd : o.netProfitUsd) | number:'1.2-2' }} <span class="text-xs text-muted">({{ o.netProfitUsd < 0 ? '-₺' : '₺' }}{{ (o.netProfitUsd < 0 ? -o.netProfitUsd : o.netProfitUsd) * apiService.exchangeRate() | number:'1.0-0' }})</span> <span *ngIf="!o.hasCost" class="text-xs" [title]="t('dashboard.recentOrders.missingCostTooltip')">⚠</span></td>
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
              <h3 class="section-title">{{ t('dashboard.copilot.title') }}</h3>
              <span class="section-sub">{{ t('dashboard.copilot.subtitle') }}</span>
            </div>
          </div>

          <div class="copilot-insights">
            <div class="insight-item">
              <span class="insight-label text-emerald">{{ t('dashboard.copilot.topProductLabel') }}</span>
              <p class="insight-body">
                {{ topProductText }}
              </p>
            </div>

            <div class="insight-item">
              <span class="insight-label text-cyan">{{ t('dashboard.copilot.adsFeesLabel') }}</span>
              <p class="insight-body">
                <ng-container *ngIf="financialPerformance; else noFinanceInsight">{{ t('dashboard.copilot.adsFeesSummary', { margin: (financialPerformance.netProfitMargin / 100 | percent:'1.0-1'), fees: (financialPerformance.platformFees | number:'1.2-2') }) }}</ng-container>
              </p>
            </div>

            <div class="insight-item">
              <span class="insight-label text-purple">{{ t('dashboard.copilot.growthLabel') }}</span>
              <p class="insight-body">
                {{ t('dashboard.copilot.growthTip') }}
              </p>
              <a routerLink="/research/market" class="btn-micro-action">{{ t('dashboard.copilot.marketResearchBtn') }}</a>
            </div>
          </div>
        </div>
      </section>

      <!-- BOTTOM: CHART SECTION MATCHING WINFORMS IMAGE 2 -->
      <section class="glass-card chart-section">
        <div class="card-header-flex">
          <div>
            <h3 class="section-title">{{ t('dashboard.chart.title') }}</h3>
            <span class="section-sub">{{ t('dashboard.chart.subtitle') }}</span>
          </div>
          <div class="chart-badges">
            <span class="legend-badge legend-revenue">{{ t('dashboard.chart.legendRevenue') }}</span>
            <span class="legend-badge legend-profit">{{ t('dashboard.chart.legendProfit') }}</span>
          </div>
        </div>
        <div class="chart-wrapper">
          <div *ngIf="!hasTrendSeries" class="chart-empty">{{ trendError ? t('dashboard.chart.loadError') : t('dashboard.chart.loading') }}</div>
          <canvas #trendChart *ngIf="hasTrendSeries"></canvas>
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
    .badge-cost { background: rgba(34, 211, 238, 0.18); color: #67e8f9; }

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

    @media (max-width: 767.98px) {
      .kpi-grid {
        grid-template-columns: repeat(auto-fit, minmax(min(100%, 160px), 1fr));
        gap: 10px;
      }
      .expenses-popover-box {
        position: fixed;
        top: auto;
        bottom: calc(var(--bottom-nav-height, 62px) + var(--safe-bottom, 0px) + 8px);
        left: 12px;
        right: 12px;
        width: auto;
        max-width: none;
        max-height: 75dvh;
        overflow-y: auto;
      }
      .action-hub-grid {
        grid-template-columns: 1fr;
        gap: 10px;
      }
      .card-header-flex {
        flex-wrap: wrap;
        gap: 8px;
      }
      .chart-wrapper {
        height: 210px;
      }
    }

    @media (max-width: 639.98px) {
      .kpi-grid,
      .popover-kpi-row {
        grid-template-columns: 1fr;
      }
      .order-item {
        grid-template-columns: 1fr auto;
        gap: 6px 10px;
      }
      .order-item-title {
        max-width: 100%;
      }
      .popover-footer {
        flex-direction: column;
        align-items: stretch;
      }
      .btn-goto-accounting {
        text-align: center;
        min-height: 40px;
        display: inline-flex;
        align-items: center;
        justify-content: center;
      }
    }
  `]
})
export class DashboardComponent implements OnInit, OnDestroy {
  apiService = inject(EtsyApiService);
  private transloco = inject(TranslocoService);
  private destroyRef = inject(DestroyRef);

  @ViewChild('trendChart') chartCanvas?: ElementRef<HTMLCanvasElement>;
  chartInstance?: Chart;
  private chartInitTimer: any = null;
  hasTrendSeries = false; // Canlı seri yüklendiğinde true olur
  recentOrdersError = false;
  trendError = false;
  topProductTitle: string | null = null;
  topProductRevenueUsd = 0;
  recentOrdersWarning: string | null = null;
  seriesWarning: string | null = null;
  rateLimitStatus: EtsyRateLimitSnapshot | null = null;
  private trendLabels: string[] = [];
  private trendGross: number[] = [];
  private trendNet: number[] = [];

  financialPerformance: FinancialPerformanceDto | null = null;
  dailyBrief: DailyBrief | null = null;
  grossSalesUsd = 0;
  netProfitUsd = 0;
  etsyFeesUsd = 0;
  internalAdsUsd = 0;
  externalAdsUsd = 0;
  etsyDeductionsUsd = 0;
  adsTotalUsd = 0;
  private activeListingsCount: number | null = null;
  private activeListingsError = false;

  // EXPENSES HOVER TOOLTIP POPOVER
  isHoveringExpenses = false;
  isExpensesPopoverOpen = false;
  private hoverTimer: any = null;
  private leaveTimer: any = null;

  expensesBreakdown: Array<{type: string; typeClass: string; name: string; rate: string; amountText: string; desc: string; isTotal: boolean}> = [];
  liveOrders: Array<{date: string; receiptId: number; title: string; quantity: number; totalUsd: number; netProfitUsd: number; hasCost: boolean}> = [];

  ngOnInit(): void {
    this.loadLiveKpis();
    this.loadRecentOrders();
    this.loadDailySeries();
    this.loadRateLimitStatus();

    this.transloco.selectTranslation('dashboard').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.financialPerformance) {
        this.buildExpensesBreakdown(this.financialPerformance);
      }
      if (this.chartInstance) {
        this.chartInstance.data.datasets[0].label = this.transloco.translate('dashboard.chart.datasetRevenue');
        this.chartInstance.data.datasets[1].label = this.transloco.translate('dashboard.chart.datasetProfit');
        this.chartInstance.update('none');
      }
    });
  }

  get activeListingsText(): string {
    if (this.activeListingsCount === null) return '—';
    return this.activeListingsCount >= 100
      ? this.transloco.translate('dashboard.kpi.listingsCountPlus')
      : this.transloco.translate('dashboard.kpi.listingsCount', { count: this.activeListingsCount });
  }

  get activeListingsNote(): string {
    if (this.activeListingsError) return this.transloco.translate('dashboard.kpi.listingsAuthNote');
    if (this.activeListingsCount === null) return this.transloco.translate('dashboard.kpi.listingsLoading');
    return this.transloco.translate('dashboard.kpi.listingsLiveNote');
  }

  loadRateLimitStatus(): void {
    this.apiService.getEtsyRateLimitStatus().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (status) => { this.rateLimitStatus = status; },
      error: () => {}
    });
  }

  formatRetryAfter(seconds: number | null): string {
    if (!seconds || seconds <= 0) return this.transloco.translate('dashboard.rateLimit.resetPending');
    const hrs = Math.floor(seconds / 3600);
    const mins = Math.floor((seconds % 3600) / 60);
    return hrs > 0
      ? this.transloco.translate('dashboard.rateLimit.resetHoursMins', { hrs, mins })
      : this.transloco.translate('dashboard.rateLimit.resetMins', { mins });
  }

  loadRecentOrders(): void {
    this.apiService.getRecentOrders(15).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.liveOrders = res?.orders ?? [];
        this.recentOrdersError = false;
        this.recentOrdersWarning = (res?.ledgerOk === false && res?.ledgerWarning) ? res.ledgerWarning : null;
      },
      error: () => {
        this.liveOrders = [];
        this.recentOrdersError = true;
        this.recentOrdersWarning = null;
      }
    });
  }

  loadDailySeries(): void {
    this.apiService.getDailySeries().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.trendLabels = res?.labels ?? [];
        this.trendGross = res?.grossSales ?? [];
        this.trendNet = res?.netProfit ?? [];
        this.topProductTitle = res?.topProductTitle ?? null;
        this.topProductRevenueUsd = res?.topProductRevenueUsd ?? 0;
        this.hasTrendSeries = this.trendLabels.length > 0;
        this.trendError = false;
        this.seriesWarning = (res?.ledgerOk === false && res?.ledgerWarning) ? res.ledgerWarning : null;
        this.tryInitChart();
      },
      error: () => {
        this.hasTrendSeries = false;
        this.trendError = true;
        this.seriesWarning = null;
      }
    });
  }

  get topProductText(): string {
    if (!this.topProductTitle) {
      return this.seriesWarning
        ? this.transloco.translate('dashboard.copilot.topProductApiWarning', { warning: this.seriesWarning })
        : this.transloco.translate('dashboard.copilot.topProductEmpty');
    }
    return this.transloco.translate('dashboard.copilot.topProductSummary', {
      title: this.topProductTitle,
      revenue: this.topProductRevenueUsd.toFixed(2)
    });
  }

  loadLiveKpis(): void {
    this.apiService.getFinancialPerformance('this_month').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (perf) => {
        this.financialPerformance = perf;
        this.grossSalesUsd = perf.grossSales;
        this.netProfitUsd = perf.netProfit;
        this.etsyFeesUsd = perf.platformFees;
        this.internalAdsUsd = perf.internalAdsCost ?? 0;
        this.externalAdsUsd = perf.externalAdsCost ?? 0;
        this.adsTotalUsd = this.internalAdsUsd + this.externalAdsUsd;
        this.etsyDeductionsUsd = this.etsyFeesUsd + this.adsTotalUsd;
        this.buildExpensesBreakdown(perf);
        this.tryInitChart();
        this.loadRateLimitStatus();
      },
      error: () => {
        this.loadRateLimitStatus();
        this.financialPerformance = null;
        this.grossSalesUsd = 0;
        this.netProfitUsd = 0;
        this.etsyFeesUsd = 0;
        this.internalAdsUsd = 0;
        this.externalAdsUsd = 0;
        this.adsTotalUsd = 0;
        this.etsyDeductionsUsd = 0;
        this.expensesBreakdown = [];
      }
    });

    this.apiService.getDailyBrief().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (brief) => {
        this.dailyBrief = brief;
      },
      error: () => {
        this.dailyBrief = null;
      }
    });

    this.apiService.getShopActiveListings(undefined, 100).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (items) => {
        this.activeListingsCount = items?.length ?? 0;
        this.activeListingsError = false;
      },
      error: () => {
        this.activeListingsCount = null;
        this.activeListingsError = true;
      }
    });
  }

  private buildExpensesBreakdown(perf: FinancialPerformanceDto): void {
    const rate = this.apiService.exchangeRate() || 49.12;
    const gross = perf.grossSales || 0;
    const tr = (key: string, params?: Record<string, unknown>) => this.transloco.translate(key, params);
    const pct = (value: number) =>
      gross > 0 ? tr('dashboard.popover.ratioOfGross', { pct: ((value / gross) * 100).toFixed(1) }) : '—';
    const money = (value: number) => `-$${value.toFixed(2)} (-₺${(value * rate).toFixed(2)})`;

    const rows = [
      { type: tr('dashboard.popover.rows.feeType'), typeClass: 'badge-fee', name: tr('dashboard.popover.rows.feeName'), rate: pct(perf.platformFees), amountText: money(perf.platformFees), desc: tr('dashboard.popover.rows.feeDesc'), isTotal: false },
      { type: tr('dashboard.popover.rows.adType'), typeClass: 'badge-ad', name: tr('dashboard.popover.rows.internalAdName'), rate: pct(perf.internalAdsCost), amountText: money(perf.internalAdsCost), desc: tr('dashboard.popover.rows.internalAdDesc'), isTotal: false },
      { type: tr('dashboard.popover.rows.adType'), typeClass: 'badge-ad', name: tr('dashboard.popover.rows.externalAdName'), rate: pct(perf.externalAdsCost), amountText: money(perf.externalAdsCost), desc: tr('dashboard.popover.rows.externalAdDesc'), isTotal: false },
      { type: tr('dashboard.popover.rows.costType'), typeClass: 'badge-cost', name: tr('dashboard.popover.rows.productCostName'), rate: pct(perf.productCosts), amountText: money(perf.productCosts), desc: tr('dashboard.popover.rows.productCostDesc'), isTotal: false },
      { type: tr('dashboard.popover.rows.costType'), typeClass: 'badge-cost', name: tr('dashboard.popover.rows.shippingCostName'), rate: pct(perf.shippingCosts), amountText: money(perf.shippingCosts), desc: tr('dashboard.popover.rows.shippingCostDesc'), isTotal: false },
      { type: tr('dashboard.popover.rows.refundType'), typeClass: 'badge-refund', name: tr('dashboard.popover.rows.refundName'), rate: pct(perf.refunds), amountText: money(perf.refunds), desc: tr('dashboard.popover.rows.refundDesc'), isTotal: false }
    ];
    const total = perf.platformFees + perf.internalAdsCost + perf.externalAdsCost + perf.productCosts + perf.shippingCosts + perf.refunds;
    rows.push({
      type: tr('dashboard.popover.rows.totalType'),
      typeClass: 'badge-total',
      name: tr('dashboard.popover.rows.totalName'),
      rate: pct(total),
      amountText: money(total),
      desc: tr('dashboard.popover.rows.totalDesc'),
      isTotal: true
    });
    this.expensesBreakdown = rows;
  }

  private tryInitChart(): void {
    if (!this.hasTrendSeries || this.chartInstance) return;
    // Canvas *ngIf ile görünür hale geldiği için DOM güncellendikten sonra başlatılır.
    this.chartInitTimer = setTimeout(() => {
      if (this.chartInstance) return;
      const canvas = this.chartCanvas?.nativeElement;
      const ctx = canvas?.getContext('2d');
      if (!ctx) return;

      const days: string[] = this.trendLabels;
      const revenueData: number[] = this.trendGross;
      const profitData: number[] = this.trendNet;

      this.chartInstance = new Chart(ctx, {
        type: 'line',
        data: {
          labels: days,
          datasets: [
            {
              label: this.transloco.translate('dashboard.chart.datasetRevenue'),
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
              label: this.transloco.translate('dashboard.chart.datasetProfit'),
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
              min: 0
            }
          }
        }
      });
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
    if (this.chartInitTimer) clearTimeout(this.chartInitTimer);
    if (this.chartInstance) this.chartInstance.destroy();
  }
}
