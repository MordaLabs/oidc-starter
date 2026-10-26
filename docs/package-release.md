# Package Release Notes

This note records the local pack and maintainer publish commands for the reusable backend and frontend packages. Publication status should be verified from the relevant registry.

## NuGet: OidcStarter.AspNetCore.Bff

Package page:

- https://www.nuget.org/packages/OidcStarter.AspNetCore.Bff/

From the repository root:

```powershell
dotnet build .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release
dotnet pack .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release --no-build
```

Publication command for maintainers:

```powershell
dotnet nuget push .\src\OidcStarter.AspNetCore.Bff\bin\Release\OidcStarter.AspNetCore.Bff.<version>.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

## Current published backend release: 1.4.0

`OidcStarter.AspNetCore.Bff` `1.4.0` is published on NuGet. The GitHub Release is published
under tag `v1.4.0` from source SHA `6b9cbcc78ce2437395bc936cacbcd84ddbf70477`.
The final published nupkg SHA-256 is
`ebeb35e2b367f3e29b7798c34ccd4e1d3bcdde9beb72a71382e3de553c9b27e1`.

- Targets `net9.0;net10.0`, retaining .NET 9 compatibility and adding .NET 10 support.
- No public API or configuration migration is required.
- BFF tests passed 165/165 on each target framework; clean package consumers passed on both.
- The OSS sample/backend and Docker backend were verified on .NET 10. Staging verified OIDC
  login/logout and authenticated-session continuity across backend container replacement.

## Published backend release: 1.3.0

`OidcStarter.AspNetCore.Bff` `1.3.0` is published on NuGet.

- Backward-compatible additive minor release.
- Adds the validated OIDC sign-in metadata handoff and read-only accessor.
- Breaking changes: none.
- Login/logout endpoint changes: none.
- Existing consumer configuration migration: none.
- Premium-specific API: none.
- Project Website: `https://oidc-starter.mordalabs.com/`.
- Repository: `https://github.com/MordaLabs/oidc-starter`.

## Published backend releases: 1.2.0 and 1.2.1

`OidcStarter.AspNetCore.Bff` `1.2.0` was published as a backward-compatible minor release.

- Breaking changes: none.
- Runtime behavior changes: none.
- Public API addition: `AuthenticationProperties.TryGetOidcStarterLoginProviderId(...)`, providing
  supported read-only access to the persisted login-provider identity for downstream integrations and
  extensions.
- The persisted property-key literal remains internal.
- Configuration migration: none.
- Targeted P3A backend validation: 58 tests passed, 0 failed.

`OidcStarter.AspNetCore.Bff` `1.2.1` was a published patch release. It corrected the NuGet
Project Website to `https://oidc-starter.mordalabs.com/`; repository metadata remains
`https://github.com/MordaLabs/oidc-starter`.

- Metadata-only patch release.
- Breaking changes: none.
- Public API changes: none.
- Runtime behavior changes: none.
- Configuration changes: none.

These backend releases do not include a frontend/npm release. The maintained frontend package remains
`@mordalabs/oidc-starter-auth` `0.2.0`, published and verified from the public npm registry. Angular
compatibility remains verified for Angular 20.3+, 21, and 22.

## npm scope migration

The maintained package is `@mordalabs/oidc-starter-auth`; the current lineage version is `0.2.0`. `@mordalabs/oidc-starter-auth@0.2.0` is published and was verified from the public npm registry.

- `@flying-bee/oidc-starter-auth` remains available for existing consumers.
- All published old-scope versions (`0.1.0`, `0.1.1`, and `0.2.0`) are deprecated with migration guidance to `@mordalabs/oidc-starter-auth`.
- The old package must not be unpublished.

## npm release history: @flying-bee/oidc-starter-auth

### v0.2.0

Backward-compatible feature release for provider-aware BFF integration.

