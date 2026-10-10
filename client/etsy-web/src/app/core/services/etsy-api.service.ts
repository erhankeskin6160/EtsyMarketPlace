import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DailyBrief, FinancialSummary, BankDeposit } from '../models/etsy.models';
import { User, UpdateUserRequest, AuditLog, SystemStats } from '../models/auth.models';
import { environment } from '../../../environments/environment';

/** VDS: EtsyOrderCostAlert (camelCase JSON) */
export interface EtsyOrderCostAlertDto {
  shopId: string;
  orderId: string;
  createdAt: string;
  currency: string;
  orderTotal: number;
  productCost: number | null;
  shippingCost: number | null;
  reason: string;
}

/** VDS: FinancialPerformance */
export interface FinancialPerformanceDto {
  startDate: string;
  endDate: string;
  currency: string;
  grossSales: number;
  platformFees: number;
  internalAdsCost: number;
  externalAdsCost: number;
  productCosts: number;
  shippingCosts: number;
  refunds: number;
  netProfit: number;
  netProfitMargin: number;
}

/** VDS: EtsyBankPayout */
export interface EtsyBankPayoutDto {
  shopId: string;
  referenceId: string;
  occurredAt: string;
  amount: number;
  currency: string;
  exchangeRateToTry: number | null;
  status: string;
  description: string;
}

/** VDS: EtsySyncResult */
export interface EtsySyncResultDto {
  shopId: string;
  startedAt: string;
  completedAt: string;
  payoutCount: number;
  transactionCount: number;
  orderCount: number;
  succeeded: boolean;
  errorMessage: string | null;
}

/** VDS: EtsyTokenStatus */
export interface EtsyTokenStatusDto {
  exists: boolean;
  shopId: string;
  expiresAt: string | null;
  isExpired: boolean;
  tokenType?: string;
  message?: string;
  saved?: boolean;
}

export interface SavedListingAuditDto {
  id: number;
  shopId: string;
  listingId: string;
  originalTitle: string;
  originalTagsJson: string;
  originalDescription: string;
  optimizedTitle: string;
  optimizedTagsJson: string;
  optimizedDescription: string;
  aiModel: string;
  seoScoreBefore: number;
  seoScoreAfter: number;
  structuralIssuesJson?: string;
  riskWarningsJson?: string;
  checklistJson?: string;
  isAppliedToEtsy: boolean;
  appliedAt?: string | null;
  createdAt: string;
}

export interface ShopListingItemDto {
  listingId: number;
  title: string;
  description: string;
  state?: string;
  status?: string;
  quantity: number;
  numFavorers?: number;
  favorites?: number;
  views: number;
  tags: string[];
  materials: string[];
  primaryImageUrl?: string;
  thumbnail?: string;
  images?: string[];
  price?: number;
  priceAmount: number;
  currency?: string;
  currencyCode: string;
  seoScore: number;
  structuralNeeds: string[];
  seoNeeds?: string;
  seoStrengths?: string;
  riskWarnings?: string[];
  aiScore?: number | null;
  hasSavedAudit: boolean;
  isAiAudited?: boolean;
  resultJson?: string | null;
  savedAudit?: SavedListingAuditDto | any | null;
}

export interface OptimizeListingResponseDto {
  success: boolean;
  listingId: string;
  optimizedTitle: string;
  optimizedTags: string[];
  optimizedDescription: string;
  aiModel: string;
  provider?: string;
  model?: string;
  seoScoreBefore: number;
  seoScoreAfter: number;
  critique?: string;
  structuralIssues: string[];
  riskWarnings: string[];
  checklist: string[];
  message?: string;
}

@Injectable({
  providedIn: 'root'
})
export class EtsyApiService {
  private http = inject(HttpClient);
  private readonly API_BASE = environment.apiBaseUrl;

