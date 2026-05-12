# Keycloak

Local Keycloak runs on `http://localhost:8080` and is used by both auth modes.

This setup is for local development only. It imports a checked-in realm definition on startup.

## Start With Realm Import

Start from `infra/keycloak`:

```bash
docker compose up -d
```

The compose file starts Keycloak with `start-dev --import-realm` and mounts:

- `infra/keycloak/realm-import/oidc-starter-realm.json`

Admin login:

- Username: `admin`
- Password: `admin`

## Created Automatically

On a fresh start, the import creates:

- Realm: `oidc-starter`
- Client: `oidc-starter-spa`
- Client: `oidc-starter-bff`
- Realm role: `my-test-role`
- Test user: `testuser`, with `my-test-role` assigned automatically

Local development credentials created by the import [LOCAL DEVELOPMENT ONLY - DO NOT REUSE IN REAL ENVIRONMENTS]:

- Keycloak admin: `admin` / `admin`
- Test user: `testuser` / `test123`
- BFF client secret: `ugVtPBJyldMnVuk38kxDZ9EYDwLwfBkg`

These credentials are intentionally fixed and checked in for local development only.

## Matching Local App Configuration

The imported clients are aligned with the current app settings:

- SPA redirect URI: `http://localhost:4200/*`
- SPA web origin: `http://localhost:4200`
- BFF redirect URIs: `https://localhost:7233/signin-oidc`, `https://localhost:7233/signout-callback-oidc`
- BFF client secret matches `src/backend/appsettings.Development.json`

## Manual Adjustment

Normally nothing else should be required for a clean local start.

You may still need manual adjustment if:

- you changed frontend/backend local URLs or ports
- you changed the backend callback path
- you changed the BFF client secret in app configuration
- you already have an existing `oidc-starter` realm from an older manual setup

The imported `my-test-role` role is included so BFF mode can demonstrate role mapping through
`/api/auth/me` without manual Keycloak setup. It is local sample data only.

## Retest From Scratch

If you want to retest from a clean local state, from `infra/keycloak` run:

```bash
docker compose down --remove-orphans
```

Then start again:

```bash
docker compose up -d
```

If you already imported an older realm before `my-test-role` existed, remove the existing container
explicitly and start again:

```bash
docker rm -f oidc-starter-keycloak
docker compose up -d
```

Stop:

```bash
docker compose down
```
