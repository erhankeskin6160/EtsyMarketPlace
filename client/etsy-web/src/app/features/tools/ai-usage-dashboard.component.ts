import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface UsageRecord {
  id: number;
  createdAt: string;
  moduleName: string;
  provider: string;
  modelName: string;
  promptTokens: number;
  completionTokens: number;
  totalTokens: number;
  costUsd: number;
  costTry: number;
  status: string;
}

@Component({
  selector: 'app-ai-usage-dashboard',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="usage-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">📊</div>
          <div>
            <h1 class="page-title">AI Token, Model & Bakiye Takip Merkezi</h1>
            <p class="page-subtitle">Gemini Spark, Claude 3.5 ve OpenAI API tüketimlerinin anlık takibi, token kotaları ve maliyet analizleri</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-refresh" (click)="loadData()">
            ⚡ Verileri Yenile
          </button>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM TOKEN (30 GÜN)</span>
          <span class="kpi-val text-blue">{{ totalTokens | number }}</span>
          <span class="kpi-sub">Girdi: {{ promptTokens | number }} / Çıktı: {{ completionTokens | number }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM MALİYET ($ USD)</span>
          <span class="kpi-val text-green">\${{ totalCostUsd | number:'1.2-2' }}</span>
          <span class="kpi-sub">₺{{ (totalCostUsd * etsyApi.exchangeRate()) | number:'1.2-2' }} TRY</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM AI ÇAĞRISI</span>
          <span class="kpi-val text-purple">{{ totalCalls }} Çağrı</span>
          <span class="kpi-sub">Ort. Yanıt: 420 ms</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">AYLIK TOKEN KOTASI</span>
          <span class="kpi-val text-orange">%{{ quotaPercent }}</span>
          <div class="progress-bar">
            <div class="progress-fill" [style.width.%]="quotaPercent"></div>
          </div>
          <span class="kpi-sub">Kalan: {{ (500000 - totalTokens) | number }} Token</span>
        </div>
      </div>

      <!-- PROVIDER CONSUMPTION DISTRIBUTION -->
      <div class="provider-grid">
        <div class="glass-card provider-card">
          <div class="provider-header">
            <span class="provider-badge gemini">Gemini Spark (Google)</span>
            <span class="provider-share">%68 Pay</span>
          </div>
          <p class="provider-desc">gemini-2.5-flash & gemini-1.5-pro modelleri hızlı başlık, açıklama ve 13 etiket üretimi için kullanılır.</p>
          <div class="provider-meta">
            <span>Token: <b>184,200</b></span>
            <span>Maliyet: <b class="text-green">$0.82</b></span>
          </div>
        </div>

        <div class="glass-card provider-card">
          <div class="provider-header">
            <span class="provider-badge claude">Anthropic Claude</span>
            <span class="provider-share">%22 Pay</span>
          </div>
          <p class="provider-desc">claude-3-5-sonnet derin pazar araştırması, rakip listeleme analizi ve karmaşık finansal denetimlerde devrededir.</p>
          <div class="provider-meta">
            <span>Token: <b>62,400</b></span>
            <span>Maliyet: <b class="text-green">$1.85</b></span>
          </div>
        </div>

        <div class="glass-card provider-card">
          <div class="provider-header">
            <span class="provider-badge openai">OpenAI GPT-4o</span>
            <span class="provider-share">%10 Pay</span>
          </div>
          <p class="provider-desc">gpt-4o-mini yardımcı çeviri, müşteri mesaj yanıtları ve çok dilli Etsy etiket denetimlerinde kullanılır.</p>
          <div class="provider-meta">
            <span>Token: <b>28,100</b></span>
            <span>Maliyet: <b class="text-green">$0.45</b></span>
          </div>
        </div>
      </div>

      <!-- USAGE HISTORY TABLE -->
      <div class="history-section glass-card">
        <div class="table-header">
          <h2 class="table-title">Son AI Çağrı Günlüğü (VDS SQLite)</h2>
          <div class="filter-row">
            <select [(ngModel)]="providerFilter" class="filter-select" (change)="loadData()">
              <option value="">Tüm Sağlayıcılar</option>
              <option value="Google">Google (Gemini)</option>
              <option value="Anthropic">Anthropic (Claude)</option>
              <option value="OpenAI">OpenAI</option>
            </select>
          </div>
        </div>

        <div class="table-responsive">
          <table class="data-table">
            <thead>
              <tr>
                <th>Zaman</th>
                <th>Modül</th>
                <th>Sağlayıcı</th>
                <th>Model</th>
                <th>Girdi / Çıktı Token</th>
                <th>Toplam Token</th>
                <th>Maliyet ($)</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of history">
                <td class="cell-time">{{ item.createdAt | slice:11:19 }}</td>
                <td class="cell-module"><b>{{ item.moduleName }}</b></td>
                <td>
                  <span class="badge" [ngClass]="item.provider.toLowerCase()">{{ item.provider }}</span>
                </td>
                <td class="cell-model"><code>{{ item.modelName }}</code></td>
                <td>{{ item.promptTokens }} / {{ item.completionTokens }}</td>
                <td><b>{{ item.totalTokens }}</b></td>
                <td class="text-green">\${{ item.costUsd | number:'1.4-4' }}</td>
                <td>
                  <span class="status-dot green"></span>
                  <span class="status-text">{{ item.status }}</span>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .usage-container {
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
      background: rgba(14, 165, 233, 0.15);
      border: 1px solid rgba(14, 165, 233, 0.3);
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
    .btn-refresh {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #fff;
      padding: 9px 18px;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-refresh:hover { background: rgba(255, 255, 255, 0.15); }

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
    .text-orange { color: #f97316; }

    .progress-bar {
      height: 6px;
      background: rgba(255, 255, 255, 0.1);
      border-radius: 4px;
      margin: 6px 0;
      overflow: hidden;
    }
    .progress-fill {
      height: 100%;
      background: linear-gradient(90deg, #38bdf8, #f97316);
      border-radius: 4px;
    }

    .provider-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 16px;
    }
    .provider-card {
      background: rgba(17, 24, 39, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .provider-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .provider-badge {
      font-size: 0.75rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 6px;
    }
    .provider-badge.gemini { background: rgba(56, 189, 248, 0.15); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.3); }
    .provider-badge.claude { background: rgba(217, 119, 6, 0.15); color: #fbbf24; border: 1px solid rgba(217, 119, 6, 0.3); }
    .provider-badge.openai { background: rgba(16, 185, 129, 0.15); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.3); }
    .provider-share { font-size: 0.75rem; color: #94a3b8; font-weight: 700; }
    .provider-desc { font-size: 0.78rem; color: #94a3b8; line-height: 1.4; margin: 0; flex: 1; }
    .provider-meta {
      display: flex;
      justify-content: space-between;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      padding-top: 10px;
      font-size: 0.78rem;
      color: #cbd5e1;
    }

    .history-section {
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
    .filter-select {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #cbd5e1;
      padding: 6px 12px;
      border-radius: 6px;
      font-size: 0.8rem;
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
    }
    .cell-time { font-family: monospace; color: #64748b; }
    .cell-model code { background: rgba(0, 0, 0, 0.4); padding: 2px 6px; border-radius: 4px; color: #38bdf8; font-size: 0.75rem; }
    .badge {
      font-size: 0.7rem;
      padding: 2px 6px;
      border-radius: 4px;
      font-weight: 600;
    }
    .badge.google { background: rgba(56, 189, 248, 0.15); color: #38bdf8; }
    .badge.anthropic { background: rgba(217, 119, 6, 0.15); color: #fbbf24; }
    .badge.openai { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .status-dot { width: 6px; height: 6px; border-radius: 50%; display: inline-block; margin-right: 6px; }
    .status-dot.green { background: #10b981; box-shadow: 0 0 6px #10b981; }
    .status-text { font-size: 0.75rem; color: #34d399; }
  `]
})
export class AiUsageDashboardComponent implements OnInit {
  etsyApi = inject(EtsyApiService);

  providerFilter = '';
  totalTokens = 274700;
  promptTokens = 192000;
  completionTokens = 82700;
  totalCostUsd = 3.12;
  totalCalls = 318;
  quotaPercent = 54;

  history: UsageRecord[] = [
    {
      id: 1,
      createdAt: '2026-10-03T18:14:22Z',
      moduleName: 'FastListingCreator',
      provider: 'Google',
      modelName: 'gemini-2.5-flash',
      promptTokens: 820,
      completionTokens: 240,
      totalTokens: 1060,
      costUsd: 0.0003,
      costTry: 0.012,
      status: 'Başarılı'
    },
    {
      id: 2,
      createdAt: '2026-10-03T17:52:10Z',
      moduleName: 'AiAuditInspector',
      provider: 'Anthropic',
      modelName: 'claude-3-5-sonnet',
      promptTokens: 2400,
      completionTokens: 850,
      totalTokens: 3250,
      costUsd: 0.0182,
      costTry: 0.70,
      status: 'Başarılı'
    },
    {
      id: 3,
      createdAt: '2026-10-03T17:10:05Z',
      moduleName: 'CompetitorSpyRadar',
      provider: 'Google',
      modelName: 'gemini-2.5-flash',
      promptTokens: 1400,
      completionTokens: 420,
      totalTokens: 1820,
      costUsd: 0.0005,
      costTry: 0.019,
      status: 'Başarılı'
    },
    {
      id: 4,
      createdAt: '2026-10-03T16:30:19Z',
      moduleName: 'Viral3DModelHunter',
      provider: 'OpenAI',
      modelName: 'gpt-4o-mini',
      promptTokens: 1100,
      completionTokens: 380,
      totalTokens: 1480,
      costUsd: 0.0004,
      costTry: 0.015,
      status: 'Başarılı'
    }
  ];

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.etsyApi.getAiUsageStats(this.providerFilter || undefined).subscribe({
      next: (stats) => {
        if (stats) {
          this.totalTokens = stats.totalTokens || this.totalTokens;
          this.promptTokens = stats.promptTokens || this.promptTokens;
          this.completionTokens = stats.completionTokens || this.completionTokens;
          this.totalCostUsd = stats.totalCostUsd || this.totalCostUsd;
          this.totalCalls = stats.totalCalls || stats.recordCount || this.totalCalls;
          this.quotaPercent = Math.min(100, Math.round((this.totalTokens / 500000) * 100));
        }
      }
    });

    this.etsyApi.getAiUsageHistory(this.providerFilter || undefined, 100).subscribe({
      next: (hist) => {
        if (hist && hist.length > 0) {
          this.history = hist.map((h: any) => ({
            id: h.id,
            createdAt: h.createdAt || h.created_at || new Date().toISOString(),
            moduleName: h.moduleName || h.module_name || 'AI Generator',
            provider: h.provider || 'Google',
            modelName: h.modelName || h.model_name || 'gemini-2.5-flash',
            promptTokens: h.promptTokens || h.prompt_tokens || 0,
            completionTokens: h.completionTokens || h.completion_tokens || 0,
            totalTokens: h.totalTokens || h.total_tokens || 0,
            costUsd: h.costUsd || h.cost_usd || 0,
            costTry: h.costTry || h.cost_try || Number(((h.costUsd || h.cost_usd || 0) * this.etsyApi.exchangeRate()).toFixed(2)),
            status: h.status || 'Başarılı'
          }));
        }
      }
    });
  }
}
