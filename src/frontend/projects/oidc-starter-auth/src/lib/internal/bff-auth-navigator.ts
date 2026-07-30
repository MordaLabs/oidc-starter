import { InjectionToken } from '@angular/core';

export type BffAuthNavigator = {
  navigate(url: string): void;
};

export const BFF_AUTH_NAVIGATOR = new InjectionToken<BffAuthNavigator>('BFF_AUTH_NAVIGATOR', {
  providedIn: 'root',
  factory: () => ({
    navigate: (url) => {
      window.location.href = url;
    },
  }),
});
