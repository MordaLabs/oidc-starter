# OIDC Starter

OIDC Starter is a public reference implementation and reusable starter for OpenID Connect with
Angular, ASP.NET Core, and Keycloak. It demonstrates both direct browser OIDC and a
cookie-backed backend-for-frontend (BFF) pattern, with reusable package sources kept alongside a
working sample app.

The BFF path is the primary production-oriented direction. It already includes practical foundations
for server-side OIDC sign-in, HTTP-only cookie sessions, antiforgery protection, authorization
policies, role mapping, local Keycloak provisioning, and package-level tests. The reusable backend
package has a stable `1.0.x` contract; the `1.0.1` backend package release is a hardening validation
patch with no runtime behavior changes, no public API changes, and no breaking changes.

The repository contains:

- `src/OidcStarter.AspNetCore.Bff`: reusable ASP.NET Core BFF NuGet package source
- `src/frontend/projects/oidc-starter-auth`: reusable Angular auth npm package source
- `src/backend`: sample ASP.NET Core host that consumes the BFF package
- `src/frontend`: sample Angular app that consumes the Angular auth package
- `src/OidcStarter.AspNetCore.Bff.Tests`: automated tests for the reusable backend package
- `infra/keycloak`: local Keycloak Docker Compose setup with automated realm import
- `docs`: architecture and package release notes

## Package Vs Sample Responsibilities

The reusable backend package owns generic BFF infrastructure: cookie/OIDC authentication,
antiforgery endpoints, current-user projection, authorization policy helpers, configurable claim
handling, and provider-agnostic role mapping extension points.

The sample backend owns local app concerns: public/demo endpoints, development configuration, and the
Keycloak-specific role mapper that reads roles from Keycloak access tokens. That provider-specific
mapper is intentionally not built into the reusable package.

The reusable Angular package owns frontend auth helpers for the sample flows. The sample Angular app
owns presentation, environment selection, and local development wiring.

## Supported Auth Modes

| Mode | Status | Description |
| --- | --- | --- |
| `spa` | Reference mode | The Angular app signs in directly with Keycloak using Authorization Code Flow with PKCE. |
| `bff` | Primary starter path | The Angular app delegates login/logout/user lookup to the ASP.NET Core backend, which keeps OIDC tokens server-side behind a cookie session. |

The development frontend currently defaults to `bff` mode in
`src/frontend/src/environments/environment.development.ts`.

## Prerequisites

- .NET SDK 9
- Node.js and npm
- Docker Desktop or another Docker Compose compatible runtime
- Local Keycloak for development

Detailed local Keycloak setup is documented in
[`infra/keycloak/README.md`](infra/keycloak/README.md), including
the imported realm, clients, test user, sample role, and local development credentials.

## Run Keycloak

From the repository root:

```powershell
cd .\infra\keycloak
docker compose up -d
```

Keycloak runs at `http://localhost:8080` and imports the local development realm automatically on a
fresh start. The import creates the SPA and BFF clients, `testuser`, and the sample realm role
`my-test-role`.

For reset/reimport steps, see [`infra/keycloak/README.md`](infra/keycloak/README.md).

Stop Keycloak with:

```powershell
docker compose down
```

## Run Backend

From the repository root:

```powershell
dotnet restore
dotnet run --project .\src\backend\Backend.csproj --launch-profile https
```

Development URLs:

- HTTPS: `https://localhost:7233`
- HTTP: `http://localhost:5184`

Useful endpoint:

- `GET https://localhost:7233/api/public/ping`

Backend OIDC settings live in:

- `src/backend/appsettings.json`
- `src/backend/appsettings.Development.json`

`Starter:FrontendOrigin` controls the allowed frontend origin for CORS and the redirect target
after BFF login/logout.

## Run Frontend

From the repository root:

```powershell
cd .\src\frontend
npm install
npm start
```

The frontend runs at `http://localhost:4200`.

