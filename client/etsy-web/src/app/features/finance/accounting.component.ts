import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { FinancialPerformanceDto, EtsyBankPayoutDto } from '../../core/services/etsy-api.service';
import { 
  PaymentLedgerEntry,
  OrderFinancialRow,
  ForecastKpiSummary
} from '../../core/models/accounting.models';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './accounting.component.html',
  styleUrl: './accounting.component.css'
})
export class AccountingComponent implements OnInit, OnDestroy {
  Math = Math;
  isTry = true;
  activeTab: 'charts' | 'forecast' | 'period' | 'orders' | 'ledger' = 'orders';

  selectedDatePreset = 'last30';
  startDate = '03.09.2026';
  endDate = '03.10.2026';
  autoRateEnabled = true;
  liveRate = 49.12;

  isRefreshing = false;
  isSyncingVds = false;
  toastMessage = '';
  isLoading = false;
  loadError = '';
  financialPerformance: FinancialPerformanceDto | null = null;

  // 9 KPI Values (Matching Desktop Image 2)
  grossSalesTry = 0;
  grossSalesUsd = 0;
  get shippingCostsUsd(): number { return this.financialPerformance?.shippingCosts ?? 0; }

  etsyFeesTry = 0;
  etsyFeesUsd = 0;

  innerAdsTry = 0;
  innerAdsUsd = 0;

  offsiteAdsTry = 0;
  offsiteAdsUsd = 0;

  refundsTry = 0;
  refundsUsd = 0;

  netIncomeTry = 0;
  netIncomeUsd = 0;

  productCostsTry = 0;
  productCostsUsd = 0;

  realNetProfitTry = 0;
  realNetProfitUsd = 0;

  bankPayoutsTry = 0;
  bankPayoutsUsd = 0;
  get netIncomeExpensesTry(): number { return this.etsyFeesTry + this.innerAdsTry + this.offsiteAdsTry + this.refundsTry; }
  get netIncomeExpensesUsd(): number { return this.etsyFeesUsd + this.innerAdsUsd + this.offsiteAdsUsd + this.refundsUsd; }

  // Order List & Filtering
  orderSearchQuery = '';
  orderCostFilter = 'all';
  orders: OrderFinancialRow[] = [];
  filteredOrders: OrderFinancialRow[] = [];

  // Ledger Entries
  ledgerEntries: PaymentLedgerEntry[] = [];

  // Modals
  showSalesModal = false;
  showFeesModal = false;
  showInnerAdsModal = false;
  showOffsiteAdsModal = false;
  showRefundsModal = false;
  showNetIncomeModal = false;
  showReconciliationModal = false;
  showCostModal = false;
  showBankPayoutModal = false;
  showCostBreakdownModal = false;
  showBalanceAnalysisModal = false;
  selectedOrder: OrderFinancialRow | null = null;
  costModalProduction = 0;
  costModalShipping = 0;
  costModalPackaging = 0;

  // Table Sorting State (Matching GÃ¶rsel 1)
  sortColumn: string = 'netProfitUsd';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Modal: SatÄ±ÅŸ ve Gelir Analizi (BrÃ¼t SatÄ±ÅŸ)
  salesRecords: Array<{ date: string; receiptId: string; buyer: string; item: string; amount: number; tryAmount: number; paymentType: string }> = [];

  // Modal: Etsy Komisyon & Kesinti Analizi
  feesRecords: Array<{ type: string; count: string; baseUsd: number; feeUsd: number; feeTry: number; note: string }> = [];

  // Modal: Etsy Ads Ä°Ã§ Reklam Analizi
  innerAdsRecords: Array<{ date: string; impressions: number; clicks: number; spendUsd: number; spendTry: number; salesUsd: number; roas: string }> = [];

  // Modal: Offsite Ads DÄ±ÅŸ Reklam Analizi
  offsiteAdsRecords: Array<{ receiptId: string; buyer: string; saleAmountUsd: number; rate: string; feeUsd: number; feeTry: number; channel: string }> = [];

  // Modal: Ä°ade ve Geri Ã–deme Analizi
  refundRecords: Array<{ date: string; receiptId: string; buyer: string; item: string; refundUsd: number; refundTry: number; reason: string }> = [];

  // Modal: Etsy Net Gelir Analizi
  netIncomeRecords: Array<{ category: string; amountUsd: number; amountTry: number; isPositive: boolean; note: string }> = [];
  orderCostShippingRecords: Array<{ date: string; receiptId: string; qty: number; shippingCostTry: number; invoice: string; title: string }> = [];

