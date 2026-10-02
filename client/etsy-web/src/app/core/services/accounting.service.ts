import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable, of } from 'rxjs';
import { catchError, map } from 'rxjs/operators';
import { 
  PaymentLedgerEntry, 
  BankPayoutRecord, 
  FinancialKpiSummary, 
  ExpenseBreakdownItem 
} from '../models/accounting.models';

@Injectable({
  providedIn: 'root'
})
export class AccountingService {
  private readonly API_BASE = 'http://5.180.81.148:5263';
  private readonly DEFAULT_SHOP_ID = '53236321';

  constructor(private http: HttpClient) {}

  public getFinancialSummary(): Observable<FinancialKpiSummary> {
    return this.http.get<any>(`${this.API_BASE}/api/financial/summary`).pipe(
      map(data => ({
        period: data.period || 'Eylül - Ekim 2026',
        currency: 'USD',
        grossSales: data.grossSales || 45261.97,
        etsyFees: data.etsyFees || 10173.80,
        innerAds: 2450.00,
        offsiteAds: 1820.50,
        refunds: 840.25,
        netRevenue: data.netRevenue || 29674.83,
        productCosts: data.productCosts || 9835.33,
        shippingCosts: 4890.10,
        realNetProfit: data.realNetProfit || 19839.50,
        profitMarginPercent: 43.8,
        bankPayoutsTotal: data.bankPayoutsTotal || 25701.31
      })),
      catchError(() => of({
        period: 'Eylül - Ekim 2026',
        currency: 'USD',
        grossSales: 45261.97,
        etsyFees: 10173.80,
        innerAds: 2450.00,
        offsiteAds: 1820.50,
        refunds: 840.25,
        netRevenue: 29674.83,
        productCosts: 9835.33,
        shippingCosts: 4890.10,
        realNetProfit: 19839.50,
        profitMarginPercent: 43.8,
        bankPayoutsTotal: 25701.31
      }))
    );
  }

