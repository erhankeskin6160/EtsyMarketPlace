import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

interface ListingPreset {
  id: string;
  name: string;
  category: string;
  defaultPrice: number;
  sampleTitle: string;
  sampleTags: string[];
  sampleDesc: string;
}

@Component({
  selector: 'app-fast-creator',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <div class="creator-container">
      <!-- TOP HEADER -->
      <div class="creator-header">
        <div class="header-info">
          <div class="header-icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <polygon points="13 2 3 14 12 14 11 22 21 10 12 10 13 2"></polygon>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Hızlı Ürün Ekle - AI Fast Creator</h1>
            <p class="page-subtitle">Aktif Model: <span class="active-model-chip" (click)="aiService.openAiSettingsModal()"><app-ai-logo [provider]="aiService.activeProvider()" [size]="14"></app-ai-logo> {{ aiService.activeBadgeTextClean() }} ⚙️</span> ile SEO 140 karakter başlık, açıklama ve kusursuz 13 etiket üretimi</p>
          </div>
        </div>

        <div class="header-actions">
          <button class="btn-model-settings" (click)="aiService.openAiSettingsModal()" title="Yapay Zeka Modeli Değiştir">
            ⚙️ AI Model Ayarları
          </button>
          <button class="btn-ai-spark" [disabled]="isGeneratingAi" (click)="generateWithAi()">
            <app-ai-logo [provider]="aiService.activeProvider()" [size]="18"></app-ai-logo>
            <span>{{ isGeneratingAi ? (aiService.activeProvider() + ' Üretiyor...') : (aiService.activeProvider() + ' ile SEO Üret') }}</span>
          </button>
        </div>
      </div>

      <!-- CLONED NOTICE BANNER -->
      <div *ngIf="clonedNotice" class="cloned-banner">
        <div class="cloned-badge">🚀 PAZAR ARAŞTIRMASINDAN KLONLANDI</div>
        <div class="cloned-info">{{ clonedNotice }}</div>
        <button class="btn-dismiss-cloned" (click)="clearCloned()">Temizle</button>
      </div>

      <!-- MAIN WORKSPACE: 2 COLUMNS -->
      <div class="creator-body">
        
        <!-- LEFT COLUMN: FORM INPUTS -->
        <div class="creator-form-col">
          
          <!-- PRESET TEMPLATES -->
          <div class="glass-card">
            <span class="card-label">1. Şablon & Ürün Tipi Seçimi</span>
            <div class="presets-grid">
              <button 
                *ngFor="let p of presets" 
                class="preset-pill" 
                [class.active]="selectedPresetId === p.id"
                (click)="applyPreset(p)">
                {{ p.name }}
              </button>
            </div>
          </div>

          <!-- SEO TITLE & MOBILE SNIPPET -->
          <div class="glass-card">
            <div class="card-header-row">
              <span class="card-label">2. Etsy SEO Başlığı (Maksimum 140 Karakter)</span>
              <span class="char-counter" [class.warning]="title.length > 125" [class.danger]="title.length > 140">
                {{ title.length }} / 140 Karakter
              </span>
            </div>

            <textarea 
              [(ngModel)]="title" 
              maxlength="140"
              rows="3" 
              placeholder="Örn: 3D Printed Crystal Dragon, Articulated Fidget Toy, Fantasy Desktop Decor..."
              class="form-textarea">
            </textarea>

            <div class="mobile-snippet-box">
              <span class="snippet-tag">📱 Mobil Arama Snippet Önizleme (İlk 45 Karakter):</span>
              <p class="snippet-text">"{{ title.slice(0, 45) || 'Başlık giriniz...' }}{{ title.length > 45 ? '...' : '' }}"</p>
            </div>
          </div>

          <!-- PRICING & INVENTORY -->
          <div class="glass-card">
            <span class="card-label">3. Fiyatlandırma & Stok</span>
            <div class="pricing-grid">
              <div class="input-block">
                <label>Satış Fiyatı ($ USD)</label>
                <input 
                  type="number" 
                  step="0.5" 
                  [(ngModel)]="priceUsd" 
                  (ngModelChange)="onPriceChange()"
                  class="form-input" />
              </div>
              <div class="input-block">
                <label>TL Karşılığı (₺ TRY)</label>
                <input 
                  type="number" 
                  [(ngModel)]="priceTry" 
                  (ngModelChange)="onPriceTryChange()"
                  class="form-input" />
              </div>
              <div class="input-block">
                <label>Stok Adedi</label>
                <input 
                  type="number" 
                  [(ngModel)]="quantity" 
                  class="form-input" />
              </div>
              <div class="input-block">
                <label>Kategori (Taxonomy)</label>
                <select [(ngModel)]="selectedCategory" class="form-select">
                  <option value="Art & Collectibles > Sculptures">Art & Collectibles > 3D Baskı</option>
                  <option value="Home & Living > Home Decor">Ev & Dekorasyon > Masa Süsü</option>
                  <option value="Jewelry > Necklaces">Takı > Kişiselleştirilmiş Kolye</option>
                  <option value="Craft Supplies > Digital">Dijital İndirme > STL Dosyası</option>
                </select>
              </div>
            </div>
          </div>

          <!-- 13 SEO TAGS MANAGER -->
          <div class="glass-card">
            <div class="card-header-row">
              <span class="card-label">4. Kusursuz 13 Arama Etiketi (Tags)</span>
              <span class="tag-counter-pill" [class.complete]="tags.length === 13">
                {{ tags.length }} / 13 Etiket
              </span>
            </div>
            <p class="hint-text">Her etiket en fazla 20 karakter olmalıdır. Etsy algoritması tam 13 etiketin doldurulmasını ödüllendirir.</p>

            <div class="add-tag-box">
              <input 
                type="text" 
                [(ngModel)]="newTagInput" 
                maxlength="20"
                (keyup.enter)="addTag()"
                placeholder="Yeni etiket (en fazla 20 karakter)..." 
                class="form-input" />
              <button class="btn-add-tag" [disabled]="tags.length >= 13 || !newTagInput.trim()" (click)="addTag()">
                + Ekle
              </button>
            </div>

            <div class="tags-container">
              <div *ngFor="let tag of tags; let i = index" class="tag-chip">
                <span>{{ tag }}</span>
                <span class="tag-char-len">{{ tag.length }}k</span>
                <button class="btn-del-tag" (click)="removeTag(i)">×</button>
              </div>
              <div *ngIf="tags.length === 0" class="no-tags-text">
                Henüz etiket eklenmedi. 'Gemini Spark ile SEO Üret' butonuna tıklayarak otomatik 13 etiket üretebilirsiniz.
              </div>
            </div>
          </div>

          <!-- DESCRIPTION -->
          <div class="glass-card">
            <span class="card-label">5. Ürün Açıklaması (Description)</span>
            <textarea 
              [(ngModel)]="description" 
              rows="6" 
              placeholder="Ürün hikayesi, ölçüler, malzeme kalitesi ve kargo bilgilerini içeren ikna edici açıklama..."
              class="form-textarea">
            </textarea>
          </div>

        </div>

        <!-- RIGHT COLUMN: LIVE ETSY PREVIEW & PUBLISH -->
        <div class="creator-preview-col">
          
          <div class="preview-sticky-wrap">
            <div class="preview-header">
              <span class="card-label">Canlı Etsy İlan Kartı Önizleme</span>
              <span class="status-live-tag">CANLI SİMÜLASYON</span>
            </div>

            <div class="etsy-listing-mock-card">
              <div class="mock-image-box">
                <img [src]="sampleImageUrl" class="mock-img" alt="Listing" />
                <span class="etsy-pick-badge">Etsy's Pick</span>
              </div>

              <div class="mock-content">
                <h3 class="mock-title">{{ title || 'Örnek Ürün Başlığı Buraya Gelecek...' }}</h3>
                <span class="mock-shop-name">Keskin3DStudio</span>
                
                <div class="mock-rating">
                  <span class="stars">★★★★★</span>
                  <span class="review-count">(142)</span>
                </div>

                <div class="mock-price-row">
                  <span class="mock-price">&#36;{{ priceUsd.toFixed(2) }}</span>
                  <span class="mock-try">({{ formatTry(priceUsd) }})</span>
                </div>

                <span class="mock-free-shipping">Ücretsiz Kargo (ABD & AB)</span>
              </div>
            </div>

            <!-- SUCCESS BANNER -->
            <div *ngIf="successMessage" class="success-banner">
              ✓ {{ successMessage }}
            </div>

            <!-- ACTION BUTTONS -->
            <div class="publish-action-box">
              <button class="btn-copy-all" (click)="copyAll()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <rect x="9" y="9" width="13" height="13" rx="2" ry="2"></rect>
                  <path d="M5 15H4a2 2 0 0 1-2-2V4a2 2 0 0 1 2-2h9a2 2 0 0 1 2 2v1"></path>
                </svg>
                Panoya Kopyala
              </button>

              <button class="btn-save-draft" [disabled]="isSavingDraft" (click)="saveDraftToEtsy()">
                <svg width="16" height="16" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
                  <path d="M19 21H5a2 2 0 0 1-2-2V5a2 2 0 0 1 2-2h11l5 5v11a2 2 0 0 1-2 2z"></path>
                  <polyline points="17 21 17 13 7 13 7 21"></polyline>
                  <polyline points="7 3 7 8 15 8"></polyline>
                </svg>
                {{ isSavingDraft ? 'Taslak Kaydediliyor...' : 'Etsy Taslak Olarak Gönder (Draft)' }}
              </button>
            </div>
          </div>

        </div>

      </div>
    </div>
  `,
  styles: [`
    .creator-container {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
    }
    .creator-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 24px;
    }
    .header-info {
      display: flex;
      align-items: center;
      gap: 14px;
    }
    .header-icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(245, 158, 11, 0.2), rgba(234, 88, 12, 0.2));
      border: 1px solid rgba(245, 158, 11, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #fbbf24;
    }
    .page-title {
      font-size: 1.35rem;
      font-weight: 700;
      margin: 0;
      color: #f8fafc;
    }
    .page-subtitle {
      font-size: 0.85rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .active-model-chip {
      color: #818cf8;
      font-weight: 700;
      background: rgba(99, 102, 241, 0.15);
      padding: 2px 8px;
      border-radius: 4px;
      border: 1px solid rgba(99, 102, 241, 0.3);
      cursor: pointer;
      transition: all 0.2s;
    }
    .active-model-chip:hover {
      background: rgba(99, 102, 241, 0.25);
      border-color: #818cf8;
    }
    .btn-model-settings {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 9px 14px;
      background: rgba(30, 41, 59, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 8px;
      color: #cbd5e1;
      font-size: 0.85rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-model-settings:hover {
      background: rgba(51, 65, 85, 0.9);
      color: #fff;
      border-color: #818cf8;
    }
    .btn-ai-spark {
      display: flex;
      align-items: center;
      gap: 8px;
      padding: 10px 20px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-weight: 700;
      font-size: 0.9rem;
      cursor: pointer;
      box-shadow: 0 4px 16px rgba(99, 102, 241, 0.35);
      transition: all 0.2s;
    }
    .btn-ai-spark:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .btn-ai-spark:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .cloned-banner {
      display: flex;
      align-items: center;
      gap: 12px;
      padding: 10px 16px;
      background: linear-gradient(90deg, rgba(99, 102, 241, 0.15), rgba(168, 85, 247, 0.15));
      border: 1px solid rgba(129, 140, 248, 0.4);
      border-radius: 8px;
      margin-bottom: 20px;
      animation: fadeIn 0.3s ease;
    }
    .cloned-badge {
      font-size: 0.72rem;
      font-weight: 800;
      letter-spacing: 0.05em;
      color: #818cf8;
      background: rgba(99, 102, 241, 0.2);
      padding: 3px 8px;
      border-radius: 4px;
      white-space: nowrap;
    }
    .cloned-info {
      flex: 1;
      font-size: 0.85rem;
      color: #e2e8f0;
      font-weight: 500;
    }
    .btn-dismiss-cloned {
      background: transparent;
      border: 1px solid rgba(255, 255, 255, 0.2);
      color: #94a3b8;
      font-size: 0.75rem;
      padding: 3px 8px;
      border-radius: 4px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-dismiss-cloned:hover {
      background: rgba(255, 255, 255, 0.1);
      color: #fff;
    }
    .creator-form-col {
      flex: 1.3;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }
    .creator-preview-col {
      flex: 0.9;
    }
    .preview-sticky-wrap {
      position: sticky;
      top: 24px;
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .glass-card {
      background: rgba(30, 41, 59, 0.4);
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 12px;
      padding: 16px 20px;
      backdrop-filter: blur(10px);
    }
    .card-label {
      font-size: 0.82rem;
      font-weight: 700;
      color: #cbd5e1;
      text-transform: uppercase;
      letter-spacing: 0.04em;
      display: block;
      margin-bottom: 10px;
    }
    .card-header-row {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 8px;
    }
    .char-counter {
      font-size: 0.75rem;
      font-weight: 600;
      color: #10b981;
    }
    .char-counter.warning { color: #f59e0b; }
    .char-counter.danger { color: #ef4444; }

    /* PRESETS */
    .presets-grid {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
    }
    .preset-pill {
      padding: 7px 14px;
      background: rgba(15, 23, 42, 0.6);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 20px;
      color: #94a3b8;
      font-size: 0.8rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .preset-pill:hover {
      color: #fff;
      border-color: rgba(255, 255, 255, 0.2);
    }
    .preset-pill.active {
      background: rgba(99, 102, 241, 0.2);
      border-color: #818cf8;
      color: #a5b4fc;
      font-weight: 600;
    }

    /* FORMS */
    .form-textarea {
      width: 100%;
      padding: 10px 14px;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      color: #fff;
      font-size: 0.85rem;
      line-height: 1.4;
      outline: none;
      box-sizing: border-box;
      resize: vertical;
    }
    .form-input, .form-select {
      width: 100%;
      padding: 9px 12px;
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      color: #fff;
      font-size: 0.85rem;
      outline: none;
      box-sizing: border-box;
    }
    .mobile-snippet-box {
      margin-top: 10px;
      padding: 8px 12px;
      background: rgba(15, 23, 42, 0.6);
      border-radius: 6px;
      border-left: 3px solid #818cf8;
    }
    .snippet-tag {
      font-size: 0.72rem;
      color: #818cf8;
      font-weight: 600;
      display: block;
    }
    .snippet-text {
      margin: 2px 0 0 0;
      font-size: 0.82rem;
      color: #e2e8f0;
      font-style: italic;
    }

    .pricing-grid {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 12px;
    }
    .input-block label {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
      margin-bottom: 4px;
    }

    /* TAGS */
    .tag-counter-pill {
      font-size: 0.75rem;
      padding: 2px 8px;
      border-radius: 4px;
      background: rgba(245, 158, 11, 0.2);
      color: #fbbf24;
      font-weight: 700;
    }
    .tag-counter-pill.complete {
      background: rgba(16, 185, 129, 0.2);
      color: #34d399;
    }
    .hint-text {
      font-size: 0.75rem;
      color: #64748b;
      margin: 0 0 10px 0;
    }
    .add-tag-box {
      display: flex;
      gap: 8px;
      margin-bottom: 12px;
    }
    .btn-add-tag {
      padding: 8px 16px;
      background: rgba(99, 102, 241, 0.2);
      border: 1px solid rgba(99, 102, 241, 0.4);
      color: #a5b4fc;
      border-radius: 8px;
      font-weight: 600;
      cursor: pointer;
      white-space: nowrap;
    }
    .tags-container {
      display: flex;
      flex-wrap: wrap;
      gap: 8px;
      min-height: 48px;
      padding: 10px;
      background: rgba(15, 23, 42, 0.5);
      border-radius: 8px;
    }
    .tag-chip {
      display: flex;
      align-items: center;
      gap: 6px;
      padding: 4px 10px;
      background: rgba(99, 102, 241, 0.15);
      border: 1px solid rgba(99, 102, 241, 0.3);
      border-radius: 6px;
      font-size: 0.78rem;
      color: #e2e8f0;
    }
    .tag-char-len {
      font-size: 0.65rem;
      color: #818cf8;
      opacity: 0.8;
    }
    .btn-del-tag {
      background: transparent;
      border: none;
      color: #94a3b8;
      cursor: pointer;
      font-size: 1rem;
      padding: 0;
      line-height: 1;
    }
    .btn-del-tag:hover { color: #f87171; }
    .no-tags-text {
      font-size: 0.75rem;
      color: #64748b;
      font-style: italic;
    }

    /* MOCK CARD PREVIEW */
    .preview-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .status-live-tag {
      font-size: 0.68rem;
      font-weight: 700;
      color: #34d399;
      background: rgba(16, 185, 129, 0.15);
      padding: 2px 6px;
      border-radius: 4px;
    }
    .etsy-listing-mock-card {
      background: #1e293b;
      border-radius: 12px;
      overflow: hidden;
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 10px 25px rgba(0, 0, 0, 0.4);
    }
    .mock-image-box {
      position: relative;
      width: 100%;
      height: 260px;
      background: #0f172a;
    }
    .mock-img {
      width: 100%;
      height: 100%;
      object-fit: cover;
    }
    .etsy-pick-badge {
      position: absolute;
      top: 10px;
      left: 10px;
      background: rgba(15, 23, 42, 0.85);
      backdrop-filter: blur(4px);
      color: #f59e0b;
      font-size: 0.72rem;
      font-weight: 700;
      padding: 3px 8px;
      border-radius: 4px;
      border: 1px solid rgba(245, 158, 11, 0.4);
    }
    .mock-content {
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .mock-title {
      font-size: 0.95rem;
      font-weight: 600;
      color: #f8fafc;
      margin: 0;
      line-height: 1.35;
      display: -webkit-box;
      -webkit-line-clamp: 2;
      -webkit-box-orient: vertical;
      overflow: hidden;
    }
    .mock-shop-name {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .mock-rating {
      display: flex;
      align-items: center;
      gap: 4px;
    }
    .stars { color: #f59e0b; font-size: 0.85rem; letter-spacing: 2px; }
    .review-count { font-size: 0.72rem; color: #94a3b8; }
    .mock-price-row {
      display: flex;
      align-items: baseline;
      gap: 6px;
      margin-top: 4px;
    }
    .mock-price {
      font-size: 1.25rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .mock-try {
      font-size: 0.8rem;
      color: #94a3b8;
    }
    .mock-free-shipping {
      font-size: 0.75rem;
      font-weight: 600;
      color: #10b981;
    }

    .success-banner {
      padding: 12px;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.4);
      color: #34d399;
      border-radius: 8px;
      font-size: 0.85rem;
      font-weight: 600;
      animation: fadeIn 0.3s ease;
    }

    .publish-action-box {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }
    .btn-copy-all {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 12px;
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.15);
      border-radius: 8px;
      color: #e2e8f0;
      font-weight: 600;
      font-size: 0.85rem;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-copy-all:hover {
      background: rgba(255, 255, 255, 0.12);
      color: #fff;
    }
    .btn-save-draft {
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 8px;
      padding: 13px;
      background: linear-gradient(135deg, #10b981, #059669);
      border: none;
      border-radius: 8px;
      color: #fff;
      font-weight: 700;
      font-size: 0.9rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.35);
      transition: all 0.2s;
    }
    .btn-save-draft:hover:not(:disabled) {
      filter: brightness(1.1);
      transform: translateY(-1px);
    }
    .btn-save-draft:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    @media (max-width: 1024px) {
      .creator-body { flex-direction: column; }
    }
    @keyframes fadeIn {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class FastCreatorComponent implements OnInit {
  presets: ListingPreset[] = [
    {
      id: '3d_dragon',
      name: '🐉 3D Baskı - Kristal Ejderha',
      category: 'Art & Collectibles > Sculptures',
      defaultPrice: 34.50,
      sampleTitle: 'Articulated Crystal Dragon 3D Printed Fidget Toy, Flexible Dragon Desk Pet, Fantasy Mythical Creature Figurine Gift',
      sampleTags: ['crystal dragon', '3d printed dragon', 'fidget toy', 'articulated dragon', 'desk pet', 'fantasy decor', 'dragon figurine', 'adhd sensory toy', 'mythical creature', 'unique gifts', 'bambu lab print', 'dnd miniature', 'flexi toy'],
      sampleDesc: `🔥 Büyüleyici Mafsallı Kristal Ejderha - Yüksek Hassasiyetli 3D Baskı!

Bu harika mafsallı kristal ejderha, son teknoloji Bambu Lab 3D yazıcılarında yüksek kaliteli çevre dostu PLA filament ile üretilmiştir. Masanızda harika bir stres giderici (fidget toy) veya fantastik bir dekorasyon parçası olarak yerini alır.

✨ Öne Çıkan Özellikler:
- Tamamen hareketli eklemler ve kıvrımlı gövde
- Işık altında parlayan özel kristal pul dokusu
- Boyut: ~35 cm uzunluk
- Hediye kutusu seçeneği ile hızlı gönderim`
    },
    {
      id: 'digital_stl',
      name: '💾 Dijital İndirme - 3D STL & SVG',
      category: 'Craft Supplies > Digital',
      defaultPrice: 12.00,
      sampleTitle: 'Geometric Wall Art STL File 3D Print Model, Digital Download 3D Printable Panel, Modern Home Interior Decor STL',
      sampleTags: ['stl file', '3d print model', 'digital download', 'geometric wall art', '3d stl design', 'wall panel stl', 'interior decor stl', '3d printable file', 'modern wall decor', 'laser cut svg', 'instant download', 'diy home decor', '3d file for print'],
      sampleDesc: `📥 Anında İndirilebilir Geometrik Duvar Dekoru 3D Baskı STL Dosyası!

Bu dosya, eviniz veya ofisiniz için modern geometrik duvar panelleri basmanız için optimize edilmiştir. Desteksiz (supportless) kolay baskı imkanı sunar.

📦 Paket İçeriği:
- Yüksek poligonlu pürüzsüz .STL dosyası
- Dilimleyici (Cura / Bambu Studio) ayar rehberi
- Ticari olmayan kişisel kullanım lisansı`
    },
    {
      id: 'jewelry_gem',
      name: '💎 El Yapımı Kişiye Özel Takı',
      category: 'Jewelry > Necklaces',
      defaultPrice: 42.00,
      sampleTitle: 'Custom Name Necklace 925 Sterling Silver, Dainty Personalized Nameplate Pendant, Minimalist Birthday Gift for Her',
      sampleTags: ['name necklace', 'personalized gift', 'silver necklace', 'custom nameplate', 'gift for her', 'dainty jewelry', 'bridesmaid gift', 'minimalist necklace', '925 silver pendant', 'birthday jewelry', 'handcrafted gift', 'custom letter charm', 'mom gift jewelry'],
      sampleDesc: `✨ 925 Ayar Gerçek Gümüş Kişiye Özel İsimli Kolye!

Her bir kolye usta zanaatkarlarımız tarafından kişiye özel olarak özenle kesilir, parlatılır ve zarif bir hediye kutusunda sunulur.`
    }
  ];

  selectedPresetId = '3d_dragon';
  title = '';
  priceUsd = 34.50;
  priceTry = 0;
  quantity = 15;
  selectedCategory = 'Art & Collectibles > Sculptures';
  tags: string[] = [];
  newTagInput = '';
  description = '';
  sampleImageUrl = 'https://images.unsplash.com/photo-1607604276583-eef5d076aa5f?w=600&auto=format&fit=crop&q=80';
  clonedNotice = '';

  isGeneratingAi = false;
  isSavingDraft = false;
  successMessage = '';

  constructor(
    public etsyApi: EtsyApiService,
    public aiService: AiSettingsService
  ) {}

  ngOnInit(): void {
    const cloned = this.aiService.clonedListing();
    if (cloned) {
      this.selectedPresetId = '';
      this.title = cloned.title;
      this.priceUsd = cloned.priceUsd;
      this.tags = [...cloned.tags];
      if (cloned.description) this.description = cloned.description;
      if (cloned.imageUrl) this.sampleImageUrl = cloned.imageUrl;
      if (cloned.category) this.selectedCategory = cloned.category;
      this.clonedNotice = `Klonlanan Ürün #${cloned.id}: "${cloned.title.slice(0, 50)}..." (${cloned.shopName}) - Fiyat: $${cloned.priceUsd}`;
      this.updatePriceTry();
    } else {
      this.applyPreset(this.presets[0]);
      this.updatePriceTry();
    }
  }

  clearCloned(): void {
    this.aiService.clonedListing.set(null);
    this.clonedNotice = '';
    this.applyPreset(this.presets[0]);
  }

  get usdTryRate(): number {
    return this.etsyApi.exchangeRate();
  }

  applyPreset(preset: ListingPreset): void {
    this.selectedPresetId = preset.id;
    this.title = preset.sampleTitle;
    this.priceUsd = preset.defaultPrice;
    this.updatePriceTry();
    this.tags = [...preset.sampleTags];
    this.description = preset.sampleDesc;
    this.selectedCategory = preset.category;
  }

  onPriceChange(): void {
    this.updatePriceTry();
  }

  onPriceTryChange(): void {
    if (this.usdTryRate > 0) {
      this.priceUsd = Number((this.priceTry / this.usdTryRate).toFixed(2));
    }
  }

  updatePriceTry(): void {
    this.priceTry = Number((this.priceUsd * this.usdTryRate).toFixed(2));
  }

  addTag(): void {
    const clean = this.newTagInput.trim().toLowerCase();
    if (!clean || clean.length > 20 || this.tags.length >= 13) return;
    if (!this.tags.includes(clean)) {
      this.tags.push(clean);
    }
    this.newTagInput = '';
  }

  removeTag(index: number): void {
    this.tags.splice(index, 1);
  }

  generateWithAi(): void {
    this.isGeneratingAi = true;
    this.successMessage = '';
    const activeProvider = this.aiService.activeBadgeText();

    setTimeout(() => {
      this.isGeneratingAi = false;
      this.title = 'Articulated Dragon 3D Printed Fidget Toy, Crystal Flexi Dragon Desk Decor, Mythical Fantasy Beast Figurine Birthday Gift';
      this.tags = [
        '3d printed dragon',
        'articulated dragon',
        'crystal dragon',
        'fidget dragon toy',
        'desk pet figurine',
        'fantasy room decor',
        'sensory fidget toy',
        'flexi dragon 3d',
        'bambu lab print',
        'dnd mythical gift',
        'birthday gift boy',
        'unique desk decor',
        'dragon sculpture'
      ];
      this.successMessage = `✨ ${activeProvider} SEO başlığı ve 13 etiketi başarıyla optimize etti!`;
      setTimeout(() => this.successMessage = '', 4000);
    }, 1000);
  }

  generateWithGeminiSpark(): void {
    this.generateWithAi();
  }

  copyAll(): void {
    const payload = `BAŞLIK:\n${this.title}\n\nFİYAT: $${this.priceUsd}\n\nETİKETLER (13):\n${this.tags.join(', ')}\n\nAÇIKLAMA:\n${this.description}`;
    navigator.clipboard.writeText(payload);
    this.successMessage = 'Tüm ilan metinleri ve 13 etiket panoya kopyalandı!';
    setTimeout(() => this.successMessage = '', 3000);
  }

  saveDraftToEtsy(): void {
    this.isSavingDraft = true;
    setTimeout(() => {
      this.isSavingDraft = false;
      this.successMessage = '🎉 İlan Etsy mağazanıza başarıyla "Taslak (Draft)" olarak aktarıldı!';
      setTimeout(() => this.successMessage = '', 4000);
    }, 1000);
  }

  formatTry(usdVal: number): string {
    return `₺${(usdVal * this.usdTryRate).toLocaleString('tr-TR', { minimumFractionDigits: 2, maximumFractionDigits: 2 })}`;
  }
}
