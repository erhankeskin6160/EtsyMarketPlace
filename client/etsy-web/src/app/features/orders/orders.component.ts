import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute } from '@angular/router';
import { Subscription } from 'rxjs';
import { OrdersService } from '../../core/services/orders.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import {
  OrderFulfillmentItem,
  CarrierQuote,
  PackageSpecs,
  CarrierAccountSession,
  GtipCodeItem
} from '../../core/models/orders.models';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  templateUrl: './orders.component.html',
  styleUrl: './orders.component.css'
})
export class OrdersComponent implements OnInit, OnDestroy {
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

  // PACKAGE PRESETS (FAZ 4)
  readonly packagePresets = [
    { id: 'envelope', label: '✉️ Zarf (22×15×4 • 0.15kg)', widthCm: 15, lengthCm: 22, heightCm: 4, weightKg: 0.15 },
    { id: 'small', label: '📦 Küçük (15×15×10 • 0.25kg)', widthCm: 15, lengthCm: 15, heightCm: 10, weightKg: 0.25 },
    { id: 'medium', label: '📦 Orta (20×20×15 • 0.50kg)', widthCm: 20, lengthCm: 20, heightCm: 15, weightKg: 0.50 },
    { id: 'large', label: '📦 Büyük (30×25×20 • 1.20kg)', widthCm: 25, lengthCm: 30, heightCm: 20, weightKg: 1.20 }
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
  stmStatusText: string = 'Durum: bilinmiyor';
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
    private route: ActivatedRoute
  ) {}

