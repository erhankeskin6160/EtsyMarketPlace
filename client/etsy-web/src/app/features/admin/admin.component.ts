import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { User, AuditLog, SystemStats, UpdateUserRequest } from '../../core/models/auth.models';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="admin-root">
      <div class="page-header-flex">
        <div>
          <div class="badge-admin-title">👑 SUPER ADMIN KOMUTA MERKEZİ</div>
          <h2 class="title">Kurumsal Kullanıcı, Rol & Güvenlik Yönetimi</h2>
          <p class="subtitle">Platformdaki tüm kullanıcıları, mağaza eşlemelerini, AI kotalarını ve audit kayıtlarını yönetin</p>
        </div>
        <button class="btn btn-secondary" (click)="loadData()">🔄 Verileri Yenile</button>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="admin-toast" [ngClass]="toastType">
        <span>{{ toastMessage }}</span>
        <button class="btn-close-toast" (click)="toastMessage = null">✕</button>
      </div>

      <!-- SYSTEM STATS STRIP -->
      <section class="stats-strip">
        <div class="glass-card stat-box">
          <span class="stat-label">Toplam Kayıtlı Kullanıcı</span>
          <span class="stat-number text-cyan">{{ stats.totalUsers }}</span>
          <span class="stat-sub">SaaS Platform Hesabı</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">Aktif Kullanıcılar</span>
          <span class="stat-number text-emerald">{{ stats.activeUsers }}</span>
          <span class="stat-sub">Yetkili Giriş Açık</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">Lisanslı Etsy Mağazaları</span>
          <span class="stat-number text-orange">{{ stats.totalShops }}</span>
          <span class="stat-sub">Aktif Entegre Mağaza</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">Toplam Tüketilen AI Token</span>
          <span class="stat-number text-purple">{{ stats.totalUsedAiTokens | number }}</span>
          <span class="stat-sub">Gemini Spark Motoru</span>
        </div>
      </section>

      <!-- USERS MANAGEMENT TABLE -->
      <section class="glass-card users-section">
        <div class="table-header">
          <div>
            <h3 class="section-title">👥 Kullanıcı ve Rol Yetkilendirme</h3>
            <span class="section-sub">Kullanıcı rollerini, mağaza eşlemelerini ve aylık AI sorgu kotalarını düzenleyin</span>
          </div>
          <span class="badge badge-admin">{{ users.length }} Kullanıcı</span>
        </div>

        <div class="table-responsive">
          <table class="admin-table">
            <thead>
              <tr>
                <th>KULLANICI</th>
                <th>E-POSTA</th>
                <th>ROL</th>
                <th>ATANMIŞ MAĞAZALAR</th>
                <th>AYLIK AI KOTASI</th>
                <th>DURUM</th>
                <th>İŞLEMLER</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let u of users">
                <td>
                  <div class="user-cell">
                    <span class="user-avatar-sm">{{ u.username.charAt(0).toUpperCase() }}</span>
                    <div>
                      <div class="user-cell-name">{{ u.username }}</div>
                      <span class="user-cell-date">Kayıt: {{ u.createdAt | date:'shortDate' }}</span>
                    </div>
                  </div>
                </td>
                <td>{{ u.email }}</td>
                <td>
                  <select [(ngModel)]="u.role" (change)="saveUser(u)" class="form-select role-select">
                    <option value="Admin">👑 Super Admin</option>
                    <option value="StoreOwner">🏪 Mağaza Sahibi</option>
                    <option value="Demo">👁️ Demo / Salt Okunur</option>
                  </select>
                </td>
                <td>
                  <div class="shop-tags">
                    <span class="shop-tag" *ngFor="let shop of u.assignedShopIds">🏬 {{ shop }}</span>
                  </div>
                </td>
                <td>
                  <span class="quota-text">{{ u.monthlyAiTokenQuota | number }} Token</span>
                </td>
                <td>
                  <span class="badge" [class.badge-success]="u.isActive" [class.badge-danger]="!u.isActive">
                    {{ u.isActive ? 'AKTİF' : 'ASKIDA' }}
                  </span>
                </td>
                <td>
                  <button class="btn btn-secondary btn-sm" (click)="toggleStatus(u)">
                    {{ u.isActive ? '🛑 Askıya Al' : '✅ Aktifleştir' }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- AUDIT LOGS SECTION -->
      <section class="glass-card audit-section">
        <div class="table-header">
          <div>
            <h3 class="section-title">🛡️ Sistem ve Audit Güvenlik Günlüğü</h3>
            <span class="section-sub">Gerçekleşen tüm girişler, rol değişiklikleri ve güvenlik olayları</span>
          </div>
          <span class="badge badge-cyan">{{ logs.length }} Kayıt</span>
        </div>

        <div class="table-responsive">
          <table class="admin-table">
            <thead>
              <tr>
                <th>ZAMAN</th>
                <th>KULLANICI</th>
                <th>AKSİYON</th>
                <th>DETAYLAR</th>
                <th>IP ADRESİ</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let log of logs">
                <td class="text-muted">{{ log.timestamp | date:'dd.MM.yyyy HH:mm:ss' }}</td>
                <td><b>{{ log.username }}</b></td>
                <td>
                  <span class="action-tag" [class.action-login]="log.action === 'Login'" [class.action-register]="log.action === 'Register'">
                    {{ log.action }}
                  </span>
                </td>
                <td>{{ log.details }}</td>
                <td class="text-muted"><code>{{ log.ipAddress || '127.0.0.1' }}</code></td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .admin-root {
      display: flex;
      flex-direction: column;
      gap: 24px;
    }
    .page-header-flex {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
    }
    .badge-admin-title {
      font-size: 0.72rem;
      font-weight: 700;
      color: #f87171;
      background: rgba(239, 68, 68, 0.12);
      border: 1px solid rgba(239, 68, 68, 0.3);
      padding: 3px 10px;
      border-radius: 9999px;
      display: inline-block;
      margin-bottom: 8px;
    }
    .title {
      font-size: 1.4rem;
      font-weight: 800;
      color: #fff;
    }
    .subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 4px;
    }

    /* STATS STRIP */
    .stats-strip {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(200px, 1fr));
      gap: 16px;
    }
    .stat-box {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .stat-label {
      font-size: 0.72rem;
      font-weight: 600;
      color: var(--text-muted);
    }
    .stat-number {
      font-size: 1.6rem;
      font-weight: 800;
    }
    .stat-sub {
      font-size: 0.72rem;
      color: var(--text-muted);
    }

    /* TABLES */
    .table-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 16px;
    }
    .section-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
    }
    .section-sub {
      font-size: 0.78rem;
      color: var(--text-muted);
    }
    .table-responsive {
      overflow-x: auto;
    }
    .admin-table {
      width: 100%;
      border-collapse: collapse;
      text-align: left;
      font-size: 0.85rem;
    }
    .admin-table th {
      padding: 12px 14px;
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--text-muted);
      border-bottom: 1px solid var(--border-color);
      letter-spacing: 0.05em;
    }
    .admin-table td {
      padding: 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #e2e8f0;
    }
    .admin-table tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }

    .user-cell {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .user-avatar-sm {
      width: 32px;
      height: 32px;
      border-radius: 50%;
      background: #334155;
      color: #fff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 700;
      font-size: 0.8rem;
    }
    .user-cell-name {
      font-weight: 600;
      color: #fff;
    }
    .user-cell-date {
      font-size: 0.7rem;
      color: var(--text-muted);
    }
    .role-select {
      background: #0f172a;
      border: 1px solid var(--border-color);
      color: #fff;
      padding: 6px 10px;
      border-radius: 6px;
      font-size: 0.8rem;
    }
    .shop-tags {
      display: flex;
      gap: 6px;
      flex-wrap: wrap;
    }
    .shop-tag {
      background: #1e293b;
      border: 1px solid #334155;
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 0.75rem;
      color: #38bdf8;
    }
    .quota-text {
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.82rem;
      color: #a78bfa;
    }
    .btn-sm {
      padding: 5px 10px;
      font-size: 0.75rem;
    }

    .action-tag {
      padding: 2px 8px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 600;
      background: #1e293b;
    }
    .action-login { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .action-register { background: rgba(59, 130, 246, 0.15); color: #60a5fa; }

    .admin-toast {
      padding: 12px 18px;
      margin-bottom: 16px;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      display: flex;
      justify-content: space-between;
      align-items: center;
      animation: fadeIn 0.25s ease;
    }
    .admin-toast.success { background: rgba(16, 185, 129, 0.15); border: 1px solid rgba(16, 185, 129, 0.4); color: #34d399; }
    .admin-toast.error { background: rgba(239, 68, 68, 0.15); border: 1px solid rgba(239, 68, 68, 0.4); color: #f87171; }
    .admin-toast.info { background: rgba(59, 130, 246, 0.15); border: 1px solid rgba(59, 130, 246, 0.4); color: #60a5fa; }
    .btn-close-toast { background: none; border: none; color: inherit; cursor: pointer; font-size: 1rem; }
  `]
})
export class AdminComponent implements OnInit {
  apiService = inject(EtsyApiService);

  users: User[] = [];
  logs: AuditLog[] = [];
  stats: SystemStats = {
    totalUsers: 1,
    activeUsers: 1,
    totalShops: 1,
    totalUsedAiTokens: 124500,
    auditLogCount: 1
  };

  toastMessage: string | null = null;
  toastType: 'success' | 'error' | 'info' = 'info';

  showToast(message: string, type: 'success' | 'error' | 'info' = 'info'): void {
    this.toastMessage = message;
    this.toastType = type;
    setTimeout(() => {
      if (this.toastMessage === message) {
        this.toastMessage = null;
      }
    }, 4500);
  }

  ngOnInit(): void {
    this.loadData();
  }

  loadData(): void {
    this.apiService.getAdminUsers().subscribe({
      next: res => this.users = res,
      error: () => {
        // Fallback default admin user
        this.users = [{
          id: 'admin-1',
          username: 'admin',
          email: 'admin@etsymarketplace.local',
          role: 'Admin',
          assignedShopIds: ['53236321'],
          monthlyAiTokenQuota: 2000000,
          usedAiTokens: 124500,
          isActive: true,
          createdAt: new Date().toISOString()
        }];
      }
    });

    this.apiService.getAuditLogs().subscribe({
      next: res => this.logs = res,
      error: () => {}
    });

    this.apiService.getSystemStats().subscribe({
      next: res => this.stats = res,
      error: () => {}
    });
  }

  saveUser(user: User): void {
    const req: UpdateUserRequest = {
      email: user.email,
      role: user.role,
      assignedShopIds: user.assignedShopIds,
      monthlyAiTokenQuota: user.monthlyAiTokenQuota,
      isActive: user.isActive
    };

    this.apiService.updateAdminUser(user.id, req).subscribe({
      next: () => {
        this.showToast(`✓ "${user.username}" kullanıcısının rolü "${user.role}" ve ayarları başarıyla kaydedildi.`, 'success');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.error || err?.message || 'Güncelleme yapılamadı';
        this.showToast(`⚠️ Kullanıcı güncelleme hatası: ${msg}`, 'error');
      }
    });
  }

  toggleStatus(user: User): void {
    this.apiService.toggleUserStatus(user.id).subscribe({
      next: res => {
        user.isActive = res.isActive;
        this.showToast(`✓ "${user.username}" hesabı ${res.isActive ? 'aktif edildi' : 'askıya alındı'}.`, 'success');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.error || err?.message || 'Durum değiştirilemedi';
        this.showToast(`⚠️ Hesap durumu değiştirilemedi: ${msg}`, 'error');
      }
    });
  }
}
