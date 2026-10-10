import { HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
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
    const token = localStorage.getItem('etsy_web_jwt_token');
    if (token) {
      const cloned = req.clone({
        setHeaders: {
          Authorization: `Bearer ${token}`
        }
      });
      return next(cloned);
    }
  }

  return next(req);
};
