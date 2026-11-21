import { signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { LocaleFormatService } from './locale-format.service';
import { LanguageService } from './language.service';

describe('LocaleFormatService', () => {
  let service: LocaleFormatService;
  let localeSignal = signal('en-US');

  beforeEach(() => {
    localeSignal = signal('en-US');
    TestBed.configureTestingModule({
      providers: [
        { provide: LanguageService, useValue: { locale: localeSignal } }
      ]
    });
    service = TestBed.inject(LocaleFormatService);
  });

  it('formats numbers according to active locale (en-US vs tr-TR)', () => {
    expect(service.formatNumber(1234.56, { minimumFractionDigits: 2, maximumFractionDigits: 2 })).toBe('1,234.56');
    expect(service.formatInteger(1234.6)).toBe('1,235');

    localeSignal.set('tr-TR');
    expect(service.formatNumber(1234.56, { minimumFractionDigits: 2, maximumFractionDigits: 2 })).toBe('1.234,56');
    expect(service.formatInteger(1234.6)).toBe('1.235');
  });

  it('returns em-dash for null, undefined, or invalid values', () => {
    expect(service.formatNumber(null)).toBe('—');
    expect(service.formatInteger(undefined)).toBe('—');
    expect(service.formatDate('invalid-date')).toBe('—');
    expect(service.formatTime(null)).toBe('—');
  });

  it('formats dates and times cleanly for valid Date instances', () => {
    const sample = new Date(2026, 10, 21, 14, 35);
    expect(service.formatTime(sample)).toMatch(/35/);
    expect(service.formatDateTime(sample)).toContain('2026');
  });
});
