# OidcStarter.AspNetCore.Bff

Reusable ASP.NET Core backend-for-frontend auth building blocks for OIDC Starter.

This project is package-ready inside the repository, but it is not published to NuGet yet. The sample backend currently consumes it through a project reference.

## What It Provides

- `AddOidcStarterBff(configuration)` for cookie/OIDC auth, CORS, forwarded headers, authorization, and BFF services.
- `UseOidcStarterBff()` for the expected middleware order.
- `/api/auth/login`, `/api/auth/me`, and `/api/auth/logout` endpoints.
- `OidcOptions` and `OidcStarterBffOptions` configuration models.
- `ICurrentUserService` and `CurrentUserResponse` for current-user claim mapping.
- A lightweight origin check for logout form posts.

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
    "AllowedForwardedHosts": [ "localhost" ]
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

Before publishing, add final license metadata, repository URL, release versioning, package validation, and tests around the public API surface.
