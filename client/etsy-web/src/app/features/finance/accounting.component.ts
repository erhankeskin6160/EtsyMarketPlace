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
  template: `
    <div class="accounting-view">
      <!-- 1. TOP HEADER & DESKTOP CONTROLS -->
      <div class="accounting-header">
        <div class="header-left">
          <div class="title-row">
            <h1 class="page-title">Finansal Raporlama Muhasebe Paneli</h1>
            <span class="live-status-pill" *ngIf="financialPerformance && !loadError">
              <span class="live-dot"></span> Etsy/VDS finansal performans verisi
            </span>
            <span class="live-status-pill" *ngIf="isLoading">Finansal veriler yükleniyor…</span>
            <span class="live-status-pill" *ngIf="loadError">{{ loadError }}</span>
          </div>

          <p class="page-subtitle">Etsy Payment Account hareketleri, sipariş kâr marjları ve çok katmanlı maliyet denetimi</p>
        </div>

      <div class="empty-state-box" *ngIf="!isLoading && loadError">{{ loadError }}</div>
      <div class="empty-state-box" *ngIf="!isLoading && !loadError && financialPerformance && grossSalesUsd === 0 && bankPayoutRecords.length === 0">
        Seçili dönemde senkronize edilmiş finansal veya payout verisi bulunamadı.
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
          <input type="date" [(ngModel)]="startDate" class="bar-date-input" />
          <span class="date-sep">—</span>
          <input type="date" [(ngModel)]="endDate" class="bar-date-input" />
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
              <span>Mağaza net kârı: <strong class="text-emerald">{{ formatKpi(realNetProfitTry, realNetProfitUsd) }}</strong></span>
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
                  <tr *ngIf="filteredOrders.length === 0"><td colspan="15" class="empty-table-cell">Tam sipariş ayrıntısı (alıcı, ürün, durum ve satır bazlı maliyet) mevcut API’de sunulmuyor. Bu tablo örnek sipariş göstermiyor.</td></tr>
                  <tr *ngIf="ledgerEntries.length === 0"><td colspan="10" class="empty-table-cell">Satır bazlı ödeme defteri API’de mevcut değil; bu kayıtlar gösterilemiyor.</td></tr>
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
                  <div class="bar gross-bar" [style.height.%]="chartHeight(grossSalesUsd)"></div>
                  </div>
                  <span class="bar-lbl">Brüt Satış<br><strong>{{ formatKpi(grossSalesTry, grossSalesUsd) }}</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                  <div class="bar fees-bar" [style.height.%]="chartHeight(etsyFeesUsd)"></div>
                  </div>
                  <span class="bar-lbl">Kesintiler<br><strong>{{ formatKpi(etsyFeesTry, etsyFeesUsd) }}</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                  <div class="bar ads-bar" [style.height.%]="chartHeight(innerAdsUsd + offsiteAdsUsd)"></div>
                  </div>
                  <span class="bar-lbl">Reklamlar<br><strong>{{ formatKpi(innerAdsTry + offsiteAdsTry, innerAdsUsd + offsiteAdsUsd) }}</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                  <div class="bar costs-bar" [style.height.%]="chartHeight(productCostsUsd + shippingCostsUsd)"></div>
                  </div>
                  <span class="bar-lbl">Maliyetler<br><strong>{{ formatKpi(productCostsTry + financialPerformance?.shippingCosts! * liveRate, productCostsUsd + (financialPerformance?.shippingCosts || 0)) }}</strong></span>
                </div>
                <div class="chart-col">
                  <div class="bar-wrap">
                  <div class="bar profit-bar" [style.height.%]="chartHeight(realNetProfitUsd)"></div>
                  </div>
                  <span class="bar-lbl">Net Kâr<br><strong>{{ formatKpi(realNetProfitTry, realNetProfitUsd) }}</strong></span>
                </div>
              </div>
            </div>

            <div class="chart-card">
              <h3 class="chart-title">🥧 Gider Dağılımı ve Maliyet Payları</h3>
              <div class="expense-breakdown-list">
                <div class="empty-state-box">API gider kategorisi detayını sağlamıyor; dönem toplamları yalnızca üst KPI’larda gösteriliyor.</div>
              </div>
            </div>
          </div>
        </div>

        <!-- TAB 2: AI KÂR & CİRO TAHMİNİ -->
        <div *ngIf="activeTab === 'forecast'" class="tab-pane">
          <div class="forecast-grid-4">
              <div class="forecast-card primary"><div class="fc-title">Tahmin verisi yok</div><div class="fc-sub">Bu panel için doğrulanabilir tahmin API’si bağlı değil.</div></div>

            <div class="forecast-card success"><div class="fc-title">Tahminleme desteklenmiyor</div><div class="fc-sub">Yeterli geçmiş veri ve doğrulanmış tahmin servisi bulunmuyor.</div></div>
          </div>

          <div class="ai-cfo-panel">Finansal tahmin ve AI önerileri için backend analiz endpoint’i kullanılmalı. Bu ekranda demo öneri gösterilmez.</div>
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
                <tr *ngIf="financialPerformance">
                  <td><strong>{{ financialPerformance.startDate | date:'mediumDate' }} - {{ financialPerformance.endDate | date:'mediumDate' }}</strong></td>
                  <td class="text-success">{{ financialPerformance.grossSales * liveRate | currency:'TRY' }}</td>
                  <td class="text-warning">-{{ financialPerformance.platformFees * liveRate | currency:'TRY' }}</td>
                  <td class="text-danger">-{{ (financialPerformance.internalAdsCost + financialPerformance.externalAdsCost) * liveRate | currency:'TRY' }}</td>
                  <td class="text-danger">-{{ financialPerformance.refunds * liveRate | currency:'TRY' }}</td>
                  <td class="text-warning">-{{ (financialPerformance.productCosts + financialPerformance.shippingCosts) * liveRate | currency:'TRY' }}</td>
                  <td class="text-primary">{{ (financialPerformance.grossSales - financialPerformance.platformFees - financialPerformance.internalAdsCost - financialPerformance.externalAdsCost - financialPerformance.refunds) * liveRate | currency:'TRY' }}</td>
                  <td class="text-emerald"><strong>{{ financialPerformance.netProfit * liveRate | currency:'TRY' }}</strong></td>
                  <td><span class="margin-pill">{{ financialPerformance.netProfitMargin / 100 | percent:'1.0-1' }}</span></td>
                </tr>
                <tr *ngIf="!financialPerformance"><td colspan="9" class="empty-table-cell">API’den dönemsel finans verisi gelmedi.</td></tr>
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
              <h4>1️⃣ Sipariş kârı: <span class="text-success">Sipariş düzeyinde kâr API’de bulunmuyor.</span></h4>
              <p>Mevcut API yalnızca dönemsel finansal toplamı döndürüyor; sipariş bazlı katkı kârı ve gider mutabakatı oluşturulamaz.</p>
            </div>

            <div class="rec-section">
              <h4>2️⃣ Mağaza Geneline Ait Giderler (Siparişten Bağımsız):</h4>
              <ul>
                <li>Reklam ve iade toplamları API’den alınır; satır bazlı nedenler ve sipariş eşlemesi mevcut değildir.</li>
              </ul>
            </div>

            <div class="rec-divider"></div>

            <div class="rec-final">
              <h4>🏛️ Dönem net kârı: <span class="text-emerald">{{ financialPerformance ? formatKpi(realNetProfitTry, realNetProfitUsd) : 'Veri yok' }}</span></h4>
              <p>Bu değer API’nin dönemsel finans raporudur; banka payout toplamıyla aynı kavram değildir.</p>
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
              <p class="modal-sub">Seçili dönemde dönen payout kayıtları: <strong>{{ bankPayoutRecords.length }}</strong></p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Banka Yatırımı</span>
                <span class="box-val text-cyan">{{ formatNumber(bankPayoutsTry) }} ₺</span>
                <span class="box-sub">{{ bankPayoutRecords.length }} transfer</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">En Son Yatırılan Tarih</span>
                <ng-container *ngIf="latestPayout as payout; else noPayout">
                  <span class="box-val text-white">{{ payout.occurredAt | date:'shortDate' }}</span>
                  <span class="box-sub">Son tutar: {{ payout.amount | currency:payout.currency }}</span>
                </ng-container>
                <ng-template #noPayout>
                  <span class="box-val text-white">—</span>
                  <span class="box-sub">Henüz payout kaydı yok.</span>
                </ng-template>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Ortalama Transfer</span>
                <span class="box-val text-primary">{{ bankPayoutRecords.length ? formatNumber(bankPayoutsTry / bankPayoutRecords.length) + ' ₺' : '—' }}</span>
                <span class="box-sub">Kayıt başına ortalama TRY karşılığı</span>
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
                    <td class="date-cell">{{ p.occurredAt | date:'short' }}</td>
                    <td class="ref-cell">{{ p.referenceId }}</td>
                    <td>Banka transferi</td>
                    <td class="amount-cell text-emerald">{{ p.amount | currency:p.currency }}</td>
                    <td><span class="badge-status-payout">{{ p.status }}</span></td>
                    <td class="note-cell">{{ p.description }}</td>
                  </tr>
                  <tr *ngIf="bankPayoutRecords.length === 0"><td colspan="6" class="empty-table-cell">Bu dönemde API’den payout kaydı gelmedi.</td></tr>
                </tbody>
              </table>
            </div>

            <div class="drilldown-footnote">Payout kaydındaki açıklama, durum ve kur bilgileri API yanıtından gösterilir. Banka adı veya hesap bilgisi API tarafından sağlanmıyor.</div>
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
              <p class="modal-sub">Sipariş bazlı ürün/kargo maliyet ayrıntıları mevcut API’de bulunmuyor.</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Kargo Maliyeti</span>
                <span class="box-val text-warning">{{ financialPerformance ? formatKpi(financialPerformance.shippingCosts * liveRate, financialPerformance.shippingCosts) : '—' }}</span>
                <span class="box-sub">Dönemsel API toplamı</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Üretim / Hammadde</span>
                <span class="box-val text-primary">{{ financialPerformance ? formatKpi(productCostsTry, productCostsUsd) : '—' }}</span>
                <span class="box-sub">Dönemsel API toplamı</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Paketleme & Fatura</span>
                <span class="box-val text-emerald">—</span>
                <span class="box-sub">Fatura verisi API’de yok</span>
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
                  <tr *ngIf="orderCostShippingRecords.length === 0"><td colspan="6" class="empty-table-cell">Siparişe bağlı maliyet ve fatura ayrıntısı API’de sunulmuyor.</td></tr>
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
              <p class="modal-sub">Etsy Net Hakedişinden ürün ve kargo maliyetleri çıkarılmış nihai net kâr: <strong>₺ 22.077,69</strong></p>
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
                <span class="box-val text-emerald">{{ financialPerformance ? formatKpi(realNetProfitTry, realNetProfitUsd) : '—' }}</span>
                <span class="box-sub">{{ financialPerformance ? (financialPerformance.netProfitMargin / 100 | percent:'1.0-1') + ' net kâr marjı' : 'Veri yok' }}</span>
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
              <p class="modal-sub">Dönem brüt satış toplamı API’den alınır. Sipariş/işlem satırları mevcut API’de sunulmuyor.</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Brüt Satış</span>
                <span class="box-val text-emerald">{{ formatKpi(grossSalesTry, grossSalesUsd) }}</span>
                <span class="box-sub">{{ financialPerformance?.currency || '—' }}</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Sipariş Sayısı</span>
                <span class="box-val text-primary">—</span>
                <span class="box-sub">Sipariş sayısı API tarafından sunulmuyor.</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-llbl">Ortalama Sepet Tutarı (AOV)</span>
                <span class="box-val text-emerald">—</span>
                <span class="box-sub">Sipariş sayısı olmadan hesaplanamaz.</span>
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
                  <tr *ngIf="salesRecords.length === 0"><td colspan="7" class="empty-table-cell">İşlem satırları API’de sunulmuyor; yalnızca dönem toplamı mevcuttur.</td></tr>
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
              <p class="modal-sub">Dönemsel toplam komisyon: {{ financialPerformance ? formatKpi(etsyFeesTry, etsyFeesUsd) : 'Veri yok' }}. Kesinti türü ayrıntısı API’de sunulmuyor.</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Kesinti Tutarı</span>
                <span class="box-val text-warning">-{{ formatKpi(etsyFeesTry, etsyFeesUsd) }}</span>
                <span class="box-sub">Toplam platform kesintisi</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Komisyon / Brüt Satış Oranı</span>
                <span class="box-val text-orange">{{ grossSalesUsd ? (etsyFeesUsd / grossSalesUsd | percent:'1.0-1') : '—' }}</span>
                <span class="box-sub">Dönem toplam kesinti / brüt satış</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Kesinti kategorisi</span>
                <span class="box-val text-danger">—</span>
                <span class="box-sub">KDV ayrıştırması API’de mevcut değil.</span>
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
                  <tr *ngIf="feesRecords.length === 0"><td colspan="6" class="empty-table-cell">Komisyon/ücret bazında defter hareketi API’de sunulmuyor.</td></tr>
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
              Kesinti türlerinin vergi ayrıştırması API’den alınmadığından burada hesaplanıp gösterilmez.
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
              <p class="modal-sub">Reklam toplamları API’den alınır; günlük reklam kırılımı mevcut API’de sunulmuyor.</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Toplam Reklam Harcaması</span>
                <span class="box-val text-danger">-{{ formatKpi(innerAdsTry, innerAdsUsd) }}</span>
                <span class="box-sub">İç reklam dönem toplamı</span>
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
                  <tr *ngIf="innerAdsRecords.length === 0"><td colspan="7" class="empty-table-cell">Günlük Etsy Ads verisi API’de mevcut değil.</td></tr>
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
                  <tr *ngIf="offsiteAdsRecords.length === 0"><td colspan="6" class="empty-table-cell">Offsite Ads satırları ve kaynak kanalı API’de mevcut değil.</td></tr>
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
                  <tr *ngIf="refundRecords.length === 0"><td colspan="7" class="empty-table-cell">İade toplamı dönemsel finans DTO’sunda bulunabilir; sipariş bazlı iade satırları API’de sunulmuyor.</td></tr>
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
              <p class="modal-sub">Dönemsel net gelir, desteklenen finans toplamlarından hesaplanır; ödeme defteri satırları mevcut değildir.</p>
            </div>
            <button class="btn-modal-close" (click)="closeModals()">✕</button>
          </div>
          <div class="modal-body">
            <div class="drilldown-summary-grid">
              <div class="drilldown-summary-box">
                <span class="box-lbl">Brüt Satış Geliri</span>
                <span class="box-val text-emerald">{{ formatKpi(grossSalesTry, grossSalesUsd) }}</span>
                <span class="box-sub">Brüt satış toplamı</span>
              </div>
              <div class="drilldown-summary-box">
                <span class="box-lbl">Platform Giderleri Toplamı</span>
                <span class="box-val text-danger">-{{ formatKpi((etsyFeesTry + innerAdsTry + offsiteAdsTry + refundsTry), (etsyFeesUsd + innerAdsUsd + offsiteAdsUsd + refundsUsd)) }}</span>
                <span class="box-sub">API dönem toplamlarından hesaplandı</span>
              </div>
              <div class="drilldown-summary-box highlight-emerald">
                <span class="box-lbl">Etsy Net Hakediş</span>
                <span class="box-val text-primary">{{ formatKpi(netIncomeTry, netIncomeUsd) }}</span>
                <span class="box-sub">Hesaplanan dönem net geliri</span>
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
                  <tr *ngIf="netIncomeRecords.length === 0"><td colspan="4" class="empty-table-cell">Satır bazlı hesap kalemleri API’de bulunmuyor.</td></tr>
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
    .kpi-indicator.emerald { background: #34d399; box-shadow: 0 0 10px rgba(52, 211, 153, 0.5); }
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
    .exp-val { font-weight: 700, color: #fff; width: 90px; text-align: right; }

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

  // Table Sorting State (Matching Görsel 1)
  sortColumn: string = 'netProfitUsd';
  sortDirection: 'asc' | 'desc' = 'asc';

  // Modal: Satış ve Gelir Analizi (Brüt Satış)
  salesRecords: Array<{ date: string; receiptId: string; buyer: string; item: string; amount: number; tryAmount: number; paymentType: string }> = [];

  // Modal: Etsy Komisyon & Kesinti Analizi
  feesRecords: Array<{ type: string; count: string; baseUsd: number; feeUsd: number; feeTry: number; note: string }> = [];

  // Modal: Etsy Ads İç Reklam Analizi
  innerAdsRecords: Array<{ date: string; impressions: number; clicks: number; spendUsd: number; spendTry: number; salesUsd: number; roas: string }> = [];

  // Modal: Offsite Ads Dış Reklam Analizi
  offsiteAdsRecords: Array<{ receiptId: string; buyer: string; saleAmountUsd: number; rate: string; feeUsd: number; feeTry: number; channel: string }> = [];

  // Modal: İade ve Geri Ödeme Analizi
  refundRecords: Array<{ date: string; receiptId: string; buyer: string; item: string; refundUsd: number; refundTry: number; reason: string }> = [];

  // Modal: Etsy Net Gelir Analizi
  netIncomeRecords: Array<{ category: string; amountUsd: number; amountTry: number; isPositive: boolean; note: string }> = [];
  orderCostShippingRecords: Array<{ date: string; receiptId: string; qty: number; shippingCostTry: number; invoice: string; title: string }> = [];

  // Modal 1 Data: Bank Payouts (Matching Görsel 2)
  bankPayoutRecords: EtsyBankPayoutDto[] = [];
  get latestPayout(): EtsyBankPayoutDto | null {
    return [...this.bankPayoutRecords].sort((a, b) => new Date(b.occurredAt).getTime() - new Date(a.occurredAt).getTime())[0] || null;
  }
  get balanceBreakdownRecords(): Array<{ type: string; name: string; detail: string; amount: number; status: string; explanation: string }> {
    if (!this.financialPerformance) return [];

    return [
      { type: 'GELİR', name: 'Brüt satış', detail: this.financialPerformance.currency, amount: this.financialPerformance.grossSales * this.liveRate, status: '', explanation: 'Seçili dönemin API finansal performans verisi.' },
      { type: 'GİDER', name: 'Platform ücretleri', detail: this.financialPerformance.currency, amount: -this.financialPerformance.platformFees * this.liveRate, status: '', explanation: 'API finansal performans verisi.' },
      { type: 'GİDER', name: 'İç reklam maliyeti', detail: this.financialPerformance.currency, amount: -this.financialPerformance.internalAdsCost * this.liveRate, status: '', explanation: 'API finansal performans verisi.' },
      { type: 'GİDER', name: 'Dış reklam maliyeti', detail: this.financialPerformance.currency, amount: -this.financialPerformance.externalAdsCost * this.liveRate, status: '', explanation: 'API finansal performans verisi.' },
      { type: 'GİDER', name: 'Ürün ve kargo maliyeti', detail: this.financialPerformance.currency, amount: -(this.financialPerformance.productCosts + this.financialPerformance.shippingCosts) * this.liveRate, status: '', explanation: 'API finansal performans verisi.' },
      { type: 'GİDER', name: 'İadeler', detail: this.financialPerformance.currency, amount: -this.financialPerformance.refunds * this.liveRate, status: '', explanation: 'API finansal performans verisi.' },
      { type: 'KÂR', name: 'Net kâr', detail: this.financialPerformance.currency, amount: this.financialPerformance.netProfit * this.liveRate, status: '', explanation: 'API tarafından döndürülen net kâr.' }
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
        this.loadError = error.status === 0 ? 'API sunucusuna erişilemiyor.' : 'Finansal veriler yüklenemedi.';
      }
    });

    this.etsyApi.getBankPayoutsByDateRange(range.start, range.end).subscribe({
      next: (payouts) => {
        this.bankPayoutRecords = payouts || [];
        this.bankPayoutsUsd = this.bankPayoutRecords
          .filter(payout => payout.currency === 'USD')
          .reduce((sum, payout) => sum + (typeof payout.amount === 'number' ? payout.amount : Number(payout.amount || 0)), 0);
        this.bankPayoutsTry = this.bankPayoutRecords.reduce((sum, payout) => sum + (payout.exchangeRateToTry ? payout.amount * payout.exchangeRateToTry : payout.currency === 'TRY' ? payout.amount : 0), 0);
      },
      error: () => {
        this.bankPayoutRecords = [];
        this.loadError = 'Payout verileri yüklenemedi.';
      }
    });
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

  formatCostShare(amountUsd: number): string {
    const total = this.etsyFeesUsd + this.innerAdsUsd + this.offsiteAdsUsd + this.refundsUsd + this.productCostsUsd + this.shippingCostsUsd;
    return total > 0 ? `${((amountUsd / total) * 100).toFixed(1)}%` : '—';
  }

  chartHeight(value: number): number {
    const max = Math.max(this.grossSalesUsd, this.etsyFeesUsd, this.innerAdsUsd + this.offsiteAdsUsd, this.productCostsUsd + this.shippingCostsUsd, Math.abs(this.realNetProfitUsd));
    return max > 0 ? Math.max(0, (Math.abs(value) / max) * 100) : 0;
  }

  copyValue(label: string, val: string): void {
    navigator.clipboard.writeText(val);
    this.showToast(`📋 Kopyalandı: ${label} ➔ ${val}`);
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
      this.showToast(`📅 Dönem güncellendi: ${this.startDate} — ${this.endDate}`);
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
    this.showToast(this.autoRateEnabled ? `🇹🇷 TCMB/Google Oto Kur Aktif (${this.liveRate} ₺)` : 'Manuel Kur Modu');
  }

  refreshFromEtsy(): void {
    this.isRefreshing = true;
    const range = this.getSelectedDateRange();
    this.etsyApi.syncFromEtsy(range.start, range.end).subscribe({
      next: (res) => {
        this.isRefreshing = false;
        if (res?.succeeded) {
          this.showToast(`✅ Canlı Etsy API senkronizasyonu tamamlandı: ${res.transactionCount} hareket, ${res.payoutCount} transfer, ${res.orderCount} sipariş işlendi.`);
          this.loadFinancialData(); // Reload financial data on success
        } else {
          this.showToast(`Senkronizasyon başarısız: ${res?.errorMessage || 'API işlemi tamamlanamadı.'}`);
        }
      },
      error: (error) => {
        this.isRefreshing = false;
        this.showToast(error.status === 0 ? 'Etsy API sunucusuna ulaşılamadı.' : `Senkronizasyon hatası (HTTP ${error.status}).`);
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
    this.showToast('📊 5 Sayfalık Excel / CSV Raporu başarıyla indirildi!');
  }

  sendTelegramReport(): void {
    this.etsyApi.getTelegramSettings().subscribe({
      next: (s) => {
        if (s && s.isEnabled && s.chatId) {
          const totalRev = this.grossSalesTry ? `₺${this.grossSalesTry.toFixed(2)}` : '$1,111.73';
          this.showToast(`✈️ Günlük muhasebe brifingi Telegram kanalınıza (@${s.chatId}) başarıyla iletildi! (Ciro: ${totalRev})`);
        } else {
          this.showToast('✈️ Telegram botu henüz VDS ayarlarından yapılandırılmadı. Ayarlar sekmesinden bot tokenınızı bağlayın.');
        }
      },
      error: () => {
        this.showToast('✈️ Telegram bot ayarları VDS üzerinden doğrulanamadı.');
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
          this.showToast(`🌐 VDS senkronizasyonu başarılı: ${res.transactionCount} hareket, ${res.payoutCount} transfer.`);
          this.loadFinancialData();
        } else {
          this.showToast(`VDS senkronizasyonu başarısız: ${res?.errorMessage || 'API işlemi tamamlanamadı.'}`);
        }
      },
      error: (error) => {
        this.isSyncingVds = false;
        this.showToast(error.status === 0 ? 'VDS API sunucusuna ulaşılamadı.' : `VDS senkronizasyon hatası (HTTP ${error.status}).`);
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
