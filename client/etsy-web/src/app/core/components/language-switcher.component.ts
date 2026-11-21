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
      <button
        *ngFor="let lang of languageService.supportedLanguages"
        type="button"
        class="lang-btn"
        [class.active]="languageService.activeLang() === lang.code"
        [attr.aria-pressed]="languageService.activeLang() === lang.code"
        [attr.aria-label]="lang.nativeName"
        [title]="lang.nativeName"
        (click)="selectLanguage(lang.code)">
        <!-- Turkish Flag (TR) -->
        <svg *ngIf="lang.code === 'tr'" class="flag-svg" viewBox="0 0 30 20" aria-hidden="true">
          <rect width="30" height="20" rx="2" fill="#E30A17" />
          <circle cx="11.5" cy="10" r="5" fill="#FFFFFF" />
          <circle cx="12.8" cy="10" r="4" fill="#E30A17" />
          <polygon
            fill="#FFFFFF"
            points="17.8,10 20.6,10.9 18.9,8.5 18.9,11.5 20.6,9.1"
            transform="rotate(-18 19.2 10)" />
        </svg>

        <!-- United Kingdom Flag (EN) -->
        <svg *ngIf="lang.code === 'en'" class="flag-svg" viewBox="0 0 30 20" aria-hidden="true">
          <clipPath id="en-flag-clip">
            <rect width="30" height="20" rx="2" />
          </clipPath>
          <g clip-path="url(#en-flag-clip)">
            <rect width="30" height="20" fill="#012169" />
            <path d="M0,0 L30,20 M30,0 L0,20" stroke="#FFFFFF" stroke-width="4" />
            <path d="M0,0 L30,20 M30,0 L0,20" stroke="#C8102E" stroke-width="2" />
            <path d="M15,0 V20 M0,10 H30" stroke="#FFFFFF" stroke-width="6.5" />
            <path d="M15,0 V20 M0,10 H30" stroke="#C8102E" stroke-width="3.8" />
          </g>
        </svg>
      </button>
    </div>
  `,
  styles: [`
    .lang-switcher {
      display: inline-flex;
      align-items: center;
      gap: 4px;
      padding: 3px 4px;
      border-radius: 8px;
      background: rgba(15, 23, 42, 0.72);
      border: 1px solid rgba(148, 163, 184, 0.22);
      backdrop-filter: blur(8px);
    }
    .lang-btn {
      display: inline-flex;
      align-items: center;
      justify-content: center;
      border: 1px solid transparent;
      background: transparent;
      padding: 3px 5px;
      border-radius: 6px;
      cursor: pointer;
      transition: all 0.18s ease;
      opacity: 0.55;
      line-height: 0;
    }
    .lang-btn:hover {
      opacity: 0.92;
      background: rgba(148, 163, 184, 0.14);
    }
    .lang-btn.active {
      opacity: 1;
      background: rgba(249, 115, 22, 0.22);
      border-color: rgba(249, 115, 22, 0.8);
      box-shadow: 0 0 10px rgba(249, 115, 22, 0.35);
    }
    .flag-svg {
      width: 22px;
      height: 15px;
      border-radius: 2px;
      display: block;
      box-shadow: 0 1px 2px rgba(0, 0, 0, 0.35);
    }
  `]
})
export class LanguageSwitcherComponent {
  readonly languageService = inject(LanguageService);

  selectLanguage(code: AppLanguage): void {
    this.languageService.setLanguage(code);
  }
}