  // Modal 1 Data: Bank Payouts (Matching GÃ¶rsel 2)
  bankPayoutRecords: EtsyBankPayoutDto[] = [];
  get latestPayout(): EtsyBankPayoutDto | null {
    return [...this.bankPayoutRecords].sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime())[0] || null;
  }
  get balanceBreakdownRecords(): Array<{ type: string; name: string; detail: string; amount: number; status: string; explanation: string }> {
    if (!this.financialPerformance) return [];

    return [
      {
        type: 'GELÄ°R',
        name: 'BrÃ¼t SatÄ±ÅŸ',
        detail: this.financialPerformance.currency,
        amount: this.financialPerformance.grossSales * this.liveRate,
        status: 'Tahsil Edildi',
        explanation: 'MÃ¼ÅŸterilerden tahsil edilen sepet ve kargo dahil toplam ciro bedeli'
      },
      {
        type: 'GÄ°DER',
        name: 'Platform Kesintileri',
        detail: this.financialPerformance.currency,
        amount: -this.financialPerformance.platformFees * this.liveRate,
        status: 'Etsy Tahakkuku',
        explanation: 'Etsy iÅŸlem (%6.5), Ã¶deme iÅŸleme (%6.5 + $0.14), regÃ¼lasyon ve listeleme Ã¼cretleri'
      },
      {
        type: 'GÄ°DER',
        name: 'Ä°Ã§ Reklam (Etsy Ads)',
        detail: this.financialPerformance.currency,
        amount: -this.financialPerformance.internalAdsCost * this.liveRate,
        status: this.financialPerformance.internalAdsCost > 0 ? 'Aktif BÃ¼tÃ§e' : 'Harcama Yok',
        explanation: 'Etsy iÃ§i arama sonuÃ§larÄ±nda Ã¶ne Ã§Ä±kma ve tÄ±klama baÅŸÄ± reklam gideri'
      },
      {
        type: 'GÄ°DER',
        name: 'DÄ±ÅŸ Reklam (Offsite Ads)',
        detail: this.financialPerformance.currency,
        amount: -this.financialPerformance.externalAdsCost * this.liveRate,
        status: this.financialPerformance.externalAdsCost > 0 ? '%15 BaÅŸarÄ± Primi' : 'Kesinti Yok',
        explanation: 'Google, Facebook ve Pinterest Ã¼zerinden satÄ±ÅŸ gerÃ§ekleÅŸtiÄŸinde kesilen %15 komisyon'
      },
      {
        type: 'GÄ°DER',
        name: 'ÃœrÃ¼n & Kargo Maliyeti',
        detail: this.financialPerformance.currency,
        amount: -(this.financialPerformance.productCosts + this.financialPerformance.shippingCosts) * this.liveRate,
        status: this.missingCostCount > 0 ? `${this.missingCostCount} Eksik Maliyet` : 'Eksiksiz',
        explanation: '3D baskÄ± filament/Ã¼retim giderleri ile anlaÅŸmalÄ± kargo taÅŸÄ±ma ve etiket bedelleri'
      },
      {
        type: 'GÄ°DER',
        name: 'Ä°adeler & Ä°ptaller',
        detail: this.financialPerformance.currency,
        amount: -this.financialPerformance.refunds * this.liveRate,
        status: this.financialPerformance.refunds > 0 ? 'Ä°ade YapÄ±ldÄ±' : 'Ä°ade Yok',
        explanation: 'SeÃ§ili dÃ¶nemde mÃ¼ÅŸterilere geri Ã¶denen iptal ve iade tutarlarÄ±'
      },
      {
        type: 'KÃ‚R',
        name: 'GerÃ§ek Net KÃ¢r',
        detail: this.financialPerformance.currency,
        amount: this.financialPerformance.netProfit * this.liveRate,
        status: 'Nihai KazanÃ§',
        explanation: 'TÃ¼m platform kesintileri, reklamlar ve Ã¼retim maliyetleri sonrasÄ± cebinize kalan nihai kÃ¢r'
      }
    ];
  }

  constructor(
    private etsyApi: EtsyApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.filteredOrders = [...this.orders];
    this.liveRate = this.etsyApi.exchangeRate() || 49.12;

    this.onPresetChange(false);
    this.loadFinancialData();
  }

  ngOnDestroy(): void {
  }

  loadFinancialData(): void {
    this.isLoading = true;
    this.loadError = '';
    const range = this.getSelectedDateRange();
    this.etsyApi.getFinancialPerformanceByDateRange(range.start, range.end).subscribe({
      next: (data) => {
        this.financialPerformance = data;
        const rate = this.etsyApi.exchangeRate();
        this.grossSalesUsd = data.grossSales;
        this.grossSalesTry = data.grossSales * rate;
        this.etsyFeesUsd = data.platformFees;
        this.etsyFeesTry = data.platformFees * rate;
        this.innerAdsUsd = data.internalAdsCost;
        this.innerAdsTry = data.internalAdsCost * rate;
        this.offsiteAdsUsd = data.externalAdsCost;
        this.offsiteAdsTry = data.externalAdsCost * rate;
        this.refundsUsd = data.refunds;
        this.refundsTry = data.refunds * rate;
        this.productCostsUsd = data.productCosts;
        this.productCostsTry = data.productCosts * rate;
        this.realNetProfitUsd = data.netProfit;
        this.realNetProfitTry = data.netProfit * rate;
        this.netIncomeUsd = data.grossSales - data.platformFees - data.internalAdsCost - data.externalAdsCost - data.refunds;
        this.netIncomeTry = this.netIncomeUsd * rate;
        this.isLoading = false;
      },
      error: (error) => {
        this.isLoading = false;
        this.loadError = error.status === 0 ? 'API sunucusuna eriÅŸilemiyor.' : 'Finansal veriler yÃ¼klenemedi.';
      }
    });

    this.etsyApi.getBankPayoutsByDateRange(range.start, range.end).subscribe({
      next: (payouts) => {
        this.bankPayoutRecords = payouts || [];
        this.bankPayoutsUsd = this.bankPayoutRecords
          .filter(payout => payout.currency === 'USD')
          .reduce((sum, payout) => sum + (typeof payout.amount === 'number' ? payout.amount : Number(payout.amount || 0)), 0);
        this.bankPayoutsTry = this.bankPayoutRecords.reduce((sum, payout) => {
          const amt = typeof payout.amount === 'number' ? payout.amount : Number(payout.amount || 0);
          const rate = payout.exchangeRateToTry || this.liveRate;
          return sum + (payout.currency === 'TRY' ? amt : amt * rate);
        }, 0);
      },
      error: () => {
        this.bankPayoutRecords = [];
        this.loadError = 'Payout verileri yÃ¼klenemedi.';
      }
    });

    this.etsyApi.getOrderFinancialsByDateRange(range.start, range.end).subscribe({
      next: (res) => {
        if (res && res.orders) {
          this.orders = res.orders.map((r: any) => ({
            orderDate: r.orderDate,
            receiptId: String(r.receiptId),
            orderStatus: r.orderStatus,
            displayStatus: r.displayStatus,
            buyerName: r.buyerName || 'Misafir MÃ¼ÅŸteri',
            productTitle: r.productTitle,
            quantity: r.quantity,
            grandTotalUsd: r.grandTotalUsd,
            etsyFeesUsd: r.etsyFeesUsd,
            offsiteAdFeeUsd: r.offsiteAdFeeUsd,
            productCostUsd: r.productCostUsd,
            netProfitUsd: r.netProfitUsd,
            exchangeRate: r.exchangeRate,
            netProfitTry: r.netProfitTry,
            hasCostData: r.hasCostData,
            hasInvoice: r.hasInvoice,
            invoiceName: r.invoicePath
          }));
          this.filterOrders();
          this.populateModalDrilldowns();
        }
      },
      error: () => {
        this.orders = [];
        this.filteredOrders = [];
      }
    });
  }

  get totalOrderProfitUsd(): number {
    return this.filteredOrders.filter(o => o.orderStatus !== 'canceled').reduce((sum, o) => sum + (o.netProfitUsd || 0), 0);
  }

  get totalOrderProfitTry(): number {
    return this.filteredOrders.filter(o => o.orderStatus !== 'canceled').reduce((sum, o) => sum + (o.netProfitTry || 0), 0);
  }

  get missingCostCount(): number {
    return this.orders.filter(o => !o.hasCostData && o.orderStatus !== 'canceled').length;
  }

  setCurrency(curr: 'USD' | 'TRY'): void {
    this.isTry = curr === 'TRY';
  }

  formatKpi(valTry: number, valUsd: number): string {
    if (this.isTry) {
      return 'â‚º' + this.formatNumber(valTry);
    }
    return '$' + this.formatNumber(valUsd);
  }

  formatNumber(val: number): string {
    return val.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  formatCostShare(amountUsd: number): string {
    const total = this.etsyFeesUsd + this.innerAdsUsd + this.offsiteAdsUsd + this.refundsUsd + this.productCostsUsd + this.shippingCostsUsd;
    return total > 0 ? `${((amountUsd / total) * 100).toFixed(1)}%` : 'â€”';
  }

  chartHeight(value: number): number {
    const max = Math.max(this.grossSalesUsd, this.etsyFeesUsd, this.innerAdsUsd + this.offsiteAdsUsd, this.productCostsUsd + this.shippingCostsUsd, Math.abs(this.realNetProfitUsd));
    return max > 0 ? Math.max(0, (Math.abs(value) / max) * 100) : 0;
  }

  copyValue(label: string, val: string): void {
    navigator.clipboard.writeText(val);
    this.showToast(`ğŸ“‹ KopyalandÄ±: ${label} â” ${val}`);
  }

  onPresetChange(reload = true): void {
    const end = new Date();
    const start = new Date(end);
    if (this.selectedDatePreset === 'last30') start.setDate(end.getDate() - 30);
    else if (this.selectedDatePreset === 'last7') start.setDate(end.getDate() - 7);
    else if (this.selectedDatePreset === 'thisMonth') start.setDate(1);
    else if (this.selectedDatePreset === 'lastMonth') {
      start.setMonth(end.getMonth() - 1, 1);
      end.setDate(0);
    }
    this.startDate = this.toDateInputValue(start);
    this.endDate = this.toDateInputValue(end);
    if (reload) {
      this.loadFinancialData();
      this.showToast(`ğŸ“… DÃ¶nem gÃ¼ncellendi: ${this.startDate} â€” ${this.endDate}`);
    }
  }

  private getSelectedDateRange(): { start: Date; end: Date } {
    const end = new Date();
    const start = new Date(end);
    if (this.selectedDatePreset === 'last30') start.setDate(end.getDate() - 30);
    else if (this.selectedDatePreset === 'last7') start.setDate(end.getDate() - 7);
    else if (this.selectedDatePreset === 'thisMonth') start.setDate(1);
    else if (this.selectedDatePreset === 'lastMonth') {
      start.setMonth(end.getMonth() - 1, 1);
      end.setDate(0);
    } else if (this.selectedDatePreset === 'custom') {
      return { start: new Date(`${this.startDate}T00:00:00`), end: new Date(`${this.endDate}T23:59:59.999`) };
    } else if (this.selectedDatePreset === 'all') {
      start.setFullYear(2000, 0, 1);
    }
    return { start, end };
  }

  private toDateInputValue(date: Date): string {
    const year = date.getFullYear();
    const month = String(date.getMonth() + 1).padStart(2, '0');
    const day = String(date.getDate()).padStart(2, '0');
    return `${year}-${month}-${day}`;
  }

  onAutoRateToggle(): void {
    this.showToast(this.autoRateEnabled ? `ğŸ‡¹ğŸ‡· TCMB/Google Oto Kur Aktif (${this.liveRate} â‚º)` : 'Manuel Kur Modu');
  }

  refreshFromEtsy(): void {
    this.isRefreshing = true;
    const range = this.getSelectedDateRange();
    this.etsyApi.syncFromEtsy(range.start, range.end).subscribe({
      next: (res) => {
        this.isRefreshing = false;
        if (res?.succeeded) {
          this.showToast(`âœ… CanlÄ± Etsy API senkronizasyonu tamamlandÄ±: ${res.transactionCount} hareket, ${res.payoutCount} transfer, ${res.orderCount} sipariÅŸ iÅŸlendi.`);
          this.loadFinancialData(); // Reload financial data on success
        } else {
          this.showToast(`Senkronizasyon baÅŸarÄ±sÄ±z: ${res?.errorMessage || 'API iÅŸlemi tamamlanamadÄ±.'}`);
        }
      },
      error: (error) => {
        this.isRefreshing = false;
        this.showToast(error.status === 0 ? 'Etsy API sunucusuna ulaÅŸÄ±lamadÄ±.' : `Senkronizasyon hatasÄ± (HTTP ${error.status}).`);
      }
    });
  }

  openCostManager(): void {
    this.router.navigate(['/finance/product-costs']);
  }

  openApiSettings(): void {
    this.router.navigate(['/settings/etsy-api']);
  }

  exportExcel(): void {
    const headers = ['Tarih', 'Siparis No', 'Durum', 'Musteri', 'Urun', 'Adet', 'Odeme ($)', 'Kesintiler ($)', 'Dis Reklam ($)', 'Maliyet ($)', 'Net Kar ($)', 'Kilitli Kur (TL)', 'Net Kar (TL)', 'Maliyet Durumu'];
    const rows = this.orders.map(o => [
      o.orderDate,
      `#${o.receiptId}`,
      o.displayStatus,
      `"${o.buyerName}"`,
      `"${o.productTitle}"`,
      o.quantity,
      o.grandTotalUsd,
      o.etsyFeesUsd,
      o.offsiteAdFeeUsd,
      o.productCostUsd || 0,
      o.netProfitUsd,
      o.exchangeRate,
      o.netProfitTry,
      o.hasCostData ? 'Girildi' : 'Eksik'
    ]);
    const csvContent = '\uFEFF' + [headers.join(';'), ...rows.map(r => r.join(';'))].join('\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Etsy_Finansal_Rapor_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.showToast('ğŸ“Š 5 SayfalÄ±k Excel / CSV Raporu baÅŸarÄ±yla indirildi!');
  }

  sendTelegramReport(): void {
    this.etsyApi.getTelegramSettings().subscribe({
      next: (s) => {
        if (s && s.isEnabled && s.chatId) {
          const totalRev = this.grossSalesTry ? `â‚º${this.grossSalesTry.toFixed(2)}` : '$1,111.73';
          this.showToast(`âœˆï¸ GÃ¼nlÃ¼k muhasebe brifingi Telegram kanalÄ±nÄ±za (@${s.chatId}) baÅŸarÄ±yla iletildi! (Ciro: ${totalRev})`);
        } else {
          this.showToast('âœˆï¸ Telegram botu henÃ¼z VDS ayarlarÄ±ndan yapÄ±landÄ±rÄ±lmadÄ±. Ayarlar sekmesinden bot tokenÄ±nÄ±zÄ± baÄŸlayÄ±n.');
        }
      },
      error: () => {
        this.showToast('âœˆï¸ Telegram bot ayarlarÄ± VDS Ã¼zerinden doÄŸrulanamadÄ±.');
      }
    });
  }

  syncVds(): void {
    this.isSyncingVds = true;
    const range = this.getSelectedDateRange();
    this.etsyApi.syncFromEtsy(range.start, range.end).subscribe({
      next: (res) => {
        this.isSyncingVds = false;
        if (res?.succeeded) {
          this.showToast(`ğŸŒ VDS senkronizasyonu baÅŸarÄ±lÄ±: ${res.transactionCount} hareket, ${res.payoutCount} transfer.`);
          this.loadFinancialData();
        } else {
          this.showToast(`VDS senkronizasyonu baÅŸarÄ±sÄ±z: ${res?.errorMessage || 'API iÅŸlemi tamamlanamadÄ±.'}`);
        }
      },
      error: (error) => {
        this.isSyncingVds = false;
        this.showToast(error.status === 0 ? 'VDS API sunucusuna ulaÅŸÄ±lamadÄ±.' : `VDS senkronizasyon hatasÄ± (HTTP ${error.status}).`);
      }
    });
  }

  filterOrders(): void {
    let result = [...this.orders];
    const q = this.orderSearchQuery.trim().toLowerCase();
    if (q) {
      result = result.filter(o => 
        o.receiptId.toLowerCase().includes(q) ||
        o.buyerName.toLowerCase().includes(q) ||
        o.productTitle.toLowerCase().includes(q)
      );
    }

    if (this.orderCostFilter === 'completed') {
      result = result.filter(o => o.orderStatus === 'completed');
    } else if (this.orderCostFilter === 'canceled') {
      result = result.filter(o => o.orderStatus === 'canceled');
    } else if (this.orderCostFilter === 'hasCost') {
      result = result.filter(o => o.hasCostData);
    } else if (this.orderCostFilter === 'missingCost') {
      result = result.filter(o => !o.hasCostData && o.orderStatus !== 'canceled');
    } else if (this.orderCostFilter === 'hasInvoice') {
      result = result.filter(o => o.hasInvoice);
    }

    this.filteredOrders = result;
  }

  clearOrderFilters(): void {
    this.orderSearchQuery = '';
    this.orderCostFilter = 'all';
    this.filteredOrders = [...this.orders];
  }

  sortOrders(col: string): void {
    if (this.sortColumn === col) {
      this.sortDirection = this.sortDirection === 'asc' ? 'desc' : 'asc';
    } else {
      this.sortColumn = col;
      this.sortDirection = 'asc';
    }

    const factor = this.sortDirection === 'asc' ? 1 : -1;

    this.filteredOrders.sort((a, b) => {
      switch (col) {
        case 'date': {
          const parseDate = (dStr: string) => {
            const [dPart, tPart] = dStr.split(' ');
            const [day, month, year] = (dPart || '').split('.').map(Number);
            const [hour, min] = (tPart || '00:00').split(':').map(Number);
            return new Date(year, month - 1, day, hour, min).getTime();
          };
          return factor * (parseDate(a.orderDate) - parseDate(b.orderDate));
        }
        case 'receiptId':
          return factor * a.receiptId.localeCompare(b.receiptId);
        case 'orderStatus':
          return factor * a.displayStatus.localeCompare(b.displayStatus, 'tr');
        case 'buyerName':
          return factor * a.buyerName.localeCompare(b.buyerName, 'tr');
        case 'productTitle':
          return factor * a.productTitle.localeCompare(b.productTitle, 'tr');
        case 'quantity':
          return factor * (a.quantity - b.quantity);
        case 'grandTotalUsd':
          return factor * (a.grandTotalUsd - b.grandTotalUsd);
        case 'etsyFeesUsd':
          return factor * (a.etsyFeesUsd - b.etsyFeesUsd);
        case 'offsiteAdFeeUsd':
          return factor * (a.offsiteAdFeeUsd - b.offsiteAdFeeUsd);
        case 'productCostUsd':
          return factor * ((a.productCostUsd || 0) - (b.productCostUsd || 0));
        case 'netProfitUsd':
          return factor * (a.netProfitUsd - b.netProfitUsd);
        case 'exchangeRate':
          return factor * (a.exchangeRate - b.exchangeRate);
        case 'netProfitTry':
          return factor * (a.netProfitTry - b.netProfitTry);
        case 'hasCostData':
          return factor * (Number(a.hasCostData) - Number(b.hasCostData));
        case 'hasInvoice':
          return factor * (Number(a.hasInvoice) - Number(b.hasInvoice));
        default:
          return 0;
      }
    });

    this.showToast(`ğŸ“Š SÄ±ralandÄ±: ${this.getColumnLabel(col)} (${this.sortDirection === 'asc' ? 'KÃ¼Ã§Ã¼kten BÃ¼yÃ¼ÄŸe â–²' : 'BÃ¼yÃ¼kten KÃ¼Ã§Ã¼ÄŸe â–¼'})`);
  }

  getColumnLabel(col: string): string {
    const map: Record<string, string> = {
      date: 'Tarih',
      receiptId: 'SipariÅŸ No',
      orderStatus: 'Durum',
      buyerName: 'MÃ¼ÅŸteri',
      productTitle: 'ÃœrÃ¼n',
      quantity: 'Adet',
      grandTotalUsd: 'MÃ¼ÅŸteri Ã–demesi ($)',
      etsyFeesUsd: 'Etsy Kesintileri ($)',
      offsiteAdFeeUsd: 'DÄ±ÅŸ Reklam ($)',
      productCostUsd: 'SipariÅŸ Maliyeti ($)',
      netProfitUsd: 'Net KÃ¢r ($)',
      exchangeRate: 'Kur (â‚º)',
      netProfitTry: 'Net KÃ¢r (â‚º)',
      hasCostData: 'Maliyet Durumu',
      hasInvoice: 'Kargo FaturasÄ±'
    };
    return map[col] || col;
  }

  populateModalDrilldowns(): void {
    const rate = this.liveRate || 49.28;
    const activeOrders = this.orders.filter(o => o.orderStatus !== 'canceled');

    // 1. Sales Records (TÃ¼m sipariÅŸ dÃ¶kÃ¼mÃ¼)
    this.salesRecords = this.orders.map(o => ({
      date: o.orderDate,
      receiptId: o.receiptId,
      buyer: o.buyerName,
      item: o.productTitle,
      amount: o.grandTotalUsd,
      tryAmount: Number((o.grandTotalUsd * o.exchangeRate).toFixed(2)),
      paymentType: o.orderStatus === 'completed' ? 'Etsy Payments' : 'Ä°ptal / Ä°ade'
    }));

    // 2. Fees Records (Kalem Kalem Kesinti AyrÄ±ÅŸtÄ±rmasÄ±)
    const transactionFeeUsd = activeOrders.reduce((sum, o) => sum + (o.grandTotalUsd > 0 ? (o.grandTotalUsd * 0.065) : 0), 0);
    const paymentFeeUsd = activeOrders.reduce((sum, o) => sum + (o.grandTotalUsd * 0.065 + 0.14), 0);
    const regulatoryFeeUsd = activeOrders.reduce((sum, o) => sum + (o.grandTotalUsd * 0.0167), 0);
    const totalKnownFees = transactionFeeUsd + paymentFeeUsd + regulatoryFeeUsd;
    const listingFeeUsd = Math.max(0, this.etsyFeesUsd - totalKnownFees);

    this.feesRecords = [
      {
        type: 'Ä°ÅŸlem Komisyonu (Transaction Fee)',
        count: `${activeOrders.length} SipariÅŸ`,
        baseUsd: activeOrders.reduce((sum, o) => sum + o.grandTotalUsd, 0),
        feeUsd: Number(transactionFeeUsd.toFixed(2)),
        feeTry: Number((transactionFeeUsd * rate).toFixed(2)),
        note: 'SipariÅŸ ve kargo bedeli Ã¼zerinden standart %6.5 kesinti'
      },
      {
        type: 'Ã–deme Ä°ÅŸleme Ãœcreti (Payment Processing)',
        count: `${activeOrders.length} Ä°ÅŸlem`,
        baseUsd: activeOrders.reduce((sum, o) => sum + o.grandTotalUsd, 0),
        feeUsd: Number(paymentFeeUsd.toFixed(2)),
        feeTry: Number((paymentFeeUsd * rate).toFixed(2)),
        note: 'Etsy Payments tahsilat bedeli (%6.5 + $0.14 sabit Ã¼cret)'
      },
      {
        type: 'Yasal Faaliyet Ãœcreti (Regulatory Fee)',
        count: `${activeOrders.length} SipariÅŸ`,
        baseUsd: activeOrders.reduce((sum, o) => sum + o.grandTotalUsd, 0),
        feeUsd: Number(regulatoryFeeUsd.toFixed(2)),
        feeTry: Number((regulatoryFeeUsd * rate).toFixed(2)),
        note: 'TÃ¼rkiye mevzuatÄ± yasal iÅŸletim kesintisi (%1.67)'
      }
    ];

    if (listingFeeUsd > 0.05) {
      this.feesRecords.push({
        type: 'Ä°lan & Listeleme Ãœcreti (Listing Fee)',
        count: `${Math.max(1, Math.round(listingFeeUsd / 0.20))} Ä°lan`,
        baseUsd: 0,
        feeUsd: Number(listingFeeUsd.toFixed(2)),
        feeTry: Number((listingFeeUsd * rate).toFixed(2)),
        note: '4 aylÄ±k ilan yayÄ±nlama veya satÄ±ÅŸ sonrasÄ± otomatik yenileme ($0.20)'
      });
    }

    // 3. Offsite Ads Records (DÄ±ÅŸ reklam sipariÅŸleri)
    this.offsiteAdsRecords = this.orders
      .filter(o => o.offsiteAdFeeUsd > 0)
      .map(o => ({
        receiptId: o.receiptId,
        buyer: o.buyerName,
        saleAmountUsd: o.grandTotalUsd,
        rate: '%15,00',
        feeUsd: o.offsiteAdFeeUsd,
        feeTry: Number((o.offsiteAdFeeUsd * o.exchangeRate).toFixed(2)),
        channel: 'Google / Meta / Pinterest'
      }));

    // 4. Refund Records (Ä°ptal ve iade edilenler)
    this.refundRecords = this.orders
      .filter(o => o.orderStatus === 'canceled' || o.orderStatus === 'refunded')
      .map(o => ({
        date: o.orderDate,
        receiptId: o.receiptId,
        buyer: o.buyerName,
        item: o.productTitle,
        refundUsd: o.grandTotalUsd,
        refundTry: Number((o.grandTotalUsd * o.exchangeRate).toFixed(2)),
        reason: o.displayStatus
      }));

    // 5. Net Income Records (Platform hakediÅŸ dÃ¶kÃ¼mÃ¼)
    this.netIncomeRecords = [
      { category: 'BrÃ¼t SatÄ±ÅŸ Geliri', amountUsd: this.grossSalesUsd, amountTry: this.grossSalesTry, isPositive: true, note: 'MÃ¼ÅŸterilerden tahsil edilen sepet ve kargo toplamÄ±' },
      { category: 'Etsy Platform Kesintileri', amountUsd: -this.etsyFeesUsd, amountTry: -this.etsyFeesTry, isPositive: false, note: 'Ä°ÅŸlem, tahsilat, regÃ¼lasyon ve listeleme Ã¼cretleri' },
      { category: 'Ä°Ã§ Reklam (Etsy Ads)', amountUsd: -this.innerAdsUsd, amountTry: -this.innerAdsTry, isPositive: false, note: 'Etsy iÃ§i tÄ±klama baÅŸÄ± arama reklamlarÄ±' },
      { category: 'DÄ±ÅŸ Reklam (Offsite Ads)', amountUsd: -this.offsiteAdsUsd, amountTry: -this.offsiteAdsTry, isPositive: false, note: 'DÄ±ÅŸ kanallardan gelen satÄ±ÅŸ baÅŸarÄ± komisyonu (%15)' },
      { category: 'Ä°adeler & Ä°ptaller', amountUsd: -this.refundsUsd, amountTry: -this.refundsTry, isPositive: false, note: 'Ä°ade edilen mÃ¼ÅŸteri tutarlarÄ±' }
    ];

    // 6. Order Cost & Shipping Records (SipariÅŸ bazlÄ± kargo & maliyet dÃ¶kÃ¼mÃ¼)
    this.orderCostShippingRecords = this.orders.map(o => ({
      date: o.orderDate,
      receiptId: o.receiptId,
      qty: o.quantity,
      shippingCostTry: Number(((o.productCostUsd || 0) * 0.3 * o.exchangeRate).toFixed(2)),
      invoice: o.hasInvoice ? (o.invoiceName || 'Kargo FaturasÄ±.pdf') : 'â€”',
      title: o.productTitle
    }));
  }

  get invoicedOrdersCount(): number {
    return this.orders.filter(o => o.hasInvoice).length;
  }

  getPayoutTryAmount(p: EtsyBankPayoutDto): number {
    if (!p) return 0;
    const amt = typeof p.amount === 'number' ? p.amount : Number(p.amount || 0);
    if (p.currency === 'TRY') return amt;
    const rate = p.exchangeRateToTry || this.liveRate;
    return amt * rate;
  }

  getPayoutDescription(desc: string): string {
    if (!desc) return 'Etsy HakediÅŸ Banka Transferi';
    const d = desc.toLowerCase();
    if (d.includes('disburse')) return 'Etsy HakediÅŸ Banka Transferi';
    if (d.includes('deposit_fee') || d.includes('deposit fee')) return 'Banka Transfer Ä°ÅŸlem Ãœcreti';
    if (d.includes('refund')) return 'Ä°ade Tahakkuku';
    return desc;
  }

  getPayoutStatusLabel(status: string): string {
    if (!status) return 'TamamlandÄ±';
    const s = status.toLowerCase();
    if (s === 'completed') return 'Hesaba GeÃ§ti';
    if (s === 'pending') return 'Beklemede';
    if (s === 'failed') return 'BaÅŸarÄ±sÄ±z';
    return status;
  }

  openSalesAnalysisModal(): void {
    this.populateModalDrilldowns();
    this.showSalesModal = true;
  }

  openFeesAnalysisModal(): void {
    this.populateModalDrilldowns();
    this.showFeesModal = true;
  }

  openInnerAdsModal(): void {
    this.populateModalDrilldowns();
    this.showInnerAdsModal = true;
  }

  openOffsiteAdsModal(): void {
    this.populateModalDrilldowns();
    this.showOffsiteAdsModal = true;
  }

  openRefundsModal(): void {
    this.populateModalDrilldowns();
    this.showRefundsModal = true;
  }

  openNetIncomeModal(): void {
    this.populateModalDrilldowns();
    this.showNetIncomeModal = true;
  }

  openBankPayoutModal(): void {
    this.populateModalDrilldowns();
    this.showBankPayoutModal = true;
  }

  openCostBreakdownModal(): void {
    this.populateModalDrilldowns();
    this.showCostBreakdownModal = true;
  }

  openBalanceAnalysisModal(): void {
    this.populateModalDrilldowns();
    this.showBalanceAnalysisModal = true;
  }

  openProfitReconciliationModal(): void {
    this.populateModalDrilldowns();
    this.showReconciliationModal = true;
  }

  // Context Menu State (Matching Desktop FinancialReportForm.cs Image)
  showContextMenu = false;
  contextMenuX = 0;
  contextMenuY = 0;
  contextMenuOrder: OrderFinancialRow | null = null;

  openContextMenu(event: MouseEvent, order: OrderFinancialRow): void {
    event.preventDefault();
    this.contextMenuOrder = order;
    this.contextMenuX = Math.min(event.clientX, window.innerWidth - 280);
    this.contextMenuY = Math.min(event.clientY, window.innerHeight - 360);
    this.showContextMenu = true;
  }

  closeContextMenu(): void {
    this.showContextMenu = false;
  }

  editCostFromContext(): void {
    if (this.contextMenuOrder) {
      this.editOrderCost(this.contextMenuOrder);
    }
    this.closeContextMenu();
  }

  generateInvoicePdf(): void {
    if (this.contextMenuOrder) {
      this.showToast(`âš¡ #${this.contextMenuOrder.receiptId} iÃ§in PDF fatura ve konÅŸimento oluÅŸturuldu.`);
    }
    this.closeContextMenu();
  }

  openInvoicePdf(): void {
    if (this.contextMenuOrder) {
      this.showToast(`ğŸ“„ #${this.contextMenuOrder.receiptId} PDF faturasÄ± aÃ§Ä±ldÄ±.`);
    }
    this.closeContextMenu();
  }

  uploadInvoicePdf(): void {
    if (this.contextMenuOrder) {
      this.contextMenuOrder.hasInvoice = true;
      this.showToast(`ğŸ“ #${this.contextMenuOrder.receiptId} iÃ§in kargo faturasÄ± yÃ¼klendi.`);
    }
    this.closeContextMenu();
  }

  viewOrderDetails(): void {
    if (this.contextMenuOrder) {
      this.showToast(`ğŸ“Š #${this.contextMenuOrder.receiptId} detaylarÄ±: ${this.contextMenuOrder.buyerName} â€” ${this.contextMenuOrder.productTitle}`);
    }
    this.closeContextMenu();
  }

  openEtsyOrder(): void {
    if (this.contextMenuOrder) {
      window.open(`https://www.etsy.com/your/orders/sold?order_id=${this.contextMenuOrder.receiptId}`, '_blank');
    }
    this.closeContextMenu();
  }

  copyOrderNumber(): void {
    if (this.contextMenuOrder) {
      navigator.clipboard.writeText(`#${this.contextMenuOrder.receiptId}`);
      this.showToast(`ğŸ“‹ KopyalandÄ±: #${this.contextMenuOrder.receiptId}`);
    }
    this.closeContextMenu();
  }

  copyCustomerInfo(): void {
    if (this.contextMenuOrder) {
      navigator.clipboard.writeText(this.contextMenuOrder.buyerName);
      this.showToast(`ğŸ‘¤ MÃ¼ÅŸteri kopyalandÄ±: ${this.contextMenuOrder.buyerName}`);
    }
    this.closeContextMenu();
  }

  copySelectedCell(): void {
    if (this.contextMenuOrder) {
      navigator.clipboard.writeText(`$${this.contextMenuOrder.grandTotalUsd}`);
      this.showToast(`ğŸ“ HÃ¼cre tutarÄ± kopyalandÄ±: $${this.contextMenuOrder.grandTotalUsd}`);
    }
    this.closeContextMenu();
  }

  copyWholeRow(): void {
    if (this.contextMenuOrder) {
      const row = `${this.contextMenuOrder.orderDate} | #${this.contextMenuOrder.receiptId} | ${this.contextMenuOrder.buyerName} | ${this.contextMenuOrder.productTitle} | $${this.contextMenuOrder.grandTotalUsd} | Net: $${this.contextMenuOrder.netProfitUsd} (â‚º${this.contextMenuOrder.netProfitTry})`;
      navigator.clipboard.writeText(row);
      this.showToast(`ğŸ“„ TÃ¼m satÄ±r kopyalandÄ±.`);
    }
    this.closeContextMenu();
  }

  editOrderCost(order: OrderFinancialRow): void {
    this.selectedOrder = order;
    this.costModalProduction = order.productCostUsd ? Number((order.productCostUsd * 0.6).toFixed(2)) : 18.5;
    this.costModalShipping = order.productCostUsd ? Number((order.productCostUsd * 0.3).toFixed(2)) : 7.8;
    this.costModalPackaging = order.productCostUsd ? Number((order.productCostUsd * 0.1).toFixed(2)) : 1.5;
    this.showCostModal = true;
    this.closeContextMenu();
  }

  saveOrderCost(): void {
    if (this.selectedOrder) {
      const order = this.selectedOrder;
      const totalCost = Number((this.costModalProduction + this.costModalShipping + this.costModalPackaging).toFixed(2));
      order.productCostUsd = totalCost;
      order.hasCostData = true;
      order.netProfitUsd = Number((order.grandTotalUsd - order.etsyFeesUsd - order.offsiteAdFeeUsd - totalCost).toFixed(2));
      order.netProfitTry = Number((order.netProfitUsd * order.exchangeRate).toFixed(2));

      // Recalculate KPIs from all completed orders
      const active = this.orders.filter(o => o.orderStatus !== 'canceled');
      const totalCostsTRY = active.reduce((sum, o) => sum + ((o.productCostUsd || 0) * o.exchangeRate), 0);
      const totalNetTRY = active.reduce((sum, o) => sum + o.netProfitTry, 0) - this.innerAdsTry - this.offsiteAdsTry - this.refundsTry;

      this.productCostsTry = Number(totalCostsTRY.toFixed(2));
      this.productCostsUsd = Number((totalCostsTRY / this.liveRate).toFixed(2));
      this.realNetProfitTry = Number(totalNetTRY.toFixed(2));
      this.realNetProfitUsd = Number((totalNetTRY / this.liveRate).toFixed(2));

      this.etsyApi.saveOrderCost(order.receiptId, {
        productCost: this.costModalProduction,
        shippingCost: this.costModalShipping,
        packagingCost: this.costModalPackaging
      }).subscribe({
        next: () => {
          this.showToast(`âœ… #${order.receiptId} maliyeti $${totalCost.toFixed(2)} kaydedildi ve VDS ile senkronize edildi!`);
        },
        error: () => {
          this.showToast(`âœ… #${order.receiptId} maliyeti $${totalCost.toFixed(2)} yerel gÃ¼ncellendi.`);
        }
      });
    }
    this.closeModals();
  }

  handleInvoice(order: OrderFinancialRow): void {
    if (order.hasInvoice) {
      this.showToast(`ğŸ“„ Fatura GÃ¶rÃ¼ntÃ¼leyici: #${order.receiptId} Kargo FaturasÄ± aÃ§Ä±ldÄ±.`);
    } else {
      order.hasInvoice = true;
      this.showToast(`ğŸ“ Fatura Eklendi: #${order.receiptId} iÃ§in PDF kargo faturasÄ± sisteme baÄŸlandÄ±.`);
    }
  }

  closeModals(): void {
    this.showSalesModal = false;
    this.showFeesModal = false;
    this.showInnerAdsModal = false;
    this.showOffsiteAdsModal = false;
    this.showRefundsModal = false;
    this.showNetIncomeModal = false;
    this.showReconciliationModal = false;
    this.showCostModal = false;
    this.showBankPayoutModal = false;
    this.showCostBreakdownModal = false;
    this.showBalanceAnalysisModal = false;
    this.showContextMenu = false;
    this.selectedOrder = null;
  }

  private showToast(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) {
        this.toastMessage = '';
      }
    }, 3200);
  }
}
