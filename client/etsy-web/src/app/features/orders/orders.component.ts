import { Component, OnInit, OnDestroy, ChangeDetectorRef, DestroyRef, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { TranslocoDirective, TranslocoService, provideTranslocoScope } from '@jsverse/transloco';
import { ToastrService } from 'ngx-toastr';
import { OrdersService } from '../../core/services/orders.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import {
  OrderFulfillmentItem,
  CarrierQuote,
  PackageSpecs,
  CarrierAccountSession,
  GtipCodeItem
} from '../../core/models/orders.models';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule, TranslocoDirective],
  providers: [provideTranslocoScope('orders')],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.css'
})
export class OrdersComponent implements OnInit, OnDestroy {
  private destroyRef = inject(DestroyRef);
  private transloco = inject(TranslocoService);
  private toastr = inject(ToastrService);
  // STATE
  allOrders: OrderFulfillmentItem[] = [];
  selectedOrder: OrderFulfillmentItem | null = null;
  carrierQuotes: CarrierQuote[] = [];
  carrierSessions: CarrierAccountSession[] = [];
  
  // TABS & FILTERS
  activeTab: 'all' | 'unfulfilled' | 'shipped' | 'missing_cost' = 'all';
  searchQuery: string = '';
  sortMode: 'newest' | 'oldest' | 'name_asc' | 'name_desc' = 'newest';
  selectedCarrierFilter: string = 'Tümü';

  // CARRIER HUB TOGGLE
  isAccountsHubVisible: boolean = true;

  // GTIP SEARCH
  gtipSearchTerm: string = '';
  showGtipSuggestions: boolean = false;
  gtipResults: GtipCodeItem[] = [];

  // CUSTOMS & BREAKDOWN DISPLAY
  customsDeclaredValueUsd: number = 1.00;
  customsQuantity: number = 1;
  quoteDisplayMode: 'freight_only' | 'all_in_ddp' = 'all_in_ddp';
  isCustomsCardExpanded: boolean = true;
  showAllQuoteDetails: boolean = false;
  expandedQuoteKeys: Record<string, boolean> = {};
  private customsDebounceTimer: any = null;

  // PACKAGE PRESETS (FAZ 4)
  readonly packagePresets = [
    { id: 'envelope', labelKey: 'orders.package.presets.envelope', widthCm: 15, lengthCm: 22, heightCm: 4, weightKg: 0.15 },
    { id: 'small', labelKey: 'orders.package.presets.small', widthCm: 15, lengthCm: 15, heightCm: 10, weightKg: 0.25 },
    { id: 'medium', labelKey: 'orders.package.presets.medium', widthCm: 20, lengthCm: 20, heightCm: 15, weightKg: 0.50 },
    { id: 'large', labelKey: 'orders.package.presets.large', widthCm: 25, lengthCm: 30, heightCm: 20, weightKg: 1.20 }
  ];

  // EDITABLE COSTS
  editableProductCost: number = 0;
  editableShippingCost: number = 0;
  liveNetProfit: number = 0;
  liveProfitMargin: number = 0;

  // MODALS STATE
  isSessionModalOpen: boolean = false;
  activeModalCarrier: CarrierAccountSession | null = null;
  modalTokenInput: string = '';
  seApiKey: string = '';
  seApiSecret: string = '';
  arasEmail: string = '';
  arasPassword: string = '';
  navlungoEmail: string = '';
  navlungoPassword: string = '';
  isCapturingBrowserToken: boolean = false;
  browserCaptureStatus: string | null = null;
  isConnectingCarrier: boolean = false;

  // ADDRESS EDIT MODAL
  isAddressModalOpen: boolean = false;
  editAddressModel: {
    buyerName: string;
    buyerEmail: string;
    phone: string;
    streetAddress: string;
    secondAddress: string;
    city: string;
    state: string;
    postalCode: string;
    country: string;
    countryCode: string;
    iossNumber: string;
  } = {
    buyerName: '',
    buyerEmail: '',
    phone: '',
    streetAddress: '',
    secondAddress: '',
    city: '',
    state: '',
    postalCode: '',
    country: 'Amerika Birleşik Devletleri',
    countryCode: 'US',
    iossNumber: ''
  };

  // AI ADDRESS PARSER & CSV UPLOAD STATE
  rawAddressInput: string = '';
  isAiParsing: boolean = false;
  aiParseSuccessMessage: string | null = null;
  aiParseErrorMessage: string | null = null;
  isUploadingCsv: boolean = false;
  csvUploadSuccessMessage: string | null = null;
  csvUploadErrorMessage: string | null = null;

  isShiptomoreModalOpen: boolean = false;
  stmClientId: string = '';
  stmClientSecret: string = '';
  stmStatusText: string = '';
  stmStatusKind: 'ok' | 'danger' | 'muted' | 'info' = 'muted';
  isTestingShiptomore: boolean = false;
  isSecretMasked: boolean = true;

  isArasTemplateModalOpen: boolean = false;
  isLabelPreviewOpen: boolean = false;
  isGeneratingShipment: boolean = false;
  isBarcodeExpanded: boolean = false;
  notifyEtsyOnShipment: boolean = false;

  // TOASTS
  saveSuccessMessage: string | null = null;
  private autoSaveTimeout: any = null;
  private subs: Subscription[] = [];

  constructor(
    public ordersService: OrdersService,
    private etsyApi: EtsyApiService,
    private route: ActivatedRoute,
    private cdr: ChangeDetectorRef
  ) {}

  ngOnInit(): void {
    // 1. Subscribe to orders stream
    const ordersSub = this.ordersService.orders$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(orders => {
      this.allOrders = orders;
      if (!this.selectedOrder && orders.length > 0) {
        this.onSelectOrder(orders[0]);
      }
    });
    this.subs.push(ordersSub);

    // 2. Subscribe to selected order
    const selectedSub = this.ordersService.selectedOrder$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(order => {
      if (order) {
        const orderChanged = !this.selectedOrder || this.selectedOrder.orderId !== order.orderId;
        this.selectedOrder = order;
        this.editableProductCost = order.productCost;
        this.editableShippingCost = order.shippingCost;
        if (!order.gtipCode) {
          const suggested = this.ordersService.suggestGtipForOrder(order);
          order.gtipCode = suggested.code;
          order.gtipDescription = suggested.description;
        }
        this.gtipSearchTerm = order.gtipCode ? `${order.gtipCode} - ${order.gtipDescription || ''}` : '';
        this.calculateLiveProfit();
        if (orderChanged || this.carrierQuotes.length === 0) {
          if (orderChanged) {
            this.customsDeclaredValueUsd = 1.00;
            this.customsQuantity = 1;
            this.expandedQuoteKeys = {};
          }
          this.loadQuotesForSelected();
        }
      }
    });
    this.subs.push(selectedSub);

    // 3. Subscribe to carrier sessions
    const sessionsSub = this.ordersService.sessions$.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(sessions => {
      this.carrierSessions = sessions;
    });
    this.subs.push(sessionsSub);

    // 4. Listen to queryParams from Shipping Hub
    const routeSub = this.route.queryParams.pipe(takeUntilDestroyed(this.destroyRef)).subscribe(params => {
      if (params['carrierName'] && params['carrierCost']) {
        const cName = params['carrierName'];
        const cCost = parseFloat(params['carrierCost']);
        const cKey = params['carrierKey'] || '';
        this.editableShippingCost = cCost;
        if (this.selectedOrder) {
          this.selectedOrder.shippingCost = cCost;
          if (cKey) this.selectedOrder.selectedCarrier = cKey;
          this.selectedOrder.carrierServiceName = cName;
          this.selectedOrder.isCostMissing = this.editableProductCost <= 0 || cCost <= 0;
          this.calculateLiveProfit();
        }
        this.saveSuccessMessage = this.transloco.translate('orders.messages.shippingHubApplied', {
          name: cName,
          cost: cCost.toFixed(2)
        });
        setTimeout(() => {
          this.saveSuccessMessage = null;
        }, 5000);
      }
    });
    this.subs.push(routeSub);
  }

