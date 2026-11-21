import { Injectable, inject } from '@angular/core';
import { LanguageService } from './language.service';

@Injectable({ providedIn: 'root' })
export class LocaleFormatService {
  private readonly languageService = inject(LanguageService);

  formatNumber(value: number | null | undefined, options?: Intl.NumberFormatOptions): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageService.locale(), options).format(value);
  }

  formatCurrency(
    value: number | null | undefined,
    currency: 'USD' | 'TRY' | 'EUR' = 'USD',
    options?: Intl.NumberFormatOptions
  ): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageService.locale(), {
      style: 'currency',
      currency,
      ...options
    }).format(value);
  }

  formatDate(
    value: Date | string | number | null | undefined,
    options: Intl.DateTimeFormatOptions = { dateStyle: 'medium' }
  ): string {
    if (value === null || value === undefined || value === '') {
      return '—';
    }
    const date = value instanceof Date ? value : new Date(value);
    if (Number.isNaN(date.getTime())) {
      return '—';
    }
    return new Intl.DateTimeFormat(this.languageService.locale(), options).format(date);
  }
}
