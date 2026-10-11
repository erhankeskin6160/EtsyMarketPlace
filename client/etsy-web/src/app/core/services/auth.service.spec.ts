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

  it('should clear session upon logout', () => {
    service.setSession('test-jwt-token-123', mockUser);
    expect(service.isAuthenticated()).toBeTrue();

    service.logout();

    expect(service.isAuthenticated()).toBeFalse();
    expect(service.currentUser()).toBeNull();
    expect(service.token()).toBeNull();
    expect(localStorage.getItem('etsy_web_jwt_token')).toBeNull();
  });
});
