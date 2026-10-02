import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';

export interface LogEntry {
  timestamp: string;
  level: 'INFO' | 'WARN' | 'ERROR' | 'SUCCESS';
  module: string;
  message: string;
}

@Component({
  selector: 'app-system-logs',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="logs-view">
      <!-- HEADER -->
      <div class="logs-header">
        <div class="header-left">
          <div class="icon-box">
            <svg width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="2">
              <path d="M14 2H6a2 2 0 0 0-2 2v16a2 2 0 0 0 2 2h12a2 2 0 0 0 2-2V8z"></path>
              <polyline points="14 2 14 8 20 8"></polyline>
              <line x1="16" y1="13" x2="8" y2="13"></line>
              <line x1="16" y1="17" x2="8" y2="17"></line>
              <polyline points="10 9 9 9 8 9"></polyline>
            </svg>
          </div>
          <div>
            <h1 class="page-title">VDS Sistem Günlüğü & Denetim Kayıtları</h1>
            <p class="page-subtitle">VDS sunucusundaki arka plan servisleri, token yenilemeleri ve API kuyruk akışı</p>
          </div>
        </div>

        <div class="header-right">
          <button class="btn-export" (click)="exportLogs()">
            💾 Logları Dışa Aktar (.log)
          </button>
          <button class="btn-clear" (click)="clearLogs()">
            🗑️ Temizle
          </button>
        </div>
      </div>

      <!-- SERVER STATS STRIP -->
      <div class="stats-strip">
        <div class="glass-card stat-card">
          <span class="stat-label">VDS SUNUCU IP</span>
          <span class="stat-val font-mono text-cyan">5.180.81.148:5263</span>
          <span class="stat-sub">🟢 Sistem Çalışıyor (Uptime: 99.98%)</span>
        </div>

        <div class="glass-card stat-card">
          <span class="stat-label">SQLITE VERİTABANI</span>
          <span class="stat-val text-emerald">18.4 MB</span>
          <span class="stat-sub">EtsyMarketPlace.db (WAL Modu Aktif)</span>
        </div>

        <div class="glass-card stat-card">
          <span class="stat-label">AI TOKEN KULLANIMI</span>
          <span class="stat-val text-purple">124.500 / 2.000.000</span>
          <span class="stat-sub">Gemini Spark Pro Kotası</span>
        </div>

        <div class="glass-card stat-card">
          <span class="stat-label">ETSY OAUTH SAĞLIĞI</span>
          <span class="stat-val text-orange">Aktif (Auto-Refresh)</span>
          <span class="stat-sub">Sonraki Yenileme: 48 dk sonra</span>
        </div>
      </div>

      <!-- LOG VIEWER TERMINAL -->
      <div class="glass-card terminal-card">
        <div class="terminal-bar">
          <div class="terminal-title">
            <span class="dot red"></span>
            <span class="dot yellow"></span>
            <span class="dot green"></span>
            <span class="terminal-name">vds-api-stdout.log (Canlı Akış)</span>
          </div>

          <div class="terminal-filters">
            <label class="filter-chk">
              <input type="checkbox" [(ngModel)]="showInfo" />
              <span>INFO</span>
            </label>
            <label class="filter-chk">
              <input type="checkbox" [(ngModel)]="showWarn" />
              <span>WARN</span>
            </label>
            <label class="filter-chk">
              <input type="checkbox" [(ngModel)]="showError" />
              <span>ERROR</span>
            </label>
          </div>
        </div>

        <div class="terminal-body font-mono">
          <div *ngFor="let log of filteredLogs" class="log-line">
            <span class="log-time">{{ log.timestamp }}</span>
            <span class="log-badge" [ngClass]="log.level">{{ log.level }}</span>
            <span class="log-mod">[{{ log.module }}]</span>
            <span class="log-msg">{{ log.message }}</span>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .logs-view {
      padding: 24px;
      background: #0b0f19;
      color: #e2e8f0;
      min-height: calc(100vh - 70px);
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 20px;
    }
    .logs-header {
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
      background: linear-gradient(135deg, rgba(59, 130, 246, 0.2), rgba(37, 99, 235, 0.2));
      border: 1px solid rgba(59, 130, 246, 0.4);
      display: flex;
      align-items: center;
      justify-content: center;
      color: #3b82f6;
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
    .header-right {
      display: flex;
      gap: 10px;
    }
    .btn-export {
      background: #2563eb;
      border: none;
      color: #fff;
      padding: 10px 18px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.82rem;
      cursor: pointer;
    }
    .btn-clear {
      background: rgba(255, 255, 255, 0.08);
      border: 1px solid rgba(255, 255, 255, 0.15);
      color: #fff;
      padding: 10px 16px;
      border-radius: 8px;
      font-weight: 600;
      font-size: 0.82rem;
      cursor: pointer;
    }

    .stats-strip {
      display: grid;
      grid-template-columns: repeat(4, 1fr);
      gap: 16px;
    }
    .stat-card {
      background: rgba(17, 24, 39, 0.7);
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 14px;
      padding: 18px;
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .stat-label {
      font-size: 0.72rem;
      font-weight: 700;
      color: #94a3b8;
    }
    .stat-val {
      font-size: 1.25rem;
      font-weight: 800;
    }
    .stat-sub {
      font-size: 0.72rem;
      color: #64748b;
    }
    .text-cyan { color: #06b6d4; }
    .text-emerald { color: #10b981; }
    .text-purple { color: #a855f7; }
    .text-orange { color: #f97316; }

    .terminal-card {
      background: #050811;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 14px;
      overflow: hidden;
      display: flex;
      flex-direction: column;
      height: 480px;
    }
    .terminal-bar {
      background: #090d18;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding: 12px 18px;
      display: flex;
      justify-content: space-between;
      align-items: center;
    }
    .terminal-title {
      display: flex;
      align-items: center;
      gap: 8px;
    }
    .dot {
      width: 10px;
      height: 10px;
      border-radius: 50%;
    }
    .red { background: #ef4444; }
    .yellow { background: #f59e0b; }
    .green { background: #10b981; }
    .terminal-name {
      font-size: 0.8rem;
      color: #94a3b8;
      margin-left: 6px;
      font-family: monospace;
    }
    .terminal-filters {
      display: flex;
      gap: 14px;
    }
    .filter-chk {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.75rem;
      color: #cbd5e1;
      cursor: pointer;
    }

    .terminal-body {
      padding: 16px;
      overflow-y: auto;
      flex: 1;
      display: flex;
      flex-direction: column;
      gap: 8px;
      font-size: 0.8rem;
      line-height: 1.5;
    }
    .log-line {
      display: flex;
      gap: 12px;
      align-items: baseline;
    }
    .log-time {
      color: #64748b;
      min-width: 80px;
    }
    .log-badge {
      font-size: 0.65rem;
      font-weight: 800;
      padding: 2px 6px;
      border-radius: 4px;
      min-width: 55px;
      text-align: center;
    }
    .INFO { background: rgba(59, 130, 246, 0.2); color: #60a5fa; }
    .WARN { background: rgba(245, 158, 11, 0.2); color: #fbbf24; }
    .ERROR { background: rgba(239, 68, 68, 0.2); color: #f87171; }
    .SUCCESS { background: rgba(16, 185, 129, 0.2); color: #34d399; }
    .log-mod {
      color: #a855f7;
      min-width: 140px;
    }
    .log-msg {
      color: #f1f5f9;
      word-break: break-all;
    }
  `]
})
export class SystemLogsComponent implements OnInit {
  showInfo = true;
  showWarn = true;
  showError = true;

  logs: LogEntry[] = [
    { timestamp: '00:15:02', level: 'SUCCESS', module: 'ExchangeRateService', message: 'Canlı USD/TRY kuru 49.12 ₺ olarak open.er-api.com üzerinden başarıyla alındı.' },
    { timestamp: '00:14:48', level: 'INFO', module: 'EtsyAccessTokenHandler', message: 'Mağaza 53236321 için OAuth v3 token doğrulandı. Süre sonuna 48 dk kaldı.' },
    { timestamp: '00:13:20', level: 'INFO', module: 'OrderFulfillmentService', message: 'Sipariş #4188710928 detayları SQLite DB üzerinden başarıyla getirildi.' },
    { timestamp: '00:11:05', level: 'SUCCESS', module: 'ShopVaultService', message: '48 adet ilan ve 624 etiket başarıyla AES-256 ile şifrelendi (.etsyvault hazir).' },
    { timestamp: '00:08:42', level: 'INFO', module: 'GeminiSparkCopilot', message: 'Gemini 2.5 Flash asistan oturumu başlatıldı. JSON-RPC bağlantısı aktif.' },
    { timestamp: '00:05:11', level: 'WARN', module: 'ShippingRateCalculator', message: 'Shiptomore API yanıt süresi 420ms (ortalama 210ms üzerinde).' },
    { timestamp: '23:58:30', level: 'INFO', module: 'SqliteDatabaseInitializer', message: 'EtsyMarketPlace.db bütünlüğü kontrol edildi: 0 bozuk indeks, WAL modu sağlıklı.' }
  ];

  ngOnInit(): void {}

  get filteredLogs(): LogEntry[] {
    return this.logs.filter(l => {
      if (l.level === 'INFO' && !this.showInfo) return false;
      if (l.level === 'WARN' && !this.showWarn) return false;
      if (l.level === 'ERROR' && !this.showError) return false;
      return true;
    });
  }

  exportLogs(): void {
    const text = this.logs.map(l => `[${l.timestamp}] [${l.level}] [${l.module}] ${l.message}`).join('\n');
    const blob = new Blob([text], { type: 'text/plain' });
    const url = URL.createObjectURL(blob);
    const a = document.createElement('a');
    a.href = url;
    a.download = `VdsSystemLogs_${Date.now()}.log`;
    a.click();
    URL.revokeObjectURL(url);
  }

  clearLogs(): void {
    this.logs = [];
  }
}
