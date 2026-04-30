import { EnvironmentProviders, makeEnvironmentProviders } from '@angular/core';
import { provideAuth, withAppInitializerAuthCheck } from 'angular-auth-oidc-client';

export type SpaAuthConfig = {
  authority: string;
  clientId: string;
  redirectUrl: string;
  postLogoutRedirectUri: string;
  scope: string;
};

export function provideSpaOidcAuth(config: SpaAuthConfig): EnvironmentProviders {
  return makeEnvironmentProviders([
    provideAuth(
      {
        config: {
          authority: config.authority,
          clientId: config.clientId,
          redirectUrl: config.redirectUrl,
          postLogoutRedirectUri: config.postLogoutRedirectUri,
          responseType: 'code',
          scope: config.scope,
          autoUserInfo: true,
        },
      },
      withAppInitializerAuthCheck(),
    ),
  ]);
}
