import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-login',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="auth-container">
      <div class="auth-card">
        <div class="auth-header">
          <div class="brand-badge">🚀 ETSY ENTERPRISE</div>
          <h2>Giriş Yap</h2>
          <p class="auth-subtitle">Etsy Mağaza Yönetim & Komuta Merkezi</p>
        </div>

        <div class="alert-error" *ngIf="errorMessage">
          ⚠️ {{ errorMessage }}
        </div>

        <form (ngSubmit)="onLogin()" class="auth-form">
          <div class="form-group">
            <label>Kullanıcı Adı veya E-Posta</label>
            <input type="text" [(ngModel)]="username" name="username" class="form-input" placeholder="örn: admin veya erhan@etsy.com" required autofocus />
          </div>

          <div class="form-group">
            <label>Şifre</label>
            <input type="password" [(ngModel)]="password" name="password" class="form-input" placeholder="••••••••" required />
          </div>

          <button type="submit" class="btn btn-orange w-full" [disabled]="loading">
            {{ loading ? 'Giriş Yapılıyor...' : '🔐 Güvenli Giriş Yap' }}
          </button>

          <div class="auth-hints">
            <button type="button" class="btn-fill-hint" (click)="fillAdmin()">⚡ Yönetici Girişini Doldur (admin / Admin123*!)</button>
          </div>
        </form>

        <div class="auth-footer">
          <span>Hesabınız yok mu?</span>
          <a routerLink="/auth/register" class="link-orange">Yeni Mağaza Hesabı Aç</a>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .auth-container {
      min-height: 100vh;
      display: flex;
      align-items: center;
      justify-content: center;
      background: radial-gradient(circle at center, #151e2e 0%, #0b0f19 100%);
      padding: 20px;
    }
    .auth-card {
      width: 100%;
      max-width: 440px;
      background: rgba(21, 30, 46, 0.9);
      backdrop-filter: blur(16px);
      border: 1px solid var(--border-color);
      border-radius: var(--radius-lg);
      padding: 36px 32px;
      box-shadow: 0 25px 50px -12px rgba(0, 0, 0, 0.6);
    }
    .brand-badge {
      display: inline-block;
      font-size: 0.72rem;
      font-weight: 700;
      color: var(--etsy-orange);
      background: rgba(249, 115, 22, 0.12);
      border: 1px solid rgba(249, 115, 22, 0.3);
      padding: 3px 10px;
      border-radius: 9999px;
      margin-bottom: 12px;
      letter-spacing: 0.06em;
    }
    .auth-header h2 {
      font-size: 1.6rem;
      font-weight: 800;
      color: #fff;
    }
    .auth-subtitle {
      font-size: 0.85rem;
      color: var(--text-muted);
      margin-top: 4px;
    }
    .auth-form {
      display: flex;
      flex-direction: column;
      gap: 16px;
      margin: 24px 0 16px;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .form-group label {
      font-size: 0.8rem;
      font-weight: 600;
      color: var(--text-secondary);
    }
    .w-full {
      width: 100%;
      padding: 12px;
      font-size: 0.95rem;
    }
    .alert-error {
      background: rgba(239, 68, 68, 0.15);
      border: 1px solid rgba(239, 68, 68, 0.3);
      color: #fca5a5;
      padding: 10px 14px;
      border-radius: 8px;
      font-size: 0.85rem;
      margin-top: 16px;
    }
    .demo-credentials {
      background: #0f172a;
      border: 1px dashed #334155;
      border-radius: 8px;
      padding: 10px 12px;
      font-size: 0.75rem;
      margin-top: 8px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      color: #94a3b8;
    }
    .demo-title {
      font-weight: 600;
      color: #38bdf8;
    }
    .auth-footer {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      margin-top: 20px;
      font-size: 0.82rem;
      color: var(--text-muted);
    }
    .link-orange {
      color: var(--etsy-orange);
      font-weight: 600;
      text-decoration: none;
    }
    .link-orange:hover {
      text-decoration: underline;
    }
    .auth-hints {
      margin-top: 12px;
      display: flex;
      justify-content: center;
    }
    .btn-fill-hint {
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.3);
      color: #38bdf8;
      font-size: 0.78rem;
      padding: 6px 12px;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-fill-hint:hover {
      background: rgba(56, 189, 248, 0.2);
      border-color: #38bdf8;
    }
  `]
})
export class LoginComponent {
  authService = inject(AuthService);
  router = inject(Router);

  username = '';
  password = '';
  loading = false;
  errorMessage = '';

  ngOnInit(): void {
    if (this.authService.isAuthenticated()) {
      this.router.navigate(['/finance/accounting']);
    }
  }

  fillAdmin(): void {
    this.username = 'admin';
    this.password = 'Admin123*!';
    this.errorMessage = '';
  }

  onLogin(): void {
    if (!this.username || !this.password) return;
    this.loading = true;
    this.errorMessage = '';

    this.authService.login({
      usernameOrEmail: this.username,
      password: this.password
    }).subscribe({
      next: res => {
        this.loading = false;
        if (res.success) {
          this.router.navigate(['/finance/accounting']);
        } else {
          this.errorMessage = res.message || 'Giriş başarısız.';
        }
      },
      error: err => {
        this.loading = false;
        console.error('[Login] Error:', err);
        if (err.status === 0) {
          this.errorMessage = `Sunucuya bağlanılamadı (Ağ Hatası / Status 0). Lütfen tarayıcıyı Ctrl+F5 ile sert yenileyin. [Denenen URL: ${err?.url || 'VDS/Proxy'}]`;
        } else {
          this.errorMessage = err?.error?.message || `Hata (HTTP ${err.status}): ${err.statusText || 'Giriş yapılamadı.'}`;
        }
      }
    });
  }
}
