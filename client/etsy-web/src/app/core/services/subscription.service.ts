import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, catchError, map, of, tap } from 'rxjs';
import { environment } from '../../../environments/environment';
import { AuthService } from './auth.service';
import {
  AdminAssignSubscriptionRequest,
  AdminUpdateSubscriptionPlanRequest,
  CheckoutSessionResponse,
  ConfirmPaymentResponse,
  ConfirmVirtualPosPaymentRequest,
  CreateCheckoutSessionRequest,
  FeatureAccessCheck,
  SubscriptionPlan,
  SubscriptionTransaction,
  UserSubscriptionSummary
} from '../models/subscription.models';

const FEATURE_ALIAS_MAP: Record<string, string> = {
  aras: 'shipping_hub',
  shipentegra: 'shipping_hub',
  navlungo: 'shipping_hub',
  shiptomore: 'shipping_hub',
  shipping: 'shipping_hub',
  tdhp_accounting: 'financial',
  accounting: 'financial',
  ai_studio: 'ai_image',
  vault: 'shop_vault',
  orders: 'orders_shipping'
};

const FEATURE_MIN_PLAN_MAP: Record<string, 'starter' | 'growth' | 'pro' | 'enterprise'> = {
  competitor_spy: 'growth',
  external: 'growth',
  ai_audit: 'growth',
  financial: 'growth',
  financial_ai: 'growth',
  automation: 'growth',
  batch: 'growth',
  shipping_hub: 'growth',
  ai_image: 'pro',
  viral_3d: 'pro',
  ab_test: 'pro',
  shop_vault: 'pro',
  desktop_client: 'pro',
  mcp_api: 'enterprise'
};

export const FEATURE_DISPLAY_NAMES: Record<string, string> = {
  competitor_spy: 'Rakip & Trend Casusu',
  external: 'Dış Pazar Yeri Bulucu',
  ai_audit: 'Mağaza AI Analizi (Audit Studio)',
  financial: 'Finansal Raporlama & TDHP Muhasebe',
  financial_ai: 'Finansal AI Analiz & Tahmin',
  automation: 'Otomasyon Zamanlayıcı',
  batch: 'Toplu İşlem Kuyruğu (Batch Queue)',
  shipping_hub: '4 Taşıyıcılı Kargo Hub & Barkod',
  aras: 'Aras Global Kargo Entegrasyonu',
  shipentegra: 'ShipEntegra Kargo Entegrasyonu',
  navlungo: 'Navlungo Kargo Entegrasyonu',
  shiptomore: 'Shiptomore Kargo Entegrasyonu',
  ai_image: 'AI Görsel Stüdyosu (Dekupe & Mockup)',
  viral_3d: 'Viral 3D Model Avcısı',
  ab_test: 'A/B Test Paneli',
  shop_vault: 'Mağaza Yedek & Transfer (.etsyvault)',
  desktop_client: 'WinForms Masaüstü & Termal Yazıcı',
  mcp_api: 'MCP API & Çoklu Operatör'
};

@Injectable({
  providedIn: 'root'
})
export class SubscriptionService {
  private http = inject(HttpClient);
  private authService = inject(AuthService);

  private readonly API_BASE = `${environment.apiBaseUrl}/api/subscriptions`;
  private readonly ADMIN_BASE = `${environment.apiBaseUrl}/api/admin`;

  readonly plans = signal<SubscriptionPlan[]>([]);
  readonly mySubscription = signal<UserSubscriptionSummary | null>(null);
  readonly transactions = signal<SubscriptionTransaction[]>([]);
  readonly loadingPlans = signal<boolean>(false);
  readonly loadingSummary = signal<boolean>(false);

  readonly activePlanId = computed(() => {
    const sub = this.mySubscription();
    if (sub?.planId) return sub.planId.toLowerCase();
    return (this.authService.currentUser()?.subscriptionPlanId || 'pro').toLowerCase();
  });

  readonly activePlanBadgeLabel = computed(() => {
    const sub = this.mySubscription();
    if (sub) {
      return sub.planName;
    }
    const id = this.activePlanId();
    return id.charAt(0).toUpperCase() + id.slice(1);
  });

  normalizeFeatureKey(featureId: string): string {
    const key = (featureId || '').trim().toLowerCase();
    return FEATURE_ALIAS_MAP[key] || key;
  }

  getMinimumPlanForFeature(featureId: string): 'starter' | 'growth' | 'pro' | 'enterprise' {
    const normalized = this.normalizeFeatureKey(featureId);
    return FEATURE_MIN_PLAN_MAP[normalized] || 'starter';
  }

