# OIDC Starter

OIDC Starter is a small learning project for comparing two OpenID Connect authentication
patterns with Angular, ASP.NET Core, and a local Keycloak identity provider.

The repository contains:

- `src/frontend`: Angular UI with selectable auth mode
- `src/backend`: ASP.NET Core Web API and BFF auth endpoints
- `infra/keycloak`: local Keycloak Docker Compose setup
- `docs`: lightweight project notes

## Supported Auth Modes

| Mode | Status | Description |
| --- | --- | --- |
| `spa` | Complete learning/reference mode | The Angular app signs in directly with Keycloak using Authorization Code Flow with PKCE. |
| `bff` | Working end-to-end in local development | The Angular app delegates login/logout/user lookup to the ASP.NET Core backend, which keeps the OIDC tokens server-side behind a cookie session. |

The development frontend currently defaults to `bff` mode in
`src/frontend/src/environments/environment.development.ts`.

## Prerequisites

- .NET SDK 9
- Node.js and npm
- Docker Desktop or another Docker Compose compatible runtime
- A local Keycloak realm named `oidc-starter`

The local app configuration expects these Keycloak clients:

- `oidc-starter-spa` for SPA mode
- `oidc-starter-bff` for BFF mode

## Run Keycloak

From the repository root:

```powershell
cd .\infra\keycloak
docker compose up -d
```

Keycloak runs at `http://localhost:8080`.

The local admin account from `docker-compose.yml` is:

- Username: `admin`
- Password: `admin`

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

## Test SPA Mode

1. In `src/frontend/src/environments/environment.development.ts`, set `authMode` to `spa`.
2. Ensure Keycloak is running and the `oidc-starter-spa` client allows:
   - Redirect URI: `http://localhost:4200/*`
   - Web origin: `http://localhost:4200`
3. Start the frontend with `npm start`.
4. Open `http://localhost:4200`.
5. Use the login button and sign in through Keycloak.

Expected result: the frontend shows the authenticated user claims from Keycloak. Backend ping can
still be used as a public connectivity check.

## Test BFF Mode

1. In `src/frontend/src/environments/environment.development.ts`, set `authMode` to `bff`.
2. Ensure Keycloak is running and the `oidc-starter-bff` client matches
   `src/backend/appsettings.Development.json`.
3. Start the backend with the HTTPS launch profile.
4. Start the frontend with `npm start`.
5. Open `http://localhost:4200`.
6. Use the login button and sign in through Keycloak.

Expected result: the backend completes the OIDC flow, sets the local session cookie, and
`/api/auth/me` returns the current user to the frontend.

## Current Gaps Before Production-Hardening

- Keycloak realm/client setup is not automated or imported by Docker Compose yet.
- BFF cookie, CORS, and HTTPS settings are local-development oriented.
- Client secrets are stored in development config and should move to user secrets or a secret store.
- No roles, authorization policies, automated tests, packaging, or deployment setup are included yet.
- SPA mode is retained for learning/reference, not as the preferred production shape for this starter.

## More Notes

See `docs/architecture.md` for a short overview of how the two auth modes differ.
