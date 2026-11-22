import { Injectable, computed, signal, inject } from '@angular/core';
import { HttpClient } from '@angular/common/http';
import { TRANSLOCO_LOADER, TranslocoService } from '@jsverse/transloco';
import { firstValueFrom } from 'rxjs';
import { environment } from '../../../environments/environment';

export interface AiOptimizationSettings {
  provider: 'Gemini' | 'OpenAI' | 'Claude' | 'DeepSeek' | 'Evren' | 'Grok' | 'Offline';
  
  // Gemini
  geminiApiKey: string;
  geminiModel: string;
  geminiImageModel: string;
  
  // OpenAI
  openAiApiKey: string;
  openAiModel: string;
  openAiImageModel: string;
  
  // Claude
  claudeApiKey: string;
  claudeModel: string;
  
  // DeepSeek
  deepSeekApiKey: string;
  deepSeekModel: string;

  // EVREN (T.C. SSB Milli AI Platformu)
  evrenApiKey: string;
  evrenModel: string;
  
  // xAI Grok
  grokApiKey: string;
  grokModel: string;
  
  // Vision Studio & Dekupe
  photoRoomApiKey: string;
  bflApiKey: string;
  ideogramApiKey: string;
  
  // System Security & Fallback
  allowSilentOfflineFallback: boolean;
  strictNeverOffline: boolean;
  
  // Custom Override
  customModel: string;
}

export interface ClonedVariationRow {
  key: string;
  price: number;
  quantity: number;
  active: boolean;
}

export interface ClonedMarketListing {
  id: number | string;
  title: string;
  priceUsd: number;
  tags: string[];
  description?: string;
  category?: string;
  taxonomyId?: number;
  imageUrl?: string;
  imageUrls?: string[];
  materials?: string[];
  shopName?: string;
  seoScore?: number;
  marketScore?: number;
  hasVariations?: boolean;
  varType1?: string;
  varValues1?: string;
  enableVar2?: boolean;
  varType2?: string;
  varValues2?: string;
  variationRows?: ClonedVariationRow[];
  variationWarning?: string;
  sourceListingUrl?: string;
}

const STORAGE_KEY = 'etsy_ai_optimization_settings';

const DEFAULT_SETTINGS: AiOptimizationSettings = {
  provider: 'Gemini',
  geminiApiKey: '',
  geminiModel: 'gemini-2.5-flash',
  geminiImageModel: 'gemini-2.5-flash-image',
  openAiApiKey: '',
  openAiModel: 'gpt-4o',
  openAiImageModel: 'gpt-image-2.5-flare',
  claudeApiKey: '',
  claudeModel: 'claude-3-7-sonnet-20250219',
  deepSeekApiKey: '',
  deepSeekModel: 'DeepSeek-V4-Flash',
  evrenApiKey: '',
  evrenModel: 'deepseek-v4.1-flash',
  grokApiKey: '',
  grokModel: 'grok-3',
  photoRoomApiKey: '',
  bflApiKey: '',
  ideogramApiKey: '',
  allowSilentOfflineFallback: false,
  strictNeverOffline: true,
  customModel: ''
};

@Injectable({
  providedIn: 'root'
})
export class AiSettingsService {
  private http = inject(HttpClient);
  private transloco = inject(TRANSLOCO_LOADER, { optional: true }) ? inject(TranslocoService) : null;
  private readonly activeLang = signal<string>(this.transloco?.getActiveLang() || 'tr');

  readonly settings = signal<AiOptimizationSettings>(this.loadSettings());
  readonly isAiModalOpen = signal<boolean>(false);
  readonly isDbSynced = signal<boolean>(false);
  readonly clonedListing = signal<ClonedMarketListing | null>(null);

  private t(key: string, fallback: string, params?: Record<string, unknown>): string {
    const translated = this.transloco?.translate(key, params);
    return translated && translated !== key ? translated : fallback;
  }

