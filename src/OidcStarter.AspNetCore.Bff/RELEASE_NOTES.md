# OidcStarter.AspNetCore.Bff Release Notes

## v1.2.1

Metadata-only patch release.

- Corrected the NuGet Project Website to `https://oidc-starter.mordalabs.com/`.
- The repository URL remains `https://github.com/MordaLabs/oidc-starter`.
- Breaking changes: none.
- Public API changes: none.
- Runtime behavior changes: none.
- Configuration changes: none.

## v1.2.0

Backward-compatible minor release.

- Added the public read-only `AuthenticationProperties.TryGetOidcStarterLoginProviderId(out string? providerId)` accessor.
- Provides a supported way for downstream integrations and extensions to read the OIDC Starter login-provider id persisted in `AuthenticationProperties`.
- The persisted property-key literal remains internal.
- Breaking changes: none.
- Login/logout runtime behavior changes: none.
- Existing consumers do not require configuration migration.
- Targeted validation for the accessor change passed: 58 tests, 0 failures.

## v1.1.0

Backward-compatible feature release for external login providers and provider-aware BFF flows.

### Highlights

- Added opt-in registration for Google (`AddOidcStarterGoogle(...)`), GitHub (`AddOidcStarterGitHub(...)`), and Facebook (`AddOidcStarterFacebook(...)`) authentication.
- Added `AddOidcStarterLoginProvider(...)` for registering a generic/custom authentication scheme as a login provider.
- Added runtime provider discovery through `GET /api/auth/providers` and provider-targeted login through `GET /api/auth/login/{provider}`.
- Added `Starter:DefaultLoginProvider` to select the provider used by the existing `GET /api/auth/login` endpoint; OpenID Connect remains the default when no override is configured.
- Added provider-aware logout behavior. Built-in OpenID Connect continues to use configured remote sign-out; external and custom login providers end the local application session without attempting provider-side sign-out.
- Extended `GET /api/auth/me` with optional, additive `externalIdentity` metadata for the authenticated provider and available profile details.

### Compatibility and upgrade notes

- Existing single-OpenID-Connect consumers can continue using their current configuration and `GET /api/auth/login` flow.
- External login providers are opt-in; applications enable and configure only the providers they use.
- `externalIdentity` is additive to the existing current-user response and does not replace existing fields.
- No known breaking public API or configuration migration is required for existing 1.0.1 consumers.

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
