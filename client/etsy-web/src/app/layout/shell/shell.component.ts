import { Component, inject, HostListener } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { NavItem } from '../../core/models/etsy.models';

import { FloatingCopilotComponent } from '../copilot/floating-copilot.component';
import { AiModelsSettingsComponent } from '../../features/settings/ai-models-settings.component';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, FloatingCopilotComponent, AiModelsSettingsComponent, AiLogoComponent],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.css']
})
export class ShellComponent {
  authService = inject(AuthService);
  apiService = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  router = inject(Router);

  isRateModalOpen = false;
  tempRate = 48.25;
  isShopDropdownOpen = false;

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.isShopDropdownOpen) {
      this.isShopDropdownOpen = false;
    }
  }

  toggleShopDropdown(event: Event): void {
    event.stopPropagation();
    this.isShopDropdownOpen = !this.isShopDropdownOpen;
    if (this.isShopDropdownOpen) {
      this.apiService.loadConnectedShops();
    }
  }

  selectShop(shopId: string, event: Event): void {
    event.stopPropagation();
    this.apiService.switchShop(shopId);
    this.isShopDropdownOpen = false;
  }

  goToConnectNewShop(event: Event): void {
    event.stopPropagation();
    this.isShopDropdownOpen = false;
    this.router.navigate(['/settings/etsy-api']);
  }

  readonly categories = [
    {
      title: 'Genel',
      items: [
        { id: 'dashboard', title: 'Kontrol Paneli', icon: 'fa-chart-pie', route: '/dashboard' },
        { id: 'orders_shipping', title: 'Sipariş & Kargo', icon: 'fa-truck-fast', route: '/orders', badge: 'YENİ' },
        { id: 'fast_creator', title: 'Hızlı Ürün Ekle (AI)', icon: 'fa-bolt', route: '/listings/fast-creator', badge: 'YENİ' },
        { id: 'product_discovery', title: 'Ürün Bul & Taslak', icon: 'fa-crosshairs', route: '/listings/discovery', badge: 'YENİ' },
        { id: 'ai_image', title: 'AI Görsel Stüdyosu', icon: 'fa-wand-magic-sparkles', route: '/ai-studio', badge: 'PRO' },
        { id: 'shop', title: 'Mağazam Performansı', icon: 'fa-store', route: '/analytics/shop' }
      ]
    },
    {
      title: 'Araştırma & Analiz',
      items: [
        { id: 'research', title: 'Pazar Araştırması', icon: 'fa-magnifying-glass-chart', route: '/research/market' },
        { id: 'viral_3d', title: 'Viral 3D Model Avcısı', icon: 'fa-cube', route: '/research/viral-3d', badge: 'YENİ' },
        { id: 'competitor_spy', title: 'Rakip & Trend Casusu', icon: 'fa-user-secret', route: '/research/competitor', badge: 'YENİ' },
        { id: 'external', title: 'Dış Pazar Yeri Bulucu', icon: 'fa-globe', route: '/research/external' },
        { id: 'ai_audit', title: 'Mağaza AI Analizi', icon: 'fa-brain', route: '/analytics/ai-audit', badge: 'YENİ' },
        { id: 'ab_test', title: 'A/B Test Paneli', icon: 'fa-chart-line', route: '/analytics/ab-test' }
      ]
    },
    {
      title: 'Otomasyon & Finansal Araçlar',
      items: [
        { id: 'financial', title: 'Finansal Raporlama', icon: 'fa-file-invoice-dollar', route: '/finance/accounting', badge: 'YENİ' },
        { id: 'financial_ai', title: 'Finansal AI Analiz', icon: 'fa-microchip', route: '/finance/ai-analysis', badge: 'YENİ' },
        { id: 'profit', title: 'Kâr Simülatörü', icon: 'fa-sack-dollar', route: '/finance/profit' },
        { id: 'automation', title: 'Otomasyon Raporu', icon: 'fa-gears', route: '/tools/automation' },
        { id: 'batch', title: 'Toplu İşlem Kuyruğu', icon: 'fa-boxes-stacked', route: '/tools/batch' },
        { id: 'shop_vault', title: 'Mağaza Yedek & Transfer', icon: 'fa-shield-halved', route: '/tools/vault', badge: 'YENİ' },
        { id: 'tracking', title: 'Takip Geçmişi', icon: 'fa-location-crosshairs', route: '/tools/tracking' },
        { id: 'ai_usage', title: 'AI Token & Bakiye Takip', icon: 'fa-chart-simple', route: '/tools/ai-usage', badge: 'YENİ' }
      ]
    },
    {
      title: 'Kargo Entegrasyonları Hub',
      items: [
        { id: 'aras', title: 'Aras Global Kargo', icon: 'fa-truck', route: '/shipping/aras' },
        { id: 'shipentegra', title: 'ShipEntegra Kargo', icon: 'fa-box', route: '/shipping/shipentegra' },
        { id: 'navlungo', title: 'Navlungo Kargo', icon: 'fa-ship', route: '/shipping/navlungo' },
        { id: 'shiptomore', title: 'Shiptomore Kargo', icon: 'fa-plane-departure', route: '/shipping/shiptomore' }
      ]
    },
    {
      title: 'Sistem & Ayarlar',
      items: [
        { id: 'ai_models', title: 'AI Model & Optimizasyon', icon: 'fa-sliders', route: '/settings/ai-models', badge: 'YENİ' },
        { id: 'notifications', title: 'Telegram Bildirim Botu', icon: 'fa-paper-plane', route: '/settings/notifications', badge: 'YENİ' },
        { id: 'api', title: 'Etsy API Ayarları', icon: 'fa-key', route: '/settings/etsy-api' },
        { id: 'update', title: 'Sürüm Güncelle (Client)', icon: 'fa-arrows-rotate', route: '/settings/update', badge: 'YENİ' },
        { id: 'logs', title: 'Uygulama Günlüğü', icon: 'fa-terminal', route: '/system/logs' }
      ]
    }
  ];

  openRateModal(): void {
    this.tempRate = this.apiService.exchangeRate();
    this.isRateModalOpen = true;
  }

  saveRate(): void {
    this.apiService.setExchangeRate(this.tempRate);
    this.isRateModalOpen = false;
  }

  closeRateModal(): void {
    this.isRateModalOpen = false;
  }
}
