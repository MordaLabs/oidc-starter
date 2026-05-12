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

On startup, the BFF view calls `GET /api/auth/me` once to discover whether the browser still has a
valid backend session. A `401 Unauthorized` response, any failed refresh, or a response that is not
authenticated clears the frontend user state and shows the logged-out UI. Logout also clears
frontend state before posting to the backend logout endpoint, then the backend completes the OIDC
sign-out redirect.

This is the main local-development flow at the current stable point.

## Configuration Shape

- Frontend mode and OIDC client settings live in `src/frontend/src/environments`.
- Backend OIDC client settings live in `src/backend/appsettings*.json`.
- `Starter:FrontendOrigin` is the backend's trusted frontend origin for CORS and post-login/logout
  redirects.
- `Starter:AllowedForwardedHosts` lists host names the backend will accept from `X-Forwarded-Host`
  when it is behind a trusted reverse proxy.
- `apiOrigin` is the frontend API origin. Leave it empty when using the Angular proxy in local
  development.

## BFF Security and Deployment Assumptions

The BFF session is represented by an HTTP-only, secure `__Host-` cookie. The local setup keeps
`SameSite=None` so the current development proxy and optional direct backend calls continue to work;
production deployments should prefer serving the frontend and backend through one public site so
the cookie does not need to support broad cross-site use.

The backend reads `X-Forwarded-For`, `X-Forwarded-Proto`, and `X-Forwarded-Host` before HTTPS
redirection. ASP.NET Core still only trusts known proxy networks by default; production hosting
should configure the actual trusted proxy addresses and replace `Starter:AllowedForwardedHosts`
with the public backend host names.

`POST /api/auth/logout` rejects requests whose `Origin` or `Referer` is not the configured frontend
origin or the current backend origin. The BFF package also exposes `GET /api/auth/csrf`, which issues
an antiforgery request token through the `XSRF-TOKEN` cookie. Frontends must send that token on
cookie-authenticated state-changing BFF requests: use the `X-XSRF-TOKEN` header for fetch/XHR calls,
or the `__RequestVerificationToken` form field for top-level form posts such as OIDC logout.
