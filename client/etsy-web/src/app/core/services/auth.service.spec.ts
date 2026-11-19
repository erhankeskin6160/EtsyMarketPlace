import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { User } from '../models/auth.models';

describe('AuthService', () => {
  let service: AuthService;
  let httpTesting: HttpTestingController;

  const mockUser: User = {
    id: 'user-1',
    username: 'seller',
    email: 'seller@example.com',
    role: 'StoreOwner',
    assignedShopIds: ['53236321'],
    monthlyAiTokenQuota: 100000,
    usedAiTokens: 500,
    isActive: true,
    createdAt: '2026-01-01',
    lastLoginAt: '2026-10-10'
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    service = TestBed.inject(AuthService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should correctly set session and update reactive signals', () => {
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.isStoreOwner()).toBeFalse();
    expect(service.token()).toBeNull();

    service.setSession('test-jwt-token-123', mockUser);

    expect(service.isAuthenticated()).toBeTrue();
    expect(service.isStoreOwner()).toBeTrue();
    expect(service.token()).toBe('test-jwt-token-123');
    expect(service.currentUser()?.username).toBe('seller');
  });

  it('should treat both Admin and SuperAdmin as admin', () => {
    service.setSession('token-admin', { ...mockUser, role: 'Admin' });
    expect(service.isAdmin()).toBeTrue();

    service.setSession('token-super', { ...mockUser, role: 'SuperAdmin' });
    expect(service.isAdmin()).toBeTrue();
  });

  it('should call backend logout endpoint and clear session upon logout', () => {
    service.setSession('test-jwt-token-123', mockUser);
    expect(service.isAuthenticated()).toBeTrue();

    service.logout();

    const req = httpTesting.expectOne(r => r.url.endsWith('/api/auth/logout'));
    expect(req.request.method).toBe('POST');
    expect(req.request.headers.get('Authorization')).toBe('Bearer test-jwt-token-123');
    req.flush({ success: true });

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
    expect(service.token()).toBeNull();
    expect(localStorage.getItem('etsy_web_jwt_token')).toBeNull();
  });

  it('should still clear local session even if backend logout fails', () => {
    service.setSession('test-jwt-token-123', mockUser);

    service.logout();

    const req = httpTesting.expectOne(r => r.url.endsWith('/api/auth/logout'));
    req.flush({ error: 'Server error' }, { status: 500, statusText: 'Internal Server Error' });

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
    expect(localStorage.getItem('etsy_web_jwt_token')).toBeNull();
  });

  it('should clear local session without server call when notifyServer is false', () => {
    service.setSession('test-jwt-token-123', mockUser);

    service.logout(false);

    httpTesting.expectNone(r => r.url.endsWith('/api/auth/logout'));
    expect(service.isAuthenticated()).toBeFalse();
    expect(service.token()).toBeNull();
  });
});
