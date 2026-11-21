import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface TrackedEntity {
  id: number;
  entityType: string;
  externalKey: string;
  displayName: string;
  url: string;
  createdAt: string;
  latestPrice?: number;
  currency?: string;
  favorites?: number;
  views?: number;
  reviewCount?: number;
  seoScore?: number;
  priceTrend?: 'up' | 'down' | 'stable';
}

@Component({
  selector: 'app-live-tracking',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('tools')],
  template: `
    <ng-container *transloco="let t">
    <div class="tracking-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">📡</div>
          <div>
            <h1 class="page-title">{{ t('tools.liveTracking.title') }}</h1>
            <p class="page-subtitle">{{ t('tools.liveTracking.subtitle') }}</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-add-track" (click)="openAddModal()">
            {{ t('tools.liveTracking.addBtn') }}
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.liveTracking.kpi.trackedListings') }}</span>
          <span class="kpi-val text-indigo">{{ t('tools.liveTracking.kpi.listingsCount', { count: items.length }) }}</span>
          <span class="kpi-sub">{{ t('tools.liveTracking.kpi.priceVolumeRadar') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.liveTracking.kpi.priceChanges24h') }}</span>
          <span class="kpi-val text-orange">{{ t('tools.liveTracking.kpi.priceChangesVal') }}</span>
          <span class="kpi-sub">{{ t('tools.liveTracking.kpi.priceChangesSub') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.liveTracking.kpi.favoriteSurge') }}</span>
          <span class="kpi-val text-green">{{ t('tools.liveTracking.kpi.favoriteSurgeVal') }}</span>
          <span class="kpi-sub">{{ t('tools.liveTracking.kpi.viralTrendAlert') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.liveTracking.kpi.radarStatus') }}</span>
          <span class="kpi-val text-emerald">{{ t('tools.liveTracking.kpi.active') }}</span>
          <span class="kpi-sub">{{ t('tools.liveTracking.kpi.snapshotInterval') }}</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- TRACKED ITEMS TABLE -->
      <div class="tracking-table-card glass-card">
        <div class="table-header">
          <h2 class="table-title">{{ t('tools.liveTracking.tableTitle', { count: items.length }) }}</h2>
          <div class="filter-row">
            <input type="text" [(ngModel)]="searchQuery" class="search-input" [placeholder]="t('tools.liveTracking.searchPlaceholder')" />
          </div>
        </div>

        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>{{ t('tools.liveTracking.columns.type') }}</th>
                <th>{{ t('tools.liveTracking.columns.name') }}</th>
                <th>{{ t('tools.liveTracking.columns.key') }}</th>
                <th>{{ t('tools.liveTracking.columns.price') }}</th>
                <th>{{ t('tools.liveTracking.columns.favorites') }}</th>
                <th>{{ t('tools.liveTracking.columns.seoScore') }}</th>
                <th>{{ t('tools.liveTracking.columns.trend') }}</th>
                <th>{{ t('tools.liveTracking.columns.actions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of filteredItems">
                <td>
                  <span class="type-pill" [ngClass]="item.entityType.toLowerCase()">{{ t('tools.liveTracking.entityTypes.' + item.entityType) }}</span>
                </td>
                <td class="cell-name">
                  <span class="name-text">{{ item.displayName }}</span>
                </td>
                <td class="cell-key">
                  <code>{{ item.externalKey }}</code>
                </td>
                <td class="cell-price">
                  <b>\${{ item.latestPrice | number:'1.2-2' }}</b> {{ item.currency }}
                </td>
                <td>{{ item.favorites | number }}</td>
                <td>
                  <span class="score-tag">{{ item.seoScore }} / 100</span>
                </td>
                <td>
                  <span *ngIf="item.priceTrend === 'down'" class="trend-tag down">{{ t('tools.liveTracking.trends.down') }}</span>
                  <span *ngIf="item.priceTrend === 'up'" class="trend-tag up">{{ t('tools.liveTracking.trends.up') }}</span>
                  <span *ngIf="item.priceTrend === 'stable'" class="trend-tag stable">{{ t('tools.liveTracking.trends.stable') }}</span>
                </td>
                <td>
                  <div class="action-btn-row">
                    <button class="btn-snapshot" (click)="takeSnapshot(item)">{{ t('tools.liveTracking.scanNowBtn') }}</button>
                    <button class="btn-delete" (click)="deleteTracked(item.id)">{{ t('tools.liveTracking.deleteBtn') }}</button>
                  </div>
                </td>
              </tr>
              <tr *ngIf="filteredItems.length === 0">
                <td colspan="8" class="empty-table-cell">
                  <div class="empty-state-box">
                    <span class="empty-icon">🎯</span>
                    <span class="empty-title">{{ t('tools.liveTracking.emptyTitle') }}</span>
                    <p class="empty-desc">
                      {{ t('tools.liveTracking.emptyDescPrefix') }} <strong>{{ t('tools.liveTracking.emptyDescStrong') }}</strong>.
                      {{ t('tools.liveTracking.emptyDescSuffix') }}
                    </p>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- ADD MODAL -->
      <div *ngIf="showAddModal" class="modal-backdrop">
        <div class="modal-content glass-card">
          <div class="modal-header">
            <h3>{{ t('tools.liveTracking.modal.title') }}</h3>
            <button class="modal-close" (click)="showAddModal = false">✕</button>
          </div>
          <div class="modal-body">
            <div class="form-group">
              <label>{{ t('tools.liveTracking.modal.nameLabel') }}</label>
              <input type="text" [(ngModel)]="newItem.displayName" class="form-input" [placeholder]="t('tools.liveTracking.modal.namePlaceholder')" />
            </div>
            <div class="form-row">
              <div class="form-group">
                <label>{{ t('tools.liveTracking.modal.keyLabel') }}</label>
                <input type="text" [(ngModel)]="newItem.externalKey" class="form-input" placeholder="189204859" />
              </div>
              <div class="form-group">
                <label>{{ t('tools.liveTracking.modal.priceLabel') }}</label>
                <input type="number" [(ngModel)]="newItem.latestPrice" class="form-input" placeholder="24.99" />
              </div>
            </div>
            <div class="form-group">
              <label>{{ t('tools.liveTracking.modal.urlLabel') }}</label>
              <input type="text" [(ngModel)]="newItem.url" class="form-input" placeholder="https://www.etsy.com/listing/..." />
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-cancel" (click)="showAddModal = false">{{ t('tools.liveTracking.modal.cancelBtn') }}</button>
            <button class="btn-save" (click)="saveNewItem()">{{ t('tools.liveTracking.modal.saveBtn') }}</button>
          </div>
        </div>
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    .tracking-container {
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
      background: rgba(129, 140, 248, 0.15);
      border: 1px solid rgba(129, 140, 248, 0.3);
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
    .btn-add-track {
      background: linear-gradient(135deg, #6366f1, #4f46e5);
      color: #fff;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(99, 102, 241, 0.35);
      transition: all 0.2s;
    }
    .btn-add-track:hover { filter: brightness(1.1); transform: translateY(-1px); }

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
    .text-indigo { color: #818cf8; }
    .text-orange { color: #f97316; }
    .text-green { color: #34d399; }
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

    .tracking-table-card {
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
    .search-input {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #cbd5e1;
      padding: 6px 14px;
      border-radius: 6px;
      font-size: 0.82rem;
      width: 220px;
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
    .type-pill {
      font-size: 0.7rem;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      text-transform: uppercase;
      background: rgba(99, 102, 241, 0.15);
      color: #a5b4fc;
    }
    .name-text { font-weight: 600; color: #f1f5f9; }
    .cell-key code { color: #f97316; font-size: 0.75rem; }
    .cell-price b { color: #34d399; font-size: 0.9rem; }
    .score-tag {
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      font-size: 0.75rem;
    }
    .trend-tag {
      font-size: 0.75rem;
      font-weight: 600;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .trend-tag.down { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .trend-tag.up { background: rgba(239, 68, 68, 0.15); color: #f87171; }
    .trend-tag.stable { color: #94a3b8; }
    .action-btn-row { display: flex; gap: 8px; }
    .btn-snapshot {
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.35);
      color: #818cf8;
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .btn-delete {
      background: transparent;
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #94a3b8;
      padding: 4px 8px;
      border-radius: 6px;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .btn-delete:hover { border-color: rgba(239, 68, 68, 0.4); color: #fca5a5; }

    /* MODAL */
    .modal-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(6px);
      display: flex;
      align-items: center;
      justify-content: center;
      z-index: 1000;
    }
    .modal-content {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 16px;
      width: 520px;
      max-width: 90vw;
      padding: 24px;
    }
    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 20px;
    }
    .modal-header h3 { font-size: 1.15rem; font-weight: 800; color: #fff; margin: 0; }
    .modal-close { background: none; border: none; color: #94a3b8; font-size: 1.2rem; cursor: pointer; }
    .modal-body { display: flex; flex-direction: column; gap: 14px; }
    .form-row { display: grid; grid-template-columns: 1fr 1fr; gap: 12px; }
    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-group label { font-size: 0.78rem; font-weight: 600; color: #cbd5e1; }
    .form-input {
      background: rgba(17, 24, 39, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 9px 12px;
      color: #fff;
      font-size: 0.85rem;
    }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      margin-top: 24px;
    }
    .btn-cancel {
      background: transparent;
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #94a3b8;
      padding: 9px 16px;
      border-radius: 8px;
      cursor: pointer;
    }
    .btn-save {
      background: #4f46e5;
      border: none;
      color: #fff;
      padding: 9px 20px;
      border-radius: 8px;
      font-weight: 700;
      cursor: pointer;
    }
  `]
})
export class LiveTrackingRadarComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);

  searchQuery = '';
  toastMessage = '';
  showAddModal = false;

  newItem = {
    displayName: '',
    externalKey: '',
    latestPrice: 19.99,
    url: ''
  };

  items: TrackedEntity[] = [];

  ngOnInit(): void {
    this.loadTracked();
  }

  loadTracked(): void {
    this.etsyApi.getTrackingItems().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.items = data.map((d: any) => ({
            id: d.id,
            entityType: d.entityType === 1 ? 'Shop' : 'Listing',
            externalKey: d.externalKey || d.external_key,
            displayName: d.displayName || d.display_name,
            url: d.url || '',
            createdAt: d.createdAt || d.created_at || new Date().toISOString(),
            latestPrice: d.latestPrice || 24.99,
            currency: 'USD',
            favorites: d.favorites || 120,
            views: d.views || 400,
            reviewCount: d.reviewCount || 10,
            seoScore: d.seoScore || 85,
            priceTrend: 'stable'
          }));
        }
      }
    });
  }

  get filteredItems(): TrackedEntity[] {
    if (!this.searchQuery.trim()) return this.items;
    const q = this.searchQuery.toLowerCase();
    return this.items.filter(i =>
      i.displayName.toLowerCase().includes(q) ||
      i.externalKey.toLowerCase().includes(q)
    );
  }

  openAddModal(): void {
    this.newItem = {
      displayName: '',
      externalKey: '',
      latestPrice: 24.99,
      url: ''
    };
    this.showAddModal = true;
  }

  saveNewItem(): void {
    if (!this.newItem.displayName) return;

    const payload = {
      entityType: 0,
      externalKey: this.newItem.externalKey || 'EXT-' + Date.now().toString().slice(-6),
      displayName: this.newItem.displayName,
      url: this.newItem.url || '',
      snapshot: {
        price: Number(this.newItem.latestPrice) || 24.99,
        currencyCode: 'USD',
        favorites: 10,
        views: 80,
        reviewCount: 2,
        seoScore: 80
      }
    };

    this.etsyApi.saveTrackingCapture(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.liveTracking.toasts.savedDb', { name: this.newItem.displayName });
        this.showAddModal = false;
        this.loadTracked();
        setTimeout(() => this.toastMessage = '', 3500);
      },
      error: () => {
        this.items.unshift({
          id: Date.now(),
          entityType: 'Listing',
          externalKey: payload.externalKey,
          displayName: payload.displayName,
          url: payload.url,
          createdAt: new Date().toISOString(),
          latestPrice: payload.snapshot.price,
          currency: 'USD',
          favorites: 10,
          views: 80,
          reviewCount: 2,
          seoScore: 80,
          priceTrend: 'stable'
        });
        this.toastMessage = this.transloco.translate('tools.liveTracking.toasts.addedLocal', { name: this.newItem.displayName });
        this.showAddModal = false;
        setTimeout(() => this.toastMessage = '', 3500);
      }
    });
  }

  takeSnapshot(item: TrackedEntity): void {
    const payload = {
      entityType: item.entityType === 'Shop' ? 1 : 0,
      externalKey: item.externalKey,
      displayName: item.displayName,
      url: item.url,
      snapshot: {
        price: item.latestPrice,
        currencyCode: item.currency || 'USD',
        favorites: item.favorites,
        views: item.views,
        reviewCount: item.reviewCount,
        seoScore: item.seoScore
      }
    };

    this.etsyApi.saveTrackingCapture(payload).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.liveTracking.toasts.snapshotDb', {
          name: item.displayName,
          price: item.latestPrice
        });
        setTimeout(() => this.toastMessage = '', 4000);
      },
      error: () => {
        this.toastMessage = this.transloco.translate('tools.liveTracking.toasts.snapshotLocal', {
          name: item.displayName,
          price: item.latestPrice
        });
        setTimeout(() => this.toastMessage = '', 4000);
      }
    });
  }

  deleteTracked(id: number): void {
    this.items = this.items.filter(i => i.id !== id);
    this.etsyApi.deleteTrackingItem(id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.toastMessage = this.transloco.translate('tools.liveTracking.toasts.deleted');
        setTimeout(() => this.toastMessage = '', 3000);
      }
    });
  }
}
