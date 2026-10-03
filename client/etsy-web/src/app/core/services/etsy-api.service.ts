import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DailyBrief, FinancialSummary, BankDeposit } from '../models/etsy.models';
import { User, UpdateUserRequest, AuditLog, SystemStats } from '../models/auth.models';

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
  [key: string]: unknown;
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

@Injectable({
  providedIn: 'root'
})
export class EtsyApiService {
  private http = inject(HttpClient);
  private readonly API_BASE = 'http://5.180.81.148:5263';

  // Global UI Signals
  readonly activeShopId = signal<string>('53236321');
  readonly exchangeRate = signal<number>(49.12);
  readonly isTryCurrency = signal<boolean>(true);
  readonly isSidebarCollapsed = signal<boolean>(false);

  constructor() {
    this.fetchLiveExchangeRate();
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

  /** Pulls finance + orders directly from Etsy Open API v3 into the VDS database. */
  syncFromEtsy(startDate?: Date, endDate?: Date): Observable<EtsySyncResultDto> {
    return this.http.post<EtsySyncResultDto>(`${this.API_BASE}/api/etsy/sync`, {
      shopId: this.activeShopId(),
      startDate: startDate ? startDate.toISOString() : null,
      endDate: endDate ? endDate.toISOString() : null
    });
  }

  getUnfulfilledCostAlerts(): Observable<EtsyOrderCostAlertDto[]> {
    return this.http.get<EtsyOrderCostAlertDto[]>(
      `${this.API_BASE}/api/etsy/orders/unfulfilled-cost-alerts?shopId=${this.activeShopId()}`);
  }

  getFinancialPerformance(period: 'today' | 'this_month' | 'last_month' = 'this_month'): Observable<FinancialPerformanceDto> {
    return this.http.get<FinancialPerformanceDto>(
      `${this.API_BASE}/api/etsy/financial/performance?shopId=${this.activeShopId()}&period=${period}`);
  }

  getBankPayouts(startDate?: Date, endDate?: Date): Observable<EtsyBankPayoutDto[]> {
    let url = `${this.API_BASE}/api/etsy/banking/payouts?shopId=${this.activeShopId()}`;
    if (startDate) url += `&startDate=${encodeURIComponent(startDate.toISOString())}`;
    if (endDate) url += `&endDate=${encodeURIComponent(endDate.toISOString())}`;
    return this.http.get<EtsyBankPayoutDto[]>(url);
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

  // ── System Health & Version ───────────────────────────────────────────────
  getSystemVersion(): Observable<any> {
    return this.http.get<any>(`${this.API_BASE}/api/system/version`);
  }
}
