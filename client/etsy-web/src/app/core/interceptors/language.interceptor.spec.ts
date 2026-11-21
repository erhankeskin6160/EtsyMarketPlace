import { HttpClient, provideHttpClient, withInterceptors } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { signal } from '@angular/core';
import { languageInterceptor } from './language.interceptor';
import { LanguageService } from '../i18n/language.service';
import { AppLanguage } from '../i18n/i18n.config';

describe('languageInterceptor', () => {
  let http: HttpClient;
  let httpMock: HttpTestingController;
  let activeLangSignal = signal<AppLanguage>('en');

  beforeEach(() => {
    activeLangSignal = signal<AppLanguage>('en');
    TestBed.configureTestingModule({
      providers: [
        { provide: LanguageService, useValue: { activeLang: activeLangSignal } },
        provideHttpClient(withInterceptors([languageInterceptor])),
        provideHttpClientTesting()
      ]
    });

    http = TestBed.inject(HttpClient);
    httpMock = TestBed.inject(HttpTestingController);
  });

  afterEach(() => {
    httpMock.verify();
  });

  it('attaches Accept-Language and X-App-Language headers to VDS API requests', () => {
    activeLangSignal.set('tr');
    http.get('/api/etsy/financial/performance').subscribe();

    const req = httpMock.expectOne('/api/etsy/financial/performance');
    expect(req.request.headers.get('Accept-Language')).toBe('tr');
    expect(req.request.headers.get('X-App-Language')).toBe('tr');
    req.flush({});
  });

  it('skips language headers for static /i18n/ assets and external LLM APIs', () => {
    http.get('/i18n/en.json').subscribe();
    const i18nReq = httpMock.expectOne('/i18n/en.json');
    expect(i18nReq.request.headers.has('Accept-Language')).toBeFalse();
    expect(i18nReq.request.headers.has('X-App-Language')).toBeFalse();
    i18nReq.flush({});

    http.post('https://api.openai.com/v1/chat/completions', {}).subscribe();
    const llmReq = httpMock.expectOne('https://api.openai.com/v1/chat/completions');
    expect(llmReq.request.headers.has('Accept-Language')).toBeFalse();
    expect(llmReq.request.headers.has('X-App-Language')).toBeFalse();
    llmReq.flush({});
  });
});
