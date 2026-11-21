import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { authGuard, adminGuard } from './core/guards/auth.guard';
import { subscriptionFeatureGuard } from './core/guards/subscription.guard';

export const routes: Routes = [
  // Public Auth Routes (Lazy-loaded)
  {
    path: 'auth/login',
    loadComponent: () => import('./features/auth/login.component').then(m => m.LoginComponent)
  },
  {
    path: 'auth/register',
    loadComponent: () => import('./features/auth/register.component').then(m => m.RegisterComponent)
  },

  // Protected App Shell Routes
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      {
        path: 'dashboard',
        loadComponent: () => import('./features/dashboard/dashboard.component').then(m => m.DashboardComponent)
      },
      {
        path: 'admin',
        canActivate: [adminGuard],
        loadComponent: () => import('./features/admin/admin.component').then(m => m.AdminComponent)
      },

      // Core Interactive Modules
      {
        path: 'orders',
        loadComponent: () => import('./features/orders/orders.component').then(m => m.OrdersComponent)
      },
      {
        path: 'finance/accounting',
        canActivate: [subscriptionFeatureGuard('financial')],
        loadComponent: () => import('./features/finance/accounting.component').then(m => m.AccountingComponent)
      },
      {
        path: 'finance/profit',
        loadComponent: () => import('./features/finance/profit-calculator.component').then(m => m.ProfitCalculatorComponent)
      },
      {
        path: 'finance/product-costs',
        loadComponent: () => import('./features/finance/product-cost-manager.component').then(m => m.ProductCostManagerComponent)
      },
      {
        path: 'listings/fast-creator',
        loadComponent: () => import('./features/listings/fast-creator.component').then(m => m.FastCreatorComponent)
      },
      { path: 'listings/creator', redirectTo: 'listings/fast-creator', pathMatch: 'full' },
      {
        path: 'listings/discovery',
        loadComponent: () => import('./features/listings/product-discovery.component').then(m => m.ProductDiscoveryComponent)
      },
      {
        path: 'ai-studio',
        canActivate: [subscriptionFeatureGuard('ai_image')],
        loadComponent: () => import('./features/ai-studio/ai-studio.component').then(m => m.AiStudioComponent)
      },

      // Research & Intelligence Modules
      {
        path: 'research/agent',
        loadComponent: () => import('./features/research/shop-research-agent.component').then(m => m.ShopResearchAgentComponent)
      },
      {
        path: 'research/market',
        loadComponent: () => import('./features/research/market-research.component').then(m => m.MarketResearchComponent)
      },
      {
        path: 'research/viral-3d',
        canActivate: [subscriptionFeatureGuard('viral_3d')],
        loadComponent: () => import('./features/research/viral-3d.component').then(m => m.Viral3DComponent)
      },
      {
        path: 'research/competitor',
        canActivate: [subscriptionFeatureGuard('competitor_spy')],
        loadComponent: () => import('./features/research/competitor-spy.component').then(m => m.CompetitorSpyComponent)
      },
      {
        path: 'research/external',
        canActivate: [subscriptionFeatureGuard('external')],
        loadComponent: () => import('./features/research/external-arbitrage.component').then(m => m.ExternalArbitrageRadarComponent)
      },

      // Analytics Modules
      {
        path: 'analytics/ai-audit',
        canActivate: [subscriptionFeatureGuard('ai_audit')],
        loadComponent: () => import('./features/analytics/ai-audit.component').then(m => m.AiAuditComponent)
      },
      {
        path: 'analytics/shop',
        loadComponent: () => import('./features/analytics/shop-performance.component').then(m => m.ShopPerformanceComponent)
      },
      {
        path: 'analytics/ab-test',
        canActivate: [subscriptionFeatureGuard('ab_test')],
        loadComponent: () => import('./features/analytics/ab-test-dashboard.component').then(m => m.AbTestDashboardComponent)
      },

      // Finance & Tools Modules
      {
        path: 'finance/ai-analysis',
        canActivate: [subscriptionFeatureGuard('financial_ai')],
        loadComponent: () => import('./features/finance/financial-ai-analysis.component').then(m => m.FinancialAiAnalysisComponent)
      },
      {
        path: 'tools/vault',
        canActivate: [subscriptionFeatureGuard('shop_vault')],
        loadComponent: () => import('./features/tools/shop-vault.component').then(m => m.ShopVaultComponent)
      },
      {
        path: 'tools/automation',
        canActivate: [subscriptionFeatureGuard('automation')],
        loadComponent: () => import('./features/tools/automation-scheduler.component').then(m => m.TaskSchedulerAutomationComponent)
      },
      {
        path: 'tools/batch',
        canActivate: [subscriptionFeatureGuard('batch')],
        loadComponent: () => import('./features/tools/batch-queue.component').then(m => m.BatchQueueComponent)
      },
      {
        path: 'tools/tracking',
        loadComponent: () => import('./features/tools/live-tracking.component').then(m => m.LiveTrackingRadarComponent)
      },
      {
        path: 'tools/ai-usage',
        loadComponent: () => import('./features/tools/ai-usage-dashboard.component').then(m => m.AiUsageDashboardComponent)
      },

      // Shipping & Logistics Modules (4-Carrier Hub)
      {
        path: 'shipping/aras',
        canActivate: [subscriptionFeatureGuard('shipping_hub')],
        loadComponent: () => import('./features/shipping/shipping-hub.component').then(m => m.ShippingHubComponent)
      },
      {
        path: 'shipping/shipentegra',
        canActivate: [subscriptionFeatureGuard('shipping_hub')],
        loadComponent: () => import('./features/shipping/shipping-hub.component').then(m => m.ShippingHubComponent)
      },
      {
        path: 'shipping/navlungo',
        canActivate: [subscriptionFeatureGuard('shipping_hub')],
        loadComponent: () => import('./features/shipping/shipping-hub.component').then(m => m.ShippingHubComponent)
      },
      {
        path: 'shipping/shiptomore',
        canActivate: [subscriptionFeatureGuard('shipping_hub')],
        loadComponent: () => import('./features/shipping/shipping-hub.component').then(m => m.ShippingHubComponent)
      },

      // System & Settings Modules
      {
        path: 'settings/subscription',
        loadComponent: () => import('./features/settings/subscription-plans.component').then(m => m.SubscriptionPlansComponent)
      },
      {
        path: 'settings/ai-models',
        loadComponent: () => import('./features/settings/ai-models-settings.component').then(m => m.AiModelsSettingsComponent)
      },
      {
        path: 'settings/etsy-api',
        loadComponent: () => import('./features/settings/etsy-api-settings.component').then(m => m.EtsyApiSettingsComponent)
      },
      {
        path: 'settings/notifications',
        loadComponent: () => import('./features/settings/notification-settings.component').then(m => m.NotificationSettingsComponent)
      },
      {
        path: 'settings/update',
        loadComponent: () => import('./features/system/system-update.component').then(m => m.SystemUpdateComponent)
      },
      {
        path: 'system/logs',
        loadComponent: () => import('./features/system/system-logs.component').then(m => m.SystemLogsComponent)
      }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
