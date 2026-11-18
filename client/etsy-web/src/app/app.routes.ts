import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { LoginComponent } from './features/auth/login.component';
import { RegisterComponent } from './features/auth/register.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AdminComponent } from './features/admin/admin.component';
import { OrdersComponent } from './features/orders/orders.component';
import { AccountingComponent } from './features/finance/accounting.component';
import { FastCreatorComponent } from './features/listings/fast-creator.component';
import { ProductDiscoveryComponent } from './features/listings/product-discovery.component';
import { ProductCostManagerComponent } from './features/finance/product-cost-manager.component';
import { AiStudioComponent } from './features/ai-studio/ai-studio.component';
import { ProfitCalculatorComponent } from './features/finance/profit-calculator.component';
import { Viral3DComponent } from './features/research/viral-3d.component';
import { CompetitorSpyComponent } from './features/research/competitor-spy.component';
import { ShopVaultComponent } from './features/tools/shop-vault.component';
import { EtsyApiSettingsComponent } from './features/settings/etsy-api-settings.component';
import { AiModelsSettingsComponent } from './features/settings/ai-models-settings.component';
import { MarketResearchComponent } from './features/research/market-research.component';
import { ShopResearchAgentComponent } from './features/research/shop-research-agent.component';
import { AiAuditComponent } from './features/analytics/ai-audit.component';
import { ShippingHubComponent } from './features/shipping/shipping-hub.component';
import { SystemLogsComponent } from './features/system/system-logs.component';

// 10 Parity Standalone Components
import { AbTestDashboardComponent } from './features/analytics/ab-test-dashboard.component';
import { AiUsageDashboardComponent } from './features/tools/ai-usage-dashboard.component';
import { BatchQueueComponent } from './features/tools/batch-queue.component';
import { LiveTrackingRadarComponent } from './features/tools/live-tracking.component';
import { ShopPerformanceComponent } from './features/analytics/shop-performance.component';
import { NotificationSettingsComponent } from './features/settings/notification-settings.component';
import { SystemUpdateComponent } from './features/system/system-update.component';
import { FinancialAiAnalysisComponent } from './features/finance/financial-ai-analysis.component';
import { ExternalArbitrageRadarComponent } from './features/research/external-arbitrage.component';
import { TaskSchedulerAutomationComponent } from './features/tools/automation-scheduler.component';
import { SubscriptionPlansComponent } from './features/settings/subscription-plans.component';

import { authGuard, adminGuard } from './core/guards/auth.guard';
import { subscriptionFeatureGuard } from './core/guards/subscription.guard';

export const routes: Routes = [
  // Public Auth Routes
  { path: 'auth/login', component: LoginComponent },
  { path: 'auth/register', component: RegisterComponent },

  // Protected App Shell Routes
  {
    path: '',
    component: ShellComponent,
    canActivate: [authGuard],
    children: [
      { path: '', redirectTo: 'dashboard', pathMatch: 'full' },
      { path: 'dashboard', component: DashboardComponent },
      { path: 'admin', component: AdminComponent, canActivate: [adminGuard] },

      // Core Interactive Modules
      { path: 'orders', component: OrdersComponent },
      { path: 'finance/accounting', component: AccountingComponent, canActivate: [subscriptionFeatureGuard('financial')] },
      { path: 'finance/profit', component: ProfitCalculatorComponent },
      { path: 'finance/product-costs', component: ProductCostManagerComponent },
      { path: 'listings/fast-creator', component: FastCreatorComponent },
      { path: 'listings/creator', redirectTo: 'listings/fast-creator', pathMatch: 'full' },
      { path: 'listings/discovery', component: ProductDiscoveryComponent },
      { path: 'ai-studio', component: AiStudioComponent, canActivate: [subscriptionFeatureGuard('ai_image')] },

      // Research & Intelligence Modules
      { path: 'research/agent', component: ShopResearchAgentComponent },
      { path: 'research/market', component: MarketResearchComponent },
      { path: 'research/viral-3d', component: Viral3DComponent, canActivate: [subscriptionFeatureGuard('viral_3d')] },
      { path: 'research/competitor', component: CompetitorSpyComponent, canActivate: [subscriptionFeatureGuard('competitor_spy')] },
      { path: 'research/external', component: ExternalArbitrageRadarComponent, canActivate: [subscriptionFeatureGuard('external')] },

      // Analytics Modules
      { path: 'analytics/ai-audit', component: AiAuditComponent, canActivate: [subscriptionFeatureGuard('ai_audit')] },
      { path: 'analytics/shop', component: ShopPerformanceComponent },
      { path: 'analytics/ab-test', component: AbTestDashboardComponent, canActivate: [subscriptionFeatureGuard('ab_test')] },

      // Finance & Tools Modules
      { path: 'finance/ai-analysis', component: FinancialAiAnalysisComponent, canActivate: [subscriptionFeatureGuard('financial_ai')] },
      { path: 'tools/vault', component: ShopVaultComponent, canActivate: [subscriptionFeatureGuard('shop_vault')] },
      { path: 'tools/automation', component: TaskSchedulerAutomationComponent, canActivate: [subscriptionFeatureGuard('automation')] },
      { path: 'tools/batch', component: BatchQueueComponent, canActivate: [subscriptionFeatureGuard('batch')] },
      { path: 'tools/tracking', component: LiveTrackingRadarComponent },
      { path: 'tools/ai-usage', component: AiUsageDashboardComponent },

      // Shipping & Logistics Modules (4-Carrier Hub)
      { path: 'shipping/aras', component: ShippingHubComponent, canActivate: [subscriptionFeatureGuard('shipping_hub')] },
      { path: 'shipping/shipentegra', component: ShippingHubComponent, canActivate: [subscriptionFeatureGuard('shipping_hub')] },
      { path: 'shipping/navlungo', component: ShippingHubComponent, canActivate: [subscriptionFeatureGuard('shipping_hub')] },
      { path: 'shipping/shiptomore', component: ShippingHubComponent, canActivate: [subscriptionFeatureGuard('shipping_hub')] },

      // System & Settings Modules
      { path: 'settings/subscription', component: SubscriptionPlansComponent },
      { path: 'settings/ai-models', component: AiModelsSettingsComponent },
      { path: 'settings/etsy-api', component: EtsyApiSettingsComponent },
      { path: 'settings/notifications', component: NotificationSettingsComponent },
      { path: 'settings/update', component: SystemUpdateComponent },
      { path: 'system/logs', component: SystemLogsComponent }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
