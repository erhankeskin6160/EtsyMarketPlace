import { Injectable } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { Observable } from 'rxjs';
import { map } from 'rxjs/operators';
import { 
  PaymentLedgerEntry, 
  BankPayoutRecord, 
  FinancialKpiSummary, 
  ExpenseBreakdownItem 
} from '../models/accounting.models';
import { environment } from '../../../environments/environment';

@Injectable({
  providedIn: 'root'
})
export class AccountingService {
  private readonly API_BASE = environment.apiBaseUrl;
  private readonly DEFAULT_SHOP_ID = environment.defaultShopId;

  constructor(private http: HttpClient) {}

  public getFinancialSummary(): Observable<FinancialKpiSummary> {
    return this.http.get<any>(`${this.API_BASE}/api/financial/summary`).pipe(
      map(data => ({
        period: data.period || '',
        currency: 'USD',
        grossSales: Number(data.grossSales) || 0,
        etsyFees: Number(data.etsyFees) || 0,
        innerAds: Number(data.innerAds) || 0,
        offsiteAds: Number(data.offsiteAds) || 0,
        refunds: Number(data.refunds) || 0,
        netRevenue: Number(data.netRevenue) || 0,
        productCosts: Number(data.productCosts) || 0,
        shippingCosts: Number(data.shippingCosts) || 0,
        realNetProfit: Number(data.realNetProfit) || 0,
        profitMarginPercent: Number(data.profitMarginPercent) || 0,
        bankPayoutsTotal: Number(data.bankPayoutsTotal) || 0
      }))
    );
  }

  public getBankPayouts(usdTryRate?: number): Observable<BankPayoutRecord[]> {
    return this.http.get<any[]>(`${this.API_BASE}/api/etsy/banking/payouts?shopId=${this.DEFAULT_SHOP_ID}`).pipe(
      map(list => (list || []).map(item => ({
        payoutId: String(item.referenceId || ''),
        initiatedDate: item.occurredAt || '',
        completedDate: item.occurredAt || '',
        status: item.status === 'completed' ? 'completed' as const : 'pending' as const,
        bankName: '',
        ibanEnding: '',
        amountUsd: item.currency === 'USD' ? Number(item.amount) : 0,
        exchangeRate: Number(item.exchangeRateToTry || usdTryRate || 0),
        amountTry: item.exchangeRateToTry ? Number(item.amount) * Number(item.exchangeRateToTry) : (item.currency === 'TRY' ? Number(item.amount) : 0),
        referenceNumber: String(item.referenceId || '')
      })))
    );
  }

  public getLedgerEntries(_usdTryRate?: number): Observable<PaymentLedgerEntry[]> {
    return new Observable<PaymentLedgerEntry[]>(subscriber => {
      subscriber.next([]);
      subscriber.complete();
    });
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

}
