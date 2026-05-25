# OidcStarter.AspNetCore.Bff Release Notes

## v1.0.1

Hardening validation patch release.

- Added focused validation coverage for login/logout/current-user/session-state behavior.
- Added focused antiforgery behavior coverage for token issuing, request validation, and unsafe
  endpoint protection.
- Added unauthorized and forbidden API behavior coverage for package-configured cookie auth events.
- Added package/sample build compatibility evidence by building the sample backend from the package
  test project.
- Audit smoke confirmed 0 findings after the hardening validation work.

Breaking changes: none.

Public API changes: none.

Runtime behavior changes: none.

## v1.0.0

Version 1.0.0 established a small public API for cookie-backed OIDC BFF authentication in
ASP.NET Core.

Current package capabilities:

- BFF login, current-user, CSRF-token, and logout endpoints.
- HTTP-only secure cookie session defaults with configurable lifetime, SameSite mode, and sliding
  expiration.
- Package-local antiforgery validation for cookie-authenticated package logout requests, implemented
  with ASP.NET Core `IAntiforgery`.
- Reverse-proxy forwarded-header configuration hooks.
- Authorization policy constants and scope/claim policy helpers.
- Configurable name and role claim types.
- Provider-agnostic role-mapping extension point via `IOidcStarterRoleMapper`.
- Focused automated tests for role mapping, current-user projection, CSRF/origin validation,
  logout behavior, and service/policy registration.

### Breaking Changes

`POST /api/auth/logout` now always requires antiforgery validation through a package-local filter
that calls ASP.NET Core `IAntiforgery`. Frontends must call `GET /api/auth/csrf` and submit the
returned token with logout requests. `Starter:RequireAntiforgeryToken` is retained for compatibility
but no longer controls package-provided endpoints.

### Consumer Integration Notes

- Custom frontends must call `GET /api/auth/csrf` and send the returned token on state-changing BFF
  requests such as logout.
- Applications using providers with nested or provider-specific role structures should register an
  `IOidcStarterRoleMapper`; the package default only assumes flat role claims.
- Production deployments should configure trusted forwarded hosts/proxies and move secrets to a
  proper secret store.
