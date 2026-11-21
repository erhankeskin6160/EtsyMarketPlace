import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, Router } from '@angular/router';
import { DomSanitizer, SafeResourceUrl } from '@angular/platform-browser';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { SubscriptionService } from '../../core/services/subscription.service';
import { AuthService } from '../../core/services/auth.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import {
  BillingCycle,
  CheckoutSessionResponse,
  SubscriptionPlan,
  SubscriptionTransaction,
  UserSubscriptionSummary
} from '../../core/models/subscription.models';

const ALLOWED_PAYMENT_IFRAME_ORIGINS = [
  'https://www.paytr.com',
  'https://sandbox-odeme.paytr.com',
  'https://sandbox-cpp.iyzipay.com',
  'https://cpp.iyzipay.com',
  'https://sandbox-api.iyzipay.com',
  'https://api.iyzipay.com'
];

@Component({
  selector: 'app-subscription-plans',
  standalone: true,
  imports: [CommonModule, TranslocoDirective],
  providers: [provideTranslocoScope('settings')],
  templateUrl: './subscription-plans.component.html',
  styleUrls: ['./subscription-plans.component.css']
})
export class SubscriptionPlansComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  private sanitizer = inject(DomSanitizer);
  private transloco = inject(TranslocoService);

  subscriptionService = inject(SubscriptionService);
  authService = inject(AuthService);
  apiService = inject(EtsyApiService);

  billingCycle: BillingCycle = 'Monthly';
  currency: 'USD' | 'TRY' = 'USD';

  upgradeFeatureKey: string | null = null;
  upgradeFeatureTitle = '';
  requiredPlanForUpgrade = 'pro';

  activeCheckoutSession: CheckoutSessionResponse | null = null;
  trustedCheckoutIframeUrl: SafeResourceUrl | null = null;
  startingCheckoutPlanId: string | null = null;
  isPaying = false;
  paymentError: string | null = null;

  statusMessage: string | null = null;
  statusType: 'success' | 'error' = 'success';

  readonly featureRows = [
    { key: 'fast_creator', labelKey: 'settings.subscription.features.fastCreator' },
    { key: 'ai_audit', labelKey: 'settings.subscription.features.aiAudit' },
    { key: 'shipping_hub', labelKey: 'settings.subscription.features.shippingHub' },
    { key: 'financial', labelKey: 'settings.subscription.features.financial' },
    { key: 'batch', labelKey: 'settings.subscription.features.batch' },
    { key: 'ai_image', labelKey: 'settings.subscription.features.aiImage' },
    { key: 'viral_3d', labelKey: 'settings.subscription.features.viral3d' },
    { key: 'ab_test', labelKey: 'settings.subscription.features.abTest' },
    { key: 'shop_vault', labelKey: 'settings.subscription.features.shopVault' },
    { key: 'desktop_client', labelKey: 'settings.subscription.features.desktopClient' },
    { key: 'mcp_api', labelKey: 'settings.subscription.features.mcpApi' }
  ];

  ngOnInit(): void {
    this.currency = this.apiService.isTryCurrency() ? 'TRY' : 'USD';

    this.route.queryParamMap.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      const upgrade = params.get('upgrade');
      if (upgrade) {
        this.upgradeFeatureKey = upgrade;
        this.upgradeFeatureTitle = this.subscriptionService.getFeatureDisplayName(upgrade);
        this.requiredPlanForUpgrade = this.subscriptionService.getMinimumPlanForFeature(upgrade);
      } else {
        this.upgradeFeatureKey = null;
      }

      const paymentStatus = params.get('payment_status');
      const providerParam = params.get('provider');
      if (paymentStatus === 'success') {
        this.activeCheckoutSession = null;
        this.trustedCheckoutIframeUrl = null;
        this.paymentError = null;
        this.statusType = 'success';
        this.statusMessage = providerParam
          ? this.transloco.translate('settings.subscription.messages.paymentSuccessProvider', { provider: providerParam })
          : this.transloco.translate('settings.subscription.messages.paymentSuccessGeneric');
        this.refreshAll();
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { payment_status: null, provider: null, sessionId: null, checkoutSessionId: null, upgrade: null },
          queryParamsHandling: 'merge'
        });
        return;
      }

      if (paymentStatus === 'failed') {
        this.paymentError = this.transloco.translate('settings.subscription.messages.paymentFailed');
        this.refreshAll();
      }

      const sessionId = params.get('checkoutSessionId');
      if (sessionId && (!this.activeCheckoutSession || this.activeCheckoutSession.sessionId !== sessionId)) {
        this.tryRestoreSessionFromTransactions(sessionId);
      }
    });

    this.refreshAll();
  }

  refreshAll(): void {
    this.subscriptionService.loadPlans().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
    this.subscriptionService.fetchMySubscription().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
    this.subscriptionService.loadTransactions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe(txs => {
      const sessionId = this.route.snapshot.queryParamMap.get('checkoutSessionId');
      if (sessionId && !this.activeCheckoutSession) {
        const match = txs.find(t => t.sessionId === sessionId && t.status === 'Pending');
        if (match) {
          this.activeCheckoutSession = this.toCheckoutResponse(match);
          this.trustedCheckoutIframeUrl = null;
        }
      }
    });
  }

  isCurrentPlan(planId: string): boolean {
    const sub = this.subscriptionService.mySubscription();
    if (!sub || sub.isAdminExempt) return false;
    return sub.planId.toLowerCase() === planId.toLowerCase() && sub.status === 'Active';
  }

  isRecommendedUpgrade(planId: string): boolean {
    return !!this.upgradeFeatureKey &&
      this.requiredPlanForUpgrade.toLowerCase() === planId.toLowerCase();
  }

  planIncludesFeature(plan: SubscriptionPlan, featureKey: string): boolean {
    const normalized = this.subscriptionService.normalizeFeatureKey(featureKey);
    return (plan.allowedFeatures || []).some(f => f.toLowerCase() === normalized);
  }

  getShopUsagePct(sub: UserSubscriptionSummary): number {
    if (!sub.maxShops || sub.maxShops <= 0) return 0;
    return Math.min(100, Math.round((sub.assignedShopsCount / sub.maxShops) * 100));
  }

  formatPlanPrice(plan: SubscriptionPlan): string {
    if (this.currency === 'TRY') {
      const val = this.billingCycle === 'Yearly' ? plan.yearlyPriceTry : plan.monthlyPriceTry;
      return `₺${val.toLocaleString('tr-TR', { maximumFractionDigits: 0 })}`;
    }
    const val = this.billingCycle === 'Yearly' ? plan.yearlyPriceUsd : plan.monthlyPriceUsd;
    return `$${val.toLocaleString('en-US', { maximumFractionDigits: 0 })}`;
  }

  formatMonthlyEquivalent(plan: SubscriptionPlan): string {
    const unit = this.transloco.translate('settings.subscription.plans.perMonthShort');
    if (this.currency === 'TRY') {
      const monthly = Math.round(plan.yearlyPriceTry / 12);
      return `₺${monthly.toLocaleString('tr-TR')}/${unit}`;
    }
    const monthly = Math.round(plan.yearlyPriceUsd / 12);
    return `$${monthly}/${unit}`;
  }

  formatSessionAmount(session: CheckoutSessionResponse): string {
    if (session.currency === 'TRY') {
      return `₺${session.amountTry.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }
    return `$${session.amountUsd.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  getProviderDisplayLabel(provider: string): string {
    switch ((provider || '').toLowerCase()) {
      case 'paytr': return this.transloco.translate('settings.subscription.providers.paytr');
      case 'iyzico': return this.transloco.translate('settings.subscription.providers.iyzico');
      case 'stripe': return this.transloco.translate('settings.subscription.providers.stripe');
      default: return this.transloco.translate('settings.subscription.providers.unconfigured');
    }
  }

  startCheckout(plan: SubscriptionPlan): void {
    this.startingCheckoutPlanId = plan.id;
    this.paymentError = null;
    this.statusMessage = null;
    this.trustedCheckoutIframeUrl = null;

    this.subscriptionService.createCheckoutSession({
      planId: plan.id,
      billingCycle: this.billingCycle,
      currency: this.currency,
      returnBaseUrl: window.location.origin
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.startingCheckoutPlanId = null;

        if (res.mode === 'StripeLive') {
          if (this.isAllowedExternalPaymentUrl(res.checkoutUrl, ['https://checkout.stripe.com'])) {
            window.location.href = res.checkoutUrl;
            return;
          }
          this.statusType = 'error';
          this.statusMessage = this.transloco.translate('settings.subscription.messages.externalUrlBlocked');
          return;
        }

        if (res.mode === 'Iframe') {
          const trusted = this.resolveTrustedIframeUrl(res.checkoutUrl);
          if (!trusted) {
            this.statusType = 'error';
            this.statusMessage = this.transloco.translate('settings.subscription.messages.iframeOriginMismatch', { provider: res.provider });
            return;
          }
          this.activeCheckoutSession = res;
          this.trustedCheckoutIframeUrl = trusted;
        } else {
          this.activeCheckoutSession = res;
          this.trustedCheckoutIframeUrl = null;
        }

        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { checkoutSessionId: res.sessionId },
          queryParamsHandling: 'merge'
        });
        this.subscriptionService.loadTransactions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
        setTimeout(() => {
          document.getElementById('stripe-vpos-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
        }, 50);
      },
      error: err => {
        this.startingCheckoutPlanId = null;
        this.statusType = 'error';
        this.statusMessage = err?.error?.message || this.transloco.translate('settings.subscription.messages.checkoutStartFailed');
      }
    });
  }

  confirmAdminTestActivation(): void {
    if (!this.activeCheckoutSession || this.isPaying || !this.authService.isAdmin()) return;

    this.isPaying = true;
    this.paymentError = null;

    this.subscriptionService.confirmAdminTest(this.activeCheckoutSession.sessionId)
      .pipe(takeUntilDestroyed(this.destroyRef))
      .subscribe({
        next: res => {
          this.isPaying = false;
          this.activeCheckoutSession = null;
          this.trustedCheckoutIframeUrl = null;
          this.upgradeFeatureKey = null;
          this.statusType = 'success';
          this.statusMessage = this.transloco.translate('settings.subscription.messages.adminTestActivated', {
            message: res.message,
            invoice: res.transaction.invoiceNumber
          });
          this.router.navigate([], {
            relativeTo: this.route,
            queryParams: { checkoutSessionId: null, upgrade: null },
            queryParamsHandling: 'merge'
          });
          this.subscriptionService.loadTransactions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
        },
        error: err => {
          this.isPaying = false;
          this.paymentError = err?.error?.message || err?.message || this.transloco.translate('settings.subscription.messages.adminTestFailed');
        }
      });
  }

  goToAdminPaymentSettings(): void {
    this.router.navigate(['/admin']);
  }

  resumePendingTransaction(tx: SubscriptionTransaction): void {
    this.activeCheckoutSession = this.toCheckoutResponse(tx);
    this.trustedCheckoutIframeUrl = null;
    this.paymentError = null;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { checkoutSessionId: tx.sessionId },
      queryParamsHandling: 'merge'
    });
    setTimeout(() => {
      document.getElementById('stripe-vpos-panel')?.scrollIntoView({ behavior: 'smooth', block: 'start' });
    }, 50);
  }

  cancelCheckout(): void {
    this.activeCheckoutSession = null;
    this.trustedCheckoutIframeUrl = null;
    this.paymentError = null;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { checkoutSessionId: null },
      queryParamsHandling: 'merge'
    });
  }

  clearUpgradeNotice(): void {
    this.upgradeFeatureKey = null;
    this.router.navigate([], {
      relativeTo: this.route,
      queryParams: { upgrade: null },
      queryParamsHandling: 'merge'
    });
  }

  private resolveTrustedIframeUrl(rawUrl: string): SafeResourceUrl | null {
    if (!this.isAllowedExternalPaymentUrl(rawUrl, ALLOWED_PAYMENT_IFRAME_ORIGINS)) {
      return null;
    }
    return this.sanitizer.bypassSecurityTrustResourceUrl(rawUrl);
  }

  private isAllowedExternalPaymentUrl(rawUrl: string, allowlist: string[]): boolean {
    try {
      const candidateUrl = new URL(rawUrl);
      if (candidateUrl.protocol !== 'https:' || candidateUrl.origin === 'null') {
        return false;
      }

      const originCtor = (globalThis as unknown as { Origin?: { from(value: string): { isSameOrigin(other: unknown): boolean } } }).Origin;
      if (originCtor) {
        const candidateOrigin = originCtor.from(rawUrl);
        return allowlist.some(allowed => candidateOrigin.isSameOrigin(originCtor.from(allowed)));
      }

      return allowlist.some(allowed => new URL(allowed).origin === candidateUrl.origin);
    } catch {
      return false;
    }
  }

  private tryRestoreSessionFromTransactions(sessionId: string): void {
    const match = this.subscriptionService.transactions().find(t => t.sessionId === sessionId && t.status === 'Pending');
    if (match) {
      this.activeCheckoutSession = this.toCheckoutResponse(match);
      this.trustedCheckoutIframeUrl = null;
    }
  }

  private toCheckoutResponse(tx: SubscriptionTransaction): CheckoutSessionResponse {
    return {
      sessionId: tx.sessionId,
      checkoutUrl: `/settings/subscription?checkoutSessionId=${tx.sessionId}`,
      mode: 'VirtualPos',
      provider: tx.paymentProvider,
      planId: tx.planId,
      planName: tx.planName,
      billingCycle: tx.billingCycle,
      amountUsd: tx.amountUsd,
      amountTry: tx.amountTry,
      currency: tx.currency,
      invoiceNumber: tx.invoiceNumber,
      expiresAtUtc: tx.createdAtUtc
    };
  }
}
