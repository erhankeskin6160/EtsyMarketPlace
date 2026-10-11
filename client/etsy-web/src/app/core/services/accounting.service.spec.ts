import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AccountingService } from './accounting.service';
import { FinancialKpiSummary } from '../models/accounting.models';

describe('AccountingService', () => {
  let service: AccountingService;
  let httpTesting: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({
      providers: [
        AccountingService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(AccountingService);
    httpTesting = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpTesting.verify();
  });

  it('should be created', () => {
    expect(service).toBeTruthy();
  });

  it('should calculate expense breakdown with accurate TRY conversions and percentages', () => {
    const summary: FinancialKpiSummary = {
      period: 'this_month',
      currency: 'USD',
      grossSales: 1000,
      etsyFees: 100,
      innerAds: 50,
      offsiteAds: 30,
      refunds: 20,
      netRevenue: 800,
      productCosts: 200,
      shippingCosts: 100,
      realNetProfit: 500,
      profitMarginPercent: 50,
      bankPayoutsTotal: 300
    };

    const rate = 50.0;
    const breakdown = service.getExpenseBreakdown(summary, rate);

    expect(breakdown.length).toBe(6);

    // Total expenses = 100 + 50 + 30 + 200 + 100 + 20 = 500
    const productItem = breakdown.find(b => b.name.includes('Ürün İmalat'));
    expect(productItem).toBeDefined();
    expect(productItem?.amountUsd).toBe(200);
    expect(productItem?.amountTry).toBe(10000); // 200 * 50
    expect(productItem?.percentage).toBe(40.0); // 200 / 500 * 100

    const feeItem = breakdown.find(b => b.name.includes('Etsy İşlem'));
    expect(feeItem).toBeDefined();
    expect(feeItem?.amountUsd).toBe(100);
    expect(feeItem?.amountTry).toBe(5000);
    expect(feeItem?.percentage).toBe(20.0);
  });

  it('should fetch and map financial summary from API', () => {
    const mockApiResponse = {
      period: 'last_month',
      grossSales: 2500,
      etsyFees: 300,
      innerAds: 100,
      offsiteAds: 50,
      refunds: 0,
      netRevenue: 2050,
      productCosts: 400,
      shippingCosts: 150,
      realNetProfit: 1500,
      profitMarginPercent: 60,
      bankPayoutsTotal: 1200
    };

    service.getFinancialSummary().subscribe(summary => {
      expect(summary.grossSales).toBe(2500);
      expect(summary.etsyFees).toBe(300);
      expect(summary.realNetProfit).toBe(1500);
      expect(summary.currency).toBe('USD');
    });

    const req = httpTesting.expectOne(r => r.url.includes('/api/financial/summary'));
    expect(req.request.method).toBe('GET');
    req.flush(mockApiResponse);
  });
});
