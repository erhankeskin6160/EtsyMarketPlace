import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { HttpClient } from '@angular/common/http';
import { ActivatedRoute, Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

@Component({
  selector: 'app-etsy-api-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="settings-view">
      <!-- HEADER -->
      <div class="settings-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="12" cy="12" r="3"></circle>
              <path d="M19.4 15a1.65 1.65 0 0 0 .33 1.82l.06.06a2 2 0 0 1 0 2.83 2 2 0 0 1-2.83 0l-.06-.06a1.65 1.65 0 0 0-1.82-.33 1.65 1.65 0 0 0-1 1.51V21a2 2 0 0 1-2 2 2 2 0 0 1-2-2v-.09A1.65 1.65 0 0 0 9 19.4a1.65 1.65 0 0 0-1.82.33l-.06.06a2 2 0 0 1-2.83 0 2 2 0 0 1 0-2.83l.06-.06a1.65 1.65 0 0 0 .33-1.82 1.65 1.65 0 0 0-1.51-1H3a2 2 0 0 1-2-2 2 2 0 0 1 2-2h.09A1.65 1.65 0 0 0 4.6 9a1.65 1.65 0 0 0-.33-1.82l-.06-.06a2 2 0 0 1 0-2.83 2 2 0 0 1 2.83 0l.06.06a1.65 1.65 0 0 0 1.82.33H9a1.65 1.65 0 0 0 1-1.51V3a2 2 0 0 1 2-2 2 2 0 0 1 2 2v.09a1.65 1.65 0 0 0 1 1.51 1.65 1.65 0 0 0 1.82-.33l.06-.06a2 2 0 0 1 2.83 0 2 2 0 0 1 0 2.83l-.06.06a1.65 1.65 0 0 0-.33 1.82V9a1.65 1.65 0 0 0 1.51 1H21a2 2 0 0 1 2 2 2 2 0 0 1-2 2h-.09a1.65 1.65 0 0 0-1.51 1z"></path>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Etsy API & Canlı Mağaza Bağlantı Ayarları</h1>
            <p class="page-subtitle">Etsy OAuth v3 token sağlığı, doğrudan Etsy bağlantısı ve canlı döviz kuru motoru</p>
          </div>
        </div>

        <div class="header-badge">
          <span>● VDS API: 5.180.81.148:5263 (Online)</span>
        </div>
      </div>

      <!-- MAIN CARDS -->
      <div class="settings-grid">
        
        <!-- CARD 1: STORE CONNECTION & TOKEN HEALTH -->
        <div class="glass-card">
          <div class="card-head">
            <span class="card-label">1. Etsy Mağaza Bağlantısı & OAuth Token Sağlığı</span>
            <span class="status-pill" [ngClass]="etsyApi.tokenStatus()">
              {{ etsyApi.tokenStatus() === 'connected' ? 'BAĞLI & AKTİF' : (etsyApi.tokenStatus() === 'expired' ? 'SÜRESİ DOLDU' : 'BAĞLI DEĞİL') }}
            </span>
          </div>

          <div class="form-group">
            <label>Aktif Etsy Mağaza ID</label>
            <div class="input-with-btn">
              <input type="text" [(ngModel)]="currentShopId" class="form-input font-bold" />
              <button class="btn-primary" (click)="saveShopId()">Kaydet</button>
            </div>
          </div>

          <div class="token-health-box">
            <div class="health-row">
              <span class="h-label">OAuth v3 Token Durumu:</span>
              <span class="h-val" [ngClass]="etsyApi.tokenStatus() === 'connected' ? 'green' : 'amber'">
                {{ etsyApi.tokenDetails() }}
              </span>
            </div>
            <div class="health-row">
              <span class="h-label">Son Kullanma Zamanı:</span>
              <span class="h-val text-muted">
                {{ etsyApi.tokenExpiresAt() ? (etsyApi.tokenExpiresAt() | date:'medium') : 'Belirtilmedi' }}
              </span>
            </div>
            <div class="health-row">
              <span class="h-label">Otomatik Yenileme (Auto-Refresh):</span>
              <span class="h-val green">EtsyAccessTokenHandler Devrede (90 Gün)</span>
            </div>
            <div class="health-row">
              <span class="h-label">Erişim İzinleri (Scopes):</span>
              <span class="h-val text-muted">listings_r, listings_w, transactions_r, shops_r, billing_r</span>
            </div>
          </div>

          <!-- DIRECT OAUTH CONNECT BUTTON -->
          <div class="direct-connect-banner">
            <div class="connect-info">
              <div class="connect-title">🔑 1-Tıkla Doğrudan Etsy ile Yetkilendir (PKCE)</div>
              <div class="connect-sub">Etsy resmi onay ekranını açar, izin verildiğinde yeni Access & Refresh token çiftini otomatik sisteme kaydeder.</div>
            </div>
            <button class="btn-direct-connect" [disabled]="isConnecting" (click)="connectWithEtsy()">
              {{ isConnecting ? 'Bağlanıyor...' : 'Etsy ile Hesabımı Doğrudan Bağla' }}
            </button>
          </div>

          <div class="btn-actions-row">
            <button class="btn-sync-token" [disabled]="isTestingToken" (click)="testEtsyToken()">
              🔍 Durumu Sorgula
            </button>
            <button class="btn-refresh-token" [disabled]="isRefreshingToken" (click)="tryRefreshToken()">
              ⚡ Token'ı Şimdi Yenile
            </button>
            <button class="btn-toggle-manual" (click)="showManualCodeModal = !showManualCodeModal">
              📋 Yetki Kodu Yapıştır
            </button>
            <a class="btn-connect-etsy" href="https://developers.etsy.com/documentation/essentials/authentication" target="_blank">
              📖 Etsy Kılavuzu
            </a>
          </div>

          <!-- MANUAL CODE EXCHANGE BOX -->
          <div *ngIf="showManualCodeModal" class="manual-code-box">
            <div class="manual-code-head">
              <span>Etsy'den Dönen URL'yi veya Yetki Kodunu (code=...) Girin:</span>
              <button class="btn-close-sm" (click)="showManualCodeModal = false">✕</button>
            </div>
            <div class="input-with-btn">
              <input type="text" [(ngModel)]="manualAuthCode" placeholder="https://.../?code=abcd1234... veya doğrudan kod" class="form-input font-mono" />
              <button class="btn-primary" [disabled]="!manualAuthCode.trim() || isExchangingCode" (click)="submitManualCode()">
                {{ isExchangingCode ? 'Doğrulanıyor...' : 'Token Al ve Kaydet' }}
              </button>
            </div>
          </div>
        </div>

        <!-- CARD 2: LIVE USD/TRY EXCHANGE RATE API -->
        <div class="glass-card">
          <div class="card-head">
            <span class="card-label">2. Canlı USD/TRY Kur Sağlayıcısı</span>
            <span class="status-pill live">CANLI API</span>
          </div>

          <p class="card-desc">
            Sistemdeki tüm finansal hesaplamalar, sipariş kârları ve muhasebe defteri bu canlı kur üzerinden gerçek zamanlı hesaplanır.
          </p>

          <div class="rate-display-box">
            <span class="rate-large">1 USD = {{ etsyApi.exchangeRate() }} ₺ TRY</span>
            <span class="rate-source">Kaynak: open.er-api.com (Yedek: api.frankfurter.app)</span>
          </div>

          <div class="form-group">
            <label>Manuel Kur Güncelle (İsteğe Bağlı)</label>
            <div class="input-with-btn">
              <input type="number" step="0.01" [(ngModel)]="manualRate" class="form-input" />
              <button class="btn-primary" (click)="applyManualRate()">Uygula</button>
            </div>
          </div>

          <button class="btn-fetch-live" (click)="fetchFreshRate()">
            💱 Canlı Kuru Merkezden Yeniden Çek
          </button>
        </div>

        <!-- CARD 3: ETSY DEVELOPER API CREDENTIALS -->
        <div class="glass-card full-width">
          <div class="card-head">
            <span class="card-label">3. Etsy Developer Uygulama Anahtarları (Keystring & Secret)</span>
            <span class="status-pill active-pill">VDS SQLite Şifreli Kasa</span>
          </div>

          <p class="card-desc">
            Etsy OpenAPI v3 ile doğrudan bağlantı kurmak ve token yenilemek için Etsy Developer Portal'ınızdan aldığınız uygulama anahtarlarıdır. Bu veriler VDS veritabanında AES-256 (DataProtection) ile korunur.
          </p>

          <div class="credentials-grid">
            <div class="form-group">
              <label>Etsy App Keystring (Client ID)</label>
              <input type="text" [(ngModel)]="keystring" class="form-input font-mono" placeholder="7k7h5b6g9ks6m0dx8tgl7vcn" />
            </div>

            <div class="form-group">
              <label>Etsy Shared Secret</label>
              <div class="secret-wrapper">
                <input [type]="showSecret ? 'text' : 'password'" [(ngModel)]="sharedSecret" class="form-input font-mono" placeholder="ho2tfkzko9" />
                <button type="button" class="btn-eye" (click)="showSecret = !showSecret">
                  {{ showSecret ? '👁️' : '🔒' }}
                </button>
              </div>
            </div>

            <div class="form-group">
              <label>Callback / Redirect URI</label>
              <input type="text" [(ngModel)]="redirectUri" class="form-input font-mono" placeholder="http://localhost:4200/settings/etsy-api" />
              <small class="hint-text">Etsy Developer portalınızdaki uygulamanızın 'Redirect URIs' listesi ile birebir uyuşmalıdır.</small>
            </div>
          </div>

          <div class="credentials-actions">
            <button class="btn-primary-lg" [disabled]="isSavingCredentials" (click)="saveCredentials()">
              {{ isSavingCredentials ? 'Kaydediliyor...' : '💾 API Anahtarlarını Güvenli Kaydet' }}
            </button>
            <a href="https://developers.etsy.com/your-apps" target="_blank" class="btn-secondary-link">
              ↗ Etsy Developer Portalına Git (Uygulamalarım)
            </a>
          </div>
        </div>

      </div>

      <!-- TOAST -->
      <div *ngIf="toastMessage" class="toast-card" [ngClass]="toastType">
        {{ toastMessage }}
      </div>
    </div>
  `,
  styles: [`
    .settings-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .settings-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(168, 85, 247, 0.2));
      border: 1px solid rgba(99, 102, 241, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #818cf8;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 2px 0 0;
    }
    .header-badge span {
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.3);
      color: #34d399;
      padding: 6px 14px;
      border-radius: 20px;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .settings-grid {
      display: grid;
      grid-template-columns: 1.15fr 0.85fr;
      gap: 20px;
    }
    .full-width {
      grid-column: 1 / -1;
    }
    .glass-card {
      background: rgba(15, 23, 42, 0.7);
      backdrop-filter: blur(12px);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 16px;
      padding: 24px;
      display: flex;
      flex-direction: column;
      gap: 18px;
    }
    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .card-label {
      font-size: 0.95rem;
      font-weight: 700;
      color: #f1f5f9;
      letter-spacing: 0.01em;
    }
    .status-pill {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 4px 10px;
      border-radius: 20px;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }
    .status-pill.connected {
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
      border: 1px solid rgba(16, 185, 129, 0.4);
    }
    .status-pill.expired {
      background: rgba(245, 158, 11, 0.15);
      color: #fbbf24;
      border: 1px solid rgba(245, 158, 11, 0.4);
    }
    .status-pill.missing, .status-pill.error {
      background: rgba(239, 68, 68, 0.15);
      color: #f87171;
      border: 1px solid rgba(239, 68, 68, 0.4);
    }
    .status-pill.live {
      background: rgba(59, 130, 246, 0.15);
      color: #60a5fa;
      border: 1px solid rgba(59, 130, 246, 0.3);
    }
    .status-pill.active-pill {
      background: rgba(139, 92, 246, 0.15);
      color: #a78bfa;
      border: 1px solid rgba(139, 92, 246, 0.3);
    }
    .card-desc {
      font-size: 0.84rem;
      color: #94a3b8;
      line-height: 1.5;
      margin: 0;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .form-group label {
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
    }
    .font-mono {
      font-family: monospace;
      letter-spacing: 0.02em;
    }
    .input-with-btn {
      display: flex;
      gap: 8px;
    }
    .form-input {
      flex: 1;
      background: rgba(2, 6, 23, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 9px 14px;
      color: #f8fafc;
      font-size: 0.88rem;
      outline: none;
      transition: all 0.2s;
    }
    .form-input:focus {
      border-color: #6366f1;
      box-shadow: 0 0 0 2px rgba(99, 102, 241, 0.25);
    }
    .secret-wrapper {
      display: flex;
      position: relative;
    }
    .secret-wrapper .form-input {
      padding-right: 44px;
    }
    .btn-eye {
      position: absolute;
      right: 8px;
      top: 50%;
      transform: translateY(-50%);
      background: none;
      border: none;
      color: #94a3b8;
      cursor: pointer;
      font-size: 1rem;
      padding: 4px;
    }
    .hint-text {
      font-size: 0.74rem;
      color: #64748b;
      margin-top: 2px;
    }
    .btn-primary {
      background: linear-gradient(135deg, #4f46e5, #6366f1);
      color: white;
      border: none;
      padding: 9px 18px;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
      white-space: nowrap;
    }
    .btn-primary:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .btn-primary:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
    .token-health-box {
      background: rgba(2, 6, 23, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 10px;
      padding: 14px 16px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .health-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.82rem;
    }
    .h-label {
      color: #94a3b8;
    }
    .h-val.green {
      color: #34d399;
      font-weight: 600;
    }
    .h-val.amber {
      color: #fbbf24;
      font-weight: 600;
    }
    .text-muted {
      color: #cbd5e1;
    }

    /* DIRECT CONNECT BANNER */
    .direct-connect-banner {
      background: linear-gradient(135deg, rgba(234, 88, 12, 0.12), rgba(99, 102, 241, 0.12));
      border: 1px solid rgba(234, 88, 12, 0.35);
      border-radius: 12px;
      padding: 14px 16px;
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 16px;
    }
    .connect-title {
      font-size: 0.92rem;
      font-weight: 700;
      color: #fb923c;
    }
    .connect-sub {
      font-size: 0.77rem;
      color: #cbd5e1;
      margin-top: 3px;
    }
    .btn-direct-connect {
      background: linear-gradient(135deg, #f97316, #ea580c);
      color: white;
      border: none;
      padding: 10px 20px;
      border-radius: 8px;
      font-size: 0.86rem;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
      box-shadow: 0 4px 14px rgba(234, 88, 12, 0.35);
      transition: all 0.2s;
    }
    .btn-direct-connect:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-2px);
      box-shadow: 0 6px 20px rgba(234, 88, 12, 0.5);
    }

    .btn-actions-row {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .btn-sync-token {
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #e2e8f0;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-sync-token:hover:not(:disabled) {
      background: rgba(51, 65, 85, 0.8);
    }
    .btn-refresh-token {
      background: linear-gradient(135deg, #0284c7, #0ea5e9);
      border: none;
      color: white;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-refresh-token:hover:not(:disabled) {
      filter: brightness(1.1);
    }
    .btn-toggle-manual {
      background: rgba(147, 51, 234, 0.2);
      border: 1px solid rgba(147, 51, 234, 0.4);
      color: #c084fc;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-connect-etsy {
      background: rgba(245, 158, 11, 0.1);
      border: 1px solid rgba(245, 158, 11, 0.3);
      color: #fbbf24;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      text-decoration: none;
      display: inline-flex;
      align-items: center;
      transition: all 0.2s;
    }
    .manual-code-box {
      background: rgba(15, 23, 42, 0.9);
      border: 1px solid rgba(147, 51, 234, 0.4);
      border-radius: 10px;
      padding: 14px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      animation: fadeIn 0.25s ease;
    }
    .manual-code-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.8rem;
      color: #c084fc;
      font-weight: 600;
    }
    .btn-close-sm {
      background: none;
      border: none;
      color: #94a3b8;
      cursor: pointer;
      font-size: 0.9rem;
    }

    /* CARD 2: EXCHANGE RATE */
    .rate-display-box {
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.08), rgba(59, 130, 246, 0.08));
      border: 1px solid rgba(16, 185, 129, 0.25);
      border-radius: 12px;
      padding: 18px;
      text-align: center;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .rate-large {
      font-size: 1.5rem;
      font-weight: 800;
      color: #34d399;
      letter-spacing: -0.01em;
    }
    .rate-source {
      font-size: 0.72rem;
      color: #64748b;
    }
    .btn-fetch-live {
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #cbd5e1;
      padding: 10px;
      border-radius: 8px;
      font-size: 0.82rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }

    /* CARD 3: CREDENTIALS */
    .credentials-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
    }
    .credentials-actions {
      display: flex;
      align-items: center;
      gap: 16px;
      margin-top: 6px;
    }
    .btn-primary-lg {
      background: linear-gradient(135deg, #6366f1, #4f46e5);
      color: white;
      border: none;
      padding: 11px 24px;
      border-radius: 8px;
      font-size: 0.88rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
      box-shadow: 0 4px 14px rgba(99, 102, 241, 0.35);
    }
    .btn-primary-lg:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .btn-secondary-link {
      font-size: 0.82rem;
      color: #818cf8;
      text-decoration: none;
      font-weight: 600;
    }
    .btn-secondary-link:hover {
      text-decoration: underline;
    }

    /* TOAST */
    .toast-card {
      position: fixed;
      bottom: 24px;
      right: 24px;
      padding: 12px 20px;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.4);
      animation: fadeIn 0.3s ease;
      z-index: 9999;
      max-width: 480px;
    }
    .toast-card.success {
      background: rgba(6, 78, 59, 0.95);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
    }
    .toast-card.warning {
      background: rgba(120, 53, 15, 0.95);
      border: 1px solid rgba(245, 158, 11, 0.4);
      color: #fde68a;
    }
    .toast-card.error {
      background: rgba(127, 29, 29, 0.95);
      border: 1px solid rgba(239, 68, 68, 0.4);
      color: #fca5a5;
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (max-width: 1024px) {
      .settings-grid { grid-template-columns: 1fr; }
      .credentials-grid { grid-template-columns: 1fr; }
      .direct-connect-banner { flex-direction: column; align-items: flex-start; }
    }
  `]
})
export class EtsyApiSettingsComponent implements OnInit {
  currentShopId = '53236321';
  manualRate = 49.12;

  // Etsy Developer Credentials
  keystring = '7k7h5b6g9ks6m0dx8tgl7vcn';
  sharedSecret = 'ho2tfkzko9';
  redirectUri = 'http://localhost:4200/settings/etsy-api';
  showSecret = false;
  isSavingCredentials = false;

  // Direct OAuth & PKCE
  isConnecting = false;
  isTestingToken = false;
  isRefreshingToken = false;
  isExchangingCode = false;

  // Manual code paste
  showManualCodeModal = false;
  manualAuthCode = '';

  // Toast
  toastMessage = '';
  toastType: 'success' | 'warning' | 'error' = 'success';

  constructor(
    public etsyApi: EtsyApiService,
    private http: HttpClient,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.currentShopId = this.etsyApi.activeShopId();
    this.manualRate = this.etsyApi.exchangeRate();

    // Default redirectUri based on current window origin
    if (typeof window !== 'undefined' && window.location?.origin) {
      this.redirectUri = `${window.location.origin}/settings/etsy-api`;
    }

    // Load saved credentials from VDS SQLite
    this.loadSavedCredentials();

    // Check if redirected from Etsy with ?code=...
    this.route.queryParams.subscribe(params => {
      const code = params['code'];
      const state = params['state'];
      if (code) {
        this.showToast('Etsy yetkilendirme kodu algılandı! Token takası yapılıyor...', 'warning');
        // Clean URL to prevent re-triggering
        this.router.navigate([], { queryParams: {}, replaceUrl: true });
        this.performExchange(code, state);
      }
    });
  }

  loadSavedCredentials(): void {
    this.etsyApi.getEtsyCredentials(this.currentShopId).subscribe({
      next: (res) => {
        if (res) {
          if (res.keystringMasked && !res.keystringMasked.includes('****')) {
            this.keystring = res.keystringMasked;
          }
          if (res.secretMasked && !res.secretMasked.includes('****')) {
            this.sharedSecret = res.secretMasked;
          }
          if (res.redirectUri) {
            this.redirectUri = res.redirectUri;
          }
        }
      },
      error: () => {
        // Fallback to defaults already set
      }
    });
  }

  saveCredentials(): void {
    if (!this.keystring.trim()) {
      this.showToast('Lütfen geçerli bir Keystring (Client ID) girin.', 'error');
      return;
    }

    this.isSavingCredentials = true;
    this.etsyApi.saveEtsyCredentials({
      shopId: this.currentShopId,
      keystring: this.keystring.trim(),
      sharedSecret: this.sharedSecret.trim(),
      redirectUri: this.redirectUri.trim()
    }).subscribe({
      next: () => {
        this.isSavingCredentials = false;
        this.showToast('✅ Etsy Developer API anahtarları VDS kasasına güvenle kaydedildi!', 'success');
      },
      error: (err) => {
        this.isSavingCredentials = false;
        this.showToast('⚠️ Anahtarlar kaydedilemedi: ' + (err.error?.error || err.message), 'error');
      }
    });
  }

  connectWithEtsy(): void {
    if (!this.keystring.trim()) {
      this.showToast('Önce Etsy Keystring bilgisini kaydedin.', 'error');
      return;
    }

    this.isConnecting = true;
    this.etsyApi.getEtsyConnectUrl(this.currentShopId, this.redirectUri).subscribe({
      next: (res) => {
        this.isConnecting = false;
        this.showToast('Etsy resmi yetkilendirme sayfasına yönlendiriliyorsunuz...', 'success');
        // Open authorization in new tab/window so user stays logged in
        window.open(res.url, '_blank');
        this.showManualCodeModal = true;
      },
      error: (err) => {
        this.isConnecting = false;
        this.showToast('OAuth bağlantı URL\'i üretilemedi: ' + (err.error?.error || err.message), 'error');
      }
    });
  }

  submitManualCode(): void {
    if (!this.manualAuthCode.trim()) return;

    let code = this.manualAuthCode.trim();
    let state = '';

    // If user pasted full callback URL
    if (code.includes('code=')) {
      try {
        const urlObj = new URL(code.startsWith('http') ? code : `http://dummy.com/${code}`);
        code = urlObj.searchParams.get('code') || code;
        state = urlObj.searchParams.get('state') || '';
      } catch {
        const m = code.match(/code=([^&]+)/);
        if (m) code = m[1];
      }
    }

    this.performExchange(code, state);
  }

  private performExchange(code: string, state?: string): void {
    this.isExchangingCode = true;
    this.etsyApi.exchangeEtsyCode({
      shopId: this.currentShopId,
      code,
      state: state || undefined,
      redirectUri: this.redirectUri
    }).subscribe({
      next: (res) => {
        this.isExchangingCode = false;
        this.showManualCodeModal = false;
        this.manualAuthCode = '';
        this.etsyApi.verifyApiConnection();
        this.showToast('🎉 ' + (res.message || 'Etsy OAuth v3 bağlantısı başarıyla tamamlandı!'), 'success');
      },
      error: (err) => {
        this.isExchangingCode = false;
        const msg = err.error?.message || 'Yetki kodu takası başarısız oldu.';
        this.showToast('⚠️ ' + msg, 'error');
      }
    });
  }

  saveShopId(): void {
    if (this.currentShopId.trim()) {
      this.etsyApi.activeShopId.set(this.currentShopId.trim());
      this.etsyApi.verifyApiConnection();
      this.loadSavedCredentials();
      this.showToast(`Aktif mağaza #${this.currentShopId} olarak ayarlandı.`, 'success');
    }
  }

  applyManualRate(): void {
    if (this.manualRate > 0) {
      this.etsyApi.setExchangeRate(this.manualRate);
      this.showToast(`Döviz kuru 1 USD = ${this.manualRate} ₺ olarak güncellendi!`, 'success');
    }
  }

  fetchFreshRate(): void {
    this.etsyApi.fetchLiveExchangeRate();
    this.showToast('Canlı kur merkezden başarıyla yenilendi!', 'success');
    setTimeout(() => {
      this.manualRate = this.etsyApi.exchangeRate();
    }, 2500);
  }

  testEtsyToken(): void {
    this.isTestingToken = true;
    this.etsyApi.checkTokenStatus(this.currentShopId).subscribe({
      next: (res) => {
        this.isTestingToken = false;
        this.etsyApi.verifyApiConnection();
        if (res.exists && !res.isExpired) {
          this.showToast(`✅ Etsy OAuth v3 bağlantısı aktif ve geçerli! Mağaza: ${res.shopId}`, 'success');
        } else if (res.exists && res.isExpired) {
          this.showToast(`⚠️ Etsy OAuth v3 token süresi dolmuş. Lütfen "Token'ı Şimdi Yenile" veya "Etsy ile Hesabımı Doğrudan Bağla" butonunu kullanın.`, 'warning');
        } else {
          this.showToast(`❌ Mağaza #${this.currentShopId} için kayıtlı bir Etsy token bulunamadı.`, 'error');
        }
      },
      error: () => {
        this.isTestingToken = false;
        this.showToast('⚠️ VDS API sunucusuna erişilemedi veya bağlantı kurulamadı.', 'error');
      }
    });
  }

  tryRefreshToken(): void {
    this.isRefreshingToken = true;
    this.etsyApi.refreshToken(this.currentShopId).subscribe({
      next: (res) => {
        this.isRefreshingToken = false;
        this.etsyApi.verifyApiConnection();
        this.showToast(`✅ ${res.message || 'Etsy OAuth v3 token başarıyla yenilendi!'}`, 'success');
      },
      error: (err) => {
        this.isRefreshingToken = false;
        this.etsyApi.verifyApiConnection();
        const msg = err.error?.message || 'Etsy API yetkilendirme yenilemesi başarısız oldu.';
        this.showToast(`⚠️ ${msg} Lütfen "Etsy ile Hesabımı Doğrudan Bağla" butonuna tıklayarak taze yetki verin.`, 'warning');
      }
    });
  }

  private showToast(msg: string, type: 'success' | 'warning' | 'error'): void {
    this.toastMessage = msg;
    this.toastType = type;
    setTimeout(() => {
      if (this.toastMessage === msg) this.toastMessage = '';
    }, 6000);
  }
}