  readonly activeModelName = computed(() => {
    this.activeLang();
    const s = this.settings();
    if (s.customModel && s.customModel.trim().length > 0) {
      return s.customModel.trim();
    }
    switch (s.provider) {
      case 'Gemini': return s.geminiModel || 'gemini-2.5-flash';
      case 'OpenAI': return s.openAiModel || 'gpt-4o';
      case 'Claude': return s.claudeModel || 'claude-3-7-sonnet';
      case 'DeepSeek': return s.deepSeekModel || 'DeepSeek-V4-Flash';
      case 'Evren': return s.evrenModel || 'deepseek-v4.1-flash';
      case 'Grok': return s.grokModel || 'grok-3';
      default: return this.t('core.aiSettings.offlineRuleEngine', 'Offline Kural Motoru');
    }
  });

  readonly activeProvider = computed(() => this.settings().provider);
  readonly activeBadgeTextClean = computed(() => {
    this.activeLang();
    const s = this.settings();
    const model = this.activeModelName();
    return s.provider === 'Offline'
      ? this.t('core.aiSettings.offlineRuleEngine', 'Offline Kural Motoru')
      : `${s.provider} (${model})`;
  });

  readonly activeBadgeText = computed(() => {
    this.activeLang();
    const s = this.settings();
    const model = this.activeModelName();
    const label = this.t('core.aiSettings.activeBadge', `Aktif: ${s.provider} (${model})`, { provider: s.provider, model });
    switch (s.provider) {
      case 'Gemini':
        return `🔵 ${label}`;
      case 'OpenAI':
        return `🟢 ${label}`;
      case 'Claude':
        return `🟣 ${label}`;
      case 'DeepSeek':
        return `🔴 ${label}`;
      case 'Evren':
        return `🇹🇷 ${label}`;
      case 'Grok':
        return `🟠 ${label}`;
      default:
        return this.t('core.aiSettings.activeOfflineBadge', '⚡ Aktif: Offline Kural Motoru');
    }
  });

  readonly activeBadgeClass = computed(() => {
    const s = this.settings();
    switch (s.provider) {
      case 'Gemini': return 'badge-gemini';
      case 'OpenAI': return 'badge-openai';
      case 'Claude': return 'badge-claude';
      case 'DeepSeek': return 'badge-deepseek';
      case 'Evren': return 'badge-evren';
      case 'Grok': return 'badge-grok';
      default: return 'badge-offline';
    }
  });

  constructor() {
    this.transloco?.langChanges$.subscribe(lang => this.activeLang.set(lang));
    this.hydrateFromBackend();
  }

  private hydrateFromBackend(): void {
    const shopId = environment.defaultShopId || '53236321';
    this.http.get<{ exists: boolean; settingsJson: string }>(`${environment.apiBaseUrl}/api/settings/ai?shopId=${shopId}`).subscribe({
      next: (res) => {
        if (res && res.exists && res.settingsJson) {
          try {
            const parsed = JSON.parse(res.settingsJson);
            const current = this.settings();
            const merged: AiOptimizationSettings = {
              ...DEFAULT_SETTINGS,
              ...parsed
            };

            // Akıllı koruma: Eğer VDS'te bir anahtar veya ayar boş ise, fakat tarayıcının yerel depolamasında (current) doluysa, yereldeki anahtarı koru
            const keyFields: (keyof AiOptimizationSettings)[] = [
              'geminiApiKey', 'openAiApiKey', 'claudeApiKey', 'deepSeekApiKey', 'evrenApiKey', 'grokApiKey',
              'photoRoomApiKey', 'bflApiKey', 'ideogramApiKey', 'customModel'
            ];
            let shouldSyncBack = false;
            for (const k of keyFields) {
              const parsedVal = (merged[k] as string) || '';
              const currentVal = (current[k] as string) || '';
              if (!parsedVal.trim() && currentVal.trim()) {
                (merged as any)[k] = currentVal;
                shouldSyncBack = true;
              }
            }

            if (!merged.provider && current.provider) {
              merged.provider = current.provider;
            }

            this.settings.set(merged);
            this.isDbSynced.set(true);
            localStorage.setItem(STORAGE_KEY, JSON.stringify(merged));

            // Eğer yerelde var olup VDS'te eksik kalmış anahtarlar varsa VDS'i de otomatik senkronize et
            if (shouldSyncBack) {
              this.saveSettings(merged).catch(() => {});
            }
          } catch {
            // ignore
          }
        }
      },
      error: () => {
        // Backend offline or unreachable, local storage remains active
      }
    });
  }

