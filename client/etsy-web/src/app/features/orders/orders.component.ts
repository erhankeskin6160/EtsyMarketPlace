import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Subscription } from 'rxjs';
import { OrdersService } from '../../core/services/orders.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { OrderFulfillmentItem, CarrierQuote, PackageSpecs } from '../../core/models/orders.models';

@Component({
  selector: 'app-orders',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="orders-cockpit">
      <!-- 1. TOP HEADER & METRIC SUMMARY -->
      <div class="cockpit-header">
        <div class="header-left">
          <div class="header-icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <rect x="1" y="3" width="15" height="13"></rect>
              <polygon points="16 8 20 8 23 11 23 16 16 16 16 8"></polygon>
              <circle cx="5.5" cy="18.5" r="2.5"></circle>
              <circle cx="18.5" cy="18.5" r="2.5"></circle>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Sipariş & Kargo Karşılama Stüdyosu</h1>
            <p class="page-subtitle">Canlı Etsy sipariş kuyruğu, eksik maliyet mutabakatı ve 4 taşıyıcılı anlık kargo fiyat kıyaslaması</p>
          </div>
        </div>

        <div class="header-metrics">
          <div class="mini-kpi">
            <span class="mini-label">Toplam Sipariş</span>
            <span class="mini-val">{{ allOrders.length }}</span>
          </div>
          <div class="mini-kpi warning">
            <span class="mini-label">⚠️ Eksik Maliyet</span>
            <span class="mini-val warning-val">{{ missingCostCount }}</span>
          </div>
          <div class="mini-kpi success">
            <span class="mini-label">Kargolanmamış</span>
            <span class="mini-val success-val">{{ unfulfilledCount }}</span>
          </div>
          <button class="btn-refresh" (click)="refreshOrders()">
            <svg width="15" height="15" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M23 4v6h-6"></path>
              <path d="M1 20v-6h6"></path>
              <path d="M3.51 9a9 9 0 0 1 14.85-3.36L23 10M1 14l4.64 4.36A9 9 0 0 0 20.49 15"></path>
            </svg>
            Yenile
          </button>
        </div>
      </div>

      <!-- 2. MAIN THREE-REGION WORKSPACE -->
      <div class="cockpit-body">
        
        <!-- REGION 1: ORDER QUEUE (LEFT) -->
        <div class="cockpit-region queue-region">
          <div class="queue-toolbar">
            <div class="search-input-wrap">
              <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                <circle cx="11" cy="11" r="8"></circle>
                <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
              </svg>
              <input 
                type="text" 
                [(ngModel)]="searchQuery" 
                placeholder="Sipariş no, alıcı veya SKU ara..."
                class="search-input" />
            </div>

            <div class="filter-tabs">
              <button 
                class="filter-tab" 
                [class.active]="activeTab === 'all'" 
                (click)="activeTab = 'all'">
                Tümü ({{ allOrders.length }})
              </button>
              <button 
                class="filter-tab" 
                [class.active]="activeTab === 'unfulfilled'" 
                (click)="activeTab = 'unfulfilled'">
                Bekleyen ({{ unfulfilledCount }})
              </button>
              <button 
                class="filter-tab alert-tab" 
                [class.active]="activeTab === 'missing_cost'" 
                (click)="activeTab = 'missing_cost'">
                ⚠️ Maliyet Eksik ({{ missingCostCount }})
              </button>
              <button 
                class="filter-tab" 
                [class.active]="activeTab === 'shipped'" 
                (click)="activeTab = 'shipped'">
                Kargolananlar
              </button>
            </div>
          </div>

          <div class="order-list-scroll">
            <div 
              *ngFor="let order of filteredOrders" 
              class="order-card"
              [class.selected]="selectedOrder?.orderId === order.orderId"
              [class.has-alert]="order.isCostMissing"
              (click)="onSelectOrder(order)">
              
              <div class="order-card-header">
                <span class="order-num">{{ order.orderNumber }}</span>
                <span class="order-badge" [ngClass]="order.status">
                  {{ order.status === 'unfulfilled' ? 'Bekliyor' : order.status === 'shipped' ? 'Kargolandı' : 'Teslim' }}
                </span>
              </div>

              <div class="buyer-row">
                <span class="buyer-name">{{ order.buyerName }}</span>
                <span class="country-badge">{{ order.countryCode }}</span>
              </div>

              <div class="order-items-snippet">
                <span>{{ order.items.length }} Kalem Ürün</span>
                <span class="order-total">{{ formatCurrency(order.totalAmount) }}</span>
              </div>

              <div *ngIf="order.isCostMissing" class="cost-alert-badge">
                <svg width="12" height="12" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M10.29 3.86L1.82 18a2 2 0 0 0 1.71 3h16.94a2 2 0 0 0 1.71-3L13.71 3.86a2 2 0 0 0-3.42 0z"></path>
                  <line x1="12" y1="9" x2="12" y2="13"></line>
                  <line x1="12" y1="17" x2="12.01" y2="17"></line>
                </svg>
                Maliyet Girilmedi
              </div>

              <div *ngIf="!order.isCostMissing" class="profit-snippet">
                <span>Net Kâr: <strong>{{ formatCurrency(order.netProfit) }}</strong></span>
                <span class="margin-pill">{{ order.profitMarginPercent }}%</span>
              </div>
            </div>

            <div *ngIf="filteredOrders.length === 0" class="empty-orders-view">
              <p>Filtreye uygun sipariş bulunamadı.</p>
            </div>
          </div>
        </div>

        <!-- REGION 2: ORDER DETAILS & PACKAGE SPECS (CENTER) -->
        <div class="cockpit-region detail-region" *ngIf="selectedOrder">
          
          <div class="region-title-bar">
            <div>
              <h2 class="region-title">Sipariş Detayı {{ selectedOrder.orderNumber }}</h2>
              <span class="order-date-text">Tarih: {{ selectedOrder.orderDate | date:'medium' }}</span>
            </div>
            <div class="dest-badge">
              <span>📍 {{ selectedOrder.city }}, {{ selectedOrder.country }}</span>
            </div>
          </div>

          <div class="detail-scroll">
            <!-- BUYER & ADDRESS CARD -->
            <div class="glass-card address-card">
              <div class="card-head">
                <span class="section-label">Alıcı ve Teslimat Adresi</span>
                <span class="buyer-email">{{ selectedOrder.buyerEmail }}</span>
              </div>
              <p class="address-text">{{ selectedOrder.buyerName }} — {{ selectedOrder.addressSnippet }}</p>
            </div>

            <!-- ORDER ITEMS LIST -->
            <div class="glass-card items-card">
              <span class="section-label">Siparişteki Ürünler</span>
              <div class="items-list">
                <div *ngFor="let item of selectedOrder.items" class="order-item-row">
                  <img [src]="item.imageUrl || 'assets/placeholder-product.png'" class="item-thumb" alt="Product" />
                  <div class="item-info">
                    <span class="item-name">{{ item.title }}</span>
                    <div class="item-meta">
                      <span class="sku-tag">SKU: {{ item.sku }}</span>
                      <span *ngFor="let v of item.variations" class="var-tag">{{ v }}</span>
                    </div>
                  </div>
                  <div class="item-pricing">
                    <span class="item-qty">{{ item.quantity }} Adet</span>
                    <span class="item-price">{{ formatCurrency(item.price * item.quantity) }}</span>
                  </div>
                </div>
              </div>
            </div>

            <!-- PACKAGE DIMENSIONS & DESI CALCULATOR -->
            <div class="glass-card package-card">
              <div class="card-head">
                <span class="section-label">Koli Ölçüleri & Desi Hesaplayıcı</span>
                <span class="desi-badge">Desi: <strong>{{ selectedOrder.packageSpecs.desi }}</strong></span>
              </div>

              <div class="specs-grid">
                <div class="spec-input-group">
                  <label>En (cm)</label>
                  <input 
                    type="number" 
                    [(ngModel)]="selectedOrder.packageSpecs.widthCm" 
                    (ngModelChange)="onDimensionChange()"
                    class="form-control" />
                </div>
                <div class="spec-input-group">
                  <label>Boy (cm)</label>
                  <input 
                    type="number" 
                    [(ngModel)]="selectedOrder.packageSpecs.lengthCm" 
                    (ngModelChange)="onDimensionChange()"
                    class="form-control" />
                </div>
                <div class="spec-input-group">
                  <label>Yükseklik (cm)</label>
                  <input 
                    type="number" 
                    [(ngModel)]="selectedOrder.packageSpecs.heightCm" 
                    (ngModelChange)="onDimensionChange()"
                    class="form-control" />
                </div>
                <div class="spec-input-group">
                  <label>Ağırlık (kg)</label>
                  <input 
                    type="number" 
                    step="0.1" 
                    [(ngModel)]="selectedOrder.packageSpecs.weightKg" 
                    (ngModelChange)="onDimensionChange()"
                    class="form-control" />
                </div>
              </div>
            </div>

            <!-- FINANCIAL COST & NET PROFIT ENGINE -->
            <div class="glass-card profit-engine-card">
              <div class="card-head">
                <span class="section-label">Maliyet Girişi & Anlık Kâr Analizi</span>
                <span *ngIf="selectedOrder.isCostMissing" class="badge-alert-small">Eksik Maliyet</span>
                <span *ngIf="!selectedOrder.isCostMissing" class="badge-ok-small">Hesaplandı</span>
              </div>

              <div class="costs-grid">
                <div class="cost-input-box">
                  <label>Ürün Maliyeti ($ USD)</label>
                  <input 
                    type="number" 
                    step="0.5" 
                    [(ngModel)]="editableProductCost" 
                    (ngModelChange)="calculateLiveProfit()"
                    placeholder="0.00" 
                    class="form-control highlight" />
                  <span class="try-hint">≈ {{ formatTry(editableProductCost) }}</span>
                </div>

                <div class="cost-input-box">
                  <label>Kargo Maliyeti ($ USD)</label>
                  <input 
                    type="number" 
                    step="0.5" 
                    [(ngModel)]="editableShippingCost" 
                    (ngModelChange)="calculateLiveProfit()"
                    placeholder="0.00" 
                    class="form-control highlight" />
                  <span class="try-hint">≈ {{ formatTry(editableShippingCost) }}</span>
                </div>
              </div>

              <!-- LIVE PROFIT METRICS -->
              <div class="profit-kpi-banner" [class.negative]="liveNetProfit < 0">
                <div class="profit-kpi-item">
                  <span class="p-label">Brüt Sipariş Tutarı</span>
                  <span class="p-val">{{ formatCurrency(selectedOrder.totalAmount) }}</span>
                </div>
                <div class="profit-kpi-item">
                  <span class="p-label">Tahmini Etsy Kesintisi (~%9.5)</span>
                  <span class="p-val fee">-{{ formatCurrency(selectedOrder.totalAmount * 0.095) }}</span>
                </div>
                <div class="profit-kpi-item highlight">
                  <span class="p-label">Net Kâr</span>
                  <span class="p-val profit">{{ formatCurrency(liveNetProfit) }}</span>
                  <span class="try-val">({{ formatTry(liveNetProfit) }})</span>
                </div>
                <div class="profit-kpi-item">
                  <span class="p-label">Kâr Marjı</span>
                  <span class="p-val margin">{{ liveProfitMargin }}%</span>
                </div>
              </div>
            </div>

          </div>
        </div>

        <!-- REGION 3: MULTI-CARRIER QUOTES DRAWER (RIGHT) -->
        <div class="cockpit-region quotes-region" *ngIf="selectedOrder">
          <div class="region-title-bar">
            <div>
              <h2 class="region-title">Canlı Kargo Fiyat Karşılaştırması</h2>
              <span class="order-date-text">{{ selectedOrder.countryCode }} / Desi: {{ selectedOrder.packageSpecs.desi }}</span>
            </div>
          </div>

          <div class="quotes-scroll">
            <div 
              *ngFor="let quote of carrierQuotes" 
              class="carrier-quote-card"
              [class.recommended]="quote.isRecommended"
              [class.active-carrier]="selectedOrder.selectedCarrier === quote.carrierKey">
              
              <div class="quote-header">
                <div>
                  <h3 class="carrier-name">{{ quote.carrierName }}</h3>
                  <span class="service-type">{{ quote.serviceType }}</span>
                </div>
                <div *ngIf="quote.isRecommended" class="recommended-badge">
                  ★ En Uygun
                </div>
              </div>

              <div class="quote-specs">
                <div class="delivery-time">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <circle cx="12" cy="12" r="10"></circle>
                    <polyline points="12 6 12 12 16 14"></polyline>
                  </svg>
                  <span>{{ quote.estimatedDays }}</span>
                </div>
                <div class="price-box">
                  <span class="price-usd">&#36;{{ quote.priceUsd }}</span>
                  <span class="price-try">₺{{ quote.priceTry }}</span>
                </div>
              </div>

              <div class="quote-actions">
                <button 
                  class="btn-select-carrier" 
                  (click)="applyCarrierQuote(quote)">
                  <svg width="14" height="14" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                    <polyline points="20 6 9 17 4 12"></polyline>
                  </svg>
                  Bu Taşıyıcıyı Seç (Kargoyu Tanımla)
                </button>
              </div>
            </div>

            <!-- FULFILLMENT STATUS & TRACKING CODE BOX -->
            <div class="glass-card tracking-card">
              <span class="section-label">Kargo Takip & Gönderim</span>
              
              <div *ngIf="selectedOrder.status === 'shipped'" class="shipped-info">
                <div class="shipped-badge">
                  ✓ Kargolandı
                </div>
                <p>Taşıyıcı: <strong>{{ selectedOrder.carrierServiceName || selectedOrder.selectedCarrier }}</strong></p>
                <p>Takip No: <code>{{ selectedOrder.trackingCode }}</code></p>
              </div>

              <div *ngIf="selectedOrder.status !== 'shipped'" class="tracking-input-wrap">
                <label>Takip Kodu Gir:</label>
                <div class="input-with-button">
                  <input 
                    type="text" 
                    [(ngModel)]="manualTrackingCode" 
                    placeholder="Örn: 1Z9999999999999999 veya DHL..." 
                    class="form-control" />
                  <button 
                    class="btn-fulfill" 
                    (click)="submitTracking()">
                    Kargolandı İşaretle
                  </button>
                </div>
              </div>
            </div>
          </div>
        </div>

      </div>

      <!-- 3. BOTTOM PERSISTENT ACTION STRIP -->
      <div class="cockpit-footer" *ngIf="selectedOrder">
        <div class="footer-left">
          <span class="footer-order-info">
            Seçili: <strong>{{ selectedOrder.orderNumber }}</strong> ({{ selectedOrder.buyerName }})
          </span>
          <span *ngIf="saveSuccessMessage" class="save-toast">
            ✓ {{ saveSuccessMessage }}
          </span>
        </div>

        <div class="footer-right">
          <button class="btn-save-costs" (click)="saveCosts()">
            <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
              <polyline points="17 21 17 13 7 13 7 21"></polyline>
              <polyline points="7 3 7 8 15 8"></polyline>
            </svg>
            Maliyetleri Kaydet
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .orders-cockpit {
      display: flex;
      flex-direction: column;
      height: calc(100vh - 70px);
      background: #0b0f19;
      color: #e2e8f0;
      overflow: hidden;
    }

    /* TOP BAR */
    .cockpit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 24px;
      background: rgba(15, 23, 42, 0.85);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(12px);
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .header-icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.2), rgba(168, 85, 247, 0.2));
      border: 1px solid rgba(99, 102, 241, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #818cf8;
    }
    .page-title {
      font-size: 1.25rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .header-metrics {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .mini-kpi {
      padding: 6px 14px;
      background: rgba(30, 41, 59, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 8px;
      display: flex;
      flex-direction: column;
      align-items: flex-end;
    }
    .mini-label {
      font-size: 0.7rem;
      color: #94a3b8;
    }
    .mini-val {
      font-size: 1rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .warning-val { color: #f59e0b; }
    .success-val { color: #10b981; }
    .btn-refresh {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 8px 14px;
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.35);
      color: #a5b4fc;
      border-radius: 8px;
      cursor: pointer;
      font-size: 0.85rem;
      font-weight: 600;
      transition: all 0.2s;
    }
    .btn-refresh:hover {
      background: rgba(99, 102, 241, 0.3);
      color: #fff;
    }

    /* COCKPIT BODY & 3 REGIONS */
    .cockpit-body {
      display: flex;
      flex: 1;
      overflow: hidden;
    }
    .cockpit-region {
      display: flex;
      flex-direction: column;
      height: 100%;
      border-right: 1px solid rgba(255, 255, 255, 0.06);
    }
    .region-title-bar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 14px 18px;
      background: rgba(15, 23, 42, 0.6);
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }
    .region-title {
      font-size: 0.95rem;
      font-weight: 700;
      margin: 0;
      color: #f1f5f9;
    }
    .order-date-text {
      font-size: 0.75rem;
      color: #94a3b8;
    }

    /* REGION 1: ORDER QUEUE */
    .queue-region {
      width: 320px;
      min-width: 320px;
      background: rgba(15, 23, 42, 0.4);
    }
    .queue-toolbar {
      padding: 12px;
      display: flex;
      flex-direction: column;
      gap: 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
    }
    .search-input-wrap {
      position: relative;
      display: flex;
      align-items: center;
    }
    .search-input-wrap svg {
      position: absolute;
      left: 10px;
      color: #64748b;
    }
    .search-input {
      width: 100%;
      padding: 8px 10px 8px 34px;
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 8px;
      color: #fff;
      font-size: 0.82rem;
      outline: none;
    }
    .filter-tabs {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 6px;
    }
    .filter-tab {
      padding: 6px 8px;
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 6px;
      color: #94a3b8;
      font-size: 0.75rem;
      cursor: pointer;
      text-align: center;
      transition: all 0.2s;
    }
    .filter-tab.active {
      background: rgba(99, 102, 241, 0.2);
      border-color: rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
      font-weight: 600;
    }
    .filter-tab.alert-tab.active {
      background: rgba(245, 158, 11, 0.2);
      border-color: rgba(245, 158, 11, 0.4);
      color: #fbbf24;
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
      padding: 12px;
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.05);
      border-radius: 10px;
      cursor: pointer;
      transition: all 0.2s ease;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .order-card:hover {
      background: rgba(30, 41, 59, 0.7);
      border-color: rgba(255, 255, 255, 0.12);
    }
    .order-card.selected {
      background: rgba(99, 102, 241, 0.15);
      border-color: rgba(99, 102, 241, 0.5);
      box-shadow: 0 0 16px rgba(99, 102, 241, 0.15);
    }
    .order-card.has-alert {
      border-left: 3px solid #f59e0b;
    }
    .order-card-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .order-num {
      font-size: 0.85rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .order-badge {
      font-size: 0.7rem;
      padding: 2px 6px;
      border-radius: 4px;
      font-weight: 600;
    }
    .order-badge.unfulfilled { background: rgba(245, 158, 11, 0.15); color: #fbbf24; }
    .order-badge.shipped { background: rgba(59, 130, 246, 0.15); color: #60a5fa; }
    .order-badge.delivered { background: rgba(16, 185, 129, 0.15); color: #34d399; }
    .buyer-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.8rem;
      color: #cbd5e1;
    }
    .country-badge {
      background: rgba(255, 255, 255, 0.07);
      padding: 1px 6px;
      border-radius: 4px;
      font-size: 0.7rem;
    }
    .order-items-snippet {
      display: flex;
      justify-content: space-between;
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .order-total {
      font-weight: 700;
      color: #f8fafc;
    }
    .cost-alert-badge {
      display: flex;
      align-items: center;
      gap: 4px;
      font-size: 0.7rem;
      color: #fbbf24;
      background: rgba(245, 158, 11, 0.1);
      padding: 3px 6px;
      border-radius: 4px;
      font-weight: 600;
    }
    .profit-snippet {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.72rem;
      color: #10b981;
    }
    .margin-pill {
      background: rgba(16, 185, 129, 0.15);
      padding: 1px 5px;
      border-radius: 4px;
      font-weight: 700;
    }

    /* REGION 2: DETAIL REGION */
    .detail-region {
      flex: 1;
      background: #0b0f19;
      overflow: hidden;
    }
    .dest-badge {
      background: rgba(99, 102, 241, 0.1);
      border: 1px solid rgba(99, 102, 241, 0.25);
      padding: 4px 10px;
      border-radius: 6px;
      font-size: 0.8rem;
      color: #a5b4fc;
    }
    .detail-scroll {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 14px 18px;
    }
    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 10px;
    }
    .section-label {
      font-size: 0.8rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
      color: #94a3b8;
      font-weight: 700;
    }
    .buyer-email {
      font-size: 0.75rem;
      color: #64748b;
    }
    .address-text {
      margin: 0;
      font-size: 0.88rem;
      color: #e2e8f0;
      line-height: 1.4;
    }

    /* ITEMS LIST */
    .items-list {
      display: flex;
      flex-direction: column;
      gap: 10px;
      margin-top: 10px;
    }
    .order-item-row {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 8px;
      background: rgba(15, 23, 42, 0.5);
      border-radius: 8px;
    }
    .item-thumb {
      width: 48px;
      height: 48px;
      border-radius: 8px;
      object-fit: cover;
      background: #1e293b;
    }
    .item-info {
      flex: 1;
    }
    .item-name {
      font-size: 0.85rem;
      font-weight: 600;
      color: #f1f5f9;
      display: block;
    }
    .item-meta {
      display: flex;
      gap: 6px;
      margin-top: 4px;
    }
    .sku-tag, .var-tag {
      font-size: 0.7rem;
      padding: 1px 6px;
      border-radius: 4px;
      background: rgba(255, 255, 255, 0.06);
      color: #94a3b8;
    }
    .item-pricing {
      text-align: right;
    }
    .item-qty {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .item-price {
      font-size: 0.9rem;
      font-weight: 700;
      color: #f8fafc;
    }

    /* PACKAGE SPECS */
    .specs-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      margin-top: 8px;
    }
    .spec-input-group label {
      display: block;
      font-size: 0.75rem;
      color: #94a3b8;
      margin-bottom: 4px;
    }
    .form-control {
      width: 100%;
      padding: 8px 10px;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 6px;
      color: #fff;
      font-size: 0.85rem;
      box-sizing: border-box;
    }
    .form-control.highlight {
      border-color: rgba(99, 102, 241, 0.4);
      background: rgba(30, 41, 59, 0.7);
    }
    .desi-badge {
      background: rgba(99, 102, 241, 0.15);
      color: #a5b4fc;
      padding: 3px 8px;
      border-radius: 6px;
      font-size: 0.8rem;
    }

    /* PROFIT ENGINE */
    .costs-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
      margin-top: 8px;
    }
    .cost-input-box label {
      display: block;
      font-size: 0.75rem;
      color: #cbd5e1;
      margin-bottom: 4px;
      font-weight: 600;
    }
    .try-hint {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
      margin-top: 4px;
    }
    .profit-kpi-banner {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      margin-top: 14px;
      padding: 12px;
      background: rgba(15, 23, 42, 0.8);
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .profit-kpi-item {
      display: flex;
      flex-direction: column;
    }
    .p-label {
      font-size: 0.7rem;
      color: #94a3b8;
    }
    .p-val {
      font-size: 1rem;
      font-weight: 700;
      color: #f8fafc;
      margin-top: 2px;
    }
    .p-val.fee { color: #f87171; }
    .p-val.profit { color: #34d399; }
    .p-val.margin { color: #60a5fa; }
    .try-val {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .badge-alert-small {
      background: rgba(245, 158, 11, 0.2);
      color: #fbbf24;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 0.7rem;
    }
    .badge-ok-small {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 0.7rem;
    }

    /* REGION 3: QUOTES REGION */
    .quotes-region {
      width: 360px;
      min-width: 360px;
      background: rgba(15, 23, 42, 0.35);
    }
    .quotes-scroll {
      flex: 1;
      overflow-y: auto;
      padding: 14px;
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .carrier-quote-card {
      padding: 12px 14px;
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 10px;
      display: flex;
      flex-direction: column;
      gap: 8px;
      transition: all 0.2s;
    }
    .carrier-quote-card.recommended {
      border-color: rgba(99, 102, 241, 0.5);
      background: rgba(99, 102, 241, 0.08);
    }
    .carrier-quote-card.active-carrier {
      border-color: #34d399;
      background: rgba(16, 185, 129, 0.08);
    }
    .quote-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
    }
    .carrier-name {
      font-size: 0.88rem;
      font-weight: 700;
      margin: 0;
      color: #f1f5f9;
    }
    .service-type {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .recommended-badge {
      font-size: 0.68rem;
      background: rgba(99, 102, 241, 0.25);
      color: #c7d2fe;
      padding: 2px 6px;
      border-radius: 4px;
      font-weight: 700;
    }
    .quote-specs {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-top: 4px;
    }
    .delivery-time {
      display: flex;
      align-items: center;
      gap: 5px;
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .price-box {
      text-align: right;
    }
    .price-usd {
      font-size: 1.1rem;
      font-weight: 700;
      color: #f8fafc;
      display: block;
    }
    .price-try {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .btn-select-carrier {
      width: 100%;
      padding: 7px 10px;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 6px;
      color: #e2e8f0;
      font-size: 0.75rem;
      font-weight: 600;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      transition: all 0.2s;
    }
    .btn-select-carrier:hover {
      background: rgba(99, 102, 241, 0.3);
      border-color: rgba(99, 102, 241, 0.5);
      color: #fff;
    }

    /* TRACKING CARD */
    .tracking-card {
      margin-top: 8px;
    }
    .shipped-info {
      margin-top: 8px;
      font-size: 0.82rem;
      color: #cbd5e1;
    }
    .shipped-badge {
      display: inline-block;
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
      padding: 3px 8px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.75rem;
      margin-bottom: 6px;
    }
    .tracking-input-wrap {
      margin-top: 8px;
    }
    .tracking-input-wrap label {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
      margin-bottom: 4px;
    }
    .input-with-button {
      display: flex;
      gap: 6px;
    }
    .btn-fulfill {
      padding: 8px 12px;
      background: #10b981;
      border: none;
      border-radius: 6px;
      color: #fff;
      font-size: 0.75rem;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
    }
    .btn-fulfill:hover {
      background: #059669;
    }

    /* BOTTOM PERSISTENT ACTION STRIP */
    .cockpit-footer {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 24px;
      background: rgba(15, 23, 42, 0.95);
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(12px);
    }
    .footer-order-info {
      font-size: 0.85rem;
      color: #94a3b8;
    }
    .footer-order-info strong {
      color: #f1f5f9;
    }
    .save-toast {
      margin-left: 16px;
      color: #34d399;
      font-size: 0.85rem;
      font-weight: 600;
      animation: fadeIn 0.3s ease;
    }
    .btn-save-costs {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 9px 18px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-size: 0.85rem;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(99, 102, 241, 0.35);
      transition: all 0.2s;
    }
    .btn-save-costs:hover {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateX(-6px); }
      to { opacity: 1; transform: translateX(0); }
    }
  `]
})
export class OrdersComponent implements OnInit, OnDestroy {
  allOrders: OrderFulfillmentItem[] = [];
  selectedOrder: OrderFulfillmentItem | null = null;
  carrierQuotes: CarrierQuote[] = [];

  activeTab: 'all' | 'unfulfilled' | 'missing_cost' | 'shipped' = 'all';
  searchQuery: string = '';

  editableProductCost: number = 0;
  editableShippingCost: number = 0;
  liveNetProfit: number = 0;
  liveProfitMargin: number = 0;
  manualTrackingCode: string = '';
  saveSuccessMessage: string = '';

  private sub = new Subscription();

  constructor(
    private ordersService: OrdersService,
    public etsyApi: EtsyApiService
  ) {}

  get isTry(): boolean {
    return this.etsyApi.isTryCurrency();
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  ngOnInit(): void {
    this.sub.add(
      this.ordersService.orders$.subscribe(orders => {
        this.allOrders = orders;
      })
    );

    this.sub.add(
      this.ordersService.selectedOrder$.subscribe(order => {
        this.selectedOrder = order;
        if (order) {
          this.editableProductCost = order.productCost;
          this.editableShippingCost = order.shippingCost;
          this.manualTrackingCode = order.trackingCode || '';
          this.calculateLiveProfit();
          this.refreshCarrierQuotes();
        }
      })
    );
  }

  ngOnDestroy(): void {
    this.sub.unsubscribe();
  }

  get filteredOrders(): OrderFulfillmentItem[] {
    return this.allOrders.filter(o => {
      // Tab filter
      if (this.activeTab === 'unfulfilled' && o.status !== 'unfulfilled') return false;
      if (this.activeTab === 'missing_cost' && !o.isCostMissing) return false;
      if (this.activeTab === 'shipped' && o.status !== 'shipped' && o.status !== 'delivered') return false;

      // Query filter
      if (this.searchQuery.trim()) {
        const q = this.searchQuery.toLowerCase();
        const matchesNum = o.orderNumber.toLowerCase().includes(q);
        const matchesBuyer = o.buyerName.toLowerCase().includes(q);
        const matchesSku = o.items.some(it => it.sku?.toLowerCase().includes(q) || it.title.toLowerCase().includes(q));
        if (!matchesNum && !matchesBuyer && !matchesSku) return false;
      }

      return true;
    });
  }

  get missingCostCount(): number {
    return this.allOrders.filter(o => o.isCostMissing).length;
  }

  get unfulfilledCount(): number {
    return this.allOrders.filter(o => o.status === 'unfulfilled').length;
  }

  onSelectOrder(order: OrderFulfillmentItem): void {
    this.ordersService.selectOrder(order);
  }

  refreshOrders(): void {
    this.ordersService.loadOrders();
  }

  onDimensionChange(): void {
    if (!this.selectedOrder) return;
    const { widthCm, lengthCm, heightCm } = this.selectedOrder.packageSpecs;
    const desi = Number(((widthCm * lengthCm * heightCm) / 5000).toFixed(2));
    this.selectedOrder.packageSpecs.desi = desi;
    this.refreshCarrierQuotes();
  }

  refreshCarrierQuotes(): void {
    if (!this.selectedOrder) return;
    this.carrierQuotes = this.ordersService.calculateCarrierQuotes(
      this.selectedOrder.packageSpecs,
      this.selectedOrder.countryCode,
      this.usdTryRate
    );
  }

  applyCarrierQuote(quote: CarrierQuote): void {
    if (!this.selectedOrder) return;
    this.editableShippingCost = quote.priceUsd;
    this.selectedOrder.selectedCarrier = quote.carrierKey;
    this.selectedOrder.carrierServiceName = quote.carrierName;
    this.calculateLiveProfit();
  }

  calculateLiveProfit(): void {
    if (!this.selectedOrder) return;
    const total = this.selectedOrder.totalAmount;
    const etsyFee = total * 0.095;
    const cost = (this.editableProductCost || 0) + (this.editableShippingCost || 0) + etsyFee;
    this.liveNetProfit = Number((total - cost).toFixed(2));
    this.liveProfitMargin = total > 0 ? Number(((this.liveNetProfit / total) * 100).toFixed(1)) : 0;
  }

  saveCosts(): void {
    if (!this.selectedOrder) return;
    this.ordersService.updateOrderCosts(
      this.selectedOrder.orderId,
      this.editableProductCost,
      this.editableShippingCost
    ).subscribe(() => {
      this.saveSuccessMessage = 'Maliyetler başarıyla güncellendi!';
      setTimeout(() => this.saveSuccessMessage = '', 3000);
    });
  }

  submitTracking(): void {
    if (!this.selectedOrder || !this.manualTrackingCode.trim()) return;
    const carrier = this.selectedOrder.selectedCarrier || 'shiptomore';
    const carrierName = this.selectedOrder.carrierServiceName || 'Shiptomore Express';

    this.ordersService.fulfillOrder(
      this.selectedOrder.orderId,
      carrier,
      carrierName,
      this.manualTrackingCode.trim()
    ).subscribe(() => {
      this.saveSuccessMessage = 'Sipariş kargolandı olarak güncellendi!';
      setTimeout(() => this.saveSuccessMessage = '', 3000);
    });
  }

  formatCurrency(usdVal: number): string {
    if (this.isTry) {
      return `₺${(usdVal * this.usdTryRate).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
    }
    return `$${usdVal.toLocaleString('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }

  formatTry(usdVal: number): string {
    return `₺${(usdVal * this.usdTryRate).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
}
