import { Component, EventEmitter, Input, OnInit, Output, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { AiSettingsService, AiOptimizationSettings } from '../../core/services/ai-settings.service';

@Component({
  selector: 'app-ai-models-settings',
  standalone: true,
  imports: [CommonModule, FormsModule],
  template: `
    <div class="ai-hub-container" [class.is-modal-view]="isModal">
      <!-- HEADER -->
      <div class="hub-header">
        <div class="header-titles">
          <h2 class="hub-title">⚡ Yapay Zeka Optimizasyon & Model Merkezi</h2>
          <p class="hub-subtitle">
            Birincil LLM motoru, API anahtarları, görsel tasarım entegrasyonları ve kesintisiz çalışma politikaları.
          </p>
        </div>
        <button *ngIf="isModal" class="btn-close-modal" (click)="closeModal()" title="Kapat">✕</button>
      </div>

      <!-- SCROLLABLE BODY -->
      <div class="hub-body">
        
        <!-- CARD 1: BİRİNCİL METİN / AKIL YÜRÜTME MOTORU (LLM) -->
        <div class="hub-card">
          <div class="card-head">
            <h3 class="card-title">🧠 Birincil Metin / Akıl Yürütme Motoru (LLM)</h3>
            <p class="card-desc">Etsy ürün başlığı, açıklaması ve anahtar kelime üretiminde kullanılan birincil zeka motoru.</p>
          </div>

          <!-- Aktif Sağlayıcı Seçimi (Buttons Bar) -->
          <div class="form-row">
            <label class="row-label">Aktif Sağlayıcı:</label>
            <div class="provider-pill-grid">
              <button 
                type="button"
                class="provider-pill gemini" 
                [class.active]="formSettings.provider === 'Gemini'"
                (click)="selectProvider('Gemini')">
                <span class="pill-icon">🔵</span>
                <span class="pill-name">Google Gemini</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'Gemini'">✓</span>
              </button>

              <button 
                type="button"
                class="provider-pill openai" 
                [class.active]="formSettings.provider === 'OpenAI'"
                (click)="selectProvider('OpenAI')">
                <span class="pill-icon">🟢</span>
                <span class="pill-name">OpenAI (GPT)</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'OpenAI'">✓</span>
              </button>

              <button 
                type="button"
                class="provider-pill deepseek" 
                [class.active]="formSettings.provider === 'DeepSeek'"
                (click)="selectProvider('DeepSeek')">
                <span class="pill-icon">🔴</span>
                <span class="pill-name">DeepSeek</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'DeepSeek'">✓</span>
              </button>

              <button 
                type="button"
                class="provider-pill claude" 
                [class.active]="formSettings.provider === 'Claude'"
                (click)="selectProvider('Claude')">
                <span class="pill-icon">🟣</span>
                <span class="pill-name">Claude</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'Claude'">✓</span>
              </button>

              <button 
                type="button"
                class="provider-pill grok" 
                [class.active]="formSettings.provider === 'Grok'"
                (click)="selectProvider('Grok')">
                <span class="pill-icon">🟠</span>
                <span class="pill-name">xAI Grok</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'Grok'">✓</span>
              </button>

              <button 
                type="button"
                class="provider-pill offline" 
                [class.active]="formSettings.provider === 'Offline'"
                (click)="selectProvider('Offline')">
                <span class="pill-icon">⚡</span>
                <span class="pill-name">Offline Motor</span>
                <span class="pill-check" *ngIf="formSettings.provider === 'Offline'">✓</span>
              </button>
            </div>
          </div>

          <!-- Model & Özel Model Girişi -->
          <div class="form-row model-row" *ngIf="formSettings.provider !== 'Offline'">
            <div class="field-group">
              <label class="row-label">LLM Model:</label>
              <select [(ngModel)]="currentSelectedModel" (change)="onModelDropdownChange()" class="hub-select">
                <option *ngFor="let m of getAvailableModels()" [value]="m">{{ m }}</option>
              </select>
            </div>

            <div class="field-group custom-field">
              <label class="row-label">veya Özel Model:</label>
              <input 
                type="text" 
                [(ngModel)]="formSettings.customModel" 
                placeholder="Örn: gemini-2.5-flash, grok-3, o3-mini..." 
                class="hub-input" />
            </div>
          </div>

          <!-- Görsel Modeli -->
          <div class="form-row" *ngIf="formSettings.provider !== 'Offline'">
            <div class="field-group">
              <label class="row-label">Görsel Modeli:</label>
              <select [(ngModel)]="currentSelectedImageModel" class="hub-select">
                <option *ngFor="let im of getAvailableImageModels()" [value]="im">{{ im }}</option>
              </select>
            </div>
          </div>

          <!-- API Anahtarı -->
          <div class="form-row" *ngIf="formSettings.provider !== 'Offline'">
            <label class="row-label">API Anahtarı:</label>
            <div class="input-with-toggle">
              <input 
                [type]="showKey ? 'text' : 'password'" 
                [(ngModel)]="currentSelectedApiKey"
                placeholder="sk-... veya AIzaSy... API anahtarını girin"
                class="hub-input key-input" />
              <button type="button" class="btn-eye" (click)="showKey = !showKey">
                {{ showKey ? '🙈' : '👁️' }}
              </button>
            </div>
          </div>
        </div>

        <!-- CARD 2: GÖRSEL ÜRETİM & ARKA PLAN AI MOTORLARI (VISION STUDIO) -->
        <div class="hub-card">
          <div class="card-head">
            <h3 class="card-title">📸 Görsel Üretim & Arka Plan AI Motorları (Vision Studio)</h3>
            <p class="card-desc">Ürün fotoğraflarının arka planını şeffaflaştırmak (dekupe) veya 3D modelleri fotoğrafik renderlamak için kullanılır.</p>
          </div>

          <div class="vision-grid">
            <div class="vision-field">
              <label class="vision-label">PhotoRoom API Key (Dekupe & Arka Plan):</label>
              <div class="input-with-toggle">
                <input 
                  [type]="showPhotoRoomKey ? 'text' : 'password'" 
                  [(ngModel)]="formSettings.photoRoomApiKey" 
                  placeholder="pr_live_... veya PhotoRoom anahtarı"
                  class="hub-input" />
                <button type="button" class="btn-eye" (click)="showPhotoRoomKey = !showPhotoRoomKey">
                  {{ showPhotoRoomKey ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>

            <div class="vision-field">
              <label class="vision-label">Black Forest Labs (FLUX 1.1 Pro):</label>
              <div class="input-with-toggle">
                <input 
                  [type]="showBflKey ? 'text' : 'password'" 
                  [(ngModel)]="formSettings.bflApiKey" 
                  placeholder="bfl_... FLUX API anahtarı"
                  class="hub-input" />
                <button type="button" class="btn-eye" (click)="showBflKey = !showBflKey">
                  {{ showBflKey ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>

            <div class="vision-field">
              <label class="vision-label">Ideogram v4 (Tipografi & Logo):</label>
              <div class="input-with-toggle">
                <input 
                  [type]="showIdeogramKey ? 'text' : 'password'" 
                  [(ngModel)]="formSettings.ideogramApiKey" 
                  placeholder="ideogram_... API anahtarı"
                  class="hub-input" />
                <button type="button" class="btn-eye" (click)="showIdeogramKey = !showIdeogramKey">
                  {{ showIdeogramKey ? '🙈' : '👁️' }}
                </button>
              </div>
            </div>
          </div>
        </div>

        <!-- CARD 3: SİSTEM GÜVENLİĞİ & FALLBACK DAVRANIŞI -->
        <div class="hub-card">
          <div class="card-head">
            <h3 class="card-title">🛡️ Çevrimdışı (Offline) / Kotasız Hata Davranışı</h3>
            <p class="card-desc">API kotası bittiğinde veya bağlantı kesildiğinde sistemin nasıl tepki vereceğini yapılandırın.</p>
          </div>

          <div class="checkbox-group">
            <label class="checkbox-label">
              <input type="checkbox" [(ngModel)]="formSettings.allowSilentOfflineFallback" />
              <span class="checkbox-text">
                <strong>Hata Anında Sessizce Çevrimdışı (Offline) Kural Motoruna Geç</strong>
                <small>Canlı AI yanıt vermezse veya 503/429 hatası alınırsa kesintisiz çalışmayı sürdürür.</small>
              </span>
            </label>

            <label class="checkbox-label">
              <input type="checkbox" [(ngModel)]="formSettings.strictNeverOffline" />
              <span class="checkbox-text">
                <strong>Canlı AI Zorunlu: Asla Offline Kural Motoruna Düşme</strong>
                <small>Canlı AI yanıt vermezse kullanıcıya açıkça hata bildirir; asla sentetik offline şablon kullanmaz.</small>
              </span>
            </label>
          </div>
        </div>
      </div>

      <!-- FOOTER: EYLEMLER & TERMİNAL LOG KUTUSU -->
      <div class="hub-footer">
        <div class="footer-left-buttons">
          <button type="button" class="btn-save-ai" (click)="saveAndActivate()">
            💾 Kaydet & Aktifleştir
          </button>
          
          <button type="button" class="btn-test-ai" [disabled]="isTesting" (click)="runQuickApiTest()">
            <span>{{ isTesting ? '⏳ Test Ediliyor...' : '⚡ Hızlı API Testi' }}</span>
          </button>

          <span class="save-toast" *ngIf="showToast">
            ✅ Ayarlar başarıyla kaydedildi ve tüm sisteme aktarıldı!
          </span>
        </div>

        <!-- Terminal Log Box (Matching Desktop Green Monospace Console) -->
        <div class="terminal-log-box">
          <div class="terminal-header">
            <span class="terminal-title">📟 SİSTEM GÜNLÜĞÜ & BAĞLANTI KONSOLU</span>
            <span class="terminal-dot green"></span>
          </div>
          <div class="terminal-content">
            <div *ngFor="let log of terminalLogs" class="terminal-line" [class.success]="log.includes('başarıyla')" [class.error]="log.includes('Hata:')">
              {{ log }}
            </div>
          </div>
        </div>
      </div>
    </div>
  `,
  styles: [`
    .ai-hub-container {
      background: #090d16;
      color: #f1f5f9;
      min-height: calc(100vh - 70px);
      padding: 24px 32px;
      font-family: -apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, sans-serif;
      box-sizing: border-box;
      display: flex;
      flex-direction: column;
      gap: 18px;
    }

    .ai-hub-container.is-modal-view {
      min-height: auto;
      padding: 20px 24px;
      max-height: 88vh;
      overflow-y: auto;
      background: #0f172a;
      border-radius: 16px;
    }

    .hub-header {
      display: flex;
      justify-content: space-between;
      align-items: flex-start;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 14px;
    }
    .hub-title {
      font-size: 1.35rem;
      font-weight: 800;
      color: #f8fafc;
      margin: 0 0 4px 0;
      letter-spacing: -0.3px;
    }
    .hub-subtitle {
      font-size: 0.82rem;
      color: #94a3b8;
      margin: 0;
    }
    .btn-close-modal {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.2rem;
      cursor: pointer;
      padding: 4px 8px;
      border-radius: 6px;
      transition: all 0.15s;
    }
    .btn-close-modal:hover {
      background: rgba(255, 255, 255, 0.1);
      color: #fff;
    }

    .hub-body {
      display: flex;
      flex-direction: column;
      gap: 16px;
    }

    .hub-card {
      background: #111a2e;
      border: 1px solid rgba(255, 255, 255, 0.08);
      border-radius: 12px;
      padding: 18px 20px;
      display: flex;
      flex-direction: column;
      gap: 14px;
    }

    .card-head {
      border-bottom: 1px solid rgba(255, 255, 255, 0.06);
      padding-bottom: 8px;
    }
    .card-title {
      font-size: 0.96rem;
      font-weight: 700;
      color: #38bdf8;
      margin: 0 0 4px 0;
    }
    .card-desc {
      font-size: 0.78rem;
      color: #94a3b8;
      margin: 0;
    }

    .form-row {
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .row-label {
      font-size: 0.8rem;
      font-weight: 700;
      color: #cbd5e1;
    }

    .provider-pill-grid {
      display: grid;
      grid-template-columns: repeat(6, 1fr);
      gap: 10px;
    }
    .provider-pill {
      background: #182238;
      border: 1px solid #26334d;
      border-radius: 8px;
      padding: 10px 8px;
      color: #cbd5e1;
      font-size: 0.8rem;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      gap: 6px;
      transition: all 0.15s;
    }
    .provider-pill:hover {
      background: #1e2c47;
      border-color: #3b82f6;
    }
    .provider-pill.active {
      border-color: #38bdf8;
      box-shadow: 0 0 12px rgba(56, 189, 248, 0.3);
    }
    .provider-pill.active.gemini { background: rgba(59, 130, 246, 0.2); border-color: #3b82f6; color: #60a5fa; }
    .provider-pill.active.openai { background: rgba(16, 185, 129, 0.2); border-color: #10b981; color: #34d399; }
    .provider-pill.active.deepseek { background: rgba(239, 68, 68, 0.2); border-color: #ef4444; color: #f87171; }
    .provider-pill.active.claude { background: rgba(168, 85, 247, 0.2); border-color: #a855f7; color: #c084fc; }
    .provider-pill.active.grok { background: rgba(249, 115, 22, 0.2); border-color: #f97316; color: #fb923c; }
    .provider-pill.active.offline { background: rgba(100, 116, 139, 0.2); border-color: #64748b; color: #94a3b8; }

    .pill-check {
      font-size: 0.85rem;
      font-weight: 900;
    }

    .model-row {
      display: grid;
      grid-template-columns: 1fr 1fr;
      gap: 16px;
    }
    .field-group {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }

    .hub-select, .hub-input {
      background: #090e1a;
      border: 1px solid #26334d;
      border-radius: 8px;
      padding: 9px 12px;
      color: #f1f5f9;
      font-size: 0.82rem;
      outline: none;
      transition: border-color 0.15s;
      width: 100%;
      box-sizing: border-box;
    }
    .hub-select:focus, .hub-input:focus {
      border-color: #38bdf8;
      box-shadow: 0 0 8px rgba(56, 189, 248, 0.25);
    }

    .input-with-toggle {
      display: flex;
      position: relative;
    }
    .key-input {
      padding-right: 40px;
    }
    .btn-eye {
      position: absolute;
      right: 8px;
      top: 50%;
      transform: translateY(-50%);
      background: transparent;
      border: none;
      cursor: pointer;
      font-size: 1rem;
      padding: 4px;
    }

    .vision-grid {
      display: grid;
      grid-template-columns: 1fr 1fr 1fr;
      gap: 16px;
    }
    .vision-field {
      display: flex;
      flex-direction: column;
      gap: 6px;
    }
    .vision-label {
      font-size: 0.76rem;
      font-weight: 700;
      color: #cbd5e1;
    }

    .checkbox-group {
      display: flex;
      flex-direction: column;
      gap: 12px;
    }
    .checkbox-label {
      display: flex;
      align-items: flex-start;
      gap: 10px;
      cursor: pointer;
    }
    .checkbox-label input {
      margin-top: 3px;
      accent-color: #38bdf8;
      cursor: pointer;
    }
    .checkbox-text {
      display: flex;
      flex-direction: column;
      gap: 2px;
    }
    .checkbox-text strong {
      font-size: 0.82rem;
      color: #f1f5f9;
    }
    .checkbox-text small {
      font-size: 0.74rem;
      color: #94a3b8;
    }

    /* FOOTER */
    .hub-footer {
      display: grid;
      grid-template-columns: 1fr 1.2fr;
      gap: 20px;
      margin-top: 6px;
      padding-top: 14px;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      align-items: flex-start;
    }

    .footer-left-buttons {
      display: flex;
      flex-direction: column;
      gap: 10px;
    }

    .btn-save-ai {
      background: #10b981;
      border: none;
      border-radius: 8px;
      padding: 12px 20px;
      color: #fff;
      font-weight: 700;
      font-size: 0.88rem;
      cursor: pointer;
      transition: all 0.15s;
      box-shadow: 0 4px 14px rgba(16, 185, 129, 0.3);
    }
    .btn-save-ai:hover {
      background: #059669;
      transform: translateY(-1px);
    }

    .btn-test-ai {
      background: #3b82f6;
      border: none;
      border-radius: 8px;
      padding: 10px 18px;
      color: #fff;
      font-weight: 700;
      font-size: 0.84rem;
      cursor: pointer;
      transition: all 0.15s;
    }
    .btn-test-ai:hover {
      background: #2563eb;
    }
    .btn-test-ai:disabled {
      opacity: 0.6;
      cursor: not-allowed;
    }

    .save-toast {
      font-size: 0.78rem;
      color: #34d399;
      background: rgba(16, 185, 129, 0.15);
      border: 1px solid rgba(16, 185, 129, 0.3);
      padding: 8px 12px;
      border-radius: 6px;
      animation: fade-in 0.2s;
    }

    /* TERMINAL */
    .terminal-log-box {
      background: #050811;
      border: 1px solid #1e293b;
      border-radius: 8px;
      padding: 10px 14px;
      font-family: Consolas, "Courier New", monospace;
      font-size: 0.75rem;
      display: flex;
      flex-direction: column;
      gap: 6px;
      min-height: 95px;
    }
    .terminal-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      border-bottom: 1px solid #1e293b;
      padding-bottom: 4px;
    }
    .terminal-title {
      color: #64748b;
      font-weight: 700;
      font-size: 0.7rem;
    }
    .terminal-dot.green {
      width: 8px;
      height: 8px;
      border-radius: 50%;
      background: #10b981;
      box-shadow: 0 0 6px #10b981;
    }
    .terminal-content {
      max-height: 90px;
      overflow-y: auto;
      display: flex;
      flex-direction: column;
      gap: 3px;
    }
    .terminal-line {
      color: #a7f3d0;
      white-space: pre-wrap;
      word-break: break-all;
    }
    .terminal-line.error {
      color: #f87171;
    }

    @keyframes fade-in {
      from { opacity: 0; transform: translateY(-4px); }
      to { opacity: 1; transform: translateY(0); }
    }
  `]
})
export class AiModelsSettingsComponent implements OnInit {
  @Input() isModal = false;
  @Output() modalClosed = new EventEmitter<void>();

  private aiService = inject(AiSettingsService);

  formSettings: AiOptimizationSettings = { ...this.aiService.settings() };

  showKey = false;
  showPhotoRoomKey = false;
  showBflKey = false;
  showIdeogramKey = false;
  isTesting = false;
  showToast = false;

  terminalLogs: string[] = [];

  readonly geminiModels = ['gemini-2.5-flash', 'gemini-2.5-pro', 'gemini-2.0-flash', 'gemini-1.5-flash-latest', 'gemini-1.5-pro-latest'];
  readonly openAiModels = ['gpt-4o', 'gpt-4o-mini', 'o1-preview', 'o3-mini', 'gpt-4-turbo'];
  readonly claudeModels = ['claude-3-7-sonnet-20250219', 'claude-3-5-sonnet-20241022', 'claude-3-5-haiku-20241022'];
  readonly deepSeekModels = ['deepseek-reasoner', 'deepseek-chat'];
  readonly grokModels = ['grok-3', 'grok-2', 'grok-beta'];

  readonly geminiImageModels = ['gemini-2.5-flash-image', 'imagen-3.0-generate-002'];
  readonly openAiImageModels = ['gpt-image-2.5-flare', 'dall-e-3', 'dall-e-2'];
  readonly generalImageModels = ['flux-pro-1.1', 'ideogram-v4'];

  ngOnInit(): void {
    this.formSettings = { ...this.aiService.settings() };
    const now = new Date().toLocaleTimeString('tr-TR');
    this.terminalLogs.push(`${now} - Ayar dosyası başarıyla yüklendi: ai-optimization-settings.json`);
    this.terminalLogs.push(`${now} - Aktif Sağlayıcı: ${this.formSettings.provider} (${this.aiService.activeModelName()})`);
  }

  selectProvider(prov: AiOptimizationSettings['provider']): void {
    this.formSettings.provider = prov;
    const now = new Date().toLocaleTimeString('tr-TR');
    this.terminalLogs.push(`${now} - Sağlayıcı değiştirildi: ${prov}`);
  }

  get currentSelectedModel(): string {
    switch (this.formSettings.provider) {
      case 'Gemini': return this.formSettings.geminiModel;
      case 'OpenAI': return this.formSettings.openAiModel;
      case 'Claude': return this.formSettings.claudeModel;
      case 'DeepSeek': return this.formSettings.deepSeekModel;
      case 'Grok': return this.formSettings.grokModel;
      default: return 'offline';
    }
  }

  set currentSelectedModel(val: string) {
    switch (this.formSettings.provider) {
      case 'Gemini': this.formSettings.geminiModel = val; break;
      case 'OpenAI': this.formSettings.openAiModel = val; break;
      case 'Claude': this.formSettings.claudeModel = val; break;
      case 'DeepSeek': this.formSettings.deepSeekModel = val; break;
      case 'Grok': this.formSettings.grokModel = val; break;
    }
  }

  get currentSelectedImageModel(): string {
    switch (this.formSettings.provider) {
      case 'Gemini': return this.formSettings.geminiImageModel;
      case 'OpenAI': return this.formSettings.openAiImageModel;
      default: return 'flux-pro-1.1';
    }
  }

  set currentSelectedImageModel(val: string) {
    switch (this.formSettings.provider) {
      case 'Gemini': this.formSettings.geminiImageModel = val; break;
      case 'OpenAI': this.formSettings.openAiImageModel = val; break;
    }
  }

  get currentSelectedApiKey(): string {
    switch (this.formSettings.provider) {
      case 'Gemini': return this.formSettings.geminiApiKey;
      case 'OpenAI': return this.formSettings.openAiApiKey;
      case 'Claude': return this.formSettings.claudeApiKey;
      case 'DeepSeek': return this.formSettings.deepSeekApiKey;
      case 'Grok': return this.formSettings.grokApiKey;
      default: return '';
    }
  }

  set currentSelectedApiKey(val: string) {
    switch (this.formSettings.provider) {
      case 'Gemini': this.formSettings.geminiApiKey = val; break;
      case 'OpenAI': this.formSettings.openAiApiKey = val; break;
      case 'Claude': this.formSettings.claudeApiKey = val; break;
      case 'DeepSeek': this.formSettings.deepSeekApiKey = val; break;
      case 'Grok': this.formSettings.grokApiKey = val; break;
    }
  }

  getAvailableModels(): string[] {
    switch (this.formSettings.provider) {
      case 'Gemini': return this.geminiModels;
      case 'OpenAI': return this.openAiModels;
      case 'Claude': return this.claudeModels;
      case 'DeepSeek': return this.deepSeekModels;
      case 'Grok': return this.grokModels;
      default: return ['offline-heuristic'];
    }
  }

  getAvailableImageModels(): string[] {
    switch (this.formSettings.provider) {
      case 'Gemini': return this.geminiImageModels;
      case 'OpenAI': return this.openAiImageModels;
      default: return this.generalImageModels;
    }
  }

  onModelDropdownChange(): void {
    const now = new Date().toLocaleTimeString('tr-TR');
    this.terminalLogs.push(`${now} - Model seçildi: ${this.currentSelectedModel}`);
  }

  async runQuickApiTest(): Promise<void> {
    this.isTesting = true;
    const now = new Date().toLocaleTimeString('tr-TR');
    const prov = this.formSettings.provider;
    const key = this.currentSelectedApiKey;
    const model = this.formSettings.customModel || this.currentSelectedModel;

    this.terminalLogs.push(`${now} - [TEST BAŞLATILDI] ${prov} (${model}) ping isteği gönderiliyor...`);

    const result = await this.aiService.testApiConnection(prov, key, model);
    const endNow = new Date().toLocaleTimeString('tr-TR');
    this.terminalLogs.push(`${endNow} - ${result.message}`);
    this.isTesting = false;
  }

  saveAndActivate(): void {
    this.aiService.saveSettings(this.formSettings);
    const now = new Date().toLocaleTimeString('tr-TR');
    this.terminalLogs.push(`${now} - [BAŞARILI] AI konfigürasyonu kaydedildi ve global olarak aktifleştirildi.`);
    this.terminalLogs.push(`${now} - Yeni Aktif Rozet: ${this.aiService.activeBadgeText()}`);

    this.showToast = true;
    setTimeout(() => {
      this.showToast = false;
      if (this.isModal) {
        this.closeModal();
      }
    }, 1800);
  }

  closeModal(): void {
    this.aiService.closeAiSettingsModal();
    this.modalClosed.emit();
  }
}
