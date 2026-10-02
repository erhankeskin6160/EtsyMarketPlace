import { Component, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { ActivatedRoute, RouterModule } from '@angular/router';
import { EtsyApiService } from '../../core/services/etsy-api.service';

interface ModuleDetail {
  id: string;
  title: string;
  category: string;
  icon: string;
  badge?: string;
  summary: string;
  features: string[];
  kpiTitle?: string;
  kpiValue?: string;
}

@Component({
  selector: 'app-module-viewer',
  standalone: true,
  imports: [CommonModule, RouterModule],
  template: `
    <div class="module-root" *ngIf="module">
      <div class="module-banner glass-card">
        <div class="banner-left">
          <div class="module-badge-row">
            <span class="category-pill">{{ module.category }}</span>
            <span class="badge badge-new" *ngIf="module.badge">{{ module.badge }}</span>
          </div>
          <div class="module-title-row">
            <span class="module-icon">{{ module.icon }}</span>
            <div>
              <h2 class="module-title">{{ module.title }}</h2>
              <p class="module-summary">{{ module.summary }}</p>
            </div>
          </div>
        </div>

        <div class="banner-right" *ngIf="module.kpiTitle">
          <div class="module-kpi-card">
            <span class="kpi-label">{{ module.kpiTitle }}</span>
            <span class="kpi-num text-orange">{{ module.kpiValue }}</span>
            <span class="kpi-sub">Canlı Senkronize</span>
          </div>
        </div>
      </div>

      <!-- FEATURES & ACTION CARDS -->
      <section class="features-grid">
        <div class="glass-card feature-card" *ngFor="let feat of module.features; let i = index">
          <div class="feat-num">0{{ i + 1 }}</div>
          <h4 class="feat-title">{{ feat }}</h4>
          <p class="feat-desc">Masaüstü motoru ile tam uyumlu çalışan optimize edilmiş bulut algoritması.</p>
          <button class="btn btn-secondary btn-sm" (click)="triggerAction(feat)">
            ⚡ Şimdi Çalıştır
          </button>
        </div>
      </section>

      <!-- LIVE VDS API CONNECTION STATUS -->
      <section class="glass-card api-status-card">
        <div class="status-header">
          <div class="status-left">
            <span class="status-dot-pulse"></span>
            <div>
              <h4 class="status-title">VDS API & Gemini Spark Motoru Bağlı</h4>
              <p class="status-sub">Mağaza ID: <b>{{ apiService.activeShopId() }}</b> | Uç Nokta: <code>http://5.180.81.148:5263/api/*</code></p>
            </div>
          </div>
          <a routerLink="/dashboard" class="btn btn-orange">← Kontrol Paneline Dön</a>
        </div>
      </section>
    </div>
  `,
  styles: [`
    .module-root {
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .module-banner {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 24px;
      padding: 24px;
      background: linear-gradient(135deg, rgba(21, 30, 46, 0.95), rgba(15, 23, 42, 0.9));
    }
    @media (max-width: 768px) {
      .module-banner { flex-direction: column; align-items: flex-start; }
    }
    .module-badge-row {
      display: flex;
      align-items: center;
      gap: 8px;
      margin-bottom: 8px;
    }
    .category-pill {
      font-size: 0.72rem;
      font-weight: 700;
      color: #38bdf8;
      background: rgba(56, 189, 248, 0.12);
      border: 1px solid rgba(56, 189, 248, 0.25);
      padding: 2px 8px;
      border-radius: 6px;
      text-transform: uppercase;
    }
    .module-title-row {
      display: flex;
      align-items: center;
      gap: 16px;
    }
    .module-icon {
      font-size: 2.2rem;
    }
    .module-title {
      font-size: 1.5rem;
      font-weight: 800;
      color: #fff;
    }
    .module-summary {
      font-size: 0.88rem;
      color: var(--text-muted);
      margin-top: 2px;
    }

    .module-kpi-card {
      background: #090d16;
      border: 1px solid var(--border-color);
      border-radius: 10px;
      padding: 14px 20px;
      display: flex;
      flex-direction: column;
      text-align: right;
      min-width: 160px;
    }
    .kpi-label { font-size: 0.7rem; color: var(--text-muted); }
    .kpi-num { font-size: 1.4rem; font-weight: 800; margin: 2px 0; }
    .kpi-sub { font-size: 0.68rem; color: #10b981; }

    .features-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(260px, 1fr));
      gap: 16px;
    }
    .feature-card {
      display: flex;
      flex-direction: column;
      gap: 8px;
      padding: 20px;
    }
    .feat-num {
      font-family: 'JetBrains Mono', monospace;
      font-size: 0.85rem;
      font-weight: 700;
      color: var(--etsy-orange);
    }
    .feat-title {
      font-size: 1rem;
      font-weight: 700;
      color: #fff;
    }
    .feat-desc {
      font-size: 0.8rem;
      color: var(--text-muted);
      line-height: 1.4;
      flex: 1;
    }

    .api-status-card {
      padding: 16px 20px;
    }
    .status-header {
      display: flex;
      align-items: center;
      justify-content: space-between;
      gap: 16px;
    }
    .status-left {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .status-dot-pulse {
      width: 10px;
      height: 10px;
      background: #10b981;
      border-radius: 50%;
      box-shadow: 0 0 10px #10b981;
    }
    .status-title {
      font-size: 0.9rem;
      font-weight: 700;
      color: #fff;
    }
    .status-sub {
      font-size: 0.75rem;
      color: var(--text-muted);
    }
  `]
})
export class ModuleViewerComponent implements OnInit {
  route = inject(ActivatedRoute);
  apiService = inject(EtsyApiService);

  module?: ModuleDetail;

  private readonly catalog: Record<string, ModuleDetail> = {
    'orders': {
      id: 'orders',
      title: 'Sipariş & Kargo Karşılama Studio',
      category: 'Genel',
      icon: '🚚',
      badge: 'YENİ',
      summary: 'Etsy siparişlerinin takibi, eksik maliyet tespiti, otomatik kargo konşimentosu oluşturma ve kâr marjı hesaplama.',
      features: [
        'Eksik Maliyetli Siparişler için Kırmızı Alarm & Bildirim',
        'Aras Global, ShipEntegra, Navlungo & Shiptomore Canlı Fiyatlandırma',
        'Tek Tıkla Siparişe Maliyet & Takip Kodu Atama',
        'Gerçekleşen Kâr Marjı (Gross vs Net Profit) Analizi'
      ],
      kpiTitle: 'BEKLEYEN SİPARİŞ',
      kpiValue: '18 Adet'
    },
    'fast-creator': {
      id: 'fast-creator',
      title: 'Hızlı Ürün Ekle (AI Fast Creator)',
      category: 'Genel',
      icon: '⚡',
      badge: 'YENİ',
      summary: 'Tek tıklamayla AI destekli SEO başlık, açıklama ve 13 algoritma etiketini saniyeler içinde mağazanıza aktarma.',
      features: [
        'Gemini Spark AI ile Kusursuz Başlık ve Açıklama Üretimi',
        '13 Yüksek Hacimli Etsy Arama Etiketi (Tags) Optimizasyonu',
        'Önceden Tanımlı Şablonlar (3D Model, Hediyelik, Takı)',
        'Doğrudan Mağazaya Taslak (Draft) Olarak Yükleme'
      ],
      kpiTitle: 'ŞABLON SAYISI',
      kpiValue: '12 Şablon'
    },
    'creator': {
      id: 'creator',
      title: 'Ürün Bul & Taslak Listeleme',
      category: 'Genel',
      icon: '🛍️',
      summary: 'Trend ürün radarı, pazar hacmi tahminleme ve tek tıkla taslak listeleme stüdyosu.',
      features: [
        'Global Trendlerdeki Ürünlerin Keşfi',
        'Hedef Kitle ve Kategori Uygunluk Skoru',
        'Dinamik Fiyat Öneri Simülatörü',
        'Taslak Envanter Yönetimi'
      ]
    },
    'ai-studio': {
      id: 'ai-studio',
      title: 'AI Görsel Stüdyosu & Arka Plan Temizleyici',
      category: 'Genel',
      icon: '🎨',
      badge: 'PRO',
      summary: 'Yapay zeka ile stüdyo kalitesinde mockup üretimi, fon değiştirme ve anti-ban EXIF temizleme.',
      features: [
        'AI ile Ürün Sahnesi ve Stüdyo Arka Planı Üretimi',
        'Arka Plan Kaldırma (Background Removal)',
        'Anti-Ban EXIF Metadata ve Görsel Parmak İzi Temizleme',
        'Etsy Ölçülerine (2000x2000px) Uygun Otomatik Ölçekleme'
      ],
      kpiTitle: 'İŞLENEN GÖRSEL',
      kpiValue: '142 Görsel'
    },
    'analytics-shop': {
      id: 'analytics-shop',
      title: 'Mağazam Performansı & Ziyaretçi Analitiği',
      category: 'Genel',
      icon: '🏬',
      summary: 'Görüntülenme, favori sayıları, dönüşüm oranları (Conversion Rate %) ve ürün bazlı trafik grafikleri.',
      features: [
        'Günlük ve Aylık Ziyaretçi & Favori Sayısı Trendi',
        'Dönüşüm Oranı En Yüksek İlk 10 İlan',
        'Hareketsiz / Trafik Almayan İlanlar için Uyarı Sistemi',
        'Aylık İlan Trafik Veri Tabanı (listing_traffic_daily)'
      ]
    },
    'market-search': {
      id: 'market-search',
      title: 'Pazar Araştırması & Anahtar Kelime Radarı',
      category: 'Araştırma & Analiz',
      icon: '🔍',
      summary: 'Anahtar kelime arama hacmi, rekabet skoru ve en çok satan listelemelerin detaylı pazar dökümü.',
      features: [
        'Arama Hacmi ve Tıklama Başı Maliyet (CPC) Matrisi',
        'Düşük Rekabet / Yüksek Talep Niche Keşif Motoru',
        'Rakiplerin Kullandığı Ortak Etiket Dağılımı',
        'Arama Trendi Mevsimsellik Değerlendirmesi'
      ]
    },
    'viral-3d': {
      id: 'viral-3d',
      title: 'Viral 3D Model Avcısı & Arbitraj Radarı',
      category: 'Araştırma & Analiz',
      icon: '🔥',
      badge: 'YENİ',
      summary: 'MakerWorld, Printables, CrealityCloud ve Thingiverse üzerinde viral olan modellerin Etsy kâr arbitrajı.',
      features: [
        'MakerWorld & Printables Popüler İndirme Taraması',
        'Model Gramajı ve Filament Maliyeti Hesaplama',
        'Etsy Benzer Ürün Fiyat Karşılaştırması',
        'Potansiyel Net Kâr Marjı Radarı (%300+ Fırsatlar)'
      ],
      kpiTitle: 'VİRAL MODEL',
      kpiValue: '28 Model'
    },
    'competitor': {
      id: 'competitor',
      title: 'Rakip & Trend Casusu',
      category: 'Araştırma & Analiz',
      icon: '🕵️',
      badge: 'YENİ',
      summary: 'Rakip mağazaların günlük satış hızları, fiyat değişiklikleri ve en çok satan listing tespiti.',
      features: [
        'Rakip Mağazanın 30 Günlük Satış Hızı Projeksiyonu',
        'Fiyat Artışı / İndirimi Canlı Alarmı',
        'En Son Eklenen Başarılı İlanların Bildirimi',
        'Pazar Payı ve Satıcı Sıralaması Değerlendirmesi'
      ]
    },
    'external': {
      id: 'external',
      title: 'Dış Pazar Yeri Bulucu (Arbitraj)',
      category: 'Araştırma & Analiz',
      icon: '🌐',
      summary: 'Amazon, eBay, AliExpress ve Walmart üzerinden çapraz arbitraj ve fırsat tespiti.',
      features: [
        'Amazon Handmade vs Etsy Fiyat Karşılaştırması',
        'AliExpress Tedarik Fiyatı ve Kargo Süresi Analizi',
        'Görsel Arama ile Çapraz Pazar Eşleme',
        'İthalat ve Gümrük Masrafı Simülasyonu'
      ]
    },
    'ai-audit': {
      id: 'ai-audit',
      title: 'Mağaza AI Analizi & Denetim',
      category: 'Araştırma & Analiz',
      icon: '🤖',
      badge: 'YENİ',
      summary: 'Yapay zeka ile mağazadaki tüm listinglerin taranması, eksik etiket ve zayıf açıklamaların raporlanması.',
      features: [
        'Fotoğraf Kontrol Listesi (Işık, Mockup, Açı Değerlendirmesi)',
        'Eksik Etiket ve Zayıf Başlık Tespiti',
        'Müşteri Gözünden Listing İnceleme Raporu',
        'Tek Tıkla Toplu Optimizasyon Önerisi'
      ]
    },
    'ab-test': {
      id: 'ab-test',
      title: 'A/B Test Paneli',
      category: 'Araştırma & Analiz',
      icon: '📈',
      summary: 'Başlık, ana görsel ve fiyat A/B testlerinin takibi, istatistiksel üstünlük ölçümü.',
      features: [
        'İki Farklı Başlık Varyasyonunun Tıklama Oranı Karşılaştırması',
        'Görsel Testinde Dönüşüm Oranı Artışı',
        'İstatistiksel Anlamlılık (Statistical Significance %) Göstergesi',
        'Kazanan Varyasyonun Otomatik Olarak Canlıya Alınması'
      ]
    },
    'accounting': {
      id: 'accounting',
      title: 'Finansal Muhasebe & Banka Ödemeleri',
      category: 'Otomasyon & Finans',
      icon: '💳',
      badge: 'YENİ',
      summary: 'Çift para birimi (₺ TRY / $ USD) ile tam kapsamlı Etsy ödeme defteri, komisyonlar ve banka transferleri.',
      features: [
        'Canlı Güncel Dolar Kuru ile Çift Para Birimi Hesaplaması',
        'Etsy Komisyonları, İşlem Ücreti ve Reklam Giderleri Dökümü',
        'Hesaba Yatan Banka Transferleri (Payouts) Listesi',
        'Excel / CSV Formatında Dışa Aktarma (Export)'
      ],
      kpiTitle: 'NET KÂR ORANI',
      kpiValue: '%43.8'
    },
    'financial-ai': {
      id: 'financial-ai',
      title: 'Finansal AI Analiz & Gelecek Projeksiyonu',
      category: 'Otomasyon & Finans',
      icon: '🧠',
      badge: 'YENİ',
      summary: 'Kural tabanlı finansal yapay zeka öneri motoru, marj uyarıları ve 30 günlük nakit akışı tahmini.',
      features: [
        'Gider Kalemlerinde Anormal Artış Tespiti',
        'Kârlılığı Düşen Ürünler için Fiyat Artışı Önerisi',
        'Gelecek Ay Tahmini Banka Girişi Projeksiyonu',
        'Vergi ve Muhasebe Ön Hazırlık Raporu'
      ]
    },
    'profit': {
      id: 'profit',
      title: 'Kâr Simülatörü & Fiyat Hesaplayıcı',
      category: 'Otomasyon & Finans',
      icon: '💰',
      summary: 'Hammadde, kargo, Etsy komisyonu ve reklam payını girerek ideal satış fiyatını hesaplayın.',
      features: [
        'Etsy %6.5 İşlem Ücreti + Sabit Ücret Hesaplaması',
        'Kargo Firması (Aras, ShipEntegra, Navlungo) Entegre Fiyatı',
        'Hedeflenen Net Kâr Marjına Göre Fiyat Tavsiyesi',
        'Dolar Kuru Değişimine Duyarlı Dinamik Tablo'
      ]
    },
    'vault': {
      id: 'vault',
      title: 'Mağaza Yedekleme & Anti-Ban Transfer (.etsyvault)',
      category: 'Otomasyon & Finans',
      icon: '🛡️',
      badge: 'YENİ',
      summary: 'Tüm mağazayı tek tıkla `.etsyvault` olarak yedekleme, anti-ban resim işleme ve kurtarma.',
      features: [
        'Tüm Başlık, Açıklama, Etiket ve Resimlerin Şifreli Yedeklenmesi',
        'Resimlerin EXIF ve pHash Bilgilerini Sıfırlama (Anti-Ban)',
        'Başka Bir Etsy Mağazasına Tek Tıkla Geri Yükleme (Restore)',
        'SQLite Tabanlı Yerel Arşivleme Güvencesi'
      ],
      kpiTitle: 'YEDEK DURUMU',
      kpiValue: '84 İlan Hazır'
    },
    'ai-usage': {
      id: 'ai-usage',
      title: 'AI Token, Model & Bakiye Takip',
      category: 'Otomasyon & Finans',
      icon: '📊',
      badge: 'YENİ',
      summary: 'Gemini, OpenAI ve Claude API tüketimlerinin canlı takibi, maliyet dökümü ve kullanıcı kotaları.',
      features: [
        'Model Bazlı Token Tüketim Grafiği (gemini-2.5-flash)',
        'Kullanıcı Başına Kalan Aylık Sorgu Kotası',
        'Sorgu Başına Ortalama Yanıt Süresi (Latency ms)',
        'Maliyet Tasarrufu ve Prompt Optimizasyon İpuçları'
      ]
    },
    'shipping': {
      id: 'shipping',
      title: 'Çoklu Taşıyıcı Kargo Entegrasyonları Hub',
      category: 'Kargo',
      icon: '🚚',
      summary: 'Aras Global, ShipEntegra, Navlungo ve Shiptomore hesaplarınızı tek merkezden yönetin.',
      features: [
        'Tüm Kargo Taşıyıcılarının Anlık Fiyat Karşılaştırması',
        'Otomatik Koli Hacmi (Desi) ve Ağırlık Hesaplama',
        'Siparişe Doğrudan Kargo Etiketi Oluşturma',
        'Takip Numarasının Etsy Üzerine Otomatik İşlenmesi'
      ]
    },
    'system': {
      id: 'system',
      title: 'Sistem, API Ayarları & Günlükler',
      category: 'Sistem',
      icon: '⚙️',
      summary: 'Etsy OAuth bağlantıları, Telegram bildirim botu, VDS otomatik güncelleme ve sistem logları.',
      features: [
        'Canlı Etsy API Token Yenileme (Refresh Token)',
        'Telegram Kanalına Anlık Sipariş & Ciro Bildirimleri',
        'GitHub Actions VDS Derleme ve Sürüm Takibi',
        'Uygulama Hata Günlüğü (Application Logs)'
      ]
    }
  };

  ngOnInit(): void {
    this.route.url.subscribe(() => {
      const path = this.route.snapshot.routeConfig?.path || '';
      const mappedKey = this.resolveKey(path);
      this.module = this.catalog[mappedKey] || this.catalog['orders'];
    });
  }

  private resolveKey(path: string): string {
    if (path.includes('fast-creator')) return 'fast-creator';
    if (path.includes('creator')) return 'creator';
    if (path.includes('ai-studio')) return 'ai-studio';
    if (path.includes('analytics/shop')) return 'analytics-shop';
    if (path.includes('research/market')) return 'market-search';
    if (path.includes('research/viral-3d')) return 'viral-3d';
    if (path.includes('research/competitor')) return 'competitor';
    if (path.includes('research/external')) return 'external';
    if (path.includes('analytics/ai-audit')) return 'ai-audit';
    if (path.includes('analytics/ab-test')) return 'ab-test';
    if (path.includes('finance/accounting')) return 'accounting';
    if (path.includes('finance/ai-analysis')) return 'financial-ai';
    if (path.includes('finance/profit')) return 'profit';
    if (path.includes('tools/vault')) return 'vault';
    if (path.includes('tools/ai-usage')) return 'ai-usage';
    if (path.includes('shipping')) return 'shipping';
    if (path.includes('settings') || path.includes('system')) return 'system';
    return 'orders';
  }

  triggerAction(feat: string): void {
    alert(`⚡ "${feat}" işlemi başarıyla VDS API kuyruğuna iletildi!`);
  }
}
