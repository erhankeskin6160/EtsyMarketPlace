import { Injectable } from '@angular/core';
import { AssistantChatMessage, ChatAttachment } from './store-ai-assistant.service';

export interface CopilotChatSession {
  id: string;               // e.g. "sess_1730000000_abc"
  title: string;            // e.g. "✈️ Antonov An-2 Kit Card Analizi"
  createdAt: number;
  updatedAt: number;
  messages: AssistantChatMessage[];
  conversationHistory: {
    role: 'user' | 'assistant' | 'system';
    content: string;
    attachments?: ChatAttachment[];
  }[];
}

@Injectable({
  providedIn: 'root'
})
export class CopilotSessionStorageService {
  private readonly DB_NAME = 'EtsyCopilotDb';
  private readonly DB_VERSION = 1;
  private readonly STORE_NAME = 'chat_sessions';
  private readonly ACTIVE_ID_KEY = 'etsy_copilot_active_session_id';

  private dbPromise: Promise<IDBDatabase> | null = null;

  constructor() {
    this.initDb().catch(err => {
      console.warn('[CopilotSessionStorage] IndexedDB initialization warning:', err);
    });
  }

  private initDb(): Promise<IDBDatabase> {
    if (this.dbPromise) return this.dbPromise;

    this.dbPromise = new Promise((resolve, reject) => {
      if (typeof window === 'undefined' || !window.indexedDB) {
        reject(new Error('IndexedDB not supported'));
        return;
      }

      const req = window.indexedDB.open(this.DB_NAME, this.DB_VERSION);

      req.onupgradeneeded = (e: IDBVersionChangeEvent) => {
        const db = (e.target as IDBOpenDBRequest).result;
        if (!db.objectStoreNames.contains(this.STORE_NAME)) {
          const store = db.createObjectStore(this.STORE_NAME, { keyPath: 'id' });
          store.createIndex('updatedAt', 'updatedAt', { unique: false });
        }
      };

      req.onsuccess = () => resolve(req.result);
      req.onerror = () => reject(req.error);
    });

    return this.dbPromise;
  }

  public getActiveSessionId(): string | null {
    try {
      return localStorage.getItem(this.ACTIVE_ID_KEY);
    } catch {
      return null;
    }
  }

  public setActiveSessionId(id: string): void {
    try {
      localStorage.setItem(this.ACTIVE_ID_KEY, id);
    } catch {}
  }

  public clearActiveSessionId(): void {
    try {
      localStorage.removeItem(this.ACTIVE_ID_KEY);
    } catch {}
  }

  public async getAllSessions(): Promise<CopilotChatSession[]> {
    try {
      const db = await this.initDb();
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readonly');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.getAll();

        req.onsuccess = () => {
          const list = (req.result || []) as CopilotChatSession[];
          list.sort((a, b) => b.updatedAt - a.updatedAt);
          resolve(list);
        };
        req.onerror = () => reject(req.error);
      });
    } catch (err) {
      console.warn('[CopilotSessionStorage] IndexedDB read error:', err);
      return [];
    }
  }

  public async getSession(id: string): Promise<CopilotChatSession | null> {
    try {
      const db = await this.initDb();
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readonly');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.get(id);

        req.onsuccess = () => resolve(req.result || null);
        req.onerror = () => reject(req.error);
      });
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to get session:', err);
      return null;
    }
  }

  public async saveSession(session: CopilotChatSession): Promise<void> {
    try {
      const db = await this.initDb();
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readwrite');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.put(session);

        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
      });
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to save session:', err);
    }
  }

  public async deleteSession(id: string): Promise<void> {
    try {
      const db = await this.initDb();
      return new Promise((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readwrite');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.delete(id);

        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
      });
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to delete session:', err);
    }
  }

  public async appendMessageToSession(
    sessionId: string,
    message: AssistantChatMessage,
    history?: { role: 'user' | 'assistant' | 'system'; content: string; attachments?: ChatAttachment[] }[]
  ): Promise<void> {
    try {
      const session = await this.getSession(sessionId);
      if (session) {
        session.messages = session.messages || [];
        session.messages.push(message);
        if (history && history.length > 0) {
          session.conversationHistory = history;
        }
        session.updatedAt = Date.now();
        await this.saveSession(session);
      }
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to append message to session:', sessionId, err);
    }
  }

  public createNewSession(initialTitle = 'Yeni Sohbet'): CopilotChatSession {
    const now = Date.now();
    const id = 'sess_' + now + '_' + Math.random().toString(36).substring(2, 7);
    const session: CopilotChatSession = {
      id,
      title: initialTitle,
      createdAt: now,
      updatedAt: now,
      messages: [],
      conversationHistory: []
    };
    this.setActiveSessionId(id);
    return session;
  }
}
