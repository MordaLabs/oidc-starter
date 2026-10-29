import { DOCUMENT } from '@angular/common';
import { computed, inject, Injectable, signal } from '@angular/core';
import { localeCatalog, type Locale } from './locale.catalog';

export const LOCALE_STORAGE_KEY = 'oidc-starter.public-demo.locale';

@Injectable({ providedIn: 'root' })
export class LocaleService {
  private readonly document = inject(DOCUMENT);
  private readonly currentLocale = signal<Locale>(this.resolveInitialLocale());

  readonly locale = this.currentLocale.asReadonly();
  readonly copy = computed(() => localeCatalog[this.currentLocale()]);

  constructor() {
    this.syncDocumentMetadata(this.currentLocale());
  }

  selectLocale(locale: Locale): void {
    this.currentLocale.set(locale);
    this.syncDocumentMetadata(locale);

    try {
      this.document.defaultView?.localStorage.setItem(LOCALE_STORAGE_KEY, locale);
    } catch {
      // Keep the in-memory selection when browser storage is unavailable.
    }
  }

  private syncDocumentMetadata(locale: Locale): void {
    this.document.documentElement?.setAttribute('lang', locale);
    this.document.title = localeCatalog[locale].documentTitle;
    this.document.querySelector?.('meta[name="description"]')
      ?.setAttribute('content', localeCatalog[locale].metaDescription);
  }

  private resolveInitialLocale(): Locale {
    try {
      const saved = this.document.defaultView?.localStorage.getItem(LOCALE_STORAGE_KEY);
      if (saved === 'en' || saved === 'pl') {
        return saved;
      }
    } catch {
      // Continue with browser language when browser storage is unavailable.
    }

    try {
      const navigator = this.document.defaultView?.navigator;
      const languages = [...(navigator?.languages ?? []), navigator?.language];
      for (const language of languages) {
        const primaryLanguage = language?.toLowerCase().split('-')[0];
        if (primaryLanguage === 'en' || primaryLanguage === 'pl') {
          return primaryLanguage;
        }
      }
    } catch {
      // Browser language may be unavailable outside a browser.
    }

    return 'en';
  }
}
