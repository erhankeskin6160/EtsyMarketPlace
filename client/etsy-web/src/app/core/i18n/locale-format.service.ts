import { Injectable, inject } from '@angular/core';
import { LanguageService } from './language.service';

@Injectable({ providedIn: 'root' })
export class LocaleFormatService {
  private readonly languageService = inject(LanguageService);

  get currentLocale(): string {
    return this.languageService.locale();
  }

  formatNumber(value: number | null | undefined, options?: Intl.NumberFormatOptions): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageService.locale(), options).format(value);
  }

  formatInteger(value: number | null | undefined): string {
    if (value === null || value === undefined || Number.isNaN(value)) {
      return '—';
    }
    return new Intl.NumberFormat(this.languageService.locale(), { maximumFractionDigits: 0 }).format(Math.round(value));
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
    const date = this.toValidDate(value);
    if (!date) return '—';
    return new Intl.DateTimeFormat(this.languageService.locale(), options).format(date);
  }

  formatTime(
    value: Date | string | number | null | undefined = new Date(),
    options: Intl.DateTimeFormatOptions = { hour: '2-digit', minute: '2-digit' }
  ): string {
    const date = this.toValidDate(value);
    if (!date) return '—';
    return new Intl.DateTimeFormat(this.languageService.locale(), options).format(date);
  }

  formatDateTime(
    value: Date | string | number | null | undefined = new Date(),
    options: Intl.DateTimeFormatOptions = {
      day: '2-digit',
      month: '2-digit',
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }
  ): string {
    const date = this.toValidDate(value);
    if (!date) return '—';
    return new Intl.DateTimeFormat(this.languageService.locale(), options).format(date);
  }

  private toValidDate(value: Date | string | number | null | undefined): Date | null {
    if (value === null || value === undefined || value === '') {
      return null;
    }
    const date = value instanceof Date ? value : new Date(value);
    return Number.isNaN(date.getTime()) ? null : date;
  }
}
