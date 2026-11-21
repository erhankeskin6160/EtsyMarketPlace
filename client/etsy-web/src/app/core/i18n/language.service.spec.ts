import { TestBed } from '@angular/core/testing';
import { provideTransloco, Translation, TranslocoLoader } from '@jsverse/transloco';
import { Observable, of } from 'rxjs';
import { LanguageService } from './language.service';
import { DEFAULT_LANGUAGE, LANG_STORAGE_KEY, SUPPORTED_LANGUAGE_CODES } from './i18n.config';

class MockTranslocoLoader implements TranslocoLoader {
  private readonly translations: Record<string, Translation> = {
    en: { common: { appTitle: 'Etsy Studio EN', save: 'Save' } },
    tr: { common: { appTitle: 'Etsy Studio TR', save: 'Kaydet' } }
  };

  getTranslation(lang: string): Observable<Translation> {
    return of(this.translations[lang] ?? {});
  }
}

describe('LanguageService', () => {
  beforeEach(() => {
    localStorage.removeItem(LANG_STORAGE_KEY);
    TestBed.configureTestingModule({
      providers: [
        provideTransloco({
          config: {
            availableLangs: SUPPORTED_LANGUAGE_CODES,
            defaultLang: DEFAULT_LANGUAGE,
            reRenderOnLangChange: true
          },
          loader: MockTranslocoLoader
        })
      ]
    });
  });

  afterEach(() => {
    localStorage.removeItem(LANG_STORAGE_KEY);
  });

  it('defaults to English (en) when localStorage is empty', async () => {
    const service = TestBed.inject(LanguageService);
    await service.init();

    expect(service.activeLang()).toBe('en');
    expect(service.locale()).toBe('en-US');
    expect(document.documentElement.lang).toBe('en');
    expect(service.translate('common.save')).toBe('Save');
  });

  it('restores saved language from localStorage on startup', async () => {
    localStorage.setItem(LANG_STORAGE_KEY, 'tr');
    const service = TestBed.inject(LanguageService);
    await service.init();

    expect(service.activeLang()).toBe('tr');
    expect(service.locale()).toBe('tr-TR');
    expect(document.documentElement.lang).toBe('tr');
    expect(service.translate('common.save')).toBe('Kaydet');
  });

  it('switches language at runtime, persists to localStorage, and updates AI prompt directive', async () => {
    const service = TestBed.inject(LanguageService);
    await service.init();

    service.setLanguage('tr');
    expect(service.activeLang()).toBe('tr');
    expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('tr');
    expect(document.documentElement.lang).toBe('tr');
    expect(service.translate('common.save')).toBe('Kaydet');
    expect(service.getAiResponseLanguageInstruction()).toContain('TÜRKÇE');

    service.setLanguage('en');
    expect(service.activeLang()).toBe('en');
    expect(localStorage.getItem(LANG_STORAGE_KEY)).toBe('en');
    expect(service.getAiResponseLanguageInstruction()).toContain('ENGLISH');
  });
});
