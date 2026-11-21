import { DOCUMENT } from '@angular/common';
import { Injectable, computed, inject, signal } from '@angular/core';
import { TranslocoService } from '@jsverse/transloco';
import { Observable, firstValueFrom } from 'rxjs';
import {
  AppLanguage,
  DEFAULT_LANGUAGE,
  LANG_STORAGE_KEY,
  LanguageOption,
  SUPPORTED_LANGUAGES,
  getLanguageOption,
  normalizeLanguage
} from './i18n.config';

@Injectable({ providedIn: 'root' })
export class LanguageService {
  private readonly transloco = inject(TranslocoService);
  private readonly document = inject(DOCUMENT);

  readonly supportedLanguages: readonly LanguageOption[] = SUPPORTED_LANGUAGES;
  readonly activeLang = signal<AppLanguage>(this.resolveInitialLanguage());
  readonly currentOption = computed<LanguageOption>(() => getLanguageOption(this.activeLang()));
  readonly locale = computed<string>(() => this.currentOption().locale);
  readonly dir = computed<'ltr' | 'rtl'>(() => this.currentOption().dir);

  /**
   * Called via APP_INITIALIZER before first render to prevent FOUC (raw translation keys).
   */
  async init(): Promise<void> {
    const initial = this.activeLang();
    this.transloco.setActiveLang(initial);
    this.applyDocumentMetadata(initial);

    try {
      await firstValueFrom(this.transloco.load(initial));
      this.applyDocumentTitle();
    } catch (err) {
      console.error('[i18n] Failed to preload initial language:', initial, err);
    }
  }

  setLanguage(lang: AppLanguage): void {
    const normalized = normalizeLanguage(lang) ?? DEFAULT_LANGUAGE;
    if (normalized === this.activeLang() && this.transloco.getActiveLang() === normalized) {
      return;
    }

    this.activeLang.set(normalized);
    this.persistLanguage(normalized);
    this.transloco.setActiveLang(normalized);
    this.applyDocumentMetadata(normalized);

    this.transloco.load(normalized).subscribe({
      next: () => this.applyDocumentTitle(),
      error: (err) => console.error('[i18n] Failed to load language:', normalized, err)
    });
  }

  translate(key: string, params?: Record<string, unknown>): string {
    return this.transloco.translate(key, params);
  }

  selectTranslate(key: string, params?: Record<string, unknown>): Observable<string> {
    return this.transloco.selectTranslate(key, params);
  }

  /**
   * Returns a concise language directive for AI Copilot / Financial AI prompts.
   * Note: Etsy listing SEO output language remains decoupled from the UI language.
   */
  getAiResponseLanguageInstruction(): string {
    if (this.activeLang() === 'tr') {
      return 'YANIT DİLİ KURALI: Kullanıcının arayüz dili TÜRKÇE (tr). Kullanıcı farklı bir dilde özellikle talep etmedikçe tüm sohbet, analiz ve finansal açıklamalarını doğal ve akıcı Türkçe ile yaz. (Etsy ilan başlıkları/etiketleri hedef pazar dilinde kalabilir.)';
    }
    return 'RESPONSE LANGUAGE RULE: The user\'s active UI language is ENGLISH (en). Unless the user explicitly asks for another language, write all chat replies, explanations, and financial analyses in clear, natural English. (Etsy listing titles/tags should remain in the target marketplace language.)';
  }

  private resolveInitialLanguage(): AppLanguage {
    try {
      const saved = localStorage.getItem(LANG_STORAGE_KEY);
      const parsed = normalizeLanguage(saved);
      if (parsed) {
        return parsed;
      }
    } catch {
      // Ignore storage access errors in restricted browser contexts
    }
    return DEFAULT_LANGUAGE;
  }

  private persistLanguage(lang: AppLanguage): void {
    try {
      localStorage.setItem(LANG_STORAGE_KEY, lang);
    } catch {
      // Ignore storage access errors
    }
  }

  private applyDocumentMetadata(lang: AppLanguage): void {
    const option = getLanguageOption(lang);
    const htmlEl = this.document?.documentElement;
    if (htmlEl) {
      htmlEl.lang = option.code;
      htmlEl.dir = option.dir;
    }
  }

  private applyDocumentTitle(): void {
    const translatedTitle = this.transloco.translate('common.appTitle');
    if (translatedTitle && translatedTitle !== 'common.appTitle' && this.document) {
      this.document.title = translatedTitle;
    }
  }
}
