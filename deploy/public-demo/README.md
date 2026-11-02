# Public Demo Docker Deployment

This package builds the existing Angular `public-demo` configuration and the ASP.NET Core BFF sample as separate containers. Host-level Caddy retains ports 80/443, TLS, and the public hostname; Docker publishes only loopback ports for Caddy to reach.

## Prepare configuration

From this directory, create an untracked deployment configuration and replace the OIDC secret placeholder:

```sh
cp .env.example .env
```

Required non-secret settings are `PUBLIC_DEMO_HOST`, the loopback ports, the fixed Docker subnet/gateway, `OIDC_AUTHORITY`, and `OIDC_CLIENT_ID`. `OIDC_CLIENT_SECRET` is required and must remain only in the local `.env` or the operator's secret-management path.

Google, Facebook, and GitHub are optional. Set a provider's `*_ENABLED` value to `true` only with its corresponding client/app credentials. Register the public callback paths for every enabled provider. The template retains the package default login provider (`oidc`); do not hardcode an environment-specific hostname or provider set into the reusable template.

## Frontend build selection

`PUBLIC_DEMO_FRONTEND_CONFIGURATION=public-demo` is the safe default in
`.env.example` and when the variable is absent. It builds a BFF frontend with
both invitation interfaces disabled. The frontend Dockerfile accepts only the
exact build configurations `public-demo` and `public-demo-acceptance`; an empty
or other value fails the image build. From `src/frontend`, the corresponding
local builds are `npm run build:public-demo` and
`npm run build:public-demo-acceptance`.

Select `PUBLIC_DEMO_FRONTEND_CONFIGURATION=public-demo-acceptance` only in a
separately isolated staging deployment for P5-A8b. It compiles both invitation
interfaces into the BFF UI but grants no operator access. The backend workflow,
Google enforcement, GitHub enforcement, and exact Keycloak operator allowlist
remain independent runtime controls. Changing the Compose variable requires a
new frontend image build; it cannot change an already running Angular bundle.
Do not deploy an acceptance build to the public demo before P5-A8b acceptance.

The Compose project name remains `oidc-starter-public-demo`. A different
hostname alone does not isolate its Docker volumes and other resources. Use a
separate host or an explicitly different Compose project name with separate
volumes, configuration, ports, and bridge network for acceptance staging. Do
not build or run the acceptance image against the existing production project.

## Build and run

```sh
sudo docker compose --env-file .env config
sudo docker compose --env-file .env build
sudo docker compose --env-file .env up -d
```

The frontend binds to `127.0.0.1:8082` and the backend to `127.0.0.1:8081` with the example values. Adjust the oidc-starter loopback port values in `.env` only if those host ports conflict with other host services.

## Optional invitation database (disabled by default)

Compose mounts the named volume `backend-invitations` only into the backend at
`/var/lib/oidc-starter/invitations`. The fixed database path is
`/var/lib/oidc-starter/invitations/invitations.db`; the mount provides its parent
directory. This volume is separate from `backend-data-protection-keys`. No
invitation switch is enabled by the tracked `.env.example`. Google/GitHub
provider enablement does not enable invitation enforcement.

The backend image has `ENTRYPOINT ["dotnet", "Backend.dll"]`. After reviewing
the real untracked `.env`, building the image under separate approval, and
confirming the three invitation switches remain `false`, provision once from
this directory with the same backend image, service configuration, named volume,
and container identity used at runtime:

```sh
docker compose --env-file .env run --rm --no-deps backend --provision-demo-invitations
```

This passes the exact argument to the existing entrypoint. It exits without
starting HTTP and does not run during `compose up`. Do not add `--user`, change
the volume, or set a different database path for the one-shot command. A missing
or inaccessible database makes enabled invitation operations fail closed.

The Dockerfile does not set `USER`; determine the effective UID/GID of the
built image in staging before relying on ownership assumptions. Verify that
this identity can create and reopen the database and create SQLite journal or
`-wal`/`-shm` sidecars in the mounted directory. Keep access limited to the
backend identity; avoid broad permissions such as `chmod 777`. A read-only or
unwritable volume prevents provisioning or normal invitation operations.

### Controlled activation sequence

1. Prepare and review the untracked `.env`, keeping workflow, Google enforcement,
   and GitHub enforcement `false`. Leave the operator pair empty until verified.
2. Validate the Compose configuration with `docker compose --env-file .env config --quiet`.
3. Back up current deployment configuration and Data Protection keys; back up
   any existing invitation database before the first activation.
