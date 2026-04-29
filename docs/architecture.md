# Architecture Note

This project intentionally keeps two authentication modes side by side.

## SPA Mode

In `spa` mode, the Angular application talks directly to Keycloak with Authorization Code Flow
and PKCE. The browser receives the OIDC result and the frontend displays user claims.

This mode is complete and kept as a learning/reference path because it makes the browser-side OIDC
flow visible.

## BFF Mode

In `bff` mode, the Angular application calls backend auth endpoints:

- `GET /api/auth/login`
- `GET /api/auth/me`
- `POST /api/auth/logout`

The ASP.NET Core backend handles the OIDC redirect flow with Keycloak, stores tokens server-side,
and represents the browser session with an HTTP-only cookie. The frontend only asks the backend who
the current user is.

This is the main local-development flow at the current stable point.

## Configuration Shape

- Frontend mode and OIDC client settings live in `src/frontend/src/environments`.
- Backend OIDC client settings live in `src/backend/appsettings*.json`.
- `Starter:FrontendOrigin` is the backend's trusted frontend origin for CORS and post-login/logout
  redirects.
- `apiOrigin` is the frontend API origin. Leave it empty when using the Angular proxy in local
  development.
