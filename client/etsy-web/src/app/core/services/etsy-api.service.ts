import { Injectable, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { DailyBrief, FinancialSummary, BankDeposit } from '../models/etsy.models';
import { User, UpdateUserRequest, AuditLog, SystemStats } from '../models/auth.models';

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

  // Etsy Endpoints
  getDailyBrief(shopId?: string): Observable<DailyBrief> {
    const id = shopId || this.activeShopId();
    return this.http.get<DailyBrief>(`${this.API_BASE}/api/etsy/brief?shopId=${id}`);
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
}