4. Build the backend image only after separate deployment approval.
5. Run the one-shot provisioning command above; check its zero exit status.
6. Verify the mounted database, effective permissions, and persistence across a
   controlled backend container replacement in staging. Take a verified backup
   of the newly provisioned database before changing flags.
7. Obtain a private operator's exact validated Keycloak issuer and raw subject
   from trusted Keycloak configuration and account records. Set both allowlist
   variables; email, display name, and role are not substitutes.
8. Select the acceptance frontend build only in isolated staging and complete
   P5-A8b security acceptance.
9. Only then review whether to activate workflow and provider enforcement
   publicly. Back up the database again before changing enforcement flags.

### Backup, restore, and rollback

Back up the invitation volume before schema-affecting upgrades and flag changes.
Stop only this project's backend service and any one-shot writer; confirm no
other process is writing the volume. Use a trusted SQLite-aware backup utility
to create a consistent copy outside the volume, with restricted access. SQLite
may have `-wal` and `-shm` sidecars: do not copy only the main file while it is
live or changing. If the required backup tooling or volume access is unavailable,
do not proceed with activation. Verify the backup opens and passes SQLite
integrity and foreign-key checks; retain a tested restore procedure.

Restore only while the backend and all other database users are stopped. Place
the verified backup at the same path in the same named volume, preserve the
runtime identity's permissions, verify its schema with the one-shot provisioner,
and then restart the backend. A stale restore can erase approvals, bindings,
requests, and revocations, so review that security impact before replacing newer
data. Application rollback should preserve the current invitation volume and
the separate Data Protection key volume. Do not use `docker compose down -v`,
global pruning, or cleanup that touches co-hosted services.

The Compose project name is fixed at `oidc-starter-public-demo`, so its named
volumes have project-scoped identities. Use a separate host for staging, or an
explicit and consistently applied different Compose project name (for example,
`--project-name oidc-starter-staging`) with separate `.env`, host ports, bridge
subnet/gateway, and public host. Never run staging against the production
project's invitation volume or Data Protection keys.

## Host Caddy

Copy the site block from `Caddyfile.example` into the host Caddy configuration, replacing `demo.example.com` if needed. Caddy routes `/api/*`, `/signin-oidc`, `/signout-callback-oidc`, `/signin-google`, `/signin-facebook`, and `/signin-github` to the backend; every other request goes to the Angular frontend.

The frontend container serves static Angular assets only, so its catch-all proxy removes the browser `Cookie` header. BFF authentication cookies continue to reach backend and callback routes, while the static server avoids receiving unnecessary session cookies and oversized request headers.

## Premium product-page demo route

Static Premium product CTAs use `/premium/demo`. The frontend nginx serves a
request-access page at that route by default; the future public demo hostname
is not configured as a redirect. Trailing slashes on `/premium`,
`/premium/sessions`, and `/premium/demo` redirect to host-relative paths, so
staging requests remain on the staging product-site host.

On the staging **product-site** Caddy host `demo.yaroo.ovh`, the operator can
add this route before forwarding other paths to the static frontend:

```caddyfile
@premiumDemo path /premium/demo /premium/demo/
redir @premiumDemo https://premium-demo.yaroo.ovh/ 302
```

This fragment belongs in that host's existing site block, not in the
`Caddyfile.example` block for the OSS demo host. The production product-site
host keeps the request-access page until P5B0C has deployed and verified the
separate public Premium demo, then its operator can configure the corresponding
host-managed redirect. Do not point production at an unavailable demo host.

Caddy supplies forwarded headers to the backend. The Compose network fixes its gateway at `FORWARDED_PROXY_IP`, and the backend trusts forwarded headers only from that gateway. Keep the subnet and gateway settings aligned; do not broaden this trust to arbitrary proxy networks.

## Verify and stop

```sh
curl --fail http://127.0.0.1:8081/api/public/ping
curl --fail http://127.0.0.1:8081/api/auth/providers
sudo docker compose --env-file .env down
```

For public operation, verify `https://<public-host>/api/public/ping` and `https://<public-host>/api/auth/providers` through host Caddy. The providers response is the runtime source of truth for the enabled provider chooser options. Configure the identity provider with `https://<public-host>/signin-oidc` plus the callback paths for enabled social providers.

OIDC sessions use the configured remote sign-out flow. Social-provider sessions clear the local BFF session only; the deployment does not request remote social-provider logout.
