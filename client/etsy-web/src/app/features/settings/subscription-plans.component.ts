import { Component, DestroyRef, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { SubscriptionService } from '../../core/services/subscription.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import {
  BillingCycle,
  CheckoutSessionResponse,
  SubscriptionPlan,
  SubscriptionTransaction,
  UserSubscriptionSummary
} from '../../core/models/subscription.models';

@Component({
  selector: 'app-subscription-plans',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './subscription-plans.component.html',
  styleUrls: ['./subscription-plans.component.css']
})
export class SubscriptionPlansComponent implements OnInit {
  private route = inject(ActivatedRoute);
  private router = inject(Router);
  private destroyRef = inject(DestroyRef);
  subscriptionService = inject(SubscriptionService);
  apiService = inject(EtsyApiService);

  billingCycle: BillingCycle = 'Monthly';
  currency: 'USD' | 'TRY' = 'USD';

  upgradeFeatureKey: string | null = null;
  upgradeFeatureTitle = '';
  requiredPlanForUpgrade = 'pro';

  activeCheckoutSession: CheckoutSessionResponse | null = null;
  startingCheckoutPlanId: string | null = null;
  isPaying = false;
  paymentError: string | null = null;

  statusMessage: string | null = null;
  statusType: 'success' | 'error' = 'success';

  cardForm = {
    cardHolderName: '',
    cardNumber: '',
    expiry: '',
    cvc: ''
  };
  termsAccepted = true;

  readonly featureRows = [
    { key: 'fast_creator', label: 'AI Hızlı Ürün Ekle (13 Etiket + SEO)' },
    { key: 'ai_audit', label: 'Mağaza AI Analizi & Canlı Güncelleme' },
    { key: 'shipping_hub', label: '4 Taşıyıcılı Kargo Hub & GTİP Barkod' },
    { key: 'financial', label: 'Finansal Raporlama & TDHP Muhasebe' },
    { key: 'batch', label: 'Toplu İşlem Kuyruğu (Batch Queue)' },
    { key: 'ai_image', label: 'AI Görsel Stüdyosu (Dekupe & Mockup)' },
    { key: 'viral_3d', label: 'Viral 3D Model Avcısı & Analizi' },
    { key: 'ab_test', label: 'Listing A/B Test Paneli' },
    { key: 'shop_vault', label: 'ShopVault (.etsyvault Şifreli Yedek)' },
    { key: 'desktop_client', label: 'WinForms Masaüstü (ZPL + EXIF Kırma)' },
    { key: 'mcp_api', label: 'MCP API & Kurumsal Çoklu Operatör' }
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
    if (this.currency === 'TRY') {
      const monthly = Math.round(plan.yearlyPriceTry / 12);
      return `₺${monthly.toLocaleString('tr-TR')}/ay`;
    }
    const monthly = Math.round(plan.yearlyPriceUsd / 12);
    return `$${monthly}/ay`;
  }

