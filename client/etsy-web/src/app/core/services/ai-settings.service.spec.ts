import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { AiSettingsService } from './ai-settings.service';

describe('AiSettingsService', () => {
  let service: AiSettingsService;
  let httpTesting: HttpTestingController;
  const STORAGE_KEY = 'etsy_ai_optimization_settings';

  beforeEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem('etsy_gemini_config');
    TestBed.configureTestingModule({
      providers: [
        AiSettingsService,
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    service = TestBed.inject(AiSettingsService);
    httpTesting = TestBed.inject(HttpTestingController);

    // Constructor triggers background hydrateFromBackend GET
    const reqs = httpTesting.match(r => r.url.includes('/api/settings/ai'));
    reqs.forEach(req => req.flush({ exists: false, settingsJson: '' }));
  });

  afterEach(() => {
    localStorage.removeItem(STORAGE_KEY);
    localStorage.removeItem('etsy_gemini_config');
    httpTesting.verify();
  });

  it('should map active API key cleanly per provider without cross-provider leakage', () => {
    service.settings.set({
      ...service.settings(),
      provider: 'DeepSeek',
      geminiApiKey: 'gm-secret-should-not-leak',
      deepSeekApiKey: '',
      openAiApiKey: 'sk-openai-123',
      claudeApiKey: 'sk-ant-123',
      grokApiKey: 'xai-grok-123'
    });

    // Empty DeepSeek key must NOT fall back to geminiApiKey
    expect(service.getActiveApiKey()).toBe('');

    service.settings.update(s => ({ ...s, deepSeekApiKey: 'sk-deepseek-789' }));
    expect(service.getActiveApiKey()).toBe('sk-deepseek-789');

    service.settings.update(s => ({ ...s, provider: 'OpenAI' }));
    expect(service.getActiveApiKey()).toBe('sk-openai-123');

    service.settings.update(s => ({ ...s, provider: 'Gemini' }));
    expect(service.getActiveApiKey()).toBe('gm-secret-should-not-leak');

    service.settings.update(s => ({ ...s, provider: 'Claude' }));
    expect(service.getActiveApiKey()).toBe('sk-ant-123');

    service.settings.update(s => ({ ...s, provider: 'Grok' }));
    expect(service.getActiveApiKey()).toBe('xai-grok-123');

    service.settings.update(s => ({ ...s, provider: 'Offline' }));
    expect(service.getActiveApiKey()).toBe('');
  });

  it('should respect providerOverride argument when supplied', () => {
    service.settings.set({
      ...service.settings(),
      provider: 'Gemini',
      geminiApiKey: 'gm-primary',
      deepSeekApiKey: '  sk-ds-override  ',
      openAiApiKey: 'sk-oa-override'
    });

    expect(service.getActiveApiKey('DeepSeek')).toBe('sk-ds-override');
    expect(service.getActiveApiKey('OpenAI')).toBe('sk-oa-override');
    expect(service.getActiveApiKey('Offline')).toBe('');
    expect(service.getActiveApiKey()).toBe('gm-primary');
  });
});
