import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { BFF_AUTH_CONFIG } from './internal/bff-auth-token';

export type BffAuthConfig = {
  apiOrigin?: string;
  authPath?: string;
};

export function provideBffAuth(config: BffAuthConfig = {}): EnvironmentProviders {
  return makeEnvironmentProviders([
    {
      provide: BFF_AUTH_CONFIG,
      useValue: config,
    },
  ]);
}
