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

For controlled P5-A8b acceptance on an independently isolated staging project,
`npm run build:public-demo-acceptance` selects the separate
`frontend:build:public-demo-acceptance` configuration. It keeps BFF mode and
same-origin API calls while compiling both invitation interfaces. The normal
`public-demo` build keeps both interfaces off. In Compose, the frontend build
argument comes from `PUBLIC_DEMO_FRONTEND_CONFIGURATION`, defaulting to
`public-demo`; only that value and `public-demo-acceptance` are accepted. This
selection happens when the image is built, so changing the variable requires a
rebuild. Backend invitation flags and operator authorization remain separate
runtime controls. A staging hostname alone does not isolate the fixed Compose
project's volumes; use a separate host or project identity and resources. No
public acceptance build should be deployed before P5-A8b verification.

## Configure the backend

Run the backend in a production or other non-development environment appropriate
to the hosting platform. Do not treat `src/backend/appsettings.Development.json`
as public deployment configuration.

Supply public values through the host's environment/configuration system or an
appropriate secret-management mechanism. The important existing settings are:

| Setting | Public-demo purpose |
| --- | --- |
| `Starter:FrontendOrigin` | Trusted frontend origin for CORS and BFF login/logout return redirects, for example `https://demo.example.com`. |
| `Starter:DefaultLoginProvider` | Registered provider used by `GET /api/auth/login`; defaults to `oidc`. |
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
the default is `None`. The generic Compose template leaves
`Starter:DefaultLoginProvider` at its package default of `oidc`; operators that expose another default must supply a registered provider id through their own backend configuration plumbing.

## Provision the sample invitation database

Provisioning is an explicit, one-shot backend command. Set
`DemoInvitations__DatabasePath` to a fully qualified SQLite file path in an
existing directory, then run:

```powershell
dotnet run --project src/backend/Backend.csproj -- --provision-demo-invitations
```

The command creates or checks the sample invitation schema and exits without
starting HTTP. It can be repeated without clearing invitations, requests, or
bindings. A missing, relative, or unusable path, an incompatible schema, or a
failed integrity check returns a nonzero exit code. Normal backend startup does
not provision the database. Configure the path and directory permissions for the
same backend identity that will use the database at runtime.

The public-demo Compose template now mounts a dedicated `backend-invitations`
named volume only into the backend. Its fixed absolute database path is
`/var/lib/oidc-starter/invitations/invitations.db`. The following backend
mappings are supplied by `deploy/public-demo/.env.example` and `compose.yaml`:

| Backend setting | Compose source | Tracked default |
| --- | --- | --- |
| `DemoInvitations:DatabasePath` | Fixed path on the invitation volume | `/var/lib/oidc-starter/invitations/invitations.db` |
| `DemoInvitations:Workflow:Enabled` | `DEMO_INVITATIONS_WORKFLOW_ENABLED` | `false` |
| `DemoInvitations:EnforceGoogle` | `DEMO_INVITATIONS_ENFORCE_GOOGLE` | `false` |
| `DemoInvitations:EnforceGitHub` | `DEMO_INVITATIONS_ENFORCE_GITHUB` | `false` |
| `DemoInvitations:Workflow:Operators:0:Issuer` | `DEMO_INVITATIONS_OPERATOR_ISSUER` | empty |
| `DemoInvitations:Workflow:Operators:0:Subject` | `DEMO_INVITATIONS_OPERATOR_SUBJECT` | empty |

Empty or incomplete operator values grant no access. Use the exact validated
Keycloak issuer and raw subject of a private operator identity, obtained from
trusted Keycloak configuration and account records; email and roles are not
allowlist identities. Provider enablement remains independent of invitation
enforcement. The public-demo frontend invitation UI flags remain off.

An invitation created by operator approval of a request permits first
redemption for seven days from approval; at the exact expiry time, first
redemption is denied. A provider identity bound earlier remains valid until
revoked. The policy does not rewrite older invitations with no expiration.

For Compose, run the one-shot provisioner from `deploy/public-demo` with the
reviewed untracked `.env` and the existing backend image:

```sh
docker compose --env-file .env run --rm --no-deps backend --provision-demo-invitations
```

This passes the argument to the image's `dotnet Backend.dll` entrypoint and
does not start the web host. Before any activation, verify volume permissions,
database persistence, backup/restore, and the P5-A8b acceptance flow in staging.
The deployment runbook in `deploy/public-demo/README.md` covers ordering,
rollback, and staging isolation. The fixed Compose project name must not be
shared by a second deployment on the same host.

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

`deploy/public-demo/Caddyfile.example` contains explicit backend routes for the
repository's default callback paths. Every configured OIDC callback or
sign-out callback, including external-provider `CallbackPath` overrides, that
differs from those defaults must have a matching explicit Caddy route to the
backend. Callback routes must not fall through to the static frontend catch-all:
that route strips `Cookie`, while backend callback routes retain authentication
and session cookies.

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
sign-out from the social provider. By contrast, the built-in OIDC provider uses
its configured remote sign-out handler when the session originated from that provider.

Provider discovery is the deployment-health check for runtime login choices:
`GET /api/auth/providers` must list only providers whose handlers are registered and enabled in the running backend. Configure staging and production independently; a provider enabled for staging verification need not be enabled for the public production demo.

Morda Labs currently exposes OpenID Connect, Google, and GitHub in its public production demo. Facebook is an implemented opt-in package capability and has been validated in staging, but is intentionally not enabled publicly while Meta Business Verification is pending. This operational limitation does not affect generic deployment templates or package support.

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
