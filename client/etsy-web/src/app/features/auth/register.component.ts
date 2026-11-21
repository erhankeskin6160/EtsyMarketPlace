import { Component, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router, RouterModule } from '@angular/router';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthService } from '../../core/services/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-register',
  standalone: true,
  imports: [CommonModule, FormsModule, RouterModule, TranslocoDirective, LanguageSwitcherComponent],
  template: `
    <div class="auth-container" *transloco="let t">
      <div class="auth-card">
        <div class="auth-top-bar">
          <div class="brand-badge">{{ t('auth.register.badge') }}</div>
          <app-language-switcher></app-language-switcher>
        </div>
        <div class="auth-header">
          <h2>{{ t('auth.register.title') }}</h2>
          <p class="auth-subtitle">{{ t('auth.register.subtitle') }}</p>
        </div>

        <div class="alert-error" *ngIf="errorMessage">
          ⚠️ {{ errorMessage }}
        </div>

        <form (ngSubmit)="onRegister()" class="auth-form">
          <div class="form-group">
            <label>{{ t('auth.register.usernameLabel') }}</label>
            <input type="text" [(ngModel)]="username" name="username" class="form-input" [placeholder]="t('auth.register.usernamePlaceholder')" required minlength="3" />
          </div>

          <div class="form-group">
            <label>{{ t('auth.register.emailLabel') }}</label>
            <input type="email" [(ngModel)]="email" name="email" class="form-input" [placeholder]="t('auth.register.emailPlaceholder')" required />
          </div>

          <div class="form-group">
            <label>{{ t('auth.register.shopIdLabel') }}</label>
            <input type="text" [(ngModel)]="shopId" name="shopId" class="form-input" [placeholder]="t('auth.register.shopIdPlaceholder')" />
          </div>

          <div class="form-group">
            <label>{{ t('auth.register.passwordLabel') }}</label>
            <input type="password" [(ngModel)]="password" name="password" class="form-input" [placeholder]="t('auth.register.passwordPlaceholder')" required minlength="6" />
          </div>

          <button type="submit" class="btn btn-orange w-full" [disabled]="loading">
            {{ loading ? t('auth.register.submittingBtn') : t('auth.register.submitBtn') }}
          </button>
        </form>

        <div class="auth-footer">
          <span>{{ t('auth.register.hasAccount') }}</span>
          <a routerLink="/auth/login" class="link-orange">{{ t('auth.register.signInLink') }}</a>
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
    .auth-top-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 12px;
      margin-bottom: 12px;
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
  private destroyRef = inject(DestroyRef);
  private languageService = inject(LanguageService);
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
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.loading = false;
        if (res.success) {
          this.router.navigate(['/dashboard']);
        } else {
          this.errorMessage = res.message || this.languageService.translate('auth.register.errorDefault');
        }
      },
      error: err => {
        this.loading = false;
        this.errorMessage = err?.error?.message || this.languageService.translate('auth.register.errorGeneric');
      }
    });
  }
}
