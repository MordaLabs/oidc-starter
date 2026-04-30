import { InjectionToken, makeEnvironmentProviders } from '@angular/core';

export type BffAuthConfig = {
  apiOrigin?: string;
  authPath?: string;
};

export const BFF_AUTH_CONFIG = new InjectionToken<BffAuthConfig>('BFF_AUTH_CONFIG', {
  providedIn: 'root',
  factory: () => ({}),
});

export function provideBffAuth(config: BffAuthConfig = {}) {
  return makeEnvironmentProviders([
    {
      provide: BFF_AUTH_CONFIG,
      useValue: config,
    },
  ]);
}
