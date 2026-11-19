import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { catchError, throwError } from 'rxjs';
import { AuthService } from '../services/auth.service';
import { environment } from '../../../environments/environment';

export const jwtInterceptor: HttpInterceptorFn = (req, next) => {
  // 1. If request already has an Authorization header (e.g. DeepSeek, Gemini, OpenAI API keys), do NOT overwrite it!
  if (req.headers.has('Authorization')) {
    return next(req);
  }

  // 2. Third-party public APIs (DeepSeek, Gemini, OpenAI, Claude, Grok, etc.) must NEVER receive internal JWT
  const isExternalApi = req.url.startsWith('https://api.deepseek.com') ||
                        req.url.startsWith('https://generativelanguage.googleapis.com') ||
                        req.url.startsWith('https://api.openai.com') ||
                        req.url.startsWith('https://api.anthropic.com') ||
                        req.url.startsWith('https://api.x.ai');

  if (isExternalApi) {
    return next(req);
  }

  // 3. Only attach JWT token to internal VDS backend requests
  const hasValidBaseUrl = typeof environment.apiBaseUrl === 'string' && environment.apiBaseUrl.trim().length > 0;
  const isInternalApi = (hasValidBaseUrl && req.url.startsWith(environment.apiBaseUrl)) || 
                        req.url.startsWith('/api') || 
                        req.url.includes('5.180.81.148:5263') ||
                        req.url.includes('localhost:5263');

  if (isInternalApi) {
    const authService = inject(AuthService);
    const token = localStorage.getItem('etsy_web_jwt_token');
    const isAuthEndpoint = req.url.includes('/api/auth/login') ||
                           req.url.includes('/api/auth/register') ||
                           req.url.includes('/api/auth/logout');

    const outgoing = token
      ? req.clone({ setHeaders: { Authorization: `Bearer ${token}` } })
      : req;

    return next(outgoing).pipe(
      catchError((err: unknown) => {
        if (err instanceof HttpErrorResponse && err.status === 401 && !isAuthEndpoint) {
          const msg = typeof err.error?.error === 'string' ? err.error.error : '';
          if (msg.includes('Bearer token') || msg.includes('Yetkisiz erişim') || msg.includes('Oturum sonlandırılmış')) {
            authService.logout(false);
          }
        }
        return throwError(() => err);
      })
    );
  }

  return next(req);
};
