# Public Demo Docker Deployment

This package builds the existing Angular `public-demo` configuration and the ASP.NET Core BFF sample as separate containers. Host-level Caddy retains ports 80/443, TLS, and the public hostname; Docker publishes only loopback ports for Caddy to reach.

## Prepare configuration

From this directory, create an untracked deployment configuration and replace the OIDC secret placeholder:

```sh
cp .env.example .env
```

Required non-secret settings are `PUBLIC_DEMO_HOST`, the loopback ports, the fixed Docker subnet/gateway, `OIDC_AUTHORITY`, and `OIDC_CLIENT_ID`. `OIDC_CLIENT_SECRET` is required and must remain only in the local `.env` or the operator's secret-management path.

Google, Facebook, and GitHub are optional. Set a provider's `*_ENABLED` value to `true` only with its corresponding client/app credentials. Register the public callback paths for every enabled provider. The template retains the package default login provider (`oidc`); do not hardcode an environment-specific hostname or provider set into the reusable template.

## Build and run

```sh
sudo docker compose --env-file .env config
sudo docker compose --env-file .env build
sudo docker compose --env-file .env up -d
```

The frontend binds to `127.0.0.1:8082` and the backend to `127.0.0.1:8081` with the example values. Adjust the oidc-starter loopback port values in `.env` only if those host ports conflict with other host services.

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
