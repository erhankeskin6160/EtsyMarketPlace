import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { EtsyApiService } from '../../core/services/etsy-api.service';

export interface AuditItem {
  id: number;
  title: string;
  score: number;
  tagsCount: number;
  imageCount: number;
  priceUsd: number;
  views: number;
  issues: string[];
  recommendation: string;
}

@Component({
  selector: 'app-ai-audit',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="audit-view">
      <!-- HEADER -->
      <div class="audit-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"></path>
            </svg>
          </div>
          <div>
            <h1 class="page-title">Mağaza AI Denetimi & İlan Sağlık Raporu</h1>
            <p class="page-subtitle">Etsy algoritmasına göre başlık uzunluğu, 13 etiket, görsel kalitesi ve rekabet denetimi</p>
          </div>
        </div>

        <div class="header-right">
          <button class="btn-primary-audit" (click)="runFullAudit()" [disabled]="isAuditing">
            <span>{{ isAuditing ? 'Mağaza Taranıyor...' : '⚡ Tüm Mağazayı AI ile Denetle' }}</span>
          </button>
        </div>
      </div>

      <!-- HEALTH SCORE & METRICS CARDS -->
      <div class="score-strip">
        <div class="glass-card main-score-card">
          <div class="score-circle-wrap">
            <div class="score-number">{{ overallScore }}</div>
            <div class="score-max">/ 100</div>
          </div>
          <div class="score-details">
            <h3 class="score-title">Mağaza Genel Sağlık Skoru</h3>
            <p class="score-desc">
              Taranan 46 ilandan <b>38'i</b> yüksek performanslı, <b>8'i</b> acil optimizasyon gerektiriyor.
            </p>
            <div class="status-badge-row">
              <span class="badge-good">✓ 13/13 Etiket Uyumu: %82</span>
              <span class="badge-warn">⚠️ Başlık SEO Verimi: %68</span>
            </div>
          </div>
        </div>

        <div class="glass-card metric-sub-card">
          <span class="sub-label">SEO & BAŞLIK DENETİMİ</span>
          <div class="sub-val text-cyan">74 / 100</div>
          <span class="sub-desc">140 karakterlik başlıkların ilk 40 karakterinde hedef kelime yoğunluğu</span>
        </div>

        <div class="glass-card metric-sub-card">
          <span class="sub-label">13 ETİKET KAPSAMI</span>
          <div class="sub-val text-emerald">92 / 100</div>
          <span class="sub-desc">İlanların %91'inde tam 13 etiket kullanılmış, 4 ilanda eksik etiket var</span>
        </div>

        <div class="glass-card metric-sub-card">
          <span class="sub-label">GÖRSEL & EXIF TEMİZLİĞİ</span>
          <div class="sub-val text-purple">100 / 100</div>
          <span class="sub-desc">Tüm görseller 2000x2000px formatında ve Anti-Ban EXIF temizleyici onaylı</span>
        </div>
      </div>

      <!-- AUDITED LISTINGS TABLE & AI ADVICE -->
      <div class="audit-content-grid">
        <div class="glass-card list-card">
          <div class="card-head">
            <span class="card-title">Denetlenen İlanlar & Skorları ({{ auditItems.length }} İlan)</span>
            <span class="store-tag">Mağaza: {{ etsyApi.activeShopId() }}</span>
          </div>

          <div class="table-wrap">
            <table class="audit-table">
              <thead>
                <tr>
                  <th>İlan Başlığı</th>
                  <th>SEO Skoru</th>
                  <th>Etiket</th>
                  <th>Fiyat ($/₺)</th>
                  <th>Tespit Edilen Sorunlar</th>
                  <th>İşlem</th>
                </tr>
              </thead>
              <tbody>
                <tr *ngFor="let item of auditItems" [class.selected-row]="selectedItem?.id === item.id">
                  <td class="item-title-cell font-semibold">{{ item.title }}</td>
                  <td>
                    <span class="score-badge" [ngClass]="getScoreClass(item.score)">
                      {{ item.score }}/100
                    </span>
                  </td>
                  <td>{{ item.tagsCount }}/13</td>
                  <td>
                    <b>&#36;{{ item.priceUsd | number:'1.2-2' }}</b>
                    <span class="try-sm">₺{{ item.priceUsd * etsyApi.exchangeRate() | number:'1.0-0' }}</span>
                  </td>
                  <td>
                    <span *ngIf="item.issues.length === 0" class="text-emerald">Sorun Yok</span>
                    <span *ngIf="item.issues.length > 0" class="text-orange">{{ item.issues.length }} İyileştirme Gerekli</span>
                  </td>
                  <td>
                    <button class="btn-inspect" (click)="selectedItem = item">İncele</button>
                  </td>
                </tr>
              </tbody>
            </table>
          </div>
        </div>

        <!-- RIGHT DETAIL / AI OPTIMIZER CARD -->
        <div class="glass-card fix-card" *ngIf="selectedItem">
          <div class="fix-head">
            <span class="fix-title">🤖 Gemini Spark İyileştirme Motoru</span>
            <span class="score-badge" [ngClass]="getScoreClass(selectedItem.score)">
              Skor: {{ selectedItem.score }}
            </span>
          </div>

          <h4 class="selected-title">{{ selectedItem.title }}</h4>

          <div class="issues-box" *ngIf="selectedItem.issues.length > 0">
            <span class="issues-head">⚠️ İyileştirilmesi Gereken Noktalar:</span>
            <ul>
              <li *ngFor="let issue of selectedItem.issues">{{ issue }}</li>
            </ul>
          </div>

          <div class="ai-recommendation-box">
            <span class="rec-label">✨ Gemini Spark Yapay Zeka Önerisi:</span>
            <p class="rec-text">{{ selectedItem.recommendation }}</p>
          </div>

          <button class="btn-apply-fix" (click)="applyFix(selectedItem)">
            🚀 Önerilen Düzeltmeleri İlana Uygula
          </button>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .audit-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .audit-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .header-left {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .icon-box {
      width: 44px;
      height: 44px;
      border-radius: 12px;
      background: linear-gradient(135deg, rgba(16, 185, 129, 0.2), rgba(6, 182, 212, 0.2));
      border: 1px solid rgba(16, 185, 129, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #34d399;
    }
    .page-title {
      font-size: 1.45rem;
      font-weight: 800;
      color: #fff;
      margin: 0;
    }
    .page-subtitle {
      font-size: 0.8rem;
      color: #94a3b8;
      margin: 2px 0 0 0;
    }
    .btn-primary-audit {
      background: linear-gradient(135deg, #10b981, #059669);
      border: none;
      color: #fff;
      padding: 11px 22px;
      border-radius: 10px;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.3);
      transition: all 0.2s;
    }
    .btn-primary-audit:hover {
      transform: translateY(-1px);
    }
    .score-strip {
      display: grid;
      grid-template-columns: 1.6fr 1fr 1fr 1fr;
      gap: 16px;
    }
    .main-score-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      align-items: center;
      gap: 18px;
    }
    .score-circle-wrap {
      width: 76px;
      height: 76px;
      border-radius: 50%;
      background: conic-gradient(#10b981 0% 86%, rgba(255, 255, 255, 0.1) 86% 100%);
      display: flex;
      flex-direction: column;
      align-items: center;
      justify-content: center;
      box-shadow: 0 0 16px rgba(16, 185, 129, 0.3);
    }
    .score-number {
      font-size: 1.55rem;
      font-weight: 900;
      color: #fff;
      line-height: 1;
    }
    .score-max {
      font-size: 0.65rem;
      color: #cbd5e1;
    }
    .score-details {
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .score-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
    }
    .score-desc {
      font-size: 0.78rem;
      color: #94a3b8;
      margin: 0;
    }
    .status-badge-row {
      display: flex;
      gap: 8px;
      margin-top: 6px;
    }
    .badge-good {
      font-size: 0.7rem;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.3);
      color: #34d399;
      padding: 2px 8px;
      border-radius: 6px;
    }
    .badge-warn {
      font-size: 0.7rem;
      background: rgba(249, 115, 22, 0.15);
      border: 1px solid rgba(249, 115, 22, 0.3);
      color: #fb923c;
      padding: 2px 8px;
      border-radius: 6px;
    }
    .metric-sub-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .sub-label {
      font-size: 0.7rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .sub-val {
      font-size: 1.45rem;
      font-weight: 800;
    }
    .sub-desc {
      font-size: 0.72rem;
      color: #64748b;
      line-height: 1.35;
    }
    .text-cyan { color: #06b6d4; }
    .text-emerald { color: #10b981; }
    .text-purple { color: #a855f7; }
    .text-orange { color: #f97316; }

    .audit-content-grid {
      display: grid;
      grid-template-columns: 2fr 1fr;
      gap: 20px;
    }
    .list-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
    }
    .card-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 16px;
    }
    .card-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
    }
    .store-tag {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .table-wrap {
      overflow-x: auto;
    }
    .audit-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.82rem;
    }
    .audit-table th {
      text-align: left;
      padding: 10px;
      color: #94a3b8;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
    }
    .audit-table td {
      padding: 12px 10px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.04);
    }
    .item-title-cell {
      max-width: 260px;
      color: #f1f5f9;
    }
    .score-badge {
      display: inline-block;
      padding: 3px 8px;
      border-radius: 6px;
      font-weight: 700;
      font-size: 0.75rem;
    }
    .score-high { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .score-mid { background: rgba(249, 115, 22, 0.2); color: #fb923c; }
    .score-low { background: rgba(239, 68, 68, 0.2); color: #f87171; }

    .try-sm {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
    }
    .btn-inspect {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.1);
      color: #fff;
      padding: 5px 12px;
      border-radius: 6px;
      font-size: 0.75rem;
      cursor: pointer;
    }
    .selected-row {
      background: rgba(59, 130, 246, 0.08);
    }

    .fix-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }
    .fix-head {
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .fix-title {
      font-size: 0.85rem;
      font-weight: 700;
      color: #38bdf8;
    }
    .selected-title {
      font-size: 0.95rem;
      font-weight: 700;
      color: #fff;
      margin: 0;
      line-height: 1.4;
    }
    .issues-box {
      background: rgba(239, 68, 68, 0.1);
      border: 1px solid rgba(239, 68, 68, 0.3);
      border-radius: 8px;
      padding: 12px;
    }
    .issues-head {
      font-size: 0.75rem;
      font-weight: 700;
      color: #f87171;
      display: block;
      margin-bottom: 6px;
    }
    .issues-box ul {
      margin: 0;
      padding-left: 18px;
      font-size: 0.78rem;
      color: #fca5a5;
    }
    .ai-recommendation-box {
      background: rgba(15, 23, 42, 0.8);
      border: 1px solid rgba(59, 130, 246, 0.3);
      border-radius: 8px;
      padding: 14px;
    }
    .rec-label {
      font-size: 0.75rem;
      font-weight: 700;
      color: #38bdf8;
      display: block;
      margin-bottom: 6px;
    }
    .rec-text {
      font-size: 0.82rem;
      color: #e2e8f0;
      line-height: 1.5;
      margin: 0;
    }
    .btn-apply-fix {
      background: #2563eb;
      border: none;
      color: #fff;
      padding: 12px;
      border-radius: 10px;
      font-weight: 700;
      font-size: 0.85rem;
      cursor: pointer;
      transition: background 0.2s;
    }
    .btn-apply-fix:hover {
      background: #1d4ed8;
    }
  `]
})
export class AiAuditComponent implements OnInit {
  overallScore = 86;
  isAuditing = false;

  auditItems: AuditItem[] = [
    {
      id: 1,
      title: 'Articulated Crystal Dragon with Moving Wings 3D Print',
      score: 92,
      tagsCount: 13,
      imageCount: 10,
      priceUsd: 39.50,
      views: 12450,
      issues: [],
      recommendation: 'Başlık ve etiket uyumu mükemmel. Görsel açılarında boyut karşılaştırması (el veya cetvel) eklenirse dönüşüm oranı %12 daha artabilir.'
    },
    {
      id: 2,
      title: '3D Printed Butterfly Trainer | Colorful Safe Knife | Cosplay Display Prop',
      score: 88,
      tagsCount: 13,
      imageCount: 8,
      priceUsd: 35.91,
      views: 1820,
      issues: [
        'Açıklama alanında ilk 160 karakterde malzeme bilgisi eksik.',
        'Kargo teslim süresi belirtilmemiş.'
      ],
      recommendation: 'Açıklamanın ilk paragrafına "Bambu PLA ile üretilmiş, çevre dostu ve pürüzsüz yüzey" ibaresini ekleyiniz.'
    },
    {
      id: 3,
      title: 'Desk Clock 3D Model Gear Mechanical',
      score: 64,
      tagsCount: 9,
      imageCount: 4,
      priceUsd: 89.00,
      views: 450,
      issues: [
        'Başlık 140 karakter yerine sadece 36 karakter.',
        'Sadece 9 etiket kullanılmış (4 etiket boşta).',
        'Görsel sayısı 4 (tavsiye edilen en az 7).'
      ],
      recommendation: 'Başlığı "Steampunk Mechanical Skeleton Gear Desk Clock | Industrial Retro Table Clock for Engineers" olarak güncelleyiniz ve 4 eksik etiketi ekleyiniz.'
    }
  ];

  selectedItem?: AuditItem;

  constructor(public etsyApi: EtsyApiService) {}

  ngOnInit(): void {
    this.selectedItem = this.auditItems[1];
  }

  getScoreClass(score: number): string {
    if (score >= 85) return 'score-high';
    if (score >= 70) return 'score-mid';
    return 'score-low';
  }

  runFullAudit(): void {
    this.isAuditing = true;
    setTimeout(() => {
      this.isAuditing = false;
      this.overallScore = 88;
      alert('Mağaza AI denetimi tamamlandı! Ortalama mağaza SEO skoru: %88');
    }, 1500);
  }

  applyFix(item: AuditItem): void {
    item.score = 94;
    item.issues = [];
    alert(`"${item.title}" ilanı için önerilen SEO iyileştirmeleri başarıyla uygulandı! Yeni Skor: %94`);
  }
}
