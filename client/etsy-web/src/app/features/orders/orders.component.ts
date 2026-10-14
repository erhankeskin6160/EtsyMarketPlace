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
  selectedCarrierFilter: string = 'T�m�';

  // CARRIER HUB TOGGLE
  isAccountsHubVisible: boolean = true;

  // GTIP SEARCH
  gtipSearchTerm: string = '';
  showGtipSuggestions: boolean = false;
  gtipResults: GtipCodeItem[] = [];

  // EDITABLE COSTS
  editableProductCost: number = 0;
  editableShippingCost: number = 0;
  liveNetProfit: number = 0;
  liveProfitMargin: number = 0;

  // MODALS STATE
  isSessionModalOpen: boolean = false;
  activeModalCarrier: CarrierAccountSession | null = null;
  modalTokenInput: string = '';
  seApiKey: string = '8501f5b8c5bbbe241d6d35eccdcdcfc3';
  seApiSecret: string = 'f8515b70e86b0671ca653c2702b8a2295157b32b';
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
        this.selectedOrder = order;
        this.editableProductCost = order.productCost;
        this.editableShippingCost = order.shippingCost;
        this.gtipSearchTerm = order.gtipCode ? `${order.gtipCode} - ${order.gtipDescription || ''}` : '';
        this.calculateLiveProfit();
        this.loadQuotesForSelected();
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
        this.editableShippingCost = cCost;
        if (this.selectedOrder) {
          this.selectedOrder.shippingCost = cCost;
          this.selectedOrder.isCostMissing = false;
          this.calculateLiveProfit();
        }
        this.saveSuccessMessage = `? Kargo Hub'�ndan "${cName}" ($${cCost}) se�ildi ve sipari� maliyetine uyguland�.`;
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
    return this.carrierQuotes.find(q => q.carrierKey === this.selectedOrder?.selectedCarrier) || this.carrierQuotes[0] || null;
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
    if (tab === 'T�m�') return this.carrierQuotes.length;
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
      return `?? Aras Global oturum tokeninizin s�resi doldu!${detail} G�sterilen fiyatlar yedek listedir. L�tfen panelden tokeninizi yenileyin.`;
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
        return `? Canl� ve Aktif (${val.timeLeftStr ? 'Kalan: ' + val.timeLeftStr : 'Ge�erli'})`;
      }
      return `?? ${val.reason || 'S�resi Dolmu� (HTTP 401)'}`;
    }
    return this.isCurrentModalTokenValid ? '? Aktif' : '?? Giri� Gerekli';
  }

  get filteredQuotes(): CarrierQuote[] {
    if (this.selectedCarrierFilter === 'T�m�') return this.carrierQuotes;
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

  onDimensionChange(): void {
    if (!this.selectedOrder) return;
    this.ordersService.updatePackageSpecs(this.selectedOrder.orderId, this.selectedOrder.packageSpecs);
    this.loadQuotesForSelected();
  }

  loadQuotesForSelected(): void {
    if (!this.selectedOrder) return;
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 49.13;
    this.carrierQuotes = this.ordersService.calculateCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      rate
    );
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
    const results = this.ordersService.searchGtip(this.gtipSearchTerm);
    this.gtipResults = results;
    this.showGtipSuggestions = true;
    if (results.length > 0 && !this.gtipSearchTerm.includes('-')) {
      this.selectGtip(results[0]);
    }
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
    this.ordersService.updateOrderCosts(
      this.selectedOrder.orderId, 
      Number(this.editableProductCost) || 0, 
      Number(this.editableShippingCost) || 0
    );

    // Save GTIP if present
    const dash = this.gtipSearchTerm.indexOf('-');
    const code = dash > 0 ? this.gtipSearchTerm.substring(0, dash).trim() : this.gtipSearchTerm.trim();
    const desc = dash > 0 ? this.gtipSearchTerm.substring(dash + 1).trim() : '';
    if (code) {
      this.ordersService.updateOrderGtip(this.selectedOrder.orderId, code, desc);
    }

    this.saveSuccessMessage = 'Maliyet ve GT�P de�erleri yaln�zca bu oturumda g�ncellendi; kal�c� kay�t API�de desteklenmiyor.';
    setTimeout(() => this.saveSuccessMessage = null, 5000);
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
    const serviceCode = quote?.serviceCode || 'STANDART';
    const serviceType = quote?.serviceType || 'Express';
    const subCarrier = quote?.subCarrier || 'FedEx';

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
      notifyEtsy: true,
      recipientAddress: {
        name: this.selectedOrder.buyerName,
        first_line: this.selectedOrder.streetAddress || '',
        second_line: this.selectedOrder.secondAddress || '',
        city: this.selectedOrder.city || '',
        state: this.selectedOrder.state || '',
        zip: this.selectedOrder.postalCode || '',
        country_iso: this.selectedOrder.countryCode || 'US',
        phone: this.selectedOrder.phone || '',
        email: this.selectedOrder.buyerEmail || ''
      }
    }).subscribe({
      next: (res: any) => {
        this.isGeneratingShipment = false;
        if (res && res.success) {
          if (this.selectedOrder) {
            this.selectedOrder.status = 'shipped';
            this.selectedOrder.trackingNumber = res.trackingNumber;
            this.selectedOrder.carrier = res.carrierName || carrierKey;
            this.selectedOrder.labelUrl = res.labelUrl;
            this.selectedOrder.labelBase64 = res.labelBase64;
          }
          this.saveSuccessMessage = `✅ Kargo başarıyla oluşturuldu! Takip No: ${res.trackingNumber}. Etsy bildirimi: ${res.etsyNotified ? 'Gönderildi' : 'Beklemede'}`;
          setTimeout(() => this.saveSuccessMessage = null, 8000);
        } else {
          alert('Kargo oluşturulamadı: ' + (res?.message || 'Bilinmeyen hata'));
        }
      },
      error: (err: any) => {
        this.isGeneratingShipment = false;
        const errMsg = err?.error?.message || err?.message || 'API Hatası';
        alert('Kargo oluşturma hatası: ' + errMsg);
      }
    });
  }

  // --- ADDRESS MODAL ACTIONS ---
  openAddressModal(): void {
    if (!this.selectedOrder) return;
    this.editAddressModel = {
      buyerName: this.selectedOrder.buyerName || '',
      buyerEmail: this.selectedOrder.buyerEmail || '',
      phone: this.selectedOrder.phone || '',
      streetAddress: this.selectedOrder.streetAddress || '',
      secondAddress: this.selectedOrder.secondAddress || '',
      city: this.selectedOrder.city || '',
      state: this.selectedOrder.state || '',
      postalCode: this.selectedOrder.postalCode || '',
      country: this.selectedOrder.country || 'Amerika Birleşik Devletleri',
      countryCode: this.selectedOrder.countryCode || 'US',
      iossNumber: this.selectedOrder.iossNumber || ''
    };
    this.isAddressModalOpen = true;
  }

  closeAddressModal(): void {
    this.isAddressModalOpen = false;
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
        this.saveSuccessMessage = '✅ Alıcı teslimat adresi başarıyla kaydedildi!';
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
    if (carrier.id === 'shipentegra') {
      this.seApiKey = carrier.apiKey || localStorage.getItem('se_api_key') || '8501f5b8c5bbbe241d6d35eccdcdcfc3';
      this.seApiSecret = carrier.apiSecret || localStorage.getItem('se_api_secret') || 'f8515b70e86b0671ca653c2702b8a2295157b32b';
      this.modalTokenInput = carrier.tokenOrKey || '';
    } else {
      this.modalTokenInput = carrier.tokenOrKey || '';
    }
    this.isSessionModalOpen = true;
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
    this.saveSuccessMessage = `✅ ${this.activeModalCarrier.name} bağlantısı kaydedildi!`;
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
        next: (res) => {
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
    if (confirm(`${carrier.name} oturumunu kapatmak ve sistem ba�lant�s�n� kesmek istedi�inize emin misiniz?`)) {
      this.ordersService.disconnectCarrier(carrier.id);
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = `?? ${carrier.name} ba�lant�s� kesildi.`;
      setTimeout(() => this.saveSuccessMessage = null, 3000);
    }
  }

  openShiptomoreModal(): void {
    const stm = this.carrierSessions.find(s => s.id === 'shiptomore');
    const savedId = localStorage.getItem('stm_client_id');
    const savedSec = localStorage.getItem('stm_client_secret');
    this.stmClientId = stm?.apiKey || savedId || 'NhuCndOc1smCm0uBInrLByukAoV5JNRj';
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
    this.saveSuccessMessage = 'Masa�st� �ablonlar� senkronizasyonu web istemcisinde desteklenmiyor.';
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
    alert('? Yeni Kargo Entegrasyonu:\nYak�nda �zel API Key / Secret girerek DHL, PTT, UPS ve di�er ta��y�c�lar� do�rudan bu merkeze ekleyebileceksiniz.');
  }

  // --- HELPERS ---
  formatTry(valUsd: number): string {
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 49.13;
    const tryVal = (Number(valUsd) || 0) * rate;
    return '?' + tryVal.toFixed(2);
  }
}
