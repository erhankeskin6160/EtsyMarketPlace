import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { AuthService } from '../../core/services/auth.service';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule],
  template: `
    <div class="auth-container">
      <div class="auth-card">
        <div class="auth-header">
          <div class="brand-badge">🏪 YENİ MAĞAZA KAYDI</div>
          <h2>Hesap Oluştur</h2>
          <p class="auth-subtitle">Etsy Enterprise Studio ile mağazanızı ölçekleyin</p>
        </div>

        <div class="alert-error" *ngIf="errorMessage">
          ⚠️ {{ errorMessage }}
        </div>

        <form (ngSubmit)="onRegister()" class="auth-form">
          <div class="form-group">
            <label>Kullanıcı Adı</label>
            <input type="text" [(ngModel)]="username" name="username" class="form-input" placeholder="örn: seller_pro" required minlength="3" />
          </div>

          <div class="form-group">
            <label>E-Posta Adresi</label>
            <input type="email" [(ngModel)]="email" name="email" class="form-input" placeholder="örn: magaza@alanadi.com" required />
          </div>

          <div class="form-group">
            <label>Etsy Mağaza ID (İsteğe Bağlı)</label>
            <input type="text" [(ngModel)]="shopId" name="shopId" class="form-input" placeholder="Varsayılan: 53236321" />
          </div>

          <div class="form-group">
            <label>Şifre</label>
            <input type="password" [(ngModel)]="password" name="password" class="form-input" placeholder="En az 6 karakter" required minlength="6" />
          </div>

          <button type="submit" class="btn btn-orange w-full" [disabled]="loading">
            {{ loading ? 'Kayıt Yapılıyor...' : '✨ Mağaza Hesabımı Başlat' }}
          </button>
        </form>

        <div class="auth-footer">
          <span>Zaten hesabınız var mı?</span>
          <a routerLink="/auth/login" class="link-orange">Giriş Yap</a>
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
      color: var(--accent-color);
      background: rgba(6, 182, 212, 0.12);
      border: 1px solid rgba(6, 182, 212, 0.3);
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
      gap: 14px;
      margin: 20px 0 16px;
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
      margin-top: 14px;
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
  `]
})
export class RegisterComponent {
  authService = inject(AuthService);
  router = inject(Router);

  username = '';
  email = '';
  shopId = '53236321';
  password = '';
  loading = false;
  errorMessage = '';

  onRegister(): void {
    if (!this.username || !this.email || !this.password) return;
    this.loading = true;
    this.errorMessage = '';

    this.authService.register({
      username: this.username,
      email: this.email,
      shopId: this.shopId,
      password: this.password
    }).subscribe({
      next: res => {
        this.loading = false;
        if (res.success) {
          this.router.navigate(['/dashboard']);
        } else {
          this.errorMessage = res.message || 'Kayıt başarısız.';
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message || 'Kayıt sırasında bir hata oluştu.';
      }
    });
  }
}
