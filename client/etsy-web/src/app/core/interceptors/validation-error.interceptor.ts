import { HttpErrorResponse, HttpInterceptorFn } from '@angular/common/http';
import { inject } from '@angular/core';
import { ToastrService } from 'ngx-toastr';
import { catchError, throwError } from 'rxjs';
import { extractClientValidationErrors, formatValidationToastHtml } from '../utils/validation-error.util';

const EXTERNAL_API_PREFIXES = [
  'https://api.deepseek.com',
  'https://generativelanguage.googleapis.com',
  'https://api.openai.com',
  'https://api.anthropic.com',
  'https://api.x.ai'
];

export const validationErrorInterceptor: HttpInterceptorFn = (req, next) => {
  if (EXTERNAL_API_PREFIXES.some(prefix => req.url.startsWith(prefix))) {
    return next(req);
  }

  const toastr = inject(ToastrService);
  const suppressToast = req.headers.has('X-Suppress-Error-Toast');

  return next(req).pipe(
    catchError((err: unknown) => {
      if (!(err instanceof HttpErrorResponse)) {
        return throwError(() => err);
      }

      const parsed = extractClientValidationErrors(err);

      if (!suppressToast && err.status !== 401) {
        const html = formatValidationToastHtml(parsed);
        if (err.status === 400 || parsed.isValidationError) {
          toastr.warning(html, 'Doğrulama Uyarısı');
        } else if (req.method !== 'GET' || err.status >= 500 || err.status === 0) {
          toastr.error(html, 'İşlem Hatası');
        }
      }

      const existingBody = err.error && typeof err.error === 'object' ? err.error : {};
      const normalizedBody = {
        ...existingBody,
        success: false,
        statusCode: err.status || (existingBody as any).statusCode || 400,
        message: parsed.summary,
        error: parsed.summary,
        errors: (existingBody as any).errors ?? (existingBody as any).Errors
      };

      const sanitizedError = new HttpErrorResponse({
        error: normalizedBody,
        headers: err.headers,
        status: err.status,
        statusText: err.statusText,
        url: err.url ?? undefined
      });

      try {
        Object.defineProperty(sanitizedError, 'message', {
          value: parsed.summary,
          configurable: true
        });
      } catch {
        // Ignore if message property is non-configurable in a test mock
      }

      return throwError(() => sanitizedError);
    })
  );
};
