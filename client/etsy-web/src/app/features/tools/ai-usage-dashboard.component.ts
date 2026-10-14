import { Component, OnInit, AfterViewInit, OnDestroy, ViewChild, ElementRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService, AiProviderBalanceDto } from '../../core/services/etsy-api.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';
import { Chart, registerables } from 'chart.js';

Chart.register(...registerables);

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
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <div class="usage-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">📊</div>
          <div>
            <h1 class="page-title">AI Token, Model & Bakiye Takip Merkezi</h1>
            <p class="page-subtitle">DeepSeek, Gemini, OpenAI, Claude ve Grok resmi logoları, sıfır-mock canlı bakiyeleri ve analitik grafikleri</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-refresh" [disabled]="isLoadingBalances" (click)="loadData()">
            <span>{{ isLoadingBalances ? '⏳ Bakiye Çekiliyor...' : '⚡ Canlı Bakiyeleri Yenile' }}</span>
          </button>
        </div>
      </div>

      <!-- BRAND SELECTOR DROPDOWN BAR -->
      <div class="brand-selector-bar glass-card">
        <div class="selector-left">
          <label class="selector-label">🎯 İNCELENEN YAPAY ZEKA MARKASI:</label>
          <div class="dropdown-wrapper">
            <div class="brand-logo-preview" *ngIf="selectedBrand !== 'ALL'">
              <app-ai-logo [provider]="selectedBrand" [size]="20"></app-ai-logo>
            </div>
            <select [(ngModel)]="selectedBrand" (change)="onBrandChange()" class="brand-dropdown">
              <option value="ALL">🌐 Tüm Markalar (Konsolide Genel Bakış)</option>
              <option *ngFor="let p of providerBalances" [value]="p.provider">
                {{ p.provider }} {{ p.isActiveProvider ? '⭐ (Aktif Birincil Motor)' : '' }}
              </option>
            </select>
          </div>
        </div>

        <div class="selector-right" *ngIf="activeProviderInfo">
          <div class="active-badge-pill">
            <app-ai-logo [provider]="activeProviderInfo.provider" [size]="16"></app-ai-logo>
            <span class="active-text">Aktif Birincil Motor: <strong>{{ activeProviderInfo.provider }} ({{ activeProviderInfo.activeModel }})</strong></span>
          </div>
        </div>
      </div>

      <!-- KPI METRICS STRIP (STRICT ZERO-MOCK: ONLY LIVE/SQLITE VALUES) -->
      <div class="kpi-grid">
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM HARCANAN TOKEN</span>
          <span class="kpi-val text-blue">{{ displayTokens | number }}</span>
          <span class="kpi-sub">Girdi: {{ displayPromptTokens | number }} / Çıktı: {{ displayCompletionTokens | number }}</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM HARCANAN MALİYET ($ USD)</span>
          <span class="kpi-val text-green">\${{ displayCostUsd | number:'1.2-4' }}</span>
          <span class="kpi-sub">₺{{ displayCostTry | number:'1.2-2' }} TRY</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">KALAN BAKİYE (USD & TRY)</span>
          <span class="kpi-val text-emerald">\${{ displayRemainingBalanceUsd | number:'1.2-2' }}</span>
          <span class="kpi-sub highlight-try">₺{{ displayRemainingBalanceTry | number:'1.2-2' }} TRY</span>
        </div>
        <div class="kpi-card">
          <span class="kpi-label">TOPLAM AI İSTEK / ÇAĞRISI</span>
          <span class="kpi-val text-purple">{{ displayCalls }} Çağrı</span>
          <span class="kpi-sub">{{ selectedBrand === 'ALL' ? 'Tüm sağlayıcılar toplamı' : (selectedBrand + ' toplam çağrısı') }}</span>
        </div>
      </div>

      <!-- PROFESSIONAL ANALYTICS & CHARTS SECTION -->
      <div class="charts-row">
        <!-- DONUT CHART: Brand Token Distribution -->
        <div class="chart-card glass-card">
          <div class="chart-header">
            <div>
              <h3 class="chart-title">🤖 Yapay Zeka Sağlayıcı Token Dağılımı</h3>
              <p class="chart-subtitle">Resmi AI markalarına göre harcanan token oranları</p>
            </div>
            <div class="chart-tag">Canlı Tüketim</div>
          </div>
          <div class="chart-body">
            <canvas #tokenDonutCanvas></canvas>
            <div *ngIf="isDonutEmpty" class="chart-empty-overlay">
              <span class="empty-icon">📊</span>
              <span>Henüz harcanan token kaydı yok (0 Token / ₺0.00 TRY)</span>
            </div>
          </div>
        </div>

        <!-- BAR CHART: Module Token Breakdown -->
        <div class="chart-card glass-card">
          <div class="chart-header">
            <div>
              <h3 class="chart-title">📈 Modül Bazlı AI Tüketim Analizi</h3>
              <p class="chart-subtitle">{{ selectedBrand === 'ALL' ? 'Tüm modüllerin token tüketimi' : (selectedBrand + ' markasının modül tüketimi') }}</p>
            </div>
            <div class="chart-tag">Modül Analitiği</div>
          </div>
          <div class="chart-body">
            <canvas #moduleBarCanvas></canvas>
            <div *ngIf="isBarEmpty" class="chart-empty-overlay">
              <span class="empty-icon">📉</span>
              <span>Henüz modül bazlı tüketim kaydı oluşmadı (0 Çağrı)</span>
            </div>
          </div>
        </div>
      </div>

      <!-- SELECTED BRAND DEEP-DIVE CARD (WHEN A SPECIFIC BRAND IS SELECTED) -->
      <div class="brand-focus-card glass-card" *ngIf="selectedBrand !== 'ALL' && selectedBrandInfo">
        <div class="focus-header">
          <div class="focus-title-group">
            <span class="brand-avatar-box" [ngClass]="getBrandClass(selectedBrandInfo.provider)">
              <app-ai-logo [provider]="selectedBrandInfo.provider" [size]="36"></app-ai-logo>
            </span>
            <div>
              <div class="brand-title-row">
                <h2 class="focus-brand-name">{{ selectedBrandInfo.provider }}</h2>
                <span class="active-tag" *ngIf="selectedBrandInfo.isActiveProvider">⭐ SİSTEMİN AKTİF BİRİNCİL MOTORU</span>
                <span class="status-pill" [class.success]="selectedBrandInfo.isAvailable">
                  {{ selectedBrandInfo.isAvailable ? '● Çevrimiçi & Hazır' : '● Bağlantı Bekleniyor' }}
                </span>
              </div>
              <p class="focus-model-text">
                Kullanılan Model: <strong>{{ selectedBrandInfo.activeModel }}</strong> &nbsp;|&nbsp; 
                Birim Tarife: <span class="text-slate">{{ selectedBrandInfo.tokenUnitTariff }}</span>
              </p>
            </div>
          </div>

          <div class="focus-topup-actions">
            <a [href]="selectedBrandInfo.topUpUrl" target="_blank" rel="noopener noreferrer" class="btn-topup">
              💳 {{ selectedBrandInfo.provider }} Bakiye Yükle (Top Up) ↗
            </a>
          </div>
        </div>

        <div class="focus-metrics-row">
          <div class="focus-metric-box remaining-box">
            <span class="f-label">KALAN BAKİYE ($ USD)</span>
            <span class="f-val-usd">\${{ selectedBrandInfo.totalBalanceUsd | number:'1.2-2' }}</span>
            <span class="f-val-try">₺{{ (selectedBrandInfo.totalBalanceUsd * exchangeRate) | number:'1.2-2' }} TRY</span>
            <span class="f-status-msg">{{ selectedBrandInfo.statusMessage }}</span>
          </div>

          <div class="focus-metric-box">
            <span class="f-label">TOPLAM HARCANAN TUTAR</span>
            <span class="f-val-cost">\${{ selectedBrandInfo.totalCostUsd | number:'1.2-4' }}</span>
            <span class="f-sub-try">₺{{ (selectedBrandInfo.totalCostUsd * exchangeRate) | number:'1.2-2' }} TRY</span>
            <span class="f-sub-desc">Kümülatif AI harcaması</span>
          </div>

          <div class="focus-metric-box">
            <span class="f-label">KULLANILAN TOPLAM TOKEN</span>
            <span class="f-val-num text-blue">{{ selectedBrandInfo.totalTokensUsed | number }}</span>
            <span class="f-token-detail">
              Girdi: {{ selectedBrandInfo.promptTokensUsed | number }} &nbsp;|&nbsp; Çıktı: {{ selectedBrandInfo.completionTokensUsed | number }}
            </span>
            <span class="f-sub-desc">Gerçek SQLite tüketimi</span>
          </div>

          <div class="focus-metric-box">
            <span class="f-label">TOPLAM API ÇAĞRISI</span>
            <span class="f-val-num text-purple">{{ selectedBrandInfo.totalApiRequests }} İstek</span>
            <span class="f-key-masked">API Key: <code>{{ selectedBrandInfo.maskedApiKey }}</code></span>
            <span class="f-sub-desc">VDS SQLite güvencesinde kayıtlı</span>
          </div>
        </div>
      </div>

      <!-- ALL BRANDS CARDS GRID (WHEN 'ALL' IS SELECTED) -->
      <div class="provider-grid" *ngIf="selectedBrand === 'ALL'">
        <div class="glass-card provider-card" *ngFor="let p of providerBalances" [class.is-primary]="p.isActiveProvider">
          <div class="provider-header">
            <div class="provider-badge-wrap">
              <app-ai-logo [provider]="p.provider" [size]="24"></app-ai-logo>
              <span class="provider-badge" [ngClass]="getBrandClass(p.provider)">{{ p.provider }}</span>
            </div>
            <span class="primary-indicator" *ngIf="p.isActiveProvider">⭐ Aktif Motor</span>
          </div>

          <div class="card-balance-banner">
            <div class="banner-left">
              <span class="banner-label">Kalan Bakiye:</span>
              <span class="banner-usd">\${{ p.totalBalanceUsd | number:'1.2-2' }}</span>
            </div>
            <div class="banner-right">
              <span class="banner-try">₺{{ (p.totalBalanceUsd * exchangeRate) | number:'1.2-2' }} TRY</span>
            </div>
          </div>

          <p class="provider-desc">
            Model: <strong>{{ p.activeModel }}</strong><br>
            <small class="tariff-hint">{{ p.tokenUnitTariff }}</small>
          </p>

          <div class="provider-meta">
            <div class="meta-item">
              <span class="m-label">Harcanan Token:</span>
              <span class="m-val"><b>{{ p.totalTokensUsed | number }}</b></span>
            </div>
            <div class="meta-item">
              <span class="m-label">Toplam İstek:</span>
              <span class="m-val"><b>{{ p.totalApiRequests }} Çağrı</b></span>
            </div>
            <div class="meta-item">
              <span class="m-label">Maliyet:</span>
              <span class="m-val text-green"><b>\${{ p.totalCostUsd | number:'1.2-4' }}</b></span>
            </div>
          </div>

          <div class="card-footer-action">
            <a [href]="p.topUpUrl" target="_blank" rel="noopener noreferrer" class="card-btn-topup">
              Bakiye Yükle (Top Up) ↗
            </a>
            <button type="button" class="btn-inspect-brand" (click)="selectBrand(p.provider)">
              Detayları İncele →
            </button>
          </div>
        </div>
      </div>

      <!-- USAGE HISTORY TABLE (ZERO-MOCK: ONLY FROM SQLITE) -->
      <div class="history-section glass-card">
        <div class="table-header">
          <div>
            <h2 class="table-title">
              {{ selectedBrand === 'ALL' ? 'Son AI Çağrı Günlüğü (Tüm Markalar - VDS SQLite)' : (selectedBrand + ' Markasına Ait Çağrı Günlüğü') }}
            </h2>
            <p class="table-subtitle">VDS SQLite veritabanında depolanan her bir AI token tüketimi, model ve yanıt süresi</p>
          </div>
          <div class="filter-row">
            <select [(ngModel)]="selectedBrand" class="filter-select" (change)="onBrandChange()">
              <option value="ALL">Tüm Sağlayıcılar</option>
              <option *ngFor="let p of providerBalances" [value]="p.provider">{{ p.provider }}</option>
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
                <th>Maliyet (₺)</th>
                <th>Durum</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let item of filteredHistory">
                <td class="cell-time">{{ item.createdAt | slice:11:19 }}</td>
                <td class="cell-module"><b>{{ item.moduleName }}</b></td>
                <td>
                  <div class="table-brand-cell">
                    <app-ai-logo [provider]="item.provider" [size]="18"></app-ai-logo>
                    <span class="badge" [ngClass]="getBrandClass(item.provider)">{{ item.provider }}</span>
                  </div>
                </td>
                <td class="cell-model"><code>{{ item.modelName }}</code></td>
                <td>{{ item.promptTokens | number }} / {{ item.completionTokens | number }}</td>
                <td><b>{{ item.totalTokens | number }}</b></td>
                <td class="text-green">\${{ item.costUsd | number:'1.4-4' }}</td>
                <td class="text-emerald">₺{{ item.costTry | number:'1.2-2' }}</td>
                <td>
                  <span class="status-dot green"></span>
                  <span class="status-text">{{ item.status }}</span>
                </td>
              </tr>
              <tr *ngIf="filteredHistory.length === 0">
                <td colspan="9" class="empty-table-cell">
                  {{ selectedBrand === 'ALL' ? 'Sistemde henüz kayıtlı AI çağrı günlüğü bulunmuyor (0 Çağrı / 0 Token).' : (selectedBrand + ' markası için henüz kayıtlı çağrı günlüğü bulunmuyor.') }}
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
      gap: 20px;
      padding: 24px 32px;
      color: #e2e8f0;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
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
      letter-spacing: -0.3px;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 4px 0 0 0;
    }
    .btn-refresh {
      background: linear-gradient(135deg, rgba(56, 189, 248, 0.2), rgba(16, 185, 129, 0.2));
      border: 1px solid rgba(56, 189, 248, 0.35);
      color: #38bdf8;
      padding: 10px 20px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-refresh:hover {
      background: rgba(56, 189, 248, 0.3);
      color: #fff;
      transform: translateY(-1px);
    }
    .btn-refresh:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    /* BRAND SELECTOR BAR */
    .brand-selector-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 14px 20px;
      border-radius: 12px;
      background: #0d1527;
      border: 1px solid rgba(59, 130, 246, 0.25);
    }
    .selector-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .dropdown-wrapper {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .brand-logo-preview {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      padding: 4px;
      background: rgba(255, 255, 255, 0.05);
      border-radius: 6px;
      border: 1px solid rgba(255, 255, 255, 0.1);
    }
    .selector-label {
      font-size: 0.8rem;
      font-weight: 800;
      color: #94a3b8;
      letter-spacing: 0.5px;
    }
    .brand-dropdown {
      background: #111a2e;
      color: #f8fafc;
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 8px;
      padding: 8px 16px;
      font-size: 0.92rem;
      font-weight: 700;
      cursor: pointer;
      outline: none;
      min-width: 280px;
    }
    .brand-dropdown:focus {
      border-color: #38bdf8;
      box-shadow: 0 0 0 2px rgba(56, 189, 248, 0.25);
    }
    .active-badge-pill {
      display: flex;
      align-items: center;
      gap: 8px;
      background: rgba(16, 185, 129, 0.12);
      border: 1px solid rgba(16, 185, 129, 0.3);
      padding: 6px 14px;
      border-radius: 20px;
      font-size: 0.78rem;
      color: #34d399;
    }

    /* KPI GRID */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 16px;
    }
    .kpi-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
    }
    .kpi-label { font-size: 0.72rem; color: #94a3b8; font-weight: 700; letter-spacing: 0.4px; }
    .kpi-val { font-size: 1.7rem; font-weight: 800; margin: 4px 0; }
    .kpi-sub { font-size: 0.76rem; color: #64748b; }
    .highlight-try {
      color: #34d399;
      font-weight: 700;
    }
    .text-blue { color: #38bdf8; }
    .text-green { color: #34d399; }
    .text-emerald { color: #10b981; }
    .text-purple { color: #c084fc; }

    /* CHARTS SECTION */
    .charts-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(380px, 1fr));
      gap: 18px;
    }
    .chart-card {
      background: #0d1527;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px 24px;
      display: flex;
      flex-direction: column;
      position: relative;
    }
    .chart-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 14px;
    }
    .chart-title {
      font-size: 1.02rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
    }
    .chart-subtitle {
      font-size: 0.76rem;
      color: #94a3b8;
      margin: 3px 0 0 0;
    }
    .chart-tag {
      font-size: 0.68rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 6px;
      background: rgba(56, 189, 248, 0.15);
      color: #38bdf8;
      border: 1px solid rgba(56, 189, 248, 0.3);
    }
    .chart-body {
      position: relative;
      height: 250px;
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .chart-body canvas {
      max-height: 250px;
      width: 100% !important;
    }
    .chart-empty-overlay {
      position: absolute;
      inset: 0;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 8px;
      background: rgba(13, 21, 39, 0.88);
      border-radius: 10px;
      color: #94a3b8;
      font-size: 0.82rem;
      font-weight: 600;
      text-align: center;
      pointer-events: none;
    }
    .chart-empty-overlay .empty-icon {
      font-size: 1.8rem;
      opacity: 0.7;
    }

    /* BRAND FOCUS CARD */
    .brand-focus-card {
      background: linear-gradient(145deg, #0f192f, #090e1a);
      border: 1px solid rgba(56, 189, 248, 0.3);
      border-radius: 16px;
      padding: 24px 28px;
      display: flex;
      flex-direction: column;
      gap: 20px;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.4);
    }
    .focus-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 16px;
    }
    .focus-title-group {
      display: flex;
      align-items: center;
      gap: 18px;
    }
    .brand-avatar-box {
      width: 58px;
      height: 58px;
      border-radius: 14px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.15);
    }
    .brand-avatar-box.deepseek { background: rgba(2, 132, 199, 0.15); border-color: rgba(2, 132, 199, 0.35); }
    .brand-avatar-box.gemini { background: rgba(155, 114, 203, 0.15); border-color: rgba(155, 114, 203, 0.35); }
    .brand-avatar-box.openai { background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); }
    .brand-avatar-box.claude { background: rgba(217, 119, 6, 0.15); border-color: rgba(217, 119, 6, 0.35); }
    .brand-avatar-box.grok { background: rgba(248, 250, 252, 0.1); border-color: rgba(248, 250, 252, 0.25); }

    .brand-title-row {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .focus-brand-name {
      font-size: 1.45rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .active-tag {
      background: rgba(245, 158, 11, 0.2);
      border: 1px solid rgba(245, 158, 11, 0.4);
      color: #fbbf24;
      font-size: 0.72rem;
      font-weight: 800;
      padding: 3px 8px;
      border-radius: 6px;
    }
    .status-pill {
      font-size: 0.72rem;
      padding: 3px 8px;
      border-radius: 6px;
      background: rgba(239, 68, 68, 0.2);
      color: #f87171;
      border: 1px solid rgba(239, 68, 68, 0.3);
    }
    .status-pill.success {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
      border-color: rgba(16, 185, 129, 0.3);
    }
    .focus-model-text {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 4px 0 0 0;
    }
    .text-slate { color: #64748b; font-size: 0.8rem; }
    .btn-topup {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      background: linear-gradient(135deg, #2563eb, #1d4ed8);
      color: #fff;
      text-decoration: none;
      padding: 10px 18px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.88rem;
      transition: all 0.2s;
      border: 1px solid rgba(255, 255, 255, 0.15);
    }
    .btn-topup:hover {
      background: linear-gradient(135deg, #1d4ed8, #1e40af);
      transform: translateY(-1px);
      box-shadow: 0 4px 12px rgba(37, 99, 235, 0.4);
    }
    .focus-metrics-row {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(220px, 1fr));
      gap: 16px;
    }
    .focus-metric-box {
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 16px 18px;
      display: flex;
      flex-direction: column;
    }
    .focus-metric-box.remaining-box {
      border-color: rgba(16, 185, 129, 0.35);
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.08), rgba(15, 23, 42, 0.8));
    }
    .f-label { font-size: 0.72rem; color: #94a3b8; font-weight: 700; }
    .f-val-usd { font-size: 1.85rem; font-weight: 800; color: #34d399; margin-top: 4px; }
    .f-val-try { font-size: 1.05rem; font-weight: 700; color: #10b981; }
    .f-status-msg { font-size: 0.74rem; color: #64748b; margin-top: 6px; }
    .f-val-cost { font-size: 1.5rem; font-weight: 800; color: #34d399; margin-top: 4px; }
    .f-sub-try { font-size: 0.85rem; color: #94a3b8; font-weight: 600; }
    .f-val-num { font-size: 1.6rem; font-weight: 800; margin-top: 4px; }
    .f-token-detail { font-size: 0.78rem; color: #cbd5e1; margin-top: 4px; }
    .f-sub-desc { font-size: 0.72rem; color: #64748b; margin-top: 4px; }
    .f-key-masked { margin-top: 4px; font-size: 0.8rem; color: #94a3b8; }
    .f-key-masked code { background: rgba(0, 0, 0, 0.3); padding: 2px 6px; border-radius: 4px; color: #38bdf8; }

    /* PROVIDER CARDS GRID */
    .provider-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 16px;
    }
    .provider-card {
      background: #0d1527;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      transition: all 0.2s;
    }
    .provider-card:hover {
      border-color: rgba(56, 189, 248, 0.35);
      transform: translateY(-2px);
    }
    .provider-card.is-primary {
      border-color: rgba(16, 185, 129, 0.4);
      background: linear-gradient(145deg, #0d1a29, #0a1120);
    }
    .provider-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .provider-badge-wrap {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .provider-badge {
      font-size: 0.85rem;
      font-weight: 800;
      padding: 4px 10px;
      border-radius: 6px;
    }
    .provider-badge.deepseek { background: rgba(2, 132, 199, 0.2); color: #38bdf8; }
    .provider-badge.gemini { background: rgba(155, 114, 203, 0.2); color: #c084fc; }
    .provider-badge.openai { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .provider-badge.claude { background: rgba(217, 119, 6, 0.2); color: #fbbf24; }
    .provider-badge.grok { background: rgba(248, 250, 252, 0.15); color: #f8fafc; }
    .primary-indicator {
      font-size: 0.72rem;
      color: #34d399;
      font-weight: 700;
      background: rgba(16, 185, 129, 0.15);
      padding: 2px 8px;
      border-radius: 12px;
      border: 1px solid rgba(16, 185, 129, 0.3);
    }
    .card-balance-banner {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 8px;
      padding: 10px 14px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .banner-left { display: flex; flex-direction: column; }
    .banner-label { font-size: 0.68rem; color: #94a3b8; font-weight: 700; }
    .banner-usd { font-size: 1.25rem; font-weight: 800; color: #34d399; }
    .banner-right { text-align: right; }
    .banner-try { font-size: 0.88rem; font-weight: 700; color: #10b981; }
    .provider-desc {
      font-size: 0.8rem;
      color: #cbd5e1;
      margin: 0;
      line-height: 1.4;
    }
    .tariff-hint { color: #64748b; font-size: 0.72rem; }
    .provider-meta {
      display: flex;
      justify-content: space-between;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      padding-top: 10px;
    }
    .meta-item { display: flex; flex-direction: column; }
    .m-label { font-size: 0.68rem; color: #94a3b8; }
    .m-val { font-size: 0.82rem; color: #f8fafc; margin-top: 2px; }
    .card-footer-action {
      display: flex;
      gap: 8px;
      margin-top: 4px;
    }
    .card-btn-topup {
      flex: 1;
      text-align: center;
      background: rgba(37, 99, 235, 0.2);
      border: 1px solid rgba(37, 99, 235, 0.4);
      color: #60a5fa;
      padding: 7px 10px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 700;
      text-decoration: none;
      transition: all 0.2s;
    }
    .card-btn-topup:hover {
      background: #2563eb;
      color: #fff;
    }
    .btn-inspect-brand {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #cbd5e1;
      padding: 7px 10px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-inspect-brand:hover {
      background: rgba(255, 255, 255, 0.15);
      color: #fff;
    }

    /* HISTORY TABLE */
    .history-section {
      background: #0d1527;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 22px 26px;
    }
    .table-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 18px;
    }
    .table-title {
      font-size: 1.15rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
    }
    .table-subtitle {
      font-size: 0.8rem;
      color: #94a3b8;
      margin: 4px 0 0 0;
    }
    .filter-select {
      background: #111a2e;
      color: #f8fafc;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 6px;
      padding: 6px 12px;
      font-size: 0.82rem;
      font-weight: 600;
    }
    .table-responsive {
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
    }
    .data-table th {
      text-align: left;
      padding: 10px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
      color: #94a3b8;
      font-weight: 700;
    }
    .data-table td {
      padding: 10px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #cbd5e1;
    }
    .cell-time { color: #94a3b8; }
    .cell-module { color: #f8fafc; }
    .table-brand-cell {
      display: inline-flex;
      align-items: center;
      gap: 6px;
    }
    .cell-model code {
      color: #38bdf8;
      background: rgba(15, 23, 42, 0.8);
      padding: 2px 6px;
      border-radius: 4px;
    }
    .badge {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .badge.deepseek { background: rgba(2, 132, 199, 0.2); color: #38bdf8; }
    .badge.gemini { background: rgba(155, 114, 203, 0.2); color: #c084fc; }
    .badge.openai { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .badge.claude { background: rgba(217, 119, 6, 0.2); color: #fbbf24; }
    .badge.grok { background: rgba(248, 250, 252, 0.15); color: #f8fafc; }

    .status-dot {
      display: inline-block;
      width: 6px;
      height: 6px;
      border-radius: 50%;
      margin-right: 6px;
    }
    .status-dot.green { background: #10b981; box-shadow: 0 0 6px #10b981; }
    .empty-table-cell {
      text-align: center;
      padding: 32px;
      color: #64748b;
      font-style: italic;
    }
  `]
})
export class AiUsageDashboardComponent implements OnInit, AfterViewInit, OnDestroy {
  etsyApi = inject(EtsyApiService);

  @ViewChild('tokenDonutCanvas') tokenDonutCanvas!: ElementRef<HTMLCanvasElement>;
  @ViewChild('moduleBarCanvas') moduleBarCanvas!: ElementRef<HTMLCanvasElement>;

  providerBalances: AiProviderBalanceDto[] = [];
  selectedBrand: string = 'ALL';
  selectedBrandInfo: AiProviderBalanceDto | null = null;
  activeProviderInfo: AiProviderBalanceDto | null = null;
  isLoadingBalances: boolean = false;

  // STRICT ZERO-MOCK: Initialized to 0, no mock numbers
  history: UsageRecord[] = [];

  isDonutEmpty = false;
  isBarEmpty = false;
  private donutChart: Chart | null = null;
  private barChart: Chart | null = null;

  get exchangeRate(): number {
    return this.etsyApi.exchangeRate() || 49.38;
  }

  get displayTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalTokensUsed || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.totalTokensUsed || 0), 0);
  }

  get displayPromptTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.promptTokensUsed || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.promptTokensUsed || 0), 0);
  }

  get displayCompletionTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.completionTokensUsed || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.completionTokensUsed || 0), 0);
  }

  get displayCostUsd(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalCostUsd || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.totalCostUsd || 0), 0);
  }

  get displayCostTry(): number {
    return Number((this.displayCostUsd * this.exchangeRate).toFixed(2));
  }

  get displayRemainingBalanceUsd(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalBalanceUsd || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.totalBalanceUsd || 0), 0);
  }

  get displayRemainingBalanceTry(): number {
    return Number((this.displayRemainingBalanceUsd * this.exchangeRate).toFixed(2));
  }

  get displayCalls(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalApiRequests || 0;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.totalApiRequests || 0), 0);
  }

  get filteredHistory(): UsageRecord[] {
    if (this.selectedBrand === 'ALL') {
      return this.history;
    }
    return this.history.filter(h =>
      h.provider.toLowerCase().includes(this.selectedBrand.toLowerCase()) ||
      this.selectedBrand.toLowerCase().includes(h.provider.toLowerCase())
    );
  }

  ngOnInit(): void {
    this.loadData();
  }

  ngAfterViewInit(): void {
    // Initial chart rendering
    setTimeout(() => this.updateCharts(), 50);
  }

  ngOnDestroy(): void {
    if (this.donutChart) {
      this.donutChart.destroy();
      this.donutChart = null;
    }
    if (this.barChart) {
      this.barChart.destroy();
      this.barChart = null;
    }
  }

  loadData(): void {
    this.isLoadingBalances = true;
    this.etsyApi.getAiProviderBalances(undefined, this.exchangeRate).subscribe({
      next: (balances) => {
        this.isLoadingBalances = false;
        if (balances && balances.length > 0) {
          this.providerBalances = balances;
          this.activeProviderInfo = balances.find(b => b.isActiveProvider) || balances[0];
          this.updateSelectedBrandInfo();
          this.updateCharts();
        }
      },
      error: () => {
        this.isLoadingBalances = false;
      }
    });

    this.etsyApi.getAiUsageHistory(undefined, 100).subscribe({
      next: (hist) => {
        if (hist && hist.length > 0) {
          this.history = hist.map((h: any) => ({
            id: h.id,
            createdAt: h.createdAt || h.created_at || new Date().toISOString(),
            moduleName: h.moduleName || h.module_name || 'AI Generator',
            provider: h.provider || 'DeepSeek',
            modelName: h.modelName || h.model_name || 'DeepSeek-V4-Flash',
            promptTokens: h.promptTokens || h.prompt_tokens || 0,
            completionTokens: h.completionTokens || h.completion_tokens || 0,
            totalTokens: h.totalTokens || h.total_tokens || 0,
            costUsd: h.costUsd || h.cost_usd || 0,
            costTry: h.costTry || h.cost_try || Number(((h.costUsd || h.cost_usd || 0) * this.exchangeRate).toFixed(2)),
            status: h.status || 'Başarılı'
          }));
        } else {
          this.history = [];
        }
        this.updateCharts();
      }
    });
  }

  onBrandChange(): void {
    this.updateSelectedBrandInfo();
    this.updateCharts();
  }

  selectBrand(brand: string): void {
    this.selectedBrand = brand;
    this.updateSelectedBrandInfo();
    this.updateCharts();
  }

  private updateSelectedBrandInfo(): void {
    if (this.selectedBrand === 'ALL') {
      this.selectedBrandInfo = null;
    } else {
      this.selectedBrandInfo = this.providerBalances.find(b => b.provider === this.selectedBrand) || null;
    }
  }

  updateCharts(): void {
    this.renderDonutChart();
    this.renderBarChart();
  }

  private renderDonutChart(): void {
    if (!this.tokenDonutCanvas?.nativeElement) return;

    if (this.donutChart) {
      this.donutChart.destroy();
      this.donutChart = null;
    }

    const labels = this.providerBalances.map(p => p.provider);
    const data = this.providerBalances.map(p => p.totalTokensUsed || 0);
    const totalTokens = data.reduce((a, b) => a + b, 0);

    this.isDonutEmpty = totalTokens === 0;

    const brandColors: Record<string, string> = {
      'DeepSeek': '#0284c7',
      'Google Gemini': '#8b5cf6',
      'OpenAI': '#10b981',
      'Claude': '#d97706',
      'Grok': '#94a3b8'
    };

    const backgroundColors = labels.map(l => brandColors[l] || '#38bdf8');
    const ctx = this.tokenDonutCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.donutChart = new Chart(ctx, {
      type: 'doughnut',
      data: {
        labels: this.isDonutEmpty ? ['Veri Yok'] : labels,
        datasets: [{
          data: this.isDonutEmpty ? [1] : data,
          backgroundColor: this.isDonutEmpty ? ['rgba(255, 255, 255, 0.05)'] : backgroundColors,
          borderColor: '#0d1527',
          borderWidth: 2,
          hoverOffset: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'right',
            labels: {
              color: '#94a3b8',
              font: { size: 11, weight: 'bold' },
              padding: 12
            }
          },
          tooltip: {
            enabled: !this.isDonutEmpty,
            callbacks: {
              label: (context) => {
                const val = Number(context.raw || 0);
                return ` ${context.label}: ${val.toLocaleString()} Token`;
              }
            }
          }
        },
        cutout: '72%'
      }
    });
  }

  private renderBarChart(): void {
    if (!this.moduleBarCanvas?.nativeElement) return;

    if (this.barChart) {
      this.barChart.destroy();
      this.barChart = null;
    }

    const moduleMap = new Map<string, number>();
    for (const h of this.filteredHistory) {
      const mod = h.moduleName || 'Genel';
      moduleMap.set(mod, (moduleMap.get(mod) || 0) + (h.totalTokens || 0));
    }

    const labels = Array.from(moduleMap.keys());
    const data = Array.from(moduleMap.values());
    const total = data.reduce((a, b) => a + b, 0);

    this.isBarEmpty = total === 0 || labels.length === 0;

    const ctx = this.moduleBarCanvas.nativeElement.getContext('2d');
    if (!ctx) return;

    this.barChart = new Chart(ctx, {
      type: 'bar',
      data: {
        labels: this.isBarEmpty ? ['Modül Verisi Yok'] : labels,
        datasets: [{
          label: 'Harcanan Token',
          data: this.isBarEmpty ? [0] : data,
          backgroundColor: 'rgba(56, 189, 248, 0.45)',
          borderColor: '#38bdf8',
          borderWidth: 1.5,
          borderRadius: 6
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          x: {
            grid: { display: false },
            ticks: { color: '#94a3b8', font: { size: 11 } }
          },
          y: {
            grid: { color: 'rgba(255, 255, 255, 0.05)' },
            ticks: { color: '#94a3b8', font: { size: 10 } },
            beginAtZero: true
          }
        },
        plugins: {
          legend: { display: false },
          tooltip: {
            enabled: !this.isBarEmpty,
            callbacks: {
              label: (context) => ` ${Number(context.raw || 0).toLocaleString()} Token`
            }
          }
        }
      }
    });
  }

  getBrandClass(provider: string): string {
    const p = provider.toLowerCase();
    if (p.includes('deepseek')) return 'deepseek';
    if (p.includes('gemini') || p.includes('google')) return 'gemini';
    if (p.includes('openai') || p.includes('gpt')) return 'openai';
    if (p.includes('claude') || p.includes('anthropic')) return 'claude';
    if (p.includes('grok')) return 'grok';
    return 'default';
  }
}