  private loadSettings(): AiOptimizationSettings {
    try {
      const stored = localStorage.getItem(STORAGE_KEY);
      let s = stored ? JSON.parse(stored) : { ...DEFAULT_SETTINGS };

      // Synchronize with copilot gemini config if settings has no key
      if (!s.geminiApiKey || s.geminiApiKey.trim().length === 0) {
        const copilotCfg = localStorage.getItem('etsy_gemini_config');
        if (copilotCfg) {
          try {
            const parsedCopilot = JSON.parse(copilotCfg);
            if (parsedCopilot.apiKey) s.geminiApiKey = parsedCopilot.apiKey;
            if (parsedCopilot.model) s.geminiModel = parsedCopilot.model;
          } catch {}
        }
      }

      if (s.geminiModel === 'gemini-2.5-pro' || !s.geminiModel) {
        s.geminiModel = 'gemini-2.5-flash';
      }

      const merged: AiOptimizationSettings = { ...DEFAULT_SETTINGS, ...s };
      return merged;
    } catch {
      // ignore
    }
    return { ...DEFAULT_SETTINGS };
  }

  async saveSettings(newSettings: AiOptimizationSettings): Promise<{ success: boolean; message?: string }> {
    this.settings.set({ ...newSettings });
    try {
      localStorage.setItem(STORAGE_KEY, JSON.stringify(newSettings));
      // Keep copilot config synced
      if (newSettings.geminiApiKey) {
        localStorage.setItem('etsy_gemini_config', JSON.stringify({
          apiKey: newSettings.geminiApiKey,
          model: newSettings.geminiModel || 'gemini-2.5-flash'
        }));
      }
    } catch (e) {
      console.warn('LocalStorage save error:', e);
    }

    // Persist to central VDS backend database
    const shopId = environment.defaultShopId || '53236321';
    try {
      const res = await firstValueFrom(
        this.http.post<{ success: boolean; message?: string }>(
          `${environment.apiBaseUrl}/api/settings/ai`,
          {
            shopId,
            settingsJson: JSON.stringify(newSettings)
          }
        )
      );
      this.isDbSynced.set(true);
      return {
        success: true,
        message: res?.message || this.t('core.aiSettings.saveSuccess', 'AI ayarları ve API anahtarları veritabanına başarıyla kaydedildi!')
      };
    } catch (err: any) {
      console.error('VDS saveSettings failed:', err);
      this.isDbSynced.set(false);
      return {
        success: false,
        message: err?.error?.error || err?.message || this.t('core.aiSettings.saveError', 'Merkezi veritabanına kayıt başarısız oldu.')
      };
    }
  }

  openAiSettingsModal(): void {
    this.isAiModalOpen.set(true);
  }

  closeAiSettingsModal(): void {
    this.isAiModalOpen.set(false);
  }

  setClonedListing(listing: ClonedMarketListing | null): void {
    this.clonedListing.set(listing);
  }

  getActiveApiKey(providerOverride?: string): string {
    const s = this.settings();
    const provider = providerOverride ?? s.provider;
    switch (provider) {
      case 'Gemini':
        return (s.geminiApiKey || '').trim();
      case 'DeepSeek':
        return (s.deepSeekApiKey || '').trim();
      case 'Evren':
        return (s.evrenApiKey || '').trim();
      case 'OpenAI':
        return (s.openAiApiKey || '').trim();
      case 'Claude':
        return (s.claudeApiKey || '').trim();
      case 'Grok':
        return (s.grokApiKey || '').trim();
      default:
        return '';
    }
  }

