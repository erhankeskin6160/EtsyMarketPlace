import { Component, OnInit, ElementRef, ViewChild, HostListener, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { FormsModule } from '@angular/forms';
import { StoreAiAssistantService, AssistantChatMessage, ChatAttachment } from '../../core/services/store-ai-assistant.service';
import { CopilotSessionStorageService, CopilotChatSession } from '../../core/services/copilot-session-storage.service';
import { AiSettingsService } from '../../core/services/ai-settings.service';
import { EtsyApiService } from '../../core/services/etsy-api.service';
import { Router } from '@angular/router';
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
    <div 
      class="copilot-window" 
      *ngIf="isOpen"
      (dragover)="onDragOver($event)"
      (dragleave)="onDragLeave($event)"
      (drop)="onDrop($event)">

      <!-- DRAG OVERLAY -->
      <div class="drag-drop-overlay" *ngIf="isDraggingOver">
        <div class="drop-card">
          <span class="drop-icon">📂</span>
          <h4>Dosyayı Buraya Bırakın</h4>
          <p>Resim, Excel, PDF, CSV veya Belge</p>
        </div>
      </div>

      <div class="window-header">
        <div class="header-info">
          <app-ai-logo [provider]="activeProvider" [size]="22"></app-ai-logo>
          <div class="header-titles">
            <h4 class="copilot-title" [title]="activeSession?.title || (activeProvider + ' AI Asistanı')">
              {{ activeSession?.title || (activeProvider + ' AI Asistanı') }}
            </h4>
            <span class="copilot-sub">
              {{ activeModel }} • 1$ = {{ (etsyApi.exchangeRate() || 49.73).toFixed(2) }} ₺
            </span>
          </div>
        </div>
        <div class="header-actions">
          <button class="btn-new-chat" (click)="startNewChat()" title="Yeni Sohbet Başlat">
            <span class="btn-plus">＋</span>
            <span class="btn-text">Yeni</span>
          </button>
          <button class="btn-icon" [class.active]="isHistoryOpen" (click)="toggleHistoryDrawer()" title="Geçmiş Sohbetler">
            📜<span *ngIf="savedSessions.length > 0" class="history-count-badge">{{ savedSessions.length }}</span>
          </button>
          <button class="btn-icon" (click)="openAiSettingsModal()" title="Yapay Zeka & Model Ayarları (DeepSeek, OpenAI, Claude, Grok, Gemini)">⚙️</button>
          <button class="btn-icon" (click)="clearChat()" title="Sohbeti Temizle (/clear)">🗑️</button>
          <button class="btn-close" (click)="toggleOpen()" title="Kapat">×</button>
        </div>
      </div>

      <!-- SOHBET GEÇMİŞİ ÇEKMECESİ (HISTORY DRAWER) -->
      <div class="history-drawer" *ngIf="isHistoryOpen">
        <div class="history-drawer-header">
          <span class="history-drawer-title">📜 Geçmiş Sohbetler ({{ savedSessions.length }})</span>
          <button class="btn-close-drawer" (click)="toggleHistoryDrawer()" title="Kapat">✕</button>
        </div>
        <div class="history-list">
          <div *ngIf="savedSessions.length === 0" class="history-empty">
            Henüz kayıtlı geçmiş sohbetiniz yok.
          </div>
          <div 
            *ngFor="let s of savedSessions" 
            class="history-card"
            [class.active]="s.id === activeSession?.id"
            (click)="selectSession(s)">
            <div class="history-card-body">
              <div class="history-card-title">{{ s.title }}</div>
              <div *ngIf="getLastUserQuery(s)" class="history-card-preview">
                ❓ {{ getLastUserQuery(s) }}
              </div>
              <div class="history-card-sub">
                <span>💬 {{ s.messages.length }} mesaj</span>
                <span>•</span>
                <span>{{ formatSessionDate(s.updatedAt) }}</span>
                <span *ngIf="isGeneratingSession(s.id)" class="session-running-badge">
                  <span class="pulsing-dot"></span> Yanıt üretiliyor...
                </span>
              </div>
            </div>
            <button class="btn-del-session" (click)="deleteSession($event, s.id)" title="Bu sohbeti sil">🗑️</button>
          </div>
        </div>
      </div>

      <!-- MESSAGES STREAM -->
      <div class="messages-container" #scrollArea>
        <div *ngFor="let msg of messages" class="msg-bubble" [ngClass]="msg.sender">
          <div class="msg-author">{{ msg.sender === 'user' ? 'Siz' : (activeProvider + ' Asistanı') }}</div>

          <!-- ATTACHMENTS IN USER MESSAGE -->
          <div *ngIf="msg.attachments && msg.attachments.length > 0" class="msg-attachments-wrap">
            <div *ngFor="let att of msg.attachments" class="att-card" [ngClass]="att.type">
              <div *ngIf="att.type === 'image' && att.dataUrl" class="att-img-card" (click)="openImageModal(att.dataUrl)" title="Tam boyutta büyütmek için tıklayın">
                <div class="att-img-viewport">
                  <img [src]="att.dataUrl" class="att-thumb-contain" alt="Ekli Görsel" />
                  <span class="att-zoom-badge">🔍 Büyüt</span>
                </div>
                <div class="att-card-footer">
                  <span class="att-name-label" [title]="att.name">{{ att.name || 'Görsel.png' }}</span>
                  <span class="att-size-label">{{ (att.size / 1024).toFixed(0) }} KB</span>
                </div>
              </div>

              <div *ngIf="att.type !== 'image'" class="msg-att-doc">
                <span class="doc-icon">{{ att.type === 'excel' ? '📊' : (att.type === 'pdf' ? '📄' : '📝') }}</span>
                <span class="doc-name">{{ att.name }}</span>
                <span class="doc-size">({{ (att.size / 1024).toFixed(0) }} KB)</span>
              </div>
            </div>
          </div>

          <div class="msg-text" [innerHTML]="msg.text"></div>
          <span class="msg-time">{{ msg.time }}</span>
        </div>

        <div *ngIf="isThinking" class="msg-bubble assistant thinking">
          <div class="typing-dots">
            <span></span><span></span><span></span>
          </div>
          <span class="thinking-text">{{ assistantAi.liveToolStatus() || (activeProvider + ' düşünüyor ve mağazanızı analiz ediyor...') }}</span>
        </div>
      </div>

      <!-- QUICK ACTION CHIPS -->
      <div class="quick-prompts">
        <button class="q-chip" (click)="askLiveMarketAnalysis()">⚡ Canlı Pazar & Kargo Modülü</button>
        <button class="q-chip" (click)="askNetProfit()">💰 Net Kâr & Durum</button>
        <button class="q-chip" (click)="askMissingCosts()">⚠️ Eksik Maliyet</button>
        <button class="q-chip" (click)="askProductViability()">🔍 Bu Ürün Satılır mı?</button>
        <button class="q-chip" (click)="askPricingAdvice()">🏷️ Fiyat & Kâr Tavsiyesi</button>
        <button class="q-chip" (click)="askTrendingModels()">🔥 Trend 3D Modeller</button>
      </div>

      <!-- ATTACHMENT STAGING BAR (ABOVE INPUT) -->
      <div class="attachment-staging-bar" *ngIf="pendingAttachments.length > 0">
        <div class="staging-list">
          <div *ngFor="let att of pendingAttachments; let i = index" class="staging-item">
            <img *ngIf="att.type === 'image' && att.dataUrl" [src]="att.dataUrl" class="staging-thumb" />
            <span *ngIf="att.type === 'excel'" class="staging-badge excel">📊</span>
            <span *ngIf="att.type === 'pdf'" class="staging-badge pdf">📄</span>
            <span *ngIf="att.type === 'text'" class="staging-badge text">📝</span>
            <span class="staging-info">
              <span class="staging-name" [title]="att.name">{{ att.name }}</span>
              <span class="staging-size">{{ (att.size / 1024).toFixed(0) }} KB</span>
            </span>
            <button type="button" class="btn-remove-att" (click)="removeAttachment(i)" title="Kaldır">✕</button>
          </div>
        </div>
        <button type="button" class="btn-clear-all-att" (click)="clearAttachments()" title="Tüm ekleri temizle">✕ Tümünü Temizle</button>
      </div>

      <!-- INPUT BAR -->
      <div class="input-bar">
        <!-- HIDDEN FILE INPUT -->
        <input 
          type="file" 
          #fileInput 
          multiple 
          (change)="onFileSelected($event)" 
          accept="image/*,.pdf,.xlsx,.xls,.csv,.tsv,.txt,.json,.log,.md" 
          style="display: none" />

        <button 
          type="button" 
          class="btn-attach" 
          (click)="triggerFileInput()" 
          title="Resim, Excel, PDF veya Belge Ekle (Ctrl+V ile de yapıştırabilirsiniz)">
          📎
        </button>

        <input 
          type="text" 
          [(ngModel)]="userInput" 
          (keyup.enter)="sendMessage()"
          placeholder="Sorunuzu yazın veya resim/belge yapıştırın (Ctrl+V)..." 
          class="chat-input" />

        <button 
          class="btn-send" 
          [disabled]="(!userInput.trim() && pendingAttachments.length === 0) || isThinking" 
          (click)="sendMessage()">
          ➤
        </button>
      </div>
    </div>

    <!-- IMAGE PREVIEW MODAL -->
    <div class="image-modal-overlay" *ngIf="previewImageUrl" (click)="previewImageUrl = null">
      <div class="image-modal-content" (click)="$event.stopPropagation()">
        <img [src]="previewImageUrl" class="modal-large-img" />
        <button class="btn-close-modal" (click)="previewImageUrl = null">✕</button>
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
      width: 480px;
      height: 660px;
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

    /* DRAG DROP OVERLAY */
    .drag-drop-overlay {
      position: absolute;
      inset: 0;
      background: rgba(15, 23, 42, 0.92);
      backdrop-filter: blur(4px);
      z-index: 50;
      display: flex;
      align-items: center;
      justify-content: center;
      pointer-events: none;
      border: 2px dashed #38bdf8;
      border-radius: 20px;
    }
    .drop-card {
      text-align: center;
      color: #f8fafc;
    }
    .drop-icon {
      font-size: 3rem;
      display: block;
      margin-bottom: 8px;
      animation: pulse 1.5s infinite;
    }
    .drop-card h4 {
      margin: 0 0 4px 0;
      font-size: 1.1rem;
      font-weight: 700;
      color: #38bdf8;
    }
    .drop-card p {
      margin: 0;
      font-size: 0.8rem;
      color: #94a3b8;
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
      overflow: hidden;
      max-width: 58%;
    }
    .header-titles {
      overflow: hidden;
    }
    .copilot-title {
      margin: 0;
      font-size: 0.92rem;
      font-weight: 700;
      color: #f8fafc;
      letter-spacing: -0.2px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .copilot-sub {
      display: block;
      font-size: 0.72rem;
      color: #94a3b8;
      margin-top: 1px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .header-actions {
      display: flex;
      align-items: center;
      gap: 5px;
    }
    .btn-new-chat {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.25), rgba(56, 189, 248, 0.25));
      border: 1px solid rgba(99, 102, 241, 0.4);
      color: #38bdf8;
      border-radius: 8px;
      padding: 4px 8px;
      font-size: 0.72rem;
      font-weight: 600;
      cursor: pointer;
      transition: all 0.2s;
    }
    .btn-new-chat:hover {
      background: linear-gradient(135deg, rgba(99, 102, 241, 0.45), rgba(56, 189, 248, 0.45));
      border-color: #38bdf8;
      color: #fff;
      transform: scale(1.03);
    }
    .btn-icon, .btn-close {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 1.05rem;
      cursor: pointer;
      padding: 4px 6px;
      border-radius: 6px;
      transition: all 0.2s;
    }
    .btn-icon:hover, .btn-close:hover {
      color: #f8fafc;
      background: rgba(255, 255, 255, 0.1);
    }
    .btn-icon.active {
      background: rgba(56, 189, 248, 0.2);
      color: #38bdf8;
      border: 1px solid rgba(56, 189, 248, 0.3);
    }

    /* History Drawer */
    .history-drawer {
      position: absolute;
      top: 61px;
      left: 0;
      right: 0;
      bottom: 0;
      background: rgba(11, 17, 32, 0.98);
      backdrop-filter: blur(12px);
      z-index: 100;
      display: flex;
      flex-direction: column;
      animation: fadeIn 0.2s ease-out;
      border-bottom-left-radius: 16px;
      border-bottom-right-radius: 16px;
    }
    .history-drawer-header {
      display: flex;
      justify-content: space-between;
      align-items: center;
      padding: 12px 16px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      background: rgba(15, 23, 42, 0.7);
    }
    .history-drawer-title {
      font-size: 0.85rem;
      font-weight: 700;
      color: #f8fafc;
    }
    .btn-close-drawer {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 0.9rem;
      cursor: pointer;
      padding: 2px 6px;
      border-radius: 4px;
    }
    .btn-close-drawer:hover {
      color: #fff;
      background: rgba(255, 255, 255, 0.1);
    }
    .history-list {
      flex: 1;
      overflow-y: auto;
      padding: 12px;
      display: flex;
      flex-direction: column;
      gap: 8px;
    }
    .history-empty {
      padding: 30px 16px;
      text-align: center;
      color: #64748b;
      font-size: 0.82rem;
    }
    .history-card {
      display: flex;
      align-items: center;
      justify-content: space-between;
      background: #151f32;
      border: 1px solid rgba(255, 255, 255, 0.06);
      border-radius: 10px;
      padding: 10px 12px;
      cursor: pointer;
      transition: all 0.2s;
    }
    .history-card:hover {
      background: #1e293b;
      border-color: rgba(56, 189, 248, 0.3);
      transform: translateX(2px);
    }
    .history-card.active {
      border-color: #6366f1;
      background: rgba(99, 102, 241, 0.15);
    }
    .history-card-body {
      flex: 1;
      overflow: hidden;
      margin-right: 8px;
    }
    .history-card-title {
      font-size: 0.82rem;
      font-weight: 600;
      color: #f1f5f9;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
      display: block;
    }
    .history-card-preview {
      font-size: 0.72rem;
      color: #93c5fd;
      margin-top: 2px;
      margin-bottom: 3px;
      white-space: nowrap;
      overflow: hidden;
      text-overflow: ellipsis;
    }
    .history-count-badge {
      display: inline-block;
      margin-left: 3px;
      font-size: 0.65rem;
      font-weight: 700;
      color: #38bdf8;
      background: rgba(56, 189, 248, 0.15);
      border-radius: 10px;
      padding: 0 4px;
    }
    .history-card-sub {
      display: flex;
      align-items: center;
      gap: 6px;
      font-size: 0.68rem;
      color: #94a3b8;
      margin-top: 3px;
      flex-wrap: wrap;
    }
    .session-running-badge {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      color: #38bdf8;
      font-weight: 600;
      font-size: 0.68rem;
      background: rgba(14, 165, 233, 0.15);
      padding: 1px 6px;
      border-radius: 4px;
    }
    .session-running-badge .pulsing-dot {
      width: 5px;
      height: 5px;
      border-radius: 50%;
      background-color: #38bdf8;
      animation: pulse-dot 1.2s infinite ease-in-out;
    }
    @keyframes pulse-dot {
      0%, 100% { opacity: 0.3; transform: scale(0.8); }
      50% { opacity: 1; transform: scale(1.2); }
    }
    .btn-del-session {
      background: transparent;
      border: none;
      color: #64748b;
      font-size: 0.85rem;
      cursor: pointer;
      padding: 4px 6px;
      border-radius: 4px;
      transition: all 0.2s;
    }
    .btn-del-session:hover {
      color: #ef4444;
      background: rgba(239, 68, 68, 0.15);
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

    /* Table & Formatting Enhancements */
    .copilot-table-wrap {
      width: 100%;
      overflow-x: auto;
      margin: 10px 0;
      border-radius: 8px;
      border: 1px solid rgba(255, 255, 255, 0.12);
      background: rgba(15, 23, 42, 0.75);
    }
    .copilot-table {
      width: 100%;
      border-collapse: collapse;
      font-size: 0.76rem;
      color: #e2e8f0;
      white-space: nowrap;
    }
    .copilot-table th {
      background: rgba(56, 189, 248, 0.15);
      color: #38bdf8;
      font-weight: 700;
      padding: 8px 12px;
      text-align: left;
      border-bottom: 1px solid rgba(255, 255, 255, 0.12);
    }
    .copilot-table td {
      padding: 7px 12px;
      border-bottom: 1px solid rgba(255, 255, 255, 0.05);
    }
    .copilot-table tr:last-child td {
      border-bottom: none;
    }
    .copilot-table tr:hover td {
      background: rgba(255, 255, 255, 0.04);
    }
    .copilot-h2 {
      font-size: 1.05rem;
      font-weight: 800;
      color: #38bdf8;
      margin: 12px 0 6px 0;
    }
    .copilot-h3 {
      font-size: 0.95rem;
      font-weight: 700;
      color: #f8fafc;
      margin: 10px 0 4px 0;
      border-bottom: 1px solid rgba(255, 255, 255, 0.08);
      padding-bottom: 3px;
    }
    .copilot-h4 {
      font-size: 0.88rem;
      font-weight: 700;
      color: #cbd5e1;
      margin: 8px 0 3px 0;
    }
    .copilot-quote {
      border-left: 3px solid #6366f1;
      background: rgba(99, 102, 241, 0.1);
      margin: 8px 0;
      padding: 6px 12px;
      border-radius: 0 6px 6px 0;
      font-style: italic;
      color: #cbd5e1;
    }
    .copilot-list {
      margin: 4px 0;
      padding-left: 20px;
    }
    .copilot-list li {
      margin-bottom: 3px;
      color: #e2e8f0;
    }
    .copilot-hr {
      border: none;
      border-top: 1px solid rgba(255, 255, 255, 0.1);
      margin: 12px 0;
    }
    .copilot-inline-code {
      background: rgba(255, 255, 255, 0.1);
      color: #38bdf8;
      padding: 2px 6px;
      border-radius: 4px;
      font-size: 0.75rem;
      font-family: monospace;
    }

    /* Message Attachments */
    .msg-attachments-wrap {
      display: flex;
      flex-wrap: wrap;
      gap: 10px;
      margin-bottom: 8px;
    }
    .att-card {
      max-width: 100%;
    }
    .att-img-card {
      display: flex;
      flex-direction: column;
      width: 190px;
      background: #090e1a;
      border: 1px solid rgba(255, 255, 255, 0.18);
      border-radius: 10px;
      overflow: hidden;
      cursor: pointer;
      box-shadow: 0 4px 14px rgba(0, 0, 0, 0.45);
      transition: all 0.2s cubic-bezier(0.4, 0, 0.2, 1);
    }
    .att-img-card:hover {
      transform: translateY(-2px);
      border-color: #38bdf8;
      box-shadow: 0 6px 18px rgba(56, 189, 248, 0.25);
    }
    .att-img-viewport {
      width: 100%;
      height: 120px;
      position: relative;
      background: #050811;
      display: flex;
      align-items: center;
      justify-content: center;
      overflow: hidden;
      padding: 4px;
    }
    .att-thumb-contain {
      max-width: 100%;
      max-height: 100%;
      width: auto;
      height: auto;
      object-fit: contain; /* NEVER CROPS OR DISTORTS - KESİNLİKLE KIRPMAZ VEYA BOZMAZ */
      display: block;
      border-radius: 4px;
    }
    .att-zoom-badge {
      position: absolute;
      bottom: 6px;
      right: 6px;
      background: rgba(0, 0, 0, 0.75);
      backdrop-filter: blur(4px);
      color: #38bdf8;
      font-size: 0.65rem;
      font-weight: 600;
      padding: 2px 6px;
      border-radius: 4px;
      border: 1px solid rgba(56, 189, 248, 0.3);
      opacity: 0;
      transition: opacity 0.2s;
    }
    .att-img-card:hover .att-zoom-badge {
      opacity: 1;
    }
    .att-card-footer {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 6px 8px;
      background: #0f172a;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      font-size: 0.68rem;
    }
    .att-name-label {
      max-width: 120px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
      color: #e2e8f0;
      font-weight: 500;
    }
    .att-size-label {
      color: #94a3b8;
    }
    .msg-att-doc {
      display: inline-flex;
      align-items: center;
      gap: 6px;
      background: rgba(0, 0, 0, 0.25);
      border: 1px solid rgba(255, 255, 255, 0.15);
      padding: 4px 8px;
      border-radius: 8px;
      font-size: 0.72rem;
    }
    .doc-name {
      max-width: 160px;
      overflow: hidden;
      text-overflow: ellipsis;
      white-space: nowrap;
    }
    .doc-size {
      color: rgba(255, 255, 255, 0.5);
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

    /* ATTACHMENT STAGING BAR */
    .attachment-staging-bar {
      display: flex;
      align-items: center;
      justify-content: space-between;
      padding: 8px 12px;
      background: #0f172a;
      border-top: 1px solid rgba(56, 189, 248, 0.2);
      gap: 8px;
      overflow-x: auto;
    }
    .staging-list {
      display: flex;
      align-items: center;
      gap: 8px;
      overflow-x: auto;
      scrollbar-width: none;
    }
    .staging-list::-webkit-scrollbar { display: none; }
    .staging-item {
      display: flex;
      align-items: center;
      gap: 6px;
      background: #1e293b;
      border: 1px solid rgba(255, 255, 255, 0.1);
      border-radius: 8px;
      padding: 4px 8px;
      font-size: 0.72rem;
      white-space: nowrap;
    }
    .staging-thumb {
      width: 24px;
      height: 24px;
      object-fit: cover;
      border-radius: 4px;
    }
    .staging-badge {
      font-size: 0.9rem;
    }
    .staging-info {
      display: flex;
      flex-direction: column;
      line-height: 1.2;
    }
    .staging-name {
      max-width: 110px;
      overflow: hidden;
      text-overflow: ellipsis;
      color: #f1f5f9;
      font-weight: 600;
    }
    .staging-size {
      color: #94a3b8;
      font-size: 0.65rem;
    }
    .btn-remove-att {
      background: transparent;
      border: none;
      color: #ef4444;
      font-size: 0.75rem;
      cursor: pointer;
      padding: 2px 4px;
      border-radius: 4px;
    }
    .btn-remove-att:hover {
      background: rgba(239, 68, 68, 0.2);
    }
    .btn-clear-all-att {
      background: transparent;
      border: none;
      color: #94a3b8;
      font-size: 0.7rem;
      cursor: pointer;
      white-space: nowrap;
      padding: 4px 6px;
      border-radius: 6px;
    }
    .btn-clear-all-att:hover {
      color: #ef4444;
      background: rgba(239, 68, 68, 0.15);
    }

    .input-bar {
      display: flex;
      align-items: center;
      padding: 10px 14px;
      background: #111a2e;
      border-top: 1px solid rgba(255, 255, 255, 0.08);
      gap: 8px;
    }
    .btn-attach {
      background: rgba(255, 255, 255, 0.06);
      border: 1px solid rgba(255, 255, 255, 0.12);
      border-radius: 10px;
      width: 38px;
      height: 38px;
      color: #38bdf8;
      font-size: 1.2rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
      flex-shrink: 0;
    }
    .btn-attach:hover {
      background: rgba(56, 189, 248, 0.2);
      border-color: #38bdf8;
      transform: scale(1.05);
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
      width: 38px;
      height: 38px;
      background: linear-gradient(135deg, #4f46e5, #0ea5e9);
      border: none;
      border-radius: 10px;
      color: white;
      font-size: 1.1rem;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
      transition: all 0.2s;
      flex-shrink: 0;
    }
    .btn-send:hover:not(:disabled) {
      transform: scale(1.05);
      background: linear-gradient(135deg, #4338ca, #0284c7);
    }
    .btn-send:disabled {
      opacity: 0.4;
      cursor: not-allowed;
    }

    /* IMAGE MODAL */
    .image-modal-overlay {
      position: fixed;
      inset: 0;
      background: rgba(0, 0, 0, 0.85);
      backdrop-filter: blur(6px);
      z-index: 20000;
      display: flex;
      align-items: center;
      justify-content: center;
      padding: 24px;
    }
    .image-modal-content {
      position: relative;
      max-width: 90vw;
      max-height: 90vh;
    }
    .modal-large-img {
      max-width: 100%;
      max-height: 85vh;
      border-radius: 12px;
      box-shadow: 0 16px 40px rgba(0, 0, 0, 0.8);
      border: 1px solid rgba(255, 255, 255, 0.2);
    }
    .btn-close-modal {
      position: absolute;
      top: -16px;
      right: -16px;
      width: 36px;
      height: 36px;
      border-radius: 50%;
      background: #ef4444;
      border: 2px solid #fff;
      color: white;
      font-size: 1.1rem;
      font-weight: 700;
      cursor: pointer;
      display: flex;
      align-items: center;
      justify-content: center;
    }
  `]
})
export class FloatingCopilotComponent implements OnInit {
  @ViewChild('scrollArea') scrollArea!: ElementRef;
  @ViewChild('fileInput') fileInputRef!: ElementRef<HTMLInputElement>;

  isOpen = false;
  generatingSessionIds = new Set<string>();

  get isThinking(): boolean {
    return this.activeSession ? this.generatingSessionIds.has(this.activeSession.id) : false;
  }

  isGeneratingSession(sessionId: string): boolean {
    return this.generatingSessionIds.has(sessionId);
  }
  isDraggingOver = false;
  userInput = '';
  previewImageUrl: string | null = null;

  pendingAttachments: ChatAttachment[] = [];

  public assistantAi = inject(StoreAiAssistantService);
  public aiSettings = inject(AiSettingsService);
  public etsyApi = inject(EtsyApiService);
  public sessionStorage = inject(CopilotSessionStorageService);
  public router = inject(Router);

  activeSession: CopilotChatSession | null = null;
  savedSessions: CopilotChatSession[] = [];
  isHistoryOpen = false;
  messages: AssistantChatMessage[] = [];

  get activeProvider(): string {
    return this.assistantAi.getActiveProvider();
  }

  get activeModel(): string {
    return this.assistantAi.getActiveModel();
  }

  async ngOnInit(): Promise<void> {
    if (typeof window !== 'undefined') {
      (window as any).__copilotNavigate = (route: string) => {
        this.router.navigateByUrl(route);
      };
    }
    await this.loadActiveSession();
    await this.refreshSavedSessionsList();
  }

  /**
   * Listen to global paste event (Ctrl + V)
   */
  @HostListener('window:paste', ['$event'])
  onPaste(event: ClipboardEvent): void {
    if (!this.isOpen) return;

    const items = event.clipboardData?.items;
    if (!items || items.length === 0) return;

    let hasHandledMedia = false;
    for (let i = 0; i < items.length; i++) {
      const item = items[i];
      if (item.type.indexOf('image') !== -1) {
        const file = item.getAsFile();
        if (file) {
          this.processSingleFile(file);
          hasHandledMedia = true;
        }
      }
    }

    if (hasHandledMedia) {
      event.preventDefault();
      return;
    }

    // Check if pasted content is tabular text (from Excel / Google Sheets)
    const textData = event.clipboardData?.getData('text/plain');
    if (textData && textData.includes('\t') && textData.includes('\n')) {
      const tableMarkdown = this.convertTsvToMarkdownTable(textData);
      if (tableMarkdown) {
        this.pendingAttachments.push({
          id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
          name: 'Panodan_Tablo.tsv',
          type: 'excel',
          mimeType: 'text/tab-separated-values',
          size: new Blob([textData]).size,
          extractedText: tableMarkdown
        });
        event.preventDefault();
      }
    }
  }

  onDragOver(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDraggingOver = true;
  }

  onDragLeave(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDraggingOver = false;
  }

  onDrop(e: DragEvent): void {
    e.preventDefault();
    e.stopPropagation();
    this.isDraggingOver = false;

    const files = e.dataTransfer?.files;
    if (files && files.length > 0) {
      this.processFiles(files);
    }
  }

  triggerFileInput(): void {
    if (this.fileInputRef) {
      this.fileInputRef.nativeElement.value = '';
      this.fileInputRef.nativeElement.click();
    }
  }

  onFileSelected(e: any): void {
    const files = e.target?.files;
    if (files && files.length > 0) {
      this.processFiles(files);
    }
  }

  processFiles(fileList: FileList | File[]): void {
    const arr = Array.from(fileList);
    for (const file of arr) {
      this.processSingleFile(file);
    }
  }

  processSingleFile(file: File): void {
    const mime = file.type.toLowerCase();
    const name = file.name.toLowerCase();

    // 1. IMAGES (png, jpg, jpeg, webp, gif)
    if (mime.startsWith('image/')) {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        this.pendingAttachments.push({
          id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
          name: file.name || 'Görsel.png',
          type: 'image',
          mimeType: file.type || 'image/png',
          size: file.size,
          dataUrl: e.target.result
        });
      };
      reader.readAsDataURL(file);
      return;
    }

    // 2. CSV & TSV
    if (name.endsWith('.csv') || name.endsWith('.tsv') || mime === 'text/csv' || mime.includes('tab-separated')) {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        const text = e.target.result as string;
        const formatted = name.endsWith('.tsv') 
          ? this.convertTsvToMarkdownTable(text) 
          : this.convertCsvToMarkdownTable(text);
        this.pendingAttachments.push({
          id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
          name: file.name,
          type: 'excel',
          mimeType: file.type || 'text/csv',
          size: file.size,
          extractedText: formatted
        });
      };
      reader.readAsText(file);
      return;
    }

    // 3. EXCEL (.xlsx, .xls)
    if (name.endsWith('.xlsx') || name.endsWith('.xls') || mime.includes('spreadsheet') || mime.includes('excel')) {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        const buffer = e.target.result as ArrayBuffer;
        const extracted = this.extractTextFromBinary(buffer, file.name);
        this.pendingAttachments.push({
          id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
          name: file.name,
          type: 'excel',
          mimeType: file.type || 'application/vnd.openxmlformats-officedocument.spreadsheetml.sheet',
          size: file.size,
          extractedText: extracted
        });
      };
      reader.readAsArrayBuffer(file);
      return;
    }

    // 4. PDF (.pdf)
    if (name.endsWith('.pdf') || mime === 'application/pdf') {
      const reader = new FileReader();
      reader.onload = (e: any) => {
        const dataUrl = e.target.result as string;
        const extracted = this.extractTextFromPdfDataUrl(dataUrl, file.name);
        this.pendingAttachments.push({
          id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
          name: file.name,
          type: 'pdf',
          mimeType: 'application/pdf',
          size: file.size,
          dataUrl,
          extractedText: extracted
        });
      };
      reader.readAsDataURL(file);
      return;
    }

    // 5. TEXT / JSON / LOG / MD
    const reader = new FileReader();
    reader.onload = (e: any) => {
      const text = e.target.result as string;
      this.pendingAttachments.push({
        id: 'att_' + Date.now() + '_' + Math.random().toString(36).substring(2, 6),
        name: file.name,
        type: 'text',
        mimeType: file.type || 'text/plain',
        size: file.size,
        extractedText: text
      });
    };
    reader.readAsText(file);
  }

  removeAttachment(index: number): void {
    this.pendingAttachments.splice(index, 1);
  }

  clearAttachments(): void {
    this.pendingAttachments = [];
  }

  openImageModal(url?: string): void {
    if (url) {
      this.previewImageUrl = url;
    }
  }

  async loadActiveSession(): Promise<void> {
    const activeId = this.sessionStorage.getActiveSessionId();
    if (activeId) {
      const existing = await this.sessionStorage.getSession(activeId);
      if (existing && existing.messages && existing.messages.length > 0) {
        const hasUserMessages = existing.messages.some(m => m.sender === 'user');
        if (hasUserMessages) {
          this.activeSession = existing;
          this.messages = existing.messages;
          this.assistantAi.setConversationHistory(existing.conversationHistory || []);
          return;
        }
      }
    }

    const all = await this.sessionStorage.getAllSessions();
    const sessionWithUser = all.find(s => s.messages && s.messages.some(m => m.sender === 'user'));
    if (sessionWithUser) {
      this.activeSession = sessionWithUser;
      this.messages = sessionWithUser.messages;
      this.assistantAi.setConversationHistory(sessionWithUser.conversationHistory || []);
      this.sessionStorage.setActiveSessionId(sessionWithUser.id);
      return;
    }

    if (all.length > 0 && all[0].messages && all[0].messages.length > 0) {
      this.activeSession = all[0];
      this.messages = all[0].messages;
      this.assistantAi.setConversationHistory(all[0].conversationHistory || []);
      this.sessionStorage.setActiveSessionId(all[0].id);
      return;
    }

    this.startNewChat(false);
  }

  getLastUserQuery(s: CopilotChatSession): string {
    if (!s || !s.messages) return '';
    const userMsgs = s.messages.filter(m => m.sender === 'user');
    if (userMsgs.length === 0) return '';
    const last = userMsgs[userMsgs.length - 1].text || '';
    const cleaned = last.replace(/<[^>]*>/g, '').trim();
    return cleaned.length > 36 ? cleaned.slice(0, 34) + '...' : cleaned;
  }

  async refreshSavedSessionsList(): Promise<void> {
    this.savedSessions = await this.sessionStorage.getAllSessions();
  }

  toggleHistoryDrawer(): void {
    this.isHistoryOpen = !this.isHistoryOpen;
    if (this.isHistoryOpen) {
      this.refreshSavedSessionsList();
    }
  }

  startNewChat(savePrevious = true): void {
    if (savePrevious && this.activeSession && this.messages.length > 0) {
      this.syncCurrentSession();
    }

    this.assistantAi.resetConversation();
    this.clearAttachments();
    this.activeSession = this.sessionStorage.createNewSession('Yeni Sohbet');
    this.initWelcomeMessage();
    this.syncCurrentSession();
    this.refreshSavedSessionsList();
    this.isHistoryOpen = false;
  }

  async selectSession(s: CopilotChatSession): Promise<void> {
    this.syncCurrentSession();
    const freshSession = (await this.sessionStorage.getSession(s.id)) || s;
    this.activeSession = freshSession;
    this.messages = freshSession.messages || [];
    this.assistantAi.setConversationHistory(freshSession.conversationHistory || []);
    this.sessionStorage.setActiveSessionId(freshSession.id);
    this.isHistoryOpen = false;
    setTimeout(() => this.scrollToBottom(), 50);
  }

  async deleteSession(e: Event, id: string): Promise<void> {
    e.stopPropagation();
    await this.sessionStorage.deleteSession(id);
    if (this.activeSession?.id === id) {
      this.startNewChat(false);
    } else {
      await this.refreshSavedSessionsList();
    }
  }

  formatSessionDate(ts: number): string {
    const d = new Date(ts);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    }
    return d.toLocaleDateString('tr-TR', { day: 'numeric', month: 'short' });
  }

  private syncCurrentSession(): void {
    if (!this.activeSession) return;
    this.activeSession.messages = [...this.messages];
    this.activeSession.conversationHistory = this.assistantAi.getConversationHistory();
    this.activeSession.updatedAt = Date.now();
    this.sessionStorage.saveSession(this.activeSession);
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
• <strong>📸 Görsel İncelemesi:</strong> Ürün fotoğrafı yapıştırarak (Ctrl+V) veya yükleyerek Etsy'de satılıp satılmayacağını ve kaça satılacağını danışabilirsiniz.<br>
• <strong>📊 Excel & PDF Denetimi:</strong> Kargo faturası, maliyet tablosu veya sipariş raporu yükleyip analizi başlatabilirsiniz.<br>
• <strong>💰 Finans & Net Kâr:</strong> Mağazanızın kârlılık dökümünü ve eksik maliyetlerini inceleyebiliriz.`,
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
    this.aiSettings.isAiModalOpen.set(true);
  }

  async clearChat(): Promise<void> {
    if (this.activeSession) {
      await this.sessionStorage.deleteSession(this.activeSession.id);
    }
    this.assistantAi.resetConversation();
    this.clearAttachments();
    this.startNewChat(false);
  }

  sendPrompt(promptText: string): void {
    this.userInput = promptText;
    this.sendMessage();
  }

  askLiveMarketAnalysis(): void {
    this.sendPrompt("Bu ürün Etsy'de kaça satılır ve canlı kargo teklifleri nedir? Pazar araştırması ve kargo motorunu çalıştırır mısın?");
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
    const attachmentsToSend = [...this.pendingAttachments];

    // Command Check: /clear, /temizle, clear
    if (text.toLowerCase() === '/clear' || text.toLowerCase() === '/temizle' || text.toLowerCase() === 'clear') {
      this.userInput = '';
      this.clearChat();
      return;
    }

    if ((!text && attachmentsToSend.length === 0) || this.isThinking) return;

    if (!this.activeSession) return;
    const targetSession = this.activeSession;
    const targetSessionId = targetSession.id;

    // Auto-derive session title if it's the first message
    if (!targetSession.title || targetSession.title === 'Yeni Sohbet') {
      let derivedTitle = text || (attachmentsToSend.length > 0 ? attachmentsToSend[0].name : 'Ürün İncelemesi');
      derivedTitle = derivedTitle.replace(/\.(png|jpg|jpeg|webp|gif|pdf|xlsx|csv)$/i, '').trim();
      if (derivedTitle.length > 32) {
        derivedTitle = derivedTitle.substring(0, 30) + '...';
      }
      targetSession.title = derivedTitle;
    }

    const displayText = text || (attachmentsToSend.length === 1 
      ? `📎 ${attachmentsToSend[0].name} dosyasını inceler misin?` 
      : `📎 ${attachmentsToSend.length} adet ekli belge/resim inceler misin?`);

    const now = new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' });
    const userMsg: AssistantChatMessage = {
      sender: 'user',
      text: displayText,
      time: now,
      attachments: attachmentsToSend
    };
    this.messages.push(userMsg);
    targetSession.messages = [...this.messages];
    this.syncCurrentSession();

    // Snapshot this session's history before initiating background LLM call
    const sessionHistorySnapshot = targetSession.conversationHistory ? [...targetSession.conversationHistory] : [];

    this.userInput = '';
    this.clearAttachments();
    this.generatingSessionIds.add(targetSessionId);
    this.refreshSavedSessionsList();
    setTimeout(() => this.scrollToBottom(), 50);

    this.assistantAi.generateResponse(displayText, attachmentsToSend, sessionHistorySnapshot).subscribe({
      next: (res) => {
        this.generatingSessionIds.delete(targetSessionId);
        const assistantMsg: AssistantChatMessage = {
          sender: 'assistant',
          text: res.replyHtml,
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
          provider: this.activeProvider,
          model: this.activeModel
        };

        if (this.activeSession && this.activeSession.id === targetSessionId) {
          this.messages.push(assistantMsg);
          this.activeSession.conversationHistory = res.updatedHistory;
          this.assistantAi.setConversationHistory(res.updatedHistory);
          this.syncCurrentSession();
          this.refreshSavedSessionsList();
          setTimeout(() => this.scrollToBottom(), 50);
        } else {
          // LLM completed in background while user switched to another session!
          // Persist directly to the target session in IndexedDB without disturbing active view
          this.sessionStorage.appendMessageToSession(targetSessionId, assistantMsg, res.updatedHistory).then(() => {
            this.refreshSavedSessionsList();
          });
        }
      },
      error: () => {
        this.generatingSessionIds.delete(targetSessionId);
        const errorMsg: AssistantChatMessage = {
          sender: 'assistant',
          text: 'Üzgünüm, yanıt oluşturulurken bir bağlantı gecikmesi oluştu. Lütfen tekrar deneyiniz.',
          time: new Date().toLocaleTimeString('tr-TR', { hour: '2-digit', minute: '2-digit' }),
          provider: this.activeProvider,
          model: this.activeModel
        };

        if (this.activeSession && this.activeSession.id === targetSessionId) {
          this.messages.push(errorMsg);
          this.syncCurrentSession();
          this.refreshSavedSessionsList();
          setTimeout(() => this.scrollToBottom(), 50);
        } else {
          this.sessionStorage.appendMessageToSession(targetSessionId, errorMsg).then(() => {
            this.refreshSavedSessionsList();
          });
        }
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

  // ── DOCUMENT & TABLE FORMATTING HELPERS ───────────────────────────────────

  private convertCsvToMarkdownTable(csvText: string): string {
    const lines = csvText.split(/\r?\n/).map(l => l.trim()).filter(l => l.length > 0);
    if (lines.length === 0) return csvText;

    const parsedRows = lines.map(line => {
      // Split by comma or semicolon
      const sep = line.includes(';') ? ';' : ',';
      return line.split(sep).map(cell => cell.replace(/^["']|["']$/g, '').trim());
    });

    if (parsedRows.length === 0) return csvText;

    let md = '';
    const header = parsedRows[0];
    md += '| ' + header.join(' | ') + ' |\n';
    md += '| ' + header.map(() => '---').join(' | ') + ' |\n';

    for (let i = 1; i < Math.min(parsedRows.length, 50); i++) {
      md += '| ' + parsedRows[i].join(' | ') + ' |\n';
    }
    if (parsedRows.length > 50) {
      md += `\n*(Tablodan ilk 50 satır gösteriliyor, toplam ${parsedRows.length} satır)*\n`;
    }
    return md;
  }

  private convertTsvToMarkdownTable(tsvText: string): string {
    const lines = tsvText.split(/\r?\n/).map(l => l.trim()).filter(l => l.length > 0);
    if (lines.length === 0) return tsvText;

    const parsedRows = lines.map(line => line.split('\t').map(c => c.trim()));
    if (parsedRows.length === 0) return tsvText;

    let md = '';
    const header = parsedRows[0];
    md += '| ' + header.join(' | ') + ' |\n';
    md += '| ' + header.map(() => '---').join(' | ') + ' |\n';

    for (let i = 1; i < Math.min(parsedRows.length, 50); i++) {
      md += '| ' + parsedRows[i].join(' | ') + ' |\n';
    }
    return md;
  }

  private extractTextFromPdfDataUrl(dataUrl: string, fileName: string): string {
    try {
      const base64 = dataUrl.split(',')[1];
      const binaryString = atob(base64);
      const textMatches: string[] = [];

      // Extract text enclosed in parenthesis e.g. (Text) Tj or (Text) ' in PDF streams
      const regex = /\(([^)]+)\)\s*(Tj|'|")/g;
      let match;
      while ((match = regex.exec(binaryString)) !== null) {
        const raw = match[1];
        if (raw && raw.length > 1 && !/^[\x00-\x1F\x7F]+$/.test(raw)) {
          textMatches.push(raw);
        }
      }

      if (textMatches.length > 0) {
        return `[PDF İçeriği: ${fileName}]\n` + textMatches.join(' ').replace(/\s+/g, ' ').substring(0, 4000);
      }
    } catch {}
    return `[PDF Belgesi Eklendi: ${fileName}]`;
  }

  private extractTextFromBinary(buffer: ArrayBuffer, fileName: string): string {
    try {
      const decoder = new TextDecoder('utf-8', { fatal: false });
      const raw = decoder.decode(buffer);

      // In XLSX, text strings are stored in <t>cell text</t> or <v>123</v>
      const cellMatches: string[] = [];
      const regex = /<t[^>]*>([^<]+)<\/t>/g;
      let match;
      while ((match = regex.exec(raw)) !== null) {
        const val = match[1].trim();
        if (val.length > 0) {
          cellMatches.push(val);
        }
      }

      if (cellMatches.length > 0) {
        return `[Excel Hücre Verileri: ${fileName}]\n` + cellMatches.join(' | ').substring(0, 4000);
      }
    } catch {}
    return `[Excel Belgesi Eklendi: ${fileName}]`;
  }
}
