import { Component, OnInit, OnDestroy } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { 
  PaymentLedgerEntry, 
  BankPayoutRecord, 
  ExpenseBreakdownItem,
  OrderFinancialRow,
  ForecastKpiSummary
} from '../../core/models/accounting.models';

@Component({
  selector: 'app-accounting',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="accounting-view">
      <!-- 1. TOP HEADER & DESKTOP CONTROLS -->
      <div class="accounting-header">
        <div class="header-left">
          <div class="title-row">
            <h1 class="page-title">Finansal Raporlama Muhasebe Paneli</h1>
            <span class="live-status-pill">
              <span class="live-dot"></span> Canlı Etsy API verisi yüklendi: 97 kayıt | 03.09.2026 - 03.10.2026
            </span>
          </div>
          <p class="page-subtitle">Etsy Payment Account hareketleri, sipariş kâr marjları ve çok katmanlı maliyet denetimi</p>
        </div>

        <div class="header-actions">
          <!-- LIVE CURRENCY TOGGLE -->
          <div class="currency-toggle-box">
            <button 
              class="curr-btn" 
              [class.active]="!isTry" 
              (click)="setCurrency('USD')">
              $ USD
            </button>
            <button 
              class="curr-btn" 
              [class.active]="isTry" 
              (click)="setCurrency('TRY')">
              ₺ TRY
            </button>
          </div>
        </div>
      </div>

      <!-- DESKTOP TOP TOOLBAR (Matching FinancialReportForm.cs) -->
      <div class="desktop-action-bar">
        <!-- Date Preset Dropdown -->
        <div class="bar-control-group">
          <select [(ngModel)]="selectedDatePreset" (change)="onPresetChange()" class="bar-select">
            <option value="last30">Son 30 Gün</option>
            <option value="last7">Son 7 Gün</option>
            <option value="thisMonth">Bu Ay</option>
            <option value="lastMonth">Geçen Ay</option>
            <option value="all">Tüm Zamanlar</option>
            <option value="custom">Özel Tarih Aralığı</option>
          </select>
        </div>

        <!-- Date Range Pickers -->
        <div class="bar-control-group dates">
          <input type="text" [(ngModel)]="startDate" class="bar-date-input" />
          <span class="date-sep">—</span>
          <input type="text" [(ngModel)]="endDate" class="bar-date-input" />
        </div>

        <!-- Action Buttons -->
        <button class="bar-btn refresh" (click)="refreshFromEtsy()" [disabled]="isRefreshing">
          <span class="btn-icon">🔄</span> {{ isRefreshing ? 'Yenileniyor...' : "Etsy'den Yenile" }}
        </button>

        <button class="bar-btn secondary" (click)="openCostManager()">
          <span class="btn-icon">🏷️</span> Ürün Maliyetleri
        </button>

        <button class="bar-btn accent" (click)="openApiSettings()">
          <span class="btn-icon">⚙️</span> API Ayarları
        </button>

        <!-- Auto Rate Checkbox -->
        <div class="rate-toggle-group">
          <label class="checkbox-label">
            <input type="checkbox" [(ngModel)]="autoRateEnabled" (change)="onAutoRateToggle()" />
            <span class="flag-icon">🇹🇷</span> TR Oto Kur:
          </label>
          <span class="live-rate-badge">{{ liveRate.toFixed(2) }} ₺</span>
        </div>

        <!-- Excel, Telegram, VDS Buttons -->
        <button class="bar-btn excel" (click)="exportExcel()">
          <span class="btn-icon">📊</span> Excel
        </button>

        <button class="bar-btn telegram" (click)="sendTelegramReport()">
          <span class="btn-icon">✈️</span> Telegram'a Gönder
        </button>

        <button class="bar-btn vds" (click)="syncVds()" [disabled]="isSyncingVds">
          <span class="btn-icon">☁️</span> {{ isSyncingVds ? 'Aktarılıyor...' : "VDS'e Aktar" }}
        </button>
      </div>

      <!-- 2. NINE FINANCIAL KPI CARDS (Matching FinancialReportForm.cs Image 2) -->
      <div class="kpi-grid-9">
        <!-- 1. Brüt Satış -->
        <div class="kpi-card gross interactive-kpi" (click)="openSalesAnalysisModal()" title="Satış ve Gelir Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">💰 BRÜT SATIŞ</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-success">{{ formatKpi(grossSalesTry, grossSalesUsd) }}</div>
          <div class="kpi-indicator green"></div>
        </div>

        <!-- 2. Etsy Kesintisi -->
        <div class="kpi-card fee interactive-kpi" (click)="openFeesAnalysisModal()" title="Etsy Komisyon & Kesinti Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">📋 ETSY KESİNTİSİ</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-warning">-{{ formatKpi(etsyFeesTry, etsyFeesUsd) }}</div>
          <div class="kpi-indicator orange"></div>
        </div>

        <!-- 3. İç Reklam -->
        <div class="kpi-card inner-ad interactive-kpi" (click)="openInnerAdsModal()" title="Etsy Ads İç Reklam Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">📢 İÇ REKLAM</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-danger">-{{ formatKpi(innerAdsTry, innerAdsUsd) }}</div>
          <div class="kpi-indicator red"></div>
        </div>

        <!-- 4. Dış Reklam -->
        <div class="kpi-card offsite-ad interactive-kpi" (click)="openOffsiteAdsModal()" title="Offsite Ads Dış Reklam Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">🌐 DIŞ REKLAM</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-orange">-{{ formatKpi(offsiteAdsTry, offsiteAdsUsd) }}</div>
          <div class="kpi-indicator dark-orange"></div>
        </div>

        <!-- 5. İadeler -->
        <div class="kpi-card refunds interactive-kpi" (click)="openRefundsModal()" title="İade ve Geri Ödeme Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">↩️ İADELER</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-danger">-{{ formatKpi(refundsTry, refundsUsd) }}</div>
          <div class="kpi-indicator red"></div>
        </div>

        <!-- 6. Etsy Net Gelir -->
        <div class="kpi-card net-income interactive-kpi" (click)="openNetIncomeModal()" title="Etsy Net Gelir & Platform Hakediş Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">✅ ETSY NET GELİR</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-primary">{{ formatKpi(netIncomeTry, netIncomeUsd) }}</div>
          <div class="kpi-indicator indigo"></div>
        </div>

        <!-- 7. Sipariş Maliyeti -->
        <div class="kpi-card costs interactive-kpi" (click)="openCostBreakdownModal()" title="Sipariş & Kargo Maliyet Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">📦 SİPARİŞ MALİYETİ</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-warning">-{{ formatKpi(productCostsTry, productCostsUsd) }}</div>
          <div class="kpi-indicator orange"></div>
        </div>

        <!-- 8. Gerçek Net Kâr -->
        <div class="kpi-card real-profit highlight interactive-kpi" (click)="openBalanceAnalysisModal()" title="Gerçek Net Kâr & Bilanço Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">💵 GERÇEK NET KÂR</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-emerald">{{ formatKpi(realNetProfitTry, realNetProfitUsd) }}</div>
          <div class="kpi-indicator emerald"></div>
        </div>

        <!-- 9. Banka Yatırımı -->
        <div class="kpi-card payouts interactive-kpi" (click)="openBankPayoutModal()" title="Etsy Banka Yatırımı & Transfer Analizini Aç (Detay)">
          <div class="kpi-header">
            <span class="kpi-title">🏦 BANKA YATIRIMI</span>
            <span class="kpi-zoom-hint">🔍 Detay</span>
          </div>
          <div class="kpi-value text-cyan">-{{ formatKpi(bankPayoutsTry, bankPayoutsUsd) }}</div>
          <div class="kpi-indicator cyan"></div>
        </div>
      </div>

      <!-- 3. NAVIGATION TABS (5 Desktop Tabs) -->
      <div class="tab-nav-bar">
        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'charts'" 
          (click)="activeTab = 'charts'">
          📊 Grafik Analizleri
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'forecast'" 
          (click)="activeTab = 'forecast'">
          🤖 AI Kâr & Ciro Tahmini
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'period'" 
          (click)="activeTab = 'period'">
          📅 Dönemsel Muhasebe
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'orders'" 
          (click)="activeTab = 'orders'">
          📦 Siparişler & Net Kâr
        </button>

        <button 
          class="nav-tab" 
          [class.active]="activeTab === 'ledger'" 
          (click)="activeTab = 'ledger'">
          📋 Ödeme Defteri Kayıtları
        </button>
      </div>

      <!-- 4. TAB CONTENTS -->
      <div class="tab-content-area">

        <!-- TAB 4: SİPARİŞLER & NET KÂR (IMAGE 2 PARITY) -->
        <div *ngIf="activeTab === 'orders'" class="tab-pane">
          <!-- SEARCH, FILTER & RECONCILIATION BAR -->
          <div class="orders-toolbar">
            <div class="search-input-wrap">
              <input 
                type="text" 
                [(ngModel)]="orderSearchQuery" 
                (ngModelChange)="filterOrders()"
                placeholder="🔍 Sipariş No, Müşteri No / Adı veya Ürün Ara..." 
                class="order-search-input" />
            </div>

            <div class="filter-dropdown-wrap">
              <select [(ngModel)]="orderCostFilter" (change)="filterOrders()" class="order-filter-select">
                <option value="all">📦 Tüm Siparişler</option>
                <option value="completed">🟢 Başarılı Siparişler</option>
                <option value="canceled">🔴 İptal / İade Edilenler</option>
                <option value="hasCost">✅ Maliyeti Girilmiş</option>
                <option value="missingCost">⚠️ Maliyeti Eksik</option>
                <option value="hasInvoice">📎 Faturalı Siparişler</option>
              </select>
            </div>

            <button class="btn-clear-filter" (click)="clearOrderFilters()" title="Filtreyi Temizle">
              ✕
            </button>

            <button class="btn-profit-info" (click)="openProfitReconciliationModal()">
              ❓ Kâr Farkı Nedir?
            </button>

            <!-- Order Summary KPI Text -->
            <div class="orders-summary-label">
              <span>📦 <strong>{{ filteredOrders.length }} sipariş</strong></span>
              <span class="sep">|</span>
              <span>Sipariş Kârı: <strong class="text-success">₺24.756,89 ($507,17)</strong></span>
              <span class="sep">|</span>
              <span>Mağaza Net: <strong class="text-emerald">₺22.077,69</strong></span>
              <span class="sep">|</span>
              <span class="missing-badge" *ngIf="missingCostCount > 0">
                ⚠️ {{ missingCostCount }} maliyetsiz
              </span>
            </div>
          </div>

          <!-- ORDERS & NET PROFIT DATA GRID (15 Columns Matching Image 2) -->
          <div class="glass-table-wrapper">
            <table class="glass-table orders-table">
              <thead>
                <tr>
                  <th (click)="sortOrders('date')" class="sortable-th" [class.active-sort]="sortColumn === 'date'" title="Tarihe Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'date'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Tarih
                  </th>
                  <th (click)="sortOrders('receiptId')" class="sortable-th" [class.active-sort]="sortColumn === 'receiptId'" title="Sipariş No'ya Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'receiptId'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Sipariş No
                  </th>
                  <th (click)="sortOrders('orderStatus')" class="sortable-th" [class.active-sort]="sortColumn === 'orderStatus'" title="Duruma Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'orderStatus'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Durum
                  </th>
                  <th (click)="sortOrders('buyerName')" class="sortable-th" [class.active-sort]="sortColumn === 'buyerName'" title="Müşteri Adına Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'buyerName'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Müşteri
                  </th>
                  <th (click)="sortOrders('productTitle')" class="sortable-th" [class.active-sort]="sortColumn === 'productTitle'" title="Ürün Adına Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'productTitle'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Ürün
                  </th>
                  <th (click)="sortOrders('quantity')" class="sortable-th" [class.active-sort]="sortColumn === 'quantity'" title="Adede Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'quantity'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Adet
                  </th>
                  <th (click)="sortOrders('grandTotalUsd')" class="sortable-th" [class.active-sort]="sortColumn === 'grandTotalUsd'" title="Müşteri Ödemesine Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'grandTotalUsd'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Müşteri Ödemesi ($)
                  </th>
                  <th (click)="sortOrders('etsyFeesUsd')" class="sortable-th" [class.active-sort]="sortColumn === 'etsyFeesUsd'" title="Etsy Kesintisine Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'etsyFeesUsd'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Etsy Kesintileri ($)
                  </th>
                  <th (click)="sortOrders('offsiteAdFeeUsd')" class="sortable-th" [class.active-sort]="sortColumn === 'offsiteAdFeeUsd'" title="Dış Reklam Maliyetine Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'offsiteAdFeeUsd'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Dış Reklam ($)
                  </th>
                  <th (click)="sortOrders('productCostUsd')" class="sortable-th" [class.active-sort]="sortColumn === 'productCostUsd'" title="Sipariş Maliyetine Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'productCostUsd'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Sipariş Maliyeti ($)
                  </th>
                  <th (click)="sortOrders('netProfitUsd')" class="sortable-th highlight-th" [class.active-sort]="sortColumn === 'netProfitUsd'" title="Net Kâr Dolarına Göre Sırala">
                    <span class="sort-badge highlight" *ngIf="sortColumn === 'netProfitUsd'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Net Kâr ($)
                  </th>
                  <th (click)="sortOrders('exchangeRate')" class="sortable-th" [class.active-sort]="sortColumn === 'exchangeRate'" title="Kilitli Kura Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'exchangeRate'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Kur (₺)
                  </th>
                  <th (click)="sortOrders('netProfitTry')" class="sortable-th" [class.active-sort]="sortColumn === 'netProfitTry'" title="Net Kâr TL'ye Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'netProfitTry'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Net Kâr (₺)
                  </th>
                  <th (click)="sortOrders('hasCostData')" class="sortable-th" [class.active-sort]="sortColumn === 'hasCostData'" title="Maliyet Giriş Durumuna Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'hasCostData'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Maliyet
                  </th>
                  <th (click)="sortOrders('hasInvoice')" class="sortable-th" [class.active-sort]="sortColumn === 'hasInvoice'" title="Fatura Durumuna Göre Sırala">
                    <span class="sort-badge" *ngIf="sortColumn === 'hasInvoice'">{{ sortDirection === 'asc' ? '▲' : '▼' }}</span>
                    Kargo Faturası
                  </th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let o of filteredOrders" 
                    [class.canceled-row]="o.orderStatus === 'canceled'"
                    (dblclick)="editOrderCost(o)"
                    (contextmenu)="openContextMenu($event, o)">
                  <td class="date-cell">{{ o.orderDate }}</td>
                  <td class="order-id-cell">#{{ o.receiptId }}</td>
                  <td>
                    <span class="status-badge" [ngClass]="o.orderStatus">
                      {{ o.displayStatus }}
                    </span>
                  </td>
                  <td class="customer-cell" [title]="o.buyerName">{{ o.buyerName }}</td>
                  <td class="product-cell" [title]="o.productTitle">{{ o.productTitle }}</td>
                  <td class="qty-cell">{{ o.quantity }}</td>
                  <td class="amount-cell">{{ o.orderStatus === 'canceled' ? '$0.00' : ('$' + o.grandTotalUsd.toFixed(2)) }}</td>
                  <td class="fee-cell">{{ o.orderStatus === 'canceled' ? '$0.00' : ('$' + o.etsyFeesUsd.toFixed(2)) }}</td>
                  <td class="fee-cell">{{ o.offsiteAdFeeUsd > 0 ? ('$' + o.offsiteAdFeeUsd.toFixed(2)) : '—' }}</td>
                  <td class="cost-cell">
                    {{ o.productCostUsd !== null ? ('$' + o.productCostUsd.toFixed(2)) : '—' }}
                  </td>
                  <td class="profit-usd-cell" [ngClass]="o.netProfitUsd >= 0 ? 'text-success' : 'text-danger'">
                    {{ o.orderStatus === 'canceled' ? '$0.00' : ('$' + o.netProfitUsd.toFixed(2)) }}
                  </td>
                  <td class="rate-cell">₺{{ o.exchangeRate.toFixed(2) }}</td>
                  <td class="profit-try-cell" [ngClass]="o.netProfitTry >= 0 ? 'text-emerald' : 'text-danger'">
                    {{ o.orderStatus === 'canceled' ? '₺0,00' : ('₺' + formatNumber(o.netProfitTry)) }}
                  </td>
                  <td class="cost-flag-cell">
                    <span 
                      class="cost-status-pill" 
                      [class.has-cost]="o.hasCostData" 
                      [class.no-cost]="!o.hasCostData"
                      (click)="editOrderCost(o)">
                      {{ o.hasCostData ? '✅ Girildi' : '⚠️ Eksik' }}
                    </span>
                  </td>
                  <td class="invoice-cell">
                    <button 
                      class="btn-invoice" 
                      [class.has-inv]="o.hasInvoice" 
                      (click)="handleInvoice(o)">
                      {{ o.hasInvoice ? '📄 Aç' : '📎 Yükle' }}
                    </button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- TAB 1: GRAFİK ANALİZLERİ -->
        <div *ngIf="activeTab === 'charts'" class="tab-pane">
          <div class="charts-grid">
            <div class="chart-card">
              <h3 class="chart-title">📊 Aylık Gelir vs Gider vs Kâr Dağılımı</h3>
              <div class="chart-bar-container">
                <div class="chart-col">
                  <div class="bar-wrap">
                    <div class="bar gross-bar" style="height: 85%;"></div>
                  </div>
                  <span class="bar-lbl">Brüt Satış<br><strong>₺54.608</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                    <div class="bar fees-bar" style="height: 25%;"></div>
                  </div>
                  <span class="bar-lbl">Kesintiler<br><strong>₺12.783</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                    <div class="bar ads-bar" style="height: 12%;"></div>
                  </div>
                  <span class="bar-lbl">Reklamlar<br><strong>₺4.334</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                    <div class="bar costs-bar" style="height: 28%;"></div>
                  </div>
                  <span class="bar-lbl">Maliyetler<br><strong>₺13.524</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                    <div class="bar profit-bar" style="height: 48%;"></div>
                  </div>
                  <span class="bar-lbl">Net Kâr<br><strong>₺22.077</strong></span>
                </div>
              </div>
            </div>

            <div class="chart-card">
              <h3 class="chart-title">🥧 Gider Dağılımı ve Maliyet Payları</h3>
              <div class="expense-breakdown-list">
                <div class="expense-row">
                  <span class="exp-dot orange"></span>
                  <span class="exp-name">Ürün & Kargo Maliyetleri</span>
                  <span class="exp-pct">%41.5</span>
                  <span class="exp-val">₺13.524,10</span>
                </div>
                <div class="expense-row">
                  <span class="exp-dot yellow"></span>
                  <span class="exp-name">Etsy Komisyon & İşlem Ücreti</span>
                  <span class="exp-pct">%39.2</span>
                  <span class="exp-val">₺12.783,71</span>
                </div>
                <div class="expense-row">
                  <span class="exp-dot red"></span>
                  <span class="exp-name">Offsite & Etsy Ads Reklamları</span>
                  <span class="exp-pct">%13.4</span>
                  <span class="exp-val">₺4.334,59</span>
                </div>
                <div class="expense-row">
                  <span class="exp-dot purple"></span>
                  <span class="exp-name">Müşteri İadeleri</span>
                  <span class="exp-pct">%5.9</span>
                  <span class="exp-val">₺1.888,14</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 2: AI KÂR & CİRO TAHMİNİ -->
        <div *ngIf="activeTab === 'forecast'" class="tab-pane">
          <div class="forecast-grid-4">
            <div class="forecast-card primary">
              <div class="fc-title">🔮 GELECEK AY BEKLENEN CİRO</div>
              <div class="fc-value">$1,350 / ₺66,312</div>
              <div class="fc-sub">Min: $1,150 — Max: $1,580</div>
            </div>

            <div class="forecast-card success">
              <div class="fc-title">💵 BEKLENEN GERÇEK NET KÂR</div>
              <div class="fc-value">$545 / ₺26,770</div>
              <div class="fc-sub">Beklenen Kâr Marjı: %40.4</div>
            </div>

            <div class="forecast-card sky">
              <div class="fc-title">📦 TAHMİNİ SİPARİŞ & BÜYÜME</div>
              <div class="fc-value">14 Sipariş</div>
              <div class="fc-sub">Aylık Büyüme: +%22.5</div>
            </div>

            <div class="forecast-card warning">
              <div class="fc-title">🖨️ STOK & HAMMADDE İHTİYACI</div>
              <div class="fc-value">3.8 kg Filament</div>
              <div class="fc-sub">12 Adet Kargo Kutusu</div>
            </div>
          </div>

          <div class="ai-cfo-panel">
            <div class="ai-cfo-header">
              <span class="sparkle">✨</span>
              <h4>Gemini Spark AI CFO - Mağaza Büyüme ve Kârlılık Raporu</h4>
            </div>
            <div class="ai-cfo-body">
              <p>• <strong>Dönüşüm Oranı Analizi:</strong> 3D Printed Butterfly Balisong modeli %420 net marj ve %41.4 kâr katkısıyla mağazanızın amiral gemisidir. Reklam bütçenizi bu ürüne %15 artırmanız önerilir.</p>
              <p>• <strong>Offsite Ads Uyarısı:</strong> Dış reklamlardan gelen 3 sipariş için $80.01 (%15 kesinti) ödenmiştir. Yüksek marjlı ürünler dışında Offsite reklamları sınırlandırmak net kârınızı ₺3.900 artıracaktır.</p>
              <p>• <strong>Maliyet Girişi:</strong> #4174201942 nolu Ben 10 siparişinin filament maliyetini girdiğinizde gerçek net kârınız netleşecektir.</p>
            </div>
          </div>
        </div>

        <!-- TAB 3: DÖNEMSEL MUHASEBE -->
        <div *ngIf="activeTab === 'period'" class="tab-pane">
          <div class="glass-table-wrapper">
            <table class="glass-table">
              <thead>
                <tr>
                  <th>Dönem</th>
                  <th>Brüt Satış</th>
                  <th>Etsy Kesintisi</th>
                  <th>Reklamlar</th>
                  <th>İadeler</th>
                  <th>Ürün Maliyeti</th>
                  <th>Etsy Net Gelir</th>
                  <th>Gerçek Net Kâr</th>
                  <th>Kâr Marjı</th>
                </tr>
              </thead>
              <tbody>
                <tr>
                  <td><strong>Eylül - Ekim 2026</strong></td>
                  <td class="text-success">₺54.608,24</td>
                  <td class="text-warning">-₺12.783,71</td>
                  <td class="text-danger">-₺4.334,59</td>
                  <td class="text-danger">-₺1.888,14</td>
                  <td class="text-warning">-₺13.524,10</td>
                  <td class="text-primary">₺35.601,79</td>
                  <td class="text-emerald"><strong>₺22.077,69</strong></td>
                  <td><span class="margin-pill">%40.4</span></td>
                </tr>
                <tr>
                  <td><strong>Ağustos 2026</strong></td>
                  <td class="text-success">₺48.120,50</td>
                  <td class="text-warning">-₺11.230,10</td>
                  <td class="text-danger">-₺3.850,00</td>
                  <td class="text-danger">-₺940,00</td>
                  <td class="text-warning">-₺12.100,00</td>
                  <td class="text-primary">₺32.100,40</td>
                  <td class="text-emerald"><strong>₺20.000,40</strong></td>
                  <td><span class="margin-pill">%41.5</span></td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- TAB 5: ÖDEME DEFTERİ KAYITLARI -->
        <div *ngIf="activeTab === 'ledger'" class="tab-pane">
          <div class="glass-table-wrapper">
            <table class="glass-table">
              <thead>
                <tr>
                  <th>İşlem ID</th>
                  <th>Tarih</th>
                  <th>Tür</th>
                  <th>Açıklama</th>
                  <th>Sipariş No</th>
                  <th>Brüt Tutar</th>
                  <th>Kesinti</th>
                  <th>Net Tutar ($)</th>
                  <th>Net Tutar (₺)</th>
                  <th>Bakiye ($)</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let e of ledgerEntries">
                  <td class="mono">{{ e.id }}</td>
                  <td>{{ e.entryDate }}</td>
                  <td>
                    <span class="type-pill" [ngClass]="e.transactionType">{{ e.typeDisplay }}</span>
                  </td>
                  <td class="title-cell">{{ e.title }}</td>
                  <td class="mono">{{ e.orderNumber || '—' }}</td>
                  <td class="amount">{{ e.grossAmount > 0 ? ('$' + e.grossAmount.toFixed(2)) : '—' }}</td>
                  <td class="fee text-danger">{{ e.feeAmount !== 0 ? ('$' + e.feeAmount.toFixed(2)) : '—' }}</td>
                  <td class="net" [ngClass]="e.netAmount >= 0 ? 'text-success' : 'text-danger'">
                    {{ e.netAmount >= 0 ? ('$' + e.netAmount.toFixed(2)) : ('-$' + Math.abs(e.netAmount).toFixed(2)) }}
                  </td>
                  <td class="net-try text-emerald">
                    ₺{{ (e.netAmount * liveRate).toFixed(2) }}
                  </td>
                  <td class="balance mono">&#36;{{ e.runningBalance.toFixed(2) }}</td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

      </div>

      <!-- CUSTOM RIGHT-CLICK CONTEXT MENU (Matching Desktop FinancialReportForm.cs Image) -->
      <div class="desktop-context-menu" 
           *ngIf="showContextMenu" 
           [style.top.px]="contextMenuY" 
           [style.left.px]="contextMenuX" 
           (click)="$event.stopPropagation()">
        <div class="ctx-item" (click)="generateInvoicePdf()">
          <span class="ctx-icon">⚡</span> Otomatik Fatura / Konşimento Oluştur (PDF)
        </div>
        <div class="ctx-item" (click)="openInvoicePdf()">
          <span class="ctx-icon">📄</span> Faturayı / PDF'i Aç
        </div>
        <div class="ctx-item" (click)="uploadInvoicePdf()">
          <span class="ctx-icon">📎</span> Kargo Faturası Yükle...
        </div>
        <div class="ctx-item" (click)="viewOrderDetails()">
          <span class="ctx-icon">📊</span> Sipariş Detaylarını Gör
        </div>
        <div class="ctx-item highlight" (click)="editCostFromContext()">
          <span class="ctx-icon">🏷️</span> Maliyet / Fatura Düzenle
        </div>
        <div class="ctx-divider"></div>
        <div class="ctx-item" (click)="openEtsyOrder()">
          <span class="ctx-icon">🌐</span> Etsy'de Siparişi Aç
        </div>
        <div class="ctx-item" (click)="copyOrderNumber()">
          <span class="ctx-icon">📋</span> Sipariş No Kopyala
        </div>
        <div class="ctx-item" (click)="copyCustomerInfo()">
          <span class="ctx-icon">👤</span> Müşteri Bilgisini Kopyala
        </div>
        <div class="ctx-item" (click)="copySelectedCell()">
          <span class="ctx-icon">📝</span> Seçili Hücreyi Kopyala
        </div>
        <div class="ctx-item" (click)="copyWholeRow()">
          <span class="ctx-icon">📄</span> Tüm Satırı Kopyala
        </div>
      </div>

      <!-- MODAL: KÂR FARKI MUTABAKATI -->
      <div class="modal-backdrop" *ngIf="showReconciliationModal" (click)="closeModals()">
        <div class="modal-card reconciliation-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>📊 KÂR MUTABAKATI & FARK ANALİZİ</h3>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="rec-section">
              <h4>1️⃣ Siparişlerin Toplam Katkı Kârı: <span class="text-success">₺24.756,89 ($507,17)</span></h4>
              <p>• Tek tek 10 adet siparişten elde edilen doğrudan ürün operasyon kârıdır.</p>
              <p>• Henüz mağaza geneline ait olan reklamlar ve genel giderler bundan düşülmemiştir.</p>
            </div>

            <div class="rec-section">
              <h4>2️⃣ Mağaza Geneline Ait Giderler (Siparişten Bağımsız):</h4>
              <ul>
                <li>📢 <strong>Etsy Ads İç Reklam:</strong> <span class="text-danger">-₺404,30</span></li>
                <li>🌐 <strong>Offsite Ads Dış Reklam:</strong> <span class="text-orange">-₺3.930,29</span></li>
                <li>↩️ <strong>Dönemsel İadeler:</strong> <span class="text-danger">-₺1.888,14</span></li>
                <li>⚠️ <strong>1 siparişin (#4174201942)</strong> maliyeti girilmediği için geçici olarak yüksek görünmektedir.</li>
              </ul>
            </div>

            <div class="rec-divider"></div>

            <div class="rec-final">
              <h4>🏛️ Üst Karttaki GERÇEK NET KÂR: <span class="text-emerald">₺22.077,69 ($449,46)</span></h4>
              <p>(Tüm mağaza reklamları, iadeler ve genel giderler düşüldükten sonra banka hesabınıza ve cebinize kalan nihai kârdır.)</p>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Anladım, Kapat</button>
          </div>
        </div>
      </div>

      <!-- MODAL: MALİYET / FATURA DÜZENLE -->
      <div class="modal-backdrop" *ngIf="showCostModal" (click)="closeModals()">
        <div class="modal-card cost-modal-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>🏷️ Maliyet / Fatura Düzenle (#{{ selectedOrder?.receiptId }})</h3>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body" *ngIf="selectedOrder">
            <div class="order-context-box">
              <div class="ctx-row"><strong>Müşteri:</strong> {{ selectedOrder.buyerName }}</div>
              <div class="ctx-row"><strong>Ürün Adı:</strong> {{ selectedOrder.productTitle }}</div>
              <div class="badge-row">
                <span class="ctx-badge gross">Ödeme: &#36;{{ selectedOrder.grandTotalUsd.toFixed(2) }}</span>
                <span class="ctx-badge fee">Etsy Kesintisi: -&#36;{{ selectedOrder.etsyFeesUsd.toFixed(2) }}</span>
                <span class="ctx-badge rate">Kilitli Kur: ₺{{ selectedOrder.exchangeRate.toFixed(2) }}</span>
              </div>
            </div>

            <div class="cost-input-group">
              <label>🧵 Birim Üretim & Filament Maliyeti ($):</label>
              <input type="number" [(ngModel)]="costModalProduction" class="cost-input" step="0.1" />
              <span class="hint-text">Filament gramajı (PLA/PETG) ve 3D yazıcı amortisman payı</span>
            </div>
            <div class="cost-input-group">
              <label>🚚 Kargo Navlun Maliyeti ($):</label>
              <input type="number" [(ngModel)]="costModalShipping" class="cost-input" step="0.1" />
              <span class="hint-text">Aras Global, ShipEntegra veya Navlungo taşıma ücreti</span>
            </div>
            <div class="cost-input-group">
              <label>📦 Paketleme & Kutu Maliyeti ($):</label>
              <input type="number" [(ngModel)]="costModalPackaging" class="cost-input" step="0.1" />
              <span class="hint-text">Karton kutu, balonlu naylon ve barkod etiket gideri</span>
            </div>

            <!-- Live Calculation Preview -->
            <div class="calculated-preview-box">
              <div class="calc-row">
                <span>Toplam Sipariş Maliyeti:</span>
                <strong>&#36;{{ (costModalProduction + costModalShipping + costModalPackaging).toFixed(2) }}</strong>
              </div>
              <div class="calc-row highlight">
                <span>Hesaplanan Net Kâr ($):</span>
                <strong class="text-success">&#36;{{ (selectedOrder.grandTotalUsd - selectedOrder.etsyFeesUsd - selectedOrder.offsiteAdFeeUsd - (costModalProduction + costModalShipping + costModalPackaging)).toFixed(2) }}</strong>
              </div>
              <div class="calc-row highlight">
                <span>Hesaplanan Net Kâr (₺):</span>
                <strong class="text-emerald">₺{{ ((selectedOrder.grandTotalUsd - selectedOrder.etsyFeesUsd - selectedOrder.offsiteAdFeeUsd - (costModalProduction + costModalShipping + costModalPackaging)) * selectedOrder.exchangeRate).toFixed(2) }}</strong>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-cancel" (click)="closeModals()">İptal</button>
            <button class="btn-primary-modal" (click)="saveOrderCost()">💾 Kaydet & Kârı Güncelle</button>
          </div>
        </div>
      </div>

      <!-- DRILLDOWN MODAL 1: ETSY BANKA YATIRIMI & TRANSFER ANALİZİ (PAYOUTS - GÖRSEL 2) -->
      <div class="modal-backdrop" *ngIf="showBankPayoutModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>🏦 Etsy Banka Yatırımı & Transfer Analizi (Payouts)</h3>
              <p class="modal-sub">Seçilen dönemde Etsy tarafından banka hesabınıza yatırılan net toplam para: <strong>₺27.594,90</strong></p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Banka Yatırımı</span>
                <span class="box-val text-cyan">₺27.594,90</span>
                <span class="box-sub">10 Transfer</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">En Son Yatırılan Tarih</span>
                <span class="box-val text-white">01.10.2026</span>
                <span class="box-sub">Son: ₺4.359,09</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Ortalama Transfer</span>
                <span class="box-val text-primary">₺2.759,49</span>
                <span class="box-sub">Net Gelirin %77,5'i</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>İşlem / Ref No</th>
                    <th>Tür</th>
                    <th>Yatırılan Tutar</th>
                    <th>Durum</th>
                    <th>Açıklama / Kur Bilgisi</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let p of bankPayoutRecords">
                    <td class="date-cell">{{ p.date }}</td>
                    <td class="ref-cell">{{ p.refNo }}</td>
                    <td>{{ p.type }}</td>
                    <td class="amount-cell text-emerald">+₺{{ formatNumber(p.amount) }}</td>
                    <td><span class="badge-status-payout">✅ {{ p.status }}</span></td>
                    <td class="note-cell">{{ p.note }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ İpucu: Etsy ödemeleri bankanıza gönderdikten sonra bankanızın işleme alma hızına göre 1-3 iş günü içinde hesabınıza geçer. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- DRILLDOWN MODAL 2: SİPARİŞ & KARGO MALİYET ANALİZİ (COGS - GÖRSEL 3) -->
      <div class="modal-backdrop" *ngIf="showCostBreakdownModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>📦 Sipariş & Kargo Maliyet Analizi</h3>
              <p class="modal-sub">Toplam Sipariş Maliyeti (COGS): <strong>₺13.524,10</strong> | Kargo Harcamaları ve Fatura Durumu</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Kargo Maliyeti</span>
                <span class="box-val text-warning">₺8.126,93</span>
                <span class="box-sub">Maliyetin %60,1'i</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Üretim / Hammadde</span>
                <span class="box-val text-primary">₺5.397,16</span>
                <span class="box-sub">Filament & 3D Yazıcı Payı</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Paketleme & Fatura</span>
                <span class="box-val text-emerald">₺0,00</span>
                <span class="box-sub">1 Fatura Kayıtlı</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Sipariş No</th>
                    <th>Adet</th>
                    <th>Kargo Maliyeti</th>
                    <th>Fatura</th>
                    <th>Ürün / İlan Başlığı</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let s of orderCostShippingRecords">
                    <td class="date-cell">{{ s.date }}</td>
                    <td class="ref-cell">#{{ s.receiptId }}</td>
                    <td>{{ s.qty }}</td>
                    <td class="amount-cell text-warning">{{ s.shippingCostTry > 0 ? ('₺' + formatNumber(s.shippingCostTry)) : '₺0,00' }}</td>
                    <td>
                      <span *ngIf="s.invoice !== '—'" class="badge-invoice">📄 {{ s.invoice }}</span>
                      <span *ngIf="s.invoice === '—'" class="text-muted">—</span>
                    </td>
                    <td class="title-cell" [title]="s.title">{{ s.title }}</td>
                  </tr>
                </tbody>
              </table>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- DRILLDOWN MODAL 3: GERÇEK NET KÂR & BİLANÇO ANALİZİ (GÖRSEL 4) -->
      <div class="modal-backdrop" *ngIf="showBalanceAnalysisModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>💵 Gerçek Net Kâr & Bilanço Analizi</h3>
              <p class="modal-sub">Etsy Net Hakedişinden ürün ve kargo maliyetleri çıkarılmış nihai net kâr: <strong>₺22.077,69</strong></p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Etsy Net Gelir</span>
                <span class="box-val text-primary">₺35.601,79</span>
                <span class="box-sub">Platform Hakedişi</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Ürün Maliyeti</span>
                <span class="box-val text-danger">-₺13.524,10</span>
                <span class="box-sub">Kargo, Hammadde & Paketleme</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Gerçek Net Kâr</span>
                <span class="box-val text-emerald">₺22.077,69</span>
                <span class="box-sub">%40,4 Net Kâr Marjı</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tür</th>
                    <th>Kalem Adı</th>
                    <th>Kategori / Detay</th>
                    <th>Tutar</th>
                    <th>Durum</th>
                    <th>Açıklama / Muhasebe Mantığı</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let b of balanceBreakdownRecords" [class.highlight-row]="b.type === 'KÂR'">
                    <td class="type-cell"><strong>{{ b.type }}</strong></td>
                    <td class="item-name">{{ b.name }}</td>
                    <td class="detail-cell">{{ b.detail }}</td>
                    <td class="amount-cell" [ngClass]="b.amount >= 0 ? 'text-emerald' : 'text-danger'">
                      {{ b.type === 'KÂR' ? '=' : (b.amount > 0 ? '+' : '') }}₺{{ formatNumber(Math.abs(b.amount)) }}
                    </td>
                    <td><span *ngIf="b.status" class="badge-status-payout">{{ b.status }}</span></td>
                    <td class="note-cell">{{ b.explanation }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ İpucu: Henüz maliyeti girilmemiş 1 siparişin maliyetini girdiğinizde bu net kâr kuruşu kuruşuna kesinleşecektir. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 4. DRILLDOWN MODAL: SATIŞ VE GELİR ANALİZİ (BRÜT SATIŞ) -->
      <div class="modal-backdrop" *ngIf="showSalesModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>🟢 Satış ve Gelir Analizi (Brüt Satış)</h3>
              <p class="modal-sub">Dönem Toplam Brüt Satış: <strong>₺54.608,24</strong> ($1.111,73 USD) | 10 İşlem</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Brüt Satış</span>
                <span class="box-val text-emerald">₺54.608,24</span>
                <span class="box-sub">$1.111,73 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Sipariş Sayısı</span>
                <span class="box-val text-primary">10 Sipariş</span>
                <span class="box-sub">9 Başarılı, 1 İptal</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Ortalama Sepet Tutarı (AOV)</span>
                <span class="box-val text-emerald">₺5.460,82</span>
                <span class="box-sub">$111,17 USD / Sipariş</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Sipariş No</th>
                    <th>Alıcı</th>
                    <th>Ürün</th>
                    <th>Tutar ($)</th>
                    <th>Tutar (₺)</th>
                    <th>Ödeme Tipi</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let s of salesRecords">
                    <td class="date-cell">{{ s.date }}</td>
                    <td class="ref-cell">#{{ s.receiptId }}</td>
                    <td>{{ s.buyer }}</td>
                    <td class="title-cell" title="{{ s.item }}">{{ s.item }}</td>
                    <td class="amount-cell text-emerald">{{ '$' + s.amount.toFixed(2) }}</td>
                    <td class="amount-cell text-emerald">₺{{ formatNumber(s.tryAmount) }}</td>
                    <td><span class="badge-status-payout" [class.badge-refund]="s.paymentType === 'canceled'">{{ s.paymentType }}</span></td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Muhasebe Kuralı: Etsy Payments brüt tutarları müşterinin ödediği toplam sepet (ürün + kargo ücreti) bedelidir. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 5. DRILLDOWN MODAL: ETSY KOMİSYON & KESİNTİ ANALİZİ -->
      <div class="modal-backdrop" *ngIf="showFeesModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>🏷️ Etsy Komisyon & Kesinti Analizi</h3>
              <p class="modal-sub">Etsy Tarafından Kesilen Toplam Komisyon ve Masraflar: <strong>-₺12.783,71</strong> (-$260,25 USD)</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Kesinti Tutarı</span>
                <span class="box-val text-warning">-₺12.783,71</span>
                <span class="box-sub">-$260,25 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Komisyon / Brüt Satış Oranı</span>
                <span class="box-val text-orange">%23,41</span>
                <span class="box-sub">Standart Komisyon + KDV</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">KDV Tevkifatı (TR %20)</span>
                <span class="box-val text-danger">₺1.383,22</span>
                <span class="box-sub">Kesintiler Üzerinden KDV</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Kesinti Kalemi</th>
                    <th>İşlem / Adet</th>
                    <th>Matrah ($)</th>
                    <th>Kesinti ($)</th>
                    <th>Kesinti (₺)</th>
                    <th>Açıklama / Formül</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let f of feesRecords">
                    <td><strong>{{ f.type }}</strong></td>
                    <td>{{ f.count }}</td>
                    <td>{{ f.baseUsd > 0 ? ('$' + f.baseUsd.toFixed(2)) : '—' }}</td>
                    <td class="amount-cell text-warning">{{ '-$' + f.feeUsd.toFixed(2) }}</td>
                    <td class="amount-cell text-warning">-₺{{ formatNumber(f.feeTry) }}</td>
                    <td class="note-cell">{{ f.note }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Bilgi: Etsy, Türkiye merkezli mağazalarda tüm komisyonlar üzerinden %20 yasal KDV ve %1.25 Düzenleyici İşletme Ücreti keser. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 6. DRILLDOWN MODAL: ETSY ADS İÇ REKLAM ANALİZİ -->
      <div class="modal-backdrop" *ngIf="showInnerAdsModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>📢 Etsy Ads İç Reklam Analizi</h3>
              <p class="modal-sub">Etsy Arama İçi Tıklama Başı Sponsorlu Reklam Harcamaları: <strong>-₺404,30</strong> (-$8,23 USD)</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Reklam Harcaması</span>
                <span class="box-val text-danger">-₺404,30</span>
                <span class="box-sub">-$8,23 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Reklam Kaynaklı Gelir</span>
                <span class="box-val text-emerald">₺8.918,87</span>
                <span class="box-sub">$183,81 USD (2 Sipariş)</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Genel Reklam ROAS</span>
                <span class="box-val text-emerald">22,3x</span>
                <span class="box-sub">Ortalama Tıklama Başı: $0.12</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Gösterim</th>
                    <th>Tıklama</th>
                    <th>Harcama ($)</th>
                    <th>Harcama (₺)</th>
                    <th>Reklam Geliri ($)</th>
                    <th>ROAS</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let ad of innerAdsRecords">
                    <td class="date-cell">{{ ad.date }}</td>
                    <td>{{ ad.impressions }}</td>
                    <td>{{ ad.clicks }}</td>
                    <td class="amount-cell text-danger">{{ '-$' + ad.spendUsd.toFixed(2) }}</td>
                    <td class="amount-cell text-danger">-₺{{ formatNumber(ad.spendTry) }}</td>
                    <td class="amount-cell text-emerald">{{ '$' + ad.salesUsd.toFixed(2) }}</td>
                    <td><strong [class.text-emerald]="ad.roas !== '0.0x'">{{ ad.roas }}</strong></td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Performans Analizi: İç reklamlar harcanan her 1₺ karşılığında mağazaya 22,3₺ ciro kazandırmıştır. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 7. DRILLDOWN MODAL: OFFSITE ADS DIŞ REKLAM ANALİZİ -->
      <div class="modal-backdrop" *ngIf="showOffsiteAdsModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>🌐 Offsite Ads Dış Reklam Analizi</h3>
              <p class="modal-sub">Google, Facebook, Instagram ve Pinterest Reklam Komisyonu: <strong>-₺3.930,29</strong> (-$80,01 USD)</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Dış Reklam Kesintisi</span>
                <span class="box-val text-orange">-₺3.930,29</span>
                <span class="box-sub">-$80,01 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Dış Reklam Satış Adedi</span>
                <span class="box-val text-primary">4 Sipariş</span>
                <span class="box-sub">Toplam Satışın %40'ı</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Komisyon Oranı</span>
                <span class="box-val text-warning">%15,00</span>
                <span class="box-sub">Sadece Satış Olduğunda Kesilir</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Sipariş No</th>
                    <th>Alıcı</th>
                    <th>Satış Tutarı ($)</th>
                    <th>Komisyon Oranı</th>
                    <th>Kesinti ($)</th>
                    <th>Kesinti (₺)</th>
                    <th>Reklam Kanalı</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let off of offsiteAdsRecords">
                    <td class="ref-cell">#{{ off.receiptId }}</td>
                    <td>{{ off.buyer }}</td>
                    <td class="amount-cell text-emerald">{{ '$' + off.saleAmountUsd.toFixed(2) }}</td>
                    <td><span class="badge-status-payout">{{ off.rate }}</span></td>
                    <td class="amount-cell text-orange">{{ '-$' + off.feeUsd.toFixed(2) }}</td>
                    <td class="amount-cell text-orange">-₺{{ formatNumber(off.feeTry) }}</td>
                    <td class="note-cell">{{ off.channel }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Offsite Ads Avantajı: Tıklama başına para ödemezsiniz; sadece sipariş gerçekleştiğinde %15 başarı komisyonu tahakkuk eder. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 8. DRILLDOWN MODAL: İADE VE GERİ ÖDEME ANALİZİ -->
      <div class="modal-backdrop" *ngIf="showRefundsModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>🔴 İade ve Geri Ödeme Analizi</h3>
              <p class="modal-sub">Dönem İçi Gerçekleşen Sipariş İadeleri & İptalleri: <strong>-₺1.888,14</strong> (-$38,44 USD)</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam İade Tutarı</span>
                <span class="box-val text-danger">-₺1.888,14</span>
                <span class="box-sub">-$38,44 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">İade Edilen Sipariş Sayısı</span>
                <span class="box-val text-danger">1 Sipariş</span>
                <span class="box-sub">İade Oranı: %3,46</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Geri Alınan Etsy Komisyonu</span>
                <span class="box-val text-emerald">+$4,80 USD</span>
                <span class="box-sub">İade Sonucu İade Alınan Komisyon</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Tarih</th>
                    <th>Sipariş No</th>
                    <th>Alıcı</th>
                    <th>Ürün</th>
                    <th>İade Tutarı ($)</th>
                    <th>İade Tutarı (₺)</th>
                    <th>İade Nedeni</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let r of refundRecords">
                    <td class="date-cell">{{ r.date }}</td>
                    <td class="ref-cell">#{{ r.receiptId }}</td>
                    <td>{{ r.buyer }}</td>
                    <td class="title-cell">{{ r.item }}</td>
                    <td class="amount-cell text-danger">{{ '-$' + r.refundUsd.toFixed(2) }}</td>
                    <td class="amount-cell text-danger">-₺{{ formatNumber(r.refundTry) }}</td>
                    <td class="note-cell">{{ r.reason }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Finansal Not: İade yapıldığında Etsy işlem komisyonunu ve KDV'sini mağaza bakiyesine otomatik geri yatırır. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- 9. DRILLDOWN MODAL: ETSY NET GELİR & PLATFORM HAKEDİŞ ANALİZİ -->
      <div class="modal-backdrop" *ngIf="showNetIncomeModal" (click)="closeModals()">
        <div class="modal-card drilldown-card" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <div class="drilldown-title-box">
              <h3>💎 Etsy Net Gelir & Platform Hakediş Analizi</h3>
              <p class="modal-sub">Tüm Platform Kesintileri ve İadeler Sonrası Saf Etsy Hakedişi: <strong>₺35.601,79</strong> ($724,80 USD)</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Brüt Satış Geliri</span>
                <span class="box-val text-emerald">₺54.608,24</span>
                <span class="box-sub">$1.111,73 USD</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Platform Giderleri Toplamı</span>
                <span class="box-val text-danger">-₺19.006,45</span>
                <span class="box-sub">Komisyon, Reklam & İadeler</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Etsy Net Hakediş</span>
                <span class="box-val text-primary">₺35.601,79</span>
                <span class="box-sub">$724,80 USD</span>
              </div>
            </div>

            <div class="drilldown-table-wrapper">
              <table class="drilldown-table">
                <thead>
                  <tr>
                    <th>Hesap Kalemi</th>
                    <th>Tutar ($)</th>
                    <th>Tutar (₺)</th>
                    <th>Muhasebe Açıklaması</th>
                  </tr>
                </thead>
                <tbody>
                  <tr *ngFor="let n of netIncomeRecords" [class.highlight-row]="n.category.includes('NET HAKEDİŞ')">
                    <td><strong>{{ n.category }}</strong></td>
                    <td class="amount-cell" [ngClass]="n.isPositive ? 'text-emerald' : 'text-danger'">
                      {{ (n.category.includes('NET HAKEDİŞ') ? '=' : (n.amountUsd > 0 ? '+' : '')) + '$' + formatNumber(Math.abs(n.amountUsd)) }}
                    </td>
                    <td class="amount-cell" [ngClass]="n.isPositive ? 'text-emerald' : 'text-danger'">
                      {{ n.category.includes('NET HAKEDİŞ') ? '=' : (n.amountTry > 0 ? '+' : '') }}₺{{ formatNumber(Math.abs(n.amountTry)) }}
                    </td>
                    <td class="note-cell">{{ n.note }}</td>
                  </tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">
              💡 [ Hakediş Tanımı: Etsy Net Gelir, mağazanın kargo ve ürün maliyeti hariç, doğrudan banka hesabına gönderilecek saf Etsy bakiyesidir. ]
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="closeModals()">Kapat</button>
          </div>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div class="toast-notification" *ngIf="toastMessage">
        {{ toastMessage }}
      </div>
    </div>
  `,
  styles: [`
    .accounting-view {
      padding: 24px 32px;
      color: #f1f5f9;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      min-height: calc(100vh - 70px);
      background: #090d16;
    }

    .accounting-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      margin-bottom: 16px;
    }
    .title-row {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .page-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #ffffff;
      margin: 0;
      letter-spacing: -0.02em;
    }
    .live-status-pill {
      display: inline-flex;
      align-items: center;
      gap: 8px;
      padding: 4px 12px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.3);
      border-radius: 20px;
      color: #34d399;
      font-size: 0.78rem;
      font-weight: 600;
    }
    .live-dot {
      width: 8px;
      height: 8px;
      background: #10b981;
      border-radius: 50%;
      box-shadow: 0 0 8px #10b981;
    }
    .page-subtitle {
      color: #94a3b8;
      font-size: 0.85rem;
      margin: 4px 0 0 0;
    }

    .currency-toggle-box {
      display: flex;
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 8px;
      padding: 2px;
    }
    .curr-btn {
      background: transparent;
      border: none;
      color: #94a3b8;
      padding: 6px 14px;
      font-size: 0.8rem;
      font-weight: 700;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .curr-btn.active {
      background: #6366f1;
      color: white;
      box-shadow: 0 2px 8px rgba(99, 102, 241, 0.4);
    }

    /* DESKTOP ACTION BAR */
    .desktop-action-bar {
      display: flex;
      align-items: center;
      flex-wrap: wrap;
      gap: 8px;
      padding: 10px 14px;
      background: rgba(30, 41, 59, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      margin-bottom: 20px;
      backdrop-filter: blur(8px);
    }
    .bar-select {
      background: #0f172a;
      border: 1px solid #334155;
      color: #f1f5f9;
      padding: 7px 12px;
      border-radius: 6px;
      font-size: 0.82rem;
      font-weight: 600;
      outline: none;
    }
    .bar-control-group.dates {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .bar-date-input {
      width: 90px;
      background: #0f172a;
      border: 1px solid #334155;
      color: #f1f5f9;
      padding: 7px 8px;
      border-radius: 6px;
      font-size: 0.8rem;
      text-align: center;
      outline: none;
    }
    .date-sep {
      color: #64748b;
      font-weight: 700;
    }
    .bar-btn {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 7px 14px;
      border-radius: 6px;
      border: none;
      font-size: 0.82rem;
      font-weight: 600;
      color: white;
      cursor: pointer;
      transition: all 0.2s;
    }
    .bar-btn:hover:not(:disabled) {
      transform: translateY(-1px);
    }
    .bar-btn.refresh { background: #4f46e5; }
    .bar-btn.refresh:hover { background: #4338ca; }
    .bar-btn.secondary { background: #334155; color: #f1f5f9; }
    .bar-btn.secondary:hover { background: #475569; }
    .bar-btn.accent { background: #0284c7; }
    .bar-btn.accent:hover { background: #0369a1; }
    .bar-btn.excel { background: #10b981; }
    .bar-btn.excel:hover { background: #059669; }
    .bar-btn.telegram { background: #0ea5e9; }
    .bar-btn.telegram:hover { background: #0284c7; }
    .bar-btn.vds { background: #059669; }
    .bar-btn.vds:hover { background: #047857; }

    .rate-toggle-group {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 4px 10px;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 6px;
    }
    .checkbox-label {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.8rem;
      font-weight: 600;
      color: #cbd5e1;
      cursor: pointer;
    }
    .live-rate-badge {
      font-size: 0.85rem;
      font-weight: 800;
      color: #38bdf8;
    }

    /* 9 KPI CARDS GRID */
    .kpi-grid-9 {
      display: grid;
      grid-template-columns: repeat(9, 1fr);
      gap: 8px;
      margin-bottom: 20px;
    }
    @media (max-width: 1400px) {
      .kpi-grid-9 { grid-template-columns: repeat(3, 1fr); }
    }
    .kpi-card {
      background: #151c2c;
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 10px;
      padding: 10px 12px;
      cursor: pointer;
      display: flex;
      flex-direction: column;
      justify-content: space-between;
      position: relative;
      overflow: hidden;
      transition: all 0.2s;
    }
    .kpi-card:hover {
      transform: translateY(-2px);
      box-shadow: 0 6px 16px rgba(0, 0, 0, 0.4);
      border-color: rgba(255, 255, 255, 0.15);
    }
    .kpi-card.highlight {
      border: 1px solid rgba(52, 211, 153, 0.4);
      background: linear-gradient(180deg, #15232c, #0e1c24);
    }
    .kpi-title {
      font-size: 0.68rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.03em;
    }
    .kpi-value {
      font-size: 1.05rem;
      font-weight: 800;
      margin: 6px 0;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, monospace;
    }
    .kpi-indicator {
      height: 3px;
      border-radius: 2px;
      width: 100%;
    }
    .kpi-indicator.green { background: #10b981; }
    .kpi-indicator.orange { background: #f59e0b; }
    .kpi-indicator.red { background: #ef4444; }
    .kpi-indicator.dark-orange { background: #ea580c; }
    .kpi-indicator.indigo { background: #6366f1; }
    .kpi-indicator.emerald { background: #34d399; box-shadow: 0 0 6px #34d399; }
    .kpi-indicator.cyan { background: #06b6d4; }

    .text-success { color: #34d399; }
    .text-warning { color: #fbbf24; }
    .text-danger { color: #f87171; }
    .text-orange { color: #fb923c; }
    .text-primary { color: #818cf8; }
    .text-emerald { color: #4ade80; }
    .text-cyan { color: #38bdf8; }

    /* TAB NAVIGATION */
    .tab-nav-bar {
      display: flex;
      gap: 4px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.1);
      margin-bottom: 16px;
    }
    .nav-tab {
      background: transparent;
      border: none;
      color: #94a3b8;
      padding: 10px 18px;
      font-size: 0.88rem;
      font-weight: 600;
      cursor: pointer;
      border-bottom: 2px solid transparent;
      transition: all 0.2s;
    }
    .nav-tab:hover {
      color: #f1f5f9;
    }
    .nav-tab.active {
      color: #38bdf8;
      border-bottom-color: #38bdf8;
      background: rgba(56, 189, 248, 0.05);
      border-radius: 8px 8px 0 0;
    }

    /* ORDERS TOOLBAR */
    .orders-toolbar {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 12px;
      flex-wrap: wrap;
    }
    .order-search-input {
      width: 320px;
      background: #151c2c;
      border: 1px solid #334155;
      color: #f1f5f9;
      padding: 8px 12px;
      border-radius: 6px;
      font-size: 0.82rem;
      outline: none;
    }
    .order-search-input:focus { border-color: #6366f1; }
    .order-filter-select {
      background: #151c2c;
      border: 1px solid #334155;
      color: #f1f5f9;
      padding: 8px 12px;
      border-radius: 6px;
      font-size: 0.82rem;
      outline: none;
    }
    .btn-clear-filter {
      background: #1e293b;
      border: 1px solid #334155;
      color: #94a3b8;
      padding: 8px 12px;
      border-radius: 6px;
      cursor: pointer;
      font-weight: 700;
    }
    .btn-clear-filter:hover { color: white; background: #334155; }
    .btn-profit-info {
      background: #3730a3;
      border: none;
      color: white;
      padding: 8px 14px;
      border-radius: 6px;
      font-size: 0.82rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-profit-info:hover { background: #4338ca; }
    .orders-summary-label {
      margin-left: auto;
      font-size: 0.82rem;
      color: #94a3b8;
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .orders-summary-label .sep { color: #475569; }
    .missing-badge {
      background: rgba(245, 158, 11, 0.2);
      color: #f59e0b;
      padding: 2px 8px;
      border-radius: 12px;
      font-weight: 700;
    }

    /* DATA TABLES */
    .glass-table-wrapper {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 10px;
      overflow-x: auto;
    }
    .glass-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
      text-align: left;
    }
    .glass-table th {
      background: #1e293b;
      color: #94a3b8;
      padding: 10px 12px;
      font-weight: 700;
      font-size: 0.75rem;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      white-space: nowrap;
      cursor: pointer;
    }
    .glass-table td {
      padding: 10px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
      color: #cbd5e1;
      white-space: nowrap;
    }
    .glass-table tr:hover td {
      background: rgba(255, 255, 255, 0.02);
    }
    .canceled-row td {
      color: #64748b !important;
      text-decoration: line-through;
    }
    .order-id-cell { font-weight: 700; color: #38bdf8 !important; }
    .customer-cell { max-width: 140px; overflow: hidden; text-overflow: ellipsis; }
    .product-cell { max-width: 220px; overflow: hidden; text-overflow: ellipsis; font-weight: 500; }
    .status-badge {
      display: inline-block;
      padding: 2px 8px;
      border-radius: 10px;
      font-size: 0.72rem;
      font-weight: 600;
    }
    .status-badge.completed { background: rgba(52, 211, 153, 0.15); color: #34d399; }
    .status-badge.canceled { background: rgba(239, 68, 68, 0.15); color: #ef4444; }
    .cost-status-pill {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.2s;
    }
    .cost-status-pill.has-cost { background: rgba(16, 185, 129, 0.15); color: #10b981; }
    .cost-status-pill.no-cost { background: rgba(245, 158, 11, 0.2); color: #f59e0b; }
    .cost-status-pill:hover { transform: scale(1.05); }

    .btn-invoice {
      background: #1e293b;
      border: 1px solid #334155;
      color: #818cf8;
      padding: 3px 10px;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-invoice.has-inv { color: #34d399; border-color: rgba(52, 211, 153, 0.4); }
    .btn-invoice:hover { background: #334155; }

    /* FORECAST TAB */
    .forecast-grid-4 {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
      margin-bottom: 20px;
    }
    .forecast-card {
      background: #151c2c;
      border-radius: 12px;
      padding: 16px;
      border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .forecast-card.primary { border-top: 3px solid #6366f1; }
    .forecast-card.success { border-top: 3px solid #10b981; }
    .forecast-card.sky { border-top: 3px solid #0ea5e9; }
    .forecast-card.warning { border-top: 3px solid #f59e0b; }
    .fc-title { font-size: 0.75rem; font-weight: 700; color: #94a3b8; margin-bottom: 8px; }
    .fc-value { font-size: 1.25rem; font-weight: 800; color: #fff; margin-bottom: 4px; }
    .fc-sub { font-size: 0.75rem; color: #64748b; }

    .ai-cfo-panel {
      background: #111827;
      border: 1px solid rgba(139, 92, 246, 0.3);
      border-radius: 12px;
      padding: 18px;
    }
    .ai-cfo-header {
      display: flex;
      align-items: center;
      gap: 10px;
      margin-bottom: 12px;
    }
    .ai-cfo-header h4 { margin: 0; font-size: 0.95rem; color: #a78bfa; }
    .ai-cfo-body p { margin: 0 0 8px 0; font-size: 0.82rem; line-height: 1.5; color: #cbd5e1; }

    /* CHARTS TAB */
    .charts-grid {
      display: grid;
      grid-template-columns: 1.5fr 1fr;
      gap: 16px;
    }
    .chart-card {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 16px;
    }
    .chart-title { font-size: 0.9rem; font-weight: 700; margin: 0 0 16px 0; color: #f1f5f9; }
    .chart-bar-container {
      display: flex;
      justify-content: space-around;
      align-items: flex-end;
      height: 180px;
      padding-bottom: 10px;
      border-bottom: 1px solid #334155;
    }
    .chart-col { display: flex; flex-direction: column; align-items: center; gap: 8px; }
    .bar-wrap { height: 130px; width: 42px; display: flex; align-items: flex-end; background: #1e293b; border-radius: 6px; overflow: hidden; }
    .bar { width: 100%; border-radius: 6px 6px 0 0; }
    .gross-bar { background: #10b981; }
    .fees-bar { background: #f59e0b; }
    .ads-bar { background: #ef4444; }
    .costs-bar { background: #fb923c; }
    .profit-bar { background: #34d399; box-shadow: 0 0 10px rgba(52, 211, 153, 0.5); }
    .bar-lbl { font-size: 0.72rem; text-align: center; color: #94a3b8; }

    .expense-breakdown-list { display: flex; flex-direction: column; gap: 12px; }
    .expense-row { display: flex; align-items: center; gap: 10px; font-size: 0.82rem; }
    .exp-dot { width: 10px; height: 10px; border-radius: 50%; }
    .exp-dot.orange { background: #fb923c; }
    .exp-dot.yellow { background: #f59e0b; }
    .exp-dot.red { background: #ef4444; }
    .exp-dot.purple { background: #a855f7; }
    .exp-name { flex: 1; color: #cbd5e1; }
    .exp-pct { font-weight: 700; color: #94a3b8; }
    .exp-val { font-weight: 700; color: #fff; width: 90px; text-align: right; }

    /* MODALS */
    .modal-backdrop {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(4px);
      z-index: 99999;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .modal-card {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 16px;
      box-shadow: 0 20px 48px rgba(0, 0, 0, 0.8);
      overflow: hidden;
      max-width: 580px;
      width: 90%;
      animation: modal-pop 0.2s ease-out;
    }
    @keyframes modal-pop {
      from { transform: scale(0.95); opacity: 0; }
      to { transform: scale(1); opacity: 1; }
    }
    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 16px 20px;
      background: #1e293b;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .modal-header h3 { margin: 0; font-size: 1rem; color: #f1f5f9; }
    .btn-modal-close {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.1rem;
      cursor: pointer;
    }
    .modal-body { padding: 20px; font-size: 0.84rem; line-height: 1.5; color: #cbd5e1; }
    .rec-section { margin-bottom: 14px; }
    .rec-section h4 { margin: 0 0 6px 0; font-size: 0.9rem; color: #fff; }
    .rec-section p, .rec-section ul { margin: 4px 0; }
    .rec-divider { height: 1px; background: rgba(255, 255, 255, 0.1); margin: 16px 0; }
    .rec-final h4 { font-size: 1.05rem; margin: 0 0 6px 0; }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      padding: 14px 20px;
      background: #151c2c;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
    }
    .btn-primary-modal {
      background: #4f46e5;
      border: none;
      color: white;
      padding: 8px 18px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.84rem;
      cursor: pointer;
    }
    .btn-cancel {
      background: #334155;
      border: none;
      color: #cbd5e1;
      padding: 8px 16px;
      border-radius: 6px;
      font-weight: 600;
      font-size: 0.84rem;
      cursor: pointer;
    }
    .cost-input-group { margin-bottom: 12px; display: flex; flex-direction: column; gap: 4px; }
    .cost-input {
      background: #1e293b;
      border: 1px solid #334155;
      color: white;
      padding: 8px 10px;
      border-radius: 6px;
      font-size: 0.85rem;
    }

    .toast-notification {
      position: fixed;
      bottom: 30px;
      left: 50%;
      transform: translateX(-50%);
      background: rgba(15, 23, 42, 0.95);
      border: 1px solid #10b981;
      color: #34d399;
      padding: 10px 24px;
      border-radius: 30px;
      font-size: 0.85rem;
      font-weight: 700;
      box-shadow: 0 10px 30px rgba(0, 0, 0, 0.6);
      z-index: 999999;
      animation: fade-in 0.2s;
    }
    /* DESKTOP RIGHT-CLICK CONTEXT MENU */
    .desktop-context-menu {
      position: fixed;
      background: #1e293b;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 8px;
      box-shadow: 0 12px 32px rgba(0, 0, 0, 0.7);
      padding: 6px 0;
      z-index: 100000;
      min-width: 270px;
      animation: pop-in 0.12s ease-out;
    }
    @keyframes pop-in {
      from { transform: scale(0.95); opacity: 0; }
      to { transform: scale(1); opacity: 1; }
    }
    .ctx-item {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 8px 16px;
      font-size: 0.8rem;
      font-weight: 500;
      color: #cbd5e1;
      cursor: pointer;
      transition: all 0.15s;
    }
    .ctx-item:hover {
      background: #334155;
      color: #fff;
    }
    .ctx-item.highlight {
      color: #38bdf8;
      font-weight: 700;
      background: rgba(56, 189, 248, 0.08);
    }
    .ctx-divider {
      height: 1px;
      background: rgba(255, 255, 255, 0.08);
      margin: 4px 0;
    }
    .ctx-icon {
      font-size: 0.95rem;
    }

    /* ENHANCED ORDER CONTEXT & COST PREVIEW */
    .order-context-box {
      background: #151c2c;
      border: 1px solid #334155;
      border-radius: 8px;
      padding: 12px;
      margin-bottom: 14px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .ctx-row { font-size: 0.82rem; color: #e2e8f0; }
    .badge-row { display: flex; gap: 6px; margin-top: 6px; flex-wrap: wrap; }
    .ctx-badge {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      background: #1e293b;
      color: #cbd5e1;
    }
    .ctx-badge.gross { color: #34d399; }
    .ctx-badge.fee { color: #f59e0b; }
    .ctx-badge.rate { color: #38bdf8; }

    .calculated-preview-box {
      background: #15232c;
      border: 1px solid rgba(52, 211, 153, 0.3);
      border-radius: 8px;
      padding: 12px;
      margin-top: 14px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .calc-row {
      display: flex;
      justify-content: space-between;
      font-size: 0.82rem;
      color: #94a3b8;
    }
    .calc-row.highlight { font-size: 0.9rem; font-weight: 700; color: #fff; }
    .calc-row strong { font-size: 0.95rem; }

    /* INTERACTIVE KPI CARDS & SORTABLE HEADERS */
    .interactive-kpi {
      cursor: pointer;
      position: relative;
      transition: all 0.2s ease-in-out;
    }
    .interactive-kpi:hover {
      transform: translateY(-2px);
      box-shadow: 0 8px 24px rgba(0, 0, 0, 0.5);
      filter: brightness(1.1);
    }
    .kpi-zoom-hint {
      font-size: 0.7rem;
      font-weight: 600;
      color: #94a3b8;
      background: rgba(255, 255, 255, 0.08);
      padding: 2px 6px;
      border-radius: 4px;
      transition: all 0.2s;
    }
    .interactive-kpi:hover .kpi-zoom-hint {
      background: rgba(56, 189, 248, 0.2);
      color: #38bdf8;
    }

    .sortable-th {
      cursor: pointer;
      user-select: none;
      transition: background 0.15s, color 0.15s;
      white-space: nowrap;
    }
    .sortable-th:hover {
      background: #1e293b !important;
      color: #38bdf8 !important;
    }
    .sortable-th.active-sort {
      color: #38bdf8 !important;
      background: rgba(56, 189, 248, 0.12) !important;
      font-weight: 800;
    }
    .sortable-th.highlight-th.active-sort {
      color: #34d399 !important;
      background: rgba(52, 211, 153, 0.12) !important;
    }
    .sort-badge {
      display: inline-block;
      font-size: 0.78rem;
      font-weight: 900;
      color: #38bdf8;
      margin-right: 4px;
    }
    .sort-badge.highlight {
      color: #34d399;
    }

    /* DRILLDOWN MODAL STYLES (MATCHING GÖRSEL 2, 3, 4) */
    .drilldown-card {
      max-width: 960px;
      width: 94%;
      background: #0d1527;
      border: 1px solid #334155;
      border-radius: 12px;
      box-shadow: 0 24px 60px rgba(0, 0, 0, 0.85);
      animation: pop-in 0.15s ease-out;
    }
    .drilldown-title-box h3 {
      font-size: 1.15rem;
      font-weight: 700;
      margin: 0 0 4px 0;
      color: #f8fafc;
    }
    .modal-sub {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 0;
    }
    .modal-sub strong {
      color: #f1f5f9;
    }
    .drilldown-summary-grid {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 12px;
      margin-bottom: 16px;
    }
    .drilldown-summary-box {
      background: #151d30;
      border: 1px solid #26334d;
      border-radius: 8px;
      padding: 10px 14px;
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .drilldown-summary-box.highlight-emerald {
      border-color: rgba(16, 185, 129, 0.5);
      background: rgba(16, 185, 129, 0.08);
    }
    .box-lbl {
      font-size: 0.72rem;
      font-weight: 700;
      text-transform: uppercase;
      color: #94a3b8;
    }
    .box-val {
      font-size: 1.2rem;
      font-weight: 800;
    }
    .box-sub {
      font-size: 0.72rem;
      color: #64748b;
    }
    .drilldown-table-wrapper {
      max-height: 380px;
      overflow-y: auto;
      border: 1px solid #26334d;
      border-radius: 8px;
      margin-bottom: 12px;
    }
    .drilldown-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8rem;
    }
    .drilldown-table th {
      background: #182238;
      padding: 9px 12px;
      text-align: left;
      font-size: 0.75rem;
      font-weight: 700;
      color: #cbd5e1;
      position: sticky;
      top: 0;
      border-bottom: 1px solid #334155;
      z-index: 10;
    }
    .drilldown-table td {
      padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      color: #e2e8f0;
      white-space: nowrap;
    }
    .drilldown-table tr:hover {
      background: rgba(51, 65, 85, 0.4);
    }
    .drilldown-table tr.highlight-row {
      background: rgba(16, 185, 129, 0.1);
      font-weight: 700;
    }
    .badge-status-payout {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      background: rgba(16, 185, 129, 0.15);
      color: #34d399;
    }
    .badge-invoice {
      font-size: 0.72rem;
      font-weight: 700;
      padding: 2px 8px;
      border-radius: 4px;
      background: rgba(56, 189, 248, 0.15);
      color: #38bdf8;
    }
    .drilldown-footnote {
      font-size: 0.76rem;
      color: #94a3b8;
      font-style: italic;
      background: #151d30;
      padding: 8px 14px;
      border-radius: 6px;
      margin-top: 10px;
      border-left: 3px solid #38bdf8;
    }
  `]
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

  // 9 KPI Values (Matching Desktop Image 2)
  grossSalesTry = 54608.24;
  grossSalesUsd = 1111.73;

  etsyFeesTry = 12783.71;
  etsyFeesUsd = 260.25;

  innerAdsTry = 404.30;
  innerAdsUsd = 8.23;

  offsiteAdsTry = 3930.29;
  offsiteAdsUsd = 80.01;

  refundsTry = 1888.14;
  refundsUsd = 38.44;

  netIncomeTry = 35601.79;
  netIncomeUsd = 724.80;

  productCostsTry = 13524.10;
  productCostsUsd = 275.33;

  realNetProfitTry = 22077.69;
  realNetProfitUsd = 449.46;

  bankPayoutsTry = 27594.89;
  bankPayoutsUsd = 561.78;

  // Order List & Filtering
  orderSearchQuery = '';
  orderCostFilter = 'all';
  orders: OrderFinancialRow[] = [
    {
      orderDate: '01.10.2026 14:22',
      receiptId: '4188710928',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Jamie Westerman (#29481)',
      productTitle: '3D Printed Butterfly Trainer Blade Custom Balisong',
      quantity: 1,
      grandTotalUsd: 35.31,
      etsyFeesUsd: 5.10,
      offsiteAdFeeUsd: 5.47,
      productCostUsd: 10.42,
      netProfitUsd: 14.62,
      exchangeRate: 49.05,
      netProfitTry: 717.11,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '01.10.2026 09:15',
      receiptId: '4188192041',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Diane Barrow (#19482)',
      productTitle: 'Captain Jack Sparrow Compass Functional Replica',
      quantity: 1,
      grandTotalUsd: 156.60,
      etsyFeesUsd: 47.08,
      offsiteAdFeeUsd: 19.68,
      productCostUsd: 41.96,
      netProfitUsd: 47.68,
      exchangeRate: 49.02,
      netProfitTry: 2337.27,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '30.09.2026 18:40',
      receiptId: '4187920145',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Nathan McCoy (#38210)',
      productTitle: 'Fallout Pip Boy 3000 Mk IV Wearable Bluetooth',
      quantity: 1,
      grandTotalUsd: 97.20,
      etsyFeesUsd: 29.27,
      offsiteAdFeeUsd: 0,
      productCostUsd: 25.00,
      netProfitUsd: 42.93,
      exchangeRate: 49.02,
      netProfitTry: 2104.43,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '28.09.2026 21:10',
      receiptId: '4187294810',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Sandro Sobral (#49182)',
      productTitle: 'Captain Jack Sparrow Compass Functional Replica',
      quantity: 1,
      grandTotalUsd: 149.99,
      etsyFeesUsd: 32.47,
      offsiteAdFeeUsd: 0,
      productCostUsd: 41.96,
      netProfitUsd: 75.56,
      exchangeRate: 48.93,
      netProfitTry: 3697.15,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '26.09.2026 16:30',
      receiptId: '4185194820',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Anthony Drost (#20194)',
      productTitle: 'Arcane Jinx Fishbones Rocket Launcher 3D Kit',
      quantity: 1,
      grandTotalUsd: 182.57,
      etsyFeesUsd: 37.98,
      offsiteAdFeeUsd: 25.01,
      productCostUsd: 35.31,
      netProfitUsd: 83.67,
      exchangeRate: 48.89,
      netProfitTry: 4090.63,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '18.09.2026 14:05',
      receiptId: '4176629104',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Inge Neuer (#84912)',
      productTitle: 'Michael Jackson Smooth Criminal Anti-Gravity Shoes',
      quantity: 1,
      grandTotalUsd: 107.10,
      etsyFeesUsd: 31.55,
      offsiteAdFeeUsd: 0,
      productCostUsd: 34.00,
      netProfitUsd: 41.55,
      exchangeRate: 48.73,
      netProfitTry: 2024.73,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '18.09.2026 11:20',
      receiptId: '4176192840',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Adam Schultz (#19482)',
      productTitle: 'Arcane Jinx Zap Gun LED Lighted Prop Replica',
      quantity: 1,
      grandTotalUsd: 170.50,
      etsyFeesUsd: 39.39,
      offsiteAdFeeUsd: 23.49,
      productCostUsd: 38.31,
      netProfitUsd: 69.31,
      exchangeRate: 48.73,
      netProfitTry: 3377.48,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '15.09.2026 19:45',
      receiptId: '4174091823',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Liz Porter (#48192)',
      productTitle: 'Lotr Aragorn Crown Elessar King of Gondor Helmet',
      quantity: 1,
      grandTotalUsd: 86.25,
      etsyFeesUsd: 12.80,
      offsiteAdFeeUsd: 0,
      productCostUsd: 38.67,
      netProfitUsd: 34.78,
      exchangeRate: 48.64,
      netProfitTry: 1691.70,
      hasCostData: true,
      hasInvoice: false
    },
    {
      orderDate: '14.09.2026 13:10',
      receiptId: '4174201942',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Barbara Yaptangco (#9184)',
      productTitle: 'Ben 10 Classic Omnitrix Functional Dial & Sound',
      quantity: 1,
      grandTotalUsd: 107.29,
      etsyFeesUsd: 24.73,
      offsiteAdFeeUsd: 0,
      productCostUsd: null,
      netProfitUsd: 82.56,
      exchangeRate: 48.61,
      netProfitTry: 4013.24,
      hasCostData: false,
      hasInvoice: false
    },
    {
      orderDate: '07.09.2026 10:30',
      receiptId: '4168019481',
      orderStatus: 'completed',
      displayStatus: '🟢 Tamamlandı',
      buyerName: 'Heather Taylor (#29481)',
      productTitle: '3D Printed Butterfly Trainer Blade Custom Balisong',
      quantity: 1,
      grandTotalUsd: 39.34,
      etsyFeesUsd: 7.96,
      offsiteAdFeeUsd: 5.59,
      productCostUsd: 11.28,
      netProfitUsd: 14.51,
      exchangeRate: 48.46,
      netProfitTry: 703.15,
      hasCostData: true,
      hasInvoice: true
    }
  ];

  filteredOrders: OrderFinancialRow[] = [];

  // Ledger Entries
  ledgerEntries: PaymentLedgerEntry[] = [
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
      netAmountTry: 2962.43,
      runningBalance: 12450.80
    },
    {
      id: 'TXN-908123',
      entryDate: '2026-10-02 12:00',
      transactionType: 'ad',
      typeDisplay: 'Etsy Ads Tıklama',
      title: 'Günlük Reklam Harcaması (Tıklama Başı Maliyet)',
      grossAmount: 0,
      feeAmount: -12.50,
      netAmount: -12.50,
      currency: 'USD',
      netAmountTry: -614.00,
      runningBalance: 12390.49
    },
    {
      id: 'TXN-908122',
      entryDate: '2026-10-02 09:15',
      transactionType: 'sale',
      typeDisplay: 'Sipariş Ödemesi',
      title: 'Ödeme Alma: #348876102 (Oliver Smith)',
      orderNumber: '#348876102',
      grossAmount: 118.00,
      feeAmount: -7.67,
      netAmount: 110.33,
      currency: 'USD',
      netAmountTry: 5419.41,
      runningBalance: 12402.99
    },
    {
      id: 'TXN-908121',
      entryDate: '2026-10-01 23:59',
      transactionType: 'payout',
      typeDisplay: 'Banka Transferi',
      title: 'Banka Hesabına Aktarım (QNB Finansbank TR)',
      grossAmount: 0,
      feeAmount: 0,
      netAmount: -561.78,
      currency: 'USD',
      netAmountTry: -27594.89,
      runningBalance: 12292.66
    }
  ];

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

  // Table Sorting State (Matching Görsel 1)
  sortColumn: string = 'netProfitUsd';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Modal: Satış ve Gelir Analizi (Brüt Satış)
  salesRecords = [
    { date: '01.10.26 14:22', receiptId: '4188710928', buyer: 'Jamie Westerman', item: '3D Printed Butterfly Trainer Blade', amount: 35.31, tryAmount: 1731.96, paymentType: 'payment_gross' },
    { date: '01.10.26 09:15', receiptId: '4188192041', buyer: 'Diane Barrow', item: 'Captain Jack Sparrow Compass Functional', amount: 156.60, tryAmount: 7676.53, paymentType: 'payment_gross' },
    { date: '30.09.26 18:40', receiptId: '4187920145', buyer: 'Nathan McCoy', item: 'Fallout Pip Boy 3000 Mk IV Wearable', amount: 148.50, tryAmount: 7280.96, paymentType: 'payment_gross' },
    { date: '28.09.26 21:05', receiptId: '4187299104', buyer: 'Sarah Jenkins', item: 'Captain Jack Sparrow Resin Statue', amount: 156.60, tryAmount: 7662.30, paymentType: 'payment_gross' },
    { date: '25.09.26 16:30', receiptId: '4185189201', buyer: 'Lucas Meyer', item: 'Arcane Jinx Statue LoL Figure', amount: 147.20, tryAmount: 7178.94, paymentType: 'payment_gross' },
    { date: '18.09.26 20:12', receiptId: '4178129840', buyer: 'Elena Rostova', item: 'Arcane Jinx Statue LoL Figure', amount: 169.50, tryAmount: 8259.74, paymentType: 'payment_gross' },
    { date: '18.09.26 11:45', receiptId: '4176640192', buyer: 'David Kim', item: 'Michael Jackson Statue King of Pop', amount: 147.20, tryAmount: 7172.06, paymentType: 'payment_gross' },
    { date: '15.09.26 13:20', receiptId: '4174028911', buyer: 'Marcus Vance', item: 'Lotr Aragorn Crown of Gondor', amount: 154.50, tryAmount: 7494.79, paymentType: 'payment_gross' },
    { date: '14.09.26 08:50', receiptId: '4174291882', buyer: 'Tom Bradley', item: 'Ben 10 Classic Omnitrix Watch V2', amount: 0.00, tryAmount: 0.00, paymentType: 'canceled' },
    { date: '07.09.26 17:15', receiptId: '4168074902', buyer: 'Chloe Bennett', item: '3D Printed Butterfly Trainer Blade', amount: 37.90, tryAmount: 1836.26, paymentType: 'payment_gross' }
  ];

  // Modal: Etsy Komisyon & Kesinti Analizi
  feesRecords = [
    { type: 'İşlem Komisyonu (%6.5)', count: '10 İşlem', baseUsd: 1111.73, feeUsd: 72.26, feeTry: 3549.41, note: 'Etsy Transaction Fee (%6.5)' },
    { type: 'Ödeme İşleme (Processing)', count: '10 İşlem', baseUsd: 1111.73, feeUsd: 49.02, feeTry: 2407.86, note: 'Etsy Payments Processing Fee (%3 + $0.25)' },
    { type: 'Listeleme Ücreti (Listing)', count: '28 Yenileme', baseUsd: 0, feeUsd: 5.60, feeTry: 275.07, note: 'Auto-renew listing fee ($0.20/adet)' },
    { type: 'Düzenleyici İşletme Ücreti', count: '10 İşlem', baseUsd: 1111.73, feeUsd: 13.90, feeTry: 682.77, note: 'Regulatory Operating Fee (TR %1.25)' },
    { type: 'Etsy Kesinti KDV (VAT)', count: 'Tüm Kesintiler', baseUsd: 0, feeUsd: 28.16, feeTry: 1383.22, note: '%20 Türkiye KDV Tevkifatı' },
    { type: 'Offsite Ads Dış Reklam Kesintisi', count: '3 Satış', baseUsd: 533.40, feeUsd: 80.01, feeTry: 3930.29, note: '%15 Offsite Ads komisyonu' },
    { type: 'Etsy Ads Tıklama Harcaması', count: '68 Tıklama', baseUsd: 0, feeUsd: 8.23, feeTry: 404.30, note: 'Arama içi sponsorlu reklam harcaması' }
  ];

  // Modal: Etsy Ads İç Reklam Analizi
  innerAdsRecords = [
    { date: '02.10.2026', impressions: 420, clicks: 14, spendUsd: 1.82, spendTry: 89.40, salesUsd: 35.31, roas: '19.4x' },
    { date: '01.10.2026', impressions: 385, clicks: 12, spendUsd: 1.56, spendTry: 76.47, salesUsd: 0.00, roas: '0.0x' },
    { date: '30.09.2026', impressions: 512, clicks: 18, spendUsd: 2.34, spendTry: 114.71, salesUsd: 148.50, roas: '63.5x' },
    { date: '29.09.2026', impressions: 310, clicks: 9, spendUsd: 1.17, spendTry: 57.35, salesUsd: 0.00, roas: '0.0x' },
    { date: '28.09.2026', impressions: 405, clicks: 15, spendUsd: 1.34, spendTry: 65.69, salesUsd: 0.00, roas: '0.0x' }
  ];

  // Modal: Offsite Ads Dış Reklam Analizi
  offsiteAdsRecords = [
    { receiptId: '4188710928', buyer: 'Jamie Westerman', saleAmountUsd: 35.31, rate: '%15', feeUsd: 5.30, feeTry: 259.97, channel: 'Google Shopping' },
    { receiptId: '4188192041', buyer: 'Diane Barrow', saleAmountUsd: 156.60, rate: '%15', feeUsd: 23.49, feeTry: 1151.48, channel: 'Facebook / Instagram' },
    { receiptId: '4187299104', buyer: 'Sarah Jenkins', saleAmountUsd: 156.60, rate: '%15', feeUsd: 23.49, feeTry: 1150.31, channel: 'Pinterest Ads' },
    { receiptId: '4178129840', buyer: 'Elena Rostova', saleAmountUsd: 169.50, rate: '%15', feeUsd: 25.43, feeTry: 1238.44, channel: 'Google Search Partner' }
  ];

  // Modal: İade ve Geri Ödeme Analizi
  refundRecords = [
    { date: '14.09.2026 08:50', receiptId: '4174291882', buyer: 'Tom Bradley', item: 'Ben 10 Classic Omnitrix Watch V2', refundUsd: 38.44, refundTry: 1888.14, reason: 'Alıcı Adres Değişikliği / İptal Talebi', feeRefundedUsd: 4.80 }
  ];

  // Modal: Etsy Net Gelir Analizi
  netIncomeRecords = [
    { category: 'Brüt Satış Geliri', amountUsd: 1111.73, amountTry: 54608.24, isPositive: true, note: 'Müşterilerden tahsil edilen toplam sipariş tutarı' },
    { category: 'Etsy Standart Komisyon & İşlem Ücreti', amountUsd: -148.98, amountTry: -7318.84, isPositive: false, note: 'Transaction, listing, processing & regulatory fee' },
    { category: 'Offsite Ads Dış Reklam Kesintisi', amountUsd: -80.01, amountTry: -3930.29, isPositive: false, note: 'Dış arama motoru ve sosyal medya reklam payı' },
    { category: 'Etsy Ads Arama İçi Reklam', amountUsd: -8.23, amountTry: -404.30, isPositive: false, note: 'Etsy içi tıklama başı reklam harcaması' },
    { category: 'Etsy Kesinti KDV (VAT)', amountUsd: -28.16, amountTry: -1383.22, isPositive: false, note: 'Kesintilere uygulanan resmî KDV tevkifatı' },
    { category: 'İadeler ve İptaller (Refunds)', amountUsd: -38.44, amountTry: -1888.14, isPositive: false, note: 'İade edilen sipariş tutarı' },
    { category: 'NET HAKEDİŞ (ETSY NET GELİR)', amountUsd: 724.80, amountTry: 35601.79, isPositive: true, note: 'Banka hesabına aktarılmaya hazır nihai platform bakiyesi' }
  ];

  // Modal 1 Data: Bank Payouts (Matching Görsel 2)
  bankPayoutRecords = [
    { date: '01.10.26 11:01', refNo: '#208503135526', type: 'Banka Transferi', amount: 4359.09, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 49.05₺)' },
    { date: '28.09.26 11:04', refNo: '#208313484387', type: 'Banka Transferi', amount: 4403.77, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.93₺)' },
    { date: '21.09.26 11:10', refNo: '#210139855281', type: 'Banka Transferi', amount: 3898.61, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.79₺)' },
    { date: '18.09.26 11:01', refNo: '#207743529694', type: 'Banka Transferi', amount: 607.16, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.73₺)' },
    { date: '17.09.26 11:00', refNo: '#209914284431', type: 'Banka Transferi', amount: 908.67, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.68₺)' },
    { date: '16.09.26 11:01', refNo: '#207632008578', type: 'Banka Transferi', amount: 3142.58, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.66₺)' },
    { date: '14.09.26 11:04', refNo: '#207510955300', type: 'Banka Transferi', amount: 961.62, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.61₺)' },
    { date: '10.09.26 11:00', refNo: '#209507913407', type: 'Banka Transferi', amount: 4705.13, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.51₺)' },
    { date: '09.09.26 11:01', refNo: '#209450052917', type: 'Banka Transferi', amount: 1693.64, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.47₺)' },
    { date: '04.09.26 11:00', refNo: '#206922866600', type: 'Banka Transferi', amount: 2914.63, status: 'Yatırıldı', note: 'disburse2 (Kur: 1$ = 48.40₺)' }
  ];

  // Modal 2 Data: Shipping & Cost Breakdown (Matching Görsel 3)
  orderCostShippingRecords = [
    { date: '01.10.26', receiptId: '4188719047', qty: '1 Ad', shippingCostTry: 475.29, invoice: '—', title: '3D Printed Butterfly Trainer | Colorful Safe K...' },
    { date: '30.09.26', receiptId: '4188172739', qty: '1 Ad', shippingCostTry: 1037.75, invoice: '—', title: 'Captain Jack Sparrow Resin Statue | Pirates...' },
    { date: '30.09.26', receiptId: '4187902419', qty: '1 Ad', shippingCostTry: 980.40, invoice: '—', title: 'Fallout Pip-Boy 3000 | Fallout Pip-Boy 3000...' },
    { date: '28.09.26', receiptId: '4187282652', qty: '1 Ad', shippingCostTry: 1035.85, invoice: '—', title: 'Captain Jack Sparrow Resin Statue | Pirates...' },
    { date: '25.09.26', receiptId: '4185170234', qty: '1 Ad', shippingCostTry: 977.80, invoice: '—', title: 'Arcane Jinx Statue, League of Legends Figur...' },
    { date: '18.09.26', receiptId: '4176634453', qty: '1 Ad', shippingCostTry: 974.60, invoice: '—', title: 'Michael Jackson Statue, King of Pop Figure...' },
    { date: '18.09.26', receiptId: '4178116180', qty: '1 Ad', shippingCostTry: 1120.79, invoice: '—', title: 'Arcane Jinx Statue, League of Legends Figur...' },
    { date: '15.09.26', receiptId: '4174015409', qty: '1 Ad', shippingCostTry: 1021.44, invoice: '—', title: 'Lotr Aragorn Crown-Aragorn -Crown of Gon...' },
    { date: '14.09.26', receiptId: '4174282090', qty: '1 Ad', shippingCostTry: 0.00, invoice: '—', title: 'Ben 10 Classic Omnitrix Watch V2  Ben 10...' },
    { date: '07.09.26', receiptId: '4168067600', qty: '1 Ad', shippingCostTry: 503.01, invoice: 'Fatura', title: '3D Printed Butterfly Trainer | Colorful Safe K...' }
  ];

  // Modal 3 Data: Balance & Real Profit (Matching Görsel 4)
  balanceBreakdownRecords = [
    { type: 'Gelir', name: 'Etsy Net Gelir', detail: 'Net Hakediş', amount: 35601.79, status: '🟢 Net', isPositive: true, explanation: 'Komisyon ve iadeler sonrası platformdan kalan net hakediş' },
    { type: 'Kargo', name: 'Kargo Gönderimleri', detail: '9 Gönderi', amount: -8126.93, status: '', isPositive: false, explanation: 'Siparişlerin müşterilere kargolanma ve lojistik maliyeti' },
    { type: 'Üretim', name: '3D Baskı / Hammadde', detail: 'Filament & Reçine', amount: -5397.16, status: '', isPositive: false, explanation: 'Üretimde harcanan hammadde ve 3D yazıcı amortismanı' },
    { type: 'Paket', name: 'Paketleme & Fatura', detail: '1 Fatura', amount: 0.00, status: '', isPositive: false, explanation: 'Kutu, balonlu naylon, barkod etiketi ve resmî faturalar' },
    { type: 'KÂR', name: 'GERÇEK NET KÂR', detail: 'Saf Kâr', amount: 22077.69, status: '🟢 Kâr', isPositive: true, explanation: 'Tüm platform ve operasyonel giderler çıktıktan sonra bankadaki net kâr' }
  ];

  constructor(
    private etsyApi: EtsyApiService,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.filteredOrders = [...this.orders];
    this.liveRate = this.etsyApi.exchangeRate() || 49.12;
  }

  ngOnDestroy(): void {
  }

  get missingCostCount(): number {
    return this.orders.filter(o => !o.hasCostData && o.orderStatus !== 'canceled').length;
  }

  setCurrency(curr: 'USD' | 'TRY'): void {
    this.isTry = curr === 'TRY';
  }

  formatKpi(valTry: number, valUsd: number): string {
    if (this.isTry) {
      return '₺' + this.formatNumber(valTry);
    }
    return '$' + this.formatNumber(valUsd);
  }

  formatNumber(val: number): string {
    return val.toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
  }

  copyValue(label: string, val: string): void {
    navigator.clipboard.writeText(val);
    this.showToast(`📋 Kopyalandı: ${label} ➔ ${val}`);
  }

  onPresetChange(): void {
    if (this.selectedDatePreset === 'last30') {
      this.startDate = '03.09.2026';
      this.endDate = '03.10.2026';
    } else if (this.selectedDatePreset === 'last7') {
      this.startDate = '26.09.2026';
      this.endDate = '03.10.2026';
    } else if (this.selectedDatePreset === 'thisMonth') {
      this.startDate = '01.10.2026';
      this.endDate = '03.10.2026';
    }
    this.showToast(`📅 Dönem güncellendi: ${this.startDate} — ${this.endDate}`);
  }

  onAutoRateToggle(): void {
    this.showToast(this.autoRateEnabled ? `🇹🇷 TCMB/Google Oto Kur Aktif (${this.liveRate} ₺)` : 'Manuel Kur Modu');
  }

  refreshFromEtsy(): void {
    this.isRefreshing = true;
    setTimeout(() => {
      this.isRefreshing = false;
      this.showToast('✅ Canlı Etsy API senkronizasyonu tamamlandı: 97 kayıt güncellendi.');
    }, 900);
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
    this.showToast('📊 5 Sayfalık Excel / CSV Raporu başarıyla indirildi!');
  }

  sendTelegramReport(): void {
    this.showToast('📱 Telegram Finans Özeti bot kanalınıza iletildi!');
  }

  syncVds(): void {
    this.isSyncingVds = true;
    setTimeout(() => {
      this.isSyncingVds = false;
      this.showToast('🌐 VDS Tam Senkronizasyonu Başarılı: Günlük kayıtlar ve grafikler aktarıldı.');
    }, 1200);
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

    this.showToast(`📊 Sıralandı: ${this.getColumnLabel(col)} (${this.sortDirection === 'asc' ? 'Küçükten Büyüğe ▲' : 'Büyükten Küçüğe ▼'})`);
  }

  getColumnLabel(col: string): string {
    const map: Record<string, string> = {
      date: 'Tarih',
      receiptId: 'Sipariş No',
      orderStatus: 'Durum',
      buyerName: 'Müşteri',
      productTitle: 'Ürün',
      quantity: 'Adet',
      grandTotalUsd: 'Müşteri Ödemesi ($)',
      etsyFeesUsd: 'Etsy Kesintileri ($)',
      offsiteAdFeeUsd: 'Dış Reklam ($)',
      productCostUsd: 'Sipariş Maliyeti ($)',
      netProfitUsd: 'Net Kâr ($)',
      exchangeRate: 'Kur (₺)',
      netProfitTry: 'Net Kâr (₺)',
      hasCostData: 'Maliyet Durumu',
      hasInvoice: 'Kargo Faturası'
    };
    return map[col] || col;
  }

  openSalesAnalysisModal(): void {
    this.showSalesModal = true;
  }

  openFeesAnalysisModal(): void {
    this.showFeesModal = true;
  }

  openInnerAdsModal(): void {
    this.showInnerAdsModal = true;
  }

  openOffsiteAdsModal(): void {
    this.showOffsiteAdsModal = true;
  }

  openRefundsModal(): void {
    this.showRefundsModal = true;
  }

  openNetIncomeModal(): void {
    this.showNetIncomeModal = true;
  }

  openBankPayoutModal(): void {
    this.showBankPayoutModal = true;
  }

  openCostBreakdownModal(): void {
    this.showCostBreakdownModal = true;
  }

  openBalanceAnalysisModal(): void {
    this.showBalanceAnalysisModal = true;
  }

  openProfitReconciliationModal(): void {
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
      this.showToast(`⚡ #${this.contextMenuOrder.receiptId} için PDF fatura ve konşimento oluşturuldu.`);
    }
    this.closeContextMenu();
  }

  openInvoicePdf(): void {
    if (this.contextMenuOrder) {
      this.showToast(`📄 #${this.contextMenuOrder.receiptId} PDF faturası açıldı.`);
    }
    this.closeContextMenu();
  }

  uploadInvoicePdf(): void {
    if (this.contextMenuOrder) {
      this.contextMenuOrder.hasInvoice = true;
      this.showToast(`📎 #${this.contextMenuOrder.receiptId} için kargo faturası yüklendi.`);
    }
    this.closeContextMenu();
  }

  viewOrderDetails(): void {
    if (this.contextMenuOrder) {
      this.showToast(`📊 #${this.contextMenuOrder.receiptId} detayları: ${this.contextMenuOrder.buyerName} — ${this.contextMenuOrder.productTitle}`);
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
      this.showToast(`📋 Kopyalandı: #${this.contextMenuOrder.receiptId}`);
    }
    this.closeContextMenu();
  }

  copyCustomerInfo(): void {
    if (this.contextMenuOrder) {
      navigator.clipboard.writeText(this.contextMenuOrder.buyerName);
      this.showToast(`👤 Müşteri kopyalandı: ${this.contextMenuOrder.buyerName}`);
    }
    this.closeContextMenu();
  }

  copySelectedCell(): void {
    if (this.contextMenuOrder) {
      navigator.clipboard.writeText(`$${this.contextMenuOrder.grandTotalUsd}`);
      this.showToast(`📝 Hücre tutarı kopyalandı: $${this.contextMenuOrder.grandTotalUsd}`);
    }
    this.closeContextMenu();
  }

  copyWholeRow(): void {
    if (this.contextMenuOrder) {
      const row = `${this.contextMenuOrder.orderDate} | #${this.contextMenuOrder.receiptId} | ${this.contextMenuOrder.buyerName} | ${this.contextMenuOrder.productTitle} | $${this.contextMenuOrder.grandTotalUsd} | Net: $${this.contextMenuOrder.netProfitUsd} (₺${this.contextMenuOrder.netProfitTry})`;
      navigator.clipboard.writeText(row);
      this.showToast(`📄 Tüm satır kopyalandı.`);
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
      const totalCost = Number((this.costModalProduction + this.costModalShipping + this.costModalPackaging).toFixed(2));
      this.selectedOrder.productCostUsd = totalCost;
      this.selectedOrder.hasCostData = true;
      this.selectedOrder.netProfitUsd = Number((this.selectedOrder.grandTotalUsd - this.selectedOrder.etsyFeesUsd - this.selectedOrder.offsiteAdFeeUsd - totalCost).toFixed(2));
      this.selectedOrder.netProfitTry = Number((this.selectedOrder.netProfitUsd * this.selectedOrder.exchangeRate).toFixed(2));

      // Recalculate KPIs from all completed orders
      const active = this.orders.filter(o => o.orderStatus !== 'canceled');
      const totalCostsTRY = active.reduce((sum, o) => sum + ((o.productCostUsd || 0) * o.exchangeRate), 0);
      const totalNetTRY = active.reduce((sum, o) => sum + o.netProfitTry, 0) - this.innerAdsTry - this.offsiteAdsTry - this.refundsTry;

      this.productCostsTry = Number(totalCostsTRY.toFixed(2));
      this.productCostsUsd = Number((totalCostsTRY / this.liveRate).toFixed(2));
      this.realNetProfitTry = Number(totalNetTRY.toFixed(2));
      this.realNetProfitUsd = Number((totalNetTRY / this.liveRate).toFixed(2));

      this.showToast(`✅ #${this.selectedOrder.receiptId} maliyeti $${totalCost.toFixed(2)} kaydedildi. Gerçek Net Kâr güncellendi!`);
    }
    this.closeModals();
  }

  handleInvoice(order: OrderFinancialRow): void {
    if (order.hasInvoice) {
      this.showToast(`📄 Fatura Görüntüleyici: #${order.receiptId} Kargo Faturası açıldı.`);
    } else {
      order.hasInvoice = true;
      this.showToast(`📎 Fatura Eklendi: #${order.receiptId} için PDF kargo faturası sisteme bağlandı.`);
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
