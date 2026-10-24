import { Component, OnInit, ElementRef, ViewChild, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StoreAiAssistantService, AssistantChatMessage } from '../../core/services/store-ai-assistant.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { AiLogoComponent } from '../../core/components/ai-logo.component';

@Component({
  selector: 'app-floating-copilot',
  standalone: true,
  imports: [CommonModule, FormsModule, AiLogoComponent],
  template: `
    <!-- FLOATING BUTTON -->
    <div class="floating-btn-wrap" *ngIf="!isOpen" (click)="toggleOpen()">
      <button class="copilot-fab" [title]="activeProvider + ' Mağaza Danışmanı'">
        <app-ai-logo [provider]="activeProvider" [size]="20"></app-ai-logo>
        <span class="fab-label">{{ activeProvider }} Danışmanı</span>
      </button>
    </div>

    <!-- EXPANDED CHAT WINDOW -->
    <div class="copilot-window" *ngIf="isOpen">
      <div class="window-header">
        <div class="header-info">
          <app-ai-logo [provider]="activeProvider" [size]="22"></app-ai-logo>
          <div>
            <h4 class="copilot-title">{{ activeProvider }} AI Mağaza Asistanı</h4>
            <span class="copilot-sub">
              {{ activeModel }} • Canlı Mağaza Zekası (1$ = {{ (etsyApi.exchangeRate() || 49.73).toFixed(2) }} ₺)
            </span>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-icon" (click)="openAiSettingsModal()" title="Yapay Zeka & Model Ayarları (DeepSeek, OpenAI, Claude, Grok, Gemini)">⚙️</button>
          <button class="btn-icon" (click)="clearChat()" title="Sohbeti Temizle">🗑️</button>
          <button class="btn-close" (click)="toggleOpen()" title="Kapat">×</button>
        </div>
      </div>

      <!-- MESSAGES STREAM -->
      <div class="messages-container" #scrollArea>
        <div *ngFor="let msg of messages" class="msg-bubble" [ngClass]="msg.sender">
          <div class="msg-author">{{ msg.sender === 'user' ? 'Siz' : (activeProvider + ' Asistanı') }}</div>
          <div class="msg-text" [innerHTML]="msg.text"></div>
          <span class="msg-time">{{ msg.time }}</span>
        </div>

        <div *ngIf="isThinking" class="msg-bubble assistant thinking">
          <div class="typing-dots">
            <span></span><span></span><span></span>
          </div>
          <span class="thinking-text">{{ activeProvider }} düşünüyor ve mağazanızı analiz ediyor...</span>
        </div>
      </div>

      <!-- QUICK ACTION CHIPS -->
      <div class="quick-prompts">
        <button class="q-chip" (click)="askNetProfit()">💰 Net Kâr & Durum</button>
        <button class="q-chip" (click)="askMissingCosts()">⚠️ Eksik Maliyet</button>
        <button class="q-chip" (click)="askProductViability()">🔍 Bu Ürün Satılır mı?</button>
        <button class="q-chip" (click)="askPricingAdvice()">🏷️ Fiyat & Kâr Tavsiyesi</button>
        <button class="q-chip" (click)="askTrendingModels()">🔥 Trend 3D Modeller</button>
      </div>

      <!-- INPUT BAR -->
      <div class="input-bar">
        <input 
          type="text" 
          [(ngModel)]="userInput" 
          (keyup.enter)="sendMessage()"
          placeholder="Mağazanızla veya ürün satışı ile ilgili her şeyi danışın..." 
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
      background: linear-gradient(135deg, #4f46e5, #0ea5e9);
      border: 1px solid rgba(255, 255, 255, 0.2);
      border-radius: 30px;
      color: #fff;
      font-weight: 700;
      font-size: 0.9rem;
      cursor: pointer;
      box-shadow: 0 8px 24px rgba(79, 70, 229, 0.45);
      transition: all 0.3s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .copilot-fab:hover {
      transform: translateY(-2px) scale(1.03);
      box-shadow: 0 12px 30px rgba(14, 165, 233, 0.55);
    }

    .copilot-window {
      position: fixed;
      bottom: 24px;
      right: 28px;
      width: 460px;
      height: 640px;
      background: #0b1120;
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 20px;
      display: flex;
      flex-direction: column;
      box-shadow: 0 20px 48px rgba(0, 0, 0, 0.75);
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
      background: rgba(15, 23, 42, 0.95);
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      backdrop-filter: blur(8px);
    }
    .header-info {
      display: flex;
      align-items: center;
      gap: 12px;
    }
    .copilot-title {
      margin: 0;
      font-size: 0.95rem;
      font-weight: 700;
      color: #f8fafc;
      letter-spacing: -0.2px;
    }
    .copilot-sub {
      display: block;
      font-size: 0.73rem;
      color: #94a3b8;
      margin-top: 1px;
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

    .messages-container {
      flex: 1;
      overflow-y: auto;
      padding: 16px;
      display: flex;
      flex-direction: column;
      gap: 14px;
      background: #070d19;
    }
    .msg-bubble {
      max-width: 88%;
      padding: 12px 16px;
      border-radius: 14px;
      font-size: 0.84rem;
      line-height: 1.5;
      word-break: break-word;
    }
    .msg-bubble.user {
      align-self: flex-end;
      background: linear-gradient(135deg, #4f46e5, #6366f1);
      color: #ffffff;
      border-bottom-right-radius: 4px;
    }
    .msg-bubble.assistant {
      align-self: flex-start;
      background: #151f32;
      color: #f1f5f9;
      border-bottom-left-radius: 4px;
      border: 1px solid rgba(255, 255, 255, 0.08);
      box-shadow: 0 4px 12px rgba(0,0,0,0.25);
    }
    .msg-author {
      font-size: 0.7rem;
      font-weight: 700;
      color: rgba(255, 255, 255, 0.65);
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
      background: #38bdf8;
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
      background: #0b1120;
      border-top: 1px solid rgba(255, 255, 255, 0.06);
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
      background: rgba(79, 70, 229, 0.25);
      border-color: #6366f1;
      color: #fff;
    }

    .input-bar {
      display: flex;
      padding: 12px 14px;
      background: #111a2e;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      gap: 8px;
    }
    .chat-input {
      flex: 1;
      background: #070d19;
      border: 1px solid #1e293b;
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
      background: linear-gradient(135deg, #4f46e5, #0ea5e9);
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
      background: linear-gradient(135deg, #4338ca, #0284c7);
    }
    .btn-send:disabled {
      opacity: 0.5;
      cursor: not-allowed;
    }
  `]
})
export class FloatingCopilotComponent implements OnInit {
  @ViewChild('scrollArea') scrollArea!: ElementRef;

