import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AuthService } from './auth.service';
import { SubscriptionService } from './subscription.service';
import { User } from '../models/auth.models';
import { UserSubscriptionSummary } from '../models/subscription.models';

describe('SubscriptionService', () => {
  let service: SubscriptionService;
  let authService: AuthService;
  let httpTesting: HttpTestingController;

  const starterUser: User = {
    id: 'user-starter',
    username: 'starter_seller',
    email: 'starter@example.com',
    role: 'StoreOwner',
    assignedShopIds: ['53236321'],
    monthlyAiTokenQuota: 100000,
    usedAiTokens: 1000,
    isActive: true,
    createdAt: '2026-01-01',
    subscriptionPlanId: 'starter',
    subscriptionStatus: 'Active'
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AuthService,
        SubscriptionService,
        provideHttpClient(),
        provideHttpClientTesting(),
        provideRouter([])
      ]
    });
    authService = TestBed.inject(AuthService);
    service = TestBed.inject(SubscriptionService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
    localStorage.clear();
  });

  it('should gate Pro features for Starter users and expose required plan badge', () => {
    authService.setSession('jwt-starter', starterUser);

    const starterSummary: UserSubscriptionSummary = {
      subscriptionId: 'sub-1',
      userId: starterUser.id,
      username: starterUser.username,
      role: 'StoreOwner',
      planId: 'starter',
      planName: 'Starter',
      badgeText: 'BAŞLANGIÇ',
      accentColor: '#34d399',
      status: 'Active',
      billingCycle: 'Monthly',
      startDateUtc: '2026-01-01T00:00:00Z',
      currentPeriodStartUtc: '2026-01-01T00:00:00Z',
      currentPeriodEndUtc: '2026-02-01T00:00:00Z',
      daysRemaining: 30,
      autoRenew: true,
      isAdminExempt: false,
      maxShops: 1,
      assignedShopsCount: 1,
      remainingShopSlots: 0,
      monthlyAiTokenQuota: 100000,
      usedAiTokens: 1000,
      remainingAiTokens: 99000,
      aiUsagePercentage: 1,
      maxMonthlyAiListings: 15,
      maxBatchQueueSize: 0,
      allowedFeatures: ['dashboard', 'fast_creator', 'profit']
    };

    service.mySubscription.set(starterSummary);

    expect(service.isFeatureAllowed('fast_creator')).toBeTrue();
    expect(service.isFeatureAllowed('ai_image')).toBeFalse();
    expect(service.getRequiredPlanBadge('ai_image')).toBe('PRO');
    expect(service.getRequiredPlanBadge('aras')).toBe('GROWTH');
  });

  it('should load plans from /api/subscriptions/plans', () => {
    service.loadPlans().subscribe(plans => {
      expect(plans.length).toBe(1);
      expect(plans[0].id).toBe('starter');
    });

    const req = httpTesting.expectOne('/api/subscriptions/plans');
    expect(req.request.method).toBe('GET');
    req.flush([
      {
        id: 'starter',
        name: 'Starter',
        tagline: 'Starter plan',
        badgeText: 'BAŞLANGIÇ',
        accentColor: '#34d399',
        monthlyPriceUsd: 19,
        yearlyPriceUsd: 180,
        monthlyPriceTry: 650,
        yearlyPriceTry: 6240,
        maxShops: 1,
        monthlyAiTokenQuota: 100000,
        maxMonthlyAiListings: 15,
        maxBatchQueueSize: 0,
        allowedFeatures: ['dashboard'],
        isPopular: false,
        isActive: true,
        displayOrder: 1
      }
    ]);
  });

  it('should call admin payment settings endpoints and confirmAdminTest', () => {
    service.getAdminPaymentSettings().subscribe(cfg => {
      expect(cfg.activeProvider).toBe('PayTR');
    });
    const getReq = httpTesting.expectOne('/api/admin/subscriptions/payment-settings');
    expect(getReq.request.method).toBe('GET');
    getReq.flush({
      activeProvider: 'PayTR',
      isSandbox: true,
      iyzicoApiKey: '',
      hasIyzicoSecretKey: false,
      iyzicoSecretKeyMask: '',
      iyzicoBaseUrl: 'https://sandbox-api.iyzipay.com',
      paytrMerchantId: '123456',
      hasPaytrMerchantKey: true,
      paytrMerchantKeyMask: '••••9999',
      hasPaytrMerchantSalt: true,
      paytrMerchantSaltMask: '••••8888',
      hasStripeSecretKey: false,
      stripeSecretKeyMask: '',
      configSource: 'Admin Paneli (Veritabanı)',
      updatedAtUtc: '2026-11-18T14:00:00Z'
    });

    service.confirmAdminTest('cs_vpos_test').subscribe(res => {
      expect(res.success).toBeTrue();
    });
    const testReq = httpTesting.expectOne('/api/subscriptions/confirm-admin-test');
    expect(testReq.request.method).toBe('POST');
    expect(testReq.request.body).toEqual({ sessionId: 'cs_vpos_test' });
    testReq.flush({
      success: true,
      message: 'Onaylandı',
      transaction: { id: 1, sessionId: 'cs_vpos_test' },
      subscription: { planId: 'pro', planName: 'Pro', status: 'Active', allowedFeatures: ['ai_image'] }
    });
  });
});
