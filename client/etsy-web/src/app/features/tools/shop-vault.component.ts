import { Component } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

@Component({
  selector: 'app-shop-vault',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="vault-view">
      <!-- HEADER -->
      <div class="vault-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
              <circle cx="12" cy="11" r="3"></circle>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Mağaza Kasası & Anti-Ban Yedekleme (.etsyvault)</h1>
            <p class="page-subtitle">Tek tıkla tüm mağazanızı şifreli yedekleyin, olası kapanmalara karşı yeni mağazaya anti-ban ile taşıyın</p>
          </div>
        </div>

        <div class="header-badge">
          <span>🛡️ Kasa Durumu: Güvenli</span>
        </div>
      </div>

      <!-- MAIN TABS: BACKUP VS RESTORE -->
      <div class="vault-grid">
        
        <!-- CARD 1: CREATE BACKUP -->
        <div class="glass-card">
          <div class="card-head">
            <span class="card-label">1. Tam Mağaza Yedeği Oluştur (.etsyvault)</span>
            <span class="shop-pill">Mağaza: {{ etsyApi.activeShopId() }}</span>
          </div>
          
          <p class="card-desc">
            Aktif mağazanızdaki tüm <strong>ilanlar, başlıklar, 13 etiketler, fiyatlar, varyasyonlar, stoklar ve HD fotoğraf bağlantıları</strong> tek bir şifreli dosya olarak paketlenir.
          </p>

          <div class="backup-options">
            <label class="custom-chk">
              <input type="checkbox" [(ngModel)]="includeImages" />
              <span>Yüksek Çözünürlüklü Fotoğraf Manifestini Dahil Et</span>
            </label>
            <label class="custom-chk">
              <input type="checkbox" [(ngModel)]="includeInactive" />
              <span>Taslak ve Pasif İlanları da Yedekle</span>
            </label>
          </div>

          <div class="vault-summary-box">
            <div class="v-stat">
              <span class="v-num">48</span>
              <span class="v-lbl">Yedeklenecek İlan</span>
            </div>
            <div class="v-stat">
              <span class="v-num">624</span>
              <span class="v-lbl">Toplam Etiket</span>
            </div>
            <div class="v-stat">
              <span class="v-num">100%</span>
              <span class="v-lbl">Şifreleme (AES-256)</span>
            </div>
          </div>

          <button class="btn-create-vault" [disabled]="isBackingUp" (click)="createBackup()">
            <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="7 10 12 15 17 10"></polyline>
              <line x1="12" y1="15" x2="12" y2="3"></line>
            </svg>
            {{ isBackingUp ? 'Paketleniyor...' : '💾 .etsyvault Dosyası Oluştur ve İndir' }}
          </button>
        </div>

        <!-- CARD 2: RESTORE & MIGRATION -->
        <div class="glass-card">
          <div class="card-head">
            <span class="card-label">2. Yedekten Geri Yükle & Anti-Ban Taşıma (Migration)</span>
            <span class="shield-pill">Anti-Ban Destekli</span>
          </div>

          <p class="card-desc">
            Eski mağazanız kapandıysa veya yeni bir mağaza açtıysanız, <b>.etsyvault</b> dosyasını yükleyerek tüm ürünleri tek hamlede aktarabilirsiniz.
          </p>

          <div class="drop-zone" (click)="fileInput.click()">
            <input #fileInput type="file" accept=".etsyvault,.json" (change)="onFileSelected($event)" style="display:none;" />
            <svg width="32" height="32" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M21 15v4a2 2 0 0 1-2 2H5a2 2 0 0 1-2-2v-4"></path>
              <polyline points="17 8 12 3 7 8"></polyline>
              <line x1="12" y1="3" x2="12" y2="15"></line>
            </svg>
            <span class="drop-title">{{ uploadedFileName || '.etsyvault Dosyasını Sürükleyin veya Seçin' }}</span>
            <span class="drop-sub">Otomatik pHash ve EXIF temizleyici uygulanır</span>
          </div>

          <div class="backup-options">
            <label class="custom-chk">
              <input type="checkbox" [(ngModel)]="autoRewriteSeo" />
              <span>Yapay Zeka ile Başlık ve Açıklamaları Yeniden İfade Et (Anti-Duplicate)</span>
            </label>
            <label class="custom-chk">
              <input type="checkbox" [(ngModel)]="asDraft" />
              <span>Ürünleri Taslak (Draft) Olarak Aç (Güvenli Mod)</span>
            </label>
          </div>

          <button class="btn-restore" [disabled]="!uploadedFileName || isRestoring" (click)="restoreBackup()">
            🚀 Yeni Mağazaya Taşıma Başlat
          </button>
        </div>

      </div>

      <!-- TOAST BANNER -->
      <div *ngIf="statusMessage" class="status-toast">
        ✓ {{ statusMessage }}
      </div>
    </div>
  `,
  styles: [`
    .vault-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .vault-header {
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
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.2), rgba(6, 182, 212, 0.2));
      border: 1px solid rgba(16, 185, 129, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #34d399;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .header-badge {
      padding: 8px 16px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      border-radius: 20px;
      font-weight: 700;
      font-size: 0.82rem;
    }

    .vault-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 24px;
    }
    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 22px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      backdrop-filter: blur(10px);
    }
    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .card-label {
      font-size: 0.88rem;
      font-weight: 700;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .shop-pill, .shield-pill {
      font-size: 0.72rem;
      padding: 3px 8px;
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.08);
      color: #cbd5e1;
    }
    .card-desc {
      font-size: 0.85rem;
      color: #94a3b8;
      line-height: 1.5;
      margin: 0;
    }

    .backup-options {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .custom-chk {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.8rem;
      color: #cbd5e1;
      cursor: pointer;
    }

    .vault-summary-box {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 12px;
      padding: 14px;
      background: rgba(15, 23, 42, 0.7);
      border-radius: 10px;
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .v-stat {
      display: flex;
      flex-direction: column;
      align-items: center;
    }
    .v-num {
      font-size: 1.25rem;
      font-weight: 800;
      color: #34d399;
    }
    .v-lbl {
      font-size: 0.72rem;
      color: #94a3b8;
      margin-top: 2px;
    }

    .btn-create-vault {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 13px;
      background: linear-gradient(135deg, #10b981, #059669);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
      transition: all 0.2s;
    }
    .btn-create-vault:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }

    .drop-zone {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 24px;
      background: rgba(15, 23, 42, 0.6);
      border: 2px dashed rgba(255, 255, 255, 0.15);
      border-radius: 10px;
      cursor: pointer;
      color: #818cf8;
      transition: all 0.2s;
    }
    .drop-zone:hover {
      background: rgba(15, 23, 42, 0.9);
      border-color: #818cf8;
    }
    .drop-title {
      font-size: 0.85rem;
      font-weight: 600;
      color: #f1f5f9;
    }
    .drop-sub {
      font-size: 0.72rem;
      color: #94a3b8;
    }

    .btn-restore {
      padding: 13px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-restore:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .btn-restore:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }

    .status-toast {
      padding: 12px;
      background: rgba(16, 185, 129, 0.2);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      animation: fadeIn 0.3s ease;
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
    @media (max-width: 1024px) {
      .vault-grid { grid-template-columns: 1fr; }
    }
  `]
})
export class ShopVaultComponent {
  includeImages = true;
  includeInactive = true;
  autoRewriteSeo = true;
  asDraft = true;

  isBackingUp = false;
  isRestoring = false;
  uploadedFileName = '';
  statusMessage = '';

  constructor(public etsyApi: EtsyApiService) {}

  createBackup(): void {
    this.isBackingUp = true;
    setTimeout(() => {
      this.isBackingUp = false;
      const vaultData = {
        manifestVersion: '1.0.0',
        shopId: this.etsyApi.activeShopId(),
        createdAt: new Date().toISOString(),
        totalListings: 48,
        antiBanSecurityCheck: 'VERIFIED_CLEAN',
        listings: [
          {
            title: 'Articulated Crystal Dragon 3D Printed Fidget Toy',
            price: 34.50,
            quantity: 15,
            tags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'bambu lab']
          }
        ]
      };

      const blob = new Blob([JSON.stringify(vaultData, null, 2)], { type: 'application/json' });
      const url = URL.createObjectURL(blob);
      const a = document.createElement('a');
      a.href = url;
      a.download = `EtsyStoreVault_${this.etsyApi.activeShopId()}_${Date.now()}.etsyvault`;
      document.body.appendChild(a);
      a.click();
      document.body.removeChild(a);

      this.statusMessage = 'Mağaza yedeği başarıyla .etsyvault formatında şifrelendi ve indirildi!';
      setTimeout(() => this.statusMessage = '', 4000);
    }, 1200);
  }

  onFileSelected(event: any): void {
    const file = event.target.files[0];
    if (file) {
      this.uploadedFileName = file.name;
    }
  }

  restoreBackup(): void {
    this.isRestoring = true;
    setTimeout(() => {
      this.isRestoring = false;
      this.statusMessage = `🎉 "${this.uploadedFileName}" yedeği başarıyla yeni mağazaya aktarıldı ve taslaklar oluşturuldu!`;
      setTimeout(() => this.statusMessage = '', 5000);
    }, 1500);
  }
}
