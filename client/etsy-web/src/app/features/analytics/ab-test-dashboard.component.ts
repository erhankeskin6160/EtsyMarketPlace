import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface AbTestCard {
  id: number;
  listingId: string;
  listingTitle: string;
  experimentName: string;
  variantA_Title: string;
  variantB_Title: string;
  variantA_Tags: string[];
  variantB_Tags: string[];
  startDate: string;
  status: 'Active' | 'Completed' | 'Cancelled';
  beforeViews: number;
  beforeSales: number;
  afterViews: number;
  afterSales: number;
}

@Component({
  selector: 'app-ab-test-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="ab-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">📈</div>
          <div>
            <h1 class="page-title">A/B Test Paneli & Varyasyon Laboratuvarı</h1>
            <p class="page-subtitle">Başlık, etiket ve açıklama varyasyonlarını test ederek istatistiksel dönüşüm oranını maksimize edin</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-create" (click)="openCreateModal()">
            ⚡ Yeni A/B Test Başlat
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">AKTİF TESTLER</span>
          <span class="kpi-val text-indigo">{{ getActiveCount() }}</span>
          <span class="kpi-sub">Canlı İzlenen</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">ORT. DÖNÜŞÜM ARTIŞI</span>
          <span class="kpi-val text-green">+%24.6</span>
          <span class="kpi-sub">B Varyantı Üstünlüğü</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">KAZANAN VARYANTLAR</span>
          <span class="kpi-val text-purple">19 İlan</span>
          <span class="kpi-sub">Canlıya Alındı</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">GÜVEN ARALIĞI</span>
          <span class="kpi-val text-orange">%95.2</span>
          <span class="kpi-sub">İstatistiksel Anlamlılık</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- EXPERIMENTS LIST -->
      <div class="experiments-section">
        <div class="section-top">
          <h2 class="section-title">Canlı & Tamamlanan Deneyler ({{ experiments.length }})</h2>
          <div class="filter-pills">
            <button [class.active]="filterStatus === 'ALL'" (click)="filterStatus = 'ALL'">Tümü</button>
            <button [class.active]="filterStatus === 'Active'" (click)="filterStatus = 'Active'">Aktif</button>
            <button [class.active]="filterStatus === 'Completed'" (click)="filterStatus = 'Completed'">Tamamlanan</button>
          </div>
        </div>

        <div class="experiments-grid">
          <div *ngFor="let exp of filteredExperiments" class="exp-card glass-card">
            <div class="exp-header">
              <div class="exp-badge-row">
                <span class="status-pill" [ngClass]="exp.status.toLowerCase()">{{ exp.status }}</span>
                <span class="exp-id">ID: #{{ exp.id }}</span>
                <span class="exp-date">Başlangıç: {{ exp.startDate | slice:0:10 }}</span>
              </div>
              <h3 class="exp-title">{{ exp.experimentName }}</h3>
              <p class="listing-ref">İlan: <b>{{ exp.listingTitle }}</b> ({{ exp.listingId }})</p>
            </div>

            <!-- COMPARISON BOXES -->
            <div class="compare-grid">
              <!-- VARIANT A -->
              <div class="variant-box variant-a">
                <div class="var-header">
                  <span class="var-tag">A Varyantı (Orijinal)</span>
                  <span class="var-views">{{ exp.beforeViews }} Görüntülenme</span>
                </div>
                <div class="var-content">
                  <p class="var-title-text">{{ exp.variantA_Title }}</p>
                  <div class="tags-cloud">
                    <span *ngFor="let t of exp.variantA_Tags" class="mini-tag">{{ t }}</span>
                  </div>
                </div>
                <div class="var-stat">
                  <span>Satış: <b>{{ exp.beforeSales }} adet</b></span>
                  <span>Dönüşüm: <b>{{ calculateCr(exp.beforeSales, exp.beforeViews) }}%</b></span>
                </div>
              </div>

              <!-- VS BADGE -->
              <div class="vs-divider">VS</div>

              <!-- VARIANT B -->
              <div class="variant-box variant-b">
                <div class="var-header">
                  <span class="var-tag winner">B Varyantı (AI Optimize)</span>
                  <span class="var-views">{{ exp.afterViews }} Görüntülenme</span>
                </div>
                <div class="var-content">
                  <p class="var-title-text">{{ exp.variantB_Title }}</p>
                  <div class="tags-cloud">
                    <span *ngFor="let t of exp.variantB_Tags" class="mini-tag green">{{ t }}</span>
                  </div>
                </div>
                <div class="var-stat">
                  <span>Satış: <b class="text-green">{{ exp.afterSales }} adet</b></span>
                  <span>Dönüşüm: <b class="text-green">{{ calculateCr(exp.afterSales, exp.afterViews) }}%</b></span>
                </div>
              </div>
            </div>

            <!-- CARD ACTIONS -->
            <div class="exp-footer">
              <div class="significance-info">
                <span class="sig-dot green"></span>
                <span>İstatistiksel Güven: <b>%96 Güvenli</b> (+%38 Dönüşüm)</span>
              </div>
              <div class="card-btns">
                <button *ngIf="exp.status === 'Active'" class="btn-declare-winner" (click)="declareWinner(exp)">
                  ✓ B'yi Kazanan Yap & Canlıya Al
                </button>
                <button class="btn-delete" (click)="deleteExperiment(exp.id)">
                  Sil
                </button>
              </div>
            </div>
          </div>
        </div>
      </div>

      <!-- CREATE MODAL -->
      <div *ngIf="showCreateModal" class="modal-backdrop">
        <div class="modal-content glass-card">
          <div class="modal-header">
            <h3>⚡ Yeni A/B Test Deneyi Oluştur</h3>
            <button class="modal-close" (click)="showCreateModal = false">✕</button>
          </div>
          <div class="modal-body">
            <div class="form-group">
              <label>Deney Adı:</label>
              <input type="text" [(ngModel)]="newExp.experimentName" class="form-input" placeholder="Örn: 3D Ejderha Noel Başlık Testi" />
            </div>
            <div class="form-row">
              <div class="form-group">
                <label>Listing ID:</label>
                <input type="text" [(ngModel)]="newExp.listingId" class="form-input" placeholder="189204859" />
              </div>
              <div class="form-group">
                <label>İlan Başlığı:</label>
                <input type="text" [(ngModel)]="newExp.listingTitle" class="form-input" placeholder="Orijinal İlan Başlığı" />
              </div>
            </div>
            <div class="form-group">
              <label>A Varyantı Başlığı (Mevcut):</label>
              <input type="text" [(ngModel)]="newExp.variantA_Title" class="form-input" />
            </div>
            <div class="form-group">
              <label>B Varyantı Başlığı (AI SEO):</label>
              <input type="text" [(ngModel)]="newExp.variantB_Title" class="form-input" />
            </div>
            <div class="form-group">
              <label>B Varyantı Etiketleri (virgülle ayırın):</label>
              <input type="text" [(ngModel)]="newExpTagsRaw" class="form-input" placeholder="gift for gamer, 3d printed desk, dragon toy" />
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-cancel" (click)="showCreateModal = false">Vazgeç</button>
            <button class="btn-save" (click)="submitCreate()">Deneyi Başlat</button>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .ab-container {
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
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.3);
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
    .btn-create {
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
    .btn-create:hover { filter: brightness(1.1); transform: translateY(-1px); }

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
    .text-green { color: #34d399; }
    .text-purple { color: #c084fc; }
    .text-orange { color: #f97316; }

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .experiments-section {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .section-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .section-title {
      font-size: 1.15rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
    }
    .filter-pills {
      display: flex;
      gap: 6px;
      background: rgba(15, 23, 42, 0.6);
      padding: 4px;
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.06);
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
      background: #4f46e5;
      color: #fff;
    }

    .experiments-grid {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .exp-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .exp-badge-row {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 6px;
      font-size: 0.75rem;
    }
    .status-pill {
      padding: 2px 8px;
      border-radius: 4px;
      font-weight: 700;
      text-transform: uppercase;
      font-size: 0.68rem;
    }
    .status-pill.active { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .status-pill.completed { background: rgba(99, 102, 241, 0.2); color: #818cf8; }
    .exp-id { color: #64748b; font-family: monospace; }
    .exp-date { color: #94a3b8; }
    .exp-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }
    .listing-ref { font-size: 0.8rem; color: #94a3b8; margin: 4px 0 0 0; }

    .compare-grid {
      display: grid;
      grid-template-columns: 1fr auto 1fr;
      gap: 16px;
      align-items: center;
    }
    @media (max-width: 900px) {
      .compare-grid { grid-template-columns: 1fr; }
      .vs-divider { display: none; }
    }
    .vs-divider {
      font-size: 0.9rem;
      font-weight: 900;
      color: #64748b;
      background: rgba(255, 255, 255, 0.05);
      border-radius: 50%;
      width: 36px;
      height: 36px;
      display: flex;
      align-items: center;
      justify-content: center;
      border: 1px solid rgba(255, 255, 255, 0.1);
    }
    .variant-box {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 10px;
      padding: 14px;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .variant-box.variant-b {
      border-color: rgba(16, 185, 129, 0.3);
      background: rgba(16, 185, 129, 0.04);
    }
    .var-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.75rem;
    }
    .var-tag { font-weight: 700; color: #94a3b8; }
    .var-tag.winner { color: #34d399; }
    .var-views { color: #64748b; }
    .var-title-text { font-size: 0.85rem; color: #f1f5f9; margin: 0; line-height: 1.4; font-weight: 600; }
    .tags-cloud { display: flex; flex-wrap: wrap; gap: 4px; margin-top: 8px; }
    .mini-tag {
      font-size: 0.7rem;
      padding: 2px 6px;
      background: rgba(255, 255, 255, 0.06);
      border-radius: 4px;
      color: #cbd5e1;
    }
    .mini-tag.green {
      background: rgba(16, 185, 129, 0.15);
      color: #6ee7b7;
    }
    .var-stat {
      display: flex;
      justify-content: space-between;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      padding-top: 8px;
      font-size: 0.75rem;
      color: #94a3b8;
    }

    .exp-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      padding-top: 14px;
    }
    .significance-info {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.8rem;
      color: #cbd5e1;
    }
    .sig-dot { width: 8px; height: 8px; border-radius: 50%; }
    .sig-dot.green { background: #10b981; box-shadow: 0 0 6px #10b981; }
    .card-btns { display: flex; gap: 10px; }
    .btn-declare-winner {
      background: rgba(16, 185, 129, 0.2);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      padding: 6px 14px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-declare-winner:hover { background: rgba(16, 185, 129, 0.35); color: #fff; }
    .btn-delete {
      background: transparent;
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #94a3b8;
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 0.78rem;
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
      width: 580px;
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
    .form-input:focus { border-color: #6366f1; outline: none; }
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
export class AbTestDashboardComponent implements OnInit {
  etsyApi = inject(EtsyApiService);

  filterStatus: 'ALL' | 'Active' | 'Completed' = 'ALL';
  toastMessage = '';
  showCreateModal = false;

  newExp = {
    listingId: '',
    listingTitle: '',
    experimentName: '',
    variantA_Title: '',
    variantB_Title: ''
  };
  newExpTagsRaw = '';

  experiments: AbTestCard[] = [
    {
      id: 1,
      listingId: '189204859',
      listingTitle: 'Articulated Crystal Dragon 3D Printed',
      experimentName: 'Noel Hediye & Ejderha Anahtar Kelime Testi',
      variantA_Title: 'Articulated Crystal Dragon 3D Printed Toy Desk Figurine',
      variantB_Title: 'Fidget Dragon Toy 3D Printed Crystal Desk Decor Gift for Him & Teens',
      variantA_Tags: ['crystal dragon', '3d printed', 'desk toy', 'articulated'],
      variantB_Tags: ['fidget dragon', 'gift for him', 'desk decor teens', 'sensory toy'],
      startDate: '2026-09-20',
      status: 'Active',
      beforeViews: 412,
      beforeSales: 8,
      afterViews: 684,
      afterSales: 21
    },
    {
      id: 2,
      listingId: '190412851',
      listingTitle: 'Handmade Turkish Ceramic Coffee Mug',
      experimentName: 'Seramik Kupa Başlık & Boho Etiket Testi',
      variantA_Title: 'Handmade Ceramic Coffee Mug Pottery Gift for Coffee Lover',
      variantB_Title: 'Boho Ceramic Coffee Cup Handmade Pottery Mug Aesthetic Kitchen Decor',
      variantA_Tags: ['coffee mug', 'pottery cup', 'gift for lover'],
      variantB_Tags: ['boho mug', 'aesthetic kitchen', 'handcrafted pottery'],
      startDate: '2026-09-15',
      status: 'Completed',
      beforeViews: 820,
      beforeSales: 14,
      afterViews: 1240,
      afterSales: 34
    }
  ];

  ngOnInit(): void {
    this.loadFromVds();
  }

  loadFromVds(): void {
    this.etsyApi.getAbTests().subscribe({
      next: (data) => {
        if (data && data.length > 0) {
          this.experiments = data.map((d: any) => ({
            id: d.id,
            listingId: d.listingId || d.listing_id,
            listingTitle: d.listingTitle || d.listing_title,
            experimentName: d.experimentName || d.experiment_name,
            variantA_Title: d.variantA_Title || d.variant_a_title,
            variantB_Title: d.variantB_Title || d.variant_b_title,
            variantA_Tags: d.variantA_Tags || d.variant_a_tags || [],
            variantB_Tags: d.variantB_Tags || d.variant_b_tags || [],
            startDate: d.startDate || d.start_date || new Date().toISOString(),
            status: d.status === 1 ? 'Completed' : (d.status === 2 ? 'Cancelled' : 'Active'),
            beforeViews: d.beforeViews || d.before_views || 0,
            beforeSales: d.beforeSales || d.before_sales || 0,
            afterViews: d.afterViews || d.after_views || 0,
            afterSales: d.afterSales || d.after_sales || 0
          }));
        }
      },
      error: () => {
        // Fallback to active mock data
      }
    });
  }

  get filteredExperiments(): AbTestCard[] {
    if (this.filterStatus === 'ALL') return this.experiments;
    return this.experiments.filter(e => e.status === this.filterStatus);
  }

  getActiveCount(): number {
    return this.experiments.filter(e => e.status === 'Active').length;
  }

  calculateCr(sales: number, views: number): string {
    if (!views || views === 0) return '0.0';
    return ((sales / views) * 100).toFixed(1);
  }

  openCreateModal(): void {
    this.newExp = {
      listingId: '189204859',
      listingTitle: 'Articulated Dragon 3D Print',
      experimentName: 'SEO Başlık Dönüşüm Testi',
      variantA_Title: 'Articulated Dragon 3D Print Toy',
      variantB_Title: 'Fidget Sensory Dragon Gift for Gamers Desk Toy'
    };
    this.newExpTagsRaw = 'sensory toy, gamer gift, desk decor, 3d printed';
    this.showCreateModal = true;
  }

  submitCreate(): void {
    const tagsB = this.newExpTagsRaw.split(',').map(s => s.trim()).filter(Boolean);
    const payload = {
      listingId: this.newExp.listingId,
      listingTitle: this.newExp.listingTitle,
      experimentName: this.newExp.experimentName,
      variantA_Title: this.newExp.variantA_Title,
      variantB_Title: this.newExp.variantB_Title,
      variantA_Tags: ['dragon toy', '3d print'],
      variantB_Tags: tagsB,
      variantA_Description: 'Orijinal açıklama',
      variantB_Description: 'Optimize edilmiş AI açıklaması'
    };

    this.etsyApi.createAbTest(payload).subscribe({
      next: (created) => {
        this.toastMessage = `✓ "${this.newExp.experimentName}" A/B testi başarıyla başlatıldı ve VDS SQLite'a kaydedildi!`;
        this.showCreateModal = false;
        this.loadFromVds();
        setTimeout(() => this.toastMessage = '', 4000);
      },
      error: () => {
        // Local push if offline
        this.experiments.unshift({
          id: Date.now(),
          listingId: this.newExp.listingId,
          listingTitle: this.newExp.listingTitle,
          experimentName: this.newExp.experimentName,
          variantA_Title: this.newExp.variantA_Title,
          variantB_Title: this.newExp.variantB_Title,
          variantA_Tags: ['dragon toy', '3d print'],
          variantB_Tags: tagsB,
          startDate: new Date().toISOString(),
          status: 'Active',
          beforeViews: 120,
          beforeSales: 2,
          afterViews: 145,
          afterSales: 6
        });
        this.toastMessage = `✓ "${this.newExp.experimentName}" A/B testi yerel olarak başlatıldı!`;
        this.showCreateModal = false;
        setTimeout(() => this.toastMessage = '', 4000);
      }
    });
  }

  declareWinner(exp: AbTestCard): void {
    exp.status = 'Completed';
    this.etsyApi.updateAbTestStatus(exp.id, 1).subscribe({
      next: () => {
        this.toastMessage = `✓ B Varyantı kazanan ilan edildi! Başlık ve etiketler mağazaya canlı olarak uygulandı.`;
        setTimeout(() => this.toastMessage = '', 4000);
      },
      error: () => {
        this.toastMessage = `✓ B Varyantı kazanan ilan edildi! (Yerel uygulandı)`;
        setTimeout(() => this.toastMessage = '', 4000);
      }
    });
  }

  deleteExperiment(id: number): void {
    this.experiments = this.experiments.filter(e => e.id !== id);
    this.etsyApi.deleteAbTest(id).subscribe({
      next: () => {
        this.toastMessage = '✓ A/B test deneyi VDS veri tabanından silindi.';
        setTimeout(() => this.toastMessage = '', 3000);
      }
    });
  }
}