During local development, Angular uses `proxy.conf.json` to forward `/api` calls to
`https://localhost:7233`.

Frontend auth settings live in:

- `src/frontend/src/environments/environment.development.ts`
- `src/frontend/src/environments/environment.ts`
- `src/frontend/src/environments/environment.production.ts`

`authMode` selects `spa` or `bff`. `apiOrigin` is normally empty in development so calls use the
Angular dev-server proxy.

## Public Demo Deployment

For the public HTTPS demo topology, public-demo frontend build, backend configuration, provider
callbacks, and minimum availability check, see
[Public Demo Deployment](docs/public-demo-deployment.md).

## Published Packages

The reusable package sources live in this repository and are also published for external use:

- NuGet: [`OidcStarter.AspNetCore.Bff`](https://www.nuget.org/packages/OidcStarter.AspNetCore.Bff/)
- npm: [`@flying-bee/oidc-starter-auth`](https://www.npmjs.com/package/@flying-bee/oidc-starter-auth)

The local sample apps still consume the in-repository projects so the package sources remain easy to
develop and verify alongside the sample.

## License

OIDC Starter core packages and repository content are licensed under the MIT License. See
[LICENSE](LICENSE).

## Backend Package Coverage

The backend package has focused automated tests for its core reusable behavior:

- default flat role mapping
- custom role mapper composition and role claims transformation
- current-user role projection
- login/logout/current-user/session-state behavior
- CSRF origin validation
- antiforgery token issuing, request validation, and unsafe endpoint protection
- unauthorized and forbidden API behavior
- package/sample build compatibility evidence
- package service and authorization policy registration

Run them from the repository root:

```powershell
dotnet test .\src\OidcStarter.AspNetCore.Bff.Tests\OidcStarter.AspNetCore.Bff.Tests.csproj
```

## Test SPA Mode

1. In `src/frontend/src/environments/environment.development.ts`, set `authMode` to `spa`.
2. Start local Keycloak with the imported realm. See
   [`infra/keycloak/README.md`](infra/keycloak/README.md) for the
   realm, client, test user, and local development credentials created automatically.
3. Start the frontend with `npm start`.
4. Open `http://localhost:4200`.
5. Use the login button and sign in through Keycloak.

Expected result: the frontend shows the authenticated user claims from Keycloak. Backend ping can
still be used as a public connectivity check.

## Test BFF Mode

1. In `src/frontend/src/environments/environment.development.ts`, set `authMode` to `bff`.
2. Start local Keycloak with the imported realm. See
   [`infra/keycloak/README.md`](infra/keycloak/README.md) for the
   realm, clients, test user, local development credentials, and the imported BFF client secret
   that matches `src/backend/appsettings.Development.json`.
3. Start the backend with the HTTPS launch profile.
4. Start the frontend with `npm start`.
5. Open `http://localhost:4200`.
6. Use the login button and sign in through Keycloak.

Expected result: the backend completes the OIDC flow, sets the local session cookie, and
`/api/auth/me` returns the current user and mapped roles to the frontend. With the imported local
realm, `testuser` has `my-test-role` assigned automatically.

## Current Release Readiness

`OidcStarter.AspNetCore.Bff` `v1.0.1` is a hardening validation patch release.

- Breaking changes: none.
- Public API changes: none.
- Runtime behavior changes: none.
- Audit-driven validation added focused coverage for login/logout/current-user/session-state
  behavior, antiforgery token issuing and request validation, unsafe endpoint protection,
  unauthorized/forbidden API behavior, and package/sample build compatibility.
- Final handoff validation passed the BFF test project with 24/24 tests and an audit smoke with
  0 findings.
- SPA mode is retained as a reference flow; BFF mode remains the recommended starter path for
  applications that want server-side token handling.

## More Notes

See [`docs/architecture.md`](docs/architecture.md) for a short overview of the two auth modes,
security assumptions, antiforgery contract, and role-mapping approach.
