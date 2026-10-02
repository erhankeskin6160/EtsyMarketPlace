import { Routes } from '@angular/router';
import { ShellComponent } from './layout/shell/shell.component';
import { LoginComponent } from './features/auth/login.component';
import { RegisterComponent } from './features/auth/register.component';
import { DashboardComponent } from './features/dashboard/dashboard.component';
import { AdminComponent } from './features/admin/admin.component';
import { ModuleViewerComponent } from './features/module-viewer/module-viewer.component';
import { OrdersComponent } from './features/orders/orders.component';
import { AccountingComponent } from './features/finance/accounting.component';
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

      // Desktop Modules
      { path: 'listings/fast-creator', component: ModuleViewerComponent },
      { path: 'listings/creator', component: ModuleViewerComponent },
      { path: 'ai-studio', component: ModuleViewerComponent },
      { path: 'analytics/shop', component: ModuleViewerComponent },

      { path: 'research/market', component: ModuleViewerComponent },
      { path: 'research/viral-3d', component: ModuleViewerComponent },
      { path: 'research/competitor', component: ModuleViewerComponent },
      { path: 'research/external', component: ModuleViewerComponent },
      { path: 'analytics/ai-audit', component: ModuleViewerComponent },
      { path: 'analytics/ab-test', component: ModuleViewerComponent },

      { path: 'finance/ai-analysis', component: ModuleViewerComponent },
      { path: 'finance/profit', component: ModuleViewerComponent },
      { path: 'tools/automation', component: ModuleViewerComponent },
      { path: 'tools/batch', component: ModuleViewerComponent },
      { path: 'tools/vault', component: ModuleViewerComponent },
      { path: 'tools/tracking', component: ModuleViewerComponent },
      { path: 'tools/ai-usage', component: ModuleViewerComponent },

      { path: 'shipping/aras', component: ModuleViewerComponent },
      { path: 'shipping/shipentegra', component: ModuleViewerComponent },
      { path: 'shipping/navlungo', component: ModuleViewerComponent },
      { path: 'shipping/shiptomore', component: ModuleViewerComponent },

      { path: 'settings/notifications', component: ModuleViewerComponent },
      { path: 'settings/etsy-api', component: ModuleViewerComponent },
      { path: 'settings/update', component: ModuleViewerComponent },
      { path: 'system/logs', component: ModuleViewerComponent }
    ]
  },

  { path: '**', redirectTo: 'dashboard' }
];