- Adds provider-targeted `BffAuthService.login(providerId)` while retaining the existing default `login()` flow.
- Adds `getLoginProviders()` and `BffLoginProvider` for runtime provider discovery and consumer-owned provider pickers.
- Adds optional `BffExternalIdentity` data through `BffCurrentUser.externalIdentity`.
- Existing default/single-provider BFF consumers require no mandatory migration.
- Existing SPA/reference-mode consumers require no migration.
- Declared peer compatibility remains Angular 20.3+, Angular 21, and Angular 22.

Publication status should be verified from the npm registry.

### v0.1.1 — historical

Angular compatibility maintenance release. Angular 20.3+, Angular 21, and Angular 22 compatibility,
the committed change, and committed-diff review were verified. The published artifact metadata was
verified with `latest` pointing to `0.1.1`, `@angular/common` and `@angular/core` set to
`^20.3.0 || ^21.0.0 || ^22.0.0`, `angular-auth-oidc-client` set to `^21.0.1`, and RxJS set to
`~7.8.0`. No further compatibility implementation was required; the release was published to npm.

From `src/frontend`:

```powershell
Remove-Item .\dist\oidc-starter-auth -Recurse -Force -ErrorAction SilentlyContinue
.\node_modules\.bin\ng.cmd build oidc-starter-auth --configuration production
Set-Location .\dist\oidc-starter-auth
npm pack
```

Publication command for maintainers from `src/frontend/dist/oidc-starter-auth`:

```powershell
npm publish --access public
```

Before either publish, confirm the target version, changelog/release notes, and registry credentials.

## Integration Notes

- Package logout antiforgery validation is always on through a package-local filter that calls
  ASP.NET Core `IAntiforgery`. Custom frontends must initialize antiforgery with
  `GET /api/auth/csrf` before BFF logout.
- `Starter:RequireAntiforgeryToken` is retained for compatibility but no longer controls
  package-provided endpoints.
- Frontends integrating with the BFF package must call `GET /api/auth/csrf` and send the returned
  `XSRF-TOKEN` value on state-changing BFF requests.
- Role mapping is provider-agnostic by default. Consumers should register `IOidcStarterRoleMapper`
  for providers that emit nested or custom role structures.

## Post-Release Hardening Fixes

Use this section to accumulate security and hardening fixes identified after the latest package
release and before the next patch/minor release.

Hardening IDs such as `HARDENING-021` and baseline IDs such as `BFF-CSRF-004` are defined by the
companion auditor repository, `oidc-starter-agent`: https://github.com/jszyduk/oidc-starter-agent/blob/master/docs/security-baseline-v1.md

- `HARDENING-021 / BFF-CSRF-004`: The audit flagged that unsafe browser-to-BFF endpoints might not
  be covered by antiforgery validation. In `oidc-starter`, `POST /api/auth/logout` is now always
  protected by antiforgery validation through a package-local filter that calls ASP.NET Core
  `IAntiforgery`. This matters for consumers because frontend logout integrations must continue to
  follow the documented contract: call `GET /api/auth/csrf` and send the returned token with logout
  requests. This behavior is part of the current package contract; `v1.0.1` only adds validation
  evidence and documentation clarification, with no runtime behavior change.

## Historical: v1.0.1 Hardening Validation Patch

`OidcStarter.AspNetCore.Bff` `v1.0.1` is a hardening validation patch release.

- Breaking changes: none.
- Public API changes: none.
- Runtime behavior changes: none.
- Validation improvements:
  - focused login/logout/current-user/session-state behavior test coverage,
  - antiforgery token issuing, request validation, and unsafe endpoint protection test coverage,
  - unauthorized/forbidden API behavior test coverage,
  - package/sample build compatibility evidence,
  - audit smoke confirmed 0 findings.

The `v1.0.1` package was prepared and published as a separate historical release task.

## Release Readiness Checklist

- Confirm final package version and release notes.
- Run `dotnet test .\src\OidcStarter.AspNetCore.Bff.Tests\OidcStarter.AspNetCore.Bff.Tests.csproj`.
- Build and pack the backend package in `Release`.
- Confirm README, package metadata, and NuGet package page links.
- Verify the sample BFF flow against the local Keycloak realm after a clean import.
