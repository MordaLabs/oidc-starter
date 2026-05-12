# OidcStarter.AspNetCore.Bff Release Notes

## Toward 1.0.0

The package is stabilizing around a small public API for cookie-backed OIDC BFF authentication in
ASP.NET Core.

Current package capabilities:

- BFF login, current-user, CSRF-token, and logout endpoints.
- HTTP-only secure cookie session defaults with configurable lifetime, SameSite mode, and sliding
  expiration.
- Antiforgery groundwork for cookie-authenticated state-changing BFF requests.
- Reverse-proxy forwarded-header configuration hooks.
- Authorization policy constants and scope/claim policy helpers.
- Configurable name and role claim types.
- Provider-agnostic role-mapping extension point via `IOidcStarterRoleMapper`.
- Focused automated tests for role mapping, current-user projection, CSRF/origin validation,
  logout behavior, and service/policy registration.

### Breaking Changes

No package default behavior changes are currently documented as breaking for the `1.0.0` hardening
path. Antiforgery validation remains opt-in through `Starter:RequireAntiforgeryToken`.

### Consumer Integration Notes

- Custom frontends must call `GET /api/auth/csrf` and send the returned token on state-changing BFF
  requests before enabling antiforgery validation.
- Applications using providers with nested or provider-specific role structures should register an
  `IOidcStarterRoleMapper`; the package default only assumes flat role claims.
- Production deployments should configure trusted forwarded hosts/proxies and move secrets to a
  proper secret store.
