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
  private readonly BACKUP_PREFIX = 'etsy_copilot_backup_';

  private dbPromise: Promise<IDBDatabase> | null = null;
  /**
   * Synchronous in-memory session cache to completely eliminate race conditions
   * between user message typing and asynchronous background LLM callbacks.
   */
  private sessionCache = new Map<string, CopilotChatSession>();

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
      const idbList: CopilotChatSession[] = await new Promise((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readonly');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.getAll();

        req.onsuccess = () => resolve((req.result || []) as CopilotChatSession[]);
        req.onerror = () => reject(req.error);
      });

      // Merge with in-memory cache to ensure zero lag
      const mergedMap = new Map<string, CopilotChatSession>();
      for (const s of idbList) {
        mergedMap.set(s.id, s);
      }
      for (const [id, cached] of this.sessionCache.entries()) {
        const existing = mergedMap.get(id);
        if (!existing || cached.updatedAt >= existing.updatedAt) {
          mergedMap.set(id, cached);
        }
      }

      const list = Array.from(mergedMap.values());
      list.sort((a, b) => b.updatedAt - a.updatedAt);
      return list;
    } catch (err) {
      console.warn('[CopilotSessionStorage] IndexedDB read error, falling back to cache:', err);
      const list = Array.from(this.sessionCache.values());
      list.sort((a, b) => b.updatedAt - a.updatedAt);
      return list;
    }
  }

  public async getSession(id: string): Promise<CopilotChatSession | null> {
    // 1. Check synchronous in-memory cache first (prevents race conditions)
    if (this.sessionCache.has(id)) {
      const cached = this.sessionCache.get(id)!;
      return JSON.parse(JSON.stringify(cached));
    }

    // 2. Check IndexedDB
    try {
      const db = await this.initDb();
      const session = await new Promise<CopilotChatSession | null>((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readonly');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.get(id);

        req.onsuccess = () => resolve(req.result || null);
        req.onerror = () => reject(req.error);
      });

      if (session) {
        this.sessionCache.set(id, JSON.parse(JSON.stringify(session)));
        return session;
      }
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to get session from IDB:', err);
    }

    // 3. Fallback to LocalStorage backup
    try {
      const backupRaw = localStorage.getItem(this.BACKUP_PREFIX + id);
      if (backupRaw) {
        const backupSession = JSON.parse(backupRaw) as CopilotChatSession;
        this.sessionCache.set(id, backupSession);
        return backupSession;
      }
    } catch {}

    return null;
  }

  public async saveSession(session: CopilotChatSession): Promise<void> {
    if (!session || !session.id) return;

    // 1. Synchronously update in-memory cache immediately
    const cloned = JSON.parse(JSON.stringify(session));
    this.sessionCache.set(session.id, cloned);

    // 2. Immediate LocalStorage backup (safeguards against browser reload during LLM generation)
    try {
      localStorage.setItem(this.BACKUP_PREFIX + session.id, JSON.stringify(cloned));
    } catch {}

    // 3. Persist to IndexedDB
    try {
      const db = await this.initDb();
      await new Promise<void>((resolve, reject) => {
        const tx = db.transaction(this.STORE_NAME, 'readwrite');
        const store = tx.objectStore(this.STORE_NAME);
        const req = store.put(session);

        req.onsuccess = () => resolve();
        req.onerror = () => reject(req.error);
      });
    } catch (err) {
      console.warn('[CopilotSessionStorage] Failed to save session to IDB:', err);
    }
  }

  public async deleteSession(id: string): Promise<void> {
    this.sessionCache.delete(id);
    try {
      localStorage.removeItem(this.BACKUP_PREFIX + id);
    } catch {}

    try {
      const db = await this.initDb();
      await new Promise<void>((resolve, reject) => {
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
    this.sessionCache.set(id, JSON.parse(JSON.stringify(session)));
    this.setActiveSessionId(id);
    return session;
  }
}
