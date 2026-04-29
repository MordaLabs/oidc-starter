import {
  ApplicationConfig,
  provideBrowserGlobalErrorListeners,
  provideZoneChangeDetection,
} from '@angular/core';
import { provideHttpClient } from '@angular/common/http';
import { provideRouter } from '@angular/router';
import { provideAuth, withAppInitializerAuthCheck } from 'angular-auth-oidc-client';
import { routes } from './app.routes';
import { environment } from '../environments/environment';

const authProviders =
  environment.authMode === 'spa'
    ? [
        provideAuth(
          {
            config: {
              authority: environment.oidc.authority,
              clientId: environment.oidc.clientId,
              redirectUrl: environment.oidc.redirectUrl,
              postLogoutRedirectUri: environment.oidc.postLogoutRedirectUri,
              responseType: 'code',
              scope: environment.oidc.scope,
              autoUserInfo: true,
            },
          },
          withAppInitializerAuthCheck(),
        ),
      ]
    : [];

export const appConfig: ApplicationConfig = {
  providers: [
    provideBrowserGlobalErrorListeners(),
    provideZoneChangeDetection({ eventCoalescing: true }),
    provideHttpClient(),
    provideRouter(routes),
    ...authProviders,
  ],
};