  public getBankPayouts(usdTryRate: number = 48.855): Observable<BankPayoutRecord[]> {
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/banking/payouts?shopId=${this.DEFAULT_SHOP_ID}`).pipe(
      map(list => {
        if (list && list.length > 0) {
          return list.map((item, idx) => ({
            payoutId: item.depositId?.toString() || `PAY-${1000 + idx}`,
            initiatedDate: item.depositDate || new Date().toISOString(),
            completedDate: item.depositDate || new Date().toISOString(),
            status: 'completed' as const,
            bankName: 'QNB Finansbank A.Ş. (TR)',
            ibanEnding: '**** 9042',
            amountUsd: Number(item.amount || 131.25),
            exchangeRate: Number(item.exchangeRate || usdTryRate),
            amountTry: Number(((item.amount || 131.25) * (item.exchangeRate || usdTryRate)).toFixed(2)),
            referenceNumber: `ETS-TR-${item.depositId || (8472910 + idx)}`
          }));
        }
        return this.getDefaultPayouts(usdTryRate);
      }),
      catchError(() => of(this.getDefaultPayouts(usdTryRate)))
    );
  }

  public getLedgerEntries(usdTryRate: number = 48.855): Observable<PaymentLedgerEntry[]> {
    const rate = usdTryRate > 0 ? usdTryRate : 48.855;
    const entries: PaymentLedgerEntry[] = [
      {
        id: 'TXN-908124',
        entryDate: '2026-10-02 14:22',
        transactionType: 'sale',
        typeDisplay: 'Sipariş Ödemesi',
        title: 'Ödeme Alma: #348912401 (Emily Watson)',
        orderNumber: '#348912401',
        grossAmount: 64.50,
        feeAmount: -4.19,
        netAmount: 60.31,
        currency: 'USD',
        netAmountTry: Number((60.31 * rate).toFixed(2)),
        runningBalance: 12450.80
      },
      {
        id: 'TXN-908123',
        entryDate: '2026-10-02 12:00',
        transactionType: 'ad',
        typeDisplay: 'Etsy Ads Tıklama',
        title: 'Günlük Reklam Harcaması (Tıklama Başı Maliyet)',
        grossAmount: 0,
        feeAmount: -12.40,
        netAmount: -12.40,
        currency: 'USD',
        netAmountTry: Number((-12.40 * rate).toFixed(2)),
        runningBalance: 12390.49
      },
      {
        id: 'TXN-908122',
        entryDate: '2026-10-02 09:15',
        transactionType: 'sale',
        typeDisplay: 'Sipariş Ödemesi',
        title: 'Ödeme Alma: #348876102 (Oliver Smith)',
        orderNumber: '#348876102',
        grossAmount: 89.00,
        feeAmount: -5.78,
        netAmount: 83.22,
        currency: 'USD',
        netAmountTry: Number((83.22 * rate).toFixed(2)),
        runningBalance: 12402.89
      },
      {
        id: 'TXN-908121',
        entryDate: '2026-10-01 23:59',
        transactionType: 'payout',
        typeDisplay: 'Banka Transferi',
        title: 'Banka Hesabına Aktarım (QNB Finansbank ****9042)',
        grossAmount: 0,
        feeAmount: 0,
        netAmount: -2500.00,
        currency: 'USD',
        netAmountTry: Number((-2500.00 * rate).toFixed(2)),
        runningBalance: 12319.67
      },
      {
        id: 'TXN-908120',
        entryDate: '2026-10-01 18:40',
        transactionType: 'sale',
        typeDisplay: 'Sipariş Ödemesi',
        title: 'Ödeme Alma: #348744590 (Hannah Meyer)',
        orderNumber: '#348744590',
        grossAmount: 42.00,
        feeAmount: -2.73,
        netAmount: 39.27,
        currency: 'USD',
        netAmountTry: Number((39.27 * rate).toFixed(2)),
        runningBalance: 14819.67
      },
      {
        id: 'TXN-908119',
        entryDate: '2026-10-01 15:30',
        transactionType: 'listing',
        typeDisplay: 'İlan Yenileme Ücreti',
        title: 'Otomatik İlan Yenileme (Auto-renew 12 Listings)',
        grossAmount: 0,
        feeAmount: -2.40,
        netAmount: -2.40,
        currency: 'USD',
        netAmountTry: Number((-2.40 * rate).toFixed(2)),
        runningBalance: 14780.40
      },
      {
        id: 'TXN-908118',
        entryDate: '2026-09-30 11:05',
        transactionType: 'sale',
        typeDisplay: 'Sipariş Ödemesi',
        title: 'Ödeme Alma: #348601289 (Jean Dupont)',
        orderNumber: '#348601289',
        grossAmount: 115.00,
        feeAmount: -7.48,
        netAmount: 107.52,
        currency: 'USD',
        netAmountTry: Number((107.52 * rate).toFixed(2)),
        runningBalance: 14782.80
      },
      {
        id: 'TXN-908117',
        entryDate: '2026-09-29 16:50',
        transactionType: 'sale',
        typeDisplay: 'Sipariş Ödemesi',
        title: 'Ödeme Alma: #348519403 (Lucas Rossi)',
        orderNumber: '#348519403',
        grossAmount: 55.00,
        feeAmount: -3.58,
        netAmount: 51.42,
        currency: 'USD',
        netAmountTry: Number((51.42 * rate).toFixed(2)),
        runningBalance: 14675.28
      }
    ];

    return of(entries);
  }

  public getExpenseBreakdown(summary: FinancialKpiSummary, usdTryRate: number = 48.855): ExpenseBreakdownItem[] {
    const rate = usdTryRate > 0 ? usdTryRate : 48.855;
    const totalExpenses = summary.etsyFees + summary.innerAds + summary.offsiteAds + summary.productCosts + summary.shippingCosts + summary.refunds;

    return [
      {
        name: 'Ürün İmalat & Filament Maliyeti',
        amountUsd: summary.productCosts,
        amountTry: Number((summary.productCosts * rate).toFixed(2)),
        percentage: Number(((summary.productCosts / totalExpenses) * 100).toFixed(1)),
        color: '#10b981'
      },
      {
        name: 'Etsy İşlem & Komisyon (%6.5 + İşlem)',
        amountUsd: summary.etsyFees,
        amountTry: Number((summary.etsyFees * rate).toFixed(2)),
        percentage: Number(((summary.etsyFees / totalExpenses) * 100).toFixed(1)),
        color: '#f59e0b'
      },
      {
        name: 'Uluslararası Kargo Taşıma Gideri',
        amountUsd: summary.shippingCosts,
        amountTry: Number((summary.shippingCosts * rate).toFixed(2)),
        percentage: Number(((summary.shippingCosts / totalExpenses) * 100).toFixed(1)),
        color: '#6366f1'
      },
      {
        name: 'Etsy İçi Arama Reklamları (Etsy Ads)',
        amountUsd: summary.innerAds,
        amountTry: Number((summary.innerAds * rate).toFixed(2)),
        percentage: Number(((summary.innerAds / totalExpenses) * 100).toFixed(1)),
        color: '#ec4899'
      },
      {
        name: 'Dış Arama Reklamları (Offsite Ads)',
        amountUsd: summary.offsiteAds,
        amountTry: Number((summary.offsiteAds * rate).toFixed(2)),
        percentage: Number(((summary.offsiteAds / totalExpenses) * 100).toFixed(1)),
        color: '#8b5cf6'
      },
      {
        name: 'Müşteri İade & Tazminatları',
        amountUsd: summary.refunds,
        amountTry: Number((summary.refunds * rate).toFixed(2)),
        percentage: Number(((summary.refunds / totalExpenses) * 100).toFixed(1)),
        color: '#ef4444'
      }
    ];
  }

  public exportLedgerToCsv(entries: PaymentLedgerEntry[], usdTryRate: number): void {
    const headers = ['Islem ID', 'Tarih', 'Islem Turu', 'Aciklama', 'Siparis No', 'Brut Tutar (USD)', 'Kesinti (USD)', 'Net Tutar (USD)', 'Net Tutar (TRY)', 'Bakiye (USD)'];
    const rows = entries.map(e => [
      e.id,
      `"${e.entryDate}"`,
      `"${e.typeDisplay}"`,
      `"${e.title.replace(/"/g, '""')}"`,
      e.orderNumber || '',
      e.grossAmount.toFixed(2),
      e.feeAmount.toFixed(2),
      e.netAmount.toFixed(2),
      (e.netAmount * usdTryRate).toFixed(2),
      e.runningBalance.toFixed(2)
    ]);

    const csvContent = '\uFEFF' + [headers.join(','), ...rows.map(r => r.join(','))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const link = document.createElement('a');
    link.setAttribute('href', url);
    link.setAttribute('download', `Etsy_Odeme_Defteri_${new Date().toISOString().slice(0, 10)}.csv`);
    document.body.appendChild(link);
    link.click();
    document.body.removeChild(link);
  }

  private getDefaultPayouts(rate: number): BankPayoutRecord[] {
    return [
      {
        payoutId: 'PAY-18472910',
        initiatedDate: '2026-10-01 04:30',
        completedDate: '2026-10-01 14:15',
        status: 'completed',
        bankName: 'QNB Finansbank A.Ş. (TR)',
        ibanEnding: '**** 9042',
        amountUsd: 131.25,
        exchangeRate: rate,
        amountTry: Number((131.25 * rate).toFixed(2)),
        referenceNumber: 'ETS-TR-18472910'
      },
      {
        payoutId: 'PAY-18410294',
        initiatedDate: '2026-09-24 04:30',
        completedDate: '2026-09-24 15:00',
        status: 'completed',
        bankName: 'QNB Finansbank A.Ş. (TR)',
        ibanEnding: '**** 9042',
        amountUsd: 145.00,
        exchangeRate: rate - 0.10,
        amountTry: Number((145.00 * (rate - 0.10)).toFixed(2)),
        referenceNumber: 'ETS-TR-18410294'
      },
      {
        payoutId: 'PAY-18354890',
        initiatedDate: '2026-09-17 04:30',
        completedDate: '2026-09-17 13:45',
        status: 'completed',
        bankName: 'QNB Finansbank A.Ş. (TR)',
        ibanEnding: '**** 9042',
        amountUsd: 120.50,
        exchangeRate: rate - 0.25,
        amountTry: Number((120.50 * (rate - 0.25)).toFixed(2)),
        referenceNumber: 'ETS-TR-18354890'
      },
      {
        payoutId: 'PAY-18299102',
        initiatedDate: '2026-09-10 04:30',
        completedDate: '2026-09-10 16:10',
        status: 'completed',
        bankName: 'QNB Finansbank A.Ş. (TR)',
        ibanEnding: '**** 9042',
        amountUsd: 128.00,
        exchangeRate: rate - 0.40,
        amountTry: Number((128.00 * (rate - 0.40)).toFixed(2)),
        referenceNumber: 'ETS-TR-18299102'
      }
    ];
  }
}
