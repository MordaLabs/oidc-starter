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

## npm scope migration

The maintained package source identity is `@mordalabs/oidc-starter-auth`; npm publication migration is in progress. Its lineage version remains `0.2.0`.

- `@flying-bee/oidc-starter-auth@0.2.0` remains available.
- Before deprecating the old scope, locally validate, publish, and publicly verify the new-scope package.
- Verify the new-scope publication status from npm.
- Do not unpublish the old package; deprecation is a post-verification operator action.

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

## v1.0.1 Hardening Validation Patch

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

For `v1.0.1`, confirm the package project version has been bumped in a separate versioning task,
then pack and publish the generated `OidcStarter.AspNetCore.Bff.1.0.1.nupkg` after final validation.

## Release Readiness Checklist

- Confirm final package version and release notes.
- Run `dotnet test .\src\OidcStarter.AspNetCore.Bff.Tests\OidcStarter.AspNetCore.Bff.Tests.csproj`.
- Build and pack the backend package in `Release`.
- Confirm README, package metadata, and NuGet package page links.
- Verify the sample BFF flow against the local Keycloak realm after a clean import.