  // Global UI Signals
  readonly activeShopId = signal<string>(environment.defaultShopId);
  readonly exchangeRate = signal<number>(49.12);
  readonly isTryCurrency = signal<boolean>(true);
  readonly isSidebarCollapsed = signal<boolean>(false);

  // Live Token & API Connection Signals
  readonly tokenStatus = signal<'connected' | 'expired' | 'missing' | 'error' | 'checking'>('checking');
  readonly tokenExpiresAt = signal<string | null>(null);
  readonly tokenDetails = signal<string>('Kontrol ediliyor...');

  constructor() {
    this.fetchLiveExchangeRate();
    this.verifyApiConnection();
  }

  /** Checks live Etsy OAuth v3 token validity on VDS and updates reactive signals */
  verifyApiConnection(): void {
    this.tokenStatus.set('checking');
    this.checkTokenStatus().subscribe({
      next: (res) => {
        this.tokenExpiresAt.set(res.expiresAt);
        if (res.exists && !res.isExpired) {
          this.tokenStatus.set('connected');
        } else if (res.exists && res.isExpired) {
          // Access token dolmuşsa kullanıcıyı rahatsız etmeden önce sessizce Etsy'den yenilemeyi dene
          this.refreshToken(res.shopId).subscribe({
            next: (refreshRes) => {
              if (refreshRes && !refreshRes.isExpired) {
                this.tokenStatus.set('connected');
                this.tokenExpiresAt.set(refreshRes.expiresAt);
                this.tokenDetails.set(`Etsy v3 OAuth Bağlı (${res.shopId})`);
              } else {
                this.tokenStatus.set('expired');
                this.tokenDetails.set(`Yeniden Yetki Gerekli (${res.expiresAt ? res.expiresAt.slice(0, 10) : ''})`);
              }
            },
            error: () => {
              this.tokenStatus.set('expired');
              this.tokenDetails.set(`Yeniden Yetki Gerekli (${res.expiresAt ? res.expiresAt.slice(0, 10) : ''})`);
            }
          });
        } else {
          this.tokenStatus.set('missing');
          this.tokenDetails.set('Etsy API Bağlı Değil');
        }
      },
      error: (err) => {
        if (err.status === 404) {
          this.tokenStatus.set('missing');
          this.tokenDetails.set('Etsy API Bağlı Değil');
        } else {
          this.tokenStatus.set('error');
          this.tokenDetails.set('VDS API Sunucusuna Erişilemiyor');
        }
      }
    });
  }

  checkTokenStatus(shopId?: string): Observable<EtsyTokenStatusDto> {
    const id = shopId || this.activeShopId();
    return this.http.get<EtsyTokenStatusDto>(`${this.API_BASE}/api/etsy/token/status?shopId=${id}`);
  }

  refreshToken(shopId?: string): Observable<any> {
    const id = shopId || this.activeShopId();
    return this.http.post<any>(`${this.API_BASE}/api/etsy/token/refresh?shopId=${id}`, {});
  }

  fetchLiveExchangeRate(): void {
    // 1. Primary API: open.er-api.com
    fetch('https://open.er-api.com/v6/latest/USD')
      .then(res => res.json())
      .then(data => {
        const rate = data?.rates?.TRY;
        if (rate && typeof rate === 'number' && rate > 20) {
          this.exchangeRate.set(Number(rate.toFixed(2)));
        }
      })
      .catch(() => {
        // 2. Secondary API: api.frankfurter.app
        fetch('https://api.frankfurter.app/latest?from=USD&to=TRY')
          .then(res => res.json())
          .then(fData => {
            const fRate = fData?.rates?.TRY;
            if (fRate && typeof fRate === 'number' && fRate > 20) {
              this.exchangeRate.set(Number(fRate.toFixed(2)));
            }
          })
          .catch(() => {
            // Default stays 49.12
          });
      });
  }

  setExchangeRate(rate: number): void {
    if (rate > 0) this.exchangeRate.set(rate);
  }