  formatSessionAmount(session: CheckoutSessionResponse): string {
    if (session.currency === 'TRY') {
      return `₺${session.amountTry.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }
    return `$${session.amountUsd.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  startCheckout(plan: SubscriptionPlan): void {
    this.startingCheckoutPlanId = plan.id;
    this.paymentError = null;
    this.statusMessage = null;

    this.subscriptionService.createCheckoutSession({
      planId: plan.id,
      billingCycle: this.billingCycle,
      currency: this.currency,
      returnBaseUrl: window.location.origin
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.startingCheckoutPlanId = null;
        if (res.mode === 'StripeLive' && res.checkoutUrl.startsWith('http')) {
          window.location.href = res.checkoutUrl;
          return;
        }
        this.activeCheckoutSession = res;
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
        this.statusMessage = err?.error?.message || 'Stripe Sanal POS oturumu başlatılamadı.';
      }
    });
  }

  resumePendingTransaction(tx: SubscriptionTransaction): void {
    this.activeCheckoutSession = this.toCheckoutResponse(tx);
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

  detectedCardBrand(): 'visa' | 'mastercard' | 'troy' | 'amex' | 'unknown' {
    const digits = (this.cardForm.cardNumber || '').replace(/\D/g, '');
    if (!digits) return 'unknown';
    if (digits.startsWith('9792') || digits.startsWith('650')) return 'troy';
    if (digits.startsWith('34') || digits.startsWith('37')) return 'amex';
    if (digits.startsWith('4')) return 'visa';
    if (/^(5[1-5]|222[1-9]|22[3-9]|2[3-6]|27[01]|2720)/.test(digits)) return 'mastercard';
    return 'unknown';
  }

  fillTestCard(kind: 'success' | 'troy' | 'decline'): void {
    this.paymentError = null;
    if (kind === 'success') {
      this.cardForm = {
        cardHolderName: this.subscriptionService.mySubscription()?.username || 'Etsy Satıcı',
        cardNumber: '4242 4242 4242 4242',
        expiry: '12/28',
        cvc: '424'
      };
    } else if (kind === 'troy') {
      this.cardForm = {
        cardHolderName: this.subscriptionService.mySubscription()?.username || 'TROY Test Kullanıcı',
        cardNumber: '9792 0000 0000 0003',
        expiry: '12/28',
        cvc: '792'
      };
    } else {
      this.cardForm = {
        cardHolderName: this.subscriptionService.mySubscription()?.username || 'Test Kullanıcı',
        cardNumber: '4000 0000 0000 0002',
        expiry: '12/28',
        cvc: '123'
      };
    }
  }

  submitVirtualPosPayment(): void {
    if (!this.activeCheckoutSession || this.isPaying) return;

    if (!this.termsAccepted) {
      this.paymentError = 'Lütfen Mesafeli Satış Sözleşmesi, Ön Bilgilendirme ve Abonelik İptal/İade Koşullarını onaylayınız.';
      return;
    }

    const { month, year } = this.parseExpiry(this.cardForm.expiry);
    if (!this.cardForm.cardHolderName.trim() || !this.cardForm.cardNumber.trim() || !month || !year || !this.cardForm.cvc.trim()) {
      this.paymentError = 'Lütfen tüm kart alanlarını eksiksiz ve AA/YY formatında doldurunuz.';
      return;
    }

    this.isPaying = true;
    this.paymentError = null;

    this.subscriptionService.confirmPayment({
      sessionId: this.activeCheckoutSession.sessionId,
      cardHolderName: this.cardForm.cardHolderName.trim(),
      cardNumber: this.cardForm.cardNumber.trim(),
      expiryMonth: month,
      expiryYear: year,
      cvc: this.cardForm.cvc.trim()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: res => {
        this.isPaying = false;
        this.activeCheckoutSession = null;
        this.upgradeFeatureKey = null;
        this.statusType = 'success';
        this.statusMessage = `✓ ${res.message} (Fatura: ${res.transaction.invoiceNumber}, Kart: ${res.transaction.cardMaskedNumber || 'Onaylı'})`;
        this.cardForm = { cardHolderName: '', cardNumber: '', expiry: '', cvc: '' };
        this.router.navigate([], {
          relativeTo: this.route,
          queryParams: { checkoutSessionId: null, upgrade: null },
          queryParamsHandling: 'merge'
        });
        this.subscriptionService.loadTransactions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
      },
      error: err => {
        this.isPaying = false;
        this.paymentError = err?.error?.message || err?.message || 'Kart provizyonu onaylanamadı.';
        this.subscriptionService.loadTransactions().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
      }
    });
  }

  cancelCheckout(): void {
    this.activeCheckoutSession = null;
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

  clearPaymentError(): void {
    this.paymentError = null;
  }

  private tryRestoreSessionFromTransactions(sessionId: string): void {
    const match = this.subscriptionService.transactions().find(t => t.sessionId === sessionId && t.status === 'Pending');
    if (match) {
      this.activeCheckoutSession = this.toCheckoutResponse(match);
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

  private parseExpiry(raw: string): { month: string; year: string } {
    const cleaned = (raw || '').replace(/\s+/g, '');
    const parts = cleaned.split('/');
    if (parts.length === 2) {
      return { month: parts[0], year: parts[1] };
    }
    const digits = cleaned.replace(/\D/g, '');
    if (digits.length === 4) {
      return { month: digits.slice(0, 2), year: digits.slice(2, 4) };
    }
    if (digits.length === 6) {
      return { month: digits.slice(0, 2), year: digits.slice(2, 6) };
    }
    return { month: '', year: '' };
  }
}
