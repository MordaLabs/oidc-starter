# Architecture Note

This project intentionally keeps two authentication modes side by side. The BFF is the primary production-oriented path; SPA remains a supported learning and reference path.

## SPA Mode

In `spa` mode, the Angular application talks directly to the configured OpenID Connect provider with Authorization Code Flow and PKCE. The browser receives the OIDC result and the frontend displays user claims.

## BFF Mode

In `bff` mode, the Angular application uses the ASP.NET Core BFF as its authentication boundary. The BFF starts provider redirects, owns the authenticated HTTP-only session cookie, and returns normalized session information to the browser.

### BFF endpoint inventory

| Endpoint | Purpose |
| --- | --- |
| `GET /api/auth/providers` | Returns the providers registered by the running backend, including the default provider marker and provider-specific login URL. |
| `GET /api/auth/login` | Challenges `Starter:DefaultLoginProvider`, which defaults to `oidc`. |
| `GET /api/auth/login/{provider}` | Challenges a registered provider by id. |
| `GET /api/auth/me` | Returns the normalized current user for the BFF session. |
| `GET /api/auth/csrf` | Issues the request token used before state-changing BFF requests. |
| `POST /api/auth/logout` | Clears the local BFF session; remote sign-out is used for the built-in OIDC provider where configured. |

`AddOidcStarterBff(...)` always registers the built-in `oidc` provider. Google, GitHub, Facebook, and generic handlers are opt-in package capabilities registered by the consuming backend. The running backend's configuration therefore determines what discovery returns; package support is not the same as a provider being enabled in a particular sample or deployment.

The frontend loads `/api/auth/me` to establish session state. When unauthenticated, it can load `/api/auth/providers` and render its own chooser. `@mordalabs/oidc-starter-auth` provides the discovery and navigation APIs, while the consuming application owns the provider-picker UI. Existing consumers can continue to call the default login endpoint without implementing a chooser.

### Provider-aware session behavior

All registered providers create the same BFF application session. `/api/auth/me` retains the existing normalized user contract and can add optional `externalIdentity` metadata: the provider id and available `emailVerified` and `pictureUrl` fields.

Logout always removes the local session. When the session was created by the built-in OpenID Connect provider, the BFF also uses the configured remote sign-out handler. Social and generic external providers use local-session-only logout; the BFF does not request their remote logout.

## Configuration Shape

- Frontend mode and OIDC client settings live in `src/frontend/src/environments`.
- Backend OIDC client settings live in `src/backend/appsettings*.json`.
- `Starter:DefaultLoginProvider` selects the registered provider used by `GET /api/auth/login`; it defaults to `oidc`.
- `Starter:FrontendOrigin` is the backend's trusted frontend origin for CORS and post-login/logout redirects.
- `Starter:AllowedForwardedHosts` lists host names the backend will accept from `X-Forwarded-Host` when it is behind a trusted reverse proxy.
- `apiOrigin` is the frontend API origin. Leave it empty when using the Angular proxy in local development.

## BFF Security and Deployment Assumptions

The BFF session is represented by an HTTP-only, secure `__Host-` cookie. The local setup keeps `SameSite=None` so the current development proxy and optional direct backend calls continue to work; production deployments should prefer serving the frontend and backend through one public site so the cookie does not need to support broad cross-site use.

The backend reads `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` before HTTPS redirection. ASP.NET Core still only trusts known proxy networks by default; production hosting should configure the actual trusted proxy addresses and replace `Starter:AllowedForwardedHosts` with the public backend host names.

`POST /api/auth/logout` rejects requests whose `Origin` or `Referer` is not the configured frontend origin or the current backend origin. The BFF package also exposes `GET /api/auth/csrf`, which issues an antiforgery request token through the `XSRF-TOKEN` cookie. Frontends must send that token on cookie-authenticated state-changing BFF requests: use the `X-XSRF-TOKEN` header for fetch/XHR calls, or the `__RequestVerificationToken` form field for top-level form posts such as logout.

The reusable BFF package reads flat role claims by default and exposes `IOidcStarterRoleMapper` for provider-specific role extraction. The sample backend registers a Keycloak mapper that reads the backend `access_token` and surfaces roles from `realm_access.roles` and `resource_access.{client}.roles` in `/api/auth/me`.