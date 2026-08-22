import { InjectionToken } from '@angular/core';

export type BffClipboardWriter = (text: string) => Promise<void>;

export const BFF_CLIPBOARD_WRITER = new InjectionToken<BffClipboardWriter>('BFF_CLIPBOARD_WRITER', {
  providedIn: 'root',
  factory: () => (text: string) => {
    if (typeof navigator === 'undefined' || !navigator.clipboard?.writeText) {
      return Promise.reject(new Error('Clipboard unavailable'));
    }

    return navigator.clipboard.writeText(text);
  },
});