  ngOnInit(): void {
    // 1. Subscribe to orders stream
    const ordersSub = this.ordersService.orders$.subscribe(orders => {
      this.allOrders = orders;
      if (!this.selectedOrder && orders.length > 0) {
        this.onSelectOrder(orders[0]);
      }
    });
    this.subs.push(ordersSub);

    // 2. Subscribe to selected order
    const selectedSub = this.ordersService.selectedOrder$.subscribe(order => {
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
          this.loadQuotesForSelected();
        }
      }
    });
    this.subs.push(selectedSub);

    // 3. Subscribe to carrier sessions
    const sessionsSub = this.ordersService.sessions$.subscribe(sessions => {
      this.carrierSessions = sessions;
    });
    this.subs.push(sessionsSub);

    // 4. Listen to queryParams from Shipping Hub
    const routeSub = this.route.queryParams.subscribe(params => {
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
        this.saveSuccessMessage = `✓ Kargo Hub'ından "${cName}" ($${cCost.toFixed(2)}) seçildi ve sipariş maliyetine uygulandı.`;
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
        label: 'Teslimat Adresi',
        ok: hasAddress,
        hint: hasAddress ? 'Sokak, şehir ve ülke doğrulandı' : 'Sokak veya şehir eksik'
      },
      {
        label: 'Alıcı Telefonu',
        ok: hasPhone,
        hint: hasPhone ? this.selectedOrder.phone! : 'Ekspres kurye için telefon önerilir'
      },
      {
        label: 'GTİP / HS Kodu',
        ok: hasGtip,
        hint: hasGtip ? `${this.selectedOrder.gtipCode}` : 'Gümrük beyanı için GTİP seçin'
      }
    ];

    if (isEu) {
      checks.push({
        label: 'AB IOSS Kodu',
        ok: hasIoss,
        hint: hasIoss ? this.selectedOrder.iossNumber! : 'AB gönderisi için IOSS (IM3720000224) eksik'
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
      return `⚠️ Aras Global oturum tokeninizin süresi doldu!${detail} Gösterilen fiyatlar yedek listedir. Lütfen panelden tokeninizi yenileyin.`;
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
        return `🟢 Canlı ve Aktif (${val.timeLeftStr ? 'Kalan: ' + val.timeLeftStr : 'Geçerli'})`;
      }
      return `⚠️ ${val.reason || 'Süresi Dolmuş (HTTP 401)'}`;
    }
    return this.isCurrentModalTokenValid ? '🟢 Aktif' : '⚠️ Giriş Gerekli';
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

  loadQuotesForSelected(): void {
    if (!this.selectedOrder) return;
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 36.25;
    this.carrierQuotes = this.ordersService.calculateCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate
    );

    this.ordersService.fetchLiveCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate,
      this.selectedOrder.postalCode,
      this.selectedOrder.city,
      this.selectedOrder.state
    ).subscribe(res => {
      if (res && res.quotes && res.quotes.length > 0) {
        this.carrierQuotes = res.quotes;
      }
    });
  }

  applyCarrierQuote(quote: CarrierQuote): void {
    if (!this.selectedOrder) return;
    this.selectedOrder.selectedCarrier = quote.carrierKey;
    this.selectedOrder.carrierServiceName = quote.serviceType || quote.carrierName;
    this.editableShippingCost = quote.priceUsd;
    this.calculateLiveProfit();
  }

  // --- GTIP ENGINE ---
  onSearchGtip(): void {
    const instant = this.ordersService.searchGtip(this.gtipSearchTerm);
    this.gtipResults = instant;
    this.showGtipSuggestions = true;

    this.ordersService.searchGtipRemote(this.gtipSearchTerm).subscribe(results => {
      this.gtipResults = results;
      this.showGtipSuggestions = results.length > 0;
    });
  }

  autoMatchGtip(): void {
    if (!this.selectedOrder) return;
    const best = this.ordersService.suggestGtipForOrder(this.selectedOrder);
    this.selectGtip(best);
    this.saveSuccessMessage = `✨ Ürün başlığına göre GTİP otomatik eşleştirildi: ${best.code} (${best.category})`;
    setTimeout(() => this.saveSuccessMessage = null, 4000);
  }

  selectGtip(item: GtipCodeItem): void {
    this.gtipSearchTerm = `${item.code} - ${item.description}`;
    this.showGtipSuggestions = false;
    if (this.selectedOrder) {
      this.ordersService.updateOrderGtip(this.selectedOrder.orderId, item.code, item.description);
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
    ).subscribe(saved => {
      this.saveSuccessMessage = saved
        ? '✓ Maliyet, paket ölçüleri ve GTİP kalıcı olarak SQLite veritabanına kaydedildi.'
        : '⚠️ Maliyetler bu oturumda güncellendi (sunucu kaydı sırasında bağlantı uyarısı).';
      setTimeout(() => this.saveSuccessMessage = null, 5000);
    });
  }

  // --- SHIPMENT CREATION ---
  executeCreateShipment(): void {
    if (!this.selectedOrder) return;
    if (this.selectedOrder.isAddressMissing) {
      alert('Lütfen kargo oluşturmadan önce geçerli bir teslimat adresi girin (Adres Düzenle düğmesi).');
      return;
    }

    const quote = this.selectedCarrierQuote;
    const carrierKey = quote?.carrierKey || 'shipentegra';
    const serviceCode = quote?.serviceCode || quote?.quoteId || 'STANDART';
    const serviceType = quote?.serviceType || 'Express';
    const subCarrier = quote?.subCarrier || 'FedEx';

    if (carrierKey === 'shiptomore' && this.notifyEtsyOnShipment && !this.selectedOrder.phone?.trim()) {
      alert('⚠️ Canlı Etsy gönderiminde Ship to More (FedEx/UPS) uluslararası gümrük beyanı için alıcı telefon numarası zorunludur.\n\nLütfen "Adresi Düzenle" butonuna tıklayarak alıcı telefonunu girin.');
      this.openAddressModal();
      return;
    }

    const rId = Number(this.selectedOrder.orderId);
    if (!rId || isNaN(rId)) {
      alert('Geçersiz Sipariş ID (Receipt ID)');
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
      recipientAddress: {
        buyerName: this.selectedOrder.buyerName,
        name: this.selectedOrder.buyerName,
        streetAddress: this.selectedOrder.streetAddress || '',
        first_line: this.selectedOrder.streetAddress || '',
        secondAddress: this.selectedOrder.secondAddress || '',
        second_line: this.selectedOrder.secondAddress || '',
        city: this.selectedOrder.city || '',
        state: this.selectedOrder.state || '',
        postalCode: this.selectedOrder.postalCode || '',
        zip: this.selectedOrder.postalCode || '',
        countryCode: this.selectedOrder.countryCode || 'US',
        country_iso: this.selectedOrder.countryCode || 'US',
        phone: this.selectedOrder.phone || '',
        buyerEmail: this.selectedOrder.buyerEmail || '',
        email: this.selectedOrder.buyerEmail || ''
      }
    }).subscribe({
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
            this.ordersService.persistOrderFulfillmentMeta(this.selectedOrder).subscribe({ next: () => {}, error: () => {} });
          }
          const etsyNote = this.notifyEtsyOnShipment
            ? (res.etsyTrackingSubmitted ? 'Etsy mağazanıza takip numarası iletildi ve sipariş kargolandı yapıldı.' : (res.etsyTrackingMessage || 'Etsy bildirimi iletilemedi.'))
            : '🛡️ Test modu: Etsy mağazanıza bildirilmedi, siparişiniz Etsy üzerinde güvende.';
          this.saveSuccessMessage = `✅ Kargo başarıyla oluşturuldu! Takip No: ${res.trackingNumber}. ${etsyNote}`;
          setTimeout(() => this.saveSuccessMessage = null, 9000);
        } else {
          alert('Kargo oluşturulamadı: ' + (res?.message || res?.errorMessage || 'Bilinmeyen hata'));
        }
      },
      error: (err: any) => {
        this.isGeneratingShipment = false;
        const errMsg = err?.error?.errorMessage || err?.error?.message || err?.message || 'API Hatası';
        alert('Kargo oluşturma hatası: ' + errMsg);
      }
    });
  }

  formatOrderNumber(num?: string): string {
    if (!num) return '';
    return num.startsWith('#') ? num : `#${num}`;
  }

  // --- ADDRESS MODAL ACTIONS ---
  fillRawAddressFromSelectedOrder(): void {
    const order = this.selectedOrder;
    if (!order) return;
    const addrParts = [
      order.buyerName,
      order.streetAddress,
      order.secondAddress,
      [order.city, order.state, order.postalCode].filter(Boolean).join(' '),
      order.country || order.countryCode
    ].filter(p => Boolean(p && String(p).trim().length > 0));

    this.rawAddressInput = order.rawAddressText?.trim() 
      || (addrParts.length > 0 ? addrParts.join('\n') : '')
      || order.formattedAddress 
      || order.addressSnippet 
      || '';
  }

  openAddressModal(): void {
    const order = this.selectedOrder;
    if (!order) return;
    this.fillRawAddressFromSelectedOrder();
    this.aiParseSuccessMessage = null;
    this.aiParseErrorMessage = null;
    this.editAddressModel = {
      buyerName: order.buyerName || '',
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
    this.isAddressModalOpen = true;
  }

  closeAddressModal(): void {
    this.isAddressModalOpen = false;
    this.rawAddressInput = '';
    this.aiParseSuccessMessage = null;
    this.aiParseErrorMessage = null;
  }

  parseAddressWithAi(): void {
    if (!this.rawAddressInput || !this.rawAddressInput.trim()) {
      this.aiParseErrorMessage = 'Lütfen ayrıştırılacak adres metnini yapıştırın.';
      return;
    }

    this.isAiParsing = true;
    this.aiParseErrorMessage = null;
    this.aiParseSuccessMessage = null;

    this.ordersService.aiParseAddress(this.rawAddressInput).subscribe({
      next: (res: any) => {
        this.isAiParsing = false;
        if (!res) {
          this.aiParseErrorMessage = 'Yapay zeka adres yanıtı oluşturamadı.';
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

        this.aiParseSuccessMessage = `✨ Adres ${res.source || 'AI'} motoru ile başarıyla ayrıştırıldı ve kutucuklara yerleştirildi!`;
        setTimeout(() => this.aiParseSuccessMessage = null, 5000);
      },
      error: (err: any) => {
        this.isAiParsing = false;
        this.aiParseErrorMessage = 'Yapay zeka ayrıştırması başarısız oldu: ' + (err?.error?.message || err?.message || 'Bilinmeyen hata');
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

    this.ordersService.uploadOrdersCsv(file).subscribe({
      next: (res: any) => {
        this.isUploadingCsv = false;
        this.csvUploadSuccessMessage = `✅ ${res?.count || 0} sipariş adresi CSV'den başarıyla yüklendi ve eşitlendi!`;
        this.ordersService.loadOrders();
        setTimeout(() => this.csvUploadSuccessMessage = null, 6000);
      },
      error: (err: any) => {
        this.isUploadingCsv = false;
        this.csvUploadErrorMessage = 'CSV yükleme hatası: ' + (err?.error?.message || err?.message || 'Bilinmeyen hata');
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
      alert('Geçersiz Sipariş ID');
      return;
    }
    this.ordersService.updateOrderAddress(rId, this.editAddressModel).subscribe({
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
        this.saveSuccessMessage = '✅ Alıcı teslimat adresi kalıcı olarak SQLite veritabanına kaydedildi!';
        setTimeout(() => this.saveSuccessMessage = null, 4000);
      },
      error: (err: any) => {
        alert('Adres kaydedilirken hata oluştu: ' + (err?.error?.message || err?.message || ''));
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
      }
    }
    this.isSessionModalOpen = true;
  }

  triggerArasBrowserCapture(openBrowser: boolean = true): void {
    if (!this.activeModalCarrier) return;
    if (this.arasEmail.trim()) {
      localStorage.setItem('aras_saved_email', this.arasEmail.trim());
    }

    this.isCapturingBrowserToken = true;
    this.browserCaptureStatus = openBrowser
      ? '🌐 Aras Global giriş penceresi açıldı. Giriş yaptığınız anda Bearer Token otomatik yakalanacaktır...'
      : '🔄 Kayıtlı Refresh Token / kimlik bilgileriyle Aras Global oturumu yenileniyor...';

    this.ordersService.triggerCarrierBrowserLogin(
      this.activeModalCarrier.id,
      this.arasEmail.trim() || undefined,
      this.arasPassword.trim() || undefined,
      openBrowser
    ).subscribe({
      next: (res: any) => {
        this.isCapturingBrowserToken = false;
        if (res && res.bearerToken) {
          this.modalTokenInput = res.bearerToken;
          if (this.activeModalCarrier) {
            this.activeModalCarrier.isConnected = true;
            this.activeModalCarrier.tokenOrKey = res.bearerToken;
          }
          this.browserCaptureStatus = '✅ Aras Global Bearer Token otomatik yakalandı, şifreli olarak kaydedildi ve aktif edildi!';
          this.loadQuotesForSelected();
          this.calculateLiveProfit();
          this.saveSuccessMessage = '✅ Aras Global oturum tokenı otomatik yakalandı ve canlı fiyatlar güncellendi!';
          setTimeout(() => {
            this.saveSuccessMessage = null;
          }, 6000);
        } else {
          this.browserCaptureStatus = '⚠️ ' + (res?.message || 'Token yakalanamadı. Lütfen açılan pencerede giriş yapın veya tokenı yapıştırın.');
        }
      },
      error: (err: any) => {
        this.isCapturingBrowserToken = false;
        this.browserCaptureStatus = '❌ Yakalama hatası: ' + (err?.error?.message || err?.message || 'Sunucuya ulaşılamadı.');
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
    this.isConnectingCarrier = true;

    if (cid === 'shipentegra') {
      localStorage.setItem('se_api_key', this.seApiKey.trim());
      localStorage.setItem('se_api_secret', this.seApiSecret.trim());
      this.ordersService.connectCarrier('shipentegra', this.modalTokenInput.trim(), this.seApiSecret.trim(), this.seApiKey.trim(), this.seApiSecret.trim());
      this.isConnectingCarrier = false;
      this.closeSessionModal();
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = '✅ ShipEntegra API anahtarları kaydedildi ve bağlandı!';
      setTimeout(() => this.saveSuccessMessage = null, 5000);
      return;
    }

    this.ordersService.connectCarrier(cid, this.modalTokenInput.trim());
    this.isConnectingCarrier = false;
    this.closeSessionModal();
    this.loadQuotesForSelected();
    this.calculateLiveProfit();
    this.saveSuccessMessage = `✅ ${this.activeModalCarrier.name} bağlantısı sunucuya kaydedildi!`;
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  testCarrierConnection(): void {
    if (!this.activeModalCarrier) return;
    if (this.activeModalCarrier.id === 'aras') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      if (val.isValid && !val.expired) {
        const expStr = val.expDate ? val.expDate.toLocaleString('tr-TR') : 'Süresiz';
        alert(`✅ Aras Global JWT Token Geçerli!\n\n• Durum: Canlı API Fiyatlandırması Aktif\n• Kalan Süre: ${val.timeLeftStr || 'Geçerli'}\n• Bitiş Zamanı: ${expStr}\n\nOturumu kaydederek doğrudan canlı API tekliflerini kullanabilirsiniz.`);
      } else {
        alert(`⚠️ Aras Global Token Süresi Dolmuş / Geçersiz!\n\n• Hata: ${val.reason}\n• API Yanıtı: HTTP 401 Unauthorized\n\nBu token kullanıldığında API hata vereceğinden siparişlerin kilitlenmemesi için sistem otomatik olarak tahmini sözleşme fiyatlarına geçer.\nLütfen panel.arasglobalcargo.com adresinden güncel token'ınızı kopyalayıp buraya yapıştırın.`);
      }
      return;
    }
    if (this.activeModalCarrier.id === 'shipentegra') {
      if (!this.seApiKey.trim() || !this.seApiSecret.trim()) {
        alert('Lütfen ShipEntegra Client ID ve Client Secret alanlarını doldurun.');
        return;
      }
      this.isConnectingCarrier = true;
      this.etsyApi.connectCarrier({
        carrierKey: 'shipentegra',
        apiKey: this.seApiKey.trim(),
        apiSecret: this.seApiSecret.trim(),
        bearerToken: this.modalTokenInput.trim()
      }).subscribe({
        next: () => {
          this.isConnectingCarrier = false;
          alert('✅ ShipEntegra API Bağlantısı Başarılı!\nPublic API kimlik doğrulaması tamamlandı. Canlı navlun ve gönderi oluşturma aktif.');
        },
        error: (err) => {
          this.isConnectingCarrier = false;
          alert('❌ ShipEntegra Bağlantı Testi Başarısız:\n' + (err?.error?.message || err?.message || 'Bilinmeyen hata'));
        }
      });
      return;
    }
    alert(`${this.activeModalCarrier.name} için sunucu taraflı bağlantı testi henüz desteklenmiyor.`);
  }

  disconnectSession(carrier: CarrierAccountSession): void {
    if (confirm(`${carrier.name} oturumunu kapatmak ve sistem bağlantısını kesmek istediğinize emin misiniz?`)) {
      this.ordersService.disconnectCarrier(carrier.id);
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = `⚠️ ${carrier.name} bağlantısı kesildi.`;
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
      this.stmStatusText = 'Durum: Anahtarlar hazır. Bağlantıyı Test Et butonuna basabilirsiniz.';
      this.stmStatusKind = 'ok';
    } else if (this.stmClientId) {
      this.stmStatusText = 'Durum: Client ID tanımlı. Lütfen panelden aldığınız Client Secret bilgisini yapıştırın.';
      this.stmStatusKind = 'info';
    } else {
      this.stmStatusText = 'Durum: API anahtarı girilmemiş.';
      this.stmStatusKind = 'muted';
    }
    this.isShiptomoreModalOpen = true;
  }

  closeShiptomoreModal(): void {
    this.isShiptomoreModalOpen = false;
  }

  openShiptomorePortal(): void {
    window.open('https://dev.shiptomore.com/portal/keys', '_blank');
    this.stmStatusText = 'Geliştirici paneli açıldı. Oluşturduğunuz anahtarları yukarıdaki alanlara yapıştırın.';
    this.stmStatusKind = 'info';
  }

  saveShiptomore(): void {
    if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
      this.stmStatusText = 'İki alan da doldurulmalıdır.';
      this.stmStatusKind = 'danger';
      return;
    }

    localStorage.setItem('stm_client_id', this.stmClientId.trim());
    localStorage.setItem('stm_client_secret', this.stmClientSecret.trim());
    this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim(), this.stmClientId.trim(), this.stmClientSecret.trim());
    this.stmStatusText = 'Anahtar kaydedildi. Bağlantı test ediliyor...';
    this.stmStatusKind = 'info';

    this.etsyApi.connectCarrier({
      carrierKey: 'shiptomore',
      apiKey: this.stmClientId.trim(),
      apiSecret: this.stmClientSecret.trim()
    }).subscribe({
      next: (res) => {
        this.stmStatusText = res?.message || '✅ Ship to More resmî API bağlantısı başarılı! Canlı navlun tarifeleri aktif.';
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
        this.saveSuccessMessage = '✅ Ship to More bağlantısı başarıyla kaydedildi.';
        setTimeout(() => this.saveSuccessMessage = null, 5000);
      },
      error: (err) => {
        const msg = err?.error?.message || err?.message || 'Sunucu bağlantı uyarısı';
        this.stmStatusText = '⚠️ Kaydedildi: ' + msg;
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
      }
    });
  }

  testShiptomore(): void {
    if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
      this.stmStatusText = 'İki alan da doldurulmalıdır.';
      this.stmStatusKind = 'danger';
      return;
    }

    this.isTestingShiptomore = true;
    this.stmStatusText = 'Bağlantı deneniyor (dev.shiptomore.com/v1/providers)...';
    this.stmStatusKind = 'info';

    this.etsyApi.connectCarrier({
      carrierKey: 'shiptomore',
      apiKey: this.stmClientId.trim(),
      apiSecret: this.stmClientSecret.trim()
    }).subscribe({
      next: (res) => {
        this.isTestingShiptomore = false;
        this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim(), this.stmClientId.trim(), this.stmClientSecret.trim());
        this.stmStatusText = res?.message || '✅ Ship to More resmî API bağlantısı başarılı! Canlı navlun tarifeleri aktif.';
        this.stmStatusKind = 'ok';
        this.loadQuotesForSelected();
        this.calculateLiveProfit();
      },
      error: (err) => {
        this.isTestingShiptomore = false;
        const msg = err?.error?.message || err?.message || 'Bağlantı başarısız';
        this.stmStatusText = '❌ Bağlantı hatası: ' + msg;
        this.stmStatusKind = 'danger';
      }
    });
  }

  forgetShiptomore(): void {
    if (confirm('Kayıtlı Ship to More anahtarı silinsin mi?\n\nSilindikten sonra fiyat ve gönderi işlemleri çalışmaz.')) {
      this.ordersService.disconnectCarrier('shiptomore');
      localStorage.removeItem('stm_client_id');
      localStorage.removeItem('stm_client_secret');
      this.stmClientId = '';
      this.stmClientSecret = '';
      this.stmStatusText = 'Anahtar silindi.';
      this.stmStatusKind = 'danger';
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = '⚠️ Ship to More bağlantısı kesildi.';
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
    this.saveSuccessMessage = 'Masaüstü şablonları senkronizasyonu web istemcisinde desteklenmiyor.';
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
    alert('➕ Yeni Kargo Entegrasyonu:\nYakında özel API Key / Secret girerek DHL, PTT, UPS ve diğer taşıyıcıları doğrudan bu merkeze ekleyebileceksiniz.');
  }

  // --- HELPERS ---
  formatTry(valUsd: number): string {
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 36.25;
    const tryVal = (Number(valUsd) || 0) * rate;
    return '₺' + tryVal.toFixed(2);
  }
}
