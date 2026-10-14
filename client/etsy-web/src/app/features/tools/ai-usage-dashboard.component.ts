import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService, AiProviderBalanceDto } from '../../core/services/etsy-api.service';

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
            <p class="page-subtitle">DeepSeek, Gemini, OpenAI, Claude ve Grok bakiye ve token tüketimlerinin anlık takibi, kotaları ve maliyet analizleri</p>
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
            <select [(ngModel)]="selectedBrand" (change)="onBrandChange()" class="brand-dropdown">
              <option value="ALL">🌐 Tüm Markalar (Konsolide Genel Bakış)</option>
              <option *ngFor="let p of providerBalances" [value]="p.provider">
                {{ getBrandIcon(p.provider) }} {{ p.provider }} {{ p.isActiveProvider ? '⭐ (Aktif Birincil Motor)' : '' }}
              </option>
            </select>
          </div>
        </div>

        <div class="selector-right" *ngIf="activeProviderInfo">
          <div class="active-badge-pill">
            <span class="active-dot"></span>
            <span class="active-text">Aktif Birincil Motor: <strong>{{ activeProviderInfo.provider }} ({{ activeProviderInfo.activeModel }})</strong></span>
          </div>
        </div>
      </div>

      <!-- KPI METRICS STRIP -->
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
          <span class="kpi-sub">{{ selectedBrand === 'ALL' ? 'Tüm sağlayıcılar ortalaması' : (selectedBrand + ' toplam çağrısı') }}</span>
        </div>
      </div>

      <!-- SELECTED BRAND DEEP-DIVE CARD (WHEN A SPECIFIC BRAND IS SELECTED) -->
      <div class="brand-focus-card glass-card" *ngIf="selectedBrand !== 'ALL' && selectedBrandInfo">
        <div class="focus-header">
          <div class="focus-title-group">
            <span class="brand-avatar-box" [ngClass]="getBrandClass(selectedBrandInfo.provider)">
              {{ getBrandIcon(selectedBrandInfo.provider) }}
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
            <span class="f-sub-desc">Son 30 günlük kümülatif harcama</span>
          </div>

          <div class="focus-metric-box">
            <span class="f-label">KULLANILAN TOPLAM TOKEN</span>
            <span class="f-val-num text-blue">{{ selectedBrandInfo.totalTokensUsed | number }}</span>
            <span class="f-token-detail">
              Girdi: {{ selectedBrandInfo.promptTokensUsed | number }} &nbsp;|&nbsp; Çıktı: {{ selectedBrandInfo.completionTokensUsed | number }}
            </span>
            <span class="f-sub-desc">Token başına optimize edilmiştir</span>
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
              <span class="brand-mini-icon">{{ getBrandIcon(p.provider) }}</span>
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

      <!-- USAGE HISTORY TABLE -->
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
                  <span class="badge" [ngClass]="getBrandClass(item.provider)">{{ item.provider }}</span>
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
                  {{ selectedBrand }} markası için henüz kayıtlı çağrı günlüğü bulunmuyor.
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
    .active-dot {
      width: 8px;
      height: 8px;
      background: #10b981;
      border-radius: 50%;
      box-shadow: 0 0 8px #10b981;
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
      font-size: 2.2rem;
      width: 58px;
      height: 58px;
      border-radius: 14px;
      display: flex;
      align-items: center;
      justify-content: center;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.15);
    }
    .brand-avatar-box.deepseek { background: rgba(239, 68, 68, 0.15); border-color: rgba(239, 68, 68, 0.35); }
    .brand-avatar-box.gemini { background: rgba(59, 130, 246, 0.15); border-color: rgba(59, 130, 246, 0.35); }
    .brand-avatar-box.openai { background: rgba(16, 185, 129, 0.15); border-color: rgba(16, 185, 129, 0.35); }
    .brand-avatar-box.claude { background: rgba(168, 85, 247, 0.15); border-color: rgba(168, 85, 247, 0.35); }
    .brand-avatar-box.grok { background: rgba(249, 115, 22, 0.15); border-color: rgba(249, 115, 22, 0.35); }

    .brand-title-row {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .focus-brand-name {
      font-size: 1.55rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .active-tag {
      font-size: 0.72rem;
      font-weight: 800;
      color: #fbbf24;
      background: rgba(245, 158, 11, 0.15);
      border: 1px solid rgba(245, 158, 11, 0.35);
      padding: 2px 10px;
      border-radius: 12px;
    }
    .status-pill {
      font-size: 0.74rem;
      font-weight: 600;
      color: #94a3b8;
    }
    .status-pill.success { color: #34d399; }
    .focus-model-text {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 4px 0 0 0;
    }
    .text-slate { color: #cbd5e1; }

    .btn-topup {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      text-decoration: none;
      padding: 10px 18px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.85rem;
      transition: all 0.2s;
      display: inline-flex;
      align-items: center;
      box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
    }
    .btn-topup:hover {
      background: #047857;
      transform: translateY(-1px);
    }

    .focus-metrics-row {
      display: grid;
      grid-template-columns: 1.3fr 1fr 1.1fr 1fr;
      gap: 16px;
    }
    .focus-metric-box {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 16px 18px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .focus-metric-box.remaining-box {
      background: linear-gradient(145deg, rgba(16, 185, 129, 0.12), rgba(15, 23, 42, 0.8));
      border: 1px solid rgba(16, 185, 129, 0.35);
    }
    .f-label {
      font-size: 0.7rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.4px;
    }
    .f-val-usd {
      font-size: 1.85rem;
      font-weight: 900;
      color: #34d399;
      line-height: 1.1;
    }
    .f-val-try {
      font-size: 1.05rem;
      font-weight: 800;
      color: #10b981;
    }
    .f-status-msg {
      font-size: 0.72rem;
      color: #a7f3d0;
      margin-top: 4px;
    }
    .f-val-cost {
      font-size: 1.6rem;
      font-weight: 800;
      color: #38bdf8;
    }
    .f-sub-try {
      font-size: 0.85rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .f-val-num {
      font-size: 1.6rem;
      font-weight: 800;
    }
    .f-token-detail {
      font-size: 0.74rem;
      color: #cbd5e1;
    }
    .f-key-masked code {
      font-size: 0.72rem;
      color: #38bdf8;
      background: rgba(0,0,0,0.4);
      padding: 2px 6px;
      border-radius: 4px;
    }
    .f-sub-desc {
      font-size: 0.7rem;
      color: #64748b;
      margin-top: auto;
    }

    /* ALL BRANDS GRID */
    .provider-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(280px, 1fr));
      gap: 16px;
    }
    .provider-card {
      background: #111a2e;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
      gap: 12px;
      transition: all 0.2s;
    }
    .provider-card:hover {
      border-color: rgba(56, 189, 248, 0.3);
      transform: translateY(-2px);
    }
    .provider-card.is-primary {
      border: 1px solid rgba(245, 158, 11, 0.4);
      background: linear-gradient(145deg, #141f36, #0e1627);
    }
    .provider-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .provider-badge-wrap {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .brand-mini-icon {
      font-size: 1.2rem;
    }
    .provider-badge {
      font-size: 0.78rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 6px;
    }
    .provider-badge.deepseek { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.4); }
    .provider-badge.gemini { background: rgba(59, 130, 246, 0.2); color: #60a5fa; border: 1px solid rgba(59, 130, 246, 0.4); }
    .provider-badge.openai { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }
    .provider-badge.claude { background: rgba(168, 85, 247, 0.2); color: #c084fc; border: 1px solid rgba(168, 85, 247, 0.4); }
    .provider-badge.grok { background: rgba(249, 115, 22, 0.2); color: #fb923c; border: 1px solid rgba(249, 115, 22, 0.4); }

    .primary-indicator {
      font-size: 0.7rem;
      font-weight: 800;
      color: #fbbf24;
    }

    .card-balance-banner {
      display: flex;
      justify-content: space-between;
      align-items: center;
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.25);
      padding: 8px 12px;
      border-radius: 8px;
    }
    .banner-label { font-size: 0.72rem; color: #94a3b8; }
    .banner-usd { font-size: 1.15rem; font-weight: 800; color: #34d399; margin-left: 6px; }
    .banner-try { font-size: 0.85rem; font-weight: 700; color: #10b981; }

    .provider-desc {
      font-size: 0.78rem;
      color: #94a3b8;
      margin: 0;
      line-height: 1.4;
    }
    .tariff-hint { font-size: 0.72rem; color: #64748b; }

    .provider-meta {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 0.78rem;
      background: rgba(0, 0, 0, 0.2);
      padding: 8px 10px;
      border-radius: 6px;
    }
    .meta-item {
      display: flex;
      justify-content: space-between;
    }
    .m-label { color: #94a3b8; }
    .m-val { color: #f1f5f9; }

    .card-footer-action {
      display: flex;
      gap: 8px;
      margin-top: auto;
    }
    .card-btn-topup {
      flex: 1;
      text-align: center;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      text-decoration: none;
      padding: 6px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      transition: all 0.15s;
    }
    .card-btn-topup:hover {
      background: #10b981;
      color: #fff;
    }
    .btn-inspect-brand {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #cbd5e1;
      padding: 6px 10px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
    }
    .btn-inspect-brand:hover {
      background: rgba(255, 255, 255, 0.15);
      color: #fff;
    }

    /* TABLE SECTION */
    .history-section {
      background: #0d1527;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 20px;
    }
    .table-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 16px;
    }
    .table-title {
      font-size: 1.05rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .table-subtitle {
      font-size: 0.76rem;
      color: #94a3b8;
      margin: 3px 0 0 0;
    }
    .filter-select {
      background: #111a2e;
      color: #fff;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 6px;
      padding: 6px 12px;
      font-size: 0.8rem;
    }

    .table-responsive {
      overflow-x: auto;
    }
    .data-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8rem;
      text-align: left;
    }
    .data-table th {
      padding: 10px 12px;
      color: #94a3b8;
      font-weight: 700;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      font-size: 0.72rem;
      text-transform: uppercase;
      letter-spacing: 0.4px;
    }
    .data-table td {
      padding: 10px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #cbd5e1;
    }
    .cell-time { color: #94a3b8; }
    .cell-module { color: #f8fafc; }
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
    .badge.deepseek { background: rgba(239, 68, 68, 0.2); color: #f87171; }
    .badge.gemini { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
    .badge.openai { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .badge.claude { background: rgba(168, 85, 247, 0.2); color: #c084fc; }
    .badge.grok { background: rgba(249, 115, 22, 0.2); color: #fb923c; }

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
      padding: 24px;
      color: #64748b;
      font-style: italic;
    }
  `]
})
export class AiUsageDashboardComponent implements OnInit {
  etsyApi = inject(EtsyApiService);

  providerBalances: AiProviderBalanceDto[] = [];
  selectedBrand: string = 'ALL';
  selectedBrandInfo: AiProviderBalanceDto | null = null;
  activeProviderInfo: AiProviderBalanceDto | null = null;
  isLoadingBalances: boolean = false;

  totalTokens = 274700;
  promptTokens = 192000;
  completionTokens = 82700;
  totalCostUsd = 3.12;
  totalCalls = 318;

  history: UsageRecord[] = [
    {
      id: 1,
      createdAt: '2026-10-14T18:14:22Z',
      moduleName: 'FastListingCreator',
      provider: 'DeepSeek',
      modelName: 'DeepSeek-V4-Flash',
      promptTokens: 1200,
      completionTokens: 380,
      totalTokens: 1580,
      costUsd: 0.0004,
      costTry: 0.02,
      status: 'Başarılı'
    },
    {
      id: 2,
      createdAt: '2026-10-14T17:52:10Z',
      moduleName: 'AiAuditInspector',
      provider: 'Claude',
      modelName: 'claude-3-7-sonnet',
      promptTokens: 2400,
      completionTokens: 850,
      totalTokens: 3250,
      costUsd: 0.0182,
      costTry: 0.90,
      status: 'Başarılı'
    },
    {
      id: 3,
      createdAt: '2026-10-14T17:10:05Z',
      moduleName: 'CompetitorSpyRadar',
      provider: 'Google Gemini',
      modelName: 'gemini-2.5-flash',
      promptTokens: 1400,
      completionTokens: 420,
      totalTokens: 1820,
      costUsd: 0.0005,
      costTry: 0.025,
      status: 'Başarılı'
    },
    {
      id: 4,
      createdAt: '2026-10-14T16:30:19Z',
      moduleName: 'Viral3DModelHunter',
      provider: 'OpenAI',
      modelName: 'gpt-4o-mini',
      promptTokens: 1100,
      completionTokens: 380,
      totalTokens: 1480,
      costUsd: 0.0004,
      costTry: 0.02,
      status: 'Başarılı'
    }
  ];

  get exchangeRate(): number {
    return this.etsyApi.exchangeRate() || 49.38;
  }

  get displayTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalTokensUsed;
    }
    return this.providerBalances.length > 0
      ? this.providerBalances.reduce((sum, b) => sum + (b.totalTokensUsed || 0), 0)
      : this.totalTokens;
  }

  get displayPromptTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.promptTokensUsed;
    }
    return this.providerBalances.length > 0
      ? this.providerBalances.reduce((sum, b) => sum + (b.promptTokensUsed || 0), 0)
      : this.promptTokens;
  }

  get displayCompletionTokens(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.completionTokensUsed;
    }
    return this.providerBalances.length > 0
      ? this.providerBalances.reduce((sum, b) => sum + (b.completionTokensUsed || 0), 0)
      : this.completionTokens;
  }

  get displayCostUsd(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalCostUsd;
    }
    return this.providerBalances.length > 0
      ? this.providerBalances.reduce((sum, b) => sum + (b.totalCostUsd || 0), 0)
      : this.totalCostUsd;
  }

  get displayCostTry(): number {
    return Number((this.displayCostUsd * this.exchangeRate).toFixed(2));
  }

  get displayRemainingBalanceUsd(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalBalanceUsd;
    }
    return this.providerBalances.reduce((sum, b) => sum + (b.totalBalanceUsd || 0), 0);
  }

  get displayRemainingBalanceTry(): number {
    return Number((this.displayRemainingBalanceUsd * this.exchangeRate).toFixed(2));
  }

  get displayCalls(): number {
    if (this.selectedBrand !== 'ALL' && this.selectedBrandInfo) {
      return this.selectedBrandInfo.totalApiRequests;
    }
    return this.providerBalances.length > 0
      ? this.providerBalances.reduce((sum, b) => sum + (b.totalApiRequests || 0), 0)
      : this.totalCalls;
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

  loadData(): void {
    this.isLoadingBalances = true;
    this.etsyApi.getAiProviderBalances(undefined, this.exchangeRate).subscribe({
      next: (balances) => {
        this.isLoadingBalances = false;
        if (balances && balances.length > 0) {
          this.providerBalances = balances;
          this.activeProviderInfo = balances.find(b => b.isActiveProvider) || balances[0];
          this.updateSelectedBrandInfo();
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
        }
      }
    });
  }

  onBrandChange(): void {
    this.updateSelectedBrandInfo();
  }

  selectBrand(brand: string): void {
    this.selectedBrand = brand;
    this.updateSelectedBrandInfo();
  }

  private updateSelectedBrandInfo(): void {
    if (this.selectedBrand === 'ALL') {
      this.selectedBrandInfo = null;
    } else {
      this.selectedBrandInfo = this.providerBalances.find(b => b.provider === this.selectedBrand) || null;
    }
  }

  getBrandIcon(provider: string): string {
    const p = provider.toLowerCase();
    if (p.includes('deepseek')) return '🔴';
    if (p.includes('gemini') || p.includes('google')) return '🔵';
    if (p.includes('openai') || p.includes('gpt')) return '🟢';
    if (p.includes('claude') || p.includes('anthropic')) return '🟣';
    if (p.includes('grok')) return '🟠';
    return '⚡';
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
