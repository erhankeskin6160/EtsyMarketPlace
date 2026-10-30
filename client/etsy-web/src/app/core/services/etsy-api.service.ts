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
  grossSalesTRY?: number;
  netProfitTRY?: number;
  exchangeRateUsed?: number;
  receiptCount?: number;
  ledgerOk?: boolean;
  ledgerWarning?: string | null;
  source?: string;
  bankPayoutsUSD?: number;
  bankPayoutsTRY?: number;
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
/** VDS: Canlı Etsy Payment Reserve & Bakiye Snapshot (Sıfır Ban Riskli Yer İmi & VDS Köprüsü) */
export interface EtsyPaymentReserveSnapshotDto {
  shopId: string;
  currentBalanceTry: number;
  pendingBalanceTry: number;
  totalFundsTry: number;
  reservePercent: number;
  reserveHoldDays: number;
  reevaluationDate?: string;
  reserveSummaryText?: string;
  lastUpdatedUtc?: string;
}

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

export interface AiProviderBalanceDto {
  provider: string;
  isAvailable: boolean;
  totalBalanceUsd: number;
  grantedBalanceUsd: number;
  toppedUpBalanceUsd: number;
  currency: string;
  statusMessage: string;
  dailyRequestsUsed: number;
  dailyQuotaLimit: number;
  maskedApiKey: string;
  officialMonthlyCostUsd?: number;
  balanceCny?: number;
  availableModels: string[];
  hasAdminKey: boolean;
  totalTokensUsed: number;
  promptTokensUsed: number;
  completionTokensUsed: number;
  totalApiRequests: number;
  totalCostUsd: number;
  totalCostTry: number;
  activeModel: string;
  isActiveProvider: boolean;
  topUpUrl: string;
  tokenUnitTariff: string;
}

export interface CreateListingImagePayload {
  dataUrl?: string;
  url?: string;
  rank?: number;
}

export interface CreateListingVariationPayload {
  key: string;
  price: number;
  quantity: number;
  active?: boolean;
  imageRank?: number;
}

export interface CreateListingVariationGroupPayload {
  name: string;
  values: string[];
}

export interface EtsyTaxonomyItemDto {
  id: number;
  name: string;
  path: string;
}

export interface CreateListingPayload {
  shopId?: string;
  title: string;
  description: string;
  price: number;
  quantity: number;
  taxonomyId: number;
  shippingProfileId?: number | null;
  readinessStateId?: number | null;
  isDigital?: boolean;
  tags?: string[];
  materials?: string[];
  whoMade?: string;
  whenMade?: string;
  state?: 'draft' | 'active';
  images?: CreateListingImagePayload[];
  variations?: CreateListingVariationPayload[];
  variationGroups?: CreateListingVariationGroupPayload[];
}

export interface CreatedListingResponseDto {
  success: boolean;
  listingId: number;
  url: string;
  state: string;
  uploadedImages: number;
  message: string;
  variationImagesCount?: number;
}

export interface GetAiSettingsResponseDto {
  exists: boolean;
  settingsJson: string;
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
  createListing(payload: CreateListingPayload): Observable<CreatedListingResponseDto> {
    const shopId = payload.shopId || this.activeShopId();
    return this.http.post<CreatedListingResponseDto>(`${this.API_BASE}/api/etsy/listings`, {
      ...payload,
      shopId
    });
  }

  updateListingState(listingId: number | string, state: 'active' | 'draft' | 'inactive', shopId?: string): Observable<{ success: boolean; listingId: number; state: string; message: string }> {
    const sId = shopId || this.activeShopId();
    return this.http.patch<{ success: boolean; listingId: number; state: string; message: string }>(
      `${this.API_BASE}/api/etsy/listings/${listingId}/state?shopId=${encodeURIComponent(sId)}`,
      { state }
    );
  }

  getShippingProfiles(shopId?: string): Observable<any> {
    const id = shopId || this.activeShopId();
    return this.http.get<any>(`${this.API_BASE}/api/etsy/shipping-profiles?shopId=${encodeURIComponent(id)}`);
  }

