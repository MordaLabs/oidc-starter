# OIDC Starter

**Provider-neutral authentication building blocks for Angular and ASP.NET Core.**

OIDC Starter helps teams add a reusable, understandable authentication foundation without making the browser responsible for provider tokens. Its primary path is an ASP.NET Core backend-for-frontend (BFF): Angular talks to the BFF, the BFF coordinates sign-in with an identity provider, and the browser uses a protected application session.

> ## [Try the live demo →](https://oidc-starter.mordalabs.com)

Try built-in OpenID Connect with **`demo` / `demo`**, or use your own Google or GitHub account. The demo also lets you inspect the normalized BFF session and current-user response.

| Backend package | Frontend package |
| --- | --- |
| [NuGet: `OidcStarter.AspNetCore.Bff`](https://www.nuget.org/packages/OidcStarter.AspNetCore.Bff/) | [npm: `@flying-bee/oidc-starter-auth`](https://www.npmjs.com/package/@flying-bee/oidc-starter-auth) |

## Why OIDC Starter

- **BFF-first:** keep provider interaction behind an ASP.NET Core application session instead of exposing provider tokens through the browser current-user contract.
- **Provider-neutral:** use the built-in OpenID Connect flow, add supported social providers when needed, or register a custom login provider.
- **Designed for real hosting:** includes forwarded-header support for reverse-proxy deployments, secure cookie defaults, and antiforgery protection for package-provided state-changing BFF endpoints.
- **Reusable on both sides:** pair the ASP.NET Core BFF package with optional Angular helpers for login, logout, current-user loading, provider discovery, and antiforgery initialization.

## How the BFF flow fits together

```text
Browser / Angular frontend
          │
          │  /api/auth/*
          ▼
ASP.NET Core BFF ─────────────► OpenID Connect or social provider
          │                              │
          └──── protected application ◄──┘
                 session cookie
```

The frontend discovers the providers enabled by the server, starts the selected login flow, and reads normalized session information from `GET /api/auth/me`. The package supports provider-aware login and logout while leaving application authorization, tenant access, and business rules with the consuming application.

## External login providers

OpenID Connect is the built-in, default-compatible BFF flow. Google, GitHub, and Facebook registrations are opt-in. Applications can also register another provider through the generic login-provider extension point; runtime provider discovery keeps the frontend chooser aligned with the options actually configured by the server.

| Provider | Package capability | Production live demo |
| --- | --- | --- |
| OpenID Connect | Built-in BFF flow | Available — use `demo` / `demo` |
| Google | Opt-in registration | Available — use your Google account |
| GitHub | Opt-in registration | Available — use your GitHub account |
| Facebook | Opt-in registration | Implemented, but not enabled on the public production demo while Meta Business Verification is pending |
| Custom provider | Generic registration extension point | Application-defined |

The production demo intentionally reflects its runtime configuration; it is not a complete list of every provider the packages support.

## Packages

### ASP.NET Core BFF

Install the backend package:

```powershell
dotnet add package OidcStarter.AspNetCore.Bff
```

Register the BFF services and middleware in an ASP.NET Core host:

```csharp
builder.Services.AddOidcStarterBff(builder.Configuration);

var app = builder.Build();
app.UseOidcStarterBff();
app.MapControllers();
```

The package provides cookie/OIDC configuration, BFF endpoints, authorization foundations, current-user projection, role-mapping extension points, antiforgery support, and forwarded-header configuration. See the [backend package guide](src/OidcStarter.AspNetCore.Bff/README.md) for configuration, security, and hosting details.

### Angular integration

Install the optional Angular package:

```powershell
npm install @flying-bee/oidc-starter-auth
```

Use `provideBffAuth(...)` and `BffAuthService` for BFF current-user loading, login redirects, provider discovery, logout, and antiforgery setup. The package also retains an SPA/reference wrapper for the sample's direct OIDC flow. See the [Angular package guide](src/frontend/projects/oidc-starter-auth/README.md) for setup details.

## Quick start with the sample

The repository includes a sample ASP.NET Core backend, Angular frontend, and local Keycloak environment.

1. Start the local identity provider using the [Keycloak setup guide](infra/keycloak/README.md).
2. Run `src/backend` with the HTTPS launch profile.
3. From `src/frontend`, run `npm install` and `npm start`.
4. Open `http://localhost:4200` and use the sample sign-in flow.

For the BFF and SPA reference modes, configuration, and security boundaries, see the [architecture overview](docs/architecture.md). The public demo deployment model is documented in [Public Demo Deployment](docs/public-demo-deployment.md).

## Security and hosting notes

OIDC Starter configures an HTTP-only, secure application cookie and antiforgery support for package-provided logout. Custom cookie-authenticated endpoints that change state should follow the same antiforgery contract. The BFF pipeline also applies forwarded-header handling so reverse-proxy deployments can establish the correct external request context.

These are starter building blocks, not a substitute for application-specific authorization, secret storage, provider configuration, HTTPS, or production operations decisions.

## Repository map

| Path | Purpose |
| --- | --- |
| `src/OidcStarter.AspNetCore.Bff` | Reusable ASP.NET Core BFF package |
| `src/frontend/projects/oidc-starter-auth` | Reusable Angular authentication package |
| `src/backend` | Sample BFF host |
| `src/frontend` | Sample Angular application and public demo UI |
| `infra/keycloak` | Local development identity-provider setup |

## Support and project links

- [Live demo](https://oidc-starter.mordalabs.com)
- [Source repository](https://github.com/MordaLabs/oidc-starter)
- [Issues](https://github.com/MordaLabs/oidc-starter/issues)
- [Security policy](SECURITY.md) and [private security reporting](mailto:security@mordalabs.com)
- [Privacy Policy](https://oidc-starter.mordalabs.com/privacy.html) · [Terms of Use](https://oidc-starter.mordalabs.com/terms.html) · [Data Deletion](https://oidc-starter.mordalabs.com/data-deletion.html)
- Contact Morda Labs: [contact@mordalabs.com](mailto:contact@mordalabs.com)

## License

OIDC Starter is licensed under the [MIT License](LICENSE).
