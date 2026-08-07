# Public Demo Deployment

This guide describes the existing public-demo configuration. It does not provide
deployment infrastructure or platform-specific reverse-proxy configuration.

## Recommended topology

For the simplest supported public demo, serve the Angular public-demo build and
the ASP.NET Core BFF from one public HTTPS site, such as
`https://demo.example.com`.

- The browser loads the Angular application from the public site.
- The public-demo build uses BFF authentication and calls relative `/api/*`
  URLs on that same origin.
- The BFF starts provider redirects and owns the authenticated session cookie.

Same-origin hosting is the recommended simple topology, not the only possible
architecture. A split-origin deployment must configure the trusted frontend
origin and browser behavior accordingly.

## Build the public-demo frontend

From `src/frontend`, build the dedicated public-demo configuration:

```powershell
npm run build:public-demo
```

This build uses `authMode: 'bff'`, an empty `apiOrigin` for relative same-origin
API calls, and no localhost SPA/Keycloak settings. Deploy the generated frontend
assets with the BFF available at the same public origin under `/api`.

## Configure the backend

Run the backend in a production or other non-development environment appropriate
to the hosting platform. Do not treat `src/backend/appsettings.Development.json`
as public deployment configuration.

Supply public values through the host's environment/configuration system or an
appropriate secret-management mechanism. The important existing settings are:

| Setting | Public-demo purpose |
| --- | --- |
| `Starter:FrontendOrigin` | Trusted frontend origin for CORS and BFF login/logout return redirects, for example `https://demo.example.com`. |
| `Starter:AllowedForwardedHosts` | Accepted public hosts supplied through `X-Forwarded-Host`. |
| `Starter:KnownForwardedProxies` | Trusted reverse-proxy IP addresses. |
| `Starter:KnownForwardedNetworks` | Trusted reverse-proxy CIDR ranges when IP addresses are not the right fit. |
| `Starter:CookieSameSite` | SameSite setting for the BFF session and external-login correlation cookies. |
| `Starter:AntiforgeryCookieSecurePolicy` | Antiforgery-cookie secure policy; use `Always` for the public HTTPS deployment. |
| `Oidc:Authority` | Public OIDC authority. |
| `Oidc:ClientId` | BFF OIDC client ID. |
| `Oidc:ClientSecret` | BFF OIDC client secret when required by the provider. |
| `Oidc:CallbackPath` | Local OIDC callback path; default: `/signin-oidc`. |
| `Oidc:SignedOutCallbackPath` | Local OIDC signed-out callback path; default: `/signout-callback-oidc`. |
| `Oidc:RequireHttpsMetadata` | Keep `true` for a public HTTPS identity-provider deployment. |

The BFF session cookie is HTTP-only and always secure. Configure
`Starter:CookieSameSite` for the actual provider redirect and hosting topology;
the default is `None`.

## HTTPS and reverse proxies

The BFF applies forwarded headers before HTTPS redirection and supports
`X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host`. This lets the BFF
form public callback URLs correctly when TLS terminates at a reverse proxy.

Configure `AllowedForwardedHosts` with the actual public host and configure
either `KnownForwardedProxies` or `KnownForwardedNetworks` for the proxy path.
Do not trust arbitrary forwarded headers, proxy addresses, or networks. The
exact values depend on the hosting topology and are operator configuration, not
application defaults.

## OIDC callbacks and sign-out

Register the public OIDC redirect URI with the identity provider using the
configured callback path. With the defaults above, use:

```text
https://demo.example.com/signin-oidc
```

Where the OIDC provider requires a post-logout redirect URI, also register:

```text
https://demo.example.com/signout-callback-oidc
```

OIDC is the built-in BFF provider and supports remote sign-out through its
configured handler.

## Optional social providers

Google, Facebook, and GitHub are opt-in. Enable a provider only when its
external configuration is present; each enabled provider validates its required
credentials during startup.

| Provider | Enable setting | Credential settings | Default public callback |
| --- | --- | --- | --- |
| Google | `ExternalLogin:Google:Enabled` | `ExternalLogin:Google:Options:ClientId`, `ExternalLogin:Google:Options:ClientSecret` | `https://demo.example.com/signin-google` |
| Facebook | `ExternalLogin:Facebook:Enabled` | `ExternalLogin:Facebook:Options:AppId`, `ExternalLogin:Facebook:Options:AppSecret` | `https://demo.example.com/signin-facebook` |
| GitHub | `ExternalLogin:GitHub:Enabled` | `ExternalLogin:GitHub:Options:ClientId`, `ExternalLogin:GitHub:Options:ClientSecret` | `https://demo.example.com/signin-github` |

Register the corresponding callback URL with each enabled provider. Optional
callback overrides use `ExternalLogin:Google:Options:CallbackPath`,
`ExternalLogin:Facebook:Options:CallbackPath`, or
`ExternalLogin:GitHub:Options:CallbackPath`; each must be a local absolute path.
Social-provider logout clears the local BFF session; it does not request remote
sign-out from the social provider.

## Secrets boundary

Do not commit public OIDC or social-provider secrets, and do not put them in the
Angular bundle. Supply backend secrets through deployment/environment
configuration or a suitable secret-management mechanism. The Angular
public-demo build requires no social-provider client secret.

## Availability and minimum check

Use `GET /api/public/ping` as the simple backend availability check.

After deployment:

1. Open the public HTTPS page.
2. Confirm `https://demo.example.com/api/public/ping` succeeds.
3. Confirm `https://demo.example.com/api/auth/providers` is reachable.
4. Confirm the intended enabled provider or providers appear in the response.
