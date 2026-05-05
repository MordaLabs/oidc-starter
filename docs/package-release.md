# Package Release Notes

The reusable backend and frontend packages are prepared for local packing, but they are not published yet.

## NuGet: OidcStarter.AspNetCore.Bff

From the repository root:

```powershell
dotnet build .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release
dotnet pack .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release --no-build
```

Later publication command:

```powershell
dotnet nuget push .\src\OidcStarter.AspNetCore.Bff\bin\Release\OidcStarter.AspNetCore.Bff.0.1.0.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

## npm: @jszyduk/oidc-starter-auth

From `src/frontend`:

```powershell
Remove-Item .\dist\oidc-starter-auth -Recurse -Force -ErrorAction SilentlyContinue
.\node_modules\.bin\ng.cmd build oidc-starter-auth --configuration production
Set-Location .\dist\oidc-starter-auth
npm pack
```

Later publication command from `src/frontend/dist/oidc-starter-auth`:

```powershell
npm publish --access public
```

Before either publish, confirm the version, license, repository URL, changelog/release notes, and registry credentials.
