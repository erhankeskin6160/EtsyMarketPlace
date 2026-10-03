import { Component, OnInit } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { GeminiAiService, ChatMessage, GeminiConfig } from '../../core/services/gemini-ai.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

@Component({
  selector: 'app-floating-copilot',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <!-- FLOATING BUTTON -->
    <div class="floating-btn-wrap" *ngIf="!isOpen" (click)="toggleOpen()">
      <button class="copilot-fab" title="Gemini Spark AI Asistanı">
        <app-ai-logo provider="Gemini" [size]="20"></app-ai-logo>
        <span class="fab-label">Gemini AI Danışmanı</span>
      </button>
    </div>

    <!-- EXPANDED CHAT WINDOW -->
    <div class="copilot-window" *ngIf="isOpen">
      <div class="window-header">
        <div class="header-info">
          <app-ai-logo provider="Gemini" [size]="20"></app-ai-logo>
          <div>
            <h4 class="copilot-title">Gemini Spark AI Danışmanı</h4>
            <span class="copilot-sub">
              {{ currentConfig.model }} • Canlı Mağaza Zekası (1$ = {{ etsyApi.exchangeRate() }} ₺)
            </span>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-icon" (click)="toggleSettings()" title="Gemini API & Model Ayarları">⚙️</button>
          <button class="btn-icon" (click)="clearChat()" title="Sohbeti Temizle">🗑️</button>
          <button class="btn-close" (click)="toggleOpen()" title="Kapat">×</button>
        </div>
      </div>

      <!-- SETTINGS OVERLAY -->
      <div class="settings-overlay" *ngIf="showSettings">
        <div class="settings-card">
          <div class="settings-header">
            <h5>⚙️ Google Gemini LLM Ayarları</h5>
            <button class="btn-sub-close" (click)="toggleSettings()">✕</button>
          </div>
          <div class="form-group">
            <label>Google Gemini API Key (AIzaSy...):</label>
            <input 
              type="password" 
              [(ngModel)]="tempApiKey" 
              placeholder="AIzaSy..." 
              class="cfg-input" />
            <span class="hint-text">Google AI Studio'dan aldığınız anahtarı girebilirsiniz. Boş bırakılırsa yerel mağaza zekası devrede kalır.</span>
          </div>
          <div class="form-group">
            <label>Gemini Modeli:</label>
            <select [(ngModel)]="tempModel" class="cfg-select">
              <option value="gemini-2.5-flash">Gemini 2.5 Flash (En Hızlı & Zeki - Önerilen)</option>
              <option value="gemini-2.0-flash">Gemini 2.0 Flash</option>
              <option value="gemini-1.5-flash">Gemini 1.5 Flash</option>
              <option value="gemini-1.5-pro">Gemini 1.5 Pro</option>
            </select>
          </div>
          <div class="settings-actions">
            <button class="btn-save-cfg" (click)="saveSettings()">💾 Ayarları Kaydet</button>
          </div>
        </div>
      </div>

      <!-- MESSAGES STREAM -->
      <div class="messages-container" #scrollArea>
        <div *ngFor="let msg of messages" class="msg-bubble" [ngClass]="msg.sender">
          <div class="msg-author">{{ msg.sender === 'user' ? 'Siz' : 'Gemini Spark' }}</div>
          <div class="msg-text" [innerHTML]="msg.text"></div>
          <span class="msg-time">{{ msg.time }}</span>
        </div>

        <div *ngIf="isThinking" class="msg-bubble gemini thinking">
          <div class="typing-dots">
            <span></span><span></span><span></span>
          </div>
          <span class="thinking-text">Gemini düşünüyor ve mağazanızı analiz ediyor...</span>
        </div>
      </div>

      <!-- QUICK ACTION CHIPS -->
      <div class="quick-prompts">
        <button class="q-chip" (click)="sendPrompt('Bugünkü net kârım ve cirom nedir?')">💰 Net Kâr</button>
        <button class="q-chip" (click)="sendPrompt('Maliyeti eksik olan sipariş var mı?')">⚠️ Eksik Maliyet</button>
        <button class="q-chip" (click)="sendPrompt('Kâr farkı nedir, neden sipariş kârıyla mağaza net kârı farklı?')">❓ Kâr Farkı</button>
        <button class="q-chip" (click)="sendPrompt('Hangi viral 3D modelleri satmalıyım?')">🔥 Trend 3D</button>
      </div>

      <!-- INPUT BAR -->
      <div class="input-bar">
        <input 
          type="text" 
          [(ngModel)]="userInput" 
          (keyup.enter)="sendMessage()"
          placeholder="Mağazanızla veya e-ticaretle ilgili her şeyi sorun..." 
          class="chat-input" />
        <button class="btn-send" [disabled]="!userInput.trim() || isThinking" (click)="sendMessage()">
          ➤
        </button>
      </div>
    </div>
  `,
  styles: [`
    .floating-btn-wrap {
      position: fixed;
      bottom: 24px;
      right: 28px;
      z-index: 9999;
    }
    .copilot-fab {
      display: flex;
      align-items: center;
      gap: 10px;
      padding: 12px 20px;
      background: linear-gradient(135deg, #6366f1, #a855f7);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 30px;
      color: #fff;
      font-weight: 700;
      font-size: 0.9rem;
      cursor: pointer;
      box-shadow: 0 8px 24px rgba(99, 102, 241, 0.45);
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .copilot-fab:hover {
      transform: translateY(-2px) scale(1.03);
      box-shadow: 0 12px 30px rgba(168, 85, 247, 0.55);
    }
    .sparkle-anim {
      font-size: 1.1rem;
      animation: spin-pulse 3s infinite ease-in-out;
    }
    @keyframes spin-pulse {
      0%, 100% { transform: scale(1) rotate(0deg); }
      50% { transform: scale(1.2) rotate(180deg); }
    }

    .copilot-window {
      position: fixed;
      bottom: 24px;
      right: 28px;
      width: 440px;
      height: 620px;
      background: #0f172a;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 20px;
      display: flex;
      flex-direction: column;
      box-shadow: 0 20px 48px rgba(0, 0, 0, 0.7);
      z-index: 10000;
      overflow: hidden;
      animation: slide-up 0.25s ease-out;
    }
    @keyframes slide-up {
      from { transform: translateY(20px); opacity: 0; }
      to { transform: translateY(0); opacity: 1; }
    }

    .window-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 14px 18px;
      background: rgba(30, 41, 59, 0.95);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(8px);
    }
    .header-info {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .sparkle-icon {
      font-size: 1.3rem;
    }
    .copilot-title {
      margin: 0;
      font-size: 0.95rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .copilot-sub {
      font-size: 0.75rem;
      color: #94a3b8;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 6px;
    }
    .btn-icon, .btn-close {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.1rem;
      cursor: pointer;
      padding: 4px 6px;
      border-radius: 6px;
      transition: all 0.2s;
    }
    .btn-icon:hover, .btn-close:hover {
      color: #f8fafc;
      background: rgba(255, 255, 255, 0.1);
    }

    /* Settings Overlay */
    .settings-overlay {
      position: absolute;
      top: 60px;
      left: 0;
      right: 0;
      background: rgba(15, 23, 42, 0.98);
      padding: 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.15);
      z-index: 20;
      box-shadow: 0 8px 24px rgba(0,0,0,0.5);
    }
    .settings-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      margin-bottom: 12px;
    }
    .settings-header h5 {
      margin: 0;
      font-size: 0.9rem;
      color: #38bdf8;
    }
    .btn-sub-close {
      background: transparent;
      border: none;
      color: #94a3b8;
      cursor: pointer;
    }
    .form-group {
      margin-bottom: 10px;
      display: flex;
      flex-direction: column;
      gap: 4px;
    }
    .form-group label {
      font-size: 0.75rem;
      font-weight: 600;
      color: #cbd5e1;
    }
    .cfg-input, .cfg-select {
      background: #1e293b;
      border: 1px solid #334155;
      border-radius: 6px;
      padding: 8px 10px;
      color: #fff;
      font-size: 0.8rem;
      outline: none;
    }
    .hint-text {
      font-size: 0.68rem;
      color: #64748b;
    }
    .btn-save-cfg {
      background: #10b981;
      border: none;
      color: white;
      padding: 7px 14px;
      border-radius: 6px;
      font-size: 0.78rem;
      font-weight: 600;
      cursor: pointer;
      width: 100%;
    }
    .btn-save-cfg:hover {
      background: #059669;
    }

    .messages-container {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      background: #090d16;
    }
    .msg-bubble {
      max-width: 86%;
      padding: 12px 16px;
      border-radius: 14px;
      font-size: 0.84rem;
      line-height: 1.45;
      word-break: break-word;
    }
    .msg-bubble.user {
      align-self: flex-end;
      background: linear-gradient(135deg, #4f46e5, #6366f1);
      color: #ffffff;
      border-bottom-right-radius: 4px;
    }
    .msg-bubble.gemini {
      align-self: flex-start;
      background: #1e293b;
      color: #f1f5f9;
      border-bottom-left-radius: 4px;
      border: 1px solid rgba(255, 255, 255, 0.06);
    }
    .msg-author {
      font-size: 0.7rem;
      font-weight: 700;
      color: rgba(255, 255, 255, 0.6);
      margin-bottom: 4px;
    }
    .msg-time {
      display: block;
      font-size: 0.65rem;
      color: rgba(255, 255, 255, 0.4);
      margin-top: 6px;
      text-align: right;
    }

    .thinking {
      display: flex;
      align-items: center;
      gap: 10px;
    }
    .typing-dots span {
      display: inline-block;
      width: 6px;
      height: 6px;
      background: #a855f7;
      border-radius: 50%;
      margin-right: 3px;
      animation: bounce 1.4s infinite both;
    }
    .typing-dots span:nth-child(1) { animation-delay: -0.32s; }
    .typing-dots span:nth-child(2) { animation-delay: -0.16s; }
    @keyframes bounce {
      0%, 80%, 100% { transform: scale(0); }
      40% { transform: scale(1); }
    }
    .thinking-text {
      font-size: 0.78rem;
      color: #94a3b8;
      font-style: italic;
    }

    .quick-prompts {
      display: flex;
      gap: 6px;
      padding: 8px 14px;
      background: #0f172a;
      border-top: 1px solid rgba(255, 255, 255, 0.05);
      overflow-x: auto;
      scrollbar-width: none;
    }
    .quick-prompts::-webkit-scrollbar { display: none; }
    .q-chip {
      white-space: nowrap;
      padding: 6px 12px;
      background: rgba(255, 255, 255, 0.04);
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 16px;
      color: #cbd5e1;
      font-size: 0.72rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .q-chip:hover {
      background: rgba(99, 102, 241, 0.2);
      border-color: #6366f1;
      color: #fff;
    }

    .input-bar {
      display: flex;
      padding: 12px 14px;
      background: #1e293b;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      gap: 8px;
    }
    .chat-input {
      flex: 1;
      background: #0f172a;
      border: 1px solid #334155;
      border-radius: 12px;
      padding: 10px 14px;
      color: #fff;
      font-size: 0.85rem;
      outline: none;
      transition: border-color 0.2s;
    }
    .chat-input:focus {
      border-color: #6366f1;
    }
    .btn-send {
      width: 42px;
      height: 42px;
      background: linear-gradient(135deg, #6366f1, #8b5cf6);
      border: none;
      border-radius: 12px;
      color: white;
      font-size: 1.1rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
    }
    .btn-send:hover:not(:disabled) {
      transform: scale(1.05);
      background: linear-gradient(135deg, #4f46e5, #7c3aed);
    }
    .btn-send:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `]
})
export class FloatingCopilotComponent implements OnInit {
  isOpen = false;
  isThinking = false;
  showSettings = false;
  userInput = '';

  tempApiKey = '';
  tempModel = 'gemini-2.5-flash';
  currentConfig!: GeminiConfig;

  messages: ChatMessage[] = [
    {
      sender: 'gemini',
      text: 'Merhaba! Ben <strong>Gemini Spark AI</strong>, mağazanızın canlı finansal ve e-ticaret danışmanıyım.<br>Tüm sipariş kârlılıklarınız, TCMB dolar kuru senkronizasyonu ve VDS defterleriniz hazır. Size nasıl yardımcı olabilirim?',
      time: '00:27'
    }
  ];

  constructor(
    private geminiAi: GeminiAiService,
    public etsyApi: EtsyApiService
  ) {}

  ngOnInit(): void {
    this.currentConfig = this.geminiAi.getConfig();
    this.tempApiKey = this.currentConfig.apiKey;
    this.tempModel = this.currentConfig.model;
  }

  toggleOpen(): void {
    this.isOpen = !this.isOpen;
  }

  toggleSettings(): void {
    this.showSettings = !this.showSettings;
  }

  saveSettings(): void {
    this.geminiAi.saveConfig({
      apiKey: this.tempApiKey.trim(),
      model: this.tempModel
    });
    this.currentConfig = this.geminiAi.getConfig();
    this.showSettings = false;
    this.messages.push({
      sender: 'gemini',
      text: `✅ <strong>Ayarlar Kaydedildi:</strong> Model <code>${this.currentConfig.model}</code> olarak güncellendi. ${this.currentConfig.apiKey ? 'Özel Google AI Studio API Key aktif.' : 'Yerel zeka modu aktif.'}`,
      time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
    });
  }

  clearChat(): void {
    this.geminiAi.resetConversation();
    this.messages = [
      {
        sender: 'gemini',
        text: 'Sohbet geçmişi sıfırlandı. Yeni bir analiz veya soru için hazırım!',
        time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
      }
    ];
  }

  sendPrompt(promptText: string): void {
    this.userInput = promptText;
    this.sendMessage();
  }

  sendMessage(): void {
    const text = this.userInput.trim();
    if (!text || this.isThinking) return;

    const now = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    this.messages.push({ sender: 'user', text, time: now });
    this.userInput = '';
    this.isThinking = true;

    this.geminiAi.generateResponse(text).subscribe({
      next: (replyHtml: string) => {
        this.isThinking = false;
        this.messages.push({
          sender: 'gemini',
          text: replyHtml,
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        });
      },
      error: () => {
        this.isThinking = false;
        this.messages.push({
          sender: 'gemini',
          text: 'Üzgünüm, yanıt oluşturulurken bir bağlantı gecikmesi oluştu. Lütfen tekrar deneyiniz.',
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' })
        });
      }
    });
  }
}
