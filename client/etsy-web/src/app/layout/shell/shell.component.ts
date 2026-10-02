import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { AuthService } from '../../core/services/auth.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { NavItem } from '../../core/models/etsy.models';

import { FloatingCopilotComponent } from '../copilot/floating-copilot.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [CommonModule, RouterModule, FormsModule, FloatingCopilotComponent],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.css']
})
export class ShellComponent {
  authService = inject(AuthService);
  apiService = inject(EtsyApiService);
  router = inject(Router);

  isRateModalOpen = false;
  tempRate = 48.25;

  readonly categories = [
    {
      title: 'Genel',
      items: [
        { id: 'dashboard', title: 'Kontrol Paneli', icon: '📊', route: '/dashboard' },
        { id: 'orders_shipping', title: 'Sipariş & Kargo', icon: '🚚', route: '/orders', badge: 'YENİ' },
        { id: 'fast_creator', title: 'Hızlı Ürün Ekle (AI)', icon: '⚡', route: '/listings/fast-creator', badge: 'YENİ' },
        { id: 'creator', title: 'Ürün Bul & Taslak', icon: '🛍️', route: '/listings/creator' },
        { id: 'ai_image', title: 'AI Görsel Stüdyosu', icon: '🎨', route: '/ai-studio', badge: 'PRO' },
        { id: 'shop', title: 'Mağazam Performansı', icon: '🏬', route: '/analytics/shop' }
      ]
    },
    {
      title: 'Araştırma & Analiz',
      items: [
        { id: 'research', title: 'Pazar Araştırması', icon: '🔍', route: '/research/market' },
        { id: 'viral_3d', title: 'Viral 3D Model Avcısı', icon: '🔥', route: '/research/viral-3d', badge: 'YENİ' },
        { id: 'competitor_spy', title: 'Rakip & Trend Casusu', icon: '🕵️', route: '/research/competitor', badge: 'YENİ' },
        { id: 'external', title: 'Dış Pazar Yeri Bulucu', icon: '🌐', route: '/research/external' },
        { id: 'ai_audit', title: 'Mağaza AI Analizi', icon: '🤖', route: '/analytics/ai-audit', badge: 'YENİ' },
        { id: 'ab_test', title: 'A/B Test Paneli', icon: '📈', route: '/analytics/ab-test' }
      ]
    },
    {
      title: 'Otomasyon & Finansal Araçlar',
      items: [
        { id: 'financial', title: 'Finansal Raporlama', icon: '💳', route: '/finance/accounting', badge: 'YENİ' },
        { id: 'financial_ai', title: 'Finansal AI Analiz', icon: '🧠', route: '/finance/ai-analysis', badge: 'YENİ' },
        { id: 'profit', title: 'Kâr Simülatörü', icon: '💰', route: '/finance/profit' },
        { id: 'automation', title: 'Otomasyon Raporu', icon: '⚡', route: '/tools/automation' },
        { id: 'batch', title: 'Toplu İşlem Kuyruğu', icon: '📦', route: '/tools/batch' },
        { id: 'shop_vault', title: 'Mağaza Yedek & Transfer', icon: '🛡️', route: '/tools/vault', badge: 'YENİ' },
        { id: 'tracking', title: 'Takip Geçmişi', icon: '🎯', route: '/tools/tracking' },
        { id: 'ai_usage', title: 'AI Token & Bakiye Takip', icon: '📊', route: '/tools/ai-usage', badge: 'YENİ' }
      ]
    },
    {
      title: 'Kargo Entegrasyonları Hub',
      items: [
        { id: 'aras', title: 'Aras Global Kargo', icon: '🚚', route: '/shipping/aras' },
        { id: 'shipentegra', title: 'ShipEntegra Kargo', icon: '📦', route: '/shipping/shipentegra' },
        { id: 'navlungo', title: 'Navlungo Kargo', icon: '🚢', route: '/shipping/navlungo' },
        { id: 'shiptomore', title: 'Shiptomore Kargo', icon: '✈️', route: '/shipping/shiptomore' }
      ]
    },
    {
      title: 'Sistem & Ayarlar',
      items: [
        { id: 'notifications', title: 'Telegram Bildirim Botu', icon: '✈️', route: '/settings/notifications', badge: 'YENİ' },
        { id: 'api', title: 'Etsy API Ayarları', icon: '⚙️', route: '/settings/etsy-api' },
        { id: 'update', title: 'Sürüm Güncelle (Client)', icon: '🚀', route: '/settings/update', badge: 'YENİ' },
        { id: 'logs', title: 'Uygulama Günlüğü', icon: '📄', route: '/system/logs' }
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
