import { DOCUMENT } from '@angular/common';
import { TestBed } from '@angular/core/testing';
import { LOCALE_STORAGE_KEY, LocaleService } from './locale.service';

describe('LocaleService', () => {
  function setup(languages: string[], saved: string | null = null, storageThrows = false) {
    const values = new Map<string, string>();
    if (saved !== null) {
      values.set(LOCALE_STORAGE_KEY, saved);
    }

    const storage = {
      getItem: jasmine.createSpy('getItem').and.callFake((key: string) => {
        if (storageThrows) throw new Error('Storage unavailable');
        return values.get(key) ?? null;
      }),
      setItem: jasmine.createSpy('setItem').and.callFake((key: string, value: string) => {
        if (storageThrows) throw new Error('Storage unavailable');
        values.set(key, value);
      }),
    };
    const browserDocument = {
      defaultView: { localStorage: storage, navigator: { languages, language: languages[0] } },
    } as unknown as Document;
    TestBed.configureTestingModule({ providers: [{ provide: DOCUMENT, useValue: browserDocument }] });

    return { service: TestBed.inject(LocaleService), storage, values };
  }

  it('defaults to English without a supported saved or browser language', () => {
    const { service } = setup(['fr-FR']);
    expect(service.locale()).toBe('en');
    expect(service.copy().language).toBe('Language');
  });

  it('uses a supported Polish browser language variant', () => {
    const { service } = setup(['de-DE', 'pl-PL']);
    expect(service.locale()).toBe('pl');
    expect(service.copy().language).toBe('Język');
  });

  it('gives a valid saved preference priority over the browser language', () => {
    const { service } = setup(['pl-PL'], 'en');
    expect(service.locale()).toBe('en');
  });

  it('ignores an invalid saved preference and falls back to the browser language', () => {
    const { service } = setup(['pl-PL'], 'invalid');
    expect(service.locale()).toBe('pl');
  });

  it('keeps switching when storage reads and writes throw', () => {
    const { service } = setup(['pl-PL'], null, true);
    expect(service.locale()).toBe('pl');
    expect(() => service.selectLocale('en')).not.toThrow();
    expect(service.locale()).toBe('en');
    expect(service.copy().language).toBe('Language');
  });

  it('uses the browser language when storage is unavailable', () => {
    TestBed.configureTestingModule({
      providers: [{
        provide: DOCUMENT,
        useValue: { defaultView: { navigator: { languages: ['pl-PL'], language: 'pl-PL' } } },
      }],
    });
    const service = TestBed.inject(LocaleService);
    expect(service.locale()).toBe('pl');
    expect(() => service.selectLocale('en')).not.toThrow();
  });

  it('persists explicit selections for a new service instance', () => {
    const { service, storage, values } = setup(['en-US']);
    service.selectLocale('pl');
    expect(service.locale()).toBe('pl');
    expect(storage.setItem).toHaveBeenCalledWith(LOCALE_STORAGE_KEY, 'pl');
    expect(values.get(LOCALE_STORAGE_KEY)).toBe('pl');

    TestBed.resetTestingModule();
    const { service: restored } = setup(['en-US'], values.get(LOCALE_STORAGE_KEY));
    expect(restored.locale()).toBe('pl');
  });
});