  isOpen = false;
  isThinking = false;
  userInput = '';

  private assistantAi = inject(StoreAiAssistantService);
  public aiSettings = inject(AiSettingsService);
  public etsyApi = inject(EtsyApiService);

  messages: AssistantChatMessage[] = [];

  get activeProvider(): string {
    return this.assistantAi.getActiveProvider();
  }

  get activeModel(): string {
    return this.assistantAi.getActiveModel();
  }

  ngOnInit(): void {
    this.initWelcomeMessage();
  }

  private initWelcomeMessage(): void {
    const rate = this.etsyApi.exchangeRate() || 49.73;
    const now = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    this.messages = [
      {
        sender: 'assistant',
        text: `Selamlar! Ben mağazanızın canlı <strong>${this.activeProvider} AI</strong> danışmanıyım. 🚀<br><br>
VDS sunucu finans motorundaki tüm siparişleriniz, kârlılık dökümleriniz ve anlık dolar kuru (<strong>1$ = ${rate.toFixed(2)} ₺</strong>) arka planda hafızama yüklenmiş durumda.<br><br>
Size nasıl yardımcı olabilirim?<br>
• <strong>💰 Finans & Kâr Analizi:</strong> Ciromuz, net kâr marjımız ve reklam giderlerimizin dökümünü inceleyebiliriz.<br>
• <strong>🔍 Ürün Satılabilirlik & Fiyat Danışmanlığı:</strong> Aklınızdaki bir ürünün Etsy'de satılıp satılmayacağını, pazar talebini ve kaça satılması gerektiğini hesaplayabiliriz.<br>
• <strong>⚠️ Maliyet Denetimi:</strong> Eksik maliyetli siparişlerimizi tespit edebiliriz.`,
        time: now,
        provider: this.activeProvider,
        model: this.activeModel
      }
    ];
  }

  toggleOpen(): void {
    this.isOpen = !this.isOpen;
    if (this.isOpen) {
      this.assistantAi.refreshStoreSnapshot();
      setTimeout(() => this.scrollToBottom(), 100);
    }
  }

  openAiSettingsModal(): void {
    // Open central AI settings modal in web studio
    this.aiSettings.isAiModalOpen.set(true);
  }

  clearChat(): void {
    this.assistantAi.resetConversation();
    this.initWelcomeMessage();
  }

  sendPrompt(promptText: string): void {
    this.userInput = promptText;
    this.sendMessage();
  }

  askNetProfit(): void {
    this.sendPrompt('Bugünkü net kârım, cirom ve mağaza durumum nedir?');
  }

  askMissingCosts(): void {
    this.sendPrompt('Maliyeti veya kargo faturası eksik olan siparişler var mı?');
  }

  askProductViability(): void {
    this.sendPrompt("Bu ürün Etsy'de satılır mı? Pazar talebi ve rekabet analizi yapar mısın?");
  }

  askPricingAdvice(): void {
    this.sendPrompt('Yeni ekleyeceğimiz bir ürün için perakende satış fiyatı, kargo ve net kâr tavsiyesi verir misin?');
  }

  askTrendingModels(): void {
    this.sendPrompt("Etsy ve MakerWorld'de en çok kazandıran viral 3D modeller ve kârlılıkları neler?");
  }

  sendMessage(): void {
    const text = this.userInput.trim();
    if (!text || this.isThinking) return;

    const now = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    this.messages.push({ sender: 'user', text, time: now });
    this.userInput = '';
    this.isThinking = true;
    setTimeout(() => this.scrollToBottom(), 50);

    this.assistantAi.generateResponse(text).subscribe({
      next: (replyHtml: string) => {
        this.isThinking = false;
        this.messages.push({
          sender: 'assistant',
          text: replyHtml,
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
          provider: this.activeProvider,
          model: this.activeModel
        });
        setTimeout(() => this.scrollToBottom(), 50);
      },
      error: () => {
        this.isThinking = false;
        this.messages.push({
          sender: 'assistant',
          text: 'Üzgünüm, yanıt oluşturulurken bir bağlantı gecikmesi oluştu. Lütfen tekrar deneyiniz.',
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
          provider: this.activeProvider,
          model: this.activeModel
        });
        setTimeout(() => this.scrollToBottom(), 50);
      }
    });
  }

  private scrollToBottom(): void {
    try {
      if (this.scrollArea) {
        this.scrollArea.nativeElement.scrollTop = this.scrollArea.nativeElement.scrollHeight;
      }
    } catch {}
  }
}
