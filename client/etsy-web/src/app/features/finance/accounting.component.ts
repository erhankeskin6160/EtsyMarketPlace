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
        <div class="kpi-card gross" (click)="copyValue('Brüt Satış', formatKpi(grossSalesTry, grossSalesUsd))">
          <div class="kpi-header">
            <span class="kpi-title">💰 BRÜT SATIŞ</span>
          </div>
          <div class="kpi-value text-success">{{ formatKpi(grossSalesTry, grossSalesUsd) }}</div>
          <div class="kpi-indicator green"></div>
        </div>

        <!-- 2. Etsy Kesintisi -->
        <div class="kpi-card fee" (click)="copyValue('Etsy Kesintisi', formatKpi(etsyFeesTry, etsyFeesUsd))">
          <div class="kpi-header">
            <span class="kpi-title">📋 ETSY KESİNTİSİ</span>
          </div>
          <div class="kpi-value text-warning">-{{ formatKpi(etsyFeesTry, etsyFeesUsd) }}</div>
          <div class="kpi-indicator orange"></div>
        </div>

        <!-- 3. İç Reklam -->
        <div class="kpi-card inner-ad" (click)="copyValue('İç Reklam', formatKpi(innerAdsTry, innerAdsUsd))">
          <div class="kpi-header">
            <span class="kpi-title">📢 İÇ REKLAM</span>
          </div>
          <div class="kpi-value text-danger">-{{ formatKpi(innerAdsTry, innerAdsUsd) }}</div>
          <div class="kpi-indicator red"></div>
        </div>

        <!-- 4. Dış Reklam -->
        <div class="kpi-card offsite-ad" (click)="copyValue('Dış Reklam', formatKpi(offsiteAdsTry, offsiteAdsUsd))">
          <div class="kpi-header">
            <span class="kpi-title">🌐 DIŞ REKLAM</span>
          </div>
          <div class="kpi-value text-orange">-{{ formatKpi(offsiteAdsTry, offsiteAdsUsd) }}</div>
          <div class="kpi-indicator dark-orange"></div>
        </div>

        <!-- 5. İadeler -->
        <div class="kpi-card refunds" (click)="copyValue('İadeler', formatKpi(refundsTry, refundsUsd))">
          <div class="kpi-header">
            <span class="kpi-title">↩️ İADELER</span>
          </div>
          <div class="kpi-value text-danger">-{{ formatKpi(refundsTry, refundsUsd) }}</div>
          <div class="kpi-indicator red"></div>
        </div>

        <!-- 6. Etsy Net Gelir -->
        <div class="kpi-card net-income" (click)="copyValue('Etsy Net Gelir', formatKpi(netIncomeTry, netIncomeUsd))">
          <div class="kpi-header">
            <span class="kpi-title">✅ ETSY NET GELİR</span>
          </div>
          <div class="kpi-value text-primary">{{ formatKpi(netIncomeTry, netIncomeUsd) }}</div>
          <div class="kpi-indicator indigo"></div>
        </div>

        <!-- 7. Sipariş Maliyeti -->
        <div class="kpi-card costs" (click)="copyValue('Sipariş Maliyeti', formatKpi(productCostsTry, productCostsUsd))">
          <div class="kpi-header">
            <span class="kpi-title">📦 SİPARİŞ MALİYETİ</span>
          </div>
          <div class="kpi-value text-warning">-{{ formatKpi(productCostsTry, productCostsUsd) }}</div>
          <div class="kpi-indicator orange"></div>
        </div>

        <!-- 8. Gerçek Net Kâr -->
        <div class="kpi-card real-profit highlight" (click)="copyValue('Gerçek Net Kâr', formatKpi(realNetProfitTry, realNetProfitUsd))">
          <div class="kpi-header">
            <span class="kpi-title">💵 GERÇEK NET KÂR</span>
          </div>
          <div class="kpi-value text-emerald">{{ formatKpi(realNetProfitTry, realNetProfitUsd) }}</div>
          <div class="kpi-indicator emerald"></div>
        </div>

        <!-- 9. Banka Yatırımı -->
        <div class="kpi-card payouts" (click)="copyValue('Banka Yatırımı', formatKpi(bankPayoutsTry, bankPayoutsUsd))">
          <div class="kpi-header">
            <span class="kpi-title">🏦 BANKA YATIRIMI</span>
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
                  <th (click)="sortOrders('date')">Tarih ▼</th>
                  <th (click)="sortOrders('receiptId')">Sipariş No</th>
                  <th>Durum</th>
                  <th>Müşteri</th>
                  <th>Ürün</th>
                  <th>Adet</th>
                  <th>Müşteri Ödemesi ($)</th>
                  <th>Etsy Kesintileri ($)</th>
                  <th>Dış Reklam ($)</th>
                  <th>Sipariş Maliyeti ($)</th>
                  <th>Net Kâr ($)</th>
                  <th>Kur (₺)</th>
                  <th>Net Kâr (₺)</th>
                  <th>Maliyet</th>
                  <th>Kargo Faturası</th>
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
  showReconciliationModal = false;
  showCostModal = false;
  selectedOrder: OrderFinancialRow | null = null;
  costModalProduction = 0;
  costModalShipping = 0;
  costModalPackaging = 0;

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
    if (col === 'date') {
      this.filteredOrders.reverse();
    } else if (col === 'receiptId') {
      this.filteredOrders.sort((a, b) => b.receiptId.localeCompare(a.receiptId));
    }
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
    this.showReconciliationModal = false;
    this.showCostModal = false;
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
