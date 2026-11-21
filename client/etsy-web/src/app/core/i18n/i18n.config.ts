export type AppLanguage = 'en' | 'tr';

export interface LanguageOption {
  code: AppLanguage;
  label: string;
  shortLabel: string;
  nativeName: string;
  locale: string;
  dir: 'ltr' | 'rtl';
}

export const DEFAULT_LANGUAGE: AppLanguage = 'en';
export const FALLBACK_LANGUAGE: AppLanguage = 'en';
export const LANG_STORAGE_KEY = 'etsy_web_lang';

export const SUPPORTED_LANGUAGES: readonly LanguageOption[] = [
  {
    code: 'en',
    label: 'English',
    shortLabel: 'EN',
    nativeName: 'English',
    locale: 'en-US',
    dir: 'ltr'
  },
  {
    code: 'tr',
    label: 'Turkish',
    shortLabel: 'TR',
    nativeName: 'Türkçe',
    locale: 'tr-TR',
    dir: 'ltr'
  }
] as const;

export const SUPPORTED_LANGUAGE_CODES: AppLanguage[] = SUPPORTED_LANGUAGES.map(l => l.code);

export function isSupportedLanguage(value: string | null | undefined): value is AppLanguage {
  if (!value) return false;
  return (SUPPORTED_LANGUAGE_CODES as string[]).includes(value.toLowerCase());
}

export function normalizeLanguage(value: string | null | undefined): AppLanguage | null {
  if (!value) return null;
  const base = value.trim().toLowerCase().split(/[-_]/)[0];
  return isSupportedLanguage(base) ? base : null;
}

export function getLanguageOption(code: AppLanguage): LanguageOption {
  return SUPPORTED_LANGUAGES.find(l => l.code === code) ?? SUPPORTED_LANGUAGES[0];
}
