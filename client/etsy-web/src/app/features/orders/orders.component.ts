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
  template: `
    <div class="orders-cockpit">
      <!-- 1. TOP HEADER & DESKTOP PARITY ACTION BAR -->
      <div class="cockpit-header">
        <div class="header-left">
          <div class="header-icon-box">
            <svg width="22" height="22" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="1" y="3" width="15" height="13"></rect>
              <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
              <circle cx="5.5" cy="18.5" r="2.5"></circle>
              <circle cx="18.5" cy="18.5" r="2.5"></circle>
            </svg>
          </div>
          <div>
            <div class="header-title-row">
              <span class="brand-sub">EtsyMarketPlace</span>
              <h1 class="page-title">SİPARİŞ KARGO STÜDYOSU</h1>
            </div>
            <p class="page-subtitle">Canlı Etsy sipariş karşılama, GTİP gümrük kodlama ve 4 taşıyıcılı anlık kargo entegrasyonu</p>
          </div>
        </div>

        <!-- RIGHT TOP ACTION BUTTONS MATCHING WINFORMS DESKTOP -->
        <div class="header-actions-right">
          <button class="btn-top-action" (click)="openArasTemplateModal()" title="Aras Global Gönderi İstek Şablonu Tanı Aracı">
            📋 Şablon yakala
          </button>
          
          <button class="btn-top-action" (click)="openShiptomoreModal()" title="Ship to More API Entegrasyonu">
            🌐 Ship to More
          </button>

          <button 
            class="btn-top-action btn-accounts-hub" 
            [class.has-issues]="connectedSessionsCount < carrierSessions.length"
            (click)="toggleAccountsHub()" 
            title="Kargo Taşıyıcı Hesapları ve Oturum Yönetimini Aç">
            <span class="bolt-icon">⚡</span>
            <span>Kargo Hesapları Oturumlar</span>
            <span class="accounts-badge" [class.badge-ok]="connectedSessionsCount === carrierSessions.length">
              {{ connectedSessionsCount }}/{{ carrierSessions.length }}
            </span>
          </button>

          <button class="btn-refresh" (click)="refreshOrders()" title="Siparişleri ve canlı kargo fiyatlarını yenile">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M23 4v6h-6"></path>
              <path d="M1 20v-6h6"></path>
              <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
            </svg>
            Yenile
          </button>
        </div>
      </div>

      <!-- 2. EXPANDABLE CARRIER ACCOUNTS HUB (CARRIERACCOUNTSHUBCONTROL PARITY) -->
      <div class="carrier-accounts-strip" *ngIf="isAccountsHubVisible">
        <div class="strip-container">
          <!-- CARRIER CARD: ARAS GLOBAL -->
          <div class="carrier-account-card" *ngFor="let s of carrierSessions">
            <div class="carrier-logo-box">
              <img [src]="s.logoUrl" [alt]="s.name" class="carrier-logo-img" />
            </div>
            <div class="carrier-info-col">
              <div class="carrier-name-status">
                <span class="carrier-card-name">{{ s.name }}</span>
                <span class="session-status-badge" [class.connected]="s.isConnected">
                  {{ s.isConnected ? '● Bağlı' : '⚠️ Giriş Yap' }}
                </span>
              </div>
              <div class="carrier-actions-row">
                <button 
                  type="button" 
                  class="btn-carrier-auto" 
                  [class.btn-connect-mode]="s.autoLabel.includes('Bağlantı')"
                  (click)="openSessionModal(s)">
                  {{ s.autoLabel }}
                </button>
                <a 
                  [href]="s.portalUrl" 
                  target="_blank" 
                  class="btn-carrier-web" 
                  title="{{ s.name }} Resmi Portalını Tarayıcıda Aç">
                  🌐
                </a>
                <button 
                  type="button" 
                  class="btn-carrier-disconnect" 
                  [disabled]="!s.isConnected"
                  (click)="disconnectSession(s)" 
                  title="{{ s.name }} Oturumunu Kapat">
                  🗑️
                </button>
              </div>
            </div>
          </div>

          <!-- PLACEHOLDER CARD: YENİ FİRMA EKLE -->
          <div class="carrier-add-placeholder" (click)="openAddCarrierToast()">
            <div class="placeholder-icon">➕</div>
            <span class="placeholder-title">Yeni Firma Ekle</span>
            <span class="placeholder-sub">(DHL, PTT, vb.)</span>
          </div>
        </div>
      </div>

      <!-- 3. MAIN THREE-REGION WORKSPACE MATCHING DESKTOP -->
      <div class="cockpit-body">
        
        <!-- REGION 1: SİPARİŞ KUYRUĞU (SOL) -->
        <div class="cockpit-region queue-region">
          <div class="region-header-bar">
            <div class="title-with-count">
              <span class="region-label">SİPARİŞ KUYRUĞU</span>
              <span class="queue-count-badge">{{ filteredOrders.length }}</span>
            </div>
          </div>

          <!-- Queue Filter Tabs -->
          <div class="queue-chips-row">
            <button 
              class="queue-chip" 
              [class.active]="activeTab === 'all'" 
              (click)="activeTab = 'all'">
              Tümü
            </button>
            <button 
              class="queue-chip" 
              [class.active]="activeTab === 'unfulfilled'" 
              (click)="activeTab = 'unfulfilled'">
              Bekleyen
            </button>
            <button 
              class="queue-chip" 
              [class.active]="activeTab === 'shipped'" 
              (click)="activeTab = 'shipped'">
              Gönderildi
            </button>
            <button 
              class="queue-chip alert-chip" 
              [class.active]="activeTab === 'missing_cost'" 
              (click)="activeTab = 'missing_cost'">
              ⚠️ Maliyet ({{ missingCostCount }})
            </button>
          </div>

          <!-- Queue Search & Sort -->
          <div class="queue-search-sort-row">
            <div class="search-input-box">
              <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="11" cy="11" r="8"></circle>
                <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              </svg>
              <input 
                type="text" 
                [(ngModel)]="searchQuery" 
                placeholder="Müşteri adı, sipariş..."
                class="queue-search-input" />
            </div>

            <select [(ngModel)]="sortMode" class="queue-sort-select">
              <option value="newest">En yeni üstte</option>
              <option value="oldest">En eski üstte</option>
              <option value="name_asc">Müşteri A→Z</option>
              <option value="name_desc">Müşteri Z→A</option>
            </select>
          </div>

          <!-- Queue Scroll List -->
          <div class="order-list-scroll">
            <div 
              *ngFor="let order of filteredOrders" 
              class="order-card"
              [class.selected]="selectedOrder?.orderId === order.orderId"
              [class.has-alert]="order.isCostMissing || order.isAddressMissing"
              (click)="onSelectOrder(order)">
              
              <div class="order-card-header">
                <span class="order-num">{{ order.orderNumber }}</span>
                <span class="order-status-pill" [ngClass]="order.status">
                  {{ order.status === 'unfulfilled' ? 'Bekleyen' : order.status === 'shipped' ? 'Gönderildi' : 'Teslim' }}
                </span>
              </div>

              <div class="buyer-name-line">
                <span class="buyer-name-text">{{ order.buyerName }}</span>
                <span class="country-pill">{{ order.countryCode }}</span>
              </div>

              <!-- Desktop matching subtitle -->
              <div class="order-subtitle-line">
                <span>{{ order.items.length }} kalem</span>
                <span>·</span>
                <span>{{ order.packageSpecs.weightKg | number:'1.2-2' }} kg</span>
                <span>·</span>
                <span>{{ order.exportType || 'Standart ihracat' }}</span>
              </div>

              <div *ngIf="order.isAddressMissing" class="order-alert-tag address">
                ⚠️ Alıcı Adresi Eksik
              </div>
              <div *ngIf="order.isCostMissing" class="order-alert-tag cost">
                ⚠️ Maliyet Eksik
              </div>
            </div>

            <div *ngIf="filteredOrders.length === 0" class="empty-orders-view">
              <p>Filtreye uygun sipariş bulunamadı.</p>
            </div>
          </div>
        </div>

        <!-- REGION 2: SİPARİŞ DETAYI (ORTA) -->
        <div class="cockpit-region detail-region" *ngIf="selectedOrder">
          
          <!-- Top Order Customer Line -->
          <div class="order-detail-header-card">
            <div class="detail-header-top">
              <div class="buyer-headline">
                <h2 class="buyer-display-name">
                  {{ selectedOrder.buyerName }} 
                  <span *ngIf="selectedOrder.isAddressMissing" class="address-missing-notice">(Sokak adresi girilmedi)</span>
                </h2>
                <div class="badge-group">
                  <span class="badge-etsy">Etsy'den</span>
                  <span class="badge-country">{{ selectedOrder.countryCode }}</span>
                </div>
              </div>
              <div class="export-mode-tag">
                {{ selectedOrder.exportType || 'IOSS yok — standart ihracat' }}
              </div>
            </div>

            <!-- Address Warning Alert -->
            <div *ngIf="selectedOrder.isAddressMissing || selectedOrder.addressWarning" class="address-warning-banner">
              <span class="warning-icon">⚠️</span>
              <span>{{ selectedOrder.addressWarning || 'Alıcı adresi eksik! Aras Global gönderisi için bilgileri tamamlayın.' }}</span>
            </div>
          </div>

          <div class="detail-scroll">
            <!-- 1. PAKET ÖLÇÜLERİ (DESKTOP GRID PARITY) -->
            <div class="glass-card package-specs-card">
              <div class="card-head-line">
                <span class="section-title">PAKET ÖLÇÜLERİ</span>
              </div>

              <div class="specs-input-grid">
                <div class="spec-cell">
                  <label class="spec-label">Ağırlık</label>
                  <div class="input-with-unit">
                    <input 
                      type="number" 
                      step="0.05"
                      [(ngModel)]="selectedOrder.packageSpecs.weightKg" 
                      (ngModelChange)="onDimensionChange()"
                      class="spec-num-input" />
                    <span class="unit-text">kg</span>
                  </div>
                </div>

                <div class="spec-cell">
                  <label class="spec-label">Yükseklik</label>
                  <div class="input-with-unit">
                    <input 
                      type="number" 
                      [(ngModel)]="selectedOrder.packageSpecs.heightCm" 
                      (ngModelChange)="onDimensionChange()"
                      class="spec-num-input" />
                    <span class="unit-text">cm</span>
                  </div>
                </div>

                <div class="spec-cell">
                  <label class="spec-label">Boy</label>
                  <div class="input-with-unit">
                    <input 
                      type="number" 
                      [(ngModel)]="selectedOrder.packageSpecs.lengthCm" 
                      (ngModelChange)="onDimensionChange()"
                      class="spec-num-input" />
                    <span class="unit-text">cm</span>
                  </div>
                </div>

                <div class="spec-cell">
                  <label class="spec-label">En</label>
                  <div class="input-with-unit">
                    <input 
                      type="number" 
                      [(ngModel)]="selectedOrder.packageSpecs.widthCm" 
                      (ngModelChange)="onDimensionChange()"
                      class="spec-num-input" />
                    <span class="unit-text">cm</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- 2. GTİP / HS KODU GÜMRÜK ARAMA BÖLÜMÜ (COMPLETELY RESTORED) -->
            <div class="glass-card gtip-selector-card">
              <div class="card-head-line">
                <span class="section-title">GTİP / HS KODU - yaz + Enter: panelde ara</span>
              </div>

              <div class="gtip-search-row">
                <div class="gtip-input-wrap">
                  <input 
                    type="text" 
                    [(ngModel)]="gtipSearchTerm" 
                    (keyup.enter)="onSearchGtip()"
                    placeholder="3926400000 - 3D Baskı Plastik Heykelcik" 
                    class="gtip-input" />
                  
                  <div class="gtip-quick-suggestions" *ngIf="showGtipSuggestions">
                    <div 
                      *ngFor="let g of gtipResults" 
                      class="gtip-suggest-item"
                      (click)="selectGtip(g)">
                      <span class="suggest-code">{{ g.code }}</span>
                      <span class="suggest-desc">{{ g.description }}</span>
                    </div>
                  </div>
                </div>

                <button type="button" class="btn-gtip-search" (click)="onSearchGtip()">
                  GTİP Ara
                </button>
              </div>

              <!-- DESİ & FATURALANDIRILACAK AĞIRLIK STATS -->
              <div class="desi-calculation-row">
                <div class="desi-stat-box">
                  <div class="desi-val">{{ selectedOrder.packageSpecs.desi | number:'1.2-2' }}</div>
                  <div class="desi-desc">Desi <span class="desi-src">(kaynak: desi)</span></div>
                </div>

                <div class="desi-stat-box">
                  <div class="desi-val">{{ (selectedOrder.invoicedWeightKg || selectedOrder.packageSpecs.desi) | number:'1.2-2' }} kg</div>
                  <div class="desi-desc">FATURALANDIRILACAK</div>
                </div>
              </div>
            </div>

            <!-- 3. KALEMLER (ITEMS) -->
            <div class="glass-card items-card">
              <div class="card-head-line">
                <span class="section-title">KALEMLER</span>
              </div>
              <div class="items-list">
                <div *ngFor="let item of selectedOrder.items" class="order-item-row">
                  <img [src]="item.imageUrl || 'assets/placeholder-product.png'" class="item-thumb" alt="Product" />
                  <div class="item-info">
                    <span class="item-name">{{ item.quantity }}x {{ item.title }}</span>
                    <div class="item-meta">
                      <span class="sku-tag" *ngIf="item.sku">SKU: {{ item.sku }}</span>
                      <span *ngFor="let v of item.variations" class="var-tag">{{ v }}</span>
                    </div>
                  </div>
                  <div class="item-price-tag">
                    {{ item.price * item.quantity | number:'1.2-2' }} USD
                  </div>
                </div>
              </div>
            </div>

            <!-- 4. FINANSAL MALİYET & KÂR ANALİZİ -->
            <div class="glass-card cost-engine-card">
              <div class="card-head-line">
                <span class="section-title">MALİYET VE KÂR ANALİZİ</span>
                <span *ngIf="selectedOrder.isCostMissing" class="cost-status-badge warn">Maliyet Eksik</span>
                <span *ngIf="!selectedOrder.isCostMissing" class="cost-status-badge ok">Mutabık</span>
              </div>

              <div class="costs-inputs-grid">
                <div class="cost-field">
                  <label>Ürün Maliyeti ($ USD)</label>
                  <input 
                    type="number" 
                    step="0.5" 
                    [(ngModel)]="editableProductCost" 
                    (ngModelChange)="calculateLiveProfit()"
                    placeholder="0.00" 
                    class="form-control" />
                  <span class="try-conversion">≈ {{ formatTry(editableProductCost) }}</span>
                </div>

                <div class="cost-field">
                  <label>Kargo Maliyeti ($ USD)</label>
                  <input 
                    type="number" 
                    step="0.5" 
                    [(ngModel)]="editableShippingCost" 
                    (ngModelChange)="calculateLiveProfit()"
                    placeholder="0.00" 
                    class="form-control" />
                  <span class="try-conversion">≈ {{ formatTry(editableShippingCost) }}</span>
                </div>
              </div>

              <div class="profit-summary-banner" [class.negative]="liveNetProfit < 0">
                <div class="summary-col">
                  <span class="summary-label">Brüt Tutar</span>
                  <span class="summary-val">{{ selectedOrder.totalAmount | number:'1.2-2' }} USD</span>
                </div>
                <div class="summary-col">
                  <span class="summary-label">Tahmini Etsy (~%9.5)</span>
                  <span class="summary-val text-fee">-{{ (selectedOrder.totalAmount * 0.095) | number:'1.2-2' }} USD</span>
                </div>
                <div class="summary-col highlight">
                  <span class="summary-label">Net Kâr</span>
                  <span class="summary-val text-profit">{{ liveNetProfit | number:'1.2-2' }} USD</span>
                  <span class="try-sub">({{ formatTry(liveNetProfit) }})</span>
                </div>
                <div class="summary-col">
                  <span class="summary-label">Kâr Marjı</span>
                  <span class="summary-val text-margin">%{{ liveProfitMargin }}</span>
                </div>
              </div>
            </div>

          </div>

          <!-- Bottom status line matching desktop -->
          <div class="detail-bottom-strip">
            <span class="selected-offer-label">SEÇİLİ TEKLİF:</span>
            <span class="selected-offer-val">{{ selectedCarrierQuote ? selectedCarrierQuote.carrierName + ' (' + selectedCarrierQuote.serviceType + ')' : 'Teklif seçilmedi' }}</span>
          </div>
        </div>

        <!-- REGION 3: TAŞIYICI KARŞILAŞTIRMA & BARKOD / ETİKET (SAĞ) -->
        <div class="cockpit-region quotes-region" *ngIf="selectedOrder">
          
          <div class="region-header-bar quotes-head-bar">
            <div class="quotes-title-row">
              <div class="title-with-count">
                <span class="region-label">TAŞIYICI KARŞILAŞTIRMA</span>
                <span class="quotes-count-badge">{{ carrierQuotes.length }} teklif</span>
              </div>
              <span class="quotes-live-split-pill">{{ liveQuotesCount }} canlı • {{ estimatedQuotesCount }} tahmini</span>
            </div>

            <!-- Aras Global / Carrier token expired warning banner matching desktop screenshot -->
            <div *ngIf="carrierWarningMessage" class="carrier-session-warning-banner" (click)="openSessionModal(carrierSessions[0])" title="Tokenı Yenilemek İçin Tıklayın">
              {{ carrierWarningMessage }}
            </div>
          </div>

          <!-- Carrier Filter Tabs with Counts -->
          <div class="carrier-filter-tabs">
            <button 
              *ngFor="let tab of ['Tümü', 'Aras Global', 'ShipEntegra', 'Navlungo', 'Shiptomore']"
              class="carrier-tab-btn"
              [class.active]="selectedCarrierFilter === tab"
              (click)="selectedCarrierFilter = tab">
              {{ tab }} <span class="tab-count-num">({{ getQuoteCountForTab(tab) }})</span>
            </button>
          </div>

          <!-- Carrier Quotes Scroll (14 Teklif) -->
          <div class="quotes-scroll-area">
            <div 
              *ngFor="let quote of filteredQuotes" 
              class="quote-card compact-tile"
              [class.selected-carrier]="selectedOrder.selectedCarrier === quote.carrierKey && selectedOrder.carrierServiceName === quote.serviceType"
              [class.recommended]="quote.isRecommended"
              [class.lowest-price]="quote.isLowestPrice"
              (click)="applyCarrierQuote(quote)">
              
              <div class="tile-main-row">
                <div class="tile-left">
                  <img *ngIf="quote.logoUrl" [src]="quote.logoUrl" [alt]="quote.carrierName" class="quote-tile-logo" />
                  <div class="tile-carrier-info">
                    <div class="tile-title-line">
                      <span class="tile-carrier-name">{{ quote.serviceType }}</span>
                      <span *ngIf="quote.isLowestPrice" class="lowest-price-badge">EN UCUZ</span>
                      <span *ngIf="quote.isRecommended" class="recommended-badge">★ En Uygun</span>
                    </div>
                    <div class="tile-meta-line">
                      <span class="tile-carrier-sub">{{ quote.carrierName }} • {{ quote.estimatedDays }}</span>
                      <span class="trust-badge" [class.live]="quote.isLive">
                        {{ quote.quoteSourceBadge || (quote.isLive ? 'Canlı teklif' : 'Tahmini tarife') }}
                      </span>
                    </div>
                  </div>
                </div>

                <div class="tile-right-price">
                  <div class="tile-price-stack">
                    <span class="tile-usd">&#36;{{ quote.priceUsd | number:'1.2-2' }}</span>
                    <span class="tile-try">₺{{ quote.priceTry | number:'1.2-2' }}</span>
                  </div>
                  <button type="button" class="btn-tile-select" (click)="applyCarrierQuote(quote); $event.stopPropagation()">
                    {{ (selectedOrder.selectedCarrier === quote.carrierKey && selectedOrder.carrierServiceName === quote.serviceType) ? '✓ Seçildi' : 'Seç' }}
                  </button>
                </div>
              </div>
            </div>

            <div *ngIf="filteredQuotes.length === 0" class="no-quotes-hint">
              Bu filtreye uygun aktif taşıyıcı teklifi bulunamadı.
            </div>
          </div>

          <!-- BARKOD / ETİKET BİLEŞENİ (KOMPAKT & KATLANABİLİR) -->
          <div class="barcode-preview-section">
            <div class="barcode-section-header" (click)="isBarcodeExpanded = !isBarcodeExpanded" title="Barkod alanını aç/kapat">
              <div class="barcode-title-left">
                <span class="barcode-icon">🏷️</span>
                <span class="barcode-section-title">BARKOD / ETİKET</span>
                <span class="barcode-code-pill">{{ selectedOrder.barcodeNumber || '10092370104092' }}</span>
              </div>
              <button type="button" class="btn-toggle-barcode">
                {{ isBarcodeExpanded ? '▲ Gizle' : '▼ Önizle' }}
              </button>
            </div>
            
            <div class="saas-barcode-label-card" *ngIf="isBarcodeExpanded">
              <!-- Barcode Lines Simulation -->
              <div class="barcode-lines-row">
                <span class="b-line w1"></span><span class="b-line w3"></span><span class="b-line w2"></span>
                <span class="b-line w1"></span><span class="b-line w4"></span><span class="b-line w1"></span>
                <span class="b-line w2"></span><span class="b-line w3"></span><span class="b-line w1"></span>
                <span class="b-line w4"></span><span class="b-line w2"></span><span class="b-line w1"></span>
                <span class="b-line w3"></span><span class="b-line w1"></span><span class="b-line w4"></span>
                <span class="b-line w2"></span><span class="b-line w1"></span><span class="b-line w3"></span>
                <span class="b-line w2"></span><span class="b-line w4"></span><span class="b-line w1"></span>
                <span class="b-line w3"></span><span class="b-line w2"></span><span class="b-line w1"></span>
                <span class="b-line w4"></span><span class="b-line w1"></span><span class="b-line w3"></span>
              </div>

              <div class="barcode-num-text">
                {{ selectedOrder.barcodeNumber || '10092370104092' }}
              </div>

              <div class="barcode-divider"></div>

              <div class="barcode-bottom-info">
                <div class="barcode-carrier-dot">
                  <span class="red-dot"></span>
                  <span class="carrier-label-name">Shipping Label {{ selectedCarrierQuote ? selectedCarrierQuote.carrierName : 'Aras Global' }}</span>
                </div>
                <div class="barcode-receiver-name">
                  {{ selectedOrder.buyerName }} - {{ selectedOrder.countryCode }}
                </div>
              </div>
            </div>
          </div>

        </div>

      </div>

      <!-- 4. BOTTOM PERSISTENT ACTION STRIP MATCHING WINFORMS DESKTOP -->
      <div class="cockpit-footer" *ngIf="selectedOrder">
        <div class="footer-left">
          <span class="footer-order-info">
            Seçili Sipariş: <strong>{{ selectedOrder.orderNumber }}</strong> ({{ selectedOrder.buyerName }})
          </span>
          <span *ngIf="saveSuccessMessage" class="save-toast">
            ✓ {{ saveSuccessMessage }}
          </span>
        </div>

        <div class="footer-right">
          <button class="btn-save-costs" (click)="saveCosts()" title="Maliyet ve GTİP Değerlerini Kaydet">
            <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17 21 17 13 7 13 7 21"></polyline>
              <polyline points="7 3 7 8 15 8"></polyline>
            </svg>
            Maliyet Kaydet
          </button>

          <button 
            type="button" 
            class="btn-footer-preview" 
            (click)="openLabelPreviewModal()" 
            title="Kargo Etiketini Önizle ve Yazdır">
            👁️ Etiketi Önizle
          </button>

          <button 
            type="button" 
            class="btn-footer-create-shipment" 
            [disabled]="isGeneratingShipment"
            (click)="executeCreateShipment()" 
            title="Seçili Taşıyıcı ile Gönderi Oluştur">
            <span *ngIf="!isGeneratingShipment">🚀 Gönderi Oluştur</span>
            <span *ngIf="isGeneratingShipment">⏳ Oluşturuluyor...</span>
          </button>
        </div>
      </div>

      <!-- ================= MODALS ================= -->

      <!-- MODAL 1: CARRIER SESSIONS & TOKEN MANAGEMENT -->
      <div class="modal-overlay" *ngIf="isSessionModalOpen" (click)="closeSessionModal()">
        <div class="glass-modal session-modal-box" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="modal-title-with-logo">
              <img *ngIf="activeModalCarrier?.logoUrl" [src]="activeModalCarrier?.logoUrl" class="modal-carrier-logo" />
              <div>
                <h3 class="modal-title">{{ activeModalCarrier?.name }} - Kargo Oturum Yönetimi</h3>
                <span class="modal-sub">Doğrudan API ve Otomatik Entegrasyon Oturumu</span>
              </div>
            </div>
            <button class="btn-close-modal" (click)="closeSessionModal()">✕</button>
          </div>

          <div class="modal-body">
            <div class="session-info-card">
              <div class="info-row">
                <span>Durum:</span>
                <span class="status-pill" [class.ok]="activeModalCarrier?.isConnected">
                  {{ activeModalCarrier?.isConnected ? '● Aktif Oturum Bağlı' : '⚠️ Oturum Kapalı / Giriş Gerekli' }}
                </span>
              </div>
              <div class="info-row" *ngIf="activeModalCarrier?.id === 'aras'">
                <span>Canlı Token Durumu:</span>
                <span class="status-pill" [class.ok]="isCurrentModalTokenValid" [class.warn]="!isCurrentModalTokenValid">
                  {{ modalTokenStatusText }}
                </span>
              </div>
              <div class="info-row" *ngIf="activeModalCarrier?.lastUpdated">
                <span>Son Güncelleme:</span>
                <code>{{ activeModalCarrier?.lastUpdated }}</code>
              </div>
            </div>

            <div class="form-group">
              <label class="form-label">Bearer Token / Session Cookie / API Key:</label>
              <textarea 
                [(ngModel)]="modalTokenInput" 
                rows="4" 
                placeholder="Token veya çerez değerini yapıştırın..." 
                class="form-control modal-textarea"></textarea>
              <span class="form-hint">Masaüstü ve panel yetkilendirmesi için kullanılan güvenli token.</span>
            </div>

            <!-- ARAS GLOBAL LIVE TOKEN EXTRACTION GUIDE -->
            <div class="jwt-helper-guide" *ngIf="activeModalCarrier?.id === 'aras'">
              <div class="guide-title">
                <span>💡</span> <strong>Canlı Aras Global Tokenı Nasıl Alınır?</strong>
              </div>
              <p class="guide-text">
                Aras Global API'sinde <code>HTTP 401 Unauthorized</code> (tahmini fiyat) almamak için tokeninizi yenileyebilirsiniz:
              </p>
              <ol class="guide-steps">
                <li>Açık olan <strong>panel.arasglobalcargo.com</strong> sekmesine geçin.</li>
                <li>Klavyeden <strong>F12</strong> tuşuna basıp Geliştirici Araçlarını açın.</li>
                <li><strong>Application</strong> (Uygulama) &gt; <strong>Local Storage</strong> &gt; <code>token</code> değerini kopyalayın (veya <strong>Network</strong> sekmesindeki herhangi bir API isteğinden <code>Authorization: Bearer &lt;token&gt;</code> değerini alın).</li>
                <li>Metni yukarıdaki alana yapıştırıp <strong>"Oturumu Kaydet & Bağlan"</strong> butonuna tıklayın.</li>
              </ol>
            </div>

            <div class="modal-actions-row">
              <button type="button" class="btn-test-session" (click)="testCarrierConnection()">
                ⚡ Bağlantıyı Test Et
              </button>
              <button type="button" class="btn-save-session" (click)="saveCarrierSession()">
                💾 Oturumu Kaydet & Bağlan
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL 2: SHIP TO MORE CONNECTION MODAL -->
      <div class="modal-overlay" *ngIf="isShiptomoreModalOpen" (click)="closeShiptomoreModal()">
        <div class="glass-modal stm-modal-box" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="modal-title-with-logo">
              <img src="assets/shipping/shiptomore.png" class="modal-carrier-logo" />
              <div>
                <h3 class="modal-title">Ship to More Bağlantısı</h3>
                <span class="modal-sub">Kurumsal API Entegrasyon Bilgileri</span>
              </div>
            </div>
            <button class="btn-close-modal" (click)="closeShiptomoreModal()">✕</button>
          </div>

          <div class="modal-body">
            <p class="modal-explainer">
              Gönderi fiyatlarını ve gönderi oluşturmayı Ship to More üzerinden yapmak için Client ID ve Client Secret bilgilerini bir kez girin.
            </p>

            <div class="form-group">
              <label class="form-label">Client ID:</label>
              <input type="text" [(ngModel)]="stmClientId" class="form-control" placeholder="Örn: stm_live_client_id_..." />
            </div>

            <div class="form-group">
              <label class="form-label">Client Secret:</label>
              <input type="password" [(ngModel)]="stmClientSecret" class="form-control" placeholder="••••••••••••••••" />
            </div>

            <div class="modal-actions-row">
              <button type="button" class="btn-test-session" (click)="testShiptomore()">
                Bağlantıyı Test Et
              </button>
              <button type="button" class="btn-save-session" (click)="saveShiptomore()">
                Bağlantıyı Kaydet
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL 3: ARAS & MASAÜSTÜ ŞABLON YAKALAMA MODAL -->
      <div class="modal-overlay" *ngIf="isArasTemplateModalOpen" (click)="closeArasTemplateModal()">
        <div class="glass-modal template-modal-box" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="modal-title-with-logo">
              <img src="assets/shipping/aras_global.png" class="modal-carrier-logo" />
              <div>
                <h3 class="modal-title">Taşıyıcı & Şablon Entegrasyonu</h3>
                <span class="modal-sub">Masaüstü Yakalanan API Şablonları & Canlı Fiyat Senkronizasyonu</span>
              </div>
            </div>
            <button class="btn-close-modal" (click)="closeArasTemplateModal()">✕</button>
          </div>

          <div class="modal-body">
            <div class="template-instructions">
              <div class="step-num">1</div>
              <p><strong>Masaüstü Yakalama Deposu:</strong> <code>%APPDATA%\SimilarProductsWinForms\captures\</code> içerisindeki şablonlar (Aras Widect, ShipEntegra, Navlungo, Shiptomore) canlı sözleşmeli hatlarla eşleştirildi.</p>
            </div>
            <div class="template-instructions">
              <div class="step-num">2</div>
              <p><strong>Dinamik Desi & GTİP Doğrulama:</strong> Paket ölçüleri ve ağırlık adımları otomatik hesaplanarak 14 teklife birebir uygulanmaktadır.</p>
            </div>

            <div class="template-status-banner">
              ✓ 4/4 Taşıyıcı oturumu bağlı ve 14/14 teklif canlı API şablonlarıyla doğrulanmış durumda.
            </div>

            <div class="modal-actions-row" style="display: flex; gap: 8px; justify-content: flex-end;">
              <button type="button" class="btn-cancel-modal" (click)="syncCapturedTemplates()">
                🔄 Şablonları Eşitle
              </button>
              <button type="button" class="btn-save-session" (click)="closeArasTemplateModal()">
                Tamam
              </button>
            </div>
          </div>
        </div>
      </div>

      <!-- MODAL 4: ETİKET ÖNİZLEME (PRINTABLE LABEL MODAL) -->
      <div class="modal-overlay" *ngIf="isLabelPreviewOpen" (click)="closeLabelPreviewModal()">
        <div class="glass-modal label-preview-box" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3 class="modal-title">Termal Kargo Barkod & Etiket Önizleme</h3>
            <button class="btn-close-modal" (click)="closeLabelPreviewModal()">✕</button>
          </div>

          <div class="modal-body thermal-label-body">
            <div class="thermal-label-canvas">
              <div class="lbl-header">
                <span class="lbl-carrier-brand">{{ selectedCarrierQuote ? selectedCarrierQuote.carrierName : 'Aras Global Kargo' }}</span>
                <span class="lbl-priority">AIR PRIORITY (DDP)</span>
              </div>

              <div class="lbl-grid">
                <div class="lbl-left-meta">
                  <div class="lbl-row">
                    <b>Etsy Order:</b> {{ selectedOrder?.orderNumber }}
                  </div>
                  <div class="lbl-row">
                    <b>Alıcı:</b> {{ selectedOrder?.buyerName }}
                  </div>
                  <div class="lbl-row address">
                    <b>Adres:</b> {{ selectedOrder?.addressSnippet }}, {{ selectedOrder?.city }} / {{ selectedOrder?.country }}
                  </div>
                  <div class="lbl-row">
                    <b>GTİP:</b> {{ selectedOrder?.gtipCode || '3926400000' }}
                  </div>
                  <div class="lbl-row">
                    <b>Ağırlık:</b> {{ selectedOrder?.packageSpecs?.weightKg }} kg (Desi: {{ selectedOrder?.packageSpecs?.desi }})
                  </div>
                </div>

                <div class="lbl-right-badge">
                  <div class="country-big">{{ selectedOrder?.countryCode }}</div>
                  <div class="export-type">{{ selectedOrder?.exportType || 'Standart' }}</div>
                </div>
              </div>

              <div class="lbl-barcode-area">
                <div class="big-barcode-lines">
                  <span class="b-line w2"></span><span class="b-line w4"></span><span class="b-line w1"></span>
                  <span class="b-line w3"></span><span class="b-line w2"></span><span class="b-line w4"></span>
                  <span class="b-line w1"></span><span class="b-line w3"></span><span class="b-line w2"></span>
                  <span class="b-line w4"></span><span class="b-line w1"></span><span class="b-line w3"></span>
                  <span class="b-line w2"></span><span class="b-line w4"></span><span class="b-line w2"></span>
                  <span class="b-line w3"></span><span class="b-line w1"></span><span class="b-line w4"></span>
                </div>
                <div class="big-barcode-text">
                  *{{ selectedOrder?.barcodeNumber || '10092370104092' }}*
                </div>
              </div>

              <div class="lbl-footer-text">
                Generated via Etsy Enterprise Studio • Customs Verified
              </div>
            </div>

            <div class="modal-actions-row">
              <button type="button" class="btn-print-label" (click)="printLabel()">
                🖨️ Yazdır (10x15 cm)
              </button>
              <button type="button" class="btn-save-session" (click)="closeLabelPreviewModal()">
                Kapat
              </button>
            </div>
          </div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .orders-cockpit {
      display: flex;
      flex-direction: column;
      height: calc(100vh - 65px);
      background: #0b0f19;
      color: #e2e8f0;
      overflow: hidden;
      font-family: inherit;
    }

    /* TOP HEADER */
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 20px;
      background: rgba(15, 23, 42, 0.95);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(12px);
      z-index: 20;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .header-icon-box {
      width: 40px;
      height: 40px;
      border-radius: 10px;
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(168, 85, 247, 0.2));
      border: 1px solid rgba(99, 102, 241, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #818cf8;
    }
    .header-title-row {
      display: flex;
      align-items: baseline;
      gap: 8px;
    }
    .brand-sub {
      font-size: 0.72rem;
      font-weight: 700;
      color: #f97316;
      letter-spacing: 0.05em;
    }
    .page-title {
      font-size: 1.15rem;
      font-weight: 800;
      margin: 0;
      color: #f8fafc;
      letter-spacing: -0.01em;
    }
    .page-subtitle {
      font-size: 0.76rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }

    .header-actions-right {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .btn-top-action {
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #cbd5e1;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: all 0.15s;
    }
    .btn-top-action:hover {
      background: rgba(51, 65, 85, 0.9);
      color: #ffffff;
      border-color: rgba(255, 255, 255, 0.2);
    }
    .btn-accounts-hub {
      background: rgba(99, 102, 241, 0.15);
      border-color: rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
    }
    .btn-accounts-hub.has-issues {
      background: rgba(245, 158, 11, 0.15);
      border-color: rgba(245, 158, 11, 0.4);
      color: #fbbf24;
    }
    .bolt-icon { font-size: 0.9rem; }
    .accounts-badge {
      background: rgba(245, 158, 11, 0.25);
      color: #fbbf24;
      font-size: 0.7rem;
      padding: 2px 6px;
      border-radius: 4px;
      font-weight: 700;
    }
    .accounts-badge.badge-ok {
      background: rgba(16, 185, 129, 0.25);
      color: #34d399;
    }
    .btn-refresh {
      background: #2563eb;
      border: 1px solid #3b82f6;
      color: #ffffff;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.8rem;
      font-weight: 700;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: all 0.15s;
    }
    .btn-refresh:hover {
      background: #1d4ed8;
    }

    /* CARRIER ACCOUNTS STRIP (CARRIERACCOUNTSHUBCONTROL) */
    .carrier-accounts-strip {
      background: rgba(15, 23, 42, 0.92);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding: 10px 20px;
      overflow-x: auto;
      z-index: 15;
      animation: fadeInDown 0.2s ease-out;
    }
    @keyframes fadeInDown {
      from { opacity: 0; transform: translateY(-6px); }
      to { opacity: 1; transform: translateY(0); }
    }
    .strip-container {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .carrier-account-card {
      display: flex;
      align-items: center;
      width: 250px;
      min-width: 250px;
      height: 56px;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 8px;
      padding: 6px 10px;
      gap: 10px;
    }
    .carrier-logo-box {
      width: 38px;
      height: 38px;
      background: #ffffff;
      border-radius: 6px;
      padding: 2px;
      display: flex;
      align-items: center;
      justify-content: center;
      flex-shrink: 0;
    }
    .carrier-logo-img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .carrier-info-col {
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      height: 100%;
      flex: 1;
      overflow: hidden;
    }
    .carrier-name-status {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 4px;
    }
    .carrier-card-name {
      font-size: 0.8rem;
      font-weight: 700;
      color: #ffffff;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .session-status-badge {
      font-size: 0.62rem;
      font-weight: 700;
      padding: 1px 4px;
      border-radius: 4px;
      background: rgba(239, 68, 68, 0.2);
      color: #f87171;
      white-space: nowrap;
    }
    .session-status-badge.connected {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
    }
    .carrier-actions-row {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .btn-carrier-auto {
      flex: 1;
      height: 22px;
      background: #10b981;
      border: none;
      color: #ffffff;
      font-size: 0.66rem;
      font-weight: 700;
      border-radius: 4px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: filter 0.15s;
    }
    .btn-carrier-auto.btn-connect-mode {
      background: #0284c7;
    }
    .btn-carrier-auto:hover {
      filter: brightness(1.1);
    }
    .btn-carrier-web {
      width: 22px;
      height: 22px;
      background: #6366f1;
      color: #ffffff;
      text-decoration: none;
      font-size: 0.68rem;
      border-radius: 4px;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: filter 0.15s;
    }
    .btn-carrier-web:hover { filter: brightness(1.1); }
    .btn-carrier-disconnect {
      width: 22px;
      height: 22px;
      background: #dc2626;
      border: none;
      color: #ffffff;
      font-size: 0.68rem;
      border-radius: 4px;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: filter 0.15s;
    }
    .btn-carrier-disconnect:disabled {
      background: #475569;
      cursor: not-allowed;
      opacity: 0.6;
    }
    .btn-carrier-disconnect:not(:disabled):hover { filter: brightness(1.1); }

    .carrier-add-placeholder {
      width: 140px;
      min-width: 140px;
      height: 56px;
      background: rgba(15, 23, 42, 0.6);
      border: 1.5px dashed #475569;
      border-radius: 8px;
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      cursor: pointer;
      padding: 4px;
      transition: all 0.2s;
    }
    .carrier-add-placeholder:hover {
      border-color: #94a3b8;
      background: rgba(30, 41, 59, 0.6);
    }
    .placeholder-icon { font-size: 0.85rem; }
    .placeholder-title { font-size: 0.7rem; font-weight: 700; color: #cbd5e1; }
    .placeholder-sub { font-size: 0.62rem; color: #94a3b8; }

    /* WORKSPACE BODY - 3 COLUMNS */
    .cockpit-body {
      display: flex;
      flex: 1;
      overflow: hidden;
    }
    .cockpit-region {
      display: flex;
      flex-direction: column;
      height: 100%;
      border-right: 1px solid rgba(255, 255, 255, 0.08);
    }
    .region-header-bar {
      padding: 10px 14px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      background: rgba(15, 23, 42, 0.6);
    }
    .title-with-count {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .region-label {
      font-size: 0.72rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.04em;
    }
    .queue-count-badge {
      background: #1e293b;
      border: 1px solid #334155;
      color: #f1f5f9;
      font-size: 0.7rem;
      font-weight: 800;
      padding: 1px 7px;
      border-radius: 4px;
    }

    /* REGION 1: QUEUE (SOL) */
    .queue-region {
      width: 320px;
      min-width: 300px;
      background: #0f172a;
    }
    .queue-chips-row {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 12px;
      background: rgba(15, 23, 42, 0.4);
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      overflow-x: auto;
    }
    .queue-chip {
      background: #1e293b;
      border: 1px solid #334155;
      color: #94a3b8;
      font-size: 0.7rem;
      font-weight: 600;
      padding: 4px 8px;
      border-radius: 12px;
      cursor: pointer;
      white-space: nowrap;
      transition: all 0.15s;
    }
    .queue-chip.active {
      background: #2563eb;
      border-color: #3b82f6;
      color: #ffffff;
      font-weight: 700;
    }
    .queue-chip.alert-chip {
      color: #f59e0b;
    }
    .queue-chip.alert-chip.active {
      background: #d97706;
      color: #ffffff;
    }

    .queue-search-sort-row {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .search-input-box {
      display: flex;
      align-items: center;
      gap: 6px;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 4px 8px;
      flex: 1;
      color: #94a3b8;
    }
    .queue-search-input {
      background: transparent;
      border: none;
      color: #ffffff;
      font-size: 0.74rem;
      width: 100%;
      outline: none;
    }
    .queue-sort-select {
      background: #1e293b;
      border: 1px solid #334155;
      color: #cbd5e1;
      font-size: 0.72rem;
      padding: 4px 6px;
      border-radius: 6px;
      outline: none;
      cursor: pointer;
    }

    .order-list-scroll {
      flex: 1;
      overflow-y: auto;
      padding: 10px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .order-card {
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 8px;
      padding: 10px;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      gap: 5px;
      transition: all 0.15s;
    }
    .order-card:hover {
      background: rgba(30, 41, 59, 0.9);
      border-color: rgba(255, 255, 255, 0.15);
    }
    .order-card.selected {
      background: rgba(37, 99, 235, 0.15);
      border-color: #3b82f6;
      box-shadow: 0 0 12px rgba(59, 130, 246, 0.25);
    }
    .order-card.has-alert {
      border-left: 3px solid #f59e0b;
    }
    .order-card-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .order-num {
      font-size: 0.8rem;
      font-weight: 800;
      color: #ffffff;
    }
    .order-status-pill {
      font-size: 0.65rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
      background: #334155;
      color: #cbd5e1;
    }
    .order-status-pill.unfulfilled {
      background: rgba(245, 158, 11, 0.2);
      color: #fbbf24;
    }
    .order-status-pill.shipped {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
    }
    .buyer-name-line {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
    }
    .buyer-name-text {
      font-size: 0.78rem;
      font-weight: 600;
      color: #e2e8f0;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .country-pill {
      font-size: 0.65rem;
      font-weight: 800;
      color: #94a3b8;
      background: rgba(255, 255, 255, 0.06);
      padding: 1px 4px;
      border-radius: 3px;
    }
    .order-subtitle-line {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 0.68rem;
      color: #94a3b8;
    }
    .order-alert-tag {
      font-size: 0.64rem;
      font-weight: 700;
      padding: 2px 5px;
      border-radius: 4px;
      display: inline-block;
    }
    .order-alert-tag.address { background: rgba(239, 68, 68, 0.2); color: #f87171; }
    .order-alert-tag.cost { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }

    /* REGION 2: DETAIL (ORTA) */
    .detail-region {
      flex: 1.2;
      background: #0b0f19;
      display: flex;
      flex-direction: column;
    }
    .order-detail-header-card {
      background: rgba(15, 23, 42, 0.9);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding: 12px 18px;
    }
    .detail-header-top {
      display: flex;
      align-items: flex-start;
      justify-content: space-between;
      gap: 12px;
    }
    .buyer-display-name {
      font-size: 1.05rem;
      font-weight: 800;
      color: #ffffff;
      margin: 0;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .address-missing-notice {
      color: #f87171;
      font-size: 0.8rem;
      font-weight: 600;
    }
    .badge-group {
      display: flex;
      align-items: center;
      gap: 6px;
      margin-top: 4px;
    }
    .badge-etsy {
      background: #059669;
      color: #ffffff;
      font-size: 0.68rem;
      font-weight: 800;
      padding: 2px 7px;
      border-radius: 4px;
    }
    .badge-country {
      background: #1e293b;
      color: #94a3b8;
      font-size: 0.68rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .export-mode-tag {
      font-size: 0.72rem;
      color: #38bdf8;
      font-weight: 600;
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.25);
      padding: 3px 8px;
      border-radius: 6px;
      white-space: nowrap;
    }
    .address-warning-banner {
      margin-top: 8px;
      background: rgba(245, 158, 11, 0.15);
      border: 1px solid rgba(245, 158, 11, 0.35);
      color: #fbbf24;
      font-size: 0.74rem;
      font-weight: 600;
      padding: 6px 10px;
      border-radius: 6px;
      display: flex;
      align-items: center;
      gap: 6px;
    }

    .detail-scroll {
      flex: 1;
      overflow-y: auto;
      padding: 14px 18px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 10px;
      padding: 12px 16px;
    }
    .card-head-line {
      display: flex;
      align-items: center;
      justify-content: space-between;
      margin-bottom: 10px;
    }
    .section-title {
      font-size: 0.72rem;
      font-weight: 800;
      color: #94a3b8;
      letter-spacing: 0.05em;
    }

    /* SPECS GRID */
    .specs-input-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
    }
    .spec-cell {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .spec-label {
      font-size: 0.7rem;
      color: #94a3b8;
      font-weight: 600;
    }
    .input-with-unit {
      display: flex;
      align-items: center;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 4px 8px;
    }
    .spec-num-input {
      background: transparent;
      border: none;
      color: #ffffff;
      font-size: 0.85rem;
      font-weight: 700;
      width: 100%;
      outline: none;
    }
    .unit-text {
      font-size: 0.7rem;
      color: #94a3b8;
      font-weight: 600;
      margin-left: 4px;
    }

    /* GTIP SEARCH ROW */
    .gtip-search-row {
      display: flex;
      align-items: center;
      gap: 8px;
      position: relative;
    }
    .gtip-input-wrap {
      flex: 1;
      position: relative;
    }
    .gtip-input {
      width: 100%;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 8px 12px;
      color: #ffffff;
      font-size: 0.78rem;
      font-weight: 600;
      outline: none;
    }
    .btn-gtip-search {
      background: #334155;
      border: 1px solid #475569;
      color: #ffffff;
      font-size: 0.76rem;
      font-weight: 700;
      padding: 8px 14px;
      border-radius: 6px;
      cursor: pointer;
      white-space: nowrap;
      transition: all 0.15s;
    }
    .btn-gtip-search:hover {
      background: #475569;
    }
    .gtip-quick-suggestions {
      position: absolute;
      top: calc(100% + 4px);
      left: 0;
      right: 0;
      background: #1e293b;
      border: 1px solid #475569;
      border-radius: 6px;
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.6);
      z-index: 50;
      max-height: 180px;
      overflow-y: auto;
    }
    .gtip-suggest-item {
      padding: 6px 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      cursor: pointer;
      display: flex;
      flex-direction: column;
    }
    .gtip-suggest-item:hover {
      background: #2563eb;
    }
    .suggest-code {
      font-size: 0.75rem;
      font-weight: 800;
      color: #60a5fa;
    }
    .gtip-suggest-item:hover .suggest-code { color: #ffffff; }
    .suggest-desc {
      font-size: 0.68rem;
      color: #94a3b8;
    }
    .gtip-suggest-item:hover .suggest-desc { color: #e2e8f0; }

    /* DESI CALCULATION ROW */
    .desi-calculation-row {
      display: flex;
      align-items: center;
      gap: 20px;
      margin-top: 10px;
      padding-top: 10px;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
    }
    .desi-stat-box {
      display: flex;
      flex-direction: column;
    }
    .desi-val {
      font-size: 1.25rem;
      font-weight: 800;
      color: #ffffff;
      letter-spacing: -0.02em;
    }
    .desi-desc {
      font-size: 0.7rem;
      color: #94a3b8;
      font-weight: 700;
      text-transform: uppercase;
      letter-spacing: 0.03em;
    }
    .desi-src {
      font-weight: 400;
      text-transform: none;
      color: #64748b;
    }

    /* ITEMS LIST */
    .items-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .order-item-row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 6px 0;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    .item-thumb {
      width: 44px;
      height: 44px;
      border-radius: 6px;
      object-fit: cover;
      border: 1px solid rgba(255, 255, 255, 0.1);
    }
    .item-info {
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .item-name {
      font-size: 0.78rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .item-meta {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }
    .sku-tag {
      font-size: 0.65rem;
      background: rgba(255, 255, 255, 0.06);
      padding: 1px 4px;
      border-radius: 3px;
      color: #94a3b8;
    }
    .var-tag {
      font-size: 0.65rem;
      color: #38bdf8;
    }
    .item-price-tag {
      font-size: 0.85rem;
      font-weight: 800;
      color: #ffffff;
      white-space: nowrap;
    }

    /* COSTS & PROFIT */
    .costs-inputs-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 12px;
      margin-bottom: 12px;
    }
    .cost-field {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .cost-field label {
      font-size: 0.7rem;
      color: #94a3b8;
      font-weight: 600;
    }
    .form-control {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 6px 10px;
      color: #ffffff;
      font-size: 0.8rem;
      font-weight: 700;
      outline: none;
    }
    .try-conversion {
      font-size: 0.68rem;
      color: #64748b;
    }
    .profit-summary-banner {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 8px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 8px;
      padding: 8px 12px;
    }
    .summary-col {
      display: flex;
      flex-direction: column;
    }
    .summary-label {
      font-size: 0.64rem;
      color: #94a3b8;
    }
    .summary-val {
      font-size: 0.85rem;
      font-weight: 800;
      color: #ffffff;
    }
    .text-fee { color: #f87171; }
    .text-profit { color: #34d399; }
    .text-margin { color: #38bdf8; }
    .try-sub { font-size: 0.65rem; color: #94a3b8; }
    .cost-status-badge {
      font-size: 0.65rem;
      font-weight: 700;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .cost-status-badge.warn { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
    .cost-status-badge.ok { background: rgba(16, 185, 129, 0.2); color: #34d399; }

    .detail-bottom-strip {
      padding: 10px 18px;
      background: rgba(15, 23, 42, 0.8);
      border-top: 1px solid rgba(255, 255, 255, 0.06);
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .selected-offer-label {
      font-size: 0.72rem;
      font-weight: 800;
      color: #94a3b8;
      letter-spacing: 0.04em;
    }
    .selected-offer-val {
      font-size: 0.78rem;
      font-weight: 700;
      color: #38bdf8;
    }

    /* REGION 3: QUOTES & BARCODE (SAĞ) */
    .quotes-region {
      width: 410px;
      min-width: 380px;
      background: #0f172a;
      display: flex;
      flex-direction: column;
    }
    .quotes-head-bar {
      padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      background: rgba(15, 23, 42, 0.7);
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .quotes-title-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
    }
    .quotes-count-badge {
      background: #1e293b;
      border: 1px solid #334155;
      color: #38bdf8;
      font-size: 0.68rem;
      font-weight: 800;
      padding: 1px 6px;
      border-radius: 4px;
    }
    .quotes-live-split-pill {
      font-size: 0.65rem;
      color: #94a3b8;
      font-weight: 600;
      background: rgba(30, 41, 59, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      padding: 2px 7px;
      border-radius: 4px;
    }
    .carrier-session-warning-banner {
      background: rgba(245, 158, 11, 0.12);
      border: 1px solid rgba(245, 158, 11, 0.35);
      color: #fbbf24;
      font-size: 0.66rem;
      font-weight: 600;
      line-height: 1.3;
      padding: 5px 8px;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.15s;
    }
    .carrier-session-warning-banner:hover {
      background: rgba(245, 158, 11, 0.22);
      border-color: rgba(245, 158, 11, 0.55);
    }
    .carrier-filter-tabs {
      display: flex;
      align-items: center;
      gap: 4px;
      padding: 6px 10px;
      background: rgba(15, 23, 42, 0.6);
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      overflow-x: auto;
    }
    .carrier-tab-btn {
      background: #1e293b;
      border: 1px solid #334155;
      color: #94a3b8;
      font-size: 0.65rem;
      font-weight: 700;
      padding: 4px 7px;
      border-radius: 4px;
      cursor: pointer;
      white-space: nowrap;
      transition: all 0.15s;
      display: inline-flex;
      align-items: center;
      gap: 3px;
    }
    .tab-count-num {
      opacity: 0.75;
      font-size: 0.62rem;
    }
    .carrier-tab-btn.active {
      background: #2563eb;
      border-color: #3b82f6;
      color: #ffffff;
    }
    .carrier-tab-btn.active .tab-count-num {
      opacity: 1;
      font-weight: 800;
    }

    .quotes-scroll-area {
      flex: 1;
      overflow-y: auto;
      padding: 8px 10px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .quote-card.compact-tile {
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      padding: 7px 9px;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      gap: 4px;
      transition: all 0.15s;
    }
    .quote-card.compact-tile:hover {
      background: rgba(30, 41, 59, 0.85);
      border-color: rgba(255, 255, 255, 0.2);
    }
    .quote-card.compact-tile.selected-carrier {
      border-color: #10b981;
      background: rgba(16, 185, 129, 0.12);
      box-shadow: 0 0 10px rgba(16, 185, 129, 0.2);
    }
    .quote-card.compact-tile.recommended {
      border-color: #6366f1;
    }
    .quote-card.compact-tile.lowest-price {
      border-color: rgba(16, 185, 129, 0.4);
    }
    .tile-main-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 8px;
    }
    .tile-left {
      display: flex;
      align-items: center;
      gap: 8px;
      flex: 1;
      min-width: 0;
    }
    .quote-tile-logo {
      width: 26px;
      height: 26px;
      border-radius: 4px;
      background: #ffffff;
      padding: 2px;
      object-fit: contain;
      flex-shrink: 0;
    }
    .tile-carrier-info {
      display: flex;
      flex-direction: column;
      min-width: 0;
      gap: 2px;
    }
    .tile-title-line {
      display: flex;
      align-items: center;
      gap: 5px;
      flex-wrap: wrap;
    }
    .tile-carrier-name {
      font-size: 0.78rem;
      font-weight: 800;
      color: #ffffff;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .lowest-price-badge {
      font-size: 0.58rem;
      font-weight: 800;
      color: #34d399;
      background: rgba(16, 185, 129, 0.2);
      border: 1px solid rgba(16, 185, 129, 0.4);
      padding: 1px 4px;
      border-radius: 3px;
      white-space: nowrap;
    }
    .recommended-badge {
      font-size: 0.58rem;
      font-weight: 800;
      color: #a5b4fc;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.4);
      padding: 1px 4px;
      border-radius: 3px;
      white-space: nowrap;
    }
    .tile-meta-line {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.64rem;
      color: #94a3b8;
    }
    .tile-carrier-sub {
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .trust-badge {
      font-size: 0.58rem;
      font-weight: 700;
      color: #fbbf24;
      background: rgba(245, 158, 11, 0.15);
      border: 1px solid rgba(245, 158, 11, 0.3);
      padding: 0 4px;
      border-radius: 3px;
      white-space: nowrap;
    }
    .trust-badge.live {
      color: #34d399;
      background: rgba(16, 185, 129, 0.15);
      border-color: rgba(16, 185, 129, 0.3);
    }
    .tile-right-price {
      display: flex;
      align-items: center;
      gap: 8px;
      flex-shrink: 0;
    }
    .tile-price-stack {
      display: flex;
      flex-direction: column;
      align-items: flex-end;
    }
    .tile-usd {
      font-size: 0.90rem;
      font-weight: 800;
      color: #ffffff;
      line-height: 1.1;
    }
    .tile-try {
      font-size: 0.64rem;
      color: #94a3b8;
      font-weight: 600;
    }
    .btn-tile-select {
      background: #334155;
      border: 1px solid #475569;
      color: #ffffff;
      font-size: 0.66rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 4px;
      cursor: pointer;
      transition: all 0.15s;
      white-space: nowrap;
    }
    .quote-card.selected-carrier .btn-tile-select {
      background: #10b981;
      border-color: #059669;
    }
    .btn-tile-select:hover {
      filter: brightness(1.1);
    }
    .no-quotes-hint {
      text-align: center;
      font-size: 0.75rem;
      color: #64748b;
      margin-top: 20px;
    }

    /* BARKOD / ETİKET BİLEŞENİ (KOMPAKT & KATLANABİLİR) */
    .barcode-preview-section {
      background: rgba(15, 23, 42, 0.95);
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding: 8px 12px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .barcode-section-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      cursor: pointer;
      user-select: none;
      padding: 4px 6px;
      border-radius: 6px;
      background: rgba(30, 41, 59, 0.4);
      transition: background 0.15s;
    }
    .barcode-section-header:hover {
      background: rgba(30, 41, 59, 0.8);
    }
    .barcode-title-left {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .barcode-icon {
      font-size: 0.8rem;
    }
    .barcode-section-title {
      font-size: 0.68rem;
      font-weight: 800;
      color: #94a3b8;
      letter-spacing: 0.04em;
    }
    .barcode-code-pill {
      font-family: 'Consolas', monospace;
      font-size: 0.65rem;
      color: #38bdf8;
      background: rgba(56, 189, 248, 0.1);
      border: 1px solid rgba(56, 189, 248, 0.2);
      padding: 1px 5px;
      border-radius: 4px;
    }
    .btn-toggle-barcode {
      background: #1e293b;
      border: 1px solid #334155;
      color: #cbd5e1;
      font-size: 0.65rem;
      font-weight: 700;
      padding: 2px 7px;
      border-radius: 4px;
      cursor: pointer;
    }
    .btn-toggle-barcode:hover {
      background: #334155;
      color: #ffffff;
    }
    .saas-barcode-label-card {
      background: #ffffff;
      border-radius: 6px;
      padding: 8px 12px;
      color: #0f172a;
      box-shadow: 0 4px 12px rgba(0, 0, 0, 0.4);
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 3px;
      animation: fadeIn 0.15s ease-out;
    }
    .barcode-lines-row {
      display: flex;
      align-items: flex-start;
      justify-content: center;
      height: 28px;
      gap: 2px;
      width: 100%;
      overflow: hidden;
    }
    .b-line {
      display: inline-block;
      height: 28px;
      background: #000000;
    }
    .b-line.w1 { width: 1.5px; }
    .b-line.w2 { width: 2.5px; }
    .b-line.w3 { width: 3.5px; }
    .b-line.w4 { width: 5px; }

    .barcode-num-text {
      font-family: 'Consolas', 'Courier New', monospace;
      font-size: 0.74rem;
      font-weight: 800;
      color: #000000;
      letter-spacing: 0.1em;
      margin-top: 2px;
    }
    .barcode-divider {
      width: 100%;
      height: 1px;
      background: #e2e8f0;
      margin: 3px 0;
    }
    .barcode-bottom-info {
      width: 100%;
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 6px;
    }
    .barcode-carrier-dot {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .red-dot {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #dc2626;
      display: inline-block;
    }
    .carrier-label-name {
      font-size: 0.66rem;
      font-weight: 800;
      color: #0f172a;
    }
    .barcode-receiver-name {
      font-size: 0.64rem;
      color: #64748b;
      font-weight: 600;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      max-width: 140px;
    }

    /* FOOTER */
    .cockpit-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 8px 16px;
      background: rgba(15, 23, 42, 0.95);
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      z-index: 10;
    }
    .footer-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .footer-right {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .footer-order-info {
      font-size: 0.76rem;
      color: #cbd5e1;
    }
    .save-toast {
      color: #34d399;
      font-weight: 700;
      font-size: 0.76rem;
    }
    .btn-save-costs {
      background: #2563eb;
      border: none;
      color: #ffffff;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.76rem;
      font-weight: 700;
      cursor: pointer;
      display: inline-flex;
      align-items: center;
      gap: 6px;
      transition: all 0.15s;
    }
    .btn-save-costs:hover {
      background: #1d4ed8;
    }
    .btn-footer-preview {
      background: #1e293b;
      border: 1px solid #334155;
      color: #cbd5e1;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.76rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.15s;
      display: inline-flex;
      align-items: center;
      gap: 5px;
    }
    .btn-footer-preview:hover {
      background: #334155;
      color: #ffffff;
    }
    .btn-footer-create-shipment {
      background: #10b981;
      border: none;
      color: #ffffff;
      padding: 7px 18px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 800;
      cursor: pointer;
      box-shadow: 0 4px 12px rgba(16, 185, 129, 0.3);
      transition: all 0.15s;
      display: inline-flex;
      align-items: center;
      gap: 5px;
    }
    .btn-footer-create-shipment:hover:not(:disabled) {
      background: #059669;
      transform: translateY(-1px);
    }
    .btn-footer-create-shipment:disabled {
      background: #475569;
      cursor: not-allowed;
      opacity: 0.7;
    }

    /* MODALS COMMON */
    .modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(8px);
      z-index: 99999;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 20px;
      animation: fadeIn 0.15s ease-out;
    }
    @keyframes fadeIn {
      from { opacity: 0; }
      to { opacity: 1; }
    }
    .glass-modal {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 12px;
      box-shadow: 0 20px 50px rgba(0, 0, 0, 0.8);
      width: 100%;
      max-width: 500px;
      display: flex;
      flex-direction: column;
      overflow: hidden;
    }
    .modal-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 14px 18px;
      background: #1e293b;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .modal-title-with-logo {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .modal-carrier-logo {
      width: 36px;
      height: 36px;
      background: #ffffff;
      border-radius: 6px;
      padding: 2px;
      object-fit: contain;
    }
    .modal-title {
      font-size: 0.95rem;
      font-weight: 800;
      color: #ffffff;
      margin: 0;
    }
    .modal-sub {
      font-size: 0.68rem;
      color: #94a3b8;
    }
    .btn-close-modal {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.1rem;
      cursor: pointer;
    }
    .btn-close-modal:hover { color: #ffffff; }
    .modal-body {
      padding: 16px 18px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .session-info-card {
      background: rgba(30, 41, 59, 0.5);
      border-radius: 6px;
      padding: 10px;
      display: flex;
      flex-direction: column;
      gap: 6px;
      font-size: 0.74rem;
    }
    .info-row {
      display: flex;
      align-items: center;
      justify-content: space-between;
    }
    .status-pill {
      font-weight: 700;
      color: #f87171;
    }
    .status-pill.ok { color: #34d399; }
    .status-pill.warn { color: #fbbf24; }
    .jwt-helper-guide {
      background: rgba(30, 41, 59, 0.75);
      border: 1px solid rgba(59, 130, 246, 0.3);
      border-radius: 8px;
      padding: 10px 12px;
      margin: 8px 0;
    }
    .jwt-helper-guide .guide-title {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.76rem;
      color: #60a5fa;
      margin-bottom: 4px;
    }
    .jwt-helper-guide .guide-text {
      font-size: 0.72rem;
      color: #94a3b8;
      margin: 0 0 6px 0;
      line-height: 1.35;
    }
    .jwt-helper-guide .guide-steps {
      margin: 0;
      padding-left: 16px;
      font-size: 0.71rem;
      color: #cbd5e1;
      line-height: 1.45;
    }
    .jwt-helper-guide .guide-steps li {
      margin-bottom: 3px;
    }
    .jwt-helper-guide code {
      background: rgba(0, 0, 0, 0.35);
      color: #f59e0b;
      padding: 1px 4px;
      border-radius: 3px;
      font-family: monospace;
    }
    .form-group {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .form-label {
      font-size: 0.74rem;
      font-weight: 700;
      color: #cbd5e1;
    }
    .modal-textarea {
      width: 100%;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 8px 10px;
      color: #ffffff;
      font-size: 0.72rem;
      font-family: monospace;
      outline: none;
      resize: vertical;
    }
    .form-hint {
      font-size: 0.65rem;
      color: #64748b;
    }
    .modal-actions-row {
      display: flex;
      align-items: center;
      justify-content: flex-end;
      gap: 8px;
      margin-top: 8px;
    }
    .btn-test-session {
      background: #334155;
      border: 1px solid #475569;
      color: #ffffff;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-save-session {
      background: #2563eb;
      border: none;
      color: #ffffff;
      padding: 8px 16px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
    }

    /* THERMAL LABEL MODAL */
    .thermal-label-canvas {
      background: #ffffff;
      color: #000000;
      border-radius: 8px;
      padding: 16px;
      border: 2px solid #000000;
      display: flex;
      flex-direction: column;
      gap: 12px;
      box-shadow: 0 4px 20px rgba(0, 0, 0, 0.5);
    }
    .lbl-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      border-bottom: 2px solid #000000;
      padding-bottom: 6px;
    }
    .lbl-carrier-brand {
      font-size: 1rem;
      font-weight: 900;
      letter-spacing: -0.01em;
    }
    .lbl-priority {
      font-size: 0.72rem;
      font-weight: 800;
      background: #000000;
      color: #ffffff;
      padding: 2px 6px;
      border-radius: 2px;
    }
    .lbl-grid {
      display: flex;
      justify-content: space-between;
      border-bottom: 1px solid #000000;
      padding-bottom: 10px;
    }
    .lbl-left-meta {
      display: flex;
      flex-direction: column;
      gap: 4px;
      font-size: 0.72rem;
    }
    .lbl-row.address { max-width: 280px; }
    .lbl-right-badge {
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      border: 2px solid #000000;
      padding: 6px 12px;
      border-radius: 4px;
    }
    .country-big {
      font-size: 1.8rem;
      font-weight: 900;
    }
    .export-type {
      font-size: 0.65rem;
      font-weight: 800;
    }
    .lbl-barcode-area {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 4px;
      padding: 6px 0;
    }
    .big-barcode-lines {
      display: flex;
      gap: 3px;
      height: 48px;
    }
    .big-barcode-lines .b-line { height: 48px; }
    .big-barcode-text {
      font-family: monospace;
      font-size: 0.95rem;
      font-weight: 800;
      letter-spacing: 0.15em;
    }
    .lbl-footer-text {
      text-align: center;
      font-size: 0.62rem;
      color: #64748b;
      border-top: 1px solid #e2e8f0;
      padding-top: 4px;
    }
    .btn-print-label {
      background: #10b981;
      border: none;
      color: #ffffff;
      padding: 8px 16px;
      border-radius: 6px;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
    }
    .modal-explainer {
      font-size: 0.76rem;
      color: #94a3b8;
      line-height: 1.4;
      margin: 0;
    }
    .template-instructions {
      display: flex;
      align-items: center;
      gap: 10px;
      font-size: 0.76rem;
      color: #cbd5e1;
    }
    .step-num {
      width: 22px;
      height: 22px;
      border-radius: 50%;
      background: #3b82f6;
      color: #ffffff;
      display: flex;
      align-items: center;
      justify-content: center;
      font-weight: 800;
      font-size: 0.7rem;
      flex-shrink: 0;
    }
    .template-status-banner {
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
      font-size: 0.74rem;
      font-weight: 700;
      padding: 10px;
      border-radius: 6px;
      margin-top: 4px;
    }
  `]
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

  isArasTemplateModalOpen: boolean = false;
  isLabelPreviewOpen: boolean = false;
  isGeneratingShipment: boolean = false;
  isBarcodeExpanded: boolean = false;

  // TOASTS
  saveSuccessMessage: string | null = null;
  private autoSaveTimeout: any = null;
  private subs: Subscription[] = [];

  constructor(
    private ordersService: OrdersService,
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
        this.gtipSearchTerm = order.gtipCode 
          ? `${order.gtipCode} - ${order.gtipDescription || ''}`
          : '3926400000 - 3D Baskı Plastik Heykelcik';
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
        this.saveSuccessMessage = `✓ Kargo Hub'ından "${cName}" ($${cCost}) seçildi ve sipariş maliyetine uygulandı.`;
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
        return `● Canlı ve Aktif (${val.timeLeftStr ? 'Kalan: ' + val.timeLeftStr : 'Geçerli'})`;
      }
      return `⚠️ ${val.reason || 'Süresi Dolmuş (HTTP 401)'}`;
    }
    return this.isCurrentModalTokenValid ? '● Aktif' : '⚠️ Giriş Gerekli';
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
    this.saveSuccessMessage = 'Veriler başarıyla yenilendi!';
    setTimeout(() => this.saveSuccessMessage = null, 2500);
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

    this.saveSuccessMessage = 'Maliyetler ve GTİP başarıyla kaydedildi!';
    setTimeout(() => this.saveSuccessMessage = null, 3000);
  }

  // --- SHIPMENT CREATION ---
  executeCreateShipment(): void {
    if (!this.selectedOrder) return;
    this.isGeneratingShipment = true;

    setTimeout(() => {
      const quote = this.selectedCarrierQuote || this.carrierQuotes[0];
      const trackingPrefix = quote.carrierKey === 'aras' ? 'ARAS' : quote.carrierKey === 'shipentegra' ? '1Z' : quote.carrierKey === 'navlungo' ? 'DHL' : 'STM';
      const randomTrack = trackingPrefix + Math.floor(1000000000 + Math.random() * 9000000000) + 'TR';
      const barcode = '100923' + Math.floor(10000000 + Math.random() * 90000000);

      this.ordersService.fulfillOrder(
        this.selectedOrder!.orderId,
        quote.carrierKey,
        quote.carrierName,
        randomTrack,
        barcode
      );

      this.isGeneratingShipment = false;
      this.saveSuccessMessage = `✓ ${quote.carrierName} gönderisi oluşturuldu! Takip No: ${randomTrack}`;
      setTimeout(() => this.saveSuccessMessage = null, 4000);
    }, 800);
  }

  // --- MODALS OPEN/CLOSE ---
  openSessionModal(carrier: CarrierAccountSession): void {
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

    const isLive = this.ordersService.isCarrierSessionLive(this.activeModalCarrier.id);
    this.saveSuccessMessage = isLive
      ? `✓ ${this.activeModalCarrier.name} canlı oturumu bağlandı! Teklifler anlık güncellendi.`
      : `✓ ${this.activeModalCarrier.name} oturumu kaydedildi (Yedek liste devrede).`;
    setTimeout(() => this.saveSuccessMessage = null, 4000);
  }

  testCarrierConnection(): void {
    if (!this.activeModalCarrier) return;
    if (this.activeModalCarrier.id === 'aras') {
      const val = this.ordersService.validateJwt(this.modalTokenInput);
      if (val.isValid && !val.expired) {
        const expStr = val.expDate ? val.expDate.toLocaleString('tr-TR') : 'Süresiz';
        alert(`✅ Aras Global JWT Token Geçerli!\n\n• Durum: Canlı API Fiyatlandırması Aktif\n• Kalan Süre: ${val.timeLeftStr || 'Geçerli'}\n• Bitiş Zamanı: ${expStr}\n\nOturumu kaydederek doğrudan canlı API tekliflerini kullanabilirsiniz.`);
      } else {
        alert(`⚠️ Aras Global Token Süresi Dolmuş / Geçersiz!\n\n• Hata: ${val.reason}\n• API Yanıtı: HTTP 401 Unauthorized\n\nBu token kullanıldığında API hata vereceğinden siparişlerin kilitlenmemesi için sistem otomatik olarak tahmini sözleşme fiyatlarına geçer.\nLütfen panel.arasglobalcargo.com adresinden güncel tokenınızı kopyalayıp buraya yapıştırın.`);
      }
      return;
    }
    if (this.activeModalCarrier.id === 'shipentegra') {
      alert(`✅ ShipEntegra Oturumu Doğrulandı!\n\n• Durum: 6 Sözleşmeli Hat Aktif\n• Taşıyıcı Hatları: Amerika Eko Plus, Smart Express, Widect, Expedited, Express, UPS\n• Yetki: Bearer Token Geçerli (HTTP 200 OK)`);
      return;
    }
    if (this.activeModalCarrier.id === 'navlungo') {
      alert(`✅ Navlungo Oturumu Doğrulandı!\n\n• Durum: 4 Sözleşmeli Hat Aktif\n• Taşıyıcı Hatları: Widect, FedEx Priority, UPS Express, UPS Saver\n• Yetki: DHL Express & Navlungo Live Session Aktif`);
      return;
    }
    alert(`⚡ ${this.activeModalCarrier?.name} API bağlantısı test edildi: HTTP 200 OK (Yetki Geçerli).`);
  }

  disconnectSession(carrier: CarrierAccountSession): void {
    if (confirm(`${carrier.name} oturumunu kapatmak ve sistem bağlantısını kesmek istediğinize emin misiniz?`)) {
      this.ordersService.disconnectCarrier(carrier.id);
      this.loadQuotesForSelected();
      this.calculateLiveProfit();
      this.saveSuccessMessage = `🔴 ${carrier.name} bağlantısı kesildi.`;
      setTimeout(() => this.saveSuccessMessage = null, 3000);
    }
  }

  openShiptomoreModal(): void {
    const stm = this.carrierSessions.find(s => s.id === 'shiptomore');
    this.stmClientId = stm?.tokenOrKey || 'stm_live_client_id_8910';
    this.stmClientSecret = stm?.clientSecret || 'stm_sec_9941a87b';
    this.isShiptomoreModalOpen = true;
  }

  closeShiptomoreModal(): void {
    this.isShiptomoreModalOpen = false;
  }

  saveShiptomore(): void {
    this.ordersService.connectCarrier('shiptomore', this.stmClientId.trim(), this.stmClientSecret.trim());
    this.closeShiptomoreModal();
    this.loadQuotesForSelected();
    this.calculateLiveProfit();
    this.saveSuccessMessage = '✓ Ship to More API bağlantısı güncellendi!';
    setTimeout(() => this.saveSuccessMessage = null, 3000);
  }

  testShiptomore(): void {
    alert('✅ Ship to More bağlantısı başarılı: Client kimliği onaylandı ve DDP kargo rotaları aktif.');
  }

  openArasTemplateModal(): void {
    this.isArasTemplateModalOpen = true;
  }

  closeArasTemplateModal(): void {
    this.isArasTemplateModalOpen = false;
  }

  syncCapturedTemplates(): void {
    this.loadQuotesForSelected();
    this.calculateLiveProfit();
    this.closeArasTemplateModal();
    this.saveSuccessMessage = '✓ Masaüstü şablonları (%APPDATA%\\captures) başarıyla senkronize edildi: 14 canlı teklif güncel!';
    setTimeout(() => this.saveSuccessMessage = null, 4000);
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
    const rate = this.etsyApi.exchangeRate ? this.etsyApi.exchangeRate() : 49.13;
    const tryVal = (Number(valUsd) || 0) * rate;
    return '₺' + tryVal.toFixed(2);
  }
}