  getReadinessStates(shopId?: string): Observable<any> {
    const id = shopId || this.activeShopId();
    return this.http.get<any>(`${this.API_BASE}/api/etsy/readiness-states?shopId=${encodeURIComponent(id)}`);
  }

  getSellerTaxonomy(shopId?: string): Observable<{ count: number; results: EtsyTaxonomyItemDto[] }> {
    const id = shopId || this.activeShopId();
    return this.http.get<{ count: number; results: EtsyTaxonomyItemDto[] }>(`${this.API_BASE}/api/etsy/taxonomy/nodes?shopId=${encodeURIComponent(id)}`);
  }

  searchSellerTaxonomy(query: string, shopId?: string, limit: number = 100): Observable<{ count: number; results: EtsyTaxonomyItemDto[] }> {
    const id = shopId || this.activeShopId();
    return this.http.get<{ count: number; results: EtsyTaxonomyItemDto[] }>(`${this.API_BASE}/api/etsy/taxonomy/search?shopId=${encodeURIComponent(id)}&q=${encodeURIComponent(query)}&limit=${limit}`);
  }

  getAiSettings(shopId?: string): Observable<GetAiSettingsResponseDto> {
    const id = shopId || this.activeShopId();
    return this.http.get<GetAiSettingsResponseDto>(`${this.API_BASE}/api/settings/ai?shopId=${id}`);
  }

  saveAiSettings(settingsJson: string, shopId?: string): Observable<{ success: boolean; message: string }> {
    const id = shopId || this.activeShopId();
    return this.http.post<{ success: boolean; message: string }>(`${this.API_BASE}/api/settings/ai`, {
      shopId: id,
      settingsJson
    });
  }

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

  /** Canlı Etsy API: Alıcı teslimat adresleri ve ürün satırları ile birlikte sipariş kuyruğunu getirir. */
  getFulfillmentQueue(shopId?: string, lookbackDays: number = 45, wasShipped?: boolean): Observable<{ count: number; results: any[] }> {
    const id = shopId || this.activeShopId();
    let url = `${this.API_BASE}/api/etsy/orders/fulfillment-queue?shopId=${id}&lookbackDays=${lookbackDays}`;
    if (wasShipped !== undefined && wasShipped !== null) {
      url += `&wasShipped=${wasShipped}`;
    }
    return this.http.get<{ count: number; results: any[] }>(url);
  }