  ngOnDestroy(): void {
    this.subs.forEach(s => s.unsubscribe());
    if (this.autoSaveTimeout) clearTimeout(this.autoSaveTimeout);
    if (this.customsDebounceTimer) clearTimeout(this.customsDebounceTimer);
  }

  // --- GETTERS & METRICS ---
  get connectedSessionsCount(): number {
    return this.carrierSessions.filter(s => s.isConnected).length;
  }

  get missingCostCount(): number {
    return this.allOrders.filter(o => o.isCostMissing).length;
  }

  get unfulfilledCount(): number {
    return this.allOrders.filter(o => o.status === 'unfulfilled').length;
  }

  get shippedCount(): number {
    return this.allOrders.filter(o => o.status === 'shipped').length;
  }

  get selectedCarrierQuote(): CarrierQuote | null {
    if (!this.selectedOrder || !this.selectedOrder.selectedCarrier) {
      return this.carrierQuotes.length > 0 ? this.carrierQuotes[0] : null;
    }
    return this.carrierQuotes.find(q =>
      q.carrierKey === this.selectedOrder?.selectedCarrier &&
      (!this.selectedOrder?.carrierServiceName || q.serviceType === this.selectedOrder.carrierServiceName)
    ) || this.carrierQuotes.find(q => q.carrierKey === this.selectedOrder?.selectedCarrier) || this.carrierQuotes[0] || null;
  }

  get preFlightChecks(): { label: string; ok: boolean; hint: string }[] {
    if (!this.selectedOrder) return [];
    const hasAddress = !this.selectedOrder.isAddressMissing;
    const hasPhone = Boolean(this.selectedOrder.phone && this.selectedOrder.phone.trim().length >= 7);
    const gtipDigits = (this.selectedOrder.gtipCode || '').replace(/\D/g, '');
    const hasGtip = gtipDigits.length >= 6;
    const isEu = this.isEuCountry(this.selectedOrder.countryCode);
    const hasIoss = !isEu || Boolean(this.selectedOrder.iossNumber && this.selectedOrder.iossNumber.trim().length >= 8);

    const checks = [
      {
        label: this.transloco.translate('orders.preflight.addressLabel'),
        ok: hasAddress,
        hint: hasAddress
          ? this.transloco.translate('orders.preflight.addressOk')
          : this.transloco.translate('orders.preflight.addressMissing')
      },
      {
        label: this.transloco.translate('orders.preflight.phoneLabel'),
        ok: hasPhone,
        hint: hasPhone
          ? this.selectedOrder.phone!
          : this.transloco.translate('orders.preflight.phoneMissing')
      },
      {
        label: this.transloco.translate('orders.preflight.gtipLabel'),
        ok: hasGtip,
        hint: hasGtip
          ? `${this.selectedOrder.gtipCode}`
          : this.transloco.translate('orders.preflight.gtipMissing')
      }
    ];

    if (isEu) {
      checks.push({
        label: this.transloco.translate('orders.preflight.iossLabel'),
        ok: hasIoss,
        hint: hasIoss
          ? this.selectedOrder.iossNumber!
          : this.transloco.translate('orders.preflight.iossMissing')
      });
    }

    return checks;
  }

  // --- FILTERED ORDERS & SORT ---
  get filteredOrders(): OrderFulfillmentItem[] {
    let list = [...this.allOrders];

    // Status chip filter
    if (this.activeTab === 'unfulfilled') {
      list = list.filter(o => o.status === 'unfulfilled');
    } else if (this.activeTab === 'shipped') {
      list = list.filter(o => o.status === 'shipped');
    } else if (this.activeTab === 'missing_cost') {
      list = list.filter(o => o.isCostMissing);
    }

    // Search query
    if (this.searchQuery.trim()) {
      const q = this.searchQuery.toLowerCase().trim();
      list = list.filter(o => 
        o.orderNumber.toLowerCase().includes(q) ||
        o.buyerName.toLowerCase().includes(q) ||
        (o.countryCode && o.countryCode.toLowerCase().includes(q)) ||
        o.items.some(i => i.title.toLowerCase().includes(q) || (i.sku && i.sku.toLowerCase().includes(q)))
      );
    }

    // Sort mode
    if (this.sortMode === 'newest') {
      list.sort((a, b) => new Date(b.orderDate).getTime() - new Date(a.orderDate).getTime());
    } else if (this.sortMode === 'oldest') {
      list.sort((a, b) => new Date(a.orderDate).getTime() - new Date(b.orderDate).getTime());
    } else if (this.sortMode === 'name_asc') {
      list.sort((a, b) => a.buyerName.localeCompare(b.buyerName));
    } else if (this.sortMode === 'name_desc') {
      list.sort((a, b) => b.buyerName.localeCompare(a.buyerName));
    }

    return list;
  }

  // --- FILTERED QUOTES & METRICS ---
  get liveQuotesCount(): number {
    return this.carrierQuotes.filter(q => q.isLive).length;
  }

  get estimatedQuotesCount(): number {
    return this.carrierQuotes.filter(q => !q.isLive).length;
  }

  getQuoteCountForTab(tab: string): number {
    if (tab === 'Tümü') return this.carrierQuotes.length;
    const filter = tab.toLowerCase();
    return this.carrierQuotes.filter(q => 
      q.carrierName.toLowerCase().includes(filter) ||
      q.carrierKey.toLowerCase().includes(filter)
    ).length;
  }

  get carrierWarningMessage(): string | null {
    const isArasLive = this.ordersService.isCarrierSessionLive('aras');
    if (!isArasLive) {
      const aras = this.carrierSessions.find(s => s.id === 'aras');
      const val = this.ordersService.validateJwt(aras?.tokenOrKey || '');
      const detail = val.reason ? ` (${val.reason})` : ' (HTTP 401 Unauthorized)';
      return this.transloco.translate('orders.messages.arasTokenExpiredWarning', { detail });
    }
    return null;
  }