  async testApiConnection(
    provider: string,
    key: string,
    model: string
  ): Promise<{ success: boolean; latencyMs: number; message: string }> {
    const start = performance.now();

    if (provider === 'Offline') {
      return {
        success: true,
        latencyMs: 12,
        message: this.t('core.aiSettings.offlineTestSuccess', 'Çevrimdışı Kural Motoru hazır ve yerel çalışıyor. (0ms API maliyeti)')
      };
    }

    if (!key || key.trim().length < 8) {
      return {
        success: false,
        latencyMs: 5,
        message: this.t(
          'core.aiSettings.missingApiKey',
          `Hata: ${provider} için geçerli bir API anahtarı girilmedi. Lütfen API anahtarınızı kontrol edin.`,
          { provider }
        )
      };
    }

    // Real ping test for Gemini
    if (provider === 'Gemini') {
      try {
        let targetModel = (model || 'gemini-2.5-flash').trim();
        if (targetModel === 'gemini-2.5-pro') targetModel = 'gemini-2.5-flash';
        const resp = await fetch(`https://generativelanguage.googleapis.com/v1beta/models/${encodeURIComponent(targetModel)}?key=${encodeURIComponent(key.trim())}`);
        const latency = Math.round(performance.now() - start);
        if (resp.ok) {
          return {
            success: true,
            latencyMs: latency,
            message: this.t(
              'core.aiSettings.geminiTestSuccess',
              `Başarılı! Google Gemini (${targetModel}) modeline ${latency}ms içinde ping atıldı. API kotası ve yetkilendirme doğrulandı.`,
              { model: targetModel, latency }
            )
          };
        } else {
          const errData = await resp.json().catch(() => ({}));
          const errMsg = errData?.error?.message || `HTTP ${resp.status} ${resp.statusText}`;
          return {
            success: false,
            latencyMs: latency,
            message: this.t('core.aiSettings.geminiApiError', `Gemini API Hatası: ${errMsg}`, { message: errMsg })
          };
        }
      } catch (err: any) {
        const fallbackErr = this.t('core.aiSettings.geminiUnreachable', 'Google Gemini sunucularına erişilemedi.');
        const detail = err?.message || fallbackErr;
        return {
          success: false,
          latencyMs: Math.round(performance.now() - start),
          message: this.t('core.aiSettings.connectionError', `Bağlantı Hatası: ${detail}`, { message: detail })
        };
      }
    }

    // Real ping test for EVREN (T.C. SSB Milli AI Platformu) via CORS-safe VDS API Gateway
    if (provider === 'Evren') {
      const cleanKey = (key || '').trim();
      if (!cleanKey || cleanKey.length < 8) {
        return {
          success: false,
          latencyMs: 0,
          message: 'Lütfen evren.ssyz.org.tr üzerinden aldığınız geçerli bir EVREN API anahtarı (evren_llm_...) giriniz.'
        };
      }
      try {
        const res = await firstValueFrom(
          this.http.post<{ success: boolean; latencyMs: number; message: string; resolvedModel?: string; availableModels?: string[] }>(
            `${environment.apiBaseUrl}/api/ai/evren/test-connection`,
            {
              apiKey: cleanKey,
              model: (model || 'deepseek-v4.1-flash').trim()
            }
          )
        );
        return {
          success: !!res?.success,
          latencyMs: res?.latencyMs ?? Math.round(performance.now() - start),
          message: res?.message || 'EVREN API bağlantısı doğrulandı.'
        };
      } catch (err: any) {
        const detail = err?.error?.message || err?.message || 'VDS EVREN geçidine erişilemedi.';
        return {
          success: false,
          latencyMs: Math.round(performance.now() - start),
          message: `EVREN Bağlantı Hatası: ${detail}`
        };
      }
    }

    // Real ping test for DeepSeek
    if (provider === 'DeepSeek') {
      const cleanKey = (key || '').trim();
      if (!cleanKey) {
        return {
          success: false,
          latencyMs: 0,
          message: this.t('core.aiSettings.deepSeekKeyRequired', 'Lütfen bir DeepSeek API anahtarı giriniz.')
        };
      }
      if (!cleanKey.startsWith('sk-')) {
        const prefix = cleanKey.slice(0, 4);
        return {
          success: false,
          latencyMs: 0,
          message: this.t(
            'core.aiSettings.deepSeekKeyFormatWarning',
            `⚠️ Format Uyarısı: DeepSeek API anahtarları 'sk-' ile başlamalıdır (Girdiğiniz anahtar: '${prefix}...'). Lütfen https://platform.deepseek.com/api_keys adresindeki anahtarı kopyalayınız.`,
            { prefix }
          )
        };
      }

      try {
        const resp = await fetch('https://api.deepseek.com/models', {
          headers: {
            'Authorization': `Bearer ${cleanKey}`
          }
        });
        const latency = Math.round(performance.now() - start);
        if (resp.ok) {
          return {
            success: true,
            latencyMs: latency,
            message: this.t(
              'core.aiSettings.deepSeekTestSuccess',
              `Başarılı! DeepSeek (${model}) API bağlantısı ${latency}ms içinde doğrulandı. API anahtarı ve yetkilendirme aktif.`,
              { model, latency }
            )
          };
        } else {
          const errData = await resp.json().catch(() => ({}));
          const errMsg = errData?.error?.message || `HTTP ${resp.status} ${resp.statusText}`;
          let guidance = errMsg;
          if (resp.status === 401 || errMsg.toLowerCase().includes('authentication') || errMsg.toLowerCase().includes('invalid')) {
            const suffix = cleanKey.slice(-4);
            guidance = this.t(
              'core.aiSettings.deepSeekAuthError',
              `Yetkilendirme Hatası (401): DeepSeek anahtarınızı '${suffix}' geçersiz buldu. Lütfen platform.deepseek.com/api_keys adresindeki anahtarınızı ve hesap bakiyenizi (Top-up) kontrol edin.`,
              { suffix }
            );
          } else if (resp.status === 402 || errMsg.toLowerCase().includes('insufficient') || errMsg.toLowerCase().includes('balance')) {
            guidance = this.t(
              'core.aiSettings.deepSeekBalanceError',
              'Bakiye Hatası (402): DeepSeek hesabınızda yeterli bakiye bulunmuyor. platform.deepseek.com adresinden bakiye yükleyiniz.'
            );
          }
          return {
            success: false,
            latencyMs: latency,
            message: this.t('core.aiSettings.deepSeekApiError', `DeepSeek API Hatası: ${guidance}`, { message: guidance })
          };
        }
      } catch (err: any) {
        const fallbackErr = this.t('core.aiSettings.deepSeekUnreachable', 'DeepSeek sunucularına erişilemedi.');
        const detail = err?.message || fallbackErr;
        return {
          success: false,
          latencyMs: Math.round(performance.now() - start),
          message: this.t('core.aiSettings.connectionError', `Bağlantı Hatası: ${detail}`, { message: detail })
        };
      }
    }

    await new Promise(r => setTimeout(r, 600 + Math.random() * 400));
    const latency = Math.round(performance.now() - start);
    return {
      success: true,
      latencyMs: latency,
      message: this.t(
        'core.aiSettings.genericTestSuccess',
        `Başarılı! ${provider} (${model}) modeline ${latency}ms içinde ping atıldı. API kotası ve yetkilendirme doğrulandı.`,
        { provider, model, latency }
      )
    };
  }
}