  getRequiredPlanBadge(featureId: string): string | null {
    if (this.isFeatureAllowed(featureId)) return null;
    const minPlan = this.getMinimumPlanForFeature(featureId);
    return minPlan.toUpperCase();
  }

  getFeatureDisplayName(featureId: string): string {
    const raw = (featureId || '').trim().toLowerCase();
    return FEATURE_DISPLAY_NAMES[raw] || FEATURE_DISPLAY_NAMES[this.normalizeFeatureKey(raw)] || featureId;
  }

  isFeatureAllowed(featureId: string): boolean {
    if (this.authService.isAdmin()) return true;
    const sub = this.mySubscription();
    if (!sub) {
      // Henüz abonelik özeti yüklenmediyse kullanıcının token'ındaki paket düzeyine göre geçici izin ver
      const planId = this.activePlanId();
      const minPlan = this.getMinimumPlanForFeature(featureId);
      return this.planRank(planId) >= this.planRank(minPlan);
    }
    if (sub.isAdminExempt) return true;
    const normalized = this.normalizeFeatureKey(featureId);
    return sub.allowedFeatures.some(f => f.toLowerCase() === normalized);
  }

  loadPlans(): Observable<SubscriptionPlan[]> {
    this.loadingPlans.set(true);
    return this.http.get<SubscriptionPlan[]>(`${this.API_BASE}/plans`).pipe(
      tap(res => {
        this.plans.set(res || []);
        this.loadingPlans.set(false);
      }),
      catchError(() => {
        this.loadingPlans.set(false);
        return of([]);
      })
    );
  }

  fetchMySubscription(): Observable<UserSubscriptionSummary | null> {
    if (!this.authService.token()) {
      this.mySubscription.set(null);
      return of(null);
    }
    this.loadingSummary.set(true);
    return this.http.get<UserSubscriptionSummary>(`${this.API_BASE}/me`).pipe(
      tap(res => {
        this.mySubscription.set(res);
        this.loadingSummary.set(false);
      }),
      catchError(() => {
        this.loadingSummary.set(false);
        return of(null);
      })
    );
  }

  ensureSubscriptionLoaded(): Observable<UserSubscriptionSummary | null> {
    const current = this.mySubscription();
    if (current) return of(current);
    return this.fetchMySubscription();
  }

  loadTransactions(limit: number = 50): Observable<SubscriptionTransaction[]> {
    return this.http.get<SubscriptionTransaction[]>(`${this.API_BASE}/transactions?limit=${limit}`).pipe(
      tap(res => this.transactions.set(res || [])),
      catchError(() => of([]))
    );
  }

  createCheckoutSession(req: CreateCheckoutSessionRequest): Observable<CheckoutSessionResponse> {
    return this.http.post<CheckoutSessionResponse>(`${this.API_BASE}/checkout-session`, req);
  }

  confirmPayment(req: ConfirmVirtualPosPaymentRequest): Observable<ConfirmPaymentResponse> {
    return this.http.post<ConfirmPaymentResponse>(`${this.API_BASE}/confirm-payment`, req).pipe(
      tap(res => {
        if (res.success && res.subscription) {
          this.mySubscription.set(res.subscription);
        }
        if (res.success && res.refreshedToken && res.user) {
          this.authService.setSession(res.refreshedToken, res.user);
        }
      })
    );
  }

  checkFeature(featureKey: string): Observable<FeatureAccessCheck> {
    return this.http.get<FeatureAccessCheck>(`${this.API_BASE}/check-feature/${encodeURIComponent(featureKey)}`);
  }

  adminAssignUserSubscription(userId: string, req: AdminAssignSubscriptionRequest): Observable<UserSubscriptionSummary> {
    return this.http.post<UserSubscriptionSummary>(`${this.ADMIN_BASE}/users/${encodeURIComponent(userId)}/subscription`, req);
  }

  adminUpdatePlan(planId: string, req: AdminUpdateSubscriptionPlanRequest): Observable<SubscriptionPlan> {
    return this.http.put<SubscriptionPlan>(`${this.ADMIN_BASE}/subscriptions/plans/${encodeURIComponent(planId)}`, req);
  }

  getPlanEmoji(_planId: string): string {
    return '';
  }

  private planRank(planId: string): number {
    switch ((planId || '').toLowerCase()) {
      case 'starter': return 1;
      case 'growth': return 2;
      case 'pro': return 3;
      case 'enterprise': return 4;
      default: return 3;
    }
  }
}
