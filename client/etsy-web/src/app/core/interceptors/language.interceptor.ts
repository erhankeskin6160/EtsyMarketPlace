import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { LanguageService } from '../i18n/language.service';

const SKIP_PREFIXES = [
  '/i18n/',
  'https://api.deepseek.com',
  'https://generativelanguage.googleapis.com',
  'https://api.openai.com',
  'https://api.anthropic.com',
  'https://api.x.ai'
];

export const languageInterceptor: HttpInterceptorFn = (req, next) => {
  if (SKIP_PREFIXES.some(prefix => req.url.startsWith(prefix))) {
    return next(req);
  }

  const languageService = inject(LanguageService);
  const lang = languageService.activeLang();

  const headers: Record<string, string> = {};
  if (!req.headers.has('Accept-Language')) {
    headers['Accept-Language'] = lang;
  }
  if (!req.headers.has('X-App-Language')) {
    headers['X-App-Language'] = lang;
  }

  if (Object.keys(headers).length === 0) {
    return next(req);
  }

  const cloned = req.clone({ setHeaders: headers });

  return next(cloned);
};
