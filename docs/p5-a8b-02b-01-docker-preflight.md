# P5-A8b-02B-01 Docker and staging isolation preflight

**Scope:** read-only local inventory and tracked configuration review on 2026-11-02 at 16:55 UTC. No Docker acceptance, image build, container operation, database provisioning, backup, restore, remote-host access, or public activation occurred. **Public release decision: NO-GO.** The canonical acceptance matrix remains unchanged.

## Baseline and environment

| Item | Evidence and classification |
| --- | --- |
| Repository guard | **MEASURED:** `oidc-starter`, branch `docs/p5-a8b-02b-01-docker-preflight`, HEAD `eb26aa096da8c2fa151e468fa8fc4b7d06f68cdd`, initially clean worktree. |
| Local OS and tools | **MEASURED:** Microsoft Windows 10.0.26100; Docker CLI 29.7.2; Docker Compose v5.4.0. |
| Selected context | **MEASURED:** `desktop-linux`; Docker endpoint classified as a local socket, not SSH/TCP; no `DOCKER_HOST` override. The endpoint address was not retained. |
| Engine | **MEASURED:** local Docker Desktop Linux engine available, server 29.7.2. Restricted inspection first returned a permission error; the permitted read-only local inspection succeeded with engine access. |
| Static Compose validation | **MEASURED:** `docker compose --project-name oidc-starter-a8b-preflight --env-file .env.example -f compose.yaml config --quiet` exited 0 with no output, from `deploy/public-demo`. Only tracked `.env.example` was used. |

## Bounded local resource inventory

**MEASURED:** read-only `docker info` and name-only `docker ps -a`, `docker volume ls`, and `docker network ls`, plus candidate Compose project-label filters, found 5 containers, 5 volumes, and 4 networks on this local engine. No container, volume, or network name began with the proposed `oidc-starter-a8b-staging` project prefix; no such resource carried its Compose project label. There were also no name matches for the tracked public project prefix. No individual existing resource names, container environments, mounts, secret values, or application data were recorded.

**UNKNOWN:** the local inventory does not establish whether a future staging host is the same engine, whether a chosen bridge subnet overlaps an existing network, whether future loopback ports are free, or whether other host processes use them. The absence of a candidate-name collision is a narrow current observation, not staging isolation PASS. Recheck identity, names, labels, subnet, and ports immediately before any later resource creation.

## Tracked Compose and image contract

**SOURCE-VERIFIED:** `compose.yaml` declares the fixed default project name `oidc-starter-public-demo`; a consistently supplied CLI `--project-name` must override it for staging. A hostname change alone leaves the Compose project, resource names, ports, volumes, network, and secrets potentially shared. The backend mounts separate logical volumes `backend-invitations` and `backend-data-protection-keys`; project-scoped names under the proposed staging project would be `oidc-starter-a8b-staging_backend-invitations` and `oidc-starter-a8b-staging_backend-data-protection-keys`. The proposed project-scoped bridge name would be `oidc-starter-a8b-staging_public-demo`. These are **PROPOSED names only**, not created resources.

**SOURCE-VERIFIED:** frontend and backend published ports bind to `127.0.0.1`; the bridge subnet and forwarded proxy gateway come from configuration and must agree with the trusted-proxy setting. The tracked example is illustrative and grants no approved staging allocation. Workflow, Google enforcement, and GitHub enforcement default false; the operator issuer/subject defaults are empty. Provider enablement is separate. The frontend build argument defaults to `public-demo`; its Dockerfile accepts only exact `public-demo` and `public-demo-acceptance` values, rejecting all others. The acceptance UI requires explicit build selection and must never target the public Compose project. The backend Dockerfile does not set `USER`, so effective UID/GID and invitation-volume write/SQLite sidecar permissions remain unverified.

**MEASURED only for syntax:** `config --quiet` accepted the tracked example with the preflight project override. It did not prove resolved staging secret quality, port availability, network separation, volume ownership, image behavior, or live deployment state. No real untracked `.env` was opened or created.