  setCurrency(curr: 'TRY' | 'USD'): void {
    this.isTryCurrency.set(curr === 'TRY');
  }

  toggleCurrency(): void {
    this.isTryCurrency.update(curr => !curr);
  }

  toggleSidebar(): void {
    this.isSidebarCollapsed.update(c => !c);
  }

  /** Base URL of the VDS API, shared with feature services. */
  get apiBase(): string {
    return this.API_BASE;
  }

  // Etsy Endpoints
  getDailyBrief(shopId?: string): Observable<DailyBrief> {
    const id = shopId || this.activeShopId();
    return this.http.get<DailyBrief>(`${this.API_BASE}/api/etsy/shop/daily-brief?shopId=${id}`);
  }

  /** Canlı Etsy API: son siparişler (kontrol paneli canlı satış akışı). */
  getRecentOrders(limit: number = 15): Observable<RecentOrdersResponse> {
    const id = this.activeShopId();
    return this.http.get<RecentOrdersResponse>(`${this.API_BASE}/api/etsy/shop/recent-orders?shopId=${id}&limit=${limit}`);
  }

  /** Canlı Etsy API: günlük gelir/net kâr serisi (kontrol paneli trend grafiği). */
  getDailySeries(month?: string): Observable<DailySeriesResponse> {
    const id = this.activeShopId();
    const suffix = month ? `&month=${month}` : '';
    return this.http.get<DailySeriesResponse>(`${this.API_BASE}/api/etsy/financial/daily-series?shopId=${id}${suffix}`);
  }

  /** Pulls finance + orders directly from Etsy Open API v3 into the VDS database. */
  syncFromEtsy(startDate?: Date, endDate?: Date): Observable<EtsySyncResultDto> {
    return this.http.post<EtsySyncResultDto>(`${this.API_BASE}/api/etsy/sync`, {
      shopId: this.activeShopId(),
      startDate: startDate ? startDate.toISOString() : null,
      endDate: endDate ? endDate.toISOString() : null
    });
  }

  getUnfulfilledCostAlerts(shopId?: string): Observable<EtsyOrderCostAlertDto[]> {
    const id = shopId || this.activeShopId();
    return this.http.get<EtsyOrderCostAlertDto[]>(
      `${this.API_BASE}/api/etsy/orders/unfulfilled-cost-alerts?shopId=${id}`);
  }

  getFinancialPerformance(period: 'today' | 'this_month' | 'last_month' = 'this_month'): Observable<FinancialPerformanceDto> {
    return this.http.get<FinancialPerformanceDto>(
      `${this.API_BASE}/api/etsy/financial/performance?shopId=${this.activeShopId()}&period=${period}`);
  }

  getFinancialPerformanceByDateRange(startDate: Date, endDate: Date): Observable<FinancialPerformanceDto> {
    const params = new URLSearchParams({
      shopId: this.activeShopId(),
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });
    return this.http.get<FinancialPerformanceDto>(`${this.API_BASE}/api/etsy/financial/performance?${params}`);
  }

  getOrderFinancialsByDateRange(startDate: Date, endDate: Date): Observable<{
    orders: any[];
    count: number;
    totalOrderProfitUsd: number;
    totalOrderProfitTry: number;
    exchangeRateUsed: number;
    ledgerOk: boolean;
    ledgerWarning?: string;
  }> {
    const params = new URLSearchParams({
      shopId: this.activeShopId(),
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });
    return this.http.get<any>(`${this.API_BASE}/api/etsy/financial/orders?${params}`);
  }

