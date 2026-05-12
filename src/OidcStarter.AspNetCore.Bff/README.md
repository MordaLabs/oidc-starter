# OidcStarter.AspNetCore.Bff

Reusable ASP.NET Core backend-for-frontend auth building blocks for OIDC Starter.

This package is published to NuGet as `OidcStarter.AspNetCore.Bff`. The sample backend in this repository consumes the in-repository project via project reference so changes can be developed and verified locally.

## Install

```powershell
dotnet add package OidcStarter.AspNetCore.Bff
```

## What It Provides

- `AddOidcStarterBff(configuration)` for cookie/OIDC auth, CORS, forwarded headers, authorization, and BFF services.
- `UseOidcStarterBff()` for the expected middleware order.
- `/api/auth/login`, `/api/auth/me`, and `/api/auth/logout` endpoints.
- `OidcOptions` and `OidcStarterBffOptions` configuration models.
- `ICurrentUserService` and `CurrentUserResponse` for current-user claim mapping.
- A lightweight origin check for logout form posts.
- Antiforgery token groundwork for cookie-authenticated BFF endpoints.

## Sample Backend Usage

```csharp
using OidcStarter.AspNetCore.Bff.Extensions;

builder.Services.AddOidcStarterBff(builder.Configuration);

var app = builder.Build();

app.UseOidcStarterBff();
app.MapControllers();
app.Run();
```

The sample backend keeps sample-only endpoints such as `/api/public/ping` in its own project.

## Configuration

```json
{
  "Starter": {
    "FrontendOrigin": "http://localhost:4200",
    "AllowedForwardedHosts": [ "localhost" ],
    "KnownForwardedProxies": [],
    "KnownForwardedNetworks": [],
    "SessionLifetime": "08:00:00",
    "SlidingExpiration": true,
    "CookieSameSite": "None",
    "RequireAntiforgeryToken": false,
    "AntiforgeryHeaderName": "X-XSRF-TOKEN",
    "AntiforgeryCookieSecurePolicy": "SameAsRequest"
  },
  "Oidc": {
    "Authority": "https://identity.example.com/realms/example",
    "ClientId": "example-bff",
    "ClientSecret": "<secret>",
    "CallbackPath": "/signin-oidc",
    "SignedOutCallbackPath": "/signout-callback-oidc",
    "RequireHttpsMetadata": true,
    "Scopes": [ "openid", "profile", "email" ]
  }
}
```

## Security And Hosting Notes

The package sets an HTTP-only, secure `__Host-` session cookie with an 8-hour sliding lifetime by
default. Keep `CookieSameSite` as `None` for split-origin local development; production BFF
deployments should usually serve the frontend and backend from one public site and change it to
`Lax` or `Strict` where the OIDC provider flow and hosting topology allow it.

## Antiforgery Contract

`POST /api/auth/logout` always checks `Origin` or `Referer` against `Starter:FrontendOrigin` and the
current backend origin.

Set `Starter:RequireAntiforgeryToken` to `true` to require ASP.NET Core antiforgery validation for
state-changing BFF endpoints. This remains opt-in in the package defaults, so enabling it is not a
breaking default behavior change yet.

The antiforgery cookie secure policy defaults to `SameAsRequest` so local HTTP samples can obtain a
token. Production apps should run behind HTTPS and set `Starter:AntiforgeryCookieSecurePolicy` to
`Always` unless their hosting platform has a specific reason not to.

Frontend integration contract:

- Call `GET /api/auth/csrf` before the first state-changing BFF request, and again whenever the
  frontend needs to refresh the antiforgery token.
- Read the request token from the `XSRF-TOKEN` cookie. The cookie name can be changed with
  `Starter:AntiforgeryRequestTokenCookieName`.
- For `fetch`, XHR, or Angular `HttpClient` requests, send the token in the `X-XSRF-TOKEN` header.
  The header name can be changed with `Starter:AntiforgeryHeaderName`.
- For top-level form posts such as OIDC logout navigation, send the same token in the ASP.NET Core
  antiforgery form field named `__RequestVerificationToken`.
- Treat every cookie-authenticated BFF request that changes server-side or identity-provider state
  as state-changing. In this package today that means `POST /api/auth/logout`; custom endpoints added
  by consuming apps should follow the same rule for `POST`, `PUT`, `PATCH`, and `DELETE`.

Any custom frontend or third-party frontend integrating with this backend package must implement this
contract before `Starter:RequireAntiforgeryToken` is enabled.

`UseOidcStarterBff()` applies forwarded headers before HTTPS redirection. `AllowedForwardedHosts`
limits accepted `X-Forwarded-Host` values. In production, also set `KnownForwardedProxies` to trusted
proxy IP addresses or `KnownForwardedNetworks` to trusted CIDR ranges such as `10.0.0.0/8`; do not
trust arbitrary forwarded headers from the public internet.

## Local Packaging

From the repository root:

```powershell
dotnet build .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release
dotnet pack .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release --no-build
```

The package is written to `src/OidcStarter.AspNetCore.Bff/bin/Release`.

Publication command for maintainers:

```powershell
dotnet nuget push .\src\OidcStarter.AspNetCore.Bff\bin\Release\OidcStarter.AspNetCore.Bff.0.1.0.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

Only publish after confirming the target version, release notes, and registry credentials.
