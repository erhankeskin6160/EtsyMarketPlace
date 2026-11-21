import { Component, inject } from '@angular/core';
import { CommonModule } from '@angular/common';
import { TranslocoDirective } from '@jsverse/transloco';
import { LanguageService } from '../i18n/language.service';
import { AppLanguage } from '../i18n/i18n.config';

@Component({
  selector: 'app-language-switcher',
  standalone: true,
  imports: [CommonModule, TranslocoDirective],
  template: `
    <div class="lang-switcher" role="group" *transloco="let t" [attr.aria-label]="t('common.languageSwitcherAria')">
      <i class="fa-solid fa-globe lang-icon" aria-hidden="true"></i>
      <button
        *ngFor="let lang of languageService.supportedLanguages"
        type="button"
        class="lang-btn"
        [class.active]="languageService.activeLang() === lang.code"
        [attr.aria-pressed]="languageService.activeLang() === lang.code"
        [title]="lang.nativeName"
        (click)="selectLanguage(lang.code)">
        {{ lang.shortLabel }}
      </button>
    </div>
  `,
  styles: [`
    .lang-switcher {
      display: inline-flex;
      align-items: center;
      gap: 2px;
      padding: 3px 4px 3px 8px;
      border-radius: 8px;
      background: rgba(15, 23, 42, 0.72);
      border: 1px solid rgba(148, 163, 184, 0.22);
      backdrop-filter: blur(8px);
    }
    .lang-icon {
      font-size: 0.72rem;
      color: #94a3b8;
      margin-right: 4px;
    }
    .lang-btn {
      border: none;
      background: transparent;
      color: #94a3b8;
      font-size: 0.72rem;
      font-weight: 700;
      letter-spacing: 0.04em;
      padding: 4px 7px;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.18s ease;
      line-height: 1;
    }
    .lang-btn:hover {
      color: #f8fafc;
      background: rgba(148, 163, 184, 0.14);
    }
    .lang-btn.active {
      color: #fff;
      background: linear-gradient(135deg, #f97316 0%, #ea580c 100%);
      box-shadow: 0 0 10px rgba(249, 115, 22, 0.35);
    }
  `]
})
export class LanguageSwitcherComponent {
  readonly languageService = inject(LanguageService);

  selectLanguage(code: AppLanguage): void {
    this.languageService.setLanguage(code);
  }
}
