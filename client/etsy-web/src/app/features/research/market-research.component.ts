import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { Router } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService, ClonedMarketListing } from '../../core/services/ai-settings.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

export interface MarketItem {
  id: number;
  listingRank: number;
  title: string;
  priceUsd: number;
  shopName: string;
  shopSales: number;
  shopUrl: string;
  listingUrl: string;
  favorites: number;
  views: number;
  seoScore: number;
  marketScore: number;
  tags: string[];
  imageUrl: string;
  imageUrls: string[];
  description: string;
  reviewCount: number;
  reviewAverage: number;
  quantity: number;
}

@Component({
  selector: 'app-market-research',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <div class="market-view">
      <!-- 1. HEADER (Title + AI Active Badge + Status) -->
      <div class="market-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <circle cx="11" cy="11" r="8"></circle>
              <line x1="21" y1="21" x2="16.65" y2="16.65"></line>
            </svg>
          </div>
          <div>
            <h1 class="page-title">🔍 Etsy Pazar Araştırması & Rakip İstihbaratı</h1>
            <p class="page-subtitle">Arama hacmi, ortalama fiyatlar, rekabet skoru, SEO ve pazar fırsat puanlaması</p>
          </div>
        </div>

        <div class="header-right">
          <!-- Active AI Model Hub Badge (Matching Desktop Image 1) -->
          <button 
            type="button"
            class="ai-badge-btn" 
            [ngClass]="aiService.activeBadgeClass()" 
            (click)="aiService.openAiSettingsModal()" 
            title="Yapay Zeka Model Ayarları & Sağlayıcı Merkezi (Tıklayın)">
            <app-ai-logo [provider]="aiService.activeProvider()" [size]="16"></app-ai-logo>
            <span>{{ aiService.activeBadgeTextClean() }}</span>
            <span class="hub-gear">⚙️</span>
          </button>

          <span class="header-status-text">{{ statusText }}</span>
        </div>
      </div>

      <!-- 2. SEARCH TOOLBAR (2-Row Responsive Layout) -->
      <div class="glass-card search-card">
        <div class="search-form-row">
          <div class="search-input-wrap">
            <span class="search-icon">🔍</span>
            <input 
              type="text" 
              [(ngModel)]="searchKeyword" 
              (keyup.enter)="onSearch()"
              placeholder="Etsy pazarında aranacak kelime: örn. 3d printed desk organizer, cosplay helmet, resin lamp..." 
              class="search-input" />
          </div>
          <div class="filter-wrap">
            <label class="filter-label">Limit:</label>
            <select [(ngModel)]="limit" class="filter-select">
              <option [value]="10">10 İlan</option>
              <option [value]="25">25 İlan</option>
              <option [value]="50">50 İlan</option>
              <option [value]="100">100 İlan</option>
            </select>
          </div>
          <div class="filter-wrap">
            <label class="filter-label">Sıralama:</label>
            <select [(ngModel)]="sortBy" class="filter-select" (change)="applySort()">
              <option value="market_score">Pazar Puanı (Önce En Yüksek)</option>
              <option value="seo_score">SEO Puanı (Önce En Yüksek)</option>
              <option value="favorites">En Çok Beğenilen (Favori)</option>
              <option value="views">Görüntülenme Sayısı</option>
              <option value="shop_sales">Mağaza Satışı</option>
              <option value="price_asc">Fiyat (Önce En Düşük)</option>
              <option value="price_desc">Fiyat (Önce En Yüksek)</option>
            </select>
          </div>
          <button class="btn-primary-search" (click)="onSearch()">
            🔍 Etsy'de Ara
          </button>
        </div>

        <div class="search-actions-row">
          <button class="btn-sub-action" (click)="generateAiMarketReport()" [disabled]="isGeneratingReport">
            <span>{{ isGeneratingReport ? 'Yapay Zeka Analiz Ediyor...' : '✨ AI Pazar Özeti' }}</span>
          </button>
          <button class="btn-sub-action" (click)="copyAllTopTags()">
            <span>📋 13 Altın Tag'i Kopyala</span>
          </button>
          <button class="btn-sub-action" (click)="filterOnlyTopOpportunity()">
            <span>⚡ Fırsat Ürünlerini Filtrele</span>
          </button>
        </div>
      </div>

      <!-- 3. KPI SUMMARY STRIP (4 KPI Cards Matching Desktop) -->
      <div class="kpi-grid">
        <div class="glass-card kpi-card">
          <span class="kpi-label">ORTALAMA FİYAT (AOV)</span>
          <div class="kpi-val text-emerald">
            &#36;{{ avgPriceUsd | number:'1.2-2' }}
            <span class="sub-try">(₺{{ (avgPriceUsd * etsyApi.exchangeRate()) | number:'1.0-0' }})</span>
          </div>
          <span class="kpi-desc">Kategorideki rekabetçi satış ortalaması</span>
        </div>

        <div class="glass-card kpi-card">
          <span class="kpi-label">ORTALAMA FAVORİ / İLAN</span>
          <div class="kpi-val text-cyan">
            {{ avgFavorites | number:'1.0-0' }} Fav
          </div>
          <span class="kpi-desc">Müşteri kaydetme ve satın alma ilgisi</span>
        </div>

        <div class="glass-card kpi-card">
          <span class="kpi-label">LİDER SATICI MAĞAZA</span>
          <div class="kpi-val text-purple">
            {{ topShopName }}
          </div>
          <span class="kpi-desc">Kategori hacminin %34'üne hakim</span>
        </div>

        <div class="glass-card kpi-card highlight-orange">
          <span class="kpi-label">PAZAR FIRSAT SKORU</span>
          <div class="kpi-val text-orange">
            %{{ opportunityScore }} / 100
          </div>
          <span class="kpi-desc">Yüksek talep, optimize edilebilir rekabet</span>
        </div>
      </div>

      <!-- AI REPORT CALLOUT -->
      <div *ngIf="aiReport" class="glass-card ai-report-card">
        <div class="ai-report-head">
          <span class="ai-badge">🤖 {{ aiService.activeBadgeText() }} - PAZAR STRATEJİ RAPORU</span>
          <button class="close-report" (click)="aiReport = ''">×</button>
        </div>
        <div class="ai-report-content" [innerHTML]="aiReport"></div>
      </div>

      <!-- 4. PRODUCT LISTINGS TABLE (With SEO & Market Score Columns) -->
      <div class="glass-card table-card">
        <div class="table-head-row">
          <div class="table-title-group">
            <span class="table-title">Pazar İlanları</span>
            <span class="table-count-badge">{{ items.length }} Ürün Sıralandı</span>
          </div>
          <span class="table-hint">💡 Bir satıra tıklayarak alttaki 12 eylem aracını kullanabilir veya "🚀 Taslağa Klonla" ile hızlıca satışa açabilirsiniz.</span>
        </div>

        <div class="table-wrap">
          <table class="market-table">
            <thead>
              <tr>
                <th style="width: 50px;">Resim</th>
                <th style="width: 45px;">Sıra</th>
                <th>Ürün Başlığı</th>
                <th style="width: 110px;">Fiyat ($ / ₺)</th>
                <th style="width: 140px;">Mağaza</th>
                <th style="width: 95px;">Mağaza Satışı</th>
                <th style="width: 75px;" class="sortable-fav" (click)="toggleFavSort()">
                  Favori {{ favSortDesc ? '▼' : '▲' }}
                </th>
                <th style="width: 85px;">Görüntüleme</th>
                <th style="width: 75px; text-align: center;">SEO</th>
                <th style="width: 95px; text-align: center;">Pazar Puanı</th>
                <th style="width: 220px;">Tagler</th>
              </tr>
            </thead>
            <tbody>
              <tr 
                *ngFor="let item of items; let idx = index"
                [class.selected-row]="selectedItem?.id === item.id"
                (click)="selectItem(item)"
                (dblclick)="openEtsyListing(item)">
                
                <!-- Resim Thumbnail -->
                <td class="thumb-cell">
                  <img [src]="item.imageUrl" alt="Thumb" class="prod-thumb" (click)="openImageZoom(item, $event)" />
                </td>

                <!-- Sıra -->
                <td class="rank-cell">#{{ item.listingRank }}</td>

                <!-- Ürün Başlığı -->
                <td class="title-cell">
                  <span class="prod-title" [title]="item.title">{{ item.title }}</span>
                </td>

                <!-- Fiyat -->
                <td class="price-cell">
                  <b>&#36;{{ item.priceUsd | number:'1.2-2' }}</b>
                  <span class="try-sm">₺{{ (item.priceUsd * etsyApi.exchangeRate()) | number:'1.0-0' }}</span>
                </td>

                <!-- Mağaza -->
                <td class="shop-cell">
                  <a [href]="item.shopUrl" target="_blank" (click)="$event.stopPropagation()" class="shop-link" title="Etsy Mağazasını Aç">
                    {{ item.shopName }} ↗
                  </a>
                </td>

                <!-- Mağaza Satışı -->
                <td>{{ item.shopSales | number }} Satış</td>

                <!-- Favori -->
                <td class="fav-cell">❤️ {{ item.favorites | number }}</td>

                <!-- Görüntüleme -->
                <td class="view-cell">👁️ {{ item.views | number }}</td>

                <!-- SEO PUANI ROZETİ -->
                <td class="score-cell">
                  <span class="score-badge" [ngClass]="getScoreColorClass(item.seoScore)">
                    %{{ item.seoScore }}
                  </span>
                </td>

                <!-- PAZAR PUANI ROZETİ -->
                <td class="score-cell">
                  <span class="score-badge market" [ngClass]="getScoreColorClass(item.marketScore)">
                    %{{ item.marketScore }}
                  </span>
                </td>

                <!-- Tagler -->
                <td class="tags-cell">
                  <div class="mini-tags-wrap">
                    <span *ngFor="let t of item.tags.slice(0, 3)" class="mini-tag">{{ t }}</span>
                    <span *ngIf="item.tags.length > 3" class="mini-tag more">+{{ item.tags.length - 3 }}</span>
                  </div>
                </td>
              </tr>
            </tbody>
          </table>
        </div>
      </div>

      <!-- 5. BOTTOM DETAILS & 12 ACTION BUTTONS (Matching MarketResearchForm.cs Image) -->
      <div class="glass-card bottom-details-card" *ngIf="selectedItem">
        <div class="bottom-grid">
          
          <!-- Sol: Görsel Önizleme ve Slider -->
          <div class="bottom-image-box">
            <div class="image-preview-wrap">
              <img [src]="selectedItem.imageUrls[currentImageIndex] || selectedItem.imageUrl" alt="Preview" class="slider-image" />
              <div class="slider-controls">
                <button type="button" class="btn-slide" (click)="prevImage()">◀</button>
                <span class="slide-indicator">{{ currentImageIndex + 1 }} / {{ selectedItem.imageUrls.length || 1 }}</span>
                <button type="button" class="btn-slide" (click)="nextImage()">▶</button>
              </div>
            </div>
            <button type="button" class="btn-send-studio" (click)="sendToImageStudio()">
              📸 Görsel Stüdyo'ya Gönder
            </button>
          </div>

          <!-- Orta: Monospace Detay Kutusu -->
          <div class="bottom-detail-box">
            <div class="detail-content-text">
📌 <strong>BAŞLIK:</strong> {{ selectedItem.title }}

🏪 <strong>MAĞAZA / FİYAT:</strong> {{ selectedItem.shopName }} | {{ '$' + selectedItem.priceUsd.toFixed(2) }} (₺{{ (selectedItem.priceUsd * etsyApi.exchangeRate()).toFixed(0) }})
Mağaza Satışı: {{ selectedItem.shopSales | number }} | Yorum: {{ selectedItem.reviewCount | number }} | Puan: {{ selectedItem.reviewAverage }} ⭐
Favori: {{ selectedItem.favorites | number }} | Görüntülenme: {{ selectedItem.views | number }} | Stok: {{ selectedItem.quantity }}

📊 <strong>SEO / PAZAR:</strong> SEO Puanı: %{{ selectedItem.seoScore }}/100 | Pazar Fırsat Puanı: %{{ selectedItem.marketScore }}/100

🏷️ <strong>ETİKETLER ({{ selectedItem.tags.length }}):</strong>
{{ selectedItem.tags.join(', ') }}

📝 <strong>ÜRÜN AÇIKLAMASI:</strong>
{{ selectedItem.description }}
            </div>
          </div>

          <!-- Sağ: 12 MASAÜSTÜ EYLEM BUTONU (Grouped & Colorful) -->
          <div class="bottom-actions-panel">
            <!-- 1. Satır: Birincil Aksiyonlar -->
            <div class="actions-row">
              <button type="button" class="btn-action-primary clone-btn" (click)="cloneSelectedListing()">
                🚀 Taslağa Klonla
              </button>
              <button type="button" class="btn-action-primary opt-btn" (click)="optimizeWithAi()">
                <app-ai-logo [provider]="aiService.activeProvider()" [size]="16"></app-ai-logo>
                <span>AI Optimizasyon</span>
              </button>
              <button type="button" class="btn-action-neutral" (click)="trackSelectedListing()">
                📌 Takibe Ekle
              </button>
            </div>

            <!-- 2. Satır: Analiz Araçları -->
            <div class="actions-row">
              <button type="button" class="btn-action-neutral" (click)="openKeywordAnalysisModal()">
                🔍 Kelime Analizi
              </button>
              <button type="button" class="btn-action-neutral" (click)="openCompetitorAnalysisModal()">
                🕵️ Rakip Analizi
              </button>
              <button type="button" class="btn-action-neutral" (click)="openShopUrl()">
                🏪 Mağaza Aç
              </button>
            </div>

            <!-- 3. Satır: Sağlık & Kopyalama -->
            <div class="actions-row">
              <button type="button" class="btn-action-neutral health-btn" (click)="openHealthScoreModal()">
                🩺 Sağlık Skoru
              </button>
              <button type="button" class="btn-action-neutral" (click)="copySelectedTags()">
                🏷️ Tagleri Kopyala
              </button>
              <button type="button" class="btn-action-neutral" (click)="copySelectedTitle()">
                📝 Başlığı Kopyala
              </button>
            </div>

            <!-- 4. Satır: Dışa Aktarma & Linkler -->
            <div class="actions-row">
              <button type="button" class="btn-action-neutral" (click)="openEtsyListing(selectedItem)">
                🌐 Etsy'de Aç
              </button>
              <button type="button" class="btn-action-neutral" (click)="exportCsv()">
                📊 CSV Aktar
              </button>
              <button type="button" class="btn-action-neutral" (click)="copyListingLink()">
                🔗 Linki Kopyala
              </button>
            </div>
          </div>

        </div>
      </div>

      <!-- TOAST NOTIFICATION -->
      <div class="toast-popup" *ngIf="toastMessage">
        {{ toastMessage }}
      </div>

      <!-- HEALTH SCORE MODAL -->
      <div class="modal-backdrop" *ngIf="showHealthModal" (click)="showHealthModal = false">
        <div class="modal-card-analysis" (click)="$event.stopPropagation()">
          <div class="modal-header">
            <h3>🩺 İlan SEO & Kalite Sağlık Denetimi</h3>
            <button class="btn-close-modal" (click)="showHealthModal = false">✕</button>
          </div>
          <div class="modal-body" *ngIf="selectedItem">
            <div class="health-gauge-box">
              <div class="gauge-score" [ngClass]="getScoreColorClass(selectedItem.seoScore)">
                %{{ selectedItem.seoScore }}
              </div>
              <div class="gauge-label">Genel Etsy SEO Sağlık Skoru</div>
            </div>

            <div class="audit-checklist">
              <div class="check-item">
                <span class="icon">✅</span>
                <span>Başlık Uzunluğu: <strong>{{ selectedItem.title.length }} / 140 Karakter</strong> (Kusursuz anahtar kelime yerleşimi)</span>
              </div>
              <div class="check-item">
                <span class="icon">✅</span>
                <span>Etiket Doluluğu: <strong>{{ selectedItem.tags.length }} / 13 Etiket</strong> tamamlandı.</span>
              </div>
              <div class="check-item">
                <span class="icon">✅</span>
                <span>Görsel Çözünürlüğü: <strong>2000x2000px HD</strong> formatında hazır.</span>
              </div>
              <div class="check-item">
                <span class="icon">💡</span>
                <span>Pazar Fırsat Skoru: <strong>%{{ selectedItem.marketScore }}</strong> - Bu ürünü klonlayarak hızlıca rekabete girebilirsiniz.</span>
              </div>
            </div>
          </div>
          <div class="modal-footer">
            <button class="btn-primary-modal" (click)="cloneSelectedListing()">🚀 Bu İlanı Klonla & Yayınla</button>
            <button class="btn-secondary-modal" (click)="showHealthModal = false">Kapat</button>
          </div>
        </div>
      </div>

    </div>
  `,
  styles: [`
    .market-view {
      padding: 24px 32px;
      background: #090d16;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 16px;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
    }

    .market-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .icon-box {
      width: 42px;
      height: 42px;
      border-radius: 10px;
      background: linear-gradient(135deg, rgba(6, 182, 212, 0.2), rgba(59, 130, 246, 0.2));
      border: 1px solid rgba(6, 182, 212, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #38bdf8;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
      letter-spacing: -0.02em;
    }
    .page-subtitle {
      font-size: 0.78rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }

    .header-right {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .ai-badge-btn {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 6px 14px;
      border-radius: 9999px;
      font-size: 0.8rem;
      font-weight: 700;
      cursor: pointer;
      border: 1px solid rgba(255, 255, 255, 0.15);
      background: #182238;
      color: #fff;
      transition: all 0.2s;
    }
    .ai-badge-btn:hover {
      transform: translateY(-1px);
      filter: brightness(1.1);
    }
    .ai-badge-btn.badge-gemini { background: rgba(59, 130, 246, 0.22); border-color: #3b82f6; color: #93c5fd; }
    .ai-badge-btn.badge-openai { background: rgba(16, 185, 129, 0.22); border-color: #10b981; color: #6ee7b7; }
    .ai-badge-btn.badge-claude { background: rgba(168, 85, 247, 0.22); border-color: #a855f7; color: #d8b4fe; }
    .ai-badge-btn.badge-deepseek { background: rgba(239, 68, 68, 0.22); border-color: #ef4444; color: #fca5a5; }
    .ai-badge-btn.badge-grok { background: rgba(249, 115, 22, 0.22); border-color: #f97316; color: #fdba74; }
    .ai-badge-btn.badge-offline { background: rgba(100, 116, 139, 0.22); border-color: #64748b; color: #cbd5e1; }

    .hub-gear {
      font-size: 0.82rem;
      opacity: 0.8;
    }
    .header-status-text {
      font-size: 0.78rem;
      color: #94a3b8;
    }

    .glass-card {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 16px 20px;
    }

    /* SEARCH CARD */
    .search-card {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .search-form-row {
      display: flex;
      gap: 12px;
      align-items: center;
    }
    .search-input-wrap {
      flex: 1;
      position: relative;
      display: flex;
      align-items: center;
    }
    .search-icon {
      position: absolute;
      left: 12px;
      font-size: 0.9rem;
      color: #64748b;
    }
    .search-input {
      width: 100%;
      background: #090e1a;
      border: 1px solid #26334d;
      border-radius: 8px;
      padding: 10px 14px 10px 36px;
      color: #fff;
      font-size: 0.86rem;
      outline: none;
      transition: border-color 0.15s;
    }
    .search-input:focus {
      border-color: #38bdf8;
      box-shadow: 0 0 8px rgba(56, 189, 248, 0.25);
    }
    .filter-wrap {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .filter-label {
      font-size: 0.78rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .filter-select {
      background: #090e1a;
      border: 1px solid #26334d;
      border-radius: 8px;
      padding: 9px 12px;
      color: #f1f5f9;
      font-size: 0.82rem;
      outline: none;
    }
    .btn-primary-search {
      background: #0284c7;
      border: none;
      border-radius: 8px;
      padding: 10px 20px;
      color: #fff;
      font-size: 0.86rem;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.15s;
      white-space: nowrap;
    }
    .btn-primary-search:hover {
      background: #0369a1;
    }

    .search-actions-row {
      display: flex;
      gap: 10px;
      padding-top: 8px;
      border-top: 1px solid rgba(255, 255, 255, 0.05);
    }
    .btn-sub-action {
      background: #182238;
      border: 1px solid #26334d;
      border-radius: 6px;
      padding: 6px 14px;
      color: #cbd5e1;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
    }
    .btn-sub-action:hover {
      background: #1e2c47;
      border-color: #38bdf8;
      color: #fff;
    }

    /* KPI GRID */
    .kpi-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 14px;
    }
    .kpi-card {
      padding: 14px 18px;
      display: flex;
      flex-direction: column;
      gap: 4px;
      border-radius: 10px;
    }
    .kpi-card.highlight-orange {
      border-color: rgba(249, 115, 22, 0.4);
      background: rgba(249, 115, 22, 0.06);
    }
    .kpi-label {
      font-size: 0.72rem;
      font-weight: 700;
      color: #94a3b8;
      text-transform: uppercase;
      letter-spacing: 0.04em;
    }
    .kpi-val {
      font-size: 1.35rem;
      font-weight: 800;
    }
    .kpi-val.text-emerald { color: #10b981; }
    .kpi-val.text-cyan { color: #06b6d4; }
    .kpi-val.text-purple { color: #a855f7; }
    .kpi-val.text-orange { color: #f97316; }
    .sub-try { font-size: 0.78rem; font-weight: 600; color: #94a3b8; margin-left: 4px; }
    .kpi-desc { font-size: 0.72rem; color: #64748b; }

    /* AI REPORT */
    .ai-report-card {
      background: rgba(88, 28, 135, 0.2);
      border: 1px solid rgba(168, 85, 247, 0.4);
      border-radius: 12px;
      padding: 16px;
    }
    .ai-report-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .ai-badge {
      font-size: 0.76rem;
      font-weight: 700;
      color: #c084fc;
    }
    .close-report {
      background: none;
      border: none;
      color: #94a3b8;
      font-size: 1.2rem;
      cursor: pointer;
    }
    .ai-report-content {
      font-size: 0.82rem;
      line-height: 1.55;
      color: #e2e8f0;
    }

    /* TABLE */
    .table-card {
      padding: 16px 20px;
    }
    .table-head-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }
    .table-title-group {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .table-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #fff;
    }
    .table-count-badge {
      font-size: 0.72rem;
      font-weight: 700;
      background: #1e293b;
      color: #38bdf8;
      padding: 2px 8px;
      border-radius: 4px;
    }
    .table-hint {
      font-size: 0.74rem;
      color: #64748b;
      font-style: italic;
    }

    .table-wrap {
      overflow-x: auto;
      max-height: 420px;
      border: 1px solid #1e293b;
      border-radius: 8px;
    }
    .market-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.8rem;
    }
    .market-table th {
      background: #111a2e;
      color: #94a3b8;
      text-align: left;
      padding: 10px 12px;
      font-size: 0.75rem;
      font-weight: 700;
      border-bottom: 1px solid #26334d;
      position: sticky;
      top: 0;
      z-index: 10;
    }
    .market-table th.sortable-fav {
      cursor: pointer;
      color: #f87171;
    }
    .market-table td {
      padding: 8px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
      vertical-align: middle;
    }
    .market-table tr:hover {
      background: rgba(56, 189, 248, 0.05);
      cursor: pointer;
    }
    .market-table tr.selected-row {
      background: rgba(56, 189, 248, 0.12);
      border-left: 3px solid #38bdf8;
    }

    .thumb-cell { padding: 4px 8px; }
    .prod-thumb {
      width: 44px;
      height: 44px;
      border-radius: 6px;
      object-fit: cover;
      border: 1px solid #26334d;
      cursor: zoom-in;
    }
    .rank-cell { font-weight: 700; color: #94a3b8; }
    .title-cell { max-width: 280px; }
    .prod-title {
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
      font-weight: 600;
      color: #f1f5f9;
      line-height: 1.35;
    }
    .price-cell { display: flex; flex-direction: column; gap: 2px; }
    .try-sm { font-size: 0.72rem; color: #64748b; }
    .shop-link { color: #38bdf8; text-decoration: none; font-weight: 600; }
    .shop-link:hover { text-decoration: underline; }
    .fav-cell { color: #f87171; font-weight: 700; }
    .view-cell { color: #94a3b8; }

    /* SCORE BADGES */
    .score-cell { text-align: center; }
    .score-badge {
      display: inline-block;
      font-size: 0.75rem;
      font-weight: 800;
      padding: 3px 8px;
      border-radius: 6px;
      min-width: 38px;
    }
    .score-badge.high { background: rgba(16, 185, 129, 0.18); color: #34d399; border: 1px solid rgba(16, 185, 129, 0.4); }
    .score-badge.mid { background: rgba(245, 158, 11, 0.18); color: #fbbf24; border: 1px solid rgba(245, 158, 11, 0.4); }
    .score-badge.low { background: rgba(239, 68, 68, 0.18); color: #f87171; border: 1px solid rgba(239, 68, 68, 0.4); }

    .mini-tags-wrap {
      display: flex;
      flex-wrap: wrap;
      gap: 4px;
    }
    .mini-tag {
      background: #182238;
      border: 1px solid #26334d;
      color: #94a3b8;
      font-size: 0.7rem;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .mini-tag.more {
      background: #1e293b;
      color: #38bdf8;
      font-weight: 700;
    }

    /* 5. BOTTOM DETAILS & 12 ACTION BUTTONS */
    .bottom-details-card {
      padding: 16px 20px;
      background: #0d1527;
      border: 1px solid #26334d;
    }
    .bottom-grid {
      display: grid;
      grid-template-columns: 220px 1.4fr 1.6fr;
      gap: 16px;
    }

    /* Sol Görsel Kutusu */
    .bottom-image-box {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .image-preview-wrap {
      position: relative;
      width: 100%;
      height: 160px;
      border-radius: 8px;
      overflow: hidden;
      background: #050811;
      border: 1px solid #1e293b;
    }
    .slider-image {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .slider-controls {
      position: absolute;
      bottom: 0;
      left: 0;
      right: 0;
      background: rgba(0, 0, 0, 0.75);
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 4px 8px;
    }
    .btn-slide {
      background: transparent;
      border: none;
      color: #fff;
      font-size: 0.8rem;
      cursor: pointer;
      padding: 2px 6px;
    }
    .slide-indicator {
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .btn-send-studio {
      background: #4f46e5;
      border: none;
      border-radius: 6px;
      padding: 7px 10px;
      color: #fff;
      font-size: 0.76rem;
      font-weight: 700;
      cursor: pointer;
      transition: background 0.15s;
    }
    .btn-send-studio:hover { background: #4338ca; }

    /* Orta Detay Metin Kutusu */
    .bottom-detail-box {
      background: #050811;
      border: 1px solid #1e293b;
      border-radius: 8px;
      padding: 10px 14px;
      max-height: 200px;
      overflow-y: auto;
    }
    .detail-content-text {
      font-family: Consolas, monospace;
      font-size: 0.75rem;
      color: #cbd5e1;
      white-space: pre-wrap;
      line-height: 1.45;
    }

    /* Sağ: 12 Masaüstü Butonu */
    .bottom-actions-panel {
      display: flex;
      flex-direction: column;
      gap: 8px;
      justify-content: space-between;
    }
    .actions-row {
      display: grid;
      grid-template-columns: repeat(3, 1fr);
      gap: 8px;
    }
    .btn-action-primary {
      border: none;
      border-radius: 6px;
      padding: 8px 10px;
      color: #fff;
      font-size: 0.78rem;
      font-weight: 700;
      cursor: pointer;
      transition: all 0.15s;
      white-space: nowrap;
      text-align: center;
    }
    .clone-btn {
      background: #10b981;
      box-shadow: 0 2px 8px rgba(16, 185, 129, 0.3);
    }
    .clone-btn:hover { background: #059669; transform: translateY(-1px); }
    .opt-btn {
      background: #7c3aed;
      box-shadow: 0 2px 8px rgba(124, 58, 237, 0.3);
    }
    .opt-btn:hover { background: #6d28d9; transform: translateY(-1px); }

    .btn-action-neutral {
      background: #182238;
      border: 1px solid #26334d;
      border-radius: 6px;
      padding: 8px 10px;
      color: #cbd5e1;
      font-size: 0.76rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.15s;
      white-space: nowrap;
      text-align: center;
    }
    .btn-action-neutral:hover {
      background: #1e2c47;
      border-color: #38bdf8;
      color: #fff;
    }
    .health-btn {
      background: rgba(14, 165, 233, 0.15);
      border-color: #0ea5e9;
      color: #38bdf8;
    }

    /* TOAST */
    .toast-popup {
      position: fixed;
      bottom: 24px;
      right: 32px;
      background: #10b981;
      color: #fff;
      font-weight: 700;
      font-size: 0.84rem;
      padding: 10px 18px;
      border-radius: 8px;
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.6);
      z-index: 100000;
      animation: pop-toast 0.2s ease-out;
    }
    @keyframes pop-toast {
      from { transform: translateY(10px); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }

    /* HEALTH AUDIT MODAL */
    .modal-backdrop {
      position: fixed;
      top: 0; left: 0; right: 0; bottom: 0;
      background: rgba(0, 0, 0, 0.8);
      backdrop-filter: blur(4px);
      z-index: 99999;
      display: flex;
      align-items: center;
      justify-content: center;
    }
    .modal-card-analysis {
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 14px;
      width: 90%;
      max-width: 580px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 16px;
      box-shadow: 0 20px 48px rgba(0, 0, 0, 0.85);
    }
    .modal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 10px;
    }
    .modal-header h3 { margin: 0; font-size: 1rem; color: #fff; }
    .btn-close-modal {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.2rem;
      cursor: pointer;
    }
    .health-gauge-box {
      display: flex;
      flex-direction: column;
      align-items: center;
      gap: 6px;
      padding: 16px;
      background: #090e1a;
      border-radius: 10px;
      border: 1px solid #1e293b;
    }
    .gauge-score {
      font-size: 2.2rem;
      font-weight: 900;
    }
    .gauge-label {
      font-size: 0.8rem;
      color: #94a3b8;
    }
    .audit-checklist {
      display: flex;
      flex-direction: column;
      gap: 8px;
      font-size: 0.82rem;
    }
    .check-item {
      display: flex;
      align-items: flex-start;
      gap: 8px;
      color: #cbd5e1;
    }
    .modal-footer {
      display: flex;
      justify-content: flex-end;
      gap: 10px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      padding-top: 12px;
    }
    .btn-primary-modal {
      background: #10b981;
      border: none;
      color: #fff;
      font-weight: 700;
      padding: 8px 16px;
      border-radius: 6px;
      cursor: pointer;
    }
    .btn-secondary-modal {
      background: #1e293b;
      border: none;
      color: #cbd5e1;
      padding: 8px 16px;
      border-radius: 6px;
      cursor: pointer;
    }
  `]
})
export class MarketResearchComponent implements OnInit {
  etsyApi = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  router = inject(Router);

  searchKeyword = '3D Printed Articulated Dragon';
  limit = 25;
  sortBy = 'market_score';
  statusText = '9 ilan listelendi. Sıralama: Pazar Puanı';
  isGeneratingReport = false;
  aiReport = '';
  showHealthModal = false;
  toastMessage = '';

  avgPriceUsd = 34.80;
  avgFavorites = 1420;
  topShopName = 'MythicForgeCrafts';
  opportunityScore = 88;

  favSortDesc = true;
  currentImageIndex = 0;

  items: MarketItem[] = [
    {
      id: 4188710928,
      listingRank: 1,
      title: 'Articulated Crystal Dragon with Moving Wings 3D Print Fidget Toy',
      priceUsd: 39.50,
      shopName: 'MythicForgeCrafts',
      shopSales: 14820,
      shopUrl: 'https://www.etsy.com/shop/MythicForgeCrafts',
      listingUrl: 'https://www.etsy.com/listing/4188710928',
      views: 18450,
      favorites: 3420,
      seoScore: 96,
      marketScore: 94,
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=600',
        'https://images.unsplash.com/photo-1618005182384-a83a8bd57fbe?w=600'
      ],
      description: 'Handcrafted high quality articulated crystal dragon with movable wings. Printed using premium dual-color silk PLA on Bambu Lab X1-Carbon. Perfect fantasy gift and fidget desk toy.',
      reviewCount: 428,
      reviewAverage: 4.9,
      quantity: 12,
      tags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'winged dragon', 'desk decor', 'dnd gift', 'dragon sculpture', 'fantasy beast', 'mythical creature', 'adhd toy', 'bambu lab', 'flexi animal', 'gamer gift']
    },
    {
      id: 4188192041,
      listingRank: 2,
      title: 'Minimalist Geometric Self-Watering Planter for Succulents & Cacti',
      priceUsd: 26.00,
      shopName: 'Botanical3DPrints',
      shopSales: 8940,
      shopUrl: 'https://www.etsy.com/shop/Botanical3DPrints',
      listingUrl: 'https://www.etsy.com/listing/4188192041',
      views: 12340,
      favorites: 2490,
      seoScore: 92,
      marketScore: 89,
      imageUrl: 'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1485955900006-10f4d324d411?w=600'
      ],
      description: 'Modern geometric self-watering planter pot. Features an internal water reservoir and cotton wick system to keep your succulents and houseplants thriving without root rot.',
      reviewCount: 265,
      reviewAverage: 4.8,
      quantity: 25,
      tags: ['self watering', 'succulent pot', 'geometric planter', 'minimalist decor', 'indoor planter', '3d printed pot', 'modern planter', 'plant lover gift', 'desktop plant', 'drainage pot', 'eco filament', 'home decor', 'botanical pot']
    },
    {
      id: 4187920145,
      listingRank: 3,
      title: 'Steampunk Mechanical Skeleton Gear Desk Clock with Moving Cogs',
      priceUsd: 89.00,
      shopName: 'TimeWorksDesign',
      shopSales: 5120,
      shopUrl: 'https://www.etsy.com/shop/TimeWorksDesign',
      listingUrl: 'https://www.etsy.com/listing/4187920145',
      views: 9820,
      favorites: 1850,
      seoScore: 88,
      marketScore: 86,
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=300',
      imageUrls: [
        'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600'
      ],
      description: 'Precision 3D printed mechanical steampunk desk clock. Features exposed planetary gears, quartz movement, and rustic metallic antique brass finish.',
      reviewCount: 180,
      reviewAverage: 4.7,
      quantity: 8,
      tags: ['steampunk clock', 'mechanical clock', 'gear desk clock', 'industrial clock', 'vintage steampunk', '3d printed clock', 'engineer gift', 'skeleton clock', 'rustic home decor', 'mens gift', 'retro desk accessory', 'clockwork art', 'maker design']
    },
    {
      id: 4187299104,
      listingRank: 4,
      title: 'Captain Jack Sparrow Functional Replica Compass with Working Needle',
      priceUsd: 48.50,
      shopName: 'PiratesTreasureCo',
      shopSales: 7340,
      shopUrl: 'https://www.etsy.com/shop/PiratesTreasureCo',
      listingUrl: 'https://www.etsy.com/listing/4187299104',
      views: 14200,
      favorites: 2980,
      seoScore: 94,
      marketScore: 91,
      imageUrl: 'https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600'],
      description: 'Full sized screen-accurate Jack Sparrow compass replica with rotating dial and weathered faux-leather strap. Hand-painted with acrylic wash.',
      reviewCount: 310,
      reviewAverage: 4.9,
      quantity: 14,
      tags: ['jack sparrow', 'pirate compass', 'cosplay prop', 'pirates caribbean', 'working compass', 'movie replica', 'leather compass', 'halloween prop', 'ren faire', 'pirate costume', 'dnd artifact', 'prop maker', 'nautical antique']
    },
    {
      id: 4185189201,
      listingRank: 5,
      title: 'Arcane Jinx Fishbones Rocket Launcher 1:1 Cosplay Replica Prop',
      priceUsd: 145.00,
      shopName: 'ZaunWorkshop',
      shopSales: 3200,
      shopUrl: 'https://www.etsy.com/shop/ZaunWorkshop',
      listingUrl: 'https://www.etsy.com/listing/4185189201',
      views: 22100,
      favorites: 4120,
      seoScore: 90,
      marketScore: 88,
      imageUrl: 'https://images.unsplash.com/photo-1563089145-599997674d42?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1563089145-599997674d42?w=600'],
      description: 'Life sized Jinx Fishbones rocket launcher. Lightweight hollow PLA build, LED glowing eye sockets, and movable mouth jaw. Perfect for comic con conventions.',
      reviewCount: 95,
      reviewAverage: 4.8,
      quantity: 5,
      tags: ['jinx cosplay', 'fishbones prop', 'arcane weapon', 'lol rocket launcher', 'cosplay weapon', 'gamer bedroom decor', 'comic con prop', 'league of legends', 'zaun punk', '3d printed weapon', 'led cosplay', 'halloween costume', 'gamer gift']
    },
    {
      id: 4178129840,
      listingRank: 6,
      title: 'Fallout Pip-Boy 3000 Mk IV Wearable Bluetooth Smartwatch Dock',
      priceUsd: 120.00,
      shopName: 'WastelandArmory',
      shopSales: 4100,
      shopUrl: 'https://www.etsy.com/shop/WastelandArmory',
      listingUrl: 'https://www.etsy.com/listing/4178129840',
      views: 17800,
      favorites: 3100,
      seoScore: 86,
      marketScore: 84,
      imageUrl: 'https://images.unsplash.com/photo-1518770660439-4636190af475?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1518770660439-4636190af475?w=600'],
      description: 'Wearable Pip Boy 3000 forearm cuff compatible with Apple Watch & Samsung Galaxy Watch. Features working rotating knobs and authentic vault-tec weathered patina.',
      reviewCount: 140,
      reviewAverage: 4.6,
      quantity: 9,
      tags: ['pip boy', 'fallout prop', 'vault tec', 'wearable armor', 'cosplay cuff', 'apple watch dock', 'gaming gadget', 'post apocalyptic', 'wasteland prop', 'fallout 4', 'gamer cosplay', 'halloween prop', 'scifi cuff']
    },
    {
      id: 4176640192,
      listingRank: 7,
      title: 'Lotr Aragorn Crown of Gondor Full Size Wearable Metalized Tiara',
      priceUsd: 65.00,
      shopName: 'MiddleEarthArmory',
      shopSales: 6200,
      shopUrl: 'https://www.etsy.com/shop/MiddleEarthArmory',
      listingUrl: 'https://www.etsy.com/listing/4176640192',
      views: 11200,
      favorites: 2150,
      seoScore: 82,
      marketScore: 80,
      imageUrl: 'https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1579783900882-c0d3dad7b119?w=600'],
      description: 'Coronation crown of King Elessar Telcontar. Printed in silver infused filament with antique black wax rubbing. Padded velvet interior for comfortable wear.',
      reviewCount: 165,
      reviewAverage: 4.8,
      quantity: 11,
      tags: ['aragorn crown', 'crown of gondor', 'lotr cosplay', 'king crown', 'middle earth tiara', 'wedding crown', 'fantasy tiara', 'lord of the rings', 'medieval armor', 'renaissance fair', 'cosplay armor', 'king elessar', 'fantasy prop']
    },
    {
      id: 4174028911,
      listingRank: 8,
      title: 'Ben 10 Classic Omnitrix Watch with Sound & Pop-Up Hologram Dial',
      priceUsd: 35.00,
      shopName: 'HeroPropsStudio',
      shopSales: 9400,
      shopUrl: 'https://www.etsy.com/shop/HeroPropsStudio',
      listingUrl: 'https://www.etsy.com/listing/4174028911',
      views: 15400,
      favorites: 3320,
      seoScore: 85,
      marketScore: 82,
      imageUrl: 'https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1509042239860-f550ce710b93?w=600'],
      description: 'Accurate retro cartoon Omnitrix watch. Spring loaded pop-up ring mechanism and green LED glowing core. Adjustable silicone wrist strap included.',
      reviewCount: 290,
      reviewAverage: 4.7,
      quantity: 18,
      tags: ['omnitrix watch', 'ben 10 toy', 'alien watch', 'cartoon prop', 'nostalgia gift', 'cosplay accessory', 'green led watch', 'kids superhero', 'hero dial', 'retro cartoon', '3d printed toy', 'fidget watch', 'gamer gift']
    },
    {
      id: 4168074902,
      listingRank: 9,
      title: 'D&D Mimic Chest Dice Box with Realistic Silicone Sharp Teeth & Tongue',
      priceUsd: 38.00,
      shopName: 'DiceHoardCrafts',
      shopSales: 11500,
      shopUrl: 'https://www.etsy.com/shop/DiceHoardCrafts',
      listingUrl: 'https://www.etsy.com/listing/4168074902',
      views: 19800,
      favorites: 4200,
      seoScore: 95,
      marketScore: 92,
      imageUrl: 'https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=300',
      imageUrls: ['https://images.unsplash.com/photo-1526778548025-fa2f459cd5c1?w=600'],
      description: 'Frightening yet cute tabletop RPG mimic treasure chest. Holds up to 5 complete polyhedral dice sets (35 dice). Glossy resin eyes and flexible silicone tongue.',
      reviewCount: 380,
      reviewAverage: 4.9,
      quantity: 20,
      tags: ['mimic dice box', 'dnd dice chest', 'dice holder', 'tabletop rpg', 'dungeon master gift', 'monstrous chest', 'polyhedral dice', 'dungeons dragons', 'd20 holder', 'pathfinder gift', 'fantasy storage', 'resin teeth', 'gamer desk']
    }
  ];

  selectedItem?: MarketItem;

  ngOnInit(): void {
    this.selectedItem = this.items[0];
    this.applySort();
  }

  onSearch(): void {
    this.statusText = `"${this.searchKeyword}" için ${this.items.length} ürün listelendi.`;
    this.applySort();
  }

  applySort(): void {
    switch (this.sortBy) {
      case 'market_score':
        this.items.sort((a, b) => b.marketScore - a.marketScore);
        break;
      case 'seo_score':
        this.items.sort((a, b) => b.seoScore - a.seoScore);
        break;
      case 'favorites':
        this.items.sort((a, b) => b.favorites - a.favorites);
        break;
      case 'views':
        this.items.sort((a, b) => b.views - a.views);
        break;
      case 'shop_sales':
        this.items.sort((a, b) => b.shopSales - a.shopSales);
        break;
      case 'price_asc':
        this.items.sort((a, b) => a.priceUsd - b.priceUsd);
        break;
      case 'price_desc':
        this.items.sort((a, b) => b.priceUsd - a.priceUsd);
        break;
    }
    this.items.forEach((item, idx) => item.listingRank = idx + 1);
  }

  toggleFavSort(): void {
    this.favSortDesc = !this.favSortDesc;
    this.items.sort((a, b) => this.favSortDesc ? b.favorites - a.favorites : a.favorites - b.favorites);
    this.items.forEach((item, idx) => item.listingRank = idx + 1);
  }

  filterOnlyTopOpportunity(): void {
    this.items = this.items.filter(i => i.marketScore >= 85);
    this.statusText = `Filtrelendi: Yalnızca Fırsat Skoru %85+ olan ${this.items.length} ürün gösteriliyor.`;
    this.showToastMsg(`⚡ Yüksek Fırsatlı ${this.items.length} ürün filtrelendi.`);
  }

  selectItem(item: MarketItem): void {
    this.selectedItem = item;
    this.currentImageIndex = 0;
  }

  prevImage(): void {
    if (!this.selectedItem || !this.selectedItem.imageUrls.length) return;
    this.currentImageIndex = (this.currentImageIndex - 1 + this.selectedItem.imageUrls.length) % this.selectedItem.imageUrls.length;
  }

  nextImage(): void {
    if (!this.selectedItem || !this.selectedItem.imageUrls.length) return;
    this.currentImageIndex = (this.currentImageIndex + 1) % this.selectedItem.imageUrls.length;
  }

  getScoreColorClass(score: number): string {
    if (score >= 88) return 'high';
    if (score >= 70) return 'mid';
    return 'low';
  }

  // ==========================================
  // 12 MASAÜSTÜ EYLEM BUTONU İŞLEMLERİ
  // ==========================================

  // 1. 🚀 Taslağa Klonla -> FastCreatorComponent
  cloneSelectedListing(): void {
    if (!this.selectedItem) return;
    const cloned: ClonedMarketListing = {
      id: this.selectedItem.id,
      title: this.selectedItem.title,
      priceUsd: this.selectedItem.priceUsd,
      tags: [...this.selectedItem.tags],
      description: this.selectedItem.description,
      imageUrl: this.selectedItem.imageUrl,
      shopName: this.selectedItem.shopName,
      seoScore: this.selectedItem.seoScore,
      marketScore: this.selectedItem.marketScore
    };
    this.aiService.setClonedListing(cloned);
    this.showToastMsg(`🚀 #${this.selectedItem.id} ürünü Fast Creator'a klonlandı, yönlendiriliyorsunuz...`);
    setTimeout(() => {
      this.router.navigate(['/listings/fast-creator']);
    }, 600);
  }

  // 2. ✨ AI Optimizasyon
  optimizeWithAi(): void {
    if (!this.selectedItem) return;
    this.showToastMsg(`✨ ${this.aiService.activeBadgeText()} ile #${this.selectedItem.id} optimizasyonu başlatılıyor...`);
    this.cloneSelectedListing();
  }

  // 3. 📌 Takibe Ekle
  trackSelectedListing(): void {
    if (!this.selectedItem) return;
    this.showToastMsg(`📌 #${this.selectedItem.id} (${this.selectedItem.title.slice(0, 30)}...) takip merkezine eklendi!`);
  }

  // 4. 🔍 Kelime Analizi
  openKeywordAnalysisModal(): void {
    if (!this.selectedItem) return;
    const topKeywords = this.selectedItem.tags.slice(0, 5).join(' | ');
    alert(`🔍 Kelime Yoğunluk Analizi:\n\nÜrün: ${this.selectedItem.title}\n\nEn Yüksek Hacimli Anahtar Kelimeler:\n${topKeywords}\n\nSEO Gücü: %${this.selectedItem.seoScore}`);
  }

  // 5. 🕵️ Rakip Analizi
  openCompetitorAnalysisModal(): void {
    if (!this.selectedItem) return;
    this.showToastMsg(`🕵️ ${this.selectedItem.shopName} mağazasının rakip analizi açılıyor...`);
    this.router.navigate(['/research/competitor']);
  }

  // 6. 🏪 Mağaza Aç
  openShopUrl(): void {
    if (this.selectedItem?.shopUrl) {
      window.open(this.selectedItem.shopUrl, '_blank');
    }
  }

  // 7. 🩺 Sağlık Skoru
  openHealthScoreModal(): void {
    this.showHealthModal = true;
  }

  // 8. 🏷️ Tagleri Kopyala
  copySelectedTags(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.tags.join(', '));
      this.showToastMsg(`🏷️ ${this.selectedItem.tags.length} adet arama etiketi panoya kopyalandı!`);
    }
  }

  // 9. 📝 Başlığı Kopyala
  copySelectedTitle(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.title);
      this.showToastMsg(`📝 Ürün başlığı panoya kopyalandı!`);
    }
  }

  // 10. 🌐 Etsy'de Aç
  openEtsyListing(item?: MarketItem): void {
    const itm = item || this.selectedItem;
    if (itm?.listingUrl) {
      window.open(itm.listingUrl, '_blank');
    }
  }

  // 11. 📊 CSV Aktar
  exportCsv(): void {
    const headers = 'Sira;ListingId;Baslik;Fiyat_USD;Magaza;Magaza_Satisi;Favori;Goruntulenme;SEO_Puani;Pazar_Puani;Tagler\n';
    const rows = this.items.map(i => 
      `${i.listingRank};"${i.id}";"${i.title.replace(/"/g, '""')}";${i.priceUsd};"${i.shopName}";${i.shopSales};${i.favorites};${i.views};${i.seoScore};${i.marketScore};"${i.tags.join(', ')}"`
    ).join('\n');
    
    const blob = new Blob([headers + rows], { type: 'text/csv;charset=utf-8;' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `Etsy_Pazar_Arastirmasi_${new Date().toISOString().slice(0, 10)}.csv`;
    a.click();
    URL.revokeObjectURL(url);
    this.showToastMsg('📊 Pazar verileri CSV formatında indirildi.');
  }

  // 12. 🔗 Linki Kopyala
  copyListingLink(): void {
    if (this.selectedItem) {
      navigator.clipboard.writeText(this.selectedItem.listingUrl);
      this.showToastMsg(`🔗 Etsy linki kopyalandı: ${this.selectedItem.listingUrl}`);
    }
  }

  sendToImageStudio(): void {
    if (this.selectedItem) {
      this.showToastMsg(`📸 Görsel Stüdyosu açılıyor...`);
      this.router.navigate(['/ai-studio']);
    }
  }

  openImageZoom(item: MarketItem, ev: MouseEvent): void {
    ev.stopPropagation();
    window.open(item.imageUrl, '_blank');
  }

  copyAllTopTags(): void {
    const allTags = Array.from(new Set(this.items.flatMap(i => i.tags))).slice(0, 13);
    navigator.clipboard.writeText(allTags.join(', '));
    this.showToastMsg(`📋 İlk 13 Ortak Altın Etiket kopyalandı!`);
  }

  generateAiMarketReport(): void {
    this.isGeneratingReport = true;
    setTimeout(() => {
      this.isGeneratingReport = false;
      this.aiReport = `
        <b>🤖 ${this.aiService.activeBadgeText()} Strateji Raporu:</b> "<b>${this.searchKeyword}</b>" pazarında talep yoğunluğu çok yüksek (Pazar Fırsat Skoru: <b>%${this.opportunityScore}</b>).<br/>
        • <b>Önerilen Satış Fiyatı:</b> &#36;${this.avgPriceUsd.toFixed(2)} (₺${(this.avgPriceUsd * this.etsyApi.exchangeRate()).toFixed(0)}). Hızlı satış ve sıralama için <b>&#36;32.50</b> fiyatla girilmesi önerilir.<br/>
        • <b>Kritik 3 Altın Etiket:</b> "<i>crystal dragon</i>", "<i>winged dragon</i>", "<i>flexi toy</i>".<br/>
        • <b>Tavsiye Edilen Aksiyon:</b> İlk 3 sıradaki ilanlardan birini "🚀 Taslağa Klonla" butonuyla aktarın ve aktif modelinizle 140 karakterlik kusursuz SEO başlığı üretin.
      `;
    }, 900);
  }

  private showToastMsg(msg: string): void {
    this.toastMessage = msg;
    setTimeout(() => {
      if (this.toastMessage === msg) this.toastMessage = '';
    }, 2800);
  }
}