  /** Alıcı adresini kalıcı SQLite deposuna kaydeder. */
  saveOrderAddress(receiptId: number, address: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/orders/save-address?receiptId=${receiptId}`, address);
  }

  /** Siparişin maliyet, paket ölçüsü, GTİP ve seçili taşıyıcı verilerini kalıcı SQLite deposuna kaydeder. */
  saveOrderFulfillmentMeta(receiptId: number, meta: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/orders/save-fulfillment-meta?receiptId=${receiptId}`, meta);
  }

  /** Ham adres metnini yapay zeka ile ayrıştırıp form alanlarına dönüştürür. */
  aiParseAddress(rawAddress: string): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/orders/ai-parse-address`, { rawAddress });
  }

  /** Etsy sipariş CSV dosyasını sunucuya yükleyerek adresleri toplu günceller. */
  uploadOrdersCsv(formData: FormData): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/orders/upload-csv`, formData);
  }

  /** Seçilen kargo platformunda gönderi ve takip kodu oluşturur. */
  createCarrierShipment(payload: any): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/shipping/create-shipment`, payload);
  }

  /** Sunucuda kayıtlı kargo firması oturumlarını ve aktif Bearer Token durumlarını getirir (gerekirse RefreshToken ile yeniler). */
  getShippingCarrierSessions(): Observable<{ count: number; results: any[] }> {
    return this.http.get<{ count: number; results: any[] }>(`${this.API_BASE}/api/etsy/shipping/carrier-sessions`);
  }

  /** Kargo platformu giriş ve API kimlik bilgilerini doğrular, kaydeder veya tarayıcıdan otomatik yakalar. */
  connectCarrier(payload: {
    carrierKey: string;
    apiKey?: string;
    apiSecret?: string;
    bearerToken?: string;
    refreshToken?: string;
    email?: string;
    password?: string;
    openBrowser?: boolean;
  }): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/shipping/connect-carrier`, payload);
  }

  /** 4 taşıyıcıdan (Aras Global, ShipEntegra, Shiptomore, Navlungo) canlı ve tarife kargo tekliflerini getirir. */
  getCarrierQuotes(payload: {
    countryCode: string;
    postalCode?: string;
    city?: string;
    state?: string;
    weightKg: number;
    widthCm: number;
    lengthCm: number;
    heightCm: number;
    usdTryRate?: number;
  }): Observable<any> {
    return this.http.post<any>(`${this.API_BASE}/api/etsy/shipping/quotes`, payload);
  }

  /** GTİP / HS gümrük tarife kodlarını ve ABD HTS kataloğunu arar. */
  searchHsCodes(query: string = '', limit: number = 25): Observable<{ count: number; results: any[] }> {
    return this.http.get<{ count: number; results: any[] }>(
      `${this.API_BASE}/api/etsy/shipping/hs-codes?query=${encodeURIComponent(query || '')}&limit=${limit}`
    );
  }



  getFinancialPerformance(period: 'today' | 'this_month' | 'last_month' | 'this_year' | 'all_time' = 'this_month'): Observable<FinancialPerformanceDto> {
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

  getPaymentReserveSnapshot(shopId?: string): Observable<{ success: boolean; data: EtsyPaymentReserveSnapshotDto; message?: string }> {
    const id = shopId || this.activeShopId();
    return this.http.get<{ success: boolean; data: EtsyPaymentReserveSnapshotDto; message?: string }>(`${this.API_BASE}/api/etsy/financial/payment-reserve-snapshot?shopId=${id}`);
  }

  savePaymentReserveSnapshot(snapshot: EtsyPaymentReserveSnapshotDto): Observable<{ success: boolean; data: EtsyPaymentReserveSnapshotDto; message?: string }> {
    return this.http.post<{ success: boolean; data: EtsyPaymentReserveSnapshotDto; message?: string }>(`${this.API_BASE}/api/etsy/financial/payment-reserve-snapshot`, snapshot);
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

  getAiProviderBalances(shopId?: string, exchangeRate?: number, period: string = '30d'): Observable<AiProviderBalanceDto[]> {
    const sId = shopId || this.activeShopId();
    const rate = exchangeRate || this.exchangeRate() || 40.0;
    return this.http.get<AiProviderBalanceDto[]>(
      `${this.API_BASE}/api/etsy/ai-usage/balances?shopId=${encodeURIComponent(sId)}&exchangeRate=${rate}&period=${encodeURIComponent(period)}`
    );
  }

  getAiUsageHistory(provider?: string, limit: number = 100, period: string = '30d'): Observable<any[]> {
    let url = `${this.API_BASE}/api/etsy/ai-usage/history?limit=${limit}&period=${encodeURIComponent(period)}`;
    if (provider && provider !== 'ALL') url += `&provider=${encodeURIComponent(provider)}`;
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

  // ── Pazar Araştırması: Kelime Takibi (Snapshot) ─────────────────────────
  trackMarketKeyword(keyword: string): Observable<TrackedKeywordDto> {
    return this.http.post<TrackedKeywordDto>(`${this.API_BASE}/api/etsy/market/keywords/track`, { keyword });
  }

  getTrackedMarketKeywords(): Observable<TrackedKeywordDto[]> {
    return this.http.get<TrackedKeywordDto[]>(`${this.API_BASE}/api/etsy/market/keywords`);
  }

  deleteTrackedMarketKeyword(keyword: string): Observable<{ success: boolean }> {
    return this.http.delete<{ success: boolean }>(`${this.API_BASE}/api/etsy/market/keywords`, {
      params: { keyword }
    });
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

  generateMarketAiReport(payload: MarketAiReportRequestDto): Observable<MarketAiReportResponseDto> {
    const shopId = payload.shopId || this.activeShopId();
    return this.http.post<MarketAiReportResponseDto>(`${this.API_BASE}/api/etsy/market/ai-report`, {
      ...payload,
      shopId
    });
  }

  analyzeCompetitorShop(shopNameOrUrl: string, limit: number = 50, shopId?: string): Observable<CompetitorShopAnalysisDto> {
    const sId = shopId || this.activeShopId();
    return this.http.get<CompetitorShopAnalysisDto>(`${this.API_BASE}/api/etsy/market/competitor`, {
      params: {
        shopNameOrUrl: shopNameOrUrl.trim(),
        limit: limit.toString(),
        shopId: sId
      }
    });
  }

  getSeasonalTrends(): Observable<SeasonalTrendItemDto[]> {
    return this.http.get<SeasonalTrendItemDto[]>(`${this.API_BASE}/api/etsy/market/seasonal-trends`);
  }

  getCompetitorListingInventory(listingId: number, shopId?: string): Observable<CompetitorListingInventoryDto> {
    const sId = shopId || this.activeShopId();
    return this.http.get<CompetitorListingInventoryDto>(`${this.API_BASE}/api/etsy/market/listing/${listingId}/inventory`, {
      params: { shopId: sId }
    });
  }

  improveListingLqs(command: AiLqsImprovementCommandDto): Observable<AiLqsImprovementResultDto> {
    return this.http.post<AiLqsImprovementResultDto>(`${this.API_BASE}/api/etsy/market/listing/improve-lqs`, command);
  }

  // ── System Health & Version ───────────────────────────────────────────────
  getSystemVersion(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/system/version`);
  }

  getVdsMcpTools(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/etsy/mcp/tools`);
  }

  callVdsMcpTool(name: string, args: Record<string, any> = {}, shopId?: string): Observable<any> {
    const sId = shopId || this.activeShopId();
    return this.http.post<any>(`${this.API_BASE}/api/etsy/mcp/call?shopId=${encodeURIComponent(sId)}`, {
      name,
      arguments: args
    });
  }

  private _noopMcpMarker(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/system/version`);
  }
}

export interface MarketAiReportRequestDto {
  keyword: string;
  shopId?: string;
  averagePrice: number;
  minPrice: number;
  maxPrice: number;
  medianPrice?: number;
  averageFavorites: number;
  averageViews: number;
  opportunityScore: number;
  totalEstimatedMarketRevenue: number;
  uniqueShopCount?: number;
  totalListings: number;
  topShopName: string;
  topShopSales: number;
  topCompetitorTitles: string[];
  topTags: string[];
  apiKey?: string;
  model?: string;
  provider?: string;
}

export interface MarketAiReportResponseDto {
  keyword: string;
  usedAi: boolean;
  provider: string;
  model: string;
  marketOverview: string;
  pricingStrategy: string;
  differentiationIdeas: string[];
  recommendedKeywords: string[];
  actionItems: string[];
  reportHtml: string;
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
  medianPrice?: number;
  currency: string;
  averageFavorites: number;
  averageViews: number;
  topShopName: string;
  topShopSales: number;
  uniqueShopCount?: number;
  topShopConcentrationPct?: number;
  freeShippingRatioPct?: number;
  bestsellerCount?: number;
  highOpportunityCount?: number;
  opportunityScore: number;
  totalEstimatedMarketRevenue: number;
  averageEstimatedMonthlySales: number;
  topSellerMonthlyRevenue: number;
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
  estimatedMonthlySales: number;
  estimatedMonthlyRevenue: number;
  salesVelocity: string;
  conversionRateEst: number;
  isTargetListing?: boolean;
  isBestseller?: boolean;
  hasFreeShipping?: boolean;
  isOpportunityListing?: boolean;
  tags: string[];
  materials: string[];
  imageUrl: string;
  imageUrls: string[];
  description: string;
  reviewCount: number;
  reviewAverage: number;
  quantity: number;
  taxonomyId?: number;
}

export interface MarketSearchResponseDto {
  targetListingId?: number | null;
  keyword: string;
  total: number;
  kpis: MarketSummaryKpisDto;
  listings: MarketListingItemDto[];
}

/** Pazar Araştırması: takip edilen kelime için tarihli KPI snapshot'ı. */
export interface TrackingSnapshotDto {
  id: number;
  trackingItemId: number;
  capturedAt: string;
  price?: number | null;
  currencyCode?: string;
  favorites?: number | null;
  opportunityScore?: number | null;
  resultCount?: number | null;
  sampleSize?: number | null;
}

/** Pazar Araştırması: takip edilen kelime + snapshot geçmişi (sunucu latest/previous dahil döner). */
export interface TrackedKeywordDto {
  item: {
    id: number;
    entityType: number;
    externalKey: string;
    displayName: string;
    url: string;
    createdAt: string;
  };
  snapshots: TrackingSnapshotDto[];
  latest?: TrackingSnapshotDto | null;
  previous?: TrackingSnapshotDto | null;
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

export interface CompetitorShopProfileDto {
  shopId: number;
  shopName: string;
  title: string;
  shopUrl: string;
  iconUrl: string;
  totalSalesCount: number;
  activeListingsCount: number;
  reviewAverage: number;
  reviewCount: number;
  createdTimestamp: number;
  shopAgeDays: number;
  dailySalesVelocity: number;
  estimatedMonthlySales: number;
  estimatedMonthlyRevenueUsd: number;
  averagePriceUsd: number;
  minPriceUsd: number;
  maxPriceUsd: number;
  medianPriceUsd: number;
  competitorStrengthScore: number;
  averageLqsGrade: string;
  bestsellerCount: number;
}

export interface CompetitorListingDto {
  listingId: number;
  taxonomyId?: number;
  rank: number;
  title: string;
  priceUsd: number;
  currency: string;
  favorites: number;
  views: number;
  quantity: number;
  estimatedMonthlySales: number;
  estimatedMonthlyRevenueUsd: number;
  salesVelocityBadge: string;
  lqsGrade: string;
  lqsScore: number;
  tagCount: number;
  imageCount: number;
  descriptionLength: number;
  tags: string[];
  materials: string[];
  imageUrl: string;
  imageUrls: string[];
  listingUrl: string;
  description: string;
  createdTimestamp: number;
}

export interface CompetitorFrequencyItemDto {
  term: string;
  count: number;
  usagePercentage: number;
}

export interface CompetitorShopAnalysisDto {
  found: boolean;
  errorMessage?: string | null;
  shop: CompetitorShopProfileDto;
  listings: CompetitorListingDto[];
  topTags: CompetitorFrequencyItemDto[];
  topTitleWords: CompetitorFrequencyItemDto[];
  aiCompetitiveGapInsight: string;
}

export interface SeasonalTrendItemDto {
  eventName: string;
  icon: string;
  peakMonth: string;
  prepStartDate: string;
  daysLeftToPeak: number;
  statusBadge: string;
  statusColorHex: string;
  categoryFocus: string;
  highDemandKeywords: string[];
  winningTags: string[];
  actionAdvice: string;
  estimatedTrafficMultiplier: number;
}

export interface CompetitorVariationGroupDto {
  name: string;
  values: string[];
}

export interface CompetitorVariationOfferingDto {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
}

export interface CompetitorListingInventoryDto {
  listingId: number;
  hasVariations: boolean;
  varType1?: string;
  varValues1?: string;
  enableVar2?: boolean;
  varType2?: string;
  varValues2?: string;
  groups: CompetitorVariationGroupDto[];
  offerings: CompetitorVariationOfferingDto[];
}

export interface AiLqsImprovementCommandDto {
  listingId: number;
  title: string;
  tags?: string[];
  description?: string;
  materials?: string[];
  priceUsd: number;
  category?: string;
  currentLqsScore: number;
  provider?: string;
  apiKey?: string;
  model?: string;
}

export interface AiLqsImprovementResultDto {
  listingId: number;
  optimizedTitle: string;
  optimizedTags: string[];
  optimizedDescription: string;
  suggestedMaterials: string[];
  suggestedVariations: CompetitorVariationGroupDto[];
  suggestedOfferings: CompetitorVariationOfferingDto[];
  targetLqsScore: number;
  targetLqsGrade: string;
  lqsDoctorAuditSummary: string;
  improvementHighlights: string[];
  usedAi: boolean;
  provider: string;
}