## Proposed isolated topology for later operator review

| Resource | Proposal and unresolved allocation |
| --- | --- |
| Host and engine | **PROPOSED:** operator selects and records an isolated host/engine identity; current local Docker Desktop is only the inspected preflight engine. Future host choice and identity are **TBD**. |
| Compose project | **PROPOSED:** `oidc-starter-a8b-staging`, passed on every future Compose command. Current local name/label collision check is clear; recheck on the selected execution host. |
| Frontend/backend loopback ports | **TBD:** two distinct, operator-approved free host ports, separate from public and other local services; preserve `127.0.0.1` binds. |
| Bridge subnet and forwarded proxy gateway | **TBD:** nonoverlapping subnet and gateway selected together after network inventory; gateway must match the trusted forwarded-proxy configuration. |
| Named volumes | **PROPOSED:** project-scoped invitation and Data Protection volumes named above, both distinct from public resources. Existence and labels must be rechecked before creation. |
| Environment and secrets | **TBD:** separately managed, untracked staging-only configuration and credentials; reviewed privately without printing expanded secret values. No file was created in this preflight. |
| Frontend/backend images | **TBD:** separate staging tags and recorded image digests from approved builds; public-demo and acceptance frontend variants must have distinct identities. No image was built here. |
| Disposable identities | **TBD:** staging-only Keycloak/operator and Google/GitHub test accounts with separately registered callbacks; HTTPS hostname, Caddy site, provider credentials, and callbacks are not assumed to exist. |
| Backup/restore | **TBD:** reserved restricted backup destination, independent restore target, SQLite-aware tooling, reviewer, and rollback owner; no target was accessed. |
| Cleanup boundary | **PROPOSED:** only resources positively identified as belonging to the approved staging project, after evidence and backup review; preserve public and unrelated projects and both persistent staging volumes until an explicit later cleanup decision. No cleanup command is proposed or run here. |

## STOP gates and next checkpoint

**BLOCKED for Docker acceptance until operator review:** stop if the selected Docker context is remote or unverified; if project labels/names or either named volume collide; if the chosen bridge subnet/gateway overlaps an existing network; if either loopback port is occupied; if staging configuration, credentials, host identity, backup destination, or restore target is missing or unreviewed; if any resource could overlap production; or if an acceptance image would build or run under `oidc-starter-public-demo`. An unresolved collision is **UNKNOWN/BLOCKED**, not a pass. The current local name check does not clear the subnet, port, or future-host gates.

**Next operator approval:** review and authorize the exact isolated host, project override, private configuration, resource allocations, image identities, disposable accounts, backup/restore target, and stop/cleanup boundary before P5-A8b-02B-02. That next checkpoint should exercise A-03/A-04 and C-01 through C-04: frontend Docker default/acceptance builds and invalid build-argument rejection, backend image build, effective UID/GID, one-shot SQLite provisioning, schema/storage checks, and named-volume persistence. Subsequent isolated staging work must verify restart/replacement durability, consistent backup, restore to a separate target, and rollback without losing revocation or Data Protection state.

**NOT EXECUTED future command examples** (placeholders require operator-reviewed allocations and configuration; the same project override is mandatory throughout):

```sh
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml config --quiet
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml build backend
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml build frontend
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml build --build-arg FRONTEND_BUILD_CONFIGURATION=public-demo-acceptance frontend
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml build --build-arg FRONTEND_BUILD_CONFIGURATION=invalid frontend
docker compose --project-name oidc-starter-a8b-staging --env-file <reviewed-staging-env> -f compose.yaml run --rm --no-deps backend --provision-demo-invitations
```

These examples are not authorization to execute Docker or provision storage. The invalid frontend value is expected to fail in the future test. **Current result: isolation readiness BLOCKED on unresolved allocations; public activation NO-GO.**
