import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { LoginComponent } from './features/auth/login.component';
import { RegisterComponent } from './features/auth/register.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AdminComponent } from './features/admin/admin.component';
import { ModuleViewerComponent } from './features/module-viewer/module-viewer.component';
import { OrdersComponent } from './features/orders/orders.component';
import { AccountingComponent } from './features/finance/accounting.component';
import { FastCreatorComponent } from './features/listings/fast-creator.component';
import { AiStudioComponent } from './features/ai-studio/ai-studio.component';
import { ProfitCalculatorComponent } from './features/finance/profit-calculator.component';
import { Viral3DComponent } from './features/research/viral-3d.component';
import { CompetitorSpyComponent } from './features/research/competitor-spy.component';
import { ShopVaultComponent } from './features/tools/shop-vault.component';
import { EtsyApiSettingsComponent } from './features/settings/etsy-api-settings.component';
import { MarketResearchComponent } from './features/research/market-research.component';
import { AiAuditComponent } from './features/analytics/ai-audit.component';
import { ShippingHubComponent } from './features/shipping/shipping-hub.component';
import { SystemLogsComponent } from './features/system/system-logs.component';
import { authGuard, adminGuard } from './core/guards/auth.guard';

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
      { path: 'finance/accounting', component: AccountingComponent },
      { path: 'finance/profit', component: ProfitCalculatorComponent },
      { path: 'listings/fast-creator', component: FastCreatorComponent },
      { path: 'ai-studio', component: AiStudioComponent },

      // Research & Intelligence Modules
      { path: 'research/market', component: MarketResearchComponent },
      { path: 'research/viral-3d', component: Viral3DComponent },
      { path: 'research/competitor', component: CompetitorSpyComponent },
      { path: 'research/external', component: ModuleViewerComponent },

      // Analytics Modules
      { path: 'analytics/ai-audit', component: AiAuditComponent },
      { path: 'analytics/shop', component: ModuleViewerComponent },
      { path: 'analytics/ab-test', component: ModuleViewerComponent },

      // Finance & Tools Modules
      { path: 'finance/ai-analysis', component: ModuleViewerComponent },
      { path: 'tools/vault', component: ShopVaultComponent },
      { path: 'tools/automation', component: ModuleViewerComponent },
      { path: 'tools/batch', component: ModuleViewerComponent },
      { path: 'tools/tracking', component: ModuleViewerComponent },
      { path: 'tools/ai-usage', component: ModuleViewerComponent },

      // Shipping & Logistics Modules (4-Carrier Hub)
      { path: 'shipping/aras', component: ShippingHubComponent },
      { path: 'shipping/shipentegra', component: ShippingHubComponent },
      { path: 'shipping/navlungo', component: ShippingHubComponent },
      { path: 'shipping/shiptomore', component: ShippingHubComponent },

      // System & Settings Modules
      { path: 'settings/etsy-api', component: EtsyApiSettingsComponent },
      { path: 'settings/notifications', component: ModuleViewerComponent },
      { path: 'settings/update', component: ModuleViewerComponent },
      { path: 'system/logs', component: SystemLogsComponent }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
