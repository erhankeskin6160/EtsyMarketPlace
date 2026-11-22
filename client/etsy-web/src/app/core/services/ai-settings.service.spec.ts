import { TestBed } from '@angular/core/testing';
import { provideHttpClient } from '@angular/common/http';
import { provideHttpClientTesting, HttpTestingController } from '@angular/common/http/testing';
import { provideRouter } from '@angular/router';
import { AiSettingsService } from './ai-settings.service';
import { AuthService } from './auth.service';
import { User } from '../models/auth.models';

describe('AiSettingsService', () => {
  let service: AiSettingsService;
  let authService: AuthService;
  let httpTesting: HttpTestingController;
  const STORAGE_KEY = 'etsy_ai_optimization_settings';

  const userA: User = {
    id: 'user-a-id',
    username: 'alice',
    email: 'alice@example.com',
    role: 'StoreOwner',
    assignedShopIds: ['53236321'],
    monthlyAiTokenQuota: 500000,
    usedAiTokens: 0,
    isActive: true,
    createdAt: new Date().toISOString()
  };

  const userB: User = {
    id: 'user-b-id',
    username: 'bob',
    email: 'bob@example.com',
    role: 'StoreOwner',
    assignedShopIds: ['53236321'],
    monthlyAiTokenQuota: 500000,
    usedAiTokens: 0,
    isActive: true,
    createdAt: new Date().toISOString()
  };

  beforeEach(() => {
    localStorage.clear();
    TestBed.configureTestingModule({
      providers: [
        AiSettingsService,
        AuthService,
        provideRouter([]),
        provideHttpClient(),
        provideHttpClientTesting()
      ]
    });
    authService = TestBed.inject(AuthService);
    service = TestBed.inject(AiSettingsService);
    httpTesting = TestBed.inject(HttpTestingController);
    TestBed.flushEffects();
  });

  afterEach(() => {
    localStorage.clear();
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

  it('should isolate AI API keys per authenticated user and clear state on logout/switch', () => {
    // 1. User A logs in -> backend returns User A's Gemini + DeepSeek keys
    authService.setSession('jwt-token-a', userA);
    TestBed.flushEffects();

    const reqA = httpTesting.expectOne(r => r.url.includes('/api/settings/ai'));
    reqA.flush({
      exists: true,
      settingsJson: JSON.stringify({
        provider: 'DeepSeek',
        geminiApiKey: 'AIza-alice-key',
        deepSeekApiKey: 'sk-alice-deepseek'
      })
    });

    expect(service.getActiveApiKey('DeepSeek')).toBe('sk-alice-deepseek');
    expect(service.getActiveApiKey('Gemini')).toBe('AIza-alice-key');
    expect(localStorage.getItem(`${STORAGE_KEY}:${userA.id}`)).toContain('sk-alice-deepseek');

    // 2. User A logs out -> settings immediately reset to empty defaults
    authService.logout(false);
    TestBed.flushEffects();

    expect(service.getActiveApiKey('DeepSeek')).toBe('');
    expect(service.getActiveApiKey('Gemini')).toBe('');

    // 3. User B logs in -> backend returns exists: false -> MUST NOT inherit User A's keys
    authService.setSession('jwt-token-b', userB);
    TestBed.flushEffects();

    const reqB = httpTesting.expectOne(r => r.url.includes('/api/settings/ai'));
    reqB.flush({ exists: false, settingsJson: '{}' });

    expect(service.getActiveApiKey('DeepSeek')).toBe('');
    expect(service.getActiveApiKey('Gemini')).toBe('');
    expect(localStorage.getItem(`${STORAGE_KEY}:${userB.id}`)).toBeNull();
  });
});