  saveOrderCost(receiptId: string | number, costData: { productCost?: number; shippingCost?: number; packagingCost?: number; notes?: string }): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/financial/orders/${receiptId}/cost?shopId=${this.activeShopId()}`, costData);
  }

  getFinancialAnalysis(startDate?: Date, endDate?: Date): Observable<any> {
    let url = `${this.API_BASE}/api/etsy/financial/analysis?shopId=${this.activeShopId()}`;
    if (startDate) url += `&startDate=${encodeURIComponent(startDate.toISOString())}`;
    if (endDate) url += `&endDate=${encodeURIComponent(endDate.toISOString())}`;
    return this.http.get<any>(url);
  }

  getBankPayouts(startDate?: Date, endDate?: Date): Observable<EtsyBankPayoutDto[]> {
    let url = `${this.API_BASE}/api/etsy/banking/payouts?shopId=${this.activeShopId()}`;
    if (startDate) url += `&startDate=${encodeURIComponent(startDate.toISOString())}`;
    if (endDate) url += `&endDate=${encodeURIComponent(endDate.toISOString())}`;
    return this.http.get<EtsyBankPayoutDto[]>(url);
  }

  getBankPayoutsByDateRange(startDate: Date, endDate: Date): Observable<EtsyBankPayoutDto[]> {
    const params = new URLSearchParams({
      shopId: this.activeShopId(),
      startDate: startDate.toISOString(),
      endDate: endDate.toISOString()
    });
    return this.http.get<EtsyBankPayoutDto[]>(`${this.API_BASE}/api/etsy/banking/payouts?${params}`);
  }

  getFinancialSummary(): Observable<FinancialSummary> {
    return this.http.get<FinancialSummary>(`${this.API_BASE}/api/financial/summary`);
  }

  getBankDeposits(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/banking/deposits`);
  }

  // Admin Endpoints
  getAdminUsers(): Observable<User[]> {
    return this.http.get<User[]>(`${this.API_BASE}/api/admin/users`);
  }

  updateAdminUser(id: string, req: UpdateUserRequest): Observable<{ success: boolean; message: string }> {
    return this.http.put<{ success: boolean; message: string }>(`${this.API_BASE}/api/admin/users/${id}`, req);
  }

  toggleUserStatus(id: string): Observable<{ success: boolean; isActive: boolean }> {
    return this.http.post<{ success: boolean; isActive: boolean }>(`${this.API_BASE}/api/admin/users/${id}/toggle-status`, {});
  }

  getAuditLogs(limit: number = 100): Observable<AuditLog[]> {
    return this.http.get<AuditLog[]>(`${this.API_BASE}/api/admin/audit-logs?limit=${limit}`);
  }

  getSystemStats(): Observable<SystemStats> {
    return this.http.get<SystemStats>(`${this.API_BASE}/api/admin/system-stats`);
  }

  // ── A/B Testing Endpoints ─────────────────────────────────────────────────
  getAbTests(listingId?: string): Observable<any[]> {
    const url = listingId 
      ? `${this.API_BASE}/api/etsy/ab-tests?listingId=${encodeURIComponent(listingId)}`
      : `${this.API_BASE}/api/etsy/ab-tests`;
    return this.http.get<any[]>(url);
  }

  createAbTest(experiment: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/ab-tests`, experiment);
  }

  updateAbTestStatus(id: number, status: number): Observable<any> {
    return this.http.put<any>(`${this.API_BASE}/api/etsy/ab-tests/${id}/status`, { status });
  }

  deleteAbTest(id: number): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.API_BASE}/api/etsy/ab-tests/${id}`);
  }

  // ── AI Usage Endpoints ────────────────────────────────────────────────────
  getAiUsageStats(provider?: string): Observable<any> {
    const url = provider
      ? `${this.API_BASE}/api/etsy/ai-usage/stats?provider=${encodeURIComponent(provider)}`
      : `${this.API_BASE}/api/etsy/ai-usage/stats`;
    return this.http.get<any>(url);
  }

  getAiUsageHistory(provider?: string, limit: number = 100): Observable<any[]> {
    let url = `${this.API_BASE}/api/etsy/ai-usage/history?limit=${limit}`;
    if (provider) url += `&provider=${encodeURIComponent(provider)}`;
    return this.http.get<any[]>(url);
  }

  recordAiUsage(record: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/ai-usage`, record);
  }

  // ── Batch Queue Endpoints ─────────────────────────────────────────────────
  getBatchQueue(status?: string): Observable<any[]> {
    const url = status
      ? `${this.API_BASE}/api/etsy/batch-queue?status=${encodeURIComponent(status)}`
      : `${this.API_BASE}/api/etsy/batch-queue`;
    return this.http.get<any[]>(url);
  }

  enqueueBatch(items: any[]): Observable<any[]> {
    return this.http.post<any[]>(`${this.API_BASE}/api/etsy/batch-queue/enqueue`, { items });
  }

  processBatchItem(id: number, req: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/batch-queue/${id}/process`, req);
  }

  clearCompletedBatch(): Observable<{ success: boolean; cleared: number }> {
    return this.http.delete<{ success: boolean; cleared: number }>(`${this.API_BASE}/api/etsy/batch-queue/completed`);
  }

  // ── Tracking Endpoints ────────────────────────────────────────────────────
  getTrackingItems(): Observable<any[]> {
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/tracking`);
  }

  getTrackingSnapshots(id: number): Observable<any[]> {
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/tracking/${id}/snapshots`);
  }

  saveTrackingCapture(capture: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/tracking/capture`, capture);
  }

  deleteTrackingItem(id: number): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.API_BASE}/api/etsy/tracking/${id}`);
  }

  // ── Shop Performance History Endpoints ────────────────────────────────────
  getShopPerformanceHistory(shopId?: number): Observable<any[]> {
    const id = shopId || Number(this.activeShopId()) || 0;
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/shop/performance?shopId=${id}`);
  }

  // ── Secure Settings Endpoints (SQLite Protected) ──────────────────────────
  getTelegramSettings(shopId?: string): Observable<any> {
    const id = shopId || this.activeShopId();
    return this.http.get<any>(`${this.API_BASE}/api/etsy/settings/telegram?shopId=${encodeURIComponent(id)}`);
  }

  saveTelegramSettings(settings: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/settings/telegram`, settings);
  }

  getCarrierSessions(shopId?: string): Observable<any[]> {
    const id = shopId || this.activeShopId();
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/settings/carrier-sessions?shopId=${encodeURIComponent(id)}`);
  }

  saveCarrierSession(session: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/settings/carrier-sessions`, session);
  }

  // ── Etsy Developer Credentials & OAuth PKCE Endpoints ──────────────────────
  getEtsyCredentials(shopId?: string): Observable<any> {
    const id = shopId || this.activeShopId();
    return this.http.get<any>(`${this.API_BASE}/api/etsy/settings/credentials?shopId=${encodeURIComponent(id)}`);
  }

  saveEtsyCredentials(payload: { shopId: string; keystring?: string; sharedSecret?: string; redirectUri?: string }): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/settings/credentials`, payload);
  }

  getEtsyConnectUrl(shopId?: string, redirectUri?: string): Observable<{ url: string; state: string; redirectUri: string; shopId: string }> {
    const id = shopId || this.activeShopId();
    let url = `${this.API_BASE}/api/etsy/oauth/connect-url?shopId=${encodeURIComponent(id)}`;
    if (redirectUri) url += `&redirectUri=${encodeURIComponent(redirectUri)}`;
    return this.http.get<{ url: string; state: string; redirectUri: string; shopId: string }>(url);
  }

  exchangeEtsyCode(payload: { shopId: string; code: string; state?: string; codeVerifier?: string; redirectUri?: string }): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/oauth/exchange-code`, payload);
  }

  // ── Shop Active Listings & AI Optimization ──────────────────────────────
  getShopActiveListings(shopId?: string, limit: number = 25): Observable<ShopListingItemDto[]> {
    const id = shopId || this.activeShopId();
    return this.http.get<ShopListingItemDto[]>(`${this.API_BASE}/api/etsy/shop/listings?shopId=${encodeURIComponent(id)}&limit=${limit}`);
  }

  optimizeListingWithAi(listingId: string | number, payload: {
    shopId?: string;
    title?: string;
    tags?: string[];
    description?: string;
    focusKeywords?: string;
    targetBuyerPersona?: string;
    tone?: string;
    model?: string;
    apiKey?: string;
    provider?: string;
  }): Observable<OptimizeListingResponseDto> {
    const shopId = payload.shopId || this.activeShopId();
    const body = { ...payload, shopId };
    return this.http.post<OptimizeListingResponseDto>(`${this.API_BASE}/api/etsy/listings/${listingId}/ai-optimize?shopId=${encodeURIComponent(shopId)}`, body);
  }

  updateEtsyListing(listingId: string | number, payload: {
    shopId?: string;
    title?: string;
    description?: string;
    tags?: string[];
    materials?: string[];
    state?: string;
  }): Observable<any> {
    const shopId = payload.shopId || this.activeShopId();
    return this.http.put<any>(`${this.API_BASE}/api/etsy/listings/${listingId}?shopId=${encodeURIComponent(shopId)}`, payload);
  }

  getListingAudits(shopId?: string): Observable<SavedListingAuditDto[]> {
    const id = shopId || this.activeShopId();
    return this.http.get<SavedListingAuditDto[]>(`${this.API_BASE}/api/etsy/listings/audits?shopId=${encodeURIComponent(id)}`);
  }

  // ── Etsy Canlı Pazar Araştırması & Rakip İstihbaratı ─────────────────────
  searchMarket(keyword: string, limit: number = 50, sortBy: string = 'market_score', shopId?: string): Observable<MarketSearchResponseDto> {
    const sId = shopId || this.activeShopId();
    return this.http.get<MarketSearchResponseDto>(`${this.API_BASE}/api/etsy/market/search`, {
      params: {
        keyword: keyword.trim(),
        limit: limit.toString(),
        sortBy,
        shopId: sId
      }
    });
  }

  // ── System Health & Version ───────────────────────────────────────────────
  getSystemVersion(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/system/version`);
  }
}

export interface MarketTagFrequencyDto {
  tag: string;
  count: number;
  usagePercentage: number;
  wordCount: number;
  charLength: number;
  isLongTail: boolean;
  competitionLevel: string;
}

export interface MarketSummaryKpisDto {
  totalListings: number;
  averagePrice: number;
  minPrice: number;
  maxPrice: number;
  currency: string;
  averageFavorites: number;
  averageViews: number;
  topShopName: string;
  topShopSales: number;
  opportunityScore: number;
  topTags: MarketTagFrequencyDto[];
}

export interface MarketListingItemDto {
  id: number;
  listingRank: number;
  title: string;
  priceUsd: number;
  currency: string;
  shopName: string;
  shopSales: number;
  shopUrl: string;
  listingUrl: string;
  favorites: number;
  views: number;
  seoScore: number;
  marketScore: number;
  tags: string[];
  materials: string[];
  imageUrl: string;
  imageUrls: string[];
  description: string;
  reviewCount: number;
  reviewAverage: number;
  quantity: number;
}

export interface MarketSearchResponseDto {
  keyword: string;
  total: number;
  kpis: MarketSummaryKpisDto;
  listings: MarketListingItemDto[];
}



/** Canlı Etsy API: kontrol paneli son sipariş satırı. */
export interface RecentOrderRow {
  date: string;
  receiptId: number;
  title: string;
  quantity: number;
  totalUsd: number;
  netProfitUsd: number;
  hasCost: boolean;
}

export interface RecentOrdersResponse {
  orders: RecentOrderRow[];
  count: number;
}

/** Canlı Etsy API: kontrol paneli günlük seri yanıtı. */
export interface DailySeriesResponse {
  month: string;
  labels: string[];
  grossSales: number[];
  netProfit: number[];
  topProductTitle: string | null;
  topProductRevenueUsd: number;
  orderCount: number;
}
