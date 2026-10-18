import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  EtsyApiService,
  CompetitorShopAnalysisDto,
  CompetitorListingDto,
  SeasonalTrendItemDto
} from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';

const COMPETITOR_STORAGE_KEY = 'etsy_competitor_shops_v2';

export interface WatchedCompetitorShop {
  shopName: string;
  shopUrl: string;
  totalSalesCount: number;
  prevTotalSalesCount?: number;
  estimatedMonthlySales: number;
  estimatedMonthlyRevenueUsd: number;
  prevEstimatedMonthlyRevenueUsd?: number;
  activeListingsCount: number;
  prevActiveListingsCount?: number;
  averagePriceUsd: number;
  competitorStrengthScore: number;
  averageLqsGrade: string;
  topItemTitle: string;
  topItemPrice: number;
  topItemTags: string[];
  lastAnalyzedAt: string;
}

export interface LqsAuditCheck {
  icon: '✅' | '⚠️' | '❌';
  label: string;
  detail: string;
  passed: boolean;
}

@Component({
  selector: 'app-competitor-spy',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="spy-view">
      <!-- HEADER & MAIN TAB SWITCHER -->
      <div class="spy-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M2 12s3-7 10-7 10 7 10 7-3 7-10 7-10-7-10-7Z"></path>
              <circle cx="12" cy="12" r="3"></circle>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Rakip & Trend Casusu (EverBee + eRank Intelligence)</h1>
            <p class="page-subtitle">Canlı Etsy v3 rakip mağaza röntgeni, ürün bazlı ciro tahmini, LQS denetimi ve yıllık sezon takvimi</p>
          </div>
        </div>

        <div class="main-tabs">
          <button
            type="button"
            class="main-tab-btn"
            [class.active]="activeMainTab === 'competitor'"
            (click)="activeMainTab = 'competitor'">
            🕵️ Rakip Mağaza Casusu
          </button>
          <button
            type="button"
            class="main-tab-btn"
            [class.active]="activeMainTab === 'seasonal'"
            (click)="switchToSeasonalTab()">
            📅 Dönemsel Trend & Q4 Radarı
          </button>
        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div *ngIf="toastMessage" class="toast-banner">
        {{ toastMessage }}
      </div>

      <!-- ========================================================= -->
      <!-- TAB 1: RAKİP MAĞAZA CASUSU                                 -->
      <!-- ========================================================= -->
      <ng-container *ngIf="activeMainTab === 'competitor'">
        <!-- SEARCH & ACTION BAR -->
        <div class="search-bar-card">
          <div class="search-input-group">
            <span class="search-icon">🔍</span>
            <input
              type="text"
              class="shop-input"
              [(ngModel)]="shopQueryInput"
              (keyup.enter)="analyzeShop()"
              placeholder="Rakip Etsy mağaza adı veya linki girin (Örn: CaitlynMinimalist veya https://www.etsy.com/shop/...)" />
          </div>

          <div class="search-controls">
            <label class="limit-label">
              Örneklem:
              <select [(ngModel)]="sampleLimit" class="limit-select">
                <option [ngValue]="25">25 Ürün</option>
                <option [ngValue]="50">50 Ürün</option>
                <option [ngValue]="100">100 Ürün</option>
              </select>
            </label>

            <button
              type="button"
              class="btn-primary-analyze"
              [disabled]="isAnalyzing"
              (click)="analyzeShop()">
              <span *ngIf="!isAnalyzing">🚀 Mağazayı Canlı Analiz Et</span>
              <span *ngIf="isAnalyzing">⏳ Etsy v3 Taranıyor...</span>
            </button>

            <button
              type="button"
              class="btn-secondary-action"
              *ngIf="analysisResult?.found"
              (click)="saveCurrentShopToWatchlist()">
              📌 Takip Listesine Kaydet / Güncelle
            </button>

            <button
              type="button"
              class="btn-secondary-action"
              *ngIf="analysisResult?.found && listings.length > 0"
              (click)="exportCsv()">
              📥 CSV İndir
            </button>

            <button
              type="button"
              class="btn-secondary-action"
              *ngIf="analysisResult?.found && analysisResult?.shop?.shopUrl"
              (click)="openExternalUrl(analysisResult!.shop.shopUrl)">
              🌐 Etsy'de Aç
            </button>
          </div>
        </div>

        <!-- WATCHLIST HORIZONTAL STRIP -->
        <div class="watchlist-strip" *ngIf="watchedShops.length > 0">
          <div class="watchlist-header">
            <span class="watchlist-title">📌 Takip Edilen Rakip Mağazalar ({{ watchedShops.length }})</span>
            <span class="watchlist-hint">Mağaza kartına tıklayarak canlı Etsy v3 röntgenini ve önceki analize göre değişim (Δ) farkını görüntüleyin</span>
          </div>
          <div class="watchlist-cards">
            <div
              *ngFor="let w of watchedShops"
              class="watch-card"
              [class.active]="analysisResult?.shop?.shopName?.toLowerCase() === w.shopName.toLowerCase()"
              (click)="selectWatchedShop(w)">
              <div class="watch-card-top">
                <span class="watch-shop-name">{{ w.shopName }}</span>
                <div class="watch-badges">
                  <span class="lqs-mini-badge" [ngClass]="getLqsClass(w.averageLqsGrade)">LQS {{ w.averageLqsGrade || 'B' }}</span>
                  <button
                    type="button"
                    class="btn-remove-watch"
                    title="Takipten Çıkar"
                    (click)="removeWatchedShop(w.shopName, $event)">✕</button>
                </div>
              </div>
              <div class="watch-metrics">
                <div class="w-metric">
                  <span class="w-label">Aylık Ciro (Tah.):</span>
                  <span class="w-val revenue">&#36;{{ (w.estimatedMonthlyRevenueUsd || 0) | number:'1.0-0' }}</span>
                  <span
                    *ngIf="getDeltaDiff(w.estimatedMonthlyRevenueUsd, w.prevEstimatedMonthlyRevenueUsd) !== 0"
                    class="delta-pill"
                    [class.up]="getDeltaDiff(w.estimatedMonthlyRevenueUsd, w.prevEstimatedMonthlyRevenueUsd) > 0"
                    [class.down]="getDeltaDiff(w.estimatedMonthlyRevenueUsd, w.prevEstimatedMonthlyRevenueUsd) < 0">
                    {{ formatDelta(w.estimatedMonthlyRevenueUsd, w.prevEstimatedMonthlyRevenueUsd, true) }}
                  </span>
                </div>
                <div class="w-metric">
                  <span class="w-label">Toplam Satış:</span>
                  <span class="w-val">{{ (w.totalSalesCount || 0) | number }}</span>
                  <span
                    *ngIf="getDeltaDiff(w.totalSalesCount, w.prevTotalSalesCount) !== 0"
                    class="delta-pill"
                    [class.up]="getDeltaDiff(w.totalSalesCount, w.prevTotalSalesCount) > 0"
                    [class.down]="getDeltaDiff(w.totalSalesCount, w.prevTotalSalesCount) < 0">
                    {{ formatDelta(w.totalSalesCount, w.prevTotalSalesCount, false) }}
                  </span>
                </div>
                <div class="w-metric">
                  <span class="w-label">Aktif İlan:</span>
                  <span class="w-val">{{ w.activeListingsCount || 0 }}</span>
                </div>
              </div>
            </div>
          </div>
        </div>

        <!-- ERROR BANNER -->
        <div *ngIf="errorMessage" class="error-banner">
          ⚠️ {{ errorMessage }}
        </div>

        <!-- EMPTY STATE WHEN NO SHOP ANALYZED YET -->
        <div *ngIf="!analysisResult && !isAnalyzing && !errorMessage" class="empty-hero-card">
          <div class="empty-hero-icon">🕵️‍♂️</div>
          <h3>Rakip Mağazanın Röntgenini Çekin</h3>
          <p>
            Herhangi bir Etsy mağazasının adını veya linkini yukarıdaki arama çubuğuna yazıp
            <strong>"Mağazayı Canlı Analiz Et"</strong> butonuna tıklayın. EverBee ağırlıklı aylık satış/ciro tahminleri,
            eRank LQS kalite notları (A+..D), en çok kullanılan 25 etiket ve AI rekabet açığı raporu saniyeler içinde hazırlanır.
          </p>
        </div>

        <!-- LIVE ANALYSIS RESULTS -->
        <ng-container *ngIf="analysisResult?.found && analysisResult?.shop as shop">
          <!-- 7 KPI CARDS -->
          <div class="kpi-grid-7">
            <div class="kpi-card">
              <span class="kpi-title">TOPLAM SATIŞ</span>
              <div class="kpi-value">{{ shop.totalSalesCount | number }}</div>
              <span class="kpi-sub">Mağaza Yaşı: {{ shop.shopAgeDays }} gün</span>
            </div>

            <div class="kpi-card">
              <span class="kpi-title">MAĞAZA PUANI</span>
              <div class="kpi-value star-val">★ {{ shop.reviewAverage | number:'1.1-2' }}</div>
              <span class="kpi-sub">{{ shop.reviewCount | number }} müşteri yorumu</span>
            </div>

            <div class="kpi-card">
              <span class="kpi-title">AKTİF ÜRÜN</span>
              <div class="kpi-value">{{ shop.activeListingsCount | number }}</div>
              <span class="kpi-sub">🔥 {{ shop.bestsellerCount }} Best Seller aday</span>
            </div>

            <div class="kpi-card">
              <span class="kpi-title">ORTALAMA FİYAT</span>
              <div class="kpi-value">&#36;{{ shop.averagePriceUsd | number:'1.2-2' }}</div>
              <span class="kpi-sub">Medyan: &#36;{{ shop.medianPriceUsd | number:'1.2-2' }} (₺{{ (shop.averagePriceUsd * usdTryRate) | number:'1.0-0' }})</span>
            </div>

            <div class="kpi-card">
              <span class="kpi-title">GÜNLÜK SATIŞ HIZI</span>
              <div class="kpi-value velocity-val">{{ shop.dailySalesVelocity | number:'1.1-2' }} <small>/gün</small></div>
              <span class="kpi-sub">Aylık Tah.: ~{{ shop.estimatedMonthlySales | number }} satış</span>
            </div>

            <div class="kpi-card highlight-revenue">
              <span class="kpi-title">TAHMİNİ AYLIK CİRO</span>
              <div class="kpi-value revenue-val">&#36;{{ shop.estimatedMonthlyRevenueUsd | number:'1.0-0' }}</div>
              <span class="kpi-sub">≈ ₺{{ (shop.estimatedMonthlyRevenueUsd * usdTryRate) | number:'1.0-0' }} / ay</span>
            </div>

            <div class="kpi-card">
              <span class="kpi-title">RAKİP GÜÇ SKORU</span>
              <div class="kpi-value" [ngClass]="getStrengthClass(shop.competitorStrengthScore)">
                {{ shop.competitorStrengthScore }}/100
              </div>
              <span class="kpi-sub">Ort. SEO/LQS Notu: <strong>{{ shop.averageLqsGrade }}</strong></span>
            </div>
          </div>

          <!-- MAIN WORKSPACE: LEFT PRODUCT TABLE + RIGHT SELECTED PRODUCT & INTELLIGENCE TABS -->
          <div class="workspace-split">
            <!-- LEFT: PRODUCT LISTINGS TABLE -->
            <div class="listings-panel glass-card">
              <div class="panel-toolbar">
                <div class="toolbar-left">
                  <span class="panel-title">📦 Rakibin Ürün Portföyü ({{ filteredListings.length }})</span>
                </div>
                <div class="toolbar-filters">
                  <input
                    type="text"
                    class="filter-input"
                    [(ngModel)]="listingSearchText"
                    (ngModelChange)="applyListingFilters()"
                    placeholder="Ürün başlığı veya etikette ara..." />

                  <select [(ngModel)]="priceBandFilter" (ngModelChange)="applyListingFilters()" class="filter-select">
                    <option value="all">Tüm Fiyatlar</option>
                    <option value="under30">$0 – $30</option>
                    <option value="30to75">$30 – $75</option>
                    <option value="over75">$75+</option>
                  </select>

                  <select [(ngModel)]="listingSortBy" (ngModelChange)="applyListingFilters()" class="filter-select">
                    <option value="revenue">Sırala: Tahmini Aylık Ciro</option>
                    <option value="sales">Sırala: Tahmini Aylık Satış</option>
                    <option value="favorites">Sırala: Favori Sayısı</option>
                    <option value="views">Sırala: Görüntülenme</option>
                    <option value="lqs">Sırala: LQS Kalite Puanı</option>
                    <option value="price_desc">Sırala: Fiyat (Azalan)</option>
                    <option value="price_asc">Sırala: Fiyat (Artan)</option>
                  </select>
                </div>
              </div>

              <div class="table-scroll">
                <table class="spy-table">
                  <thead>
                    <tr>
                      <th>#</th>
                      <th>Ürün</th>
                      <th>Fiyat</th>
                      <th>Tah. Satış/Ay</th>
                      <th>Tah. Ciro/Ay</th>
                      <th>Favori / Gör.</th>
                      <th>Hız Rozeti</th>
                      <th>LQS</th>
                      <th>İşlem</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      *ngFor="let item of filteredListings"
                      [class.selected-row]="selectedListing?.listingId === item.listingId"
                      (click)="selectListing(item)">
                      <td class="rank-cell">{{ item.rank }}</td>
                      <td class="product-cell">
                        <img
                          *ngIf="item.imageUrl"
                          [src]="item.imageUrl"
                          alt=""
                          class="thumb-img" />
                        <div class="prod-info">
                          <div class="prod-title" [title]="item.title">{{ item.title }}</div>
                          <div class="prod-meta">
                            <span>🏷️ {{ item.tagCount }}/13 etiket</span>
                            <span>📷 {{ item.imageCount }} görsel</span>
                          </div>
                        </div>
                      </td>
                      <td class="price-cell">&#36;{{ item.priceUsd | number:'1.2-2' }}</td>
                      <td class="num-cell">{{ item.estimatedMonthlySales | number }} ad.</td>
                      <td class="rev-cell">&#36;{{ item.estimatedMonthlyRevenueUsd | number:'1.0-0' }}</td>
                      <td class="num-cell">
                        <span class="fav-num">❤️ {{ item.favorites | number }}</span>
                        <span class="view-num">👁️ {{ item.views | number }}</span>
                      </td>
                      <td>
                        <span class="velocity-badge" [ngClass]="getVelocityBadgeClass(item.salesVelocityBadge)">
                          {{ item.salesVelocityBadge }}
                        </span>
                      </td>
                      <td>
                        <span class="lqs-badge" [ngClass]="getLqsClass(item.lqsGrade)">
                          {{ item.lqsGrade }} ({{ item.lqsScore }})
                        </span>
                      </td>
                      <td class="action-cell" (click)="$event.stopPropagation()">
                        <button
                          type="button"
                          class="btn-clone-mini"
                          title="Bu ürünü Hızlı İlan Oluşturucuya Klonla"
                          (click)="cloneListingToFastCreator(item)">
                          🚀 Klonla
                        </button>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- RIGHT: SELECTED PRODUCT DETAIL + 3 INTELLIGENCE TABS -->
            <div class="intelligence-panel">
              <!-- SELECTED LISTING QUICK CARD -->
              <div class="glass-card selected-product-card" *ngIf="selectedListing as sel">
                <div class="sel-header">
                  <div class="sel-thumb-wrap" *ngIf="sel.imageUrl">
                    <img [src]="sel.imageUrl" alt="" class="sel-thumb" />
                  </div>
                  <div class="sel-head-info">
                    <div class="sel-badges">
                      <span class="velocity-badge" [ngClass]="getVelocityBadgeClass(sel.salesVelocityBadge)">
                        {{ sel.salesVelocityBadge }}
                      </span>
                      <span class="lqs-badge" [ngClass]="getLqsClass(sel.lqsGrade)">
                        eRank LQS: {{ sel.lqsGrade }} ({{ sel.lqsScore }}/100)
                      </span>
                    </div>
                    <h3 class="sel-title">{{ sel.title }}</h3>
                    <div class="sel-price-row">
                      <span class="sel-price">&#36;{{ sel.priceUsd | number:'1.2-2' }}</span>
                      <span class="sel-try">(≈ ₺{{ (sel.priceUsd * usdTryRate) | number:'1.0-0' }})</span>
                      <span class="sel-est-rev">• Aylık Tah. Ciro: <strong>&#36;{{ sel.estimatedMonthlyRevenueUsd | number:'1.0-0' }}</strong></span>
                    </div>
                  </div>
                </div>

                <div class="sel-actions-bar">
                  <button type="button" class="btn-clone-primary" (click)="cloneListingToFastCreator(sel)">
                    🚀 Hızlı İlan Oluşturucuya Klonla
                  </button>
                  <button
                    type="button"
                    class="btn-copy-tags"
                    [disabled]="!sel.tags || sel.tags.length === 0"
                    (click)="copyTags(sel.tags)">
                    📋 {{ sel.tags.length }} Etiketi Kopyala
                  </button>
                  <button
                    type="button"
                    class="btn-open-etsy"
                    *ngIf="sel.listingUrl"
                    (click)="openExternalUrl(sel.listingUrl)">
                    🌐 İlanı Aç
                  </button>
                </div>

                <!-- SELECTED ITEM 13 TAGS CLOUD -->
                <div class="sel-tags-box">
                  <span class="sub-box-title">Seçili Ürünün Aktif Arama Etiketleri ({{ sel.tags.length }}/13):</span>
                  <div class="tags-cloud">
                    <span
                      *ngFor="let t of sel.tags"
                      class="tag-chip"
                      title="Tıkla: Bu etiketi Pazar Araştırmasında analiz et"
                      (click)="navigateToMarketResearch(t)">
                      #{{ t }}
                    </span>
                    <span *ngIf="sel.tags.length === 0" class="muted-text">Bu üründe etiket bulunamadı.</span>
                  </div>
                </div>
              </div>

              <!-- 3 INTELLIGENCE SUB-TABS -->
              <div class="glass-card subtabs-card">
                <div class="subtab-nav">
                  <button
                    type="button"
                    class="subtab-btn"
                    [class.active]="activeIntelTab === 'tags'"
                    (click)="activeIntelTab = 'tags'">
                    🏷️ Mağaza Etiket & Kelime Röntgeni
                  </button>
                  <button
                    type="button"
                    class="subtab-btn"
                    [class.active]="activeIntelTab === 'gap'"
                    (click)="activeIntelTab = 'gap'">
                    🎯 AI Rakip Açığı & Strateji
                  </button>
                  <button
                    type="button"
                    class="subtab-btn"
                    [class.active]="activeIntelTab === 'lqs'"
                    (click)="activeIntelTab = 'lqs'">
                    📊 eRank LQS Ürün Denetimi
                  </button>
                </div>

                <!-- SUBTAB 1: TOP TAGS & TITLE WORDS -->
                <div class="subtab-body" *ngIf="activeIntelTab === 'tags'">
                  <div class="freq-columns">
                    <div class="freq-col">
                      <div class="freq-col-header">
                        <h4>En Çok Kullanılan Etiketler (Top 25)</h4>
                        <button
                          type="button"
                          class="btn-mini-copy"
                          (click)="copyTopShopTags()">
                          İlk 13'ü Kopyala
                        </button>
                      </div>
                      <div class="freq-list">
                        <div
                          *ngFor="let tf of analysisResult!.topTags"
                          class="freq-row"
                          (click)="navigateToMarketResearch(tf.term)">
                          <span class="freq-term">#{{ tf.term }}</span>
                          <div class="freq-bar-wrap">
                            <div class="freq-bar" [style.width.%]="tf.usagePercentage"></div>
                          </div>
                          <span class="freq-count">{{ tf.count }} ilan (%{{ tf.usagePercentage }})</span>
                        </div>
                      </div>
                    </div>

                    <div class="freq-col">
                      <div class="freq-col-header">
                        <h4>Başlıklarda En Çok Geçen Kelimeler</h4>
                      </div>
                      <div class="freq-list">
                        <div
                          *ngFor="let wf of analysisResult!.topTitleWords"
                          class="freq-row"
                          (click)="navigateToMarketResearch(wf.term)">
                          <span class="freq-term">{{ wf.term }}</span>
                          <div class="freq-bar-wrap">
                            <div class="freq-bar word-bar" [style.width.%]="wf.usagePercentage"></div>
                          </div>
                          <span class="freq-count">{{ wf.count }} kez (%{{ wf.usagePercentage }})</span>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- SUBTAB 2: AI COMPETITIVE GAP INSIGHT -->
                <div class="subtab-body" *ngIf="activeIntelTab === 'gap'">
                  <div class="ai-insight-box">
                    <div class="ai-badge-row">
                      <span class="ai-pill">✨ EverBee & eRank Rekabet İstihbaratı</span>
                    </div>
                    <p class="ai-insight-text">{{ analysisResult!.aiCompetitiveGapInsight }}</p>

                    <div class="gap-metrics-grid">
                      <div class="gap-metric-item">
                        <span class="gm-label">Fiyat Konumlandırma Fırsatı</span>
                        <span class="gm-val">
                          &#36;{{ (shop.averagePriceUsd * 0.90) | number:'1.2-2' }} – &#36;{{ (shop.averagePriceUsd * 0.95) | number:'1.2-2' }}
                        </span>
                        <span class="gm-hint">Rakibin ortalama fiyatının %5-%10 altında giriş yaparak dönüşüm avantajı yakalayın.</span>
                      </div>
                      <div class="gap-metric-item">
                        <span class="gm-label">Zayıf LQS İlan Oranı (C/D Notu)</span>
                        <span class="gm-val">{{ getWeakLqsCount() }} / {{ listings.length }} ilan</span>
                        <span class="gm-hint">Rakibin eksik etiket/görsel bıraktığı ürünlerde 13 tam etiket ve 10 görselle üst sıraya çıkın.</span>
                      </div>
                    </div>
                  </div>
                </div>

                <!-- SUBTAB 3: ERANK LQS AUDIT FOR SELECTED LISTING -->
                <div class="subtab-body" *ngIf="activeIntelTab === 'lqs'">
                  <ng-container *ngIf="selectedListing as sel">
                    <div class="lqs-audit-header">
                      <span>Seçili Ürün LQS Kalite Denetimi: <strong>{{ sel.lqsGrade }} ({{ sel.lqsScore }}/100)</strong></span>
                    </div>
                    <div class="lqs-checks-list">
                      <div *ngFor="let chk of getListingLqsAudit(sel)" class="lqs-check-item" [class.failed]="!chk.passed">
                        <span class="chk-icon">{{ chk.icon }}</span>
                        <div class="chk-content">
                          <div class="chk-label">{{ chk.label }}</div>
                          <div class="chk-detail">{{ chk.detail }}</div>
                        </div>
                      </div>
                    </div>
                  </ng-container>
                </div>
              </div>
            </div>
          </div>
        </ng-container>
      </ng-container>

      <!-- ========================================================= -->
      <!-- TAB 2: DÖNEMSEL TREND & Q4 RADARI (FAZ 4)                 -->
      <!-- ========================================================= -->
      <ng-container *ngIf="activeMainTab === 'seasonal'">
        <div class="seasonal-intro glass-card">
          <div>
            <h2 class="seasonal-heading">📅 Yıllık Etsy Dönemsel Trend & Q4 Hazırlık Takvimi</h2>
            <p class="seasonal-sub">
              Etsy arama algoritmasında mevsimsel nişlerin organik indeksleme süresi ortalama <strong>45-60 gündür</strong>.
              Zirve sezonlarından önce hangi anahtar kelimelere ve 13 etikete odaklanmanız gerektiğini aşağıdan takip edin ve tek tıkla aksiyon alın.
            </p>
          </div>
        </div>

        <div class="seasonal-grid">
          <div *ngFor="let tr of seasonalTrends" class="seasonal-card glass-card">
            <div class="sc-top">
              <div class="sc-title-group">
                <span class="sc-icon">{{ tr.icon }}</span>
                <div>
                  <h3 class="sc-name">{{ tr.eventName }}</h3>
                  <span class="sc-peak">Zirve Dönemi: {{ tr.peakMonth }} • Hazırlık: {{ tr.prepStartDate }}</span>
                </div>
              </div>
              <span
                class="sc-status-badge"
                [style.background]="tr.statusColorHex + '22'"
                [style.borderColor]="tr.statusColorHex"
                [style.color]="tr.statusColorHex">
                {{ tr.statusBadge }}
              </span>
            </div>

            <div class="sc-metrics-row">
              <div class="sc-pill">⏳ Zirveye <strong>{{ tr.daysLeftToPeak }} gün</strong></div>
              <div class="sc-pill">📈 Trafik Çarpanı: <strong>{{ tr.estimatedTrafficMultiplier }}x</strong></div>
              <div class="sc-pill">🎯 Odak: {{ tr.categoryFocus }}</div>
            </div>

            <p class="sc-advice">{{ tr.actionAdvice }}</p>

            <div class="sc-section">
              <span class="sc-sec-label">🔥 Yüksek Talep Gören Arama Kelimeleri (Tıkla & Pazar Analizi Yap):</span>
              <div class="tags-cloud">
                <span
                  *ngFor="let kw of tr.highDemandKeywords"
                  class="kw-chip"
                  (click)="navigateToMarketResearch(kw)">
                  🔍 {{ kw }}
                </span>
              </div>
            </div>

            <div class="sc-section">
              <div class="sc-sec-head">
                <span class="sc-sec-label">🏷️ Önerilen Altın Etiketler:</span>
                <div class="sc-sec-actions">
                  <button type="button" class="btn-mini-copy" (click)="copyTags(tr.winningTags)">
                    📋 Etiketleri Kopyala
                  </button>
                  <button type="button" class="btn-mini-clone" (click)="sendSeasonalTrendToFastCreator(tr)">
                    🚀 Bu Trendle İlan Oluştur
                  </button>
                </div>
              </div>
              <div class="tags-cloud">
                <span *ngFor="let tag of tr.winningTags" class="tag-chip">#{{ tag }}</span>
              </div>
            </div>
          </div>
        </div>
      </ng-container>
    </div>
  `,
  styles: [`
    .spy-view {
      padding: 22px 26px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      display: flex;
      flex-direction: column;
      gap: 18px;
      box-sizing: border-box;
    }
    .spy-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 14px;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .icon-box {
      width: 46px;
      height: 46px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(168, 85, 247, 0.25), rgba(99, 102, 241, 0.25));
      border: 1px solid rgba(168, 85, 247, 0.45);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #c084fc;
    }
    .page-title {
      font-size: 1.3rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 3px 0 0 0;
    }
    .main-tabs {
      display: flex;
      gap: 8px;
      background: rgba(15, 23, 42, 0.75);
      padding: 5px;
      border-radius: 10px;
      border: 1px solid rgba(255, 255, 255, 0.08);
    }
    .main-tab-btn {
      padding: 9px 16px;
      border: none;
      background: transparent;
      color: #94a3b8;
      font-weight: 600;
      font-size: 0.83rem;
      border-radius: 7px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .main-tab-btn.active {
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      color: #ffffff;
      box-shadow: 0 4px 12px rgba(99, 102, 241, 0.35);
    }

    .toast-banner {
      padding: 10px 16px;
      background: rgba(16, 185, 129, 0.16);
      border: 1px solid rgba(16, 185, 129, 0.45);
      color: #34d399;
      border-radius: 10px;
      font-size: 0.84rem;
      font-weight: 600;
    }
    .error-banner {
      padding: 12px 16px;
      background: rgba(239, 68, 68, 0.16);
      border: 1px solid rgba(239, 68, 68, 0.45);
      color: #fca5a5;
      border-radius: 10px;
      font-size: 0.85rem;
    }

    .search-bar-card {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
      padding: 14px 18px;
      background: rgba(30, 41, 59, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
    }
    .search-input-group {
      display: flex;
      align-items: center;
      flex: 1;
      min-width: 280px;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 9px;
      padding: 0 12px;
    }
    .search-icon {
      font-size: 1rem;
      margin-right: 8px;
    }
    .shop-input {
      width: 100%;
      padding: 10px 4px;
      background: transparent;
      border: none;
      color: #f8fafc;
      font-size: 0.88rem;
      outline: none;
    }
    .search-controls {
      display: flex;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
    }
    .limit-label {
      font-size: 0.8rem;
      color: #cbd5e1;
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .limit-select, .filter-select {
      background: #0f172a;
      color: #e2e8f0;
      border: 1px solid rgba(255, 255, 255, 0.14);
      border-radius: 7px;
      padding: 7px 10px;
      font-size: 0.8rem;
    }
    .btn-primary-analyze {
      padding: 10px 18px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      color: #fff;
      font-weight: 700;
      font-size: 0.84rem;
      border-radius: 8px;
      cursor: pointer;
    }
    .btn-primary-analyze[disabled] {
      opacity: 0.6;
      cursor: wait;
    }
    .btn-secondary-action {
      padding: 9px 13px;
      background: rgba(51, 65, 85, 0.55);
      border: 1px solid rgba(255, 255, 255, 0.12);
      color: #e2e8f0;
      border-radius: 8px;
      font-size: 0.8rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-secondary-action:hover {
      background: rgba(71, 85, 105, 0.75);
    }

    /* Watchlist strip */
    .watchlist-strip {
      background: rgba(30, 41, 59, 0.35);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 12px;
      padding: 12px 16px;
    }
    .watchlist-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 10px;
      flex-wrap: wrap;
      gap: 8px;
    }
    .watchlist-title {
      font-size: 0.8rem;
      font-weight: 700;
      text-transform: uppercase;
      color: #cbd5e1;
      letter-spacing: 0.04em;
    }
    .watchlist-hint {
      font-size: 0.75rem;
      color: #64748b;
    }
    .watchlist-cards {
      display: flex;
      gap: 10px;
      overflow-x: auto;
      padding-bottom: 4px;
    }
    .watch-card {
      min-width: 235px;
      background: rgba(15, 23, 42, 0.75);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 10px;
      padding: 10px 12px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .watch-card.active {
      border-color: #818cf8;
      background: rgba(99, 102, 241, 0.15);
    }
    .watch-card-top {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 6px;
    }
    .watch-shop-name {
      font-weight: 700;
      font-size: 0.88rem;
      color: #f8fafc;
    }
    .watch-badges {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .btn-remove-watch {
      background: transparent;
      border: none;
      color: #64748b;
      cursor: pointer;
      font-size: 0.78rem;
    }
    .btn-remove-watch:hover { color: #f87171; }
    .watch-metrics {
      display: flex;
      flex-direction: column;
      gap: 3px;
      font-size: 0.75rem;
    }
    .w-metric {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .w-label { color: #94a3b8; }
    .w-val { font-weight: 700; color: #e2e8f0; }
    .w-val.revenue { color: #34d399; }
    .delta-pill {
      font-size: 0.68rem;
      padding: 1px 5px;
      border-radius: 4px;
      font-weight: 700;
    }
    .delta-pill.up { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .delta-pill.down { background: rgba(239, 68, 68, 0.2); color: #f87171; }

    /* Empty state */
    .empty-hero-card {
      text-align: center;
      padding: 44px 24px;
      background: rgba(30, 41, 59, 0.3);
      border: 1px dashed rgba(255, 255, 255, 0.12);
      border-radius: 14px;
    }
    .empty-hero-icon { font-size: 2.6rem; margin-bottom: 8px; }
    .empty-hero-card h3 { margin: 0 0 8px 0; color: #f8fafc; }
    .empty-hero-card p { max-width: 640px; margin: 0 auto; color: #94a3b8; font-size: 0.88rem; line-height: 1.55; }

    /* 7 KPI Grid */
    .kpi-grid-7 {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(165px, 1fr));
      gap: 12px;
    }
    .kpi-card {
      background: rgba(30, 41, 59, 0.48);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 11px;
      padding: 13px 15px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .kpi-card.highlight-revenue {
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.16), rgba(30, 41, 59, 0.55));
      border-color: rgba(16, 185, 129, 0.35);
    }
    .kpi-title {
      font-size: 0.7rem;
      font-weight: 700;
      color: #94a3b8;
      letter-spacing: 0.05em;
    }
    .kpi-value {
      font-size: 1.35rem;
      font-weight: 800;
      color: #f8fafc;
    }
    .kpi-value.star-val { color: #fbbf24; }
    .kpi-value.velocity-val { color: #38bdf8; }
    .kpi-value.revenue-val { color: #34d399; }
    .kpi-sub {
      font-size: 0.74rem;
      color: #94a3b8;
    }
    .strength-high { color: #34d399; }
    .strength-mid { color: #fbbf24; }
    .strength-low { color: #f87171; }

    /* Workspace split */
    .workspace-split {
      display: grid;
      grid-template-columns: 1.25fr 1fr;
      gap: 18px;
      align-items: start;
    }
    .glass-card {
      background: rgba(30, 41, 59, 0.42);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 12px;
      padding: 16px;
    }
    .panel-toolbar {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 10px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .panel-title {
      font-size: 0.88rem;
      font-weight: 700;
      color: #f1f5f9;
    }
    .toolbar-filters {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .filter-input {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.14);
      color: #f8fafc;
      padding: 7px 10px;
      border-radius: 7px;
      font-size: 0.78rem;
      min-width: 175px;
    }

    .table-scroll {
      max-height: 580px;
      overflow-y: auto;
    }
    .spy-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.79rem;
    }
    .spy-table th {
      text-align: left;
      padding: 9px 8px;
      color: #94a3b8;
      font-weight: 700;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      position: sticky;
      top: 0;
      background: #131c2e;
      z-index: 1;
    }
    .spy-table td {
      padding: 9px 8px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      vertical-align: middle;
    }
    .spy-table tr {
      cursor: pointer;
      transition: background 0.15s;
    }
    .spy-table tr:hover {
      background: rgba(255, 255, 255, 0.04);
    }
    .spy-table tr.selected-row {
      background: rgba(99, 102, 241, 0.18);
    }
    .product-cell {
      display: flex;
      align-items: center;
      gap: 9px;
      max-width: 250px;
    }
    .thumb-img {
      width: 40px;
      height: 40px;
      border-radius: 6px;
      object-fit: cover;
      flex-shrink: 0;
    }
    .prod-title {
      font-weight: 600;
      color: #f1f5f9;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .prod-meta {
      display: flex;
      gap: 8px;
      font-size: 0.7rem;
      color: #64748b;
      margin-top: 2px;
    }
    .price-cell { font-weight: 700; color: #e2e8f0; }
    .rev-cell { font-weight: 700; color: #34d399; }
    .fav-num, .view-num { display: block; font-size: 0.72rem; color: #cbd5e1; }

    .velocity-badge {
      display: inline-block;
      padding: 3px 7px;
      border-radius: 5px;
      font-size: 0.7rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .vel-bestseller { background: rgba(249, 115, 22, 0.2); color: #fb923c; border: 1px solid rgba(249, 115, 22, 0.4); }
    .vel-hot { background: rgba(234, 179, 8, 0.2); color: #facc15; border: 1px solid rgba(234, 179, 8, 0.4); }
    .vel-steady { background: rgba(56, 189, 248, 0.15); color: #38bdf8; }
    .vel-normal { background: rgba(148, 163, 184, 0.12); color: #94a3b8; }

    .lqs-badge, .lqs-mini-badge {
      display: inline-block;
      padding: 2px 7px;
      border-radius: 5px;
      font-size: 0.72rem;
      font-weight: 700;
    }
    .lqs-a { background: rgba(16, 185, 129, 0.2); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }
    .lqs-b { background: rgba(56, 189, 248, 0.2); color: #38bdf8; border: 1px solid rgba(56, 189, 248, 0.4); }
    .lqs-c { background: rgba(245, 158, 11, 0.2); color: #fbbf24; border: 1px solid rgba(245, 158, 11, 0.4); }
    .lqs-d { background: rgba(239, 68, 68, 0.2); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.4); }

    .btn-clone-mini {
      padding: 5px 9px;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.45);
      color: #a5b4fc;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-clone-mini:hover { background: rgba(99, 102, 241, 0.4); color: #fff; }

    /* Right panel */
    .intelligence-panel {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .sel-header {
      display: flex;
      gap: 14px;
      margin-bottom: 12px;
    }
    .sel-thumb {
      width: 76px;
      height: 76px;
      border-radius: 10px;
      object-fit: cover;
    }
    .sel-badges {
      display: flex;
      gap: 8px;
      margin-bottom: 6px;
      flex-wrap: wrap;
    }
    .sel-title {
      font-size: 0.92rem;
      font-weight: 700;
      margin: 0 0 6px 0;
      color: #f8fafc;
      line-height: 1.35;
    }
    .sel-price-row {
      font-size: 0.82rem;
      color: #cbd5e1;
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }
    .sel-price {
      font-size: 1.1rem;
      font-weight: 800;
      color: #34d399;
    }
    .sel-actions-bar {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-bottom: 12px;
    }
    .btn-clone-primary {
      padding: 8px 14px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      color: #fff;
      border-radius: 7px;
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-copy-tags {
      padding: 8px 12px;
      background: rgba(16, 185, 129, 0.16);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 7px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
    }
    .btn-open-etsy {
      padding: 8px 12px;
      background: rgba(51, 65, 85, 0.5);
      border: 1px solid rgba(255, 255, 255, 0.14);
      color: #e2e8f0;
      border-radius: 7px;
      font-size: 0.78rem;
      cursor: pointer;
    }
    .sub-box-title {
      font-size: 0.75rem;
      font-weight: 700;
      color: #94a3b8;
      display: block;
      margin-bottom: 8px;
    }
    .tags-cloud {
      display: flex;
      flex-wrap: wrap;
      gap: 6px;
    }
    .tag-chip, .kw-chip {
      padding: 4px 9px;
      background: rgba(99, 102, 241, 0.14);
      border: 1px solid rgba(99, 102, 241, 0.32);
      border-radius: 6px;
      font-size: 0.74rem;
      color: #e2e8f0;
      cursor: pointer;
      transition: all 0.15s;
    }
    .tag-chip:hover, .kw-chip:hover {
      background: rgba(99, 102, 241, 0.32);
      color: #fff;
    }

    /* Subtabs */
    .subtab-nav {
      display: flex;
      gap: 6px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 10px;
      margin-bottom: 12px;
      flex-wrap: wrap;
    }
    .subtab-btn {
      padding: 7px 11px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.07);
      color: #94a3b8;
      border-radius: 7px;
      font-size: 0.76rem;
      font-weight: 600;
      cursor: pointer;
    }
    .subtab-btn.active {
      background: rgba(99, 102, 241, 0.22);
      border-color: #818cf8;
      color: #f8fafc;
    }
    .freq-columns {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 14px;
    }
    .freq-col-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .freq-col-header h4 {
      margin: 0;
      font-size: 0.78rem;
      color: #cbd5e1;
    }
    .btn-mini-copy, .btn-mini-clone {
      padding: 4px 8px;
      font-size: 0.7rem;
      border-radius: 5px;
      cursor: pointer;
      font-weight: 600;
      background: rgba(16, 185, 129, 0.16);
      border: 1px solid rgba(16, 185, 129, 0.35);
      color: #34d399;
    }
    .btn-mini-clone {
      background: rgba(99, 102, 241, 0.2);
      border-color: rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
    }
    .freq-list {
      max-height: 290px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .freq-row {
      display: flex;
      align-items: center;
      gap: 8px;
      font-size: 0.74rem;
      padding: 4px 6px;
      border-radius: 5px;
      cursor: pointer;
    }
    .freq-row:hover { background: rgba(255, 255, 255, 0.04); }
    .freq-term {
      min-width: 110px;
      font-weight: 600;
      color: #e2e8f0;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .freq-bar-wrap {
      flex: 1;
      height: 6px;
      background: rgba(255, 255, 255, 0.07);
      border-radius: 3px;
      overflow: hidden;
    }
    .freq-bar {
      height: 100%;
      background: linear-gradient(90deg, #6366f1, #a855f7);
    }
    .freq-bar.word-bar {
      background: linear-gradient(90deg, #10b981, #38bdf8);
    }
    .freq-count {
      font-size: 0.7rem;
      color: #94a3b8;
      min-width: 72px;
      text-align: right;
    }

    /* AI Gap Insight */
    .ai-pill {
      display: inline-block;
      padding: 4px 10px;
      background: rgba(168, 85, 247, 0.18);
      border: 1px solid rgba(168, 85, 247, 0.4);
      color: #d8b4fe;
      border-radius: 6px;
      font-size: 0.74rem;
      font-weight: 700;
      margin-bottom: 10px;
    }
    .ai-insight-text {
      font-size: 0.84rem;
      line-height: 1.6;
      color: #e2e8f0;
      margin: 0 0 14px 0;
    }
    .gap-metrics-grid {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 10px;
    }
    .gap-metric-item {
      background: rgba(15, 23, 42, 0.65);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 8px;
      padding: 10px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .gm-label { font-size: 0.72rem; color: #94a3b8; font-weight: 600; }
    .gm-val { font-size: 0.98rem; font-weight: 800; color: #34d399; }
    .gm-hint { font-size: 0.71rem; color: #64748b; line-height: 1.4; }

    /* LQS Audit */
    .lqs-audit-header {
      font-size: 0.82rem;
      margin-bottom: 10px;
      color: #cbd5e1;
    }
    .lqs-checks-list {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .lqs-check-item {
      display: flex;
      gap: 10px;
      padding: 8px 10px;
      background: rgba(15, 23, 42, 0.6);
      border-radius: 8px;
      border: 1px solid rgba(16, 185, 129, 0.22);
    }
    .lqs-check-item.failed {
      border-color: rgba(245, 158, 11, 0.35);
    }
    .chk-label { font-size: 0.78rem; font-weight: 700; color: #f1f5f9; }
    .chk-detail { font-size: 0.73rem; color: #94a3b8; }

    /* Seasonal Trends Tab */
    .seasonal-heading { margin: 0 0 6px 0; font-size: 1.1rem; color: #f8fafc; }
    .seasonal-sub { margin: 0; font-size: 0.84rem; color: #94a3b8; line-height: 1.5; }
    .seasonal-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(360px, 1fr));
      gap: 16px;
    }
    .seasonal-card {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .sc-top {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      gap: 10px;
    }
    .sc-title-group {
      display: flex;
      gap: 10px;
      align-items: center;
    }
    .sc-icon { font-size: 1.7rem; }
    .sc-name { margin: 0; font-size: 0.98rem; font-weight: 700; color: #f8fafc; }
    .sc-peak { font-size: 0.74rem; color: #94a3b8; }
    .sc-status-badge {
      padding: 4px 9px;
      border-radius: 6px;
      border: 1px solid;
      font-size: 0.7rem;
      font-weight: 700;
      white-space: nowrap;
    }
    .sc-metrics-row {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .sc-pill {
      padding: 4px 9px;
      background: rgba(15, 23, 42, 0.7);
      border-radius: 6px;
      font-size: 0.74rem;
      color: #cbd5e1;
    }
    .sc-advice {
      margin: 0;
      font-size: 0.8rem;
      color: #e2e8f0;
      line-height: 1.5;
      background: rgba(15, 23, 42, 0.45);
      padding: 9px 11px;
      border-radius: 8px;
    }
    .sc-section {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .sc-sec-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 6px;
    }
    .sc-sec-actions {
      display: flex;
      gap: 6px;
    }
    .sc-sec-label {
      font-size: 0.74rem;
      font-weight: 700;
      color: #94a3b8;
    }

    @media (max-width: 1180px) {
      .workspace-split { grid-template-columns: 1fr; }
      .freq-columns { grid-template-columns: 1fr; }
    }
  `]
})
export class CompetitorSpyComponent implements OnInit {
  activeMainTab: 'competitor' | 'seasonal' = 'competitor';
  activeIntelTab: 'tags' | 'gap' | 'lqs' = 'tags';

  shopQueryInput = '';
  sampleLimit = 50;
  isAnalyzing = false;
  errorMessage = '';
  toastMessage = '';

  analysisResult: CompetitorShopAnalysisDto | null = null;
  listings: CompetitorListingDto[] = [];
  filteredListings: CompetitorListingDto[] = [];
  selectedListing: CompetitorListingDto | null = null;

  listingSearchText = '';
  priceBandFilter: 'all' | 'under30' | '30to75' | 'over75' = 'all';
  listingSortBy: 'revenue' | 'sales' | 'favorites' | 'views' | 'lqs' | 'price_desc' | 'price_asc' = 'revenue';

  watchedShops: WatchedCompetitorShop[] = [];
  seasonalTrends: SeasonalTrendItemDto[] = [];

  constructor(
    public etsyApi: EtsyApiService,
    private aiSettings: AiSettingsService,
    private route: ActivatedRoute,
    private router: Router
  ) {}

  ngOnInit(): void {
    this.loadWatchedShops();
    this.loadSeasonalTrends();

    this.route.queryParams.subscribe(params => {
      const targetShop = (params['shop'] || '').trim();
      if (targetShop) {
        this.shopQueryInput = targetShop;
        this.analyzeShop(targetShop);
      } else if (this.watchedShops.length > 0 && !this.analysisResult) {
        this.shopQueryInput = this.watchedShops[0].shopName;
      }
    });
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate() || 38;
  }

  analyzeShop(overrideQuery?: string): void {
    const query = (overrideQuery ?? this.shopQueryInput ?? '').trim();
    if (!query) {
      this.showToast('⚠️ Lütfen analiz edilecek Etsy mağaza adını veya mağaza linkini girin.');
      return;
    }

    this.shopQueryInput = query;
    this.isAnalyzing = true;
    this.errorMessage = '';

    this.etsyApi.analyzeCompetitorShop(query, this.sampleLimit).subscribe({
      next: (res) => {
        this.isAnalyzing = false;
        this.analysisResult = res;

        if (!res.found) {
          this.errorMessage = res.errorMessage || `"${query}" mağazası bulunamadı.`;
          this.listings = [];
          this.filteredListings = [];
          this.selectedListing = null;
          return;
        }

        this.listings = res.listings || [];
        this.applyListingFilters();
        this.selectedListing = this.filteredListings.length > 0 ? this.filteredListings[0] : null;

        // Eğer takip listesinde varsa otomatik snapshot güncelle
        const existingIdx = this.watchedShops.findIndex(
          w => w.shopName.toLowerCase() === res.shop.shopName.toLowerCase()
        );
        if (existingIdx >= 0) {
          this.upsertWatchlistFromAnalysis(res, false);
        }
      },
      error: (err) => {
        this.isAnalyzing = false;
        this.errorMessage = err?.error?.error || err?.message || 'Etsy v3 rakip analizi sırasında bağlantı hatası oluştu.';
      }
    });
  }

  applyListingFilters(): void {
    let list = [...this.listings];

    const q = (this.listingSearchText || '').trim().toLowerCase();
    if (q) {
      list = list.filter(item =>
        item.title.toLowerCase().includes(q) ||
        (item.tags || []).some(t => t.toLowerCase().includes(q))
      );
    }

    if (this.priceBandFilter === 'under30') {
      list = list.filter(i => i.priceUsd < 30);
    } else if (this.priceBandFilter === '30to75') {
      list = list.filter(i => i.priceUsd >= 30 && i.priceUsd <= 75);
    } else if (this.priceBandFilter === 'over75') {
      list = list.filter(i => i.priceUsd > 75);
    }

    list.sort((a, b) => {
      switch (this.listingSortBy) {
        case 'sales': return b.estimatedMonthlySales - a.estimatedMonthlySales;
        case 'favorites': return b.favorites - a.favorites;
        case 'views': return b.views - a.views;
        case 'lqs': return b.lqsScore - a.lqsScore;
        case 'price_desc': return b.priceUsd - a.priceUsd;
        case 'price_asc': return a.priceUsd - b.priceUsd;
        case 'revenue':
        default:
          return b.estimatedMonthlyRevenueUsd - a.estimatedMonthlyRevenueUsd;
      }
    });

    this.filteredListings = list;
    if (this.selectedListing && !this.filteredListings.some(x => x.listingId === this.selectedListing?.listingId)) {
      this.selectedListing = this.filteredListings[0] || null;
    }
  }

  selectListing(item: CompetitorListingDto): void {
    this.selectedListing = item;
  }

  saveCurrentShopToWatchlist(): void {
    if (!this.analysisResult?.found) return;
    this.upsertWatchlistFromAnalysis(this.analysisResult, true);
  }

  private upsertWatchlistFromAnalysis(res: CompetitorShopAnalysisDto, notify: boolean): void {
    const shop = res.shop;
    const topItem = (res.listings && res.listings.length > 0) ? res.listings[0] : null;
    const idx = this.watchedShops.findIndex(w => w.shopName.toLowerCase() === shop.shopName.toLowerCase());
    const prev = idx >= 0 ? this.watchedShops[idx] : undefined;

    const updated: WatchedCompetitorShop = {
      shopName: shop.shopName,
      shopUrl: shop.shopUrl,
      totalSalesCount: shop.totalSalesCount,
      prevTotalSalesCount: prev ? prev.totalSalesCount : shop.totalSalesCount,
      estimatedMonthlySales: shop.estimatedMonthlySales,
      estimatedMonthlyRevenueUsd: shop.estimatedMonthlyRevenueUsd,
      prevEstimatedMonthlyRevenueUsd: prev ? prev.estimatedMonthlyRevenueUsd : shop.estimatedMonthlyRevenueUsd,
      activeListingsCount: shop.activeListingsCount,
      prevActiveListingsCount: prev ? prev.activeListingsCount : shop.activeListingsCount,
      averagePriceUsd: shop.averagePriceUsd,
      competitorStrengthScore: shop.competitorStrengthScore,
      averageLqsGrade: shop.averageLqsGrade,
      topItemTitle: topItem?.title || '',
      topItemPrice: topItem?.priceUsd || 0,
      topItemTags: topItem?.tags || [],
      lastAnalyzedAt: new Date().toISOString()
    };

    if (idx >= 0) {
      this.watchedShops[idx] = updated;
    } else {
      this.watchedShops.unshift(updated);
    }

    this.saveWatchedShops();

    // Sunucu SQLite takip tablosuna da senkronize et
    this.etsyApi.saveTrackingCapture({
      entityType: 2, // Shop
      externalKey: shop.shopName.toLowerCase(),
      displayName: shop.shopName,
      url: shop.shopUrl,
      price: shop.averagePriceUsd,
      currencyCode: 'USD',
      favorites: shop.totalSalesCount,
      opportunityScore: shop.competitorStrengthScore,
      resultCount: shop.activeListingsCount,
      sampleSize: res.listings?.length || 0
    }).subscribe({ error: () => {} });

    if (notify) {
      this.showToast(`📌 "${shop.shopName}" takip listesine kaydedildi ve canlı snapshot güncellendi!`);
    }
  }

  selectWatchedShop(w: WatchedCompetitorShop): void {
    this.shopQueryInput = w.shopName;
    this.analyzeShop(w.shopName);
  }

  removeWatchedShop(shopName: string, event: MouseEvent): void {
    event.stopPropagation();
    this.watchedShops = this.watchedShops.filter(s => s.shopName.toLowerCase() !== shopName.toLowerCase());
    this.saveWatchedShops();
    this.showToast(`🗑 "${shopName}" takip listesinden çıkarıldı.`);
  }

  private loadWatchedShops(): void {
    try {
      const raw = localStorage.getItem(COMPETITOR_STORAGE_KEY) || localStorage.getItem('etsy_competitor_shops_v1');
      if (raw) {
        const parsed = JSON.parse(raw);
        if (Array.isArray(parsed)) {
          this.watchedShops = parsed.map((p: any) => ({
            shopName: p.shopName || '',
            shopUrl: p.shopUrl || `https://www.etsy.com/shop/${p.shopName}`,
            totalSalesCount: p.totalSalesCount || p.sales30d || 0,
            prevTotalSalesCount: p.prevTotalSalesCount,
            estimatedMonthlySales: p.estimatedMonthlySales || p.sales30d || 0,
            estimatedMonthlyRevenueUsd: p.estimatedMonthlyRevenueUsd || p.revenueEstUsd || 0,
            prevEstimatedMonthlyRevenueUsd: p.prevEstimatedMonthlyRevenueUsd,
            activeListingsCount: p.activeListingsCount || p.activeListings || 0,
            prevActiveListingsCount: p.prevActiveListingsCount,
            averagePriceUsd: p.averagePriceUsd || p.topItemPrice || 0,
            competitorStrengthScore: p.competitorStrengthScore || 50,
            averageLqsGrade: p.averageLqsGrade || 'B',
            topItemTitle: p.topItemTitle || '',
            topItemPrice: p.topItemPrice || 0,
            topItemTags: p.topItemTags || [],
            lastAnalyzedAt: p.lastAnalyzedAt || new Date().toISOString()
          })).filter(x => x.shopName);
        }
      }
    } catch {
      this.watchedShops = [];
    }
  }

  private saveWatchedShops(): void {
    try {
      localStorage.setItem(COMPETITOR_STORAGE_KEY, JSON.stringify(this.watchedShops));
    } catch {}
  }

  getDeltaDiff(curr?: number, prev?: number): number {
    if (curr === undefined || prev === undefined) return 0;
    return Math.round((curr - prev) * 100) / 100;
  }

  formatDelta(curr?: number, prev?: number, isCurrency: boolean = false): string {
    const diff = this.getDeltaDiff(curr, prev);
    if (diff === 0) return '';
    const prefix = diff > 0 ? '▲ +' : '▼ -';
    const absVal = Math.abs(Math.round(diff)).toLocaleString('tr-TR');
    return isCurrency ? `${prefix}$${absVal}` : `${prefix}${absVal}`;
  }

  cloneListingToFastCreator(item: CompetitorListingDto): void {
    const cloned: ClonedMarketListing = {
      id: item.listingId,
      title: item.title,
      priceUsd: item.priceUsd,
      tags: [...(item.tags || [])],
      description: item.description || '',
      imageUrl: item.imageUrl || '',
      shopName: this.analysisResult?.shop?.shopName || '',
      seoScore: item.lqsScore,
      marketScore: item.lqsScore
    };
    this.aiSettings.setClonedListing(cloned);
    this.showToast(`🚀 "${item.title.slice(0, 38)}..." Hızlı İlan Oluşturucuya aktarıldı!`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 350);
  }

  copyTags(tags: string[]): void {
    if (!tags || tags.length === 0) return;
    navigator.clipboard.writeText(tags.join(', '));
    this.showToast(`📋 ${tags.length} etiket panoya kopyalandı!`);
  }

  copyTopShopTags(): void {
    if (!this.analysisResult?.topTags) return;
    const top13 = this.analysisResult.topTags.slice(0, 13).map(t => t.term);
    this.copyTags(top13);
  }

  navigateToMarketResearch(keyword: string): void {
    if (!keyword) return;
    this.router.navigate(['/research/market'], { queryParams: { keyword } });
  }

  openExternalUrl(url: string): void {
    if (!url) return;
    window.open(url, '_blank', 'noopener');
  }

  exportCsv(): void {
    if (!this.analysisResult?.found || this.listings.length === 0) return;
    const shopName = this.analysisResult.shop.shopName;
    const headers = ['Sira', 'ListingId', 'Baslik', 'FiyatUSD', 'TahminiAylikSatis', 'TahminiAylikCiroUSD', 'Favori', 'Goruntulenme', 'LQSNotu', 'LQSSkor', 'EtiketSayisi', 'Etiketler'];
    const rows = this.listings.map(l => [
      l.rank,
      l.listingId,
      `"${(l.title || '').replace(/"/g, '""')}"`,
      l.priceUsd.toFixed(2),
      l.estimatedMonthlySales,
      l.estimatedMonthlyRevenueUsd.toFixed(2),
      l.favorites,
      l.views,
      l.lqsGrade,
      l.lqsScore,
      l.tagCount,
      `"${(l.tags || []).join(', ').replace(/"/g, '""')}"`
    ]);

    const csvContent = '\uFEFF' + [headers.join(';'), ...rows.map(r => r.join(';'))].join('\r\n');
    const blob = new Blob([csvContent], { type: 'text/csv;charset=utf-8;' });
    const link = document.createElement('a');
    link.href = URL.createObjectURL(blob);
    link.download = `rakip_analizi_${shopName}_${new Date().toISOString().slice(0, 10)}.csv`;
    link.click();
    this.showToast(`📥 "${shopName}" ürün portföyü CSV olarak indirildi.`);
  }

  getWeakLqsCount(): number {
    return this.listings.filter(l => l.lqsGrade === 'C' || l.lqsGrade === 'D').length;
  }

  getListingLqsAudit(item: CompetitorListingDto): LqsAuditCheck[] {
    const titleLen = (item.title || '').length;
    const checks: LqsAuditCheck[] = [];

    checks.push({
      icon: titleLen >= 70 && titleLen <= 140 ? '✅' : (titleLen >= 40 ? '⚠️' : '❌'),
      label: `Başlık Uzunluğu (${titleLen}/140 karakter)`,
      detail: titleLen >= 70
        ? 'Güçlü uzun kuyruklu (long-tail) anahtar kelime kapsaması.'
        : 'Başlık kısa bırakılmış; siz 90-130 karakter arası 3 anahtar kelime öbeği kullanarak bu ürünü geçebilirsiniz.',
      passed: titleLen >= 70
    });

    checks.push({
      icon: item.tagCount >= 13 ? '✅' : (item.tagCount >= 10 ? '⚠️' : '❌'),
      label: `Arama Etiketi Kullanımı (${item.tagCount}/13 etiket)`,
      detail: item.tagCount >= 13
        ? 'Rakip 13 etiket hakkının tamamını kullanıyor.'
        : `Rakip ${13 - item.tagCount} etiket hakkını boş bırakmış! 13 etiketi eksiksiz doldurarak organik görünürlük farkı yaratın.`,
      passed: item.tagCount >= 13
    });

    checks.push({
      icon: item.imageCount >= 8 ? '✅' : (item.imageCount >= 5 ? '⚠️' : '❌'),
      label: `Görsel Galerisi (${item.imageCount}/10 fotoğraf)`,
      detail: item.imageCount >= 8
        ? 'Görsel çeşitliliği yüksek.'
        : 'Görsel sayısı yetersiz; ölçü tablosu, yaşam alanı (lifestyle) ve yakın çekim fotoğraflarla dönüşüm oranını artırın.',
      passed: item.imageCount >= 8
    });

    checks.push({
      icon: item.descriptionLength >= 300 ? '✅' : (item.descriptionLength >= 150 ? '⚠️' : '❌'),
      label: `Ürün Açıklaması (${item.descriptionLength} karakter)`,
      detail: item.descriptionLength >= 300
        ? 'Açıklama metni Google ve Etsy SEO indekslemesi için yeterli uzunlukta.'
        : 'Açıklama çok kısa; malzeme, boyut, kargo ve hediye paketi detaylarını içeren zengin bir açıklama yazın.',
      passed: item.descriptionLength >= 300
    });

    return checks;
  }

  switchToSeasonalTab(): void {
    this.activeMainTab = 'seasonal';
    if (this.seasonalTrends.length === 0) {
      this.loadSeasonalTrends();
    }
  }

  private loadSeasonalTrends(): void {
    this.etsyApi.getSeasonalTrends().subscribe({
      next: (list) => {
        this.seasonalTrends = list || [];
      },
      error: () => {}
    });
  }

  sendSeasonalTrendToFastCreator(tr: SeasonalTrendItemDto): void {
    const cloned: ClonedMarketListing = {
      id: 0,
      title: `${tr.eventName} - ${(tr.highDemandKeywords || []).slice(0, 3).join(' | ')}`,
      priceUsd: 39.90,
      tags: [...(tr.winningTags || []).slice(0, 13)],
      description: tr.actionAdvice,
      imageUrl: '',
      shopName: tr.eventName,
      seoScore: 95,
      marketScore: 95
    };
    this.aiSettings.setClonedListing(cloned);
    this.showToast(`🚀 "${tr.eventName}" sezon etiketleri Hızlı İlan Oluşturucuya aktarıldı!`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 350);
  }

  getVelocityBadgeClass(badge: string): string {
    if (!badge) return 'vel-normal';
    if (badge.includes('Best Seller')) return 'vel-bestseller';
    if (badge.includes('Hot')) return 'vel-hot';
    if (badge.includes('İstikrarlı')) return 'vel-steady';
    return 'vel-normal';
  }

  getLqsClass(grade: string): string {
    if (!grade) return 'lqs-b';
    if (grade.startsWith('A')) return 'lqs-a';
    if (grade.startsWith('B')) return 'lqs-b';
    if (grade.startsWith('C')) return 'lqs-c';
    return 'lqs-d';
  }

  getStrengthClass(score: number): string {
    if (score >= 75) return 'strength-high';
    if (score >= 45) return 'strength-mid';
    return 'strength-low';
  }

  private showToast(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) {
        this.toastMessage = '';
      }
    }, 3500);
  }
}
