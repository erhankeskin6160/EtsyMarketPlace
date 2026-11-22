import { Component, DestroyRef, HostListener, OnInit, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { RouterModule, Router } from '@angular/router';
import { FormsModule } from '@angular/forms';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { TranslocoDirective } from '@jsverse/transloco';
import { AuthService } from '../../core/services/auth.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { SubscriptionService } from '../../core/services/subscription.service';
import { BreakpointService } from '../../core/services/breakpoint.service';

import { FloatingCopilotComponent } from '../copilot/floating-copilot.component';
import { AiModelsSettingsComponent } from '../../features/settings/ai-models-settings.component';
import { AiLogoComponent } from '../../core/components/ai-logo.component';
import { LanguageSwitcherComponent } from '../../core/components/language-switcher.component';

@Component({
  selector: 'app-shell',
  standalone: true,
  imports: [
    CommonModule,
    RouterModule,
    FormsModule,
    TranslocoDirective,
    FloatingCopilotComponent,
    AiModelsSettingsComponent,
    AiLogoComponent,
    LanguageSwitcherComponent
  ],
  templateUrl: './shell.component.html',
  styleUrls: ['./shell.component.css']
})
export class ShellComponent implements OnInit {
  private destroyRef = inject(DestroyRef);
  authService = inject(AuthService);
  apiService = inject(EtsyApiService);
  aiService = inject(AiSettingsService);
  subscriptionService = inject(SubscriptionService);
  breakpointService = inject(BreakpointService);
  router = inject(Router);

  isRateModalOpen = false;
  tempRate = 48.25;
  isShopDropdownOpen = false;

  ngOnInit(): void {
    this.subscriptionService.loadPlans().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
    this.subscriptionService.fetchMySubscription().pipe(takeUntilDestroyed(this.destroyRef)).subscribe();
  }

  onNavItemClick(event: Event, item: { id: string; route: string }): void {
    if (!this.subscriptionService.isFeatureAllowed(item.id)) {
      event.preventDefault();
      this.router.navigate(['/settings/subscription'], { queryParams: { upgrade: item.id } });
    }
  }

  @HostListener('document:click')
  onDocumentClick(): void {
    if (this.isShopDropdownOpen) {
      this.isShopDropdownOpen = false;
    }
  }

  @HostListener('document:keydown.escape')
  onEscapeKey(): void {
    if (this.breakpointService.isDrawerOpen()) {
      this.breakpointService.closeDrawer();
    }
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
      titleKey: 'shell.categories.general',
      items: [
        { id: 'dashboard', titleKey: 'shell.nav.dashboard', icon: 'fa-chart-pie', route: '/dashboard' },
        { id: 'orders_shipping', titleKey: 'shell.nav.orders_shipping', icon: 'fa-truck-fast', route: '/orders' },
        { id: 'fast_creator', titleKey: 'shell.nav.fast_creator', icon: 'fa-bolt', route: '/listings/fast-creator' },
        { id: 'product_discovery', titleKey: 'shell.nav.product_discovery', icon: 'fa-crosshairs', route: '/listings/discovery' },
        { id: 'ai_image', titleKey: 'shell.nav.ai_image', icon: 'fa-wand-magic-sparkles', route: '/ai-studio' },
        { id: 'shop', titleKey: 'shell.nav.shop', icon: 'fa-store', route: '/analytics/shop' }
      ]
    },
    {
      titleKey: 'shell.categories.research',
      items: [
        { id: 'research_agent', titleKey: 'shell.nav.research_agent', icon: 'fa-robot', route: '/research/agent' },
        { id: 'research', titleKey: 'shell.nav.research', icon: 'fa-magnifying-glass-chart', route: '/research/market' },
        { id: 'viral_3d', titleKey: 'shell.nav.viral_3d', icon: 'fa-cube', route: '/research/viral-3d' },
        { id: 'competitor_spy', titleKey: 'shell.nav.competitor_spy', icon: 'fa-user-secret', route: '/research/competitor' },
        { id: 'external', titleKey: 'shell.nav.external', icon: 'fa-globe', route: '/research/external' },
        { id: 'ai_audit', titleKey: 'shell.nav.ai_audit', icon: 'fa-brain', route: '/analytics/ai-audit' },
        { id: 'ab_test', titleKey: 'shell.nav.ab_test', icon: 'fa-chart-line', route: '/analytics/ab-test' }
      ]
    },
    {
      titleKey: 'shell.categories.automationFinance',
      items: [
        { id: 'financial', titleKey: 'shell.nav.financial', icon: 'fa-file-invoice-dollar', route: '/finance/accounting' },
        { id: 'financial_ai', titleKey: 'shell.nav.financial_ai', icon: 'fa-microchip', route: '/finance/ai-analysis' },
        { id: 'profit', titleKey: 'shell.nav.profit', icon: 'fa-sack-dollar', route: '/finance/profit' },
        { id: 'automation', titleKey: 'shell.nav.automation', icon: 'fa-gears', route: '/tools/automation' },
        { id: 'batch', titleKey: 'shell.nav.batch', icon: 'fa-boxes-stacked', route: '/tools/batch' },
        { id: 'shop_vault', titleKey: 'shell.nav.shop_vault', icon: 'fa-shield-halved', route: '/tools/vault' },
        { id: 'tracking', titleKey: 'shell.nav.tracking', icon: 'fa-location-crosshairs', route: '/tools/tracking' },
        { id: 'ai_usage', titleKey: 'shell.nav.ai_usage', icon: 'fa-chart-simple', route: '/tools/ai-usage' }
      ]
    },
    {
      titleKey: 'shell.categories.shippingHub',
      items: [
        { id: 'aras', titleKey: 'shell.nav.aras', icon: 'fa-truck', route: '/shipping/aras' },
        { id: 'shipentegra', titleKey: 'shell.nav.shipentegra', icon: 'fa-box', route: '/shipping/shipentegra' },
        { id: 'navlungo', titleKey: 'shell.nav.navlungo', icon: 'fa-ship', route: '/shipping/navlungo' },
        { id: 'shiptomore', titleKey: 'shell.nav.shiptomore', icon: 'fa-plane-departure', route: '/shipping/shiptomore' }
      ]
    },
    {
      titleKey: 'shell.categories.systemSettings',
      items: [
        { id: 'subscription', titleKey: 'shell.nav.subscription', icon: 'fa-layer-group', route: '/settings/subscription' },
        { id: 'ai_models', titleKey: 'shell.nav.ai_models', icon: 'fa-sliders', route: '/settings/ai-models' },
        { id: 'notifications', titleKey: 'shell.nav.notifications', icon: 'fa-paper-plane', route: '/settings/notifications' },
        { id: 'api', titleKey: 'shell.nav.api', icon: 'fa-key', route: '/settings/etsy-api' },
        { id: 'update', titleKey: 'shell.nav.update', icon: 'fa-arrows-rotate', route: '/settings/update' },
        { id: 'logs', titleKey: 'shell.nav.logs', icon: 'fa-terminal', route: '/system/logs' }
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
