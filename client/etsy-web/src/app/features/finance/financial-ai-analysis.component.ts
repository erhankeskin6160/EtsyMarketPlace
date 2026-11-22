import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface FinancialInsight {
  type: 'warning' | 'opportunity' | 'success';
  title: string;
  description: string;
  impactUsd: number;
  actionText: string;
}

interface PayoutForecastRow {
  periodLabel: string;
  estimatedOrders: number;
  grossUsd: number;
  feesAndAdsUsd: number;
  costsUsd: number;
  netPayoutTry: number;
  netProfitUsd: number;
  marginPct: number;
}

@Component({
  selector: 'app-financial-ai-analysis',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('finance')],
  template: `
    <ng-container *transloco="let t">
    <div class="fin-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">🧠</div>
          <div>
            <h1 class="page-title">{{ t('finance.aiAnalysis.title') }}</h1>
            <p class="page-subtitle">{{ t('finance.aiAnalysis.subtitle') }}</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-run-audit" (click)="runFinancialAudit()">
            {{ t('finance.aiAnalysis.runAuditBtn') }}
          </button>
        </div>
      </div>

      <!-- METRIC STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">{{ t('finance.aiAnalysis.kpiRevenue30d') }}</span>
          <span class="kpi-val text-green">\${{ projectedRevenue | number:'1.2-2' }}</span>
          <span class="kpi-sub">{{ growthSubtitle }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('finance.aiAnalysis.kpiNetProfit') }}</span>
          <span class="kpi-val text-blue">\${{ projectedNetProfit | number:'1.2-2' }}</span>
          <span class="kpi-sub">{{ t('finance.aiAnalysis.avgMarginSub', { pct: netMarginPercent }) }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('finance.aiAnalysis.kpiFeeLoad') }}</span>
          <span class="kpi-val text-orange">-%{{ feePercentage }}</span>
          <span class="kpi-sub">{{ t('finance.aiAnalysis.kpiFeeLoadSub') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('finance.aiAnalysis.kpiRoas') }}</span>
          <span class="kpi-val text-purple">{{ roasScore }}x</span>
          <span class="kpi-sub">{{ roasScore > 0 ? t('finance.aiAnalysis.roasSubActive', { score: roasScore }) : t('finance.aiAnalysis.roasSubEmpty') }}</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- DISCONNECTED / EXPIRED API WARNING BANNER -->
      <div *ngIf="etsyApi.tokenStatus() !== 'connected'" class="alert-banner">
        <div class="alert-icon">⚠️</div>
        <div class="alert-body">
          <span class="alert-title">{{ t('finance.aiAnalysis.apiDisconnectedTitle') }}</span>
          <p class="alert-desc">
            {{ t('finance.aiAnalysis.apiDisconnectedDesc') }}
          </p>
        </div>
      </div>

      <!-- AI RECOMMENDATIONS GRID -->
      <div class="insights-section">
        <h2 class="section-title">{{ t('finance.aiAnalysis.insightsSectionTitle') }}</h2>
        <div *ngIf="insights.length > 0" class="insights-grid">
          <div *ngFor="let item of insights" class="insight-card glass-card" [ngClass]="item.type">
            <div class="card-head">
              <span class="type-badge" [ngClass]="item.type">
                {{ item.type === 'warning' ? t('finance.aiAnalysis.badgeWarning') : (item.type === 'opportunity' ? t('finance.aiAnalysis.badgeOpportunity') : t('finance.aiAnalysis.badgeSuccess')) }}
              </span>
              <span class="impact-val" [class.text-green]="item.impactUsd > 0" [class.text-red]="item.impactUsd < 0">
                {{ t('finance.aiAnalysis.impactPerMonth', { sign: item.impactUsd > 0 ? '+' : '', amount: item.impactUsd }) }}
              </span>
            </div>
            <h3 class="insight-title">{{ item.title }}</h3>
            <p class="insight-desc">{{ item.description }}</p>
            <div class="card-foot">
              <button class="btn-action" (click)="applyRecommendation(item)">
                {{ item.actionText }} →
              </button>
            </div>
          </div>
        </div>

        <div *ngIf="insights.length === 0" class="empty-state-box glass-card">
          <span class="empty-icon">🧠</span>
          <span class="empty-title">{{ t('finance.aiAnalysis.emptyTitle') }}</span>
          <p class="empty-desc">
            {{ t('finance.aiAnalysis.emptyDesc') }}
          </p>
        </div>
      </div>

      <!-- CASH FLOW PROJECTION TABLE -->
      <div class="projection-card glass-card">
        <h2 class="section-title">{{ t('finance.aiAnalysis.projectionTitle') }}</h2>
        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>{{ t('finance.aiAnalysis.colPeriod') }}</th>
                <th>{{ t('finance.aiAnalysis.colEstOrders') }}</th>
                <th>{{ t('finance.aiAnalysis.colGrossRevenue') }}</th>
                <th>{{ t('finance.aiAnalysis.colFeesAndAds') }}</th>
                <th>{{ t('finance.aiAnalysis.colProductionShipping') }}</th>
                <th>{{ t('finance.aiAnalysis.colEstNetProfit') }}</th>
                <th>{{ t('finance.aiAnalysis.colEstBankPayout') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let row of payoutForecastRows">
                <td><b>{{ row.periodLabel }}</b></td>
                <td>{{ t('finance.aiAnalysis.ordersUnit', { count: row.estimatedOrders }) }}</td>
                <td>\${{ row.grossUsd | number:'1.2-2' }}</td>
                <td class="text-orange">-\${{ row.feesAndAdsUsd | number:'1.2-2' }}</td>
                <td class="text-orange">-\${{ row.costsUsd | number:'1.2-2' }}</td>
                <td [class.text-green]="row.netProfitUsd >= 0" [class.text-red]="row.netProfitUsd < 0"><b>\${{ row.netProfitUsd | number:'1.2-2' }}</b></td>
                <td><span class="conf-pill high">₺{{ row.netPayoutTry | number:'1.2-2' }} (%{{ row.marginPct }})</span></td>
              </tr>
              <tr *ngIf="payoutForecastRows.length === 0">
                <td colspan="7" style="text-align: center; color: #94a3b8; padding: 20px;">
                  {{ t('finance.aiAnalysis.projectionEmpty') }}
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    .fin-container {
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
      background: rgba(168, 85, 247, 0.15);
      border: 1px solid rgba(168, 85, 247, 0.3);
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
    .btn-run-audit {
      background: linear-gradient(135deg, #8b5cf6, #6d28d9);
      color: #fff;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(139, 92, 246, 0.35);
      transition: all 0.2s;
    }
    .btn-run-audit:hover { filter: brightness(1.1); transform: translateY(-1px); }

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
    .text-orange { color: #f97316; }
    .text-purple { color: #c084fc; }
    .text-red { color: #f87171; }

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .insights-section { display: flex; flex-direction: column; gap: 16px; }
    .section-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }
    .insights-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(min(100%, 280px), 1fr));
      gap: 16px;
    }
    .insight-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .insight-card.warning { border-color: rgba(245, 158, 11, 0.3); background: rgba(245, 158, 11, 0.04); }
    .insight-card.opportunity { border-color: rgba(56, 189, 248, 0.3); background: rgba(56, 189, 248, 0.04); }
    .insight-card.success { border-color: rgba(16, 185, 129, 0.3); background: rgba(16, 185, 129, 0.04); }
    .card-head { display: flex; justify-content: space-between; align-items: center; }
    .type-badge { font-size: 0.72rem; font-weight: 700; padding: 2px 8px; border-radius: 4px; }
    .type-badge.warning { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
    .type-badge.opportunity { background: rgba(56, 189, 248, 0.2); color: #38bdf8; }
    .type-badge.success { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .impact-val { font-size: 0.85rem; font-weight: 800; }
    .insight-title { font-size: 1rem; font-weight: 700; color: #fff; margin: 0; }
    .insight-desc { font-size: 0.8rem; color: #94a3b8; line-height: 1.4; margin: 0; flex: 1; }
    .btn-action {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #fff;
      padding: 6px 14px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-action:hover { background: rgba(255, 255, 255, 0.15); }

    .projection-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
    }
    .table-responsive { overflow-x: auto; margin-top: 14px; }
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
    .conf-pill {
      font-size: 0.72rem;
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 700;
    }
    .conf-pill.high { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .conf-pill.mid { background: rgba(56, 189, 248, 0.15); color: #38bdf8; }

    @media (max-width: 767.98px) {
      .analysis-grid,
      .kpi-row,
      .cards-grid {
        grid-template-columns: 1fr !important;
      }
      .table-wrapper,
      .data-table-wrap {
        overflow-x: auto;
        -webkit-overflow-scrolling: touch;
        max-width: 100%;
      }
      .data-table th,
      .data-table td {
        padding: 8px 10px;
        font-size: 0.78rem;
      }
    }
  `]
})
export class FinancialAiAnalysisComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private router = inject(Router);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);

  // Initialized to 0 - Zero fake data policy!
  projectedRevenue = 0;
  projectedNetProfit = 0;
  netMarginPercent = 0;
  feePercentage = 0;
  roasScore = 0;
  growthSubtitle = '';
  private lastPerf: any = null;

  toastMessage = '';
  insights: FinancialInsight[] = [];
  payoutForecastRows: PayoutForecastRow[] = [];

  ngOnInit(): void {
    this.growthSubtitle = this.transloco.translate('finance.aiAnalysis.waitingLedger');
    this.loadFinancials();
    this.transloco.selectTranslation('finance').pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      if (this.lastPerf) {
        this.applyPerformanceMetrics(this.lastPerf);
      } else {
        this.growthSubtitle = this.transloco.translate('finance.aiAnalysis.waitingLedger');
      }
    });
  }

  private applyPerformanceMetrics(perf: any): void {
    const gross = Number(perf.grossSalesUsd ?? perf.grossSales ?? 0);
    const net = Number(perf.netProfitUsd ?? perf.netProfit ?? 0);
    const margin = Number(perf.profitMarginPercent ?? perf.netMarginPercent ?? perf.netProfitMargin ?? 0);
    const fees = Number(perf.platformFees ?? 0);
    const ads = Number(perf.internalAdsCost ?? 0) + Number(perf.externalAdsCost ?? 0);

    if (gross > 0) {
      this.projectedRevenue = Number((gross * 1.12).toFixed(2));
      this.projectedNetProfit = Number((net * 1.12).toFixed(2));
      this.netMarginPercent = Number(margin.toFixed(1));
      this.feePercentage = Number(((fees / gross) * 100).toFixed(1));
      this.roasScore = ads > 0 ? Number((gross / ads).toFixed(1)) : 0;
      this.growthSubtitle = this.transloco.translate('finance.aiAnalysis.baseGrossProjection', { gross: gross.toFixed(2) });

      const realInsights: FinancialInsight[] = [];
      if (this.feePercentage > 15) {
        realInsights.push({
          type: 'warning',
          title: this.transloco.translate('finance.aiAnalysis.feeWarningTitle'),
          description: this.transloco.translate('finance.aiAnalysis.feeWarningDesc', { pct: this.feePercentage }),
          impactUsd: Math.round(gross * 0.05),
          actionText: this.transloco.translate('finance.aiAnalysis.feeWarningAction')
        });
      }
      if (this.netMarginPercent > 35) {
        realInsights.push({
          type: 'success',
          title: this.transloco.translate('finance.aiAnalysis.healthyMarginTitle'),
          description: this.transloco.translate('finance.aiAnalysis.healthyMarginDesc', { pct: this.netMarginPercent }),
          impactUsd: Math.max(0, Math.round(net * 0.1)),
          actionText: this.transloco.translate('finance.aiAnalysis.healthyMarginAction')
        });
      } else if (net < 0) {
        realInsights.push({
          type: 'warning',
          title: this.transloco.translate('finance.aiAnalysis.netLossTitle'),
          description: this.transloco.translate('finance.aiAnalysis.netLossDesc', { amount: Math.abs(net).toFixed(2) }),
          impactUsd: Math.round(net),
          actionText: this.transloco.translate('finance.aiAnalysis.netLossAction')
        });
      }
      if (this.roasScore > 0) {
        realInsights.push({
          type: 'opportunity',
          title: this.transloco.translate('finance.aiAnalysis.adEfficiencyTitle', { score: this.roasScore }),
          description: this.transloco.translate('finance.aiAnalysis.adEfficiencyDesc', { score: this.roasScore }),
          impactUsd: Math.round(gross * 0.12),
          actionText: this.transloco.translate('finance.aiAnalysis.adEfficiencyAction')
        });
      }
      this.insights = realInsights;
    } else {
      this.projectedRevenue = 0;
      this.projectedNetProfit = Number(net.toFixed(2));
      this.netMarginPercent = 0;
      this.feePercentage = 0;
      this.roasScore = 0;
      this.growthSubtitle = this.transloco.translate('finance.aiAnalysis.noCompletedSales');
    }
  }

  loadFinancials(): void {
    this.etsyApi.getFinancialPerformance('this_month').pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (perf: any) => {
        if (perf) {
          this.lastPerf = perf;
          const gross = Number(perf.grossSalesUsd ?? perf.grossSales ?? 0);
          const net = Number(perf.netProfitUsd ?? perf.netProfit ?? 0);
          const fees = Number(perf.platformFees ?? 0);
          const rate = Number(perf.exchangeRateUsed ?? this.etsyApi.exchangeRate() ?? 49.5);

          this.applyPerformanceMetrics(perf);

          const end = new Date();
          const start = new Date();
          start.setDate(end.getDate() - 30);
          this.etsyApi.generateAiFinancialForecast({
            shopId: this.etsyApi.activeShopId(),
            startDate: start.toISOString(),
            endDate: end.toISOString(),
            provider: 'Offline',
            exchangeRate: rate,
            currentGrossSalesUsd: gross,
            currentEtsyFeesUsd: fees,
            currentInnerAdsUsd: Number(perf.internalAdsCost ?? 0),
            currentOffsiteAdsUsd: Number(perf.externalAdsCost ?? 0),
            currentRefundsUsd: Number(perf.refunds ?? 0),
            currentProductCostsUsd: Number((perf.productCosts ?? 0) + (perf.shippingCosts ?? 0)),
            currentRealNetProfitUsd: net,
            currentOrderCount: Number(perf.receiptCount ?? 0)
          }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
            next: (forecast) => {
              if (forecast?.horizonProjections?.length) {
                this.payoutForecastRows = forecast.horizonProjections.map(h => ({
                  periodLabel: h.horizonTitle,
                  estimatedOrders: h.estimatedOrders,
                  grossUsd: h.expectedGrossUsd,
                  feesAndAdsUsd: h.expectedFeesAndAdsUsd,
                  costsUsd: h.expectedCostsUsd,
                  netPayoutTry: h.estimatedBankPayoutTry,
                  netProfitUsd: h.expectedNetProfitUsd,
                  marginPct: h.expectedMarginPct
                }));
              }
            }
          });
        }
      }
    });
  }

  runFinancialAudit(): void {
    this.toastMessage = this.transloco.translate('finance.aiAnalysis.auditRunning');
    this.etsyApi.getFinancialAnalysis().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        if (res && res.recommendations && res.recommendations.length > 0) {
          this.insights = res.recommendations.map((rec: any, idx: number) => ({
            type: idx === 0 ? 'warning' : (idx === 1 ? 'opportunity' : 'success'),
            title: rec.title || rec.category || this.transloco.translate('finance.aiAnalysis.auditFallbackTitle'),
            description: rec.description || rec.message || this.transloco.translate('finance.aiAnalysis.auditFallbackDesc'),
            impactUsd: Number(rec.potentialGainUsd ?? 0),
            actionText: rec.actionTitle || this.transloco.translate('finance.aiAnalysis.auditFallbackAction')
          }));
          this.toastMessage = this.transloco.translate('finance.aiAnalysis.auditCompletedUpdated');
          setTimeout(() => this.toastMessage = '', 4000);
        } else {
          this.toastMessage = this.transloco.translate('finance.aiAnalysis.auditCompleted');
          setTimeout(() => this.toastMessage = '', 4000);
        }
      },
      error: () => {
        this.toastMessage = this.transloco.translate('finance.aiAnalysis.auditError');
        setTimeout(() => this.toastMessage = '', 4000);
      }
    });
  }

  applyRecommendation(_item: FinancialInsight): void {
    this.router.navigate(['/finance/accounting']);
  }
}
