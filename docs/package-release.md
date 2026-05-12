# Package Release Notes

The reusable backend and frontend packages are published. This note records the local pack and maintainer publish commands.

## NuGet: OidcStarter.AspNetCore.Bff

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

- Backend antiforgery validation remains opt-in through `Starter:RequireAntiforgeryToken`; this is
  not a backend package breaking default change yet.
- The sample backend enables `RequireAntiforgeryToken` because the sample frontend now initializes
  antiforgery with `GET /api/auth/csrf` before BFF logout.
- Frontends integrating with the BFF package must call `GET /api/auth/csrf` and send the returned
  `XSRF-TOKEN` value on state-changing BFF requests before enabling backend antiforgery validation.
