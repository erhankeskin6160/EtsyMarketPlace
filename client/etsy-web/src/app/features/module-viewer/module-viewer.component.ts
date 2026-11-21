import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface ModuleDetail {
  id: string;
  categoryKey: string;
  icon: string;
  badgeKey?: string;
  featureKeys: string[];
  hasKpi?: boolean;
}

@Component({
  selector: 'app-module-viewer',
  standalone: true,
  imports: [CommonModule, RouterModule, TranslocoDirective],
  providers: [provideTranslocoScope('tools')],
  template: `
    <ng-container *transloco="let t">
    <div class="module-root" *ngIf="module">
      <div class="module-banner glass-card">
        <div class="banner-left">
          <div class="module-badge-row">
            <span class="category-pill">{{ t('tools.moduleViewer.categories.' + module.categoryKey) }}</span>
            <span class="badge badge-new" *ngIf="module.badgeKey">{{ t('tools.moduleViewer.badges.' + module.badgeKey) }}</span>
          </div>
          <div class="module-title-row">
            <span class="module-icon">{{ module.icon }}</span>
            <div>
              <h2 class="module-title">{{ t('tools.moduleViewer.modules.' + module.id + '.title') }}</h2>
              <p class="module-summary">{{ t('tools.moduleViewer.modules.' + module.id + '.summary') }}</p>
            </div>
          </div>
        </div>

        <div class="banner-right" *ngIf="module.hasKpi">
          <div class="module-kpi-card">
            <span class="kpi-label">{{ t('tools.moduleViewer.modules.' + module.id + '.kpiTitle') }}</span>
            <span class="kpi-num text-orange">{{ t('tools.moduleViewer.modules.' + module.id + '.kpiValue') }}</span>
            <span class="kpi-sub">{{ t('tools.moduleViewer.liveSynced') }}</span>
          </div>
        </div>
      </div>

      <!-- FEATURES & ACTION CARDS -->
      <section class="features-grid">
        <div class="glass-card feature-card" *ngFor="let featKey of module.featureKeys; let i = index">
          <div class="feat-num">0{{ i + 1 }}</div>
          <h4 class="feat-title">{{ t('tools.moduleViewer.modules.' + module.id + '.features.' + featKey) }}</h4>
          <p class="feat-desc">{{ t('tools.moduleViewer.featureDesc') }}</p>
          <button class="btn btn-secondary btn-sm" (click)="triggerAction(featKey)">
            {{ t('tools.moduleViewer.runNow') }}
          </button>
        </div>
      </section>

      <!-- LIVE VDS API CONNECTION STATUS -->
      <section class="glass-card api-status-card">
        <div class="status-header">
          <div class="status-left">
            <span class="status-dot-pulse"></span>
            <div>
              <h4 class="status-title">{{ t('tools.moduleViewer.apiStatusTitle') }}</h4>
              <p class="status-sub">{{ t('tools.moduleViewer.shopIdLabel') }} <b>{{ apiService.activeShopId() }}</b> | {{ t('tools.moduleViewer.endpointLabel') }} <code>http://5.180.81.148:5263/api/*</code></p>
            </div>
          </div>
          <a routerLink="/dashboard" class="btn btn-orange">{{ t('tools.moduleViewer.backToDashboard') }}</a>
        </div>
      </section>
    </div>
    </ng-container>
  `,
  styles: [`
    .module-root {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .module-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 24px;
      padding: 24px;
      background: linear-gradient(135deg, rgba(21, 30, 46, 0.95), rgba(15, 23, 42, 0.9));
    }
    @media (max-width: 768px) {
      .module-banner { flex-direction: column; align-items: flex-start; }
    }
    .module-badge-row {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
    }
    .category-pill {
      font-size: 0.72rem;
      font-weight: 700;
      color: #38bdf8;
      background: rgba(56, 189, 248, 0.12);
      border: 1px solid rgba(56, 189, 248, 0.25);
      padding: 2px 8px;
      border-radius: 6px;
      text-transform: uppercase;
    }
    .module-title-row {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .module-icon {
      font-size: 2.2rem;
    }
    .module-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #fff;
    }
    .module-summary {
      font-size: 0.88rem;
      color: var(--text-muted);
      margin-top: 2px;
    }

    .module-kpi-card {
      background: #090d16;
      border: 1px solid var(--border-color);
      border-radius: 10px;
      padding: 14px 20px;
      display: flex;
      flex-direction: column;
      text-align: right;
      min-width: 160px;
    }
    .kpi-label { font-size: 0.7rem; color: var(--text-muted); }
    .kpi-num { font-size: 1.4rem; font-weight: 800; margin: 2px 0; }
    .kpi-sub { font-size: 0.68rem; color: #10b981; }

    .features-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 16px;
    }
    .feature-card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 20px;
    }
    .feat-num {
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.85rem;
      font-weight: 700;
      color: var(--etsy-orange);
    }
    .feat-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
    }
    .feat-desc {
      font-size: 0.8rem;
      color: var(--text-muted);
      line-height: 1.4;
      flex: 1;
    }

    .api-status-card {
      padding: 16px 20px;
    }
    .status-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    .status-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .status-dot-pulse {
      width: 10px;
      height: 10px;
      background: #10b981;
      border-radius: 50%;
      box-shadow: 0 0 10px #10b981;
    }
    .status-title {
      font-size: 0.9rem;
      font-weight: 700;
      color: #fff;
    }
    .status-sub {
      font-size: 0.75rem;
      color: var(--text-muted);
    }
  `]
})
export class ModuleViewerComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  route = inject(ActivatedRoute);
  apiService = inject(EtsyApiService);

  module?: ModuleDetail;

  private readonly defaultFeatureKeys = ['f1', 'f2', 'f3', 'f4'];

  private readonly catalog: Record<string, ModuleDetail> = {
    'orders': {
      id: 'orders',
      categoryKey: 'general',
      icon: '🚚',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'fast-creator': {
      id: 'fast-creator',
      categoryKey: 'general',
      icon: '⚡',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'creator': {
      id: 'creator',
      categoryKey: 'general',
      icon: '🛍️',
      featureKeys: this.defaultFeatureKeys
    },
    'ai-studio': {
      id: 'ai-studio',
      categoryKey: 'general',
      icon: '🎨',
      badgeKey: 'pro',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'analytics-shop': {
      id: 'analytics-shop',
      categoryKey: 'general',
      icon: '🏬',
      featureKeys: this.defaultFeatureKeys
    },
    'market-search': {
      id: 'market-search',
      categoryKey: 'research',
      icon: '🔍',
      featureKeys: this.defaultFeatureKeys
    },
    'viral-3d': {
      id: 'viral-3d',
      categoryKey: 'research',
      icon: '🔥',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'competitor': {
      id: 'competitor',
      categoryKey: 'research',
      icon: '🕵️',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys
    },
    'external': {
      id: 'external',
      categoryKey: 'research',
      icon: '🌐',
      featureKeys: this.defaultFeatureKeys
    },
    'ai-audit': {
      id: 'ai-audit',
      categoryKey: 'research',
      icon: '🤖',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys
    },
    'ab-test': {
      id: 'ab-test',
      categoryKey: 'research',
      icon: '📈',
      featureKeys: this.defaultFeatureKeys
    },
    'accounting': {
      id: 'accounting',
      categoryKey: 'automationFinance',
      icon: '💳',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'financial-ai': {
      id: 'financial-ai',
      categoryKey: 'automationFinance',
      icon: '🧠',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys
    },
    'profit': {
      id: 'profit',
      categoryKey: 'automationFinance',
      icon: '💰',
      featureKeys: this.defaultFeatureKeys
    },
    'vault': {
      id: 'vault',
      categoryKey: 'automationFinance',
      icon: '🛡️',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys,
      hasKpi: true
    },
    'ai-usage': {
      id: 'ai-usage',
      categoryKey: 'automationFinance',
      icon: '📊',
      badgeKey: 'new',
      featureKeys: this.defaultFeatureKeys
    },
    'shipping': {
      id: 'shipping',
      categoryKey: 'shipping',
      icon: '🚚',
      featureKeys: this.defaultFeatureKeys
    },
    'system': {
      id: 'system',
      categoryKey: 'system',
      icon: '⚙️',
      featureKeys: this.defaultFeatureKeys
    }
  };

  ngOnInit(): void {
    this.route.url.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(() => {
      const path = this.route.snapshot.routeConfig?.path || '';
      const mappedKey = this.resolveKey(path);
      this.module = this.catalog[mappedKey] || this.catalog['orders'];
    });
  }

  private resolveKey(path: string): string {
    if (path.includes('fast-creator')) return 'fast-creator';
    if (path.includes('creator')) return 'creator';
    if (path.includes('ai-studio')) return 'ai-studio';
    if (path.includes('analytics/shop')) return 'analytics-shop';
    if (path.includes('research/market')) return 'market-search';
    if (path.includes('research/viral-3d')) return 'viral-3d';
    if (path.includes('research/competitor')) return 'competitor';
    if (path.includes('research/external')) return 'external';
    if (path.includes('analytics/ai-audit')) return 'ai-audit';
    if (path.includes('analytics/ab-test')) return 'ab-test';
    if (path.includes('finance/accounting')) return 'accounting';
    if (path.includes('finance/ai-analysis')) return 'financial-ai';
    if (path.includes('finance/profit')) return 'profit';
    if (path.includes('tools/vault')) return 'vault';
    if (path.includes('tools/ai-usage')) return 'ai-usage';
    if (path.includes('shipping')) return 'shipping';
    if (path.includes('settings') || path.includes('system')) return 'system';
    return 'orders';
  }

  triggerAction(featKey: string): void {
    const featLabel = this.module
      ? this.transloco.translate('tools.moduleViewer.modules.' + this.module.id + '.features.' + featKey)
      : featKey;
    alert(this.transloco.translate('tools.moduleViewer.actionAlert', { feature: featLabel }));
  }
}
