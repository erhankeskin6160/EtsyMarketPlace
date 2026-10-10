import { Injectable, computed, inject, signal } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Router } from '@angular/router';
import { Observable, tap, catchError, of, throwError } from 'rxjs';
import { AuthResponse, LoginRequest, RegisterRequest, User } from '../models/auth.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AuthService {
  private http = inject(HttpClient);
  private router = inject(Router);

  private readonly API_BASE = `${environment.apiBaseUrl}/api/auth`;
  private readonly TOKEN_KEY = 'etsy_web_jwt_token';
  private readonly USER_KEY = 'etsy_web_user';
  private sessionVerified = false;

  // Signals for reactive state
  readonly currentUser = signal<User | null>(null);
  readonly token = signal<string | null>(this.getStoredToken());

  readonly isAuthenticated = computed(() => !!this.currentUser());
  readonly isAdmin = computed(() => this.currentUser()?.role === 'Admin');
  readonly isStoreOwner = computed(() => this.currentUser()?.role === 'StoreOwner' || this.currentUser()?.role === 'Admin');

  constructor() {}

  login(request: LoginRequest): Observable<AuthResponse> {
    const primaryUrl = `${this.API_BASE}/login`;
    const fallbackUrl = 'http://5.180.81.148:5263/api/auth/login';

    return this.http.post<AuthResponse>(primaryUrl, request).pipe(
      catchError(err => {
        if (err.status === 0 && primaryUrl !== fallbackUrl) {
          console.warn(`[AuthService] Primary login (${primaryUrl}) failed with network error, trying fallback: ${fallbackUrl}`);
          return this.http.post<AuthResponse>(fallbackUrl, request);
        }
        return throwError(() => err);
      }),
      tap(res => {
        if (res.success && res.token && res.user) {
          this.setSession(res.token, res.user);
        }
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
    if (!this.token()) {
      this.currentUser.set(null);
      return of(null);
    }
    if (this.sessionVerified && this.currentUser()) {
      return of(this.currentUser());
    }
    const primaryUrl = `${this.API_BASE}/me`;
    const fallbackUrl = 'http://5.180.81.148:5263/api/auth/me';

    return this.http.get<User>(primaryUrl).pipe(
      catchError(err => {
        if (err.status === 0 && primaryUrl !== fallbackUrl) {
          return this.http.get<User>(fallbackUrl);
        }
        return throwError(() => err);
      }),
      tap(user => {
        this.currentUser.set(user);
        this.sessionVerified = true;
        localStorage.setItem(this.USER_KEY, JSON.stringify(user));
      }),
      catchError(error => {
        if (error.status === 401 || error.status === 403) {
          this.clearSession();
        }
        return throwError(() => error);
      })
    );
  }

  logout(): void {
    this.clearSession();
    this.router.navigate(['/auth/login']);
  }

  private clearSession(): void {
    localStorage.removeItem(this.TOKEN_KEY);
    localStorage.removeItem(this.USER_KEY);
    this.token.set(null);
    this.currentUser.set(null);
    this.sessionVerified = false;
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
    this.sessionVerified = true;
  }

  getStoredToken(): string | null {
    try {
      return localStorage.getItem(this.TOKEN_KEY);
    } catch {
      return null;
    }
  }

}