  get isCurrentModalTokenValid(): boolean {
    if (!this.activeModalCarrier) return false;
    if (this.activeModalCarrier.id === 'aras') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      return val.isValid && !val.expired;
    }
    return !!(this.modalTokenInput && !this.modalTokenInput.includes('...') && this.modalTokenInput.length > 10);
  }

  get modalTokenStatusText(): string {
    if (!this.activeModalCarrier) return '';
    if (this.activeModalCarrier.id === 'aras') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      if (val.isValid && !val.expired) {
        const info = val.timeLeftStr
          ? this.transloco.translate('orders.messages.tokenRemainingPrefix', { time: val.timeLeftStr })
          : this.transloco.translate('orders.messages.tokenValid');
        return this.transloco.translate('orders.messages.tokenLiveActive', { info });
      }
      return `⚠️ ${val.reason || this.transloco.translate('orders.messages.tokenExpiredFallback')}`;
    }
    return this.isCurrentModalTokenValid
      ? this.transloco.translate('orders.messages.tokenActiveSimple')
      : this.transloco.translate('orders.messages.tokenLoginRequired');
  }

  get filteredQuotes(): CarrierQuote[] {
    if (this.selectedCarrierFilter === 'Tümü') return this.carrierQuotes;
    const filter = this.selectedCarrierFilter.toLowerCase();
    return this.carrierQuotes.filter(q => 
      q.carrierName.toLowerCase().includes(filter) ||
      q.carrierKey.toLowerCase().includes(filter)
    );
  }

  // --- ACTIONS ---
  onSelectOrder(order: OrderFulfillmentItem): void {
    this.ordersService.selectOrder(order);
  }

  refreshOrders(): void {
    this.ordersService.loadOrders();
    this.saveSuccessMessage = null;
  }

  toggleAccountsHub(): void {
    this.isAccountsHubVisible = !this.isAccountsHubVisible;
  }

  applyPackagePreset(preset: { widthCm: number; lengthCm: number; heightCm: number; weightKg: number }): void {
    if (!this.selectedOrder) return;
    this.selectedOrder.packageSpecs.widthCm = preset.widthCm;
    this.selectedOrder.packageSpecs.lengthCm = preset.lengthCm;
    this.selectedOrder.packageSpecs.heightCm = preset.heightCm;
    this.selectedOrder.packageSpecs.weightKg = preset.weightKg;
    this.onDimensionChange();
  }

  onDimensionChange(): void {
    if (!this.selectedOrder) return;
    this.ordersService.updatePackageSpecs(this.selectedOrder.orderId, this.selectedOrder.packageSpecs);
    this.loadQuotesForSelected();
  }

  getActiveHsCode(): string {
    const raw = (this.selectedOrder?.gtipCode || this.gtipSearchTerm || '').split('-')[0].trim();
    const digits = raw.replace(/\D/g, '');
    return digits.length >= 6 ? digits : '3926909990';
  }

  getQuoteKey(quote: CarrierQuote): string {
    return `${quote.carrierKey}::${(quote.serviceType || quote.serviceCode || '').trim().toLowerCase()}`;
  }

  toggleCustomsCard(event?: Event): void {
    event?.stopPropagation();
    this.isCustomsCardExpanded = !this.isCustomsCardExpanded;
  }

  onQuoteCardClick(quote: CarrierQuote, event?: Event): void {
    this.applyCarrierQuote(quote);
    this.toggleQuoteDetails(quote, event);
  }

  isQuoteExpanded(quote: CarrierQuote): boolean {
    const key = this.getQuoteKey(quote);
    if (key in this.expandedQuoteKeys) {
      return this.expandedQuoteKeys[key];
    }
    return this.showAllQuoteDetails;
  }

  toggleQuoteDetails(quote: CarrierQuote, event?: Event): void {
    event?.stopPropagation();
    const key = this.getQuoteKey(quote);
    this.expandedQuoteKeys[key] = !this.isQuoteExpanded(quote);
  }

  toggleAllQuoteDetails(): void {
    this.showAllQuoteDetails = !this.showAllQuoteDetails;
    this.expandedQuoteKeys = {};
  }

  get isDdpEnabled(): boolean {
    return this.quoteDisplayMode === 'all_in_ddp';
  }

  toggleDdpMode(enabled?: boolean, event?: Event): void {
    event?.stopPropagation();
    const next = enabled !== undefined ? enabled : !this.isDdpEnabled;
    this.setQuoteDisplayMode(next ? 'all_in_ddp' : 'freight_only');
  }

  setQuoteDisplayMode(mode: 'freight_only' | 'all_in_ddp'): void {
    this.quoteDisplayMode = mode;
    this.syncSelectedQuoteCost();
  }

  hasCustomsBreakdown(quote: CarrierQuote): boolean {
    return Boolean((quote.hasCustomsBreakdown || quote.hasDetailedBreakdown) && quote.fastCustomsPaymentUsd !== undefined);
  }

  getDisplayPriceUsd(quote: CarrierQuote): number {
    return this.quoteDisplayMode === 'all_in_ddp' && this.hasCustomsBreakdown(quote)
      ? (quote.grandTotalUsd ?? quote.priceUsd)
      : quote.priceUsd;
  }

  getDisplayPriceTry(quote: CarrierQuote): number {
    return this.quoteDisplayMode === 'all_in_ddp' && this.hasCustomsBreakdown(quote)
      ? (quote.grandTotalTry ?? quote.priceTry)
      : quote.priceTry;
  }

  get selectedTaxRateSummary(): string {
    const selected = this.selectedCarrierQuote;
    const ref = (selected && this.hasCustomsBreakdown(selected) ? selected : undefined)
      || this.carrierQuotes.find(q => this.hasCustomsBreakdown(q))
      || this.carrierQuotes[0];
    if (!ref) return this.transloco.translate('orders.gtip.taxDefaultSummary');
    const gen = ref.generalDutyRatePercent ?? 0;
    const add = ref.additionalDutyRatePercent ?? 0;
    const total = Number((gen + add).toFixed(2));
    const hts = ref.additionalDutyHtsCode ? ` ${ref.additionalDutyHtsCode}` : '';
    if (gen === 0 && add === 0) {
      return this.transloco.translate('orders.gtip.taxZeroSummary');
    }
    return add > 0
      ? this.transloco.translate('orders.gtip.taxCombinedSummary', { gen, hts, add, total })
      : this.transloco.translate('orders.gtip.taxGeneralOnlySummary', { gen });
  }

  setCustomsDeclaredValue(val: number): void {
    const parsed = Number(val);
    this.customsDeclaredValueUsd = parsed > 0 ? Number(parsed.toFixed(2)) : 1.00;
    this.onCustomsParamChange();
  }

  private syncSelectedQuoteCost(): void {
    if (!this.selectedOrder?.selectedCarrier) return;
    const matched = this.selectedCarrierQuote;
    if (matched) {
      this.editableShippingCost = this.getDisplayPriceUsd(matched);
      this.calculateLiveProfit();
    }
  }

  onCustomsParamChange(): void {
    if (!this.selectedOrder) return;
    this.customsDeclaredValueUsd = Math.max(0.01, Number(this.customsDeclaredValueUsd) || 1.00);
    this.customsQuantity = Math.max(1, Math.round(Number(this.customsQuantity) || 1));

    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 36.25;
    const customsOpts = {
      hsCode: this.getActiveHsCode(),
      declaredValueUsd: this.customsDeclaredValueUsd,
      quantity: this.customsQuantity
    };

    this.carrierQuotes = this.ordersService.calculateCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate,
      customsOpts
    );
    this.syncSelectedQuoteCost();

    if (this.customsDebounceTimer) {
      clearTimeout(this.customsDebounceTimer);
    }
    this.customsDebounceTimer = setTimeout(() => {
      if (!this.selectedOrder) return;
      this.ordersService.fetchLiveCarrierQuotes(
        this.selectedOrder.packageSpecs,
        this.selectedOrder.countryCode,
        rate,
        this.selectedOrder.postalCode,
        this.selectedOrder.city,
        this.selectedOrder.state,
        customsOpts
      ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
        if (res && res.quotes && res.quotes.length > 0) {
          this.carrierQuotes = res.quotes;
          this.syncSelectedQuoteCost();
        }
      });
    }, 350);
  }

  loadQuotesForSelected(): void {
    if (!this.selectedOrder) return;
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 36.25;
    const customsOpts = {
      hsCode: this.getActiveHsCode(),
      declaredValueUsd: this.customsDeclaredValueUsd > 0 ? this.customsDeclaredValueUsd : 1.00,
      quantity: this.customsQuantity > 0 ? this.customsQuantity : 1
    };

    this.carrierQuotes = this.ordersService.calculateCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate,
      customsOpts
    );

    this.ordersService.fetchLiveCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate,
      this.selectedOrder.postalCode,
      this.selectedOrder.city,
      this.selectedOrder.state,
      customsOpts
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(res => {
      if (res && res.quotes && res.quotes.length > 0) {
        this.carrierQuotes = res.quotes;
        this.syncSelectedQuoteCost();
      }
    });
  }

  applyCarrierQuote(quote: CarrierQuote, includeCustoms?: boolean): void {
    if (!this.selectedOrder) return;
    const requestedAllIn = includeCustoms ?? (this.quoteDisplayMode === 'all_in_ddp');
    const canApplyAllIn = requestedAllIn && this.hasCustomsBreakdown(quote);
    if (includeCustoms !== undefined) {
      this.quoteDisplayMode = canApplyAllIn ? 'all_in_ddp' : 'freight_only';
    }
    this.selectedOrder.selectedCarrier = quote.carrierKey;
    this.selectedOrder.carrierServiceName = quote.serviceType || quote.carrierName;
    this.editableShippingCost = canApplyAllIn ? (quote.grandTotalUsd ?? quote.priceUsd) : quote.priceUsd;
    this.calculateLiveProfit();
  }

  // --- GTIP ENGINE ---
  onSearchGtip(): void {
    const instant = this.ordersService.searchGtip(this.gtipSearchTerm);
    this.gtipResults = instant;
    this.showGtipSuggestions = true;

    const enteredDigits = (this.gtipSearchTerm || '').split('-')[0].replace(/\D/g, '');
    const currentDigits = (this.selectedOrder?.gtipCode || '').replace(/\D/g, '');
    if (this.selectedOrder && enteredDigits.length >= 6 && enteredDigits !== currentDigits) {
      const dash = this.gtipSearchTerm.indexOf('-');
      const desc = dash > 0 ? this.gtipSearchTerm.substring(dash + 1).trim() : (this.selectedOrder.gtipDescription || '');
      this.ordersService.updateOrderGtip(this.selectedOrder.orderId, enteredDigits, desc);
      this.onCustomsParamChange();
    }

    this.ordersService.searchGtipRemote(this.gtipSearchTerm).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(results => {
      this.gtipResults = results;
      this.showGtipSuggestions = results.length > 0;
    });
  }

  autoMatchGtip(): void {
    if (!this.selectedOrder) return;
    const best = this.ordersService.suggestGtipForOrder(this.selectedOrder);
    this.selectGtip(best);
    this.saveSuccessMessage = this.transloco.translate('orders.messages.gtipAutoMatched', {
      code: best.code,
      category: best.category
    });
    setTimeout(() => this.saveSuccessMessage = null, 4000);
  }

  selectGtip(item: GtipCodeItem): void {
    this.gtipSearchTerm = `${item.code} - ${item.description}`;
    this.showGtipSuggestions = false;
    if (this.selectedOrder) {
      this.ordersService.updateOrderGtip(this.selectedOrder.orderId, item.code, item.description);
      this.onCustomsParamChange();
    }
  }

  // --- FINANCIAL PROFIT CALCULATOR ---
  calculateLiveProfit(): void {
    if (!this.selectedOrder) return;
    const totalAmount = this.selectedOrder.totalAmount;
    const etsyFee = totalAmount * 0.095;
    const totalCost = (Number(this.editableProductCost) || 0) + (Number(this.editableShippingCost) || 0) + etsyFee;
    
    this.liveNetProfit = Number((totalAmount - totalCost).toFixed(2));
    this.liveProfitMargin = totalAmount > 0 
      ? Number(((this.liveNetProfit / totalAmount) * 100).toFixed(1))
      : 0;
  }

  saveCosts(): void {
    if (!this.selectedOrder) return;

    // Save GTIP if present before persisting to SQLite
    const dash = this.gtipSearchTerm.indexOf('-');
    const code = dash > 0 ? this.gtipSearchTerm.substring(0, dash).trim() : this.gtipSearchTerm.trim();
    const desc = dash > 0 ? this.gtipSearchTerm.substring(dash + 1).trim() : '';
    if (code) {
      this.ordersService.updateOrderGtip(this.selectedOrder.orderId, code, desc);
    }

    this.ordersService.updateOrderCosts(
      this.selectedOrder.orderId, 
      Number(this.editableProductCost) || 0, 
      Number(this.editableShippingCost) || 0
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe(saved => {
      this.saveSuccessMessage = saved
        ? this.transloco.translate('orders.messages.costsSavedDb')
        : this.transloco.translate('orders.messages.costsSavedSessionOnly');
      setTimeout(() => this.saveSuccessMessage = null, 5000);
    });
  }

  // --- SHIPMENT CREATION ---
  executeCreateShipment(): void {
    if (!this.selectedOrder) return;
    if (this.selectedOrder.isAddressMissing) {
      this.toastr.warning(this.transloco.translate('orders.messages.alertAddressRequired'));
      return;
    }

    const quote = this.selectedCarrierQuote;
    const carrierKey = quote?.carrierKey || 'shipentegra';
    const serviceCode = quote?.serviceCode || quote?.quoteId || 'STANDART';
    const serviceType = quote?.serviceType || 'Express';
    const subCarrier = quote?.subCarrier || 'FedEx';

    if (carrierKey === 'shiptomore' && this.notifyEtsyOnShipment && !this.selectedOrder.phone?.trim()) {
      this.toastr.warning(this.transloco.translate('orders.messages.alertStmPhoneRequired'));
      this.openAddressModal();
      return;
    }

    const destCountry = (this.selectedOrder.countryCode || 'US').toUpperCase();
    if (carrierKey === 'shiptomore' && (destCountry === 'US' || destCountry === 'CA' || destCountry === 'IE') && !this.selectedOrder.state?.trim()) {
      this.toastr.warning(this.transloco.translate('orders.messages.alertStmStateRequired'));
      this.openAddressModal();
      return;
    }

    const rId = Number(this.selectedOrder.orderId);
    if (!rId || isNaN(rId)) {
      this.toastr.warning(this.transloco.translate('orders.messages.alertInvalidReceiptId'));
      return;
    }

    this.isGeneratingShipment = true;
    this.ordersService.executeCreateShipment({
      receiptId: rId,
      carrierKey: carrierKey,
      serviceCode: serviceCode,
      serviceType: serviceType,
      subCarrier: subCarrier,
      weightKg: this.selectedOrder.packageSpecs.weightKg,
      widthCm: this.selectedOrder.packageSpecs.widthCm,
      lengthCm: this.selectedOrder.packageSpecs.lengthCm,
      heightCm: this.selectedOrder.packageSpecs.heightCm,
      hsCode: this.selectedOrder.gtipCode,
      cargoPrice: quote?.priceUsd || this.selectedOrder.shippingCost,
      notifyEtsy: this.notifyEtsyOnShipment,
      isDdp: this.isDdpEnabled,
      recipientAddress: {
        buyerName: this.selectedOrder.buyerName,
        name: this.selectedOrder.buyerName,
        streetAddress: this.selectedOrder.streetAddress || '',
        first_line: this.selectedOrder.streetAddress || '',
        secondAddress: this.selectedOrder.secondAddress || '',
        second_line: this.selectedOrder.secondAddress || '',
        city: this.selectedOrder.city || '',
        state: this.selectedOrder.state || '',
        hasState: !!(this.selectedOrder.state && this.selectedOrder.state.trim()),
        postalCode: this.selectedOrder.postalCode || '',
        zip: this.selectedOrder.postalCode || '',
        countryCode: this.selectedOrder.countryCode || 'US',
        country_iso: this.selectedOrder.countryCode || 'US',
        phone: this.selectedOrder.phone || '',
        buyerEmail: this.selectedOrder.buyerEmail || '',
        email: this.selectedOrder.buyerEmail || ''
      }
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isGeneratingShipment = false;
        if (res && res.success) {
          if (this.selectedOrder) {
            this.selectedOrder.status = 'shipped';
            this.selectedOrder.trackingNumber = res.trackingNumber;
            this.selectedOrder.trackingCode = res.trackingNumber;
            this.selectedOrder.barcodeNumber = res.trackingNumber;
            this.selectedOrder.carrier = res.carrierName || carrierKey;
            this.selectedOrder.selectedCarrier = carrierKey;
            this.selectedOrder.labelUrl = res.labelUrl;
            this.selectedOrder.labelBase64 = res.labelBase64;
            this.ordersService.persistOrderFulfillmentMeta(this.selectedOrder).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({ next: () => {}, error: () => {} });
          }
          const etsyNote = this.notifyEtsyOnShipment
            ? (res.etsyTrackingSubmitted
                ? this.transloco.translate('orders.messages.etsyTrackingSubmitted')
                : (res.etsyTrackingMessage || this.transloco.translate('orders.messages.etsyTrackingFailed')))
            : this.transloco.translate('orders.messages.etsyTestModeNote');
          this.saveSuccessMessage = this.transloco.translate('orders.messages.shipmentCreatedSuccess', {
            tracking: res.trackingNumber,
            note: etsyNote
          });
          setTimeout(() => this.saveSuccessMessage = null, 9000);
        } else {
          this.toastr.warning(this.transloco.translate('orders.messages.shipmentCreateFailed', {
            error: res?.message || res?.errorMessage || this.transloco.translate('orders.messages.unknownError')
          }));
        }
      },
      error: (err: any) => {
        this.isGeneratingShipment = false;
        // HTTP 400/422/5xx hataları global interceptor tarafından toast ile gösterilir; yalnızca ağ/durumsuz hatalarda yedek toast bas.
        if (!err?.status) {
          const errMsg = err?.error?.errorMessage || err?.error?.message || err?.message || this.transloco.translate('orders.messages.apiError');
          this.toastr.error(this.transloco.translate('orders.messages.shipmentCreateError', { error: errMsg }));
        }
      }
    });
  }

  formatOrderNumber(num?: string): string {
    if (!num) return '';
    return num.startsWith('#') ? num : `#${num}`;
  }

  // --- ADDRESS MODAL ACTIONS ---
  sanitizeBuyerName(name?: string): string {
    if (!name) return '';
    const trimmed = name.trim();
    if (/^Etsy\s*Sipari[sş]i\s*#|^#\d+/i.test(trimmed) || /^Etsy\s*Al[ıi]c[ıi]s[ıi]/i.test(trimmed) || /^Etsy\s*M[uü][sş]terisi/i.test(trimmed)) {
      return '';
    }
    return trimmed;
  }

  async pasteFromClipboard(): Promise<void> {
    try {
      if (!navigator.clipboard || !navigator.clipboard.readText) {
        this.toastr.warning(this.transloco.translate('orders.messages.clipboardUnsupported'));
        return;
      }
      const text = await navigator.clipboard.readText();
      if (text && text.trim()) {
        this.rawAddressInput = text.trim();
        this.aiParseSuccessMessage = this.transloco.translate('orders.messages.clipboardPastedParsing');
        this.parseAddressWithAi();
      } else {
        this.aiParseErrorMessage = this.transloco.translate('orders.messages.clipboardEmpty');
      }
    } catch (e: any) {
      this.toastr.warning(this.transloco.translate('orders.messages.clipboardPermissionDenied', {
        error: e?.message || this.transloco.translate('orders.messages.clipboardManualFallback')
      }));
    }
  }

  fillRawAddressFromSelectedOrder(): void {
    const order = this.selectedOrder;
    if (!order) return;
    const cleanBuyer = this.sanitizeBuyerName(order.buyerName);
    const hasAddressDetails = Boolean(order.streetAddress || order.city || order.postalCode || order.phone);

    // Eğer sokak veya şehir bilgisi yoksa sahte veya yalnız alıcı adını yapay zeka kutusuna tek başına basma
    if (!hasAddressDetails && !order.rawAddressText) {
      this.rawAddressInput = '';
    } else {
      const addrParts = [
        cleanBuyer,
        order.streetAddress,
        order.secondAddress,
        [order.city, order.state, order.postalCode].filter(Boolean).join(', '),
        order.country || order.countryCode,
        order.phone ? `Tel: ${order.phone}` : ''
      ].filter(p => Boolean(p && String(p).trim().length > 0));

      this.rawAddressInput = order.rawAddressText?.trim() 
        || (addrParts.length > 0 ? addrParts.join('\n') : '')
        || order.formattedAddress 
        || order.addressSnippet 
        || '';
    }

    const rId = Number(order.orderId);
    if (rId && !isNaN(rId)) {
      this.ordersService.getOrderAddress(rId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res: any) => {
          if (res && res.success && res.data) {
            const db = res.data;
            if (db.streetAddress || db.city || db.phone) {
              const cleanDbBuyer = this.sanitizeBuyerName(db.buyerName) || cleanBuyer;

              // Form modelini doğrudan doldur
              if (this.editAddressModel) {
                if (cleanDbBuyer) this.editAddressModel.buyerName = cleanDbBuyer;
                if (db.streetAddress) this.editAddressModel.streetAddress = db.streetAddress;
                if (db.secondAddress) this.editAddressModel.secondAddress = db.secondAddress;
                if (db.city) this.editAddressModel.city = db.city;
                if (db.state) this.editAddressModel.state = db.state;
                if (db.postalCode) this.editAddressModel.postalCode = db.postalCode;
                if (db.countryName) this.editAddressModel.country = db.countryName;
                if (db.countryCode) this.editAddressModel.countryCode = db.countryCode;
                if (db.phone) this.editAddressModel.phone = db.phone;
                if (db.iossNumber) this.editAddressModel.iossNumber = db.iossNumber;
              }

              // Seçili sipariş nesnesi ve liste senkronizasyonu
              order.streetAddress = db.streetAddress || order.streetAddress;
              order.secondAddress = db.secondAddress || order.secondAddress;
              order.city = db.city || order.city;
              order.state = db.state || order.state;
              order.postalCode = db.postalCode || order.postalCode;
              order.country = db.countryName || order.country;
              order.countryCode = db.countryCode || order.countryCode;
              order.phone = db.phone || order.phone;
              order.iossNumber = db.iossNumber || order.iossNumber;
              order.hasAddress = Boolean(order.streetAddress && order.city);
              order.isAddressMissing = !order.hasAddress;

              const dbLines = [
                cleanDbBuyer,
                db.streetAddress,
                db.secondAddress,
                [db.city, db.state, db.postalCode].filter(Boolean).join(', '),
                db.countryName || order.country,
                db.phone ? `Tel: ${db.phone}` : ''
              ].filter(p => Boolean(p && String(p).trim().length > 0));

              this.rawAddressInput = dbLines.join('\n');
              this.aiParseSuccessMessage = this.transloco.translate('orders.messages.dbAddressLoaded');
              this.cdr.detectChanges();
            }
          }
        },
        error: () => {}
      });
    }
  }

  clearRawAddressInput(): void {
    this.rawAddressInput = '';
    this.aiParseSuccessMessage = null;
    this.aiParseErrorMessage = null;
  }

  openAddressModal(): void {
    const order = this.selectedOrder;
    if (!order) return;
    this.fillRawAddressFromSelectedOrder();
    this.aiParseSuccessMessage = null;
    this.aiParseErrorMessage = null;

    const cleanBuyer = this.sanitizeBuyerName(order.buyerName);
    this.editAddressModel = {
      buyerName: cleanBuyer,
      buyerEmail: order.buyerEmail || '',
      phone: order.phone || '',
      streetAddress: order.streetAddress || '',
      secondAddress: order.secondAddress || '',
      city: order.city || '',
      state: order.state || '',
      postalCode: order.postalCode || '',
      country: order.country || 'Amerika Birleşik Devletleri',
      countryCode: order.countryCode || 'US',
      iossNumber: order.iossNumber || ''
    };
    // Eğer sokak adresi boşsa ama rawAddressInput içinde satırlar varsa formu anında otomatik ayrıştır
    if (!this.editAddressModel.streetAddress && this.rawAddressInput) {
      const lines = this.rawAddressInput.split('\n').map(l => l.trim()).filter(Boolean);
      if (lines.length >= 2) {
        if (!this.editAddressModel.buyerName) this.editAddressModel.buyerName = this.sanitizeBuyerName(lines[0]);
        const remaining = lines.slice(1);

        const pIdx = remaining.findIndex(l => /(?:\+?\d{1,3}[\s-]?)?\(?\d{3}\)?[\s.-]?\d{3}[\s.-]?\d{4}|Tel:/i.test(l));
        if (pIdx !== -1) {
          this.editAddressModel.phone = remaining[pIdx].replace(/^Tel:\s*/i, '');
          remaining.splice(pIdx, 1);
        }

        const cIdx = remaining.findIndex(l => /^(?:United States|USA|Canada|United Kingdom|Turkey|Türkiye)$/i.test(l));
        if (cIdx !== -1) {
          this.editAddressModel.country = remaining[cIdx];
          remaining.splice(cIdx, 1);
        }

        const csIdx = remaining.findIndex(l => /[A-Za-z\s.-]+,\s*[A-Z]{2}/.test(l));
        if (csIdx !== -1) {
          const m = remaining[csIdx].match(/([A-Za-z\s.-]+),\s*([A-Z]{2})(?:\s+(\d{5}(?:-\d{4})?))?/);
          if (m) {
            this.editAddressModel.city = m[1].trim();
            this.editAddressModel.state = m[2].trim().toUpperCase();
            if (m[3]) this.editAddressModel.postalCode = m[3].trim();
          }
          remaining.splice(csIdx, 1);
        }

        if (remaining.length > 0) {
          this.editAddressModel.streetAddress = remaining[0];
          if (remaining.length > 1) {
            this.editAddressModel.secondAddress = remaining.slice(1).join(', ');
          }
        }
      }
    }

    this.isAddressModalOpen = true;

    // DB'de kayıtlı adres varsa otomatik çek ve yapay zeka kutusuna doldur
    const rId = Number(order.orderId);
    if (rId && !isNaN(rId)) {
      this.ordersService.getOrderAddress(rId).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: (res: any) => {
          if (res && res.success && res.data) {
            const db = res.data;
            if (db.streetAddress || db.city || db.phone) {
              const cleanDbBuyer = this.sanitizeBuyerName(db.buyerName);
              if (cleanDbBuyer) this.editAddressModel.buyerName = cleanDbBuyer;
              if (db.streetAddress) this.editAddressModel.streetAddress = db.streetAddress;
              if (db.secondAddress) this.editAddressModel.secondAddress = db.secondAddress;
              if (db.city) this.editAddressModel.city = db.city;
              if (db.state) this.editAddressModel.state = db.state;
              if (db.postalCode) this.editAddressModel.postalCode = db.postalCode;
              if (db.countryName) this.editAddressModel.country = db.countryName;
              if (db.countryCode) this.editAddressModel.countryCode = db.countryCode;
              if (db.phone) this.editAddressModel.phone = db.phone;
              if (db.iossNumber) this.editAddressModel.iossNumber = db.iossNumber;

              const dbLines = [
                this.editAddressModel.buyerName,
                this.editAddressModel.streetAddress,
                this.editAddressModel.secondAddress,
                [this.editAddressModel.city, this.editAddressModel.state, this.editAddressModel.postalCode].filter(Boolean).join(', '),
                this.editAddressModel.country,
                this.editAddressModel.phone ? `Tel: ${this.editAddressModel.phone}` : ''
              ].filter(p => Boolean(p && String(p).trim().length > 0));

              this.rawAddressInput = dbLines.join('\n');
              this.aiParseSuccessMessage = this.transloco.translate('orders.messages.dbAddressAutoLoaded');
              order.streetAddress = this.editAddressModel.streetAddress;
              order.city = this.editAddressModel.city;
              order.hasAddress = Boolean(order.streetAddress && order.city);
              order.isAddressMissing = !order.hasAddress;
              this.cdr.detectChanges();
            }
          }
        },
        error: () => {}
      });
    }
  }

  closeAddressModal(): void {
    this.isAddressModalOpen = false;
    this.rawAddressInput = '';
    this.aiParseSuccessMessage = null;
    this.aiParseErrorMessage = null;
  }

  parseAddressWithAi(): void {
    if (!this.rawAddressInput || !this.rawAddressInput.trim()) {
      this.aiParseErrorMessage = this.transloco.translate('orders.messages.aiPasteRequired');
      return;
    }

    this.isAiParsing = true;
    this.aiParseErrorMessage = null;
    this.aiParseSuccessMessage = null;

    this.ordersService.aiParseAddress(this.rawAddressInput).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isAiParsing = false;
        if (!res) {
          this.aiParseErrorMessage = this.transloco.translate('orders.messages.aiNoResponse');
          return;
        }

        if (res.buyerName) this.editAddressModel.buyerName = res.buyerName;
        if (res.buyerEmail) this.editAddressModel.buyerEmail = res.buyerEmail;
        this.editAddressModel.phone = res.phone || '';
        if (res.streetAddress) this.editAddressModel.streetAddress = res.streetAddress;
        if (res.secondAddress) this.editAddressModel.secondAddress = res.secondAddress;
        if (res.city) this.editAddressModel.city = res.city;
        if (res.state) this.editAddressModel.state = res.state;
        if (res.postalCode) this.editAddressModel.postalCode = res.postalCode;
        if (res.country) this.editAddressModel.country = res.country;
        if (res.countryCode) this.editAddressModel.countryCode = res.countryCode;
        if (res.iossNumber) this.editAddressModel.iossNumber = res.iossNumber;

        this.aiParseSuccessMessage = this.transloco.translate('orders.messages.aiParseSuccess', {
          source: res.source || 'AI'
        });
        setTimeout(() => this.aiParseSuccessMessage = null, 5000);
      },
      error: (err: any) => {
        this.isAiParsing = false;
        this.aiParseErrorMessage = this.transloco.translate('orders.messages.aiParseFailed', {
          error: err?.error?.message || err?.message || this.transloco.translate('orders.messages.unknownError')
        });
      }
    });
  }

  setEtsyIoss(val: string): void {
    this.editAddressModel.iossNumber = val;
  }

  isEuCountry(code: string | undefined): boolean {
    if (!code) return false;
    const euCodes = ['AT','BE','BG','CY','CZ','DE','DK','EE','ES','FI','FR','GR','HR','HU','IE','IT','LT','LU','LV','MT','NL','PL','PT','RO','SE','SI','SK'];
    return euCodes.includes(code.trim().toUpperCase());
  }

  onCsvFileSelected(event: any): void {
    const file = event?.target?.files?.[0];
    if (!file) return;

    this.isUploadingCsv = true;
    this.csvUploadSuccessMessage = null;
    this.csvUploadErrorMessage = null;

    this.ordersService.uploadOrdersCsv(file).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isUploadingCsv = false;
        this.csvUploadSuccessMessage = this.transloco.translate('orders.messages.csvUploadSuccess', {
          count: res?.count || 0
        });
        this.ordersService.loadOrders();
        setTimeout(() => this.csvUploadSuccessMessage = null, 6000);
      },
      error: (err: any) => {
        this.isUploadingCsv = false;
        this.csvUploadErrorMessage = this.transloco.translate('orders.messages.csvUploadError', {
          error: err?.error?.message || err?.message || this.transloco.translate('orders.messages.unknownError')
        });
        setTimeout(() => this.csvUploadErrorMessage = null, 6000);
      }
    });

    // Reset input
    event.target.value = '';
  }

  saveEditedAddress(): void {
    if (!this.selectedOrder) return;
    const rId = Number(this.selectedOrder.orderId);
    if (!rId || isNaN(rId)) {
      this.toastr.warning(this.transloco.translate('orders.messages.alertInvalidOrderId'));
      return;
    }
    this.ordersService.updateOrderAddress(rId, this.editAddressModel).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: () => {
        if (this.selectedOrder) {
          Object.assign(this.selectedOrder, this.editAddressModel);
          const hasAddr = Boolean(this.editAddressModel.streetAddress?.trim() && this.editAddressModel.city?.trim());
          this.selectedOrder.isAddressMissing = !hasAddr;
          if (hasAddr) {
            this.selectedOrder.addressWarning = undefined;
          }
          const parts = [
            this.editAddressModel.streetAddress,
            this.editAddressModel.secondAddress,
            this.editAddressModel.city,
            this.editAddressModel.state,
            this.editAddressModel.postalCode,
            this.editAddressModel.country
          ].filter(Boolean);
          this.selectedOrder.addressSnippet = parts.join(', ');
          this.selectedOrder.formattedAddress = parts.join(', ');
        }
        this.isAddressModalOpen = false;
        this.saveSuccessMessage = this.transloco.translate('orders.messages.addressSavedDb');
        setTimeout(() => this.saveSuccessMessage = null, 4000);
      },
      error: (err: any) => {
        if (!err?.status) {
          this.toastr.error(this.transloco.translate('orders.messages.addressSaveError', {
            error: err?.error?.message || err?.message || ''
          }));
        }
      }
    });
  }

  // --- MODALS OPEN/CLOSE ---
  openSessionModal(carrier: CarrierAccountSession): void {
    if (carrier.id === 'shiptomore') {
      this.openShiptomoreModal();
      return;
    }
    this.activeModalCarrier = carrier;
    this.browserCaptureStatus = null;
    if (carrier.id === 'shipentegra') {
      this.seApiKey = carrier.apiKey || localStorage.getItem('se_api_key') || '';
      this.seApiSecret = carrier.apiSecret || localStorage.getItem('se_api_secret') || '';
      this.modalTokenInput = carrier.tokenOrKey || '';
    } else {
      this.modalTokenInput = carrier.tokenOrKey || '';
      if (carrier.id === 'aras') {
        this.arasEmail = localStorage.getItem('aras_saved_email') || '';
        // Sunucuda aktif veya yenilenebilir token varsa modal açılırken senkronize et
        this.ordersService.syncCarrierSessionsFromServer();
      } else if (carrier.id === 'navlungo') {
        this.navlungoEmail = localStorage.getItem('navlungo_saved_email') || '';
        this.ordersService.syncCarrierSessionsFromServer();
      }
    }
    this.isSessionModalOpen = true;
  }

  getCarrierRemainingValidityText(carrier: CarrierAccountSession | null): string {
    if (!carrier) return '';
    let expiryMs: number | null = null;
    if (carrier.expiresAtUtc) {
      const parsed = Date.parse(carrier.expiresAtUtc);
      if (!isNaN(parsed)) expiryMs = parsed;
    }
    if (expiryMs === null && carrier.tokenOrKey) {
      const jwt = this.ordersService.validateJwt(carrier.tokenOrKey);
      if (jwt.expDate) expiryMs = jwt.expDate.getTime();
    }
    if (expiryMs === null) {
      return carrier.isConnected
        ? this.transloco.translate('orders.messages.validityActiveUnknown')
        : this.transloco.translate('orders.messages.validityDisconnected');
    }
    const diffMs = expiryMs - Date.now();
    if (diffMs <= 0) {
      return this.transloco.translate('orders.messages.validityExpired');
    }
    const totalMinutes = Math.floor(diffMs / 60000);
    const hours = Math.floor(totalMinutes / 60);
    const mins = totalMinutes % 60;
    return hours > 0
      ? this.transloco.translate('orders.messages.validityHoursMins', { hours, mins })
      : this.transloco.translate('orders.messages.validityMins', { mins });
  }

  triggerNavlungoBrowserCapture(openBrowser: boolean = false): void {
    const targetId = this.activeModalCarrier?.id || 'navlungo';
    if (this.navlungoEmail.trim()) {
      localStorage.setItem('navlungo_saved_email', this.navlungoEmail.trim());
    }

    this.isCapturingBrowserToken = true;
    this.browserCaptureStatus = openBrowser
      ? this.transloco.translate('orders.messages.navlungoBrowserOpened')
      : this.transloco.translate('orders.messages.navlungoSilentRefreshing');

    this.ordersService.triggerCarrierBrowserLogin(
      targetId,
      this.navlungoEmail.trim() || undefined,
      this.navlungoPassword.trim() || undefined,
      openBrowser
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isCapturingBrowserToken = false;
        if (res && res.isConnected && res.bearerToken) {
          this.modalTokenInput = res.bearerToken;
          if (this.activeModalCarrier) {
            this.activeModalCarrier.isConnected = true;
            this.activeModalCarrier.tokenOrKey = res.bearerToken;
            if (res.expiresAtUtc) this.activeModalCarrier.expiresAtUtc = res.expiresAtUtc;
          }
          const navSession = this.carrierSessions.find(s => s.id === 'navlungo');
          if (navSession) {
            navSession.isConnected = true;
            navSession.tokenOrKey = res.bearerToken;
            if (res.expiresAtUtc) navSession.expiresAtUtc = res.expiresAtUtc;
          }
          this.browserCaptureStatus = this.transloco.translate('orders.messages.navlungoRefreshedStatus');
          this.loadQuotesForSelected();
          this.calculateLiveProfit();
          this.saveSuccessMessage = this.transloco.translate('orders.messages.navlungoRefreshedToast');
          setTimeout(() => {
            this.saveSuccessMessage = null;
          }, 6000);
        } else {
          this.browserCaptureStatus = '⚠️ ' + (res?.message || this.transloco.translate('orders.messages.navlungoRefreshFallback'));
        }
      },
      error: (err: any) => {
        this.isCapturingBrowserToken = false;
        this.browserCaptureStatus = this.transloco.translate('orders.messages.navlungoRefreshError', {
          error: err?.error?.message || err?.message || this.transloco.translate('orders.messages.serverUnreachable')
        });
      }
    });
  }

  triggerArasBrowserCapture(openBrowser: boolean = true): void {
    if (!this.activeModalCarrier) return;
    if (this.arasEmail.trim()) {
      localStorage.setItem('aras_saved_email', this.arasEmail.trim());
    }

    this.isCapturingBrowserToken = true;
    this.browserCaptureStatus = openBrowser
      ? this.transloco.translate('orders.messages.arasBrowserOpened')
      : this.transloco.translate('orders.messages.arasSilentRefreshing');

    this.ordersService.triggerCarrierBrowserLogin(
      this.activeModalCarrier.id,
      this.arasEmail.trim() || undefined,
      this.arasPassword.trim() || undefined,
      openBrowser
    ).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res: any) => {
        this.isCapturingBrowserToken = false;
        if (res && res.bearerToken) {
          this.modalTokenInput = res.bearerToken;
          if (this.activeModalCarrier) {
            this.activeModalCarrier.isConnected = true;
            this.activeModalCarrier.tokenOrKey = res.bearerToken;
          }
          this.browserCaptureStatus = this.transloco.translate('orders.messages.arasCapturedStatus');
          this.loadQuotesForSelected();
          this.calculateLiveProfit();
          this.saveSuccessMessage = this.transloco.translate('orders.messages.arasCapturedToast');
          setTimeout(() => {
            this.saveSuccessMessage = null;
          }, 6000);
        } else {
          this.browserCaptureStatus = '⚠️ ' + (res?.message || this.transloco.translate('orders.messages.arasCaptureFallback'));
        }
      },
      error: (err: any) => {
        this.isCapturingBrowserToken = false;
        this.browserCaptureStatus = this.transloco.translate('orders.messages.captureError', {
          error: err?.error?.message || err?.message || this.transloco.translate('orders.messages.serverUnreachable')
        });
      }
    });
  }

  closeSessionModal(): void {
    this.isSessionModalOpen = false;
    this.activeModalCarrier = null;
  }

  saveCarrierSession(): void {
    if (!this.activeModalCarrier) return;
    const cid = this.activeModalCarrier.id;
    const carrierName = this.activeModalCarrier.name;
    this.isConnectingCarrier = true;

    if (cid === 'shipentegra') {
      localStorage.setItem('se_api_key', this.seApiKey.trim());
      localStorage.setItem('se_api_secret', this.seApiSecret.trim());
      this.ordersService.connectCarrier('shipentegra', this.modalTokenInput.trim(), this.seApiSecret.trim(), this.seApiKey.trim(), this.seApiSecret.trim());
      this.isConnectingCarrier = false;
      this.closeSessionModal();
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = this.transloco.translate('orders.messages.seSavedToast');
      setTimeout(() => this.saveSuccessMessage = null, 5000);
      return;
    }

    this.ordersService.connectCarrier(cid, this.modalTokenInput.trim());
    this.isConnectingCarrier = false;
    this.closeSessionModal();
    this.loadQuotesForSelected();
    this.calculateLiveProfit();
    this.saveSuccessMessage = this.transloco.translate('orders.messages.carrierSavedToast', { name: carrierName });
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  testCarrierConnection(): void {
    if (!this.activeModalCarrier) return;
    if (this.activeModalCarrier.id === 'aras' || this.activeModalCarrier.id === 'navlungo') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      if (val.isValid && !val.expired) {
        const locale = this.transloco.getActiveLang() === 'en' ? 'en-US' : 'tr-TR';
        const expStr = val.expDate ? val.expDate.toLocaleString(locale) : this.transloco.translate('orders.messages.jwtUnlimited');
        this.toastr.success(this.transloco.translate('orders.messages.arasTestValid', {
          remaining: val.timeLeftStr || this.transloco.translate('orders.messages.tokenValid'),
          expires: expStr
        }));
      } else {
        this.toastr.warning(this.transloco.translate('orders.messages.arasTestInvalid', { reason: val.reason || '' }));
      }
      return;
    }
    if (this.activeModalCarrier.id === 'shipentegra') {
      if (!this.seApiKey.trim() || !this.seApiSecret.trim()) {
        this.toastr.warning(this.transloco.translate('orders.messages.seFieldsRequired'));
        return;
      }
      this.isConnectingCarrier = true;
      this.etsyApi.connectCarrier({
        carrierKey: 'shipentegra',
        apiKey: this.seApiKey.trim(),
        apiSecret: this.seApiSecret.trim(),
        bearerToken: this.modalTokenInput.trim()
      }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
        next: () => {
          this.isConnectingCarrier = false;
          this.toastr.success(this.transloco.translate('orders.messages.seTestSuccess'));
        },
        error: (err) => {
          this.isConnectingCarrier = false;
          if (!err?.status) {
            this.toastr.error(this.transloco.translate('orders.messages.seTestFailed', {
              error: err?.error?.message || err?.message || this.transloco.translate('orders.messages.unknownError')
            }));
          }
        }
      });
      return;
    }
    this.toastr.info(this.transloco.translate('orders.messages.testNotSupported', { name: this.activeModalCarrier.name }));
  }

  disconnectSession(carrier: CarrierAccountSession): void {
    if (confirm(this.transloco.translate('orders.messages.confirmDisconnect', { name: carrier.name }))) {
      this.ordersService.disconnectCarrier(carrier.id);
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = this.transloco.translate('orders.messages.carrierDisconnectedToast', { name: carrier.name });
      setTimeout(() => this.saveSuccessMessage = null, 3000);
    }
  }

  openShiptomoreModal(): void {
    const stm = this.carrierSessions.find(s => s.id === 'shiptomore');
    const savedId = localStorage.getItem('stm_client_id');
    const savedSec = localStorage.getItem('stm_client_secret');
    this.stmClientId = stm?.apiKey || savedId || '';
    this.stmClientSecret = stm?.clientSecret || savedSec || '';

    if (this.stmClientId && this.stmClientSecret) {
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusReady');
      this.stmStatusKind = 'ok';
    } else if (this.stmClientId) {
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusNeedSecret');
      this.stmStatusKind = 'info';
    } else {
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusMissing');
      this.stmStatusKind = 'muted';
    }
    this.isShiptomoreModalOpen = true;
  }

  closeShiptomoreModal(): void {
    this.isShiptomoreModalOpen = false;
  }

  openShiptomorePortal(): void {
    window.open('https://dev.shiptomore.com/portal/keys', '_blank');
    this.stmStatusText = this.transloco.translate('orders.shiptomore.statusPortalOpened');
    this.stmStatusKind = 'info';
  }

  saveShiptomore(): void {
    if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusBothRequired');
      this.stmStatusKind = 'danger';
      return;
    }

    localStorage.setItem('stm_client_id', this.stmClientId.trim());
    localStorage.setItem('stm_client_secret', this.stmClientSecret.trim());
    this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim(), this.stmClientId.trim(), this.stmClientSecret.trim());
    this.stmStatusText = this.transloco.translate('orders.shiptomore.statusSavedTesting');
    this.stmStatusKind = 'info';

    this.etsyApi.connectCarrier({
      carrierKey: 'shiptomore',
      apiKey: this.stmClientId.trim(),
      apiSecret: this.stmClientSecret.trim()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.stmStatusText = res?.message || this.transloco.translate('orders.shiptomore.statusConnectedOk');
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
        this.saveSuccessMessage = this.transloco.translate('orders.shiptomore.toastSaved');
        setTimeout(() => this.saveSuccessMessage = null, 5000);
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || this.transloco.translate('orders.shiptomore.statusServerWarnFallback');
        this.stmStatusText = this.transloco.translate('orders.shiptomore.statusSavedWarning', { msg });
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
      }
    });
  }

  testShiptomore(): void {
    if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusBothRequired');
      this.stmStatusKind = 'danger';
      return;
    }

    this.isTestingShiptomore = true;
    this.stmStatusText = this.transloco.translate('orders.shiptomore.statusTestingEndpoint');
    this.stmStatusKind = 'info';

    this.etsyApi.connectCarrier({
      carrierKey: 'shiptomore',
      apiKey: this.stmClientId.trim(),
      apiSecret: this.stmClientSecret.trim()
    }).pipe(takeUntilDestroyed(this.destroyRef)).subscribe({
      next: (res) => {
        this.isTestingShiptomore = false;
        this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim(), this.stmClientId.trim(), this.stmClientSecret.trim());
        this.stmStatusText = res?.message || this.transloco.translate('orders.shiptomore.statusConnectedOk');
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
      },
      error: (err) => {
        this.isTestingShiptomore = false;
        const msg = err?.error?.message || err?.message || this.transloco.translate('orders.shiptomore.statusConnectFailedFallback');
        this.stmStatusText = this.transloco.translate('orders.shiptomore.statusConnectError', { msg });
        this.stmStatusKind = 'danger';
      }
    });
  }

  forgetShiptomore(): void {
    if (confirm(this.transloco.translate('orders.shiptomore.confirmForget'))) {
      this.ordersService.disconnectCarrier('shiptomore');
      localStorage.removeItem('stm_client_id');
      localStorage.removeItem('stm_client_secret');
      this.stmClientId = '';
      this.stmClientSecret = '';
      this.stmStatusText = this.transloco.translate('orders.shiptomore.statusDeleted');
      this.stmStatusKind = 'danger';
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = this.transloco.translate('orders.shiptomore.toastDisconnected');
      setTimeout(() => this.saveSuccessMessage = null, 5000);
    }
  }

  openArasTemplateModal(): void {
    this.isArasTemplateModalOpen = true;
  }

  closeArasTemplateModal(): void {
    this.isArasTemplateModalOpen = false;
  }

  syncCapturedTemplates(): void {
    this.closeArasTemplateModal();
    this.saveSuccessMessage = this.transloco.translate('orders.messages.desktopTemplatesUnsupported');
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  openLabelPreviewModal(): void {
    this.isLabelPreviewOpen = true;
  }

  closeLabelPreviewModal(): void {
    this.isLabelPreviewOpen = false;
  }

  printLabel(): void {
    window.print();
  }

  openAddCarrierToast(): void {
    this.toastr.info(this.transloco.translate('orders.messages.addCarrierComingSoon'));
  }

  // --- HELPERS ---
  formatTry(valUsd: number): string {
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 36.25;
    const tryVal = (Number(valUsd) || 0) * rate;
    return '₺' + tryVal.toFixed(2);
  }
}
