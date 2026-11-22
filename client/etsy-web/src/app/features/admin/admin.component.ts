import { Component, OnInit, inject, DestroyRef } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { PaymentGatewaySettings, SubscriptionPlan } from '../../core/models/subscription.models';
import { User, AuditLog, SystemStats, UpdateUserRequest } from '../../core/models/auth.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-admin',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('admin')],
  template: `
    <ng-container *transloco="let t">
    <div class="admin-root">
      <div class="page-header-flex">
        <div>
          <div class="badge-admin-title">{{ t('admin.header.badge') }}</div>
          <h2 class="title">{{ t('admin.header.title') }}</h2>
          <p class="subtitle">{{ t('admin.header.subtitle') }}</p>
        </div>
        <button class="btn btn-secondary" (click)="loadData()">{{ t('admin.header.refreshBtn') }}</button>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="admin-toast" [ngClass]="toastType">
        <span>{{ toastMessage }}</span>
        <button class="btn-close-toast" (click)="toastMessage = null">✕</button>
      </div>

      <!-- SYSTEM STATS STRIP -->
      <section class="stats-strip">
        <div class="glass-card stat-box">
          <span class="stat-label">{{ t('admin.stats.totalUsersLabel') }}</span>
          <span class="stat-number text-cyan">{{ stats.totalUsers }}</span>
          <span class="stat-sub">{{ t('admin.stats.totalUsersSub') }}</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">{{ t('admin.stats.activeUsersLabel') }}</span>
          <span class="stat-number text-emerald">{{ stats.activeUsers }}</span>
          <span class="stat-sub">{{ t('admin.stats.activeUsersSub') }}</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">{{ t('admin.stats.totalShopsLabel') }}</span>
          <span class="stat-number text-orange">{{ stats.totalShops }}</span>
          <span class="stat-sub">{{ t('admin.stats.totalShopsSub') }}</span>
        </div>
        <div class="glass-card stat-box">
          <span class="stat-label">{{ t('admin.stats.totalTokensLabel') }}</span>
          <span class="stat-number text-purple">{{ stats.totalUsedAiTokens | number }}</span>
          <span class="stat-sub">{{ t('admin.stats.totalTokensSub') }}</span>
        </div>
      </section>

      <!-- USERS MANAGEMENT TABLE -->
      <section class="glass-card users-section">
        <div class="table-header">
          <div>
            <h3 class="section-title">{{ t('admin.users.title') }}</h3>
            <span class="section-sub">{{ t('admin.users.subtitle') }}</span>
          </div>
          <span class="badge badge-admin">{{ t('admin.users.countBadge', { count: users.length }) }}</span>
        </div>

        <div class="table-responsive">
          <table class="admin-table">
            <thead>
              <tr>
                <th>{{ t('admin.users.colUser') }}</th>
                <th>{{ t('admin.users.colEmail') }}</th>
                <th>{{ t('admin.users.colRole') }}</th>
                <th>{{ t('admin.users.colPlan') }}</th>
                <th>{{ t('admin.users.colShops') }}</th>
                <th>{{ t('admin.users.colQuota') }}</th>
                <th>{{ t('admin.users.colStatus') }}</th>
                <th>{{ t('admin.users.colActions') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let u of users">
                <td>
                  <div class="user-cell">
                    <span class="user-avatar-sm">{{ u.username.charAt(0).toUpperCase() }}</span>
                    <div>
                      <div class="user-cell-name">{{ u.username }}</div>
                      <span class="user-cell-date">{{ t('admin.users.registeredAt', { date: (u.createdAt | date:'shortDate') }) }}</span>
                    </div>
                  </div>
                </td>
                <td>{{ u.email }}</td>
                <td>
                  <select [(ngModel)]="u.role" (change)="saveUser(u)" class="form-select role-select">
                    <option value="Admin">{{ t('admin.users.roleAdmin') }}</option>
                    <option value="StoreOwner">{{ t('admin.users.roleStoreOwner') }}</option>
                    <option value="Demo">{{ t('admin.users.roleDemo') }}</option>
                  </select>
                </td>
                <td>
                  <select [ngModel]="u.subscriptionPlanId || 'pro'"
                          (ngModelChange)="assignUserPlan(u, $event)"
                          class="form-select role-select">
                    <option value="starter">{{ t('admin.users.planStarter') }}</option>
                    <option value="growth">{{ t('admin.users.planGrowth') }}</option>
                    <option value="pro">{{ t('admin.users.planPro') }}</option>
                    <option value="enterprise">{{ t('admin.users.planEnterprise') }}</option>
                  </select>
                </td>
                <td>
                  <div class="shop-tags">
                    <span class="shop-tag" *ngFor="let shop of u.assignedShopIds">🏬 {{ shop }}</span>
                  </div>
                </td>
                <td>
                  <span class="quota-text">{{ t('admin.users.tokensUnit', { count: (u.monthlyAiTokenQuota | number) }) }}</span>
                </td>
                <td>
                  <span class="badge" [class.badge-success]="u.isActive" [class.badge-danger]="!u.isActive">
                    {{ u.isActive ? t('admin.users.statusActive') : t('admin.users.statusSuspended') }}
                  </span>
                </td>
                <td>
                  <button class="btn btn-secondary btn-sm" (click)="toggleStatus(u)">
                    {{ u.isActive ? t('admin.users.suspendBtn') : t('admin.users.activateBtn') }}
                  </button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- SUBSCRIPTION PLANS MANAGEMENT SECTION -->
      <section class="glass-card users-section">
        <div class="table-header">
          <div>
            <h3 class="section-title">{{ t('admin.plans.title') }}</h3>
            <span class="section-sub">{{ t('admin.plans.subtitle') }}</span>
          </div>
          <span class="badge badge-pro">{{ t('admin.plans.countBadge', { count: plans.length }) }}</span>
        </div>

        <div class="table-responsive">
          <table class="admin-table">
            <thead>
              <tr>
                <th>{{ t('admin.plans.colPlan') }}</th>
                <th>{{ t('admin.plans.colMonthlyUsd') }}</th>
                <th>{{ t('admin.plans.colMonthlyTry') }}</th>
                <th>{{ t('admin.plans.colMaxShops') }}</th>
                <th>{{ t('admin.plans.colAiQuota') }}</th>
                <th>{{ t('admin.plans.colAction') }}</th>
              </tr>
            </thead>
            <tbody>
              <tr *ngFor="let p of plans">
                <td><b>{{ subscriptionService.getPlanEmoji(p.id) }} {{ p.name }}</b> <code class="text-muted">({{ p.id }})</code></td>
                <td><input type="number" [(ngModel)]="p.monthlyPriceUsd" class="role-select" style="width:90px" /></td>
                <td><input type="number" [(ngModel)]="p.monthlyPriceTry" class="role-select" style="width:105px" /></td>
                <td><input type="number" [(ngModel)]="p.maxShops" class="role-select" style="width:80px" /></td>
                <td><input type="number" [(ngModel)]="p.monthlyAiTokenQuota" class="role-select" style="width:130px" /></td>
                <td>
                  <button class="btn btn-secondary btn-sm" (click)="savePlanConfig(p)">{{ t('admin.plans.saveBtn') }}</button>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </section>

      <!-- PAYMENT GATEWAY CONFIGURATION SECTION (IYZICO / PAYTR / STRIPE) -->
      <section class="glass-card users-section payment-settings-card" id="payment-gateway-settings">
        <div class="table-header">
          <div>
            <h3 class="section-title" id="payment-gateway-heading">
              <i class="fa-solid fa-credit-card pg-heading-icon" aria-hidden="true"></i>
              {{ t('admin.payment.title') }}
            </h3>
            <span class="section-sub">
              {{ t('admin.payment.subtitle') }}
            </span>
          </div>
          <span class="pg-source-pill">
            {{ t('admin.payment.sourceLabel', { source: paymentSettings?.configSource || t('admin.payment.unconfiguredShort') }) }}
          </span>
        </div>

        <form class="pg-form"
              aria-labelledby="payment-gateway-heading"
              method="POST"
              (ngSubmit)="savePaymentGatewaySettings()">
          <div class="pg-top-grid">
            <div class="pg-field">
              <label for="pg-provider">{{ t('admin.payment.activeProviderLabel') }}</label>
              <select id="pg-provider"
                      name="activeProvider"
                      [(ngModel)]="paymentForm.activeProvider"
                      class="pg-input">
                <option value="Unconfigured">{{ t('admin.payment.providerUnconfigured') }}</option>
                <option value="PayTR">{{ t('admin.payment.providerPaytr') }}</option>
                <option value="Iyzico">{{ t('admin.payment.providerIyzico') }}</option>
                <option value="Stripe">{{ t('admin.payment.providerStripe') }}</option>
              </select>
            </div>

            <div class="pg-field">
              <label for="pg-env">{{ t('admin.payment.envLabel') }}</label>
              <select id="pg-env"
                      name="isSandbox"
                      [ngModel]="paymentForm.isSandbox"
                      (ngModelChange)="onSandboxChanged($event)"
                      class="pg-input">
                <option [ngValue]="true">{{ t('admin.payment.envSandbox') }}</option>
                <option [ngValue]="false">{{ t('admin.payment.envLive') }}</option>
              </select>
            </div>
          </div>

          <!-- PAYTR SETTINGS -->
          <fieldset *ngIf="paymentForm.activeProvider === 'PayTR'" class="pg-fieldset">
            <legend>{{ t('admin.payment.paytrLegend') }}</legend>
            <div class="pg-fields-grid">
              <div class="pg-field">
                <label for="paytr-id">{{ t('admin.payment.paytrMerchantIdLabel') }}</label>
                <input id="paytr-id"
                       name="paytrMerchantId"
                       type="text"
                       class="pg-input pg-mono"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.paytrMerchantId"
                       [placeholder]="t('admin.payment.paytrMerchantIdPlaceholder')" />
              </div>

              <div class="pg-field">
                <div class="pg-label-row">
                  <label for="paytr-key">{{ t('admin.payment.paytrMerchantKeyLabel') }}</label>
                  <span *ngIf="paymentSettings?.hasPaytrMerchantKey" class="pg-saved-badge">
                    {{ t('admin.payment.savedBadge', { mask: paymentSettings?.paytrMerchantKeyMask }) }}
                  </span>
                </div>
                <input id="paytr-key"
                       name="paytrMerchantKey"
                       type="password"
                       class="pg-input pg-mono"
                       autocomplete="off"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.paytrMerchantKey"
                       [placeholder]="t('admin.payment.paytrMerchantKeyPlaceholder')" />
              </div>

              <div class="pg-field">
                <div class="pg-label-row">
                  <label for="paytr-salt">{{ t('admin.payment.paytrMerchantSaltLabel') }}</label>
                  <span *ngIf="paymentSettings?.hasPaytrMerchantSalt" class="pg-saved-badge">
                    {{ t('admin.payment.savedBadge', { mask: paymentSettings?.paytrMerchantSaltMask }) }}
                  </span>
                </div>
                <input id="paytr-salt"
                       name="paytrMerchantSalt"
                       type="password"
                       class="pg-input pg-mono"
                       autocomplete="off"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.paytrMerchantSalt"
                       [placeholder]="t('admin.payment.paytrMerchantSaltPlaceholder')" />
              </div>
            </div>

            <div class="pg-callback-notice">
              <i class="fa-solid fa-link" aria-hidden="true"></i>
              <div>
                <strong>{{ t('admin.payment.paytrCallbackLabel') }}</strong>
                <code>{{ paytrCallbackUrl }}</code>
              </div>
            </div>
          </fieldset>

          <!-- IYZICO SETTINGS -->
          <fieldset *ngIf="paymentForm.activeProvider === 'Iyzico'" class="pg-fieldset">
            <legend>{{ t('admin.payment.iyzicoLegend') }}</legend>
            <div class="pg-fields-grid">
              <div class="pg-field">
                <label for="iyzi-key">{{ t('admin.payment.iyzicoApiKeyLabel') }}</label>
                <input id="iyzi-key"
                       name="iyzicoApiKey"
                       type="text"
                       class="pg-input pg-mono"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.iyzicoApiKey"
                       [placeholder]="t('admin.payment.iyzicoApiKeyPlaceholder')" />
              </div>

              <div class="pg-field">
                <div class="pg-label-row">
                  <label for="iyzi-secret">{{ t('admin.payment.iyzicoSecretKeyLabel') }}</label>
                  <span *ngIf="paymentSettings?.hasIyzicoSecretKey" class="pg-saved-badge">
                    {{ t('admin.payment.savedBadge', { mask: paymentSettings?.iyzicoSecretKeyMask }) }}
                  </span>
                </div>
                <input id="iyzi-secret"
                       name="iyzicoSecretKey"
                       type="password"
                       class="pg-input pg-mono"
                       autocomplete="off"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.iyzicoSecretKey"
                       [placeholder]="t('admin.payment.iyzicoSecretKeyPlaceholder')" />
              </div>

              <div class="pg-field">
                <label for="iyzi-url">{{ t('admin.payment.iyzicoBaseUrlLabel') }}</label>
                <input id="iyzi-url"
                       name="iyzicoBaseUrl"
                       type="url"
                       class="pg-input pg-mono"
                       autocorrect="off"
                       autocapitalize="none"
                       spellcheck="false"
                       [(ngModel)]="paymentForm.iyzicoBaseUrl"
                       placeholder="https://sandbox-api.iyzipay.com" />
              </div>
            </div>
          </fieldset>

          <!-- STRIPE SETTINGS -->
          <fieldset *ngIf="paymentForm.activeProvider === 'Stripe'" class="pg-fieldset">
            <legend>{{ t('admin.payment.stripeLegend') }}</legend>
            <div class="pg-field">
              <div class="pg-label-row">
                <label for="stripe-secret">{{ t('admin.payment.stripeSecretKeyLabel') }}</label>
                <span *ngIf="paymentSettings?.hasStripeSecretKey" class="pg-saved-badge">
                  {{ t('admin.payment.savedBadge', { mask: paymentSettings?.stripeSecretKeyMask }) }}
                </span>
              </div>
              <input id="stripe-secret"
                     name="stripeSecretKey"
                     type="password"
                     class="pg-input pg-mono"
                     autocomplete="off"
                     autocorrect="off"
                     autocapitalize="none"
                     spellcheck="false"
                     [(ngModel)]="paymentForm.stripeSecretKey"
                     [placeholder]="t('admin.payment.stripeSecretKeyPlaceholder')" />
            </div>
          </fieldset>

          <div class="pg-footer-actions">
            <span class="pg-hint-note">
              {{ t('admin.payment.secretHint') }}
            </span>
            <button type="submit"
                    class="btn pg-btn-save"
                    [disabled]="savingPaymentSettings">
              <ng-container *ngIf="!savingPaymentSettings">
                <i class="fa-solid fa-floppy-disk" aria-hidden="true"></i> {{ t('admin.payment.saveBtn') }}
              </ng-container>
              <ng-container *ngIf="savingPaymentSettings">
                <i class="fa-solid fa-spinner fa-spin" aria-hidden="true"></i> {{ t('admin.payment.savingBtn') }}
              </ng-container>
            </button>
          </div>
        </form>
      </section>

      <!-- AUDIT LOGS SECTION -->
      <section class="glass-card audit-section">
        <div class="table-header">
          <div>
            <h3 class="section-title">{{ t('admin.audit.title') }}</h3>
            <span class="section-sub">{{ t('admin.audit.subtitle') }}</span>
          </div>
          <span class="badge badge-cyan">{{ t('admin.audit.countBadge', { count: logs.length }) }}</span>
        </div>

        <div class="table-responsive">
          <table class="admin-table">
            <thead>
              <tr>
                <th>{{ t('admin.audit.colTime') }}</th>
                <th>{{ t('admin.audit.colUser') }}</th>
                <th>{{ t('admin.audit.colAction') }}</th>
                <th>{{ t('admin.audit.colDetails') }}</th>
                <th>{{ t('admin.audit.colIp') }}</th>
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
    </ng-container>
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

    /* PAYMENT GATEWAY SETTINGS */
    .payment-settings-card {
      background: #111622;
      border: 1px solid #222b3d;
    }
    .pg-heading-icon { color: #f97316; margin-right: 6px; }
    .pg-source-pill {
      font-size: 0.73rem;
      font-weight: 600;
      padding: 4px 10px;
      border-radius: 6px;
      background: #141923;
      border: 1px solid #263042;
      color: #cbd5e1;
      white-space: nowrap;
    }
    .pg-form { display: flex; flex-direction: column; gap: 16px; }
    .pg-top-grid, .pg-fields-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(240px, 1fr));
      gap: 14px;
    }
    .pg-field { display: flex; flex-direction: column; gap: 6px; }
    .pg-field label { font-size: 0.78rem; font-weight: 600; color: #cbd5e1; }
    .pg-label-row { display: flex; align-items: center; justify-content: space-between; gap: 8px; }
    .pg-saved-badge {
      font-size: 0.68rem;
      font-weight: 600;
      color: #34d399;
      background: #0d1f18;
      border: 1px solid #166534;
      padding: 1px 7px;
      border-radius: 4px;
    }
    .pg-input {
      background: #0a0e17;
      border: 1px solid #263042;
      color: #f8fafc;
      padding: 9px 12px;
      border-radius: 6px;
      font-size: 0.85rem;
      min-height: 40px;
    }
    .pg-input:focus-visible { outline: 2px solid #f97316; outline-offset: 2px; }
    .pg-mono { font-family: 'JetBrains Mono', monospace; }
    .pg-fieldset {
      border: 1px solid #222b3d;
      border-radius: 8px;
      padding: 16px;
      background: #0b0f18;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .pg-fieldset legend {
      font-size: 0.82rem;
      font-weight: 700;
      color: #f97316;
      padding: 0 8px;
    }
    .pg-callback-notice {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      padding: 10px 12px;
      background: #141923;
      border: 1px solid #263042;
      border-radius: 6px;
      font-size: 0.78rem;
      color: #cbd5e1;
    }
    .pg-callback-notice i { color: #f97316; margin-top: 3px; }
    .pg-callback-notice code {
      display: block;
      margin-top: 4px;
      color: #f8fafc;
      font-family: 'JetBrains Mono', monospace;
      word-break: break-all;
    }
    .pg-footer-actions {
      display: flex;
      align-items: center;
      justify-content: space-between;
      flex-wrap: wrap;
      gap: 12px;
      padding-top: 4px;
    }
    .pg-hint-note { font-size: 0.75rem; color: #64748b; }
    .pg-btn-save {
      background: #f97316;
      color: #0b0f18;
      border: 1px solid #ea580c;
      border-radius: 6px;
      padding: 9px 18px;
      font-size: 0.85rem;
      font-weight: 700;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 8px;
    }
    .pg-btn-save:hover:not(:disabled) { background: #fb923c; }
    .pg-btn-save:disabled { opacity: 0.6; cursor: not-allowed; }

    @media (max-width: 767.98px) {
      .admin-header,
      .admin-tabs,
      .toolbar-row {
        flex-wrap: wrap;
        gap: 8px;
      }
      .admin-tabs {
        flex-wrap: nowrap;
        overflow-x: auto;
        -webkit-overflow-scrolling: touch;
        scrollbar-width: none;
      }
      .admin-tabs::-webkit-scrollbar { display: none; }
      .table-wrap,
      .admin-table-wrap {
        overflow-x: auto;
        -webkit-overflow-scrolling: touch;
        max-width: 100%;
      }
      .kpi-grid,
      .stats-grid {
        grid-template-columns: 1fr !important;
      }
    }
  `]
})
export class AdminComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  apiService = inject(EtsyApiService);
  subscriptionService = inject(SubscriptionService);

  users: User[] = [];
  plans: SubscriptionPlan[] = [];
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

  paymentSettings: PaymentGatewaySettings | null = null;
  savingPaymentSettings = false;
  paymentForm = {
    activeProvider: 'Unconfigured',
    isSandbox: true,
    iyzicoApiKey: '',
    iyzicoSecretKey: '',
    iyzicoBaseUrl: 'https://sandbox-api.iyzipay.com',
    paytrMerchantId: '',
    paytrMerchantKey: '',
    paytrMerchantSalt: '',
    stripeSecretKey: ''
  };

  get paytrCallbackUrl(): string {
    return `${window.location.origin}/api/subscriptions/callback/paytr`;
  }

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
    this.apiService.getAdminUsers().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
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

    this.apiService.getAuditLogs().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => this.logs = res,
      error: () => {}
    });

    this.apiService.getSystemStats().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => this.stats = res,
      error: () => {}
    });

    this.subscriptionService.loadPlans().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => this.plans = res.map(p => ({ ...p })),
      error: () => {}
    });

    this.subscriptionService.getAdminPaymentSettings().pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: cfg => this.hydratePaymentForm(cfg),
      error: () => {}
    });
  }

  onSandboxChanged(isSandbox: boolean): void {
    this.paymentForm.isSandbox = isSandbox;
    const url = (this.paymentForm.iyzicoBaseUrl || '').trim();
    if (!url || url === 'https://sandbox-api.iyzipay.com' || url === 'https://api.iyzipay.com') {
      this.paymentForm.iyzicoBaseUrl = isSandbox ? 'https://sandbox-api.iyzipay.com' : 'https://api.iyzipay.com';
    }
  }

  savePaymentGatewaySettings(): void {
    if (this.savingPaymentSettings) return;
    this.savingPaymentSettings = true;

    this.subscriptionService.updateAdminPaymentSettings({
      activeProvider: this.paymentForm.activeProvider,
      isSandbox: this.paymentForm.isSandbox,
      iyzicoApiKey: this.paymentForm.iyzicoApiKey,
      iyzicoSecretKey: this.paymentForm.iyzicoSecretKey || undefined,
      iyzicoBaseUrl: this.paymentForm.iyzicoBaseUrl,
      paytrMerchantId: this.paymentForm.paytrMerchantId,
      paytrMerchantKey: this.paymentForm.paytrMerchantKey || undefined,
      paytrMerchantSalt: this.paymentForm.paytrMerchantSalt || undefined,
      stripeSecretKey: this.paymentForm.stripeSecretKey || undefined
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: saved => {
        this.savingPaymentSettings = false;
        this.hydratePaymentForm(saved);
        this.showToast(this.transloco.translate('admin.toast.paymentSaved', { provider: saved.activeProvider }), 'success');
      },
      error: err => {
        this.savingPaymentSettings = false;
        const msg = err?.error?.message || err?.error?.error || err?.message || this.transloco.translate('admin.toast.paymentSaveDefaultError');
        this.showToast(this.transloco.translate('admin.toast.paymentSaveError', { message: msg }), 'error');
      }
    });
  }

  private hydratePaymentForm(cfg: PaymentGatewaySettings): void {
    this.paymentSettings = cfg;
    this.paymentForm = {
      activeProvider: cfg.activeProvider || 'Unconfigured',
      isSandbox: cfg.isSandbox,
      iyzicoApiKey: cfg.iyzicoApiKey || '',
      iyzicoSecretKey: '',
      iyzicoBaseUrl: cfg.iyzicoBaseUrl || (cfg.isSandbox ? 'https://sandbox-api.iyzipay.com' : 'https://api.iyzipay.com'),
      paytrMerchantId: cfg.paytrMerchantId || '',
      paytrMerchantKey: '',
      paytrMerchantSalt: '',
      stripeSecretKey: ''
    };
  }

  assignUserPlan(user: User, planId: string): void {
    this.subscriptionService.adminAssignUserSubscription(user.id, { planId }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: summary => {
        user.subscriptionPlanId = summary.planId;
        user.monthlyAiTokenQuota = summary.monthlyAiTokenQuota;
        const locale = this.transloco.getActiveLang() === 'tr' ? 'tr-TR' : 'en-US';
        this.showToast(this.transloco.translate('admin.toast.planAssigned', {
          username: user.username,
          planName: summary.planName,
          tokens: summary.monthlyAiTokenQuota.toLocaleString(locale)
        }), 'success');
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || this.transloco.translate('admin.toast.planAssignDefaultError');
        this.showToast(this.transloco.translate('admin.toast.planAssignError', { message: msg }), 'error');
      }
    });
  }

  savePlanConfig(plan: SubscriptionPlan): void {
    this.subscriptionService.adminUpdatePlan(plan.id, {
      name: plan.name,
      tagline: plan.tagline,
      badgeText: plan.badgeText,
      monthlyPriceUsd: Number(plan.monthlyPriceUsd),
      yearlyPriceUsd: Number(plan.yearlyPriceUsd),
      monthlyPriceTry: Number(plan.monthlyPriceTry),
      yearlyPriceTry: Number(plan.yearlyPriceTry),
      maxShops: Number(plan.maxShops),
      monthlyAiTokenQuota: Number(plan.monthlyAiTokenQuota),
      maxMonthlyAiListings: Number(plan.maxMonthlyAiListings),
      maxBatchQueueSize: Number(plan.maxBatchQueueSize),
      allowedFeatures: plan.allowedFeatures,
      isPopular: plan.isPopular,
      isActive: plan.isActive
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: updated => {
        this.showToast(this.transloco.translate('admin.toast.planUpdated', {
          name: updated.name,
          priceUsd: updated.monthlyPriceUsd,
          maxShops: updated.maxShops
        }), 'success');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || this.transloco.translate('admin.toast.planUpdateDefaultError');
        this.showToast(this.transloco.translate('admin.toast.planUpdateError', { message: msg }), 'error');
      }
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

    this.apiService.updateAdminUser(user.id, req).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        this.showToast(this.transloco.translate('admin.toast.userSaved', {
          username: user.username,
          role: user.role
        }), 'success');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.error || err?.message || this.transloco.translate('admin.toast.userSaveDefaultError');
        this.showToast(this.transloco.translate('admin.toast.userSaveError', { message: msg }), 'error');
      }
    });
  }

  toggleStatus(user: User): void {
    this.apiService.toggleUserStatus(user.id).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        user.isActive = res.isActive;
        const statusWord = this.transloco.translate(res.isActive ? 'admin.toast.statusActiveWord' : 'admin.toast.statusSuspendedWord');
        this.showToast(this.transloco.translate('admin.toast.userStatusToggled', {
          username: user.username,
          status: statusWord
        }), 'success');
        this.loadData();
      },
      error: (err) => {
        const msg = err?.error?.error || err?.message || this.transloco.translate('admin.toast.userStatusDefaultError');
        this.showToast(this.transloco.translate('admin.toast.userStatusError', { message: msg }), 'error');
      }
    });
  }
}
