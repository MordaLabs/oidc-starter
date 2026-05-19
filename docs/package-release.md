# Package Release Notes

The reusable backend and frontend packages are published. This note records the local pack and maintainer publish commands.

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
dotnet nuget push .\src\OidcStarter.AspNetCore.Bff\bin\Release\OidcStarter.AspNetCore.Bff.0.1.0.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

## npm: @flying-bee/oidc-starter-auth

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
  requests. This is a behavioral hardening change relevant for the next backend package release.

## v1.0.0 Readiness Checklist

- Confirm final package version and release notes.
- Run `dotnet test .\src\OidcStarter.AspNetCore.Bff.Tests\OidcStarter.AspNetCore.Bff.Tests.csproj`.
- Build and pack the backend package in `Release`.
- Confirm README, package metadata, and NuGet package page links.
- Verify the sample BFF flow against the local Keycloak realm after a clean import.
