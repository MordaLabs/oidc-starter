import { InjectionToken } from '@angular/core';
import type { BffAuthConfig } from '../bff-auth-config';

export const BFF_AUTH_CONFIG = new InjectionToken<BffAuthConfig>('BFF_AUTH_CONFIG', {
  providedIn: 'root',
  factory: () => ({}),
});
