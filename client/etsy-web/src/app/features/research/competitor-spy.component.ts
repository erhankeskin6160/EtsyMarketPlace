import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { ActivatedRoute, Router } from '@angular/router';
import {
  EtsyApiService,
  CompetitorShopAnalysisDto,
  CompetitorListingDto,
  SeasonalTrendItemDto,
  AiLqsImprovementCommandDto,
  AiLqsImprovementResultDto
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
            <p class="page-subtitle">Canlı Etsy v3 rakip mağaza röntgeni, ürün fotoğraf galerisi, açıklama inceleyici ve tek tıkla fotoğraflı klonlama</p>
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
              <span *ngIf="isAnalyzing">⏳ Etsy v3 & Görseller Taranıyor...</span>
            </button>

            <button
              type="button"
              class="btn-secondary-action"
              *ngIf="analysisResult?.found"
              (click)="saveCurrentShopToWatchlist()">
              📌 Takip Listesine Kaydet
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
              🏪 Mağazayı Etsy'de Aç ↗
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
            <strong>"Mağazayı Canlı Analiz Et"</strong> butonuna tıklayın. Ürünlerin tüm fotoğrafları, açıklamaları,
            EverBee aylık satış/ciro tahminleri, eRank LQS kalite notları (A+..D) ve tek tıkla fotoğraflı klonlama saniyeler içinde hazırlanır.
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

          <!-- MAIN WORKSPACE: LEFT PRODUCT TABLE + RIGHT SELECTED PRODUCT GALLERY, DESCRIPTION & INTELLIGENCE -->
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
                      <th>Görsel & Ürün</th>
                      <th>Fiyat</th>
                      <th>Tah. Satış/Ay</th>
                      <th>Tah. Ciro/Ay</th>
                      <th>Favori / Gör.</th>
                      <th>Hız Rozeti</th>
                      <th>LQS</th>
                      <th>İşlemler</th>
                    </tr>
                  </thead>
                  <tbody>
                    <tr
                      *ngFor="let item of filteredListings"
                      [class.selected-row]="selectedListing?.listingId === item.listingId"
                      (click)="selectListing(item)">
                      <td class="rank-cell">{{ item.rank }}</td>
                      <td class="product-cell">
                        <div class="thumb-box" (click)="openLightbox(item, 0, $event)" title="Büyütmek için tıklayın">
                          <img
                            *ngIf="getPrimaryImage(item)"
                            [src]="getPrimaryImage(item)"
                            alt=""
                            class="thumb-img" />
                          <div *ngIf="!getPrimaryImage(item)" class="thumb-placeholder">📷</div>
                          <span class="thumb-count-badge" *ngIf="getItemImages(item).length > 0">
                            {{ getItemImages(item).length }}
                          </span>
                        </div>
                        <div class="prod-info">
                          <div class="prod-title" [title]="item.title">
                            {{ item.title }}
                          </div>
                          <div class="prod-meta">
                            <span>🏷️ {{ item.tagCount }}/13 etiket</span>
                            <span>📷 {{ getItemImages(item).length }} görsel</span>
                            <span>📝 {{ (item.description || '').length }} krk.</span>
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
                        <div class="row-action-btns">
                          <button
                            type="button"
                            class="btn-etsy-mini"
                            title="Ürünü Etsy'de Yeni Sekmede Aç"
                            (click)="openListingOnEtsy(item)">
                            🌐 Etsy ↗
                          </button>
                          <button
                            type="button"
                            class="btn-clone-mini"
                            title="Bu ürünü tüm fotoğrafları, açıklaması ve 13 etiketiyle Hızlı İlan Oluşturucuya Klonla"
                            (click)="cloneListingToFastCreator(item)">
                            🚀 Klonla
                          </button>
                        </div>
                      </td>
                    </tr>
                  </tbody>
                </table>
              </div>
            </div>

            <!-- RIGHT: SELECTED PRODUCT SHOWCASE (GALLERY + DESCRIPTION + TAGS) + 3 INTELLIGENCE TABS -->
            <div class="intelligence-panel">
              <!-- SELECTED LISTING SHOWCASE CARD -->
              <div class="glass-card selected-product-card" *ngIf="selectedListing as sel">
                <div class="showcase-top-grid">
                  <!-- LEFT: INTERACTIVE MULTI-IMAGE GALLERY -->
                  <div class="showcase-gallery">
                    <div class="main-img-stage" (click)="openLightbox(sel, activeImageIndex, $event)">
                      <img
                        *ngIf="getActiveImageUrl(sel)"
                        [src]="getActiveImageUrl(sel)"
                        alt="Ürün Görseli"
                        class="main-stage-img" />
                      <div *ngIf="!getActiveImageUrl(sel)" class="main-stage-empty">
                        <span>📷 Görsel Bulunamadı</span>
                      </div>

                      <button
                        type="button"
                        class="stage-nav prev"
                        *ngIf="getItemImages(sel).length > 1"
                        (click)="prevImage(sel, $event)">‹</button>
                      <button
                        type="button"
                        class="stage-nav next"
                        *ngIf="getItemImages(sel).length > 1"
                        (click)="nextImage(sel, $event)">›</button>

                      <span class="stage-counter" *ngIf="getItemImages(sel).length > 0">
                        🔍 {{ activeImageIndex + 1 }} / {{ getItemImages(sel).length }} (Büyüt)
                      </span>
                    </div>

                    <!-- THUMBNAIL STRIP -->
                    <div class="thumb-strip" *ngIf="getItemImages(sel).length > 1">
                      <button
                        type="button"
                        *ngFor="let imgUrl of getItemImages(sel); let idx = index"
                        class="strip-thumb-btn"
                        [class.active]="idx === activeImageIndex"
                        (click)="setActiveImage(idx)">
                        <img [src]="imgUrl" alt="" class="strip-thumb-img" />
                      </button>
                    </div>
                  </div>

                  <!-- RIGHT: PRODUCT METADATA & PRIMARY ACTIONS -->
                  <div class="showcase-info">
                    <div class="sel-badges">
                      <span class="velocity-badge" [ngClass]="getVelocityBadgeClass(sel.salesVelocityBadge)">
                        {{ sel.salesVelocityBadge }}
                      </span>
                      <span class="lqs-badge" [ngClass]="getLqsClass(sel.lqsGrade)">
                        eRank LQS: {{ sel.lqsGrade }} ({{ sel.lqsScore }}/100)
                      </span>
                      <span class="id-pill">ID: #{{ sel.listingId }}</span>
                    </div>

                    <h3 class="sel-title" (click)="openListingOnEtsy(sel)" title="Etsy'de açmak için tıklayın">
                      {{ sel.title }} <span class="ext-arrow">↗</span>
                    </h3>

                    <div class="sel-price-row">
                      <span class="sel-price">&#36;{{ sel.priceUsd | number:'1.2-2' }}</span>
                      <span class="sel-try">(≈ ₺{{ (sel.priceUsd * usdTryRate) | number:'1.0-0' }})</span>
                      <span class="sel-est-rev">• Tah. Satış: <strong>{{ sel.estimatedMonthlySales }} ad/ay</strong></span>
                      <span class="sel-est-rev">• Tah. Ciro: <strong>&#36;{{ sel.estimatedMonthlyRevenueUsd | number:'1.0-0' }}/ay</strong></span>
                    </div>

                    <div class="sel-quick-stats">
                      <span>❤️ <strong>{{ sel.favorites | number }}</strong> Favori</span>
                      <span>👁️ <strong>{{ sel.views | number }}</strong> Görüntülenme</span>
                      <span>📦 <strong>{{ sel.quantity }}</strong> Stok</span>
                      <span>📷 <strong>{{ getItemImages(sel).length }}</strong> Fotoğraf</span>
                    </div>

                    <div class="sel-actions-bar">
                      <button
                        type="button"
                        class="btn-clone-primary"
                        (click)="cloneListingToFastCreator(sel)">
                        🚀 {{ getItemImages(sel).length }} Fotoğrafla İlan Oluşturucuya Klonla
                      </button>

                      <button
                        type="button"
                        class="btn-ai-lqs-glow"
                        [disabled]="isImprovingLqs"
                        (click)="improveSelectedListingLqs(sel)">
                        <span *ngIf="!isImprovingLqs">✨ AI ile 100/100 LQS Yap</span>
                        <span *ngIf="isImprovingLqs">⏳ LQS İyileştiriliyor...</span>
                      </button>

                      <button
                        type="button"
                        class="btn-open-etsy-prominent"
                        (click)="openListingOnEtsy(sel)">
                        🌐 Etsy'de Ürüne Git ↗
                      </button>

                      <button
                        type="button"
                        class="btn-copy-tags"
                        [disabled]="!sel.tags || sel.tags.length === 0"
                        (click)="copyTags(sel.tags)">
                        📋 {{ sel.tags.length }} Etiketi Kopyala
                      </button>
                    </div>
                  </div>
                </div>

                <!-- PRODUCT INSPECTOR TABS: 13 TAGS vs FULL DESCRIPTION vs AI DOCTOR -->
                <div class="inspector-tabs-wrap">
                  <div class="inspector-tab-header">
                    <button
                      type="button"
                      class="insp-tab-btn"
                      [class.active]="activeProductDetailTab === 'description'"
                      (click)="activeProductDetailTab = 'description'">
                      📝 Ürün Açıklaması ({{ (sel.description || '').length }} karakter)
                    </button>
                    <button
                      type="button"
                      class="insp-tab-btn"
                      [class.active]="activeProductDetailTab === 'tags'"
                      (click)="activeProductDetailTab = 'tags'">
                      🏷️ Aktif 13 Arama Etiketi ({{ sel.tags.length }}/13) & Materyaller
                    </button>
                    <button
                      type="button"
                      class="insp-tab-btn tab-ai-doctor"
                      [class.active]="activeProductDetailTab === 'ai-doctor'"
                      (click)="activeProductDetailTab = 'ai-doctor'">
                      ✨ 100/100 LQS Reçetesi & Varyasyonlar
                    </button>

                    <button
                      type="button"
                      class="btn-mini-copy ml-auto"
                      *ngIf="activeProductDetailTab === 'description' && sel.description"
                      (click)="copyDescription(sel.description)">
                      📋 Açıklamayı Kopyala
                    </button>
                  </div>

                  <!-- TAB: PRODUCT DESCRIPTION -->
                  <div class="inspector-tab-content" *ngIf="activeProductDetailTab === 'description'">
                    <div class="description-reader" *ngIf="sel.description; else noDescTpl">
                      {{ sel.description }}
                    </div>
                    <ng-template #noDescTpl>
                      <div class="muted-text">Bu ürün için açıklama metni bulunamadı.</div>
                    </ng-template>

                    <div class="materials-row" *ngIf="sel.materials && sel.materials.length > 0">
                      <span class="mat-label">🧵 Materyaller:</span>
                      <span *ngFor="let m of sel.materials" class="mat-chip">{{ m }}</span>
                    </div>
                  </div>

                  <!-- TAB: 13 SEARCH TAGS -->
                  <div class="inspector-tab-content" *ngIf="activeProductDetailTab === 'tags'">
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

                    <div class="materials-row" *ngIf="sel.materials && sel.materials.length > 0">
                      <span class="mat-label">🧵 Materyaller:</span>
                      <span *ngFor="let m of sel.materials" class="mat-chip">{{ m }}</span>
                    </div>
                  </div>

                  <!-- TAB: 100/100 LQS AI DOCTOR -->
                  <div class="inspector-tab-content ai-doctor-content" *ngIf="activeProductDetailTab === 'ai-doctor'">
                    <div *ngIf="isImprovingLqs" class="ai-loading-box">
                      <div class="spinner-ai"></div>
                      <p>✨ <strong>{{ activeAiModelBadge }}</strong> ürünün SEO zafiyetlerini inceliyor, 13 altın arama etiketini, 140 karakterlik viral başlığı ve varyasyon baremlerini 100/100 seviyesine optimize ediyor...</p>
                    </div>

                    <div *ngIf="!isImprovingLqs && !lqsImprovementResult" class="ai-empty-prompt">
                      <div class="empty-prompt-card">
                        <h4>🎯 Mevcut eRank LQS Kalite Skoru: <span class="badge-lqs" [class.badge-a]="sel.lqsScore >= 80" [class.badge-b]="sel.lqsScore >= 68 && sel.lqsScore < 80" [class.badge-c]="sel.lqsScore < 68">{{ sel.lqsScore }}/100 ({{ sel.lqsGrade }})</span></h4>
                        <p class="muted-text">Bu üründen ilham alarak organik sıralamada öne geçmek için yapay zeka ile 13 altın etiket, 140 karakterlik viral başlık ve akıllı varyasyon baremleri oluşturun.</p>
                        <button type="button" class="btn-ai-lqs-glow large-cta" (click)="improveSelectedListingLqs(sel)">
                          ✨ {{ activeAiModelBadge }} ile 100/100 LQS Reçetesini Başlat
                        </button>
                      </div>
                    </div>

                    <div *ngIf="!isImprovingLqs && lqsImprovementResult as opt" class="ai-result-view">
                      <div class="ai-result-header">
                        <div class="ai-header-left">
                          <span class="badge-target-lqs">🏆 Hedef LQS: 100/100 (A+)</span>
                          <span class="badge-provider">🤖 {{ opt.provider }}</span>
                        </div>
                        <button type="button" class="btn-clone-optimized-hero" (click)="cloneListingWithOptimization(sel, opt)">
                          ✨ 100/100 AI Optimize Haliyle Klonla
                        </button>
                      </div>

                      <div class="doctor-summary-box">
                        <strong>🩺 LQS Doktoru Denetim Notu:</strong>
                        <p>{{ opt.lqsDoctorAuditSummary }}</p>
                        <ul class="highlights-list" *ngIf="opt.improvementHighlights && opt.improvementHighlights.length > 0">
                          <li *ngFor="let h of opt.improvementHighlights">✅ {{ h }}</li>
                        </ul>
                      </div>

                      <div class="opt-field-block">
                        <div class="opt-field-label">
                          <span>📝 100/100 Optimize Başlık ({{ opt.optimizedTitle.length }} Karakter):</span>
                          <button type="button" class="btn-mini-copy" (click)="copyText(opt.optimizedTitle, 'Başlık')">📋 Kopyala</button>
                        </div>
                        <div class="opt-field-box">{{ opt.optimizedTitle }}</div>
                      </div>

                      <div class="opt-field-block">
                        <div class="opt-field-label">
                          <span>🏷️ 13 Altın Long-Tail Arama Etiketi ({{ opt.optimizedTags.length }}/13):</span>
                          <button type="button" class="btn-mini-copy" (click)="copyTags(opt.optimizedTags)">📋 Tümünü Kopyala</button>
                        </div>
                        <div class="tags-cloud">
                          <span *ngFor="let t of opt.optimizedTags" class="tag-chip opt-chip">#{{ t }}</span>
                        </div>
                      </div>

                      <div class="opt-field-block" *ngIf="opt.suggestedVariations && opt.suggestedVariations.length > 0">
                        <div class="opt-field-label">
                          <span>📦 Önerilen Akıllı Varyasyonlar (Sütun 3'e Otomatik Aktarılacak):</span>
                        </div>
                        <div class="vars-preview-row">
                          <div *ngFor="let v of opt.suggestedVariations" class="var-group-card">
                            <strong>{{ v.name }}:</strong>
                            <span>{{ v.values.join(', ') }}</span>
                          </div>
                        </div>
                      </div>

                      <div class="opt-field-block">
                        <div class="opt-field-label">
                          <span>📄 Google SEO & Dönüşüm Uyumlu Ürün Açıklaması:</span>
                          <button type="button" class="btn-mini-copy" (click)="copyDescription(opt.optimizedDescription)">📋 Açıklamayı Kopyala</button>
                        </div>
                        <div class="description-reader opt-desc-reader">{{ opt.optimizedDescription }}</div>
                      </div>

                      <div class="ai-bottom-actions">
                        <button type="button" class="btn-clone-optimized-hero" (click)="cloneListingWithOptimization(sel, opt)">
                          ✨ 100/100 AI Optimize Haliyle Klonla (Tüm Bilgilerle İlan Oluşturucuya Git)
                        </button>
                        <button type="button" class="btn-clone-secondary" (click)="cloneListingToFastCreator(sel)">
                          🚀 Orijinal (Ham) Rakip Haliyle Klonla
                        </button>
                      </div>
                    </div>
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

      <!-- ========================================================= -->
      <!-- LIGHTBOX FULL-SCREEN PRODUCT IMAGE GALLERY MODAL          -->
      <!-- ========================================================= -->
      <div class="lightbox-backdrop" *ngIf="lightboxListing as lb" (click)="closeLightbox()">
        <div class="lightbox-dialog" (click)="$event.stopPropagation()">
          <div class="lb-header">
            <div class="lb-title-wrap">
              <span class="lb-title">{{ lb.title }}</span>
              <span class="lb-sub">Görsel {{ lightboxIndex + 1 }} / {{ getItemImages(lb).length }} • &#36;{{ lb.priceUsd | number:'1.2-2' }}</span>
            </div>
            <div class="lb-top-actions">
              <button type="button" class="btn-open-etsy-prominent" (click)="openListingOnEtsy(lb)">
                🌐 Etsy'de Ürüne Git ↗
              </button>
              <button type="button" class="btn-clone-primary" (click)="cloneListingToFastCreator(lb)">
                🚀 {{ getItemImages(lb).length }} Fotoğrafla Klonla
              </button>
              <button type="button" class="btn-close-lb" (click)="closeLightbox()">✕</button>
            </div>
          </div>

          <div class="lb-stage">
            <button
              type="button"
              class="lb-nav prev"
              *ngIf="getItemImages(lb).length > 1"
              (click)="prevLightboxImage()">‹</button>

            <img
              *ngIf="getItemImages(lb)[lightboxIndex]"
              [src]="getItemImages(lb)[lightboxIndex]"
              alt=""
              class="lb-main-img" />

            <button
              type="button"
              class="lb-nav next"
              *ngIf="getItemImages(lb).length > 1"
              (click)="nextLightboxImage()">›</button>
          </div>

          <div class="lb-thumbs" *ngIf="getItemImages(lb).length > 1">
            <button
              type="button"
              *ngFor="let img of getItemImages(lb); let idx = index"
              class="lb-thumb-btn"
              [class.active]="idx === lightboxIndex"
              (click)="lightboxIndex = idx">
              <img [src]="img" alt="" class="lb-thumb-img" />
            </button>
          </div>
        </div>
      </div>
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
      grid-template-columns: 1.15fr 1.05fr;
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
      max-height: 680px;
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
      gap: 10px;
      max-width: 270px;
    }
    .thumb-box {
      position: relative;
      width: 50px;
      height: 50px;
      border-radius: 8px;
      overflow: hidden;
      flex-shrink: 0;
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.1);
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .thumb-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
      transition: transform 0.2s;
    }
    .thumb-box:hover .thumb-img {
      transform: scale(1.1);
    }
    .thumb-placeholder {
      font-size: 1.1rem;
      color: #475569;
    }
    .thumb-count-badge {
      position: absolute;
      bottom: 2px;
      right: 2px;
      background: rgba(15, 23, 42, 0.85);
      color: #e2e8f0;
      font-size: 0.62rem;
      font-weight: 700;
      padding: 1px 4px;
      border-radius: 4px;
    }
    .prod-title {
      font-weight: 600;
      color: #f1f5f9;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
      line-height: 1.3;
    }
    .prod-meta {
      display: flex;
      gap: 8px;
      font-size: 0.69rem;
      color: #64748b;
      margin-top: 3px;
      flex-wrap: wrap;
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

    .row-action-btns {
      display: flex;
      gap: 5px;
      align-items: center;
    }
    .btn-etsy-mini {
      padding: 5px 8px;
      background: rgba(249, 115, 22, 0.16);
      border: 1px solid rgba(249, 115, 22, 0.4);
      color: #fdba74;
      border-radius: 6px;
      font-size: 0.7rem;
      font-weight: 700;
      cursor: pointer;
      white-space: nowrap;
    }
    .btn-etsy-mini:hover { background: rgba(249, 115, 22, 0.35); color: #fff; }

    .btn-clone-mini {
      padding: 5px 9px;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.45);
      color: #a5b4fc;
      border-radius: 6px;
      font-size: 0.72rem;
      font-weight: 600;
      cursor: pointer;
      white-space: nowrap;
    }
    .btn-clone-mini:hover { background: rgba(99, 102, 241, 0.4); color: #fff; }

    /* Right panel: Selected Product Showcase */
    .intelligence-panel {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .showcase-top-grid {
      display: grid;
      grid-template-columns: 215px 1fr;
      gap: 16px;
      margin-bottom: 14px;
    }
    .showcase-gallery {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .main-img-stage {
      position: relative;
      width: 215px;
      height: 215px;
      border-radius: 12px;
      overflow: hidden;
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.12);
      cursor: zoom-in;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .main-stage-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .main-stage-empty {
      color: #64748b;
      font-size: 0.78rem;
      text-align: center;
      padding: 12px;
    }
    .stage-nav {
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
      width: 28px;
      height: 28px;
      border-radius: 50%;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.2);
      color: #fff;
      font-size: 1.1rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .stage-nav.prev { left: 6px; }
    .stage-nav.next { right: 6px; }
    .stage-counter {
      position: absolute;
      bottom: 6px;
      right: 6px;
      background: rgba(15, 23, 42, 0.82);
      color: #f8fafc;
      font-size: 0.68rem;
      font-weight: 600;
      padding: 3px 7px;
      border-radius: 5px;
    }
    .thumb-strip {
      display: flex;
      gap: 6px;
      overflow-x: auto;
      padding-bottom: 3px;
    }
    .strip-thumb-btn {
      width: 40px;
      height: 40px;
      border-radius: 6px;
      padding: 0;
      border: 2px solid transparent;
      background: #0f172a;
      cursor: pointer;
      flex-shrink: 0;
      overflow: hidden;
    }
    .strip-thumb-btn.active {
      border-color: #818cf8;
    }
    .strip-thumb-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }

    .showcase-info {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .sel-badges {
      display: flex;
      gap: 8px;
      align-items: center;
      flex-wrap: wrap;
    }
    .id-pill {
      font-size: 0.7rem;
      color: #94a3b8;
      background: rgba(15, 23, 42, 0.65);
      padding: 2px 7px;
      border-radius: 5px;
    }
    .sel-title {
      font-size: 0.96rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
      line-height: 1.38;
      cursor: pointer;
    }
    .sel-title:hover { color: #a5b4fc; }
    .ext-arrow { font-size: 0.82rem; color: #fb923c; }

    .sel-price-row {
      font-size: 0.82rem;
      color: #cbd5e1;
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
    }
    .sel-price {
      font-size: 1.2rem;
      font-weight: 800;
      color: #34d399;
    }
    .sel-quick-stats {
      display: flex;
      gap: 12px;
      flex-wrap: wrap;
      font-size: 0.76rem;
      color: #94a3b8;
      background: rgba(15, 23, 42, 0.55);
      padding: 7px 10px;
      border-radius: 8px;
    }
    .sel-actions-bar {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
      margin-top: 4px;
    }
    .btn-clone-primary {
      padding: 9px 14px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      color: #fff;
      border-radius: 8px;
      font-size: 0.79rem;
      font-weight: 700;
      cursor: pointer;
      box-shadow: 0 4px 12px rgba(99, 102, 241, 0.3);
    }
    .btn-clone-primary:hover { filter: brightness(1.08); }

    .btn-open-etsy-prominent {
      padding: 9px 14px;
      background: rgba(249, 115, 22, 0.18);
      border: 1px solid rgba(249, 115, 22, 0.5);
      color: #fdba74;
      border-radius: 8px;
      font-size: 0.79rem;
      font-weight: 700;
      cursor: pointer;
    }
    .btn-open-etsy-prominent:hover {
      background: rgba(249, 115, 22, 0.35);
      color: #fff;
    }

    .btn-copy-tags {
      padding: 9px 12px;
      background: rgba(16, 185, 129, 0.16);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
    }

    /* Product Inspector Tabs (Description & Tags) */
    .inspector-tabs-wrap {
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding-top: 12px;
    }
    .inspector-tab-header {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 10px;
      flex-wrap: wrap;
    }
    .ml-auto { margin-left: auto; }
    .insp-tab-btn {
      padding: 6px 12px;
      background: rgba(15, 23, 42, 0.65);
      border: 1px solid rgba(255, 255, 255, 0.08);
      color: #94a3b8;
      border-radius: 7px;
      font-size: 0.76rem;
      font-weight: 600;
      cursor: pointer;
    }
    .insp-tab-btn.active {
      background: rgba(99, 102, 241, 0.22);
      border-color: #818cf8;
      color: #f8fafc;
    }
    .description-reader {
      max-height: 190px;
      overflow-y: auto;
      background: rgba(15, 23, 42, 0.72);
      border: 1px solid rgba(255, 255, 255, 0.07);
      border-radius: 8px;
      padding: 11px 13px;
      font-size: 0.8rem;
      line-height: 1.55;
      color: #e2e8f0;
      white-space: pre-wrap;
    }
    .materials-row {
      display: flex;
      align-items: center;
      gap: 6px;
      flex-wrap: wrap;
      margin-top: 9px;
    }
    .mat-label {
      font-size: 0.73rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .mat-chip {
      padding: 3px 8px;
      background: rgba(56, 189, 248, 0.14);
      border: 1px solid rgba(56, 189, 248, 0.3);
      border-radius: 5px;
      font-size: 0.72rem;
      color: #7dd3fc;
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
      white-space: pre-line;
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

    /* Lightbox Modal */
    .lightbox-backdrop {
      position: fixed;
      inset: 0;
      background: rgba(7, 10, 19, 0.88);
      backdrop-filter: blur(8px);
      z-index: 9999;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }
    .lightbox-dialog {
      background: #111827;
      border: 1px solid rgba(255, 255, 255, 0.14);
      border-radius: 16px;
      max-width: 960px;
      width: 100%;
      padding: 18px 22px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      box-shadow: 0 25px 60px rgba(0, 0, 0, 0.65);
    }
    .lb-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      gap: 12px;
      flex-wrap: wrap;
    }
    .lb-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #f8fafc;
      display: block;
    }
    .lb-sub {
      font-size: 0.78rem;
      color: #94a3b8;
    }
    .lb-top-actions {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .btn-close-lb {
      width: 34px;
      height: 34px;
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.15);
      background: rgba(30, 41, 59, 0.7);
      color: #f8fafc;
      cursor: pointer;
      font-size: 0.95rem;
    }
    .lb-stage {
      position: relative;
      height: 520px;
      background: #090d16;
      border-radius: 12px;
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
    }
    .lb-main-img {
      max-width: 100%;
      max-height: 100%;
      object-fit: contain;
    }
    .lb-nav {
      position: absolute;
      top: 50%;
      transform: translateY(-50%);
      width: 42px;
      height: 42px;
      border-radius: 50%;
      background: rgba(15, 23, 42, 0.85);
      border: 1px solid rgba(255, 255, 255, 0.25);
      color: #fff;
      font-size: 1.5rem;
      cursor: pointer;
    }
    .lb-nav.prev { left: 14px; }
    .lb-nav.next { right: 14px; }
    .lb-thumbs {
      display: flex;
      gap: 8px;
      overflow-x: auto;
      justify-content: center;
      padding-bottom: 4px;
    }
    .lb-thumb-btn {
      width: 56px;
      height: 56px;
      border-radius: 8px;
      padding: 0;
      border: 2px solid transparent;
      background: #0f172a;
      cursor: pointer;
      overflow: hidden;
      flex-shrink: 0;
    }
    .lb-thumb-btn.active { border-color: #818cf8; }
    .lb-thumb-img { width: 100%; height: 100%; object-fit: cover; }

    .btn-ai-lqs-glow {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      padding: 7px 14px;
      border-radius: 8px;
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.25), rgba(168, 85, 247, 0.25));
      border: 1px solid rgba(168, 85, 247, 0.5);
      color: #c084fc;
      font-weight: 600;
      font-size: 0.84rem;
      cursor: pointer;
      transition: all 0.2s ease;
    }
    .btn-ai-lqs-glow:hover {
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.4), rgba(168, 85, 247, 0.4));
      border-color: #c084fc;
      box-shadow: 0 0 16px rgba(168, 85, 247, 0.35);
    }
    .btn-ai-lqs-glow.large-cta {
      padding: 10px 20px;
      font-size: 0.95rem;
    }
    .tab-ai-doctor {
      color: #c084fc !important;
      border-bottom-color: transparent;
    }
    .tab-ai-doctor.active {
      border-bottom-color: #c084fc !important;
      background: rgba(168, 85, 247, 0.12) !important;
    }
    .ai-doctor-content {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .ai-loading-box {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 12px;
      padding: 30px 16px;
      text-align: center;
      color: #c7d2fe;
    }
    .spinner-ai {
      width: 32px;
      height: 32px;
      border: 3px solid rgba(168, 85, 247, 0.2);
      border-top-color: #c084fc;
      border-radius: 50%;
      animation: spin 0.8s linear infinite;
    }
    @keyframes spin {
      to { transform: rotate(360deg); }
    }
    .ai-empty-prompt {
      padding: 18px 0;
    }
    .empty-prompt-card {
      background: rgba(15, 23, 42, 0.6);
      border: 1px dashed rgba(168, 85, 247, 0.4);
      border-radius: 12px;
      padding: 20px;
      text-align: center;
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 10px;
    }
    .ai-result-view {
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .ai-result-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      flex-wrap: wrap;
      gap: 10px;
      background: rgba(99, 102, 241, 0.1);
      border: 1px solid rgba(99, 102, 241, 0.25);
      border-radius: 10px;
      padding: 10px 14px;
    }
    .ai-header-left {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .badge-target-lqs {
      background: linear-gradient(135deg, #10b981, #059669);
      color: #fff;
      font-weight: 700;
      font-size: 0.85rem;
      padding: 4px 10px;
      border-radius: 6px;
    }
    .badge-provider {
      background: rgba(255, 255, 255, 0.08);
      color: #94a3b8;
      font-size: 0.8rem;
      padding: 4px 8px;
      border-radius: 6px;
    }
    .btn-clone-optimized-hero {
      background: linear-gradient(135deg, #6366f1, #a855f7);
      color: #fff;
      border: none;
      padding: 8px 16px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      transition: all 0.2s ease;
      box-shadow: 0 4px 14px rgba(99, 102, 241, 0.35);
    }
    .btn-clone-optimized-hero:hover {
      box-shadow: 0 6px 20px rgba(168, 85, 247, 0.45);
      transform: translateY(-1px);
    }
    .doctor-summary-box {
      background: rgba(168, 85, 247, 0.08);
      border-left: 3px solid #c084fc;
      border-radius: 0 8px 8px 0;
      padding: 12px 14px;
      font-size: 0.88rem;
      line-height: 1.45;
    }
    .doctor-summary-box strong {
      color: #c084fc;
      display: block;
      margin-bottom: 4px;
    }
    .doctor-summary-box p {
      margin: 0 0 6px 0;
      color: #e2e8f0;
    }
    .highlights-list {
      margin: 0;
      padding-left: 18px;
      color: #cbd5e1;
      font-size: 0.84rem;
    }
    .highlights-list li {
      margin-bottom: 2px;
    }
    .opt-field-block {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .opt-field-label {
      display: flex;
      justify-content: space-between;
      align-items: center;
      font-size: 0.82rem;
      font-weight: 600;
      color: #94a3b8;
    }
    .opt-field-box {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 10px 12px;
      color: #f1f5f9;
      font-size: 0.88rem;
      line-height: 1.4;
    }
    .opt-chip {
      background: rgba(99, 102, 241, 0.2) !important;
      border-color: rgba(99, 102, 241, 0.4) !important;
      color: #a5b4fc !important;
    }
    .vars-preview-row {
      display: flex;
      gap: 8px;
      flex-wrap: wrap;
    }
    .var-group-card {
      background: rgba(15, 23, 42, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 8px;
      padding: 8px 12px;
      font-size: 0.84rem;
      display: flex;
      gap: 6px;
    }
    .var-group-card strong {
      color: #818cf8;
    }
    .opt-desc-reader {
      max-height: 200px;
    }
    .ai-bottom-actions {
      display: flex;
      gap: 10px;
      flex-wrap: wrap;
      margin-top: 6px;
    }
    .btn-clone-secondary {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.2);
      color: #cbd5e1;
      padding: 8px 14px;
      border-radius: 8px;
      font-size: 0.84rem;
      cursor: pointer;
    }
    .btn-clone-secondary:hover {
      background: rgba(255, 255, 255, 0.14);
    }

    @media (max-width: 1180px) {
      .workspace-split { grid-template-columns: 1fr; }
      .showcase-top-grid { grid-template-columns: 1fr; }
      .main-img-stage { width: 100%; height: 260px; }
      .freq-columns { grid-template-columns: 1fr; }
    }
  `]
})
export class CompetitorSpyComponent implements OnInit {
  activeMainTab: 'competitor' | 'seasonal' = 'competitor';
  activeIntelTab: 'tags' | 'gap' | 'lqs' = 'tags';
  activeProductDetailTab: 'description' | 'tags' | 'ai-doctor' = 'description';

  isImprovingLqs = false;
  lqsImprovementResult: AiLqsImprovementResultDto | null = null;

  get activeAiModelBadge(): string {
    return this.aiSettings.activeBadgeTextClean();
  }

  shopQueryInput = '';
  sampleLimit = 50;
  isAnalyzing = false;
  errorMessage = '';
  toastMessage = '';

  analysisResult: CompetitorShopAnalysisDto | null = null;
  listings: CompetitorListingDto[] = [];
  filteredListings: CompetitorListingDto[] = [];
  selectedListing: CompetitorListingDto | null = null;
  activeImageIndex = 0;

  lightboxListing: CompetitorListingDto | null = null;
  lightboxIndex = 0;

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
        this.selectListing(this.filteredListings.length > 0 ? this.filteredListings[0] : null);

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
      this.selectListing(this.filteredListings[0] || null);
    }
  }

  selectListing(item: CompetitorListingDto | null): void {
    this.selectedListing = item;
    this.activeImageIndex = 0;
    this.lqsImprovementResult = null;
    this.isImprovingLqs = false;
  }

  getItemImages(item: CompetitorListingDto | null): string[] {
    if (!item) return [];
    if (item.imageUrls && item.imageUrls.length > 0) {
      return item.imageUrls;
    }
    return item.imageUrl ? [item.imageUrl] : [];
  }

  getPrimaryImage(item: CompetitorListingDto): string {
    const imgs = this.getItemImages(item);
    return imgs.length > 0 ? imgs[0] : '';
  }

  getActiveImageUrl(item: CompetitorListingDto): string {
    const imgs = this.getItemImages(item);
    if (imgs.length === 0) return '';
    const idx = Math.min(Math.max(0, this.activeImageIndex), imgs.length - 1);
    return imgs[idx];
  }

  setActiveImage(idx: number): void {
    this.activeImageIndex = idx;
  }

  prevImage(item: CompetitorListingDto, ev: MouseEvent): void {
    ev.stopPropagation();
    const imgs = this.getItemImages(item);
    if (imgs.length <= 1) return;
    this.activeImageIndex = (this.activeImageIndex - 1 + imgs.length) % imgs.length;
  }

  nextImage(item: CompetitorListingDto, ev: MouseEvent): void {
    ev.stopPropagation();
    const imgs = this.getItemImages(item);
    if (imgs.length <= 1) return;
    this.activeImageIndex = (this.activeImageIndex + 1) % imgs.length;
  }

  openLightbox(item: CompetitorListingDto, index: number, ev?: MouseEvent): void {
    if (ev) ev.stopPropagation();
    const imgs = this.getItemImages(item);
    if (imgs.length === 0) return;
    this.lightboxListing = item;
    this.lightboxIndex = Math.min(Math.max(0, index), imgs.length - 1);
  }

  closeLightbox(): void {
    this.lightboxListing = null;
  }

  prevLightboxImage(): void {
    if (!this.lightboxListing) return;
    const imgs = this.getItemImages(this.lightboxListing);
    if (imgs.length <= 1) return;
    this.lightboxIndex = (this.lightboxIndex - 1 + imgs.length) % imgs.length;
  }

  nextLightboxImage(): void {
    if (!this.lightboxListing) return;
    const imgs = this.getItemImages(this.lightboxListing);
    if (imgs.length <= 1) return;
    this.lightboxIndex = (this.lightboxIndex + 1) % imgs.length;
  }

  openListingOnEtsy(item: CompetitorListingDto): void {
    const url = item.listingUrl || (item.listingId > 0 ? `https://www.etsy.com/listing/${item.listingId}` : '');
    if (url) {
      window.open(url, '_blank', 'noopener');
    }
  }

  copyDescription(desc: string): void {
    if (!desc) return;
    navigator.clipboard.writeText(desc);
    this.showToast('📋 Ürün açıklaması panoya kopyalandı!');
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
    const imgs = this.getItemImages(item);
    const cloned: ClonedMarketListing = {
      id: item.listingId,
      title: item.title,
      priceUsd: item.priceUsd,
      tags: [...(item.tags || [])],
      description: item.description || '',
      imageUrl: imgs[0] || item.imageUrl || '',
      imageUrls: [...imgs],
      materials: [...(item.materials || [])],
      shopName: this.analysisResult?.shop?.shopName || '',
      seoScore: item.lqsScore,
      marketScore: item.lqsScore,
      taxonomyId: item.taxonomyId
    };

    this.etsyApi.getCompetitorListingInventory(item.listingId).subscribe({
      next: (inv) => {
        if (inv && inv.hasVariations) {
          cloned.hasVariations = true;
          cloned.varType1 = inv.varType1;
          cloned.varValues1 = inv.varValues1;
          cloned.enableVar2 = inv.enableVar2;
          cloned.varType2 = inv.varType2;
          cloned.varValues2 = inv.varValues2;
          if (inv.offerings && inv.offerings.length > 0) {
            cloned.variationRows = inv.offerings.map(o => ({
              key: o.key,
              price: o.price,
              quantity: o.quantity,
              active: o.active
            }));
          }
        }
        this.finishCloning(cloned, item, imgs.length);
      },
      error: () => {
        this.finishCloning(cloned, item, imgs.length);
      }
    });
  }

  private finishCloning(cloned: ClonedMarketListing, item: CompetitorListingDto, imgCount: number): void {
    this.aiSettings.setClonedListing(cloned);
    this.closeLightbox();
    const varText = (cloned.variationRows && cloned.variationRows.length > 0) ? ` + ${cloned.variationRows.length} varyasyon` : '';
    this.showToast(`🚀 "${item.title.slice(0, 32)}..." (${imgCount} fotoğraf + ${cloned.tags.length} etiket${varText}) İlan Oluşturucuya aktarıldı!`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 250);
  }

  cloneListingWithOptimization(item: CompetitorListingDto, opt: AiLqsImprovementResultDto): void {
    const imgs = this.getItemImages(item);
    const cloned: ClonedMarketListing = {
      id: item.listingId,
      title: opt.optimizedTitle || item.title,
      priceUsd: item.priceUsd,
      tags: [...(opt.optimizedTags && opt.optimizedTags.length > 0 ? opt.optimizedTags : (item.tags || []))],
      description: opt.optimizedDescription || item.description || '',
      imageUrl: imgs[0] || item.imageUrl || '',
      imageUrls: [...imgs],
      materials: [...(opt.suggestedMaterials && opt.suggestedMaterials.length > 0 ? opt.suggestedMaterials : (item.materials || []))],
      shopName: this.analysisResult?.shop?.shopName || '',
      seoScore: opt.targetLqsScore || 100,
      marketScore: opt.targetLqsScore || 100
    };

    if (opt.suggestedVariations && opt.suggestedVariations.length > 0) {
      cloned.hasVariations = true;
      cloned.varType1 = opt.suggestedVariations[0].name;
      cloned.varValues1 = opt.suggestedVariations[0].values.join(', ');
      if (opt.suggestedVariations.length > 1) {
        cloned.enableVar2 = true;
        cloned.varType2 = opt.suggestedVariations[1].name;
        cloned.varValues2 = opt.suggestedVariations[1].values.join(', ');
      }
      if (opt.suggestedOfferings && opt.suggestedOfferings.length > 0) {
        cloned.variationRows = opt.suggestedOfferings.map(o => ({
          key: o.key,
          price: o.price,
          quantity: o.quantity,
          active: o.active
        }));
      }
    }

    this.aiSettings.setClonedListing(cloned);
    this.closeLightbox();
    this.showToast(`✨ 100/100 Optimize "${cloned.title.slice(0, 30)}..." (${imgs.length} fotoğraf + 13 altın etiket + varyasyonlar) İlan Oluşturucuya aktarıldı!`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 250);
  }

  improveSelectedListingLqs(sel: CompetitorListingDto): void {
    this.isImprovingLqs = true;
    this.activeProductDetailTab = 'ai-doctor';
    const s = this.aiSettings.settings();
    const apiKey = (s as any)[`${s.provider.toLowerCase()}ApiKey`] || s.geminiApiKey || s.deepSeekApiKey || '';
    const cmd: AiLqsImprovementCommandDto = {
      listingId: sel.listingId,
      title: sel.title,
      tags: sel.tags,
      description: sel.description,
      materials: sel.materials,
      priceUsd: sel.priceUsd,
      category: 'Handmade',
      currentLqsScore: sel.lqsScore,
      provider: s.provider,
      apiKey: apiKey,
      model: this.aiSettings.activeModelName()
    };

    this.etsyApi.improveListingLqs(cmd).subscribe({
      next: (res) => {
        this.isImprovingLqs = false;
        this.lqsImprovementResult = res;
        this.showToast(`✨ 100/100 LQS Reçetesi hazırlandı (${res.provider})!`);
      },
      error: () => {
        this.isImprovingLqs = false;
        this.showToast('❌ LQS iyileştirmesi oluşturulamadı.');
      }
    });
  }

  copyText(text: string, label: string = 'Metin'): void {
    if (!text) return;
    navigator.clipboard.writeText(text);
    this.showToast(`📋 ${label} panoya kopyalandı!`);
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
    const headers = ['Sira', 'ListingId', 'Baslik', 'FiyatUSD', 'TahminiAylikSatis', 'TahminiAylikCiroUSD', 'Favori', 'Goruntulenme', 'GorselSayisi', 'LQSNotu', 'LQSSkor', 'EtiketSayisi', 'EtsyUrl', 'Etiketler'];
    const rows = this.listings.map(l => [
      l.rank,
      l.listingId,
      `"${(l.title || '').replace(/"/g, '""')}"`,
      l.priceUsd.toFixed(2),
      l.estimatedMonthlySales,
      l.estimatedMonthlyRevenueUsd.toFixed(2),
      l.favorites,
      l.views,
      this.getItemImages(l).length,
      l.lqsGrade,
      l.lqsScore,
      l.tagCount,
      l.listingUrl,
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
    const imgCount = this.getItemImages(item).length;
    const descLen = (item.description || '').length;
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
      icon: imgCount >= 8 ? '✅' : (imgCount >= 5 ? '⚠️' : '❌'),
      label: `Görsel Galerisi (${imgCount}/10 fotoğraf)`,
      detail: imgCount >= 8
        ? 'Görsel çeşitliliği yüksek.'
        : 'Görsel sayısı yetersiz; ölçü tablosu, yaşam alanı (lifestyle) ve yakın çekim fotoğraflarla dönüşüm oranını artırın.',
      passed: imgCount >= 8
    });

    checks.push({
      icon: descLen >= 300 ? '✅' : (descLen >= 150 ? '⚠️' : '❌'),
      label: `Ürün Açıklaması (${descLen} karakter)`,
      detail: descLen >= 300
        ? 'Açıklama metni Google ve Etsy SEO indekslemesi için yeterli uzunlukta.'
        : 'Açıklama çok kısa; malzeme, boyut, kargo ve hediye paketi detaylarını içeren zengin bir açıklama yazın.',
      passed: descLen >= 300
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
      imageUrls: [],
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
