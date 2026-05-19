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
- `/api/auth/login`, `/api/auth/me`, `/api/auth/csrf`, and `/api/auth/logout` endpoints.
- `OidcOptions` and `OidcStarterBffOptions` configuration models.
- `ICurrentUserService` and `CurrentUserResponse` for current-user claim mapping.
- A lightweight origin check for logout form posts.
- Antiforgery token groundwork for cookie-authenticated BFF endpoints.
- Authorization policy constants and policy-builder helpers.
- Provider-agnostic role mapping with `IOidcStarterRoleMapper`.

## Public API Surface

The package keeps its public surface intentionally small:

- `AddOidcStarterBff(configuration)` registers the BFF services, controllers, authentication,
  antiforgery, forwarded-header options, CORS, and authorization policies.
- `UseOidcStarterBff()` applies the expected middleware order.
- `AddOidcStarterRoleMapper<TMapper>()` registers provider-specific role extraction logic while
  preserving the default flat-claim mapper.
- `OidcOptions`, `OidcStarterBffOptions`, and `RequiredClaimOptions` describe supported
  configuration.
- `OidcStarterBffPolicies` exposes stable policy names for consuming apps.
- `OidcStarterAuthorizationPolicyBuilderExtensions` adds scope/claim policy helpers.
- `IOidcStarterRoleMapper` and `OidcStarterRoleMappingContext` are the role-mapping extension point.
- `ICurrentUserService` and `CurrentUserResponse` expose the current-user contract used by
  `/api/auth/me`.

Controllers, low-level validators, and default service implementations exist to support the package
endpoints and are not intended as customization points.

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
    "AntiforgeryHeaderName": "X-XSRF-TOKEN",
    "AntiforgeryCookieSecurePolicy": "SameAsRequest",
    "NameClaimType": "name",
    "RoleClaimType": "role",
    "AdditionalRoleClaimTypes": [
      "http://schemas.microsoft.com/ws/2008/06/identity/claims/role",
      "roles"
    ],
    "RequiredScopes": [],
    "RequiredClaims": []
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

`POST /api/auth/logout` is protected by a package-local MVC authorization filter that calls
ASP.NET Core `IAntiforgery.ValidateRequestAsync`. It also checks `Origin` or `Referer` against
`Starter:FrontendOrigin` and the current backend origin. The origin check remains defense in depth;
antiforgery validation is the primary CSRF protection for the package-provided unsafe endpoint.

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
contract before calling package-provided or app-defined state-changing BFF endpoints.

## Authorization Foundation

The package configures ASP.NET Core authorization and exposes a small policy foundation rather than a
custom RBAC framework.

Defaults:

- `Starter:NameClaimType` defaults to `name` and is used for OIDC token validation and `/api/auth/me`.
- `Starter:RoleClaimType` defaults to `role` and is used by ASP.NET Core role authorization, including
  `[Authorize(Roles = "...")]`.
- `Starter:AdditionalRoleClaimTypes` defaults to the ASP.NET Core role claim URI and `roles`; these
  extra claim types are included in the `roles` array returned by `/api/auth/me`.
- `IOidcStarterRoleMapper` has a built-in flat-claim implementation that reads those configured role
  claim types.
- The package registers `OidcStarterBffPolicies.AuthenticatedUser`, a named policy that only requires
  a valid authenticated backend session.

Optional configured policies:

- Set `Starter:RequiredScopes` to add `OidcStarterBffPolicies.ConfiguredRequiredScopes`. The policy
  requires all configured scopes and checks both `scope` and `scp` claims.
- Set `Starter:RequiredClaims` to add `OidcStarterBffPolicies.ConfiguredRequiredClaims`. Each entry
  requires the claim type to exist; when `Values` are supplied, at least one matching value must be
  present.

Example:

```json
{
  "Starter": {
    "RoleClaimType": "roles",
    "RequiredScopes": [ "profile" ],
    "RequiredClaims": [
      { "Type": "tenant", "Values": [ "academy" ] }
    ]
  }
}
```

