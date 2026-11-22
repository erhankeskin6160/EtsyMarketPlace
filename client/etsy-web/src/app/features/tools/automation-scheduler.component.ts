import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

interface ScheduledJob {
  id: string;
  name: string;
  schedule: string;
  lastRun: string;
  nextRun: string;
  status: 'Running' | 'Idle' | 'Disabled';
  isEnabled: boolean;
  desc: string;
}

@Component({
  selector: 'app-automation-scheduler',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('tools')],
  template: `
    <ng-container *transloco="let t">
    <div class="auto-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">⚙️</div>
          <div>
            <h1 class="page-title">{{ t('tools.automation.title') }}</h1>
            <p class="page-subtitle">{{ t('tools.automation.subtitle') }}</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-save-all" (click)="saveAll()">
            {{ t('tools.automation.saveBtn') }}
          </button>
        </div>
      </div>

      <!-- STATUS CARDS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.automation.kpi.activeSchedulers') }}</span>
          <span class="kpi-val text-green">{{ t('tools.automation.kpi.tasksCount', { count: getActiveCount() }) }}</span>
          <span class="kpi-sub">{{ t('tools.automation.kpi.systemdService') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.automation.kpi.last24h') }}</span>
          <span class="kpi-val text-blue">{{ t('tools.automation.kpi.triggersValue') }}</span>
          <span class="kpi-sub">{{ t('tools.automation.kpi.errorRate') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.automation.kpi.heartbeat') }}</span>
          <span class="kpi-val text-purple">{{ t('tools.automation.kpi.heartbeatValue') }}</span>
          <span class="kpi-sub">{{ t('tools.automation.kpi.sqliteSynced') }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">{{ t('tools.automation.kpi.shieldTitle') }}</span>
          <span class="kpi-val text-orange">{{ t('tools.automation.kpi.shieldValue') }}</span>
          <span class="kpi-sub">{{ t('tools.automation.kpi.shieldSub') }}</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- JOBS LIST SECTION -->
      <div class="jobs-section glass-card">
        <h2 class="section-title">{{ t('tools.automation.sectionTitle') }}</h2>

        <div class="jobs-list">
          <div *ngFor="let job of jobs" class="job-card" [class.disabled]="!job.isEnabled">
            <div class="job-left">
              <div class="job-status-dot" [class.green]="job.isEnabled" [class.gray]="!job.isEnabled"></div>
              <div class="job-info">
                <div class="job-title-row">
                  <span class="job-name">{{ t('tools.automation.jobs.' + job.id + '.name') }}</span>
                  <span class="cron-badge"><code>{{ t('tools.automation.jobs.' + job.id + '.schedule') }}</code></span>
                </div>
                <p class="job-desc">{{ t('tools.automation.jobs.' + job.id + '.desc') }}</p>
                <div class="job-meta">
                  <span>{{ t('tools.automation.lastRun') }} <b>{{ (job.lastRun === 'NOW' || job.lastRun === 'Şimdi') ? t('tools.automation.justNow') : t('tools.automation.jobs.' + job.id + '.lastRun') }}</b></span>
                  <span>{{ t('tools.automation.nextRun') }} <b>{{ t('tools.automation.jobs.' + job.id + '.nextRun') }}</b></span>
                </div>
              </div>
            </div>

            <div class="job-right">
              <button class="btn-run-now" (click)="runNow(job)">
                {{ t('tools.automation.runNow') }}
              </button>
              <label class="switch">
                <input type="checkbox" [(ngModel)]="job.isEnabled" />
                <span class="slider"></span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </div>
    </ng-container>
  `,
  styles: [`
    .auto-container {
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
    .btn-save-all {
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
    .btn-save-all:hover { filter: brightness(1.1); transform: translateY(-1px); }

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

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .jobs-section {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .section-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }

    .jobs-list {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .job-card {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 18px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 10px;
      transition: all 0.2s;
    }
    .job-card.disabled { opacity: 0.5; }
    .job-left {
      display: flex;
      align-items: flex-start;
      gap: 14px;
      flex: 1;
    }
    .job-status-dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
      margin-top: 6px;
    }
    .job-status-dot.green { background: #10b981; box-shadow: 0 0 10px #10b981; }
    .job-status-dot.gray { background: #64748b; }
    .job-info { display: flex; flex-direction: column; gap: 4px; flex: 1; }
    .job-title-row { display: flex; align-items: center; gap: 10px; flex-wrap: wrap; }
    .job-name { font-size: 0.95rem; font-weight: 700; color: #f1f5f9; }
    .cron-badge code {
      background: rgba(0, 0, 0, 0.4);
      padding: 2px 6px;
      border-radius: 4px;
      color: #38bdf8;
      font-size: 0.72rem;
    }
    .job-desc { font-size: 0.78rem; color: #94a3b8; margin: 0; }
    .job-meta {
      display: flex;
      gap: 20px;
      font-size: 0.72rem;
      color: #64748b;
      margin-top: 4px;
    }
    .job-meta b { color: #cbd5e1; }

    .job-right { display: flex; align-items: center; gap: 16px; }
    .btn-run-now {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #cbd5e1;
      padding: 6px 14px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-run-now:hover { background: rgba(255, 255, 255, 0.15); color: #fff; }

    /* SWITCH */
    .switch {
      position: relative;
      display: inline-block;
      width: 44px;
      height: 24px;
    }
    .switch input { opacity: 0; width: 0; height: 0; }
    .slider {
      position: absolute;
      cursor: pointer;
      inset: 0;
      background-color: #334155;
      border-radius: 24px;
      transition: .3s;
    }
    .slider:before {
      position: absolute;
      content: "";
      height: 18px;
      width: 18px;
      left: 3px;
      bottom: 3px;
      background-color: white;
      border-radius: 50%;
      transition: .3s;
    }
    input:checked + .slider { background-color: #10b981; }
    input:checked + .slider:before { transform: translateX(20px); }

    @media (max-width: 767.98px) {
      .scheduler-header,
      .task-row-header {
        flex-direction: column;
        align-items: stretch;
        gap: 8px;
      }
      .tasks-grid {
        grid-template-columns: 1fr !important;
      }
    }
  `]
})
export class TaskSchedulerAutomationComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  etsyApi = inject(EtsyApiService);

  toastMessage = '';

  jobs: ScheduledJob[] = [
    {
      id: 'job-1',
      name: 'Otomatik Etsy Sipariş & Ödeme Senkronizasyonu',
      schedule: '0 */1 * * * (Her 1 Saatte)',
      lastRun: '15 dakika önce',
      nextRun: '45 dakika sonra',
      status: 'Running',
      isEnabled: true,
      desc: 'Etsy Open API v3 üzerinden yeni siparişleri çeker ve VDS SQLite veritabanına işler.'
    },
    {
      id: 'job-2',
      name: 'Çoklu Taşıyıcı Kargo Fiyat & Takip Kontrolü',
      schedule: '*/15 * * * * (Her 15 Dakikada)',
      lastRun: '4 dakika önce',
      nextRun: '11 dakika sonra',
      status: 'Running',
      isEnabled: true,
      desc: 'Aras Global, ShipEntegra ve Navlungo kargo takip durumlarını günceller ve teslimat bildirimlerini hazırlar.'
    },
    {
      id: 'job-3',
      name: 'Gece Yarısı Finansal Kapanış & Telegram Brifingi',
      schedule: '59 23 * * * (Her Gece 23:59)',
      lastRun: 'Dün 23:59',
      nextRun: 'Bugün 23:59',
      status: 'Idle',
      isEnabled: true,
      desc: 'Günün brüt cirosunu, kargo masraflarını ve tahmini net kârını hesaplayıp Telegram kanalına raporlar.'
    },
    {
      id: 'job-4',
      name: 'Haftalık A/B Test Kazanan Belirleyici',
      schedule: '0 0 * * 0 (Pazar 00:00)',
      lastRun: '6 gün önce',
      nextRun: '1 gün sonra',
      status: 'Idle',
      isEnabled: false,
      desc: '7 günü tamamlayan A/B testlerini istatistiksel üstünlük kriterine göre otomatik sonlandırıp kazananı mağazaya uygular.'
    },
    {
      id: 'job-5',
      name: '🪜 CRO Terk Edilmiş Sepet & Favori 3 Kademeli Teklif Merdiveni',
      schedule: '0 */2 * * * (Her 2 Saatte)',
      lastRun: '32 dakika önce',
      nextRun: '1 saat 28 dk sonra',
      status: 'Running',
      isEnabled: true,
      desc: 'Favorileyenlere 2 saatte %8 nazik dürtme, sepete ekleyip çıkana 24 saatte %15 kurtarma kuponu ve 72. saatte Ücretsiz Kargo VIP teklifi tetikler.'
    },
    {
      id: 'job-6',
      name: '🛡️ Kupon + Offsite Ads (%15) C_max Marj Koruma Kalkanı',
      schedule: '*/30 * * * * (Her 30 Dakikada)',
      lastRun: '8 dakika önce',
      nextRun: '22 dakika sonra',
      status: 'Running',
      isEnabled: true,
      desc: 'Aktif mağaza kuponlarının (örn. %30 DSFJY25) %15 Offsite Ads komisyonuyla çakışarak birim net kârı hedef eşik altına düşürmesini denetler ve uyarır.'
    }
  ];

  ngOnInit(): void {
    const saved = localStorage.getItem('etsy_automation_jobs_v1');
    if (saved) {
      try {
        const parsed = JSON.parse(saved);
        if (Array.isArray(parsed) && parsed.length > 0) {
          const existingIds = new Set(parsed.map((j: ScheduledJob) => j.id));
          for (const defJob of this.jobs) {
            if (!existingIds.has(defJob.id)) {
              parsed.push(defJob);
            }
          }
          this.jobs = parsed;
        }
      } catch {}
    }
  }

  getActiveCount(): number {
    return this.jobs.filter(j => j.isEnabled).length;
  }

  runNow(job: ScheduledJob): void {
    job.lastRun = 'NOW';
    this.saveAll(false);
    const jobName = this.transloco.translate('tools.automation.jobs.' + job.id + '.name') || job.name;

    if (job.id === 'job-1') {
      this.toastMessage = this.transloco.translate('tools.automation.toasts.syncStarted', { name: jobName });
      this.etsyApi.syncFromEtsy().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res) => {
          this.toastMessage = this.transloco.translate('tools.automation.toasts.syncCompleted', {
            name: jobName,
            orderCount: res.orderCount,
            transactionCount: res.transactionCount
          });
          setTimeout(() => this.toastMessage = '', 4500);
        },
        error: () => {
          this.toastMessage = this.transloco.translate('tools.automation.toasts.syncTriggered', { name: jobName });
          setTimeout(() => this.toastMessage = '', 4000);
        }
      });
      return;
    }

    this.toastMessage = this.transloco.translate('tools.automation.toasts.runImmediate', { name: jobName });
    setTimeout(() => this.toastMessage = '', 4000);
  }

  saveAll(showToast = true): void {
    try {
      localStorage.setItem('etsy_automation_jobs_v1', JSON.stringify(this.jobs));
      if (showToast) {
        this.toastMessage = this.transloco.translate('tools.automation.toasts.savedAll');
        setTimeout(() => this.toastMessage = '', 4000);
      }
    } catch {}
  }
}
