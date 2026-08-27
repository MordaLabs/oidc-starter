# @flying-bee/oidc-starter-auth

Reusable Angular auth building blocks for OIDC Starter.

This package is published to npm as `@flying-bee/oidc-starter-auth`. The sample frontend in this repository consumes the in-repository Angular library through the workspace path alias `@flying-bee/oidc-starter-auth` so changes can be developed and verified locally.

## Angular Compatibility

Version `0.1.1` is human-verified for Angular 20.3+, Angular 21, and Angular 22.

## Install

```powershell
npm install @flying-bee/oidc-starter-auth
```

This README describes the package source on the current `master` branch. The published npm `0.1.1` package should be evaluated by its published artifact and release information; do not assume it contains every current-source provider capability described here.

## What It Provides

- `provideBffAuth(config)` to configure backend-for-frontend auth endpoints.
- `BffAuthService` for current-user loading, default or provider-targeted login redirects, provider discovery, antiforgery initialization, and logout form posts.
- `BffLoginProvider` for runtime provider-picker data from `GET /api/auth/providers`.
- `BffCurrentUser` and optional `BffExternalIdentity` for the normalized `/api/auth/me` response.
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
      antiforgeryCookieName: 'XSRF-TOKEN',
      antiforgeryFormFieldName: '__RequestVerificationToken',
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
    this.auth.login(); // GET /api/auth/login: the BFF default provider
  }

  loginWith(providerId: string): void {
    this.auth.login(providerId); // GET /api/auth/login/{providerId}
  }

  logout(): void {
    this.auth.logout();
  }
}
```

## BFF provider discovery

`BffAuthService.login()` preserves the default single-provider flow by navigating to `GET /api/auth/login`. `BffAuthService.login(providerId)` navigates to the provider-targeted login endpoint and rejects an empty provider id.

Call `getLoginProviders()` to retrieve `readonly BffLoginProvider[]` from `GET /api/auth/providers`. Each entry contains `id`, `displayName`, `isDefault`, and `loginUrl`. The BFF is the runtime source of truth for which providers are enabled; consuming applications own the actual provider-picker UI and may continue to render a single default login button.

`BffCurrentUser.externalIdentity` is optional (or `null`) and has the `BffExternalIdentity` shape:

```ts
type BffExternalIdentity = {
  readonly providerId: string;
  readonly emailVerified?: boolean | null;
  readonly pictureUrl?: string | null;
};
```

Treat external identity fields as display metadata supplied by the BFF, not as a replacement for application authorization decisions.

## BFF Antiforgery

`BffAuthService.logout()` calls `GET /api/auth/csrf`, reads the `XSRF-TOKEN` cookie, and includes the
token in the logout form post as `__RequestVerificationToken`. Custom frontend code that calls
state-changing BFF endpoints with `fetch`, XHR, or Angular `HttpClient` should send the same token in
the `X-XSRF-TOKEN` header.

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