```csharp
using Microsoft.AspNetCore.Authorization;
using OidcStarter.AspNetCore.Bff.Authorization;

[Authorize(Policy = OidcStarterBffPolicies.AuthenticatedUser)]
[HttpGet("/api/protected/ping")]
public IActionResult Ping() => Ok();
```

Consuming applications still own their business authorization model: application roles, tenant
membership, resource ownership, and domain-specific policies should be defined in the consuming app.

### Custom Role Mapping

Different OIDC providers represent roles differently. The package handles flat role claims by
default, but it does not hardcode provider-specific nested structures. If a provider emits roles in
custom or nested claims, add an `IOidcStarterRoleMapper` implementation in the consuming app. The
mapper receives the current principal and, during OIDC ticket creation, the saved backend
`access_token` when one is available:

```csharp
using OidcStarter.AspNetCore.Bff.Authorization;

internal sealed class MyProviderRoleMapper : IOidcStarterRoleMapper
{
    public IEnumerable<string> GetRoles(OidcStarterRoleMappingContext context)
    {
        // Extract provider-specific roles from context.Principal or context.AccessToken.
        yield return "example-role";
    }
}
```

Register it before or after `AddOidcStarterBff`:

```csharp
builder.Services.AddOidcStarterRoleMapper<MyProviderRoleMapper>();
builder.Services.AddOidcStarterBff(builder.Configuration);
```

Mapped roles are additive. The default flat-claim mapper remains active, and custom mapper output is
deduplicated for `/api/auth/me`. The package also copies mapped roles into the configured
`Starter:RoleClaimType` through claims transformation, so ASP.NET Core role checks can use the same
normalized role names.

The sample backend demonstrates this extension point with a Keycloak-specific mapper that reads the
backend `access_token` and extracts roles from `realm_access.roles` and
`resource_access.{client}.roles`. It filters `offline_access`, `uma_authorization`, and
`default-roles-*` as sample noise, but otherwise leaves realm and client roles visible. That logic
intentionally lives in the sample app, not in the reusable package.

`UseOidcStarterBff()` applies forwarded headers before HTTPS redirection. `AllowedForwardedHosts`
limits accepted `X-Forwarded-Host` values. In production, also set `KnownForwardedProxies` to trusted
proxy IP addresses or `KnownForwardedNetworks` to trusted CIDR ranges such as `10.0.0.0/8`; do not
trust arbitrary forwarded headers from the public internet.

## Release Readiness Notes

The package is being prepared for a stable `1.0.0` API. Default role mapping still reads flat role
claims, and provider-specific role extraction remains app-owned.

Integration requirements consumers should know before enabling production settings:

- Configure `Oidc` with a confidential OIDC client suitable for server-side login.
- Set `Starter:FrontendOrigin` to the trusted frontend origin.
- Implement the antiforgery contract before calling state-changing BFF endpoints such as
  `POST /api/auth/logout`.
- Configure forwarded headers with trusted hosts/proxies when running behind a reverse proxy.
- Normalize provider-specific roles with `IOidcStarterRoleMapper` when the provider does not emit
  flat role claims.
- Move secrets out of checked-in appsettings files for real deployments.

Version 1.0.0 establishes the initial stable contract for the package: cookie-backed OIDC BFF authentication,
antiforgery integration points, authorization helpers, and provider-agnostic role-mapping extensibility.
`POST /api/auth/logout` now always requires antiforgery validation. Consumers with custom frontends
must call `GET /api/auth/csrf` and submit the returned token with logout requests. The earlier
`Starter:RequireAntiforgeryToken` switch is retained for compatibility but no longer controls
package-provided endpoints.

## Local Packaging

From the repository root:

```powershell
dotnet build .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release
dotnet pack .\src\OidcStarter.AspNetCore.Bff\OidcStarter.AspNetCore.Bff.csproj -c Release --no-build
```

The package is written to `src/OidcStarter.AspNetCore.Bff/bin/Release`.

Publication command for maintainers:

```powershell
dotnet nuget push .\src\OidcStarter.AspNetCore.Bff\bin\Release\OidcStarter.AspNetCore.Bff.1.0.0.nupkg --api-key <NUGET_API_KEY> --source https://api.nuget.org/v3/index.json
```

Only publish after confirming the target version, release notes, and registry credentials.
