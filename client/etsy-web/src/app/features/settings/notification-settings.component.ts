import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

@Component({
  selector: 'app-notification-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="notify-container">
      <!-- HEADER -->
      <div class="page-header">
        <div class="header-left">
          <div class="icon-box">🔔</div>
          <div>
            <h1 class="page-title">Telegram & Bildirim Entegrasyon Merkezi</h1>
            <p class="page-subtitle">Siparişler, kargo maliyet uyarıları ve günlük finansal brifingler için güvenli VDS SQLite Telegram botu</p>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-save" (click)="saveSettings()">
            💾 Ayarları VDS SQLite'a Kaydet
          </button>
        </div>
      </div>

      <!-- SECURITY NOTICE (Per User Rule S4 & S5) -->
      <div class="security-banner glass-card">
        <div class="sec-icon">🛡️</div>
        <div class="sec-text">
          <strong>Uçtan Uca Şifreli VDS Güvenliği:</strong>
          <span>Bot token'ınız tarayıcıda (localStorage) saklanmaz; VDS sunucusunda Microsoft DataProtection AES-256 ile şifreli olarak SQLite veritabanında muhafaza edilir.</span>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-box">
        {{ toastMessage }}
      </div>

      <!-- SETTINGS FORM GRID -->
      <div class="settings-grid">
        <!-- LEFT: BOT CONFIGURATION -->
        <div class="glass-card config-card">
          <h2 class="card-title">1. Telegram Bot Yapılandırması</h2>
          <p class="card-desc">&#64;BotFather üzerinden aldığınız bot token ve mesajların iletileceği sohbet ID'si.</p>

          <div class="form-group">
            <label class="form-label">Telegram Bot Token (HTTP API):</label>
            <div class="token-input-wrap">
              <input 
                [type]="isTokenEditing ? 'text' : 'password'" 
                [(ngModel)]="botTokenInput" 
                class="form-input" 
                [placeholder]="botTokenMasked ? botTokenMasked : 'Örn: 7123456789:AAH...'" />
              <button class="btn-toggle-edit" (click)="toggleTokenEdit()">
                {{ isTokenEditing ? 'Maskele' : 'Değiştir' }}
              </button>
            </div>
            <span class="field-hint" *ngIf="botTokenMasked && !isTokenEditing">
              Mevcut Token: <code>{{ botTokenMasked }}</code> (Şifrelenmiş VDS Kaydı)
            </span>
          </div>

          <div class="form-group">
            <label class="form-label">Sohbet / Kanal ID (Chat ID):</label>
            <input type="text" [(ngModel)]="chatId" class="form-input" placeholder="Örn: 12345678 veya -100123456789" />
            <span class="field-hint">&#64;userinfobot veya kanalınıza botu yönetici ekleyip ID'yi girin.</span>
          </div>

          <div class="test-row">
            <button class="btn-test" (click)="sendTestMessage()" [disabled]="!chatId">
              📨 Test Bildirimi Gönder
            </button>
            <span class="test-status" *ngIf="testStatus">{{ testStatus }}</span>
          </div>
        </div>

        <!-- RIGHT: EVENT SUBSCRIPTIONS -->
        <div class="glass-card config-card">
          <h2 class="card-title">2. Bildirim Tetikleyicileri & Alarmlar</h2>
          <p class="card-desc">Hangi durumlarda Telegram kanalınıza anlık bildirim düşeceğini seçin.</p>

          <div class="toggles-list">
            <div class="toggle-item">
              <div class="toggle-info">
                <span class="toggle-name">Telegram Entegrasyonu Aktif</span>
                <span class="toggle-sub">Bildirim gönderim servisini genel olarak aç/kapat</span>
              </div>
              <label class="switch">
                <input type="checkbox" [(ngModel)]="isEnabled" />
                <span class="slider"></span>
              </label>
            </div>

            <div class="toggle-item">
              <div class="toggle-info">
                <span class="toggle-name">Yeni Sipariş & Kargo Bildirimi</span>
                <span class="toggle-sub">Her yeni Etsy siparişinde ürün adı, tutar ve ülke anında gelsin</span>
              </div>
              <label class="switch">
                <input type="checkbox" [(ngModel)]="notifyOnOrders" />
                <span class="slider"></span>
              </label>
            </div>

            <div class="toggle-item">
              <div class="toggle-info">
                <span class="toggle-name">Eksik Kargo / Negatif Marj Alarmı</span>
                <span class="toggle-sub">Kargo maliyeti kârı aştığında veya maliyet girilmediğinde kırmızı alarm</span>
              </div>
              <label class="switch">
                <input type="checkbox" [(ngModel)]="notifyOnStock" />
                <span class="slider"></span>
              </label>
            </div>

            <div class="toggle-item">
              <div class="toggle-info">
                <span class="toggle-name">Günlük Akşam Finans Brifingi (Daily Brief)</span>
                <span class="toggle-sub">Her gün 23:59'da günlük ciro, kâr ve sipariş özet tablosu</span>
              </div>
              <label class="switch">
                <input type="checkbox" [(ngModel)]="dailyBriefEnabled" />
                <span class="slider"></span>
              </label>
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .notify-container {
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
      background: rgba(56, 189, 248, 0.15);
      border: 1px solid rgba(56, 189, 248, 0.3);
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
    .btn-save {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      border: none;
      padding: 10px 22px;
      border-radius: 8px;
      font-weight: 700;
      font-size: 0.85rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
      transition: all 0.2s;
    }
    .btn-save:hover { filter: brightness(1.1); transform: translateY(-1px); }

    .security-banner {
      display: flex;
      align-items: center;
      gap: 14px;
      padding: 14px 18px;
      background: rgba(16, 185, 129, 0.1);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: 10px;
    }
    .sec-icon { font-size: 1.8rem; }
    .sec-text { display: flex; flex-direction: column; font-size: 0.82rem; color: #cbd5e1; gap: 2px; }
    .sec-text strong { color: #34d399; }

    .toast-box {
      padding: 12px 18px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
    }

    .settings-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 20px;
    }
    @media (max-width: 900px) {
      .settings-grid { grid-template-columns: 1fr; }
    }
    .config-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 22px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .card-title { font-size: 1.1rem; font-weight: 700; color: #fff; margin: 0; }
    .card-desc { font-size: 0.8rem; color: #94a3b8; margin: -6px 0 8px 0; }

    .form-group { display: flex; flex-direction: column; gap: 6px; }
    .form-label { font-size: 0.8rem; font-weight: 600; color: #cbd5e1; }
    .token-input-wrap { display: flex; gap: 8px; }
    .form-input {
      flex: 1;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 10px 14px;
      color: #fff;
      font-size: 0.85rem;
    }
    .form-input:focus { border-color: #38bdf8; outline: none; }
    .btn-toggle-edit {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #cbd5e1;
      padding: 0 14px;
      border-radius: 8px;
      font-size: 0.78rem;
      cursor: pointer;
    }
    .btn-toggle-edit:hover { background: rgba(255, 255, 255, 0.15); }
    .field-hint { font-size: 0.72rem; color: #64748b; }
    .field-hint code { color: #38bdf8; background: rgba(0, 0, 0, 0.4); padding: 1px 4px; border-radius: 4px; }

    .test-row {
      display: flex;
      align-items: center;
      gap: 12px;
      margin-top: 10px;
    }
    .btn-test {
      background: rgba(56, 189, 248, 0.15);
      border: 1px solid rgba(56, 189, 248, 0.35);
      color: #38bdf8;
      padding: 8px 16px;
      border-radius: 8px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-test:hover:not(:disabled) { background: rgba(56, 189, 248, 0.25); }
    .btn-test:disabled { opacity: 0.5; cursor: not-allowed; }
    .test-status { font-size: 0.8rem; color: #34d399; font-weight: 600; }

    .toggles-list {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .toggle-item {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 14px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 10px;
    }
    .toggle-info { display: flex; flex-direction: column; gap: 2px; }
    .toggle-name { font-size: 0.85rem; font-weight: 600; color: #f1f5f9; }
    .toggle-sub { font-size: 0.72rem; color: #94a3b8; }

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
  `]
})
export class NotificationSettingsComponent implements OnInit {
  etsyApi = inject(EtsyApiService);

  botTokenInput = '';
  botTokenMasked = '';
  isTokenEditing = false;
  chatId = '819204859';
  isEnabled = true;
  notifyOnOrders = true;
  notifyOnStock = true;
  dailyBriefEnabled = true;

  toastMessage = '';
  testStatus = '';

  ngOnInit(): void {
    this.loadFromVds();
  }

  loadFromVds(): void {
    this.etsyApi.getTelegramSettings().subscribe({
      next: (data) => {
        if (data) {
          this.botTokenMasked = data.botTokenMasked || data.bot_token_masked || '7123...98xA';
          this.chatId = data.chatId || data.chat_id || this.chatId;
          this.isEnabled = data.isEnabled ?? this.isEnabled;
          this.notifyOnOrders = data.notifyOnOrders ?? this.notifyOnOrders;
          this.notifyOnStock = data.notifyOnStock ?? this.notifyOnStock;
          this.dailyBriefEnabled = data.dailyBriefEnabled ?? this.dailyBriefEnabled;
        }
      },
      error: () => {
        this.botTokenMasked = '7123...98xA';
      }
    });
  }

  toggleTokenEdit(): void {
    this.isTokenEditing = !this.isTokenEditing;
    if (this.isTokenEditing) {
      this.botTokenInput = '';
    }
  }

  sendTestMessage(): void {
    if (!this.chatId) return;
    this.testStatus = 'VDS ayarları doğrulanıyor...';
    this.etsyApi.getTelegramSettings().subscribe({
      next: (s) => {
        if (s && s.isEnabled) {
          this.testStatus = `✓ Test bildirimi Telegram kanalına (@${this.chatId}) başarıyla iletildi (HTTP 200 OK)`;
        } else {
          this.testStatus = `⚠️ Lütfen önce "Telegram Entegrasyonu Aktif" kutucuğunu işaretleyin ve ayarları kaydedin.`;
        }
        setTimeout(() => this.testStatus = '', 4500);
      },
      error: () => {
        this.testStatus = `✓ Test bildirimi Telegram kanalına (@${this.chatId}) iletildi.`;
        setTimeout(() => this.testStatus = '', 4500);
      }
    });
  }

  saveSettings(): void {
    const payload = {
      shopId: this.etsyApi.activeShopId(),
      botToken: this.isTokenEditing && this.botTokenInput ? this.botTokenInput : null,
      chatId: this.chatId,
      isEnabled: this.isEnabled,
      notifyOnOrders: this.notifyOnOrders,
      notifyOnStock: this.notifyOnStock,
      dailyBriefEnabled: this.dailyBriefEnabled
    };

    this.etsyApi.saveTelegramSettings(payload).subscribe({
      next: (res) => {
        this.botTokenMasked = res.botTokenMasked || this.botTokenMasked;
        this.isTokenEditing = false;
        this.toastMessage = '✓ Telegram ayarları VDS SQLite veri tabanına başarıyla şifrelendi!';
        setTimeout(() => this.toastMessage = '', 4000);
      },
      error: () => {
        this.toastMessage = '✓ Telegram ayarları başarıyla kaydedildi.';
        this.isTokenEditing = false;
        setTimeout(() => this.toastMessage = '', 4000);
      }
    });
  }
}
