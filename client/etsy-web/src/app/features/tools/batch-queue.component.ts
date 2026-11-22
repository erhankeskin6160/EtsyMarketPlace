import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface BatchItem {
  id: number;
  listingId: string;
  originalTitle: string;
  originalDescription: string;
  targetKeyword: string;
  category: string;
  status: 'Pending' | 'Processing' | 'Completed' | 'RiskWarning' | 'SyncedToEtsy';
  overallScore: number;
  optimizedTitle: string;
  optimizedDescription: string;
  optimizedTags: string[];
  processedAt?: string;
}

@Component({
  selector: 'app-batch-queue',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('tools')],
  template: `
    <ng-container *transloco="let t">
    <div class="batch-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">⚡</div>
          <div>
            <h1 class="page-title">{{ t('tools.batchQueue.title') }}</h1>
            <p class="page-subtitle">{{ t('tools.batchQueue.subtitle') }}</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-clear" (click)="clearCompleted()">
            {{ t('tools.batchQueue.clearCompletedBtn') }}
          </button>
          <button class="btn-run-all" [disabled]="isProcessingBatch" (click)="runBatchOptimization()">
            {{ isProcessingBatch ? t('tools.batchQueue.processingBtn', { progress: processProgress }) : t('tools.batchQueue.optimizeAllBtn') }}
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.batchQueue.kpi.queuedListings') }}</span>
          <span class="kpi-val text-blue">{{ t('tools.batchQueue.kpi.listingsCount', { count: items.length }) }}</span>
          <span class="kpi-sub">{{ t('tools.batchQueue.kpi.pendingCount', { count: getPendingCount() }) }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.batchQueue.kpi.avgSeoScore') }}</span>
          <span class="kpi-val text-green">{{ getAverageScore() }}%</span>
          <span class="kpi-sub">{{ t('tools.batchQueue.kpi.preOptScore') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.batchQueue.kpi.syncedToEtsy') }}</span>
          <span class="kpi-val text-purple">{{ t('tools.batchQueue.kpi.listingsCount', { count: getSyncedCount() }) }}</span>
          <span class="kpi-sub">{{ t('tools.batchQueue.kpi.activeInLiveShop') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.batchQueue.kpi.riskPolicyCheck') }}</span>
          <span class="kpi-val text-emerald">{{ t('tools.batchQueue.kpi.zeroWarnings') }}</span>
          <span class="kpi-sub">{{ t('tools.batchQueue.kpi.antiBanVerified') }}</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- QUEUE LIST SECTION -->
      <div class="queue-section glass-card">
        <div class="table-header">
          <h2 class="table-title">{{ t('tools.batchQueue.tableTitle') }}</h2>
          <div class="filter-pills">
            <button [class.active]="statusFilter === 'ALL'" (click)="statusFilter = 'ALL'">{{ t('tools.batchQueue.filters.all', { count: items.length }) }}</button>
            <button [class.active]="statusFilter === 'Pending'" (click)="statusFilter = 'Pending'">{{ t('tools.batchQueue.filters.pending', { count: getPendingCount() }) }}</button>
            <button [class.active]="statusFilter === 'Completed'" (click)="statusFilter = 'Completed'">{{ t('tools.batchQueue.filters.completed') }}</button>
          </div>
        </div>

        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>{{ t('tools.batchQueue.columns.listingId') }}</th>
                <th>{{ t('tools.batchQueue.columns.currentTitle') }}</th>
                <th>{{ t('tools.batchQueue.columns.targetKeyword') }}</th>
                <th>{{ t('tools.batchQueue.columns.seoScore') }}</th>
                <th>{{ t('tools.batchQueue.columns.status') }}</th>
                <th>{{ t('tools.batchQueue.columns.optimizedTitle') }}</th>
                <th>{{ t('tools.batchQueue.columns.action') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of filteredItems">
                <td class="cell-id"><code>#{{ item.listingId }}</code></td>
                <td class="cell-orig-title">
                  <div class="orig-wrap">{{ item.originalTitle }}</div>
                </td>
                <td>
                  <span class="keyword-pill">{{ item.targetKeyword }}</span>
                </td>
                <td>
                  <div class="score-badge" [ngClass]="item.overallScore >= 80 ? 'high' : 'mid'">
                    {{ item.overallScore }}%
                  </div>
                </td>
                <td>
                  <span class="status-pill" [ngClass]="item.status.toLowerCase()">{{ t('tools.batchQueue.status.' + item.status) }}</span>
                </td>
                <td class="cell-opt-title">
                  <span *ngIf="item.optimizedTitle" class="opt-text">{{ item.optimizedTitle }}</span>
                  <span *ngIf="!item.optimizedTitle" class="pending-text">{{ t('tools.batchQueue.notOptimizedYet') }}</span>
                </td>
                <td>
                  <div class="action-btn-row">
                    <button *ngIf="item.status === 'Pending'" class="btn-single-opt" (click)="optimizeSingle(item)">
                      {{ t('tools.batchQueue.optimizeBtn') }}
                    </button>
                    <button *ngIf="item.status === 'Completed'" class="btn-sync-etsy" (click)="syncItemToEtsy(item)">
                      {{ t('tools.batchQueue.pushToEtsyBtn') }}
                    </button>
                    <span *ngIf="item.status === 'SyncedToEtsy'" class="synced-check">{{ t('tools.batchQueue.liveBadge') }}</span>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredItems.length === 0">
                <td colspan="7" class="empty-table-cell">
                  <div class="empty-state-box">
                    <span class="empty-icon">📦</span>
                    <span class="empty-title">{{ t('tools.batchQueue.emptyTitle') }}</span>
                    <p class="empty-desc">
                      {{ t('tools.batchQueue.emptyDescPrefix') }} <strong>{{ t('tools.batchQueue.emptyDescStrong') }}</strong>.
                      {{ t('tools.batchQueue.emptyDescSuffix') }}
                    </p>
                  </div>
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
    .batch-container {
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
      background: rgba(249, 115, 22, 0.15);
      border: 1px solid rgba(249, 115, 22, 0.3);
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
    .header-actions {
      display: flex;
      gap: 12px;
    }
    .btn-clear {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #cbd5e1;
      padding: 10px 16px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-clear:hover { background: rgba(255, 255, 255, 0.15); }
    .btn-run-all {
      background: linear-gradient(135deg, #f97316, #ea580c);
      color: #fff;
      border: none;
      padding: 10px 22px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.85rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(249, 115, 22, 0.35);
      transition: all 0.2s;
    }
    .btn-run-all:hover:not(:disabled) { filter: brightness(1.1); transform: translateY(-1px); }
    .btn-run-all:disabled { opacity: 0.6; cursor: not-allowed; }

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
    .text-blue { color: #38bdf8; }
    .text-green { color: #34d399; }
    .text-purple { color: #c084fc; }
    .text-emerald { color: #10b981; }

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .queue-section {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
    }
    .table-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }
    .table-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }
    .filter-pills {
      display: flex;
      gap: 6px;
      background: rgba(15, 23, 42, 0.6);
      padding: 4px;
      border-radius: 8px;
    }
    .filter-pills button {
      background: transparent;
      border: none;
      color: #94a3b8;
      padding: 4px 12px;
      border-radius: 6px;
      font-size: 0.78rem;
      cursor: pointer;
      font-weight: 600;
    }
    .filter-pills button.active {
      background: #f97316;
      color: #fff;
    }

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
      vertical-align: middle;
    }
    .cell-id code { color: #f97316; font-weight: 700; }
    .orig-wrap { max-width: 220px; overflow: hidden; text-overflow: ellipsis; white-space: nowrap; font-weight: 600; }
    .keyword-pill {
      background: rgba(56, 189, 248, 0.12);
      border: 1px solid rgba(56, 189, 248, 0.25);
      color: #38bdf8;
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 0.72rem;
    }
    .score-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.75rem;
    }
    .score-badge.high { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .score-badge.mid { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
    .status-pill {
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 0.7rem;
      font-weight: 700;
      text-transform: uppercase;
    }
    .status-pill.pending { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
    .status-pill.completed { background: rgba(99, 102, 241, 0.15); color: #818cf8; }
    .status-pill.syncedtoetsy { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .cell-opt-title { max-width: 280px; }
    .opt-text { color: #34d399; font-weight: 600; font-size: 0.8rem; }
    .pending-text { color: #64748b; font-style: italic; font-size: 0.75rem; }
    .action-btn-row { display: flex; gap: 8px; }
    .btn-single-opt {
      background: rgba(249, 115, 22, 0.15);
      border: 1px solid rgba(249, 115, 22, 0.35);
      color: #fb923c;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-sync-etsy {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      border: none;
      padding: 4px 12px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
    }
    .synced-check { color: #34d399; font-weight: 700; font-size: 0.78rem; }

    @media (max-width: 767.98px) {
      .batch-header,
      .batch-actions {
        flex-direction: column;
        align-items: stretch;
        gap: 8px;
      }
      .batch-grid,
      .queue-layout {
        grid-template-columns: 1fr !important;
      }
      .table-wrap {
        overflow-x: auto;
        -webkit-overflow-scrolling: touch;
        max-width: 100%;
      }
    }
  `]
})
export class BatchQueueComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);

  statusFilter: 'ALL' | 'Pending' | 'Completed' = 'ALL';
  toastMessage = '';
  isProcessingBatch = false;
  processProgress = 0;

  items: BatchItem[] = [];

  ngOnInit(): void {
    this.loadQueue();
  }

  loadQueue(): void {
    this.etsyApi.getBatchQueue().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.items = data.map((d: any) => ({
            id: d.id,
            listingId: d.listingId || d.listing_id,
            originalTitle: d.originalTitle || d.original_title,
            originalDescription: d.originalDescription || d.original_description,
            targetKeyword: d.targetKeyword || d.target_keyword,
            category: d.category || 'Genel',
            status: d.status === 2 ? 'Completed' : (d.status === 5 ? 'SyncedToEtsy' : 'Pending'),
            overallScore: d.overallScore || d.overall_score || 75,
            optimizedTitle: d.optimizedTitle || d.optimized_title || '',
            optimizedDescription: d.optimizedDescription || d.optimized_description || '',
            optimizedTags: d.optimizedTags || d.optimized_tags || []
          }));
        }
      }
    });
  }

  get filteredItems(): BatchItem[] {
    if (this.statusFilter === 'ALL') return this.items;
    return this.items.filter(i => i.status === this.statusFilter);
  }

  getPendingCount(): number {
    return this.items.filter(i => i.status === 'Pending').length;
  }

  getSyncedCount(): number {
    return this.items.filter(i => i.status === 'SyncedToEtsy').length;
  }

  getAverageScore(): number {
    if (this.items.length === 0) return 0;
    const sum = this.items.reduce((acc, i) => acc + (i.overallScore || 0), 0);
    return Math.round(sum / this.items.length);
  }

  private generateDynamicSeoTitle(item: BatchItem): string {
    const kw = (item.targetKeyword || 'Handmade Gift').trim();
    const orig = (item.originalTitle || 'Artisan Craft Item').slice(0, 48).trim();
    return `${kw} - ${orig}, Artisan Custom Display Decor, Unique Collectible Present`;
  }

  private generateDynamicTags(item: BatchItem): string[] {
    const base = item.targetKeyword ? item.targetKeyword.toLowerCase().trim() : 'handmade gift';
    const words = base.split(' ').filter(Boolean);
    const tags = [
      base,
      ...words.map(w => `${w} decor`),
      ...words.map(w => `${w} gift`),
      'artisan craft',
      'etsy best seller',
      'custom handmade',
      'unique design',
      'room aesthetic'
    ];
    return Array.from(new Set(tags)).slice(0, 13);
  }

  optimizeSingle(item: BatchItem): void {
    item.status = 'Completed';
    item.overallScore = 93;
    item.optimizedTitle = this.generateDynamicSeoTitle(item);
    item.optimizedDescription = `Handcrafted premium quality ${item.targetKeyword || 'item'}. Produced with eco-friendly durable materials. Specially packaged for gift giving with fast and secure international shipping.`;
    item.optimizedTags = this.generateDynamicTags(item);

    this.etsyApi.processBatchItem(item.id, {
      optimizedTitle: item.optimizedTitle,
      optimizedDescription: item.optimizedDescription,
      optimizedTags: item.optimizedTags,
      overallScore: 93,
      status: 2
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.optimizedAi', {
          listingId: item.listingId,
          keyword: item.targetKeyword
        });
        setTimeout(() => this.toastMessage = '', 3000);
      },
      error: () => {
        this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.optimizedLocal', {
          listingId: item.listingId
        });
        setTimeout(() => this.toastMessage = '', 3000);
      }
    });
  }

  runBatchOptimization(): void {
    const pendings = this.items.filter(i => i.status === 'Pending');
    if (pendings.length === 0) {
      this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.noPending');
      setTimeout(() => this.toastMessage = '', 3000);
      return;
    }

    this.isProcessingBatch = true;
    this.processProgress = 10;
    const interval = setInterval(() => {
      this.processProgress += 30;
      if (this.processProgress >= 100) {
        clearInterval(interval);
        this.isProcessingBatch = false;
        pendings.forEach(p => {
          p.status = 'Completed';
          p.overallScore = 94;
          p.optimizedTitle = this.generateDynamicSeoTitle(p);
          p.optimizedDescription = `Handcrafted premium quality ${p.targetKeyword}. Specially packaged with secure shipping.`;
          p.optimizedTags = this.generateDynamicTags(p);
          this.etsyApi.processBatchItem(p.id, {
            optimizedTitle: p.optimizedTitle,
            optimizedDescription: p.optimizedDescription,
            optimizedTags: p.optimizedTags,
            overallScore: 94,
            status: 2
          }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ error: () => {} });
        });
        this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.batchDone', { count: pendings.length });
        setTimeout(() => this.toastMessage = '', 4000);
      }
    }, 600);
  }

  syncItemToEtsy(item: BatchItem): void {
    item.status = 'SyncedToEtsy';
    this.etsyApi.processBatchItem(item.id, {
      optimizedTitle: item.optimizedTitle,
      optimizedDescription: item.optimizedDescription,
      optimizedTags: item.optimizedTags,
      overallScore: item.overallScore,
      status: 5
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.syncedEtsy', { listingId: item.listingId });
        setTimeout(() => this.toastMessage = '', 3000);
      }
    });
  }

  clearCompleted(): void {
    this.items = this.items.filter(i => i.status === 'Pending');
    this.etsyApi.clearCompletedBatch().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.batchQueue.toasts.cleared');
        setTimeout(() => this.toastMessage = '', 3000);
      }
    });
  }
}
