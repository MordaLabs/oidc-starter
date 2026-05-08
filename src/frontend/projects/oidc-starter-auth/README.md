# @flying-bee/oidc-starter-auth

Reusable Angular auth building blocks for OIDC Starter.

This package is published to npm as `@flying-bee/oidc-starter-auth`. The sample frontend in this repository consumes the in-repository Angular library through the workspace path alias `@flying-bee/oidc-starter-auth` so changes can be developed and verified locally.

## Install

```powershell
npm install @flying-bee/oidc-starter-auth
```

## What It Provides

- `provideBffAuth(config)` to configure backend-for-frontend auth endpoints.
- `BffAuthService` for current-user loading, login redirect, and logout form post.
- `BffCurrentUser` for the `/api/auth/me` response contract.
- `provideSpaOidcAuth(config)` for the sample SPA/reference mode wrapper around `angular-auth-oidc-client`.
- `SpaAuthConfig` and `BffAuthConfig` configuration contracts.

## Sample Frontend Usage

```ts
import { provideBffAuth, provideSpaOidcAuth } from '@flying-bee/oidc-starter-auth';

const authProviders =
  environment.authMode === 'spa'
    ? [provideSpaOidcAuth(environment.oidc)]
    : [provideBffAuth({ apiOrigin: environment.apiOrigin })];
```

The sample app keeps demo layout, copy, mode switching, and backend ping presentation in `src/app`.

## Future BFF Consumer Setup

```ts
import { provideHttpClient } from '@angular/common/http';
import { ApplicationConfig } from '@angular/core';
import { provideBffAuth } from '@flying-bee/oidc-starter-auth';

export const appConfig: ApplicationConfig = {
  providers: [
    provideHttpClient(),
    provideBffAuth({
      apiOrigin: 'https://api.example.com',
      authPath: '/api/auth',
    }),
  ],
};
```

```ts
import { Component, inject } from '@angular/core';
import { BffAuthService } from '@flying-bee/oidc-starter-auth';

@Component({ selector: 'app-auth-button', template: '' })
export class AuthButtonComponent {
  readonly auth = inject(BffAuthService);

  login(): void {
    this.auth.login();
  }

  logout(): void {
    this.auth.logout();
  }
}
```

## Local Packaging

From `src/frontend`:

```powershell
Remove-Item .\dist\oidc-starter-auth -Recurse -Force -ErrorAction SilentlyContinue
.\node_modules\.bin\ng.cmd build oidc-starter-auth --configuration production
Set-Location .\dist\oidc-starter-auth
npm pack
```

The package tarball is written under `src/frontend/dist/oidc-starter-auth`.

Publication command for maintainers from `src/frontend/dist/oidc-starter-auth`:

```powershell
npm publish --access public
```

Only publish after confirming the target version, release notes, peer dependency ranges, and registry credentials.
