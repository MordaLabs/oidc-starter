import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideBffAuth, provideSpaOidcAuth } from 'oidc-starter-auth';
import { routes } from './app.routes';
import { environment } from '../environments/environment';

const authProviders =
  environment.authMode === 'spa'
    ? [provideSpaOidcAuth(environment.oidc)]
    : [
        provideBffAuth({
          apiOrigin: environment.apiOrigin,
        }),
      ];

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(),
    provideRouter(routes),
    ...authProviders,
  ],
};
