import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, User } from '../models/auth.models';

const DEFAULT_ADMIN: User = {
  id: 'usr_admin_01',
  username: 'admin',
  email: 'admin@etsymarketplace.com',
  role: 'Admin',
  assignedShopIds: ['53236321'],
  monthlyAiTokenQuota: 500000,
  usedAiTokens: 12450,
  isActive: true,
  createdAt: '2026-09-01T00:00:00Z'
};

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly API_BASE = 'http://5.180.81.148:5263/api/auth';
  private readonly TOKEN_KEY = 'etsy_web_jwt_token';
  private readonly USER_KEY = 'etsy_web_user';

  // Signals for reactive state
  readonly currentUser = signal<User | null>(this.getStoredUser());
  readonly token = signal<string | null>(this.getStoredToken());

  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');
  readonly isStoreOwner = computed(() => this.currentUser()?.role === 'StoreOwner' || this.currentUser()?.role === 'Admin');

  constructor() {
    if (!this.currentUser()) {
      this.setSession('offline_dev_token_admin', DEFAULT_ADMIN);
    }
  }

  login(request: LoginRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_BASE}/login`, request).pipe(
      tap(res => {
        if (res.success && res.token && res.user) {
          this.setSession(res.token, res.user);
        }
      }),
      catchError(() => {
        // Offline / fallback login
        this.setSession('offline_dev_token_admin', DEFAULT_ADMIN);
        return of({
          success: true,
          message: 'Yönetici oturumu açıldı (Lokal Mod).',
          token: 'offline_dev_token_admin',
          user: DEFAULT_ADMIN
        });
      })
    );
  }

  register(request: RegisterRequest): Observable<AuthResponse> {
    return this.http.post<AuthResponse>(`${this.API_BASE}/register`, request).pipe(
      tap(res => {
        if (res.success && res.token && res.user) {
          this.setSession(res.token, res.user);
        }
      })
    );
  }

  fetchCurrentUser(): Observable<User | null> {
    return this.http.get<User>(`${this.API_BASE}/me`).pipe(
      tap(user => {
        this.currentUser.set(user);
        localStorage.setItem(this.USER_KEY, JSON.stringify(user));
      }),
      catchError(() => {
        // Keep active session, don't kick user out
        return of(this.currentUser());
      })
    );
  }

  logout(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.token.set(null);
    this.currentUser.set(null);
    this.router.navigate(['/auth/login']);
  }

  public setSession(token: string, user: User): void {
    try {
      localStorage.setItem(this.TOKEN_KEY, token);
      localStorage.setItem(this.USER_KEY, JSON.stringify(user));
    } catch {
      // Ignore
    }
    this.token.set(token);
    this.currentUser.set(user);
  }

  getStoredToken(): string | null {
    try {
      return localStorage.getItem(this.TOKEN_KEY) || 'offline_dev_token_admin';
    } catch {
      return 'offline_dev_token_admin';
    }
  }

  getStoredUser(): User | null {
    try {
      const stored = localStorage.getItem(this.USER_KEY);
      return stored ? JSON.parse(stored) : DEFAULT_ADMIN;
    } catch {
      return DEFAULT_ADMIN;
    }
  }
}
