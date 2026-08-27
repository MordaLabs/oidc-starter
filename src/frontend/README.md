# OIDC Starter sample frontend

This Angular application is the repository sample and public-demo UI for OIDC Starter. It demonstrates the reusable `@flying-bee/oidc-starter-auth` package against the sample ASP.NET Core backend; it is not a standalone authentication product.

## Authentication modes

- **BFF mode** is the default production-oriented sample path. The browser uses a BFF session and the UI loads the normalized current user from `/api/auth/me`.
- **SPA mode** remains a supported reference flow for direct browser OIDC with PKCE.

The active mode and development settings are in `src/environments`. BFF mode normally leaves `apiOrigin` empty so the Angular development proxy forwards relative `/api` requests to the local backend.

## External Login Providers in the sample

In BFF mode, the sample asks `@flying-bee/oidc-starter-auth` for `GET /api/auth/providers` after it determines that no session is active. It renders the configured runtime provider choices and starts the chosen login through `login(provider.id)`. The backend configuration, not the frontend, determines which providers appear.

The sample keeps a default-login path for the existing single OpenID Connect flow. Google, GitHub, Facebook, and custom providers appear only when the backend has registered and enabled them.

## Run locally

1. Start the local identity provider described in the [Keycloak guide](../../infra/keycloak/README.md).
2. Start the sample backend with its HTTPS launch profile.
3. From this directory, run `npm install` and `npm start`.
4. Open `http://localhost:4200`.

For the repository overview and BFF setup, see the [root README](../../README.md). For the reusable Angular API, see the [frontend package README](projects/oidc-starter-auth/README.md). The BFF and SPA boundaries are described in the [architecture note](../../docs/architecture.md).