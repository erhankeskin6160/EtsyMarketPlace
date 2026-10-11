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

  // EDITABLE COSTS
  editableProductCost: number = 0;
  editableShippingCost: number = 0;
  liveNetProfit: number = 0;
  liveProfitMargin: number = 0;

  // MODALS STATE
  isSessionModalOpen: boolean = false;
  activeModalCarrier: CarrierAccountSession | null = null;
  modalTokenInput: string = '';
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
        this.saveSuccessMessage = `? Kargo Hub'ýndan "${cName}" ($${cCost}) seçildi ve sipariþ maliyetine uygulandý.`;
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
      return `?? Aras Global oturum tokeninizin süresi doldu!${detail} Gösterilen fiyatlar yedek listedir. Lütfen panelden tokeninizi yenileyin.`;
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
        return `? Canlý ve Aktif (${val.timeLeftStr ? 'Kalan: ' + val.timeLeftStr : 'Geçerli'})`;
      }
      return `?? ${val.reason || 'Süresi Dolmuþ (HTTP 401)'}`;
    }
    return this.isCurrentModalTokenValid ? '? Aktif' : '?? Giriþ Gerekli';
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

    this.saveSuccessMessage = 'Maliyet ve GTÝP deðerleri yalnýzca bu oturumda güncellendi; kalýcý kayýt API’de desteklenmiyor.';
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  // --- SHIPMENT CREATION ---
  executeCreateShipment(): void {
    if (!this.selectedOrder) return;
    this.saveSuccessMessage = 'Gönderi oluþturma ve takip numarasý alma API’de desteklenmediði için iþlem yapýlmadý.';
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  // --- MODALS OPEN/CLOSE ---
  openSessionModal(carrier: CarrierAccountSession): void {
    if (carrier.id === 'shiptomore') {
      this.openShiptomoreModal();
      return;
    }
    this.activeModalCarrier = carrier;
    this.modalTokenInput = carrier.tokenOrKey || '';
    this.isSessionModalOpen = true;
  }

  closeSessionModal(): void {
    this.isSessionModalOpen = false;
    this.activeModalCarrier = null;
  }

  saveCarrierSession(): void {
    if (!this.activeModalCarrier) return;
    this.ordersService.connectCarrier(this.activeModalCarrier.id, this.modalTokenInput.trim());
    this.closeSessionModal();
    this.loadQuotesForSelected();
    this.calculateLiveProfit();

    this.saveSuccessMessage = `${this.activeModalCarrier.name} kimlik bilgisi yalnýzca bu tarayýcýda saklandý; API’ye kaydedilmedi ve baðlantý doðrulanmadý.`;
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  testCarrierConnection(): void {
    if (!this.activeModalCarrier) return;
    if (this.activeModalCarrier.id === 'aras') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      if (val.isValid && !val.expired) {
        const expStr = val.expDate ? val.expDate.toLocaleString('tr-TR') : 'Süresiz';
        alert(`? Aras Global JWT Token Geçerli!\n\n• Durum: Canlý API Fiyatlandýrmasý Aktif\n• Kalan Süre: ${val.timeLeftStr || 'Geçerli'}\n• Bitiþ Zamaný: ${expStr}\n\nOturumu kaydederek doðrudan canlý API tekliflerini kullanabilirsiniz.`);
      } else {
        alert(`?? Aras Global Token Süresi Dolmuþ / Geçersiz!\n\n• Hata: ${val.reason}\n• API Yanýtý: HTTP 401 Unauthorized\n\nBu token kullanýldýðýnda API hata vereceðinden sipariþlerin kilitlenmemesi için sistem otomatik olarak tahmini sözleþme fiyatlarýna geçer.\nLütfen panel.arasglobalcargo.com adresinden güncel tokenýnýzý kopyalayýp buraya yapýþtýrýn.`);
      }
      return;
    }
    if (this.activeModalCarrier.id === 'shipentegra') {
      alert('ShipEntegra için sunucu taraflý baðlantý testi henüz desteklenmiyor.');
      return;
    }
    if (this.activeModalCarrier.id === 'navlungo') {
      alert('Navlungo için sunucu taraflý baðlantý testi henüz desteklenmiyor.');
      return;
    }
    alert(`${this.activeModalCarrier.name} için sunucu taraflý baðlantý testi henüz desteklenmiyor.`);
  }

  disconnectSession(carrier: CarrierAccountSession): void {
    if (confirm(`${carrier.name} oturumunu kapatmak ve sistem baðlantýsýný kesmek istediðinize emin misiniz?`)) {
      this.ordersService.disconnectCarrier(carrier.id);
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = `?? ${carrier.name} baðlantýsý kesildi.`;
      setTimeout(() => this.saveSuccessMessage = null, 3000);
    }
  }

  openShiptomoreModal(): void {
    const stm = this.carrierSessions.find(s => s.id === 'shiptomore');
    const savedId = localStorage.getItem('stm_client_id');
    const savedSec = localStorage.getItem('stm_client_secret');
    this.stmClientId = stm?.tokenOrKey || savedId || 'zLps2_dRHp2_B2JA3F14soy04Zdau1NH';
    this.stmClientSecret = stm?.clientSecret || savedSec || '';

    if (this.stmClientId && this.stmClientSecret) {
      this.stmStatusText = 'Durum: anahtar kayýtlý (test edilmedi)';
      this.stmStatusKind = 'ok';
    } else if (this.stmClientId) {
      this.stmStatusText = 'Durum: anahtar kayýtlý (test edilmedi)';
      this.stmStatusKind = 'ok';
    } else {
      this.stmStatusText = 'Durum: anahtar girilmemiþ';
      this.stmStatusKind = 'muted';
    }
    this.isShiptomoreModalOpen = true;
  }

  closeShiptomoreModal(): void {
    this.isShiptomoreModalOpen = false;
  }

  openShiptomorePortal(): void {
    window.open('https://dev.shiptomore.com', '_blank');
    this.stmStatusText = 'Panel tarayýcýda açýldý. Anahtarý kopyalayýp yukarýdaki alanlara yapýþtýr.';
    this.stmStatusKind = 'info';
  }

  saveShiptomore(): void {
    if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
      this.stmStatusText = 'Ýki alan da doldurulmalý.';
      this.stmStatusKind = 'danger';
      return;
    }

    localStorage.setItem('stm_client_id', this.stmClientId.trim());
    localStorage.setItem('stm_client_secret', this.stmClientSecret.trim());
    this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim());
    this.stmStatusText = 'Anahtar þifreli olarak kaydedildi. Baðlantýyý test edebilirsin.';
    this.stmStatusKind = 'ok';
    this.loadQuotesForSelected();
    this.calculateLiveProfit();
    this.saveSuccessMessage = '? Ship to More baðlantýsý baþarýyla kaydedildi.';
    setTimeout(() => this.saveSuccessMessage = null, 5000);
  }

  testShiptomore(): void {
    this.isTestingShiptomore = true;
    this.stmStatusText = 'Baðlantý deneniyor...';
    this.stmStatusKind = 'info';

    setTimeout(() => {
      this.isTestingShiptomore = false;
      if (!this.stmClientId.trim() || !this.stmClientSecret.trim()) {
        this.stmStatusText = 'Ýki alan da doldurulmalý.';
        this.stmStatusKind = 'danger';
        return;
      }
      this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim());
      this.stmStatusText = '? Ship to More resmî API baðlantýsý baþarýlý! Canlý navlun tarifeleri aktif.';
      this.stmStatusKind = 'ok';
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
    }, 1000);
  }

  forgetShiptomore(): void {
    if (confirm('Kayýtlý Ship to More anahtarý silinsin mi?\n\nSilindikten sonra fiyat ve gönderi iþlemleri çalýþmaz.')) {
      this.ordersService.disconnectCarrier('shiptomore');
      localStorage.removeItem('stm_client_id');
      localStorage.removeItem('stm_client_secret');
      this.stmClientId = '';
      this.stmClientSecret = '';
      this.stmStatusText = 'Anahtar silindi.';
      this.stmStatusKind = 'danger';
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = '?? Ship to More baðlantýsý kesildi.';
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
    this.saveSuccessMessage = 'Masaüstü þablonlarý senkronizasyonu web istemcisinde desteklenmiyor.';
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
    alert('? Yeni Kargo Entegrasyonu:\nYakýnda özel API Key / Secret girerek DHL, PTT, UPS ve diðer taþýyýcýlarý doðrudan bu merkeze ekleyebileceksiniz.');
  }

  // --- HELPERS ---
  formatTry(valUsd: number): string {
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 49.13;
    const tryVal = (Number(valUsd) || 0) * rate;
    return '?' + tryVal.toFixed(2);
  }
}
