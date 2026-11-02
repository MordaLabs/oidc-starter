# P5-A8b invitation workflow acceptance plan

**Status: NO-GO for public activation.** This is an inventory and proposed test plan, not execution evidence. Baseline: `f404b9a87c88364045c83618def14d400d5b986d` on `docs/p5-a8b-01-acceptance-plan`. P5-A8-01 through P5-A8-04 are merged. Every matrix case below is **NOT RUN** in P5-A8b; prior focused test coverage is **PREVIOUSLY REPORTED**, never a staging PASS.

## Scope and implementation inventory

The following are verified from tracked source and templates. They do not establish the state of any running host.

| Component | Verified repository contract |
| --- | --- |
| `deploy/public-demo/compose.yaml` and `.env.example` | Fixed project name `oidc-starter-public-demo`; loopback-only frontend/backend ports on one bridge network; `backend-invitations` at `/var/lib/oidc-starter/invitations` and separate `backend-data-protection-keys` at `/root/.aspnet/DataProtection-Keys`. Database path is `/var/lib/oidc-starter/invitations/invitations.db`. Workflow and both enforcement switches default false; operator issuer/subject default empty. Provider enablement is independent. The frontend build argument defaults to `public-demo`. Secrets belong in untracked operator configuration. |
| `deploy/public-demo/frontend.Dockerfile` | Node build uses an exact `public-demo` / `public-demo-acceptance` allowlist and exits nonzero for other build arguments; nginx serves the resulting static output. The normal Angular build has both invitation UI flags false; the acceptance build has both true, BFF mode, and relative same-origin API calls. These flags are compile-time UI controls, not authorization. |
| `deploy/public-demo/backend.Dockerfile` | Publishes the sample backend with `ENTRYPOINT ["dotnet", "Backend.dll"]`. It does not set `USER`; effective runtime UID/GID and mounted-directory write access require staging verification. |
| `deploy/public-demo/Caddyfile.example` | Sends `/api/*` and default OIDC/social callback paths to the backend. The static frontend catch-all removes the incoming Cookie header. Actual TLS, forwarded headers, and callback routing must be verified on the isolated host. |
| `deploy/public-demo/README.md` and `docs/public-demo-deployment.md` | Describe one-shot provisioning, safe defaults, separate staging resources, consistent SQLite backup, and restoration while writers are stopped. They are procedures, not proof that a backup or restore succeeded. |
| `src/backend/Program.cs` | Exact provisioning mode branches before normal service setup. Workflow routes are mapped only when `DemoInvitations:Workflow:Enabled` is true; Google and GitHub gate flags are independent. Public Keycloak login remains available. |
| `src/backend/Auth/InvitationDatabaseProvisioning.cs` | Exact sole `--provision-demo-invitations` argument uses the configured absolute path and exits. Invalid arguments/path return 2; storage/schema failure returns 1; verified success returns 0. No HTTP host is started by this mode. |
| `src/backend/Auth/SqliteInvitationStore.cs` | Provisioning alone uses `ReadWriteCreate`; runtime uses `ReadWrite`. The provisioner checks the complete user-defined schema, `quick_check(1)`, and foreign keys in a transaction. Unique pending-request, redeemable-email, and provider-subject constraints exist. Operator approval gives first redemption seven days from one UTC timestamp. Expired unredeemed slots can be reissued; bindings survive expiry but not revocation. Historical NULL expirations are retained. |
| `src/backend/Auth/InvitationRequestEndpoint.cs` | `POST /api/demo/invitation-requests` exists only with workflow enabled. Trusted Origin, JSON type, 1024-byte bound, and antiforgery precede storage; valid and duplicate requests return neutral 202. IP-partitioned fixed-window limit is 10 per 10 minutes; storage failure is 503. |
| `src/backend/Auth/OperatorWorkflowEndpoint.cs` | Cookie-protected GET list and POST approve/reject/revoke under `/api/demo/operator`. Access requires exact validated OIDC issuer and raw subject, matching ticket metadata/client; email/roles/social sessions do not grant access. Mutations require empty bodies, trusted Origin, and antiforgery. Responses distinguish 204, 400, 403, 404, 409, and 503. |
| `src/backend/Auth/InvitationSessionValidation.cs` | Enforced Google/GitHub cookies are checked against SQLite on normal requests and rejected/deleted when binding is absent, revoked, or storage is unavailable. The exact POST `/api/auth/logout` exception permits structurally valid external tickets to complete local logout. Keycloak behavior remains separate. |
| Focused backend and frontend tests | Provisioning, request intake, operator workflow, store races and expiry, real-handler callbacks with fake providers, session revocation, and UI configuration have focused tests. Their prior reported results do not replace the P5-A8b runs below. |

**Unverified assumptions:** an isolated host or project exists; effective image identity can write the named volume and SQLite sidecars; provider test credentials/accounts and callback registrations are available; the real Caddy/TLS path preserves required cookies and trusted forwarding; a consistent backup and restore tool is available; the operator issuer/subject pair is independently confirmed. No real `.env`, secret, production database, or production host was inspected for this plan.

## Preconditions and evidence rules

Use only disposable staging identities and data. Prefer a separate host. If sharing a Docker host, override the fixed Compose project name consistently on **every** future Compose command, and use a distinct hostname, backend/frontend loopback ports, bridge subnet/gateway, named volumes, untracked environment file, test identities, and secrets. A hostname change alone is insufficient. Record the resolved project and volume names before any staging action; abort if either invitation or Data Protection volume resolves to a production resource. Keep the acceptance build off the public project.

Before P5-A8b-02, the operator must confirm host ownership and isolation, available capacity and ports/subnet, effective backend UID/GID, private operator identity source, disposable Google/GitHub accounts and callbacks, trusted TLS/proxy configuration, backup tool and restore target, and a rollback owner. Do not print or attach credentials, cookies, tokens, email lists, or private identifiers. Evidence is a restricted, redacted package with UTC time, commit/image digest, project identity, exact test ID, expected/actual result, reviewer, and sanitized logs or screenshots. Capture aggregate database counts and opaque IDs only when needed.

Phase tags: **AUT** = automated local check; **LDO** = isolated local Docker check; **STG** = isolated HTTPS staging integration; **EXT** = manual external identity-provider interaction. The procedure is a proposal for the named later phase. `NOT RUN` means no P5-A8b execution evidence exists.

## Acceptance matrix

### A. Docker images

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| A-01 | Safe frontend default | AUT; clean checkout | Run `npm run build:public-demo`; inspect environment assertions and Angular mapping. | BFF, same-origin API, both invitation UI flags false. | Build result, test summary, configuration excerpt. | BLOCKER | NOT RUN |
| A-02 | Opt-in acceptance bundle | AUT; clean checkout | Run `npm run build:public-demo-acceptance` and focused UI tests. | BFF, same-origin API, both interfaces enabled; default build untouched. | Build result, test summary. | HIGH | NOT RUN |
| A-03 | Docker build allowlist | LDO; isolated build context | Build with default, exact acceptance, then invalid values including empty, `development`, and typo. | Only two exact values build; invalid values exit nonzero before Angular build selection. | Build-argument and exit-code log, no secrets. | BLOCKER | NOT RUN |
| A-04 | Image selection is build-time | LDO; two isolated image tags | Compare image build settings and served UI after changing only container environment. | Running bundle does not change until a separately selected image is rebuilt. | Image digests, UI screenshots without session data. | HIGH | NOT RUN |

### B. Compose isolation

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| B-01 | Distinct project identity | LDO; staging project override | Resolve Compose configuration and resource names before creation; compare with production inventory. | Staging project, containers, network, and named volumes are distinct. | Sanitized config and resource-name manifest. | BLOCKER | NOT RUN |
| B-02 | Ports and network | LDO; allocated staging values | Check resolved host bindings, subnet/gateway, and Caddy upstreams; test no collision. | Loopback ports and bridge network are isolated and reachable only as intended. | Redacted config and bind/network inspection. | BLOCKER | NOT RUN |
| B-03 | Separate persistent volumes | LDO; staging backend/frontend | Inspect mounts and recreate only the staging backend container. | Invitation and Data Protection volumes are distinct, backend-only, and survive replacement. | Mount names, before/after opaque markers. | BLOCKER | NOT RUN |
| B-04 | Environment and secret boundary | LDO; reviewed untracked staging env | Inspect resolved config without exposing values; inspect frontend assets for credential/config leakage. | Backend-only secrets; tracked example and frontend contain no real secrets. | Redacted key-presence checklist and asset check. | HIGH | NOT RUN |
| B-05 | No production resource sharing | STG; production resource inventory available read-only | Compare project, volume, network, host, port, and secret references before any write. | No staging action targets production invitation or Data Protection resources. | Signed isolation comparison. | BLOCKER | NOT RUN |

### C. Provisioning

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| C-01 | Explicit one-shot mode | LDO; flags off, fresh isolated volume | Run backend service once with exact `--provision-demo-invitations` argument and `--no-deps`; check exit and listeners. | Exit 0, expected schema, no HTTP listener or implicit flag activation. | Exit code, sanitized log, schema object list. | BLOCKER | NOT RUN |
| C-02 | Repeatability and schema rejection | LDO; disposable provisioned DB | Repeat provision; separately try an incompatible disposable schema/extra trigger. | Repeat preserves data; incompatible schema exits nonzero without repair. | Before/after counts, schema check and exit codes. | HIGH | NOT RUN |
| C-03 | Identity and write permissions | LDO; staging image/volume | Compare effective UID/GID for runtime and provisioner; create/reopen DB and exercise a transaction that needs journal or WAL sidecars. | Same identity can write safely; no broad world-writable permission. | UID/GID, mount mode, sanitized operation result. | BLOCKER | NOT RUN |
| C-04 | No startup provisioning | LDO; fresh volume, flags off | Start backend normally with configured path but no one-shot argument; inspect volume. | No database file is created by startup. | File-existence check and startup log. | HIGH | NOT RUN |
| C-05 | Missing/unwritable storage | STG; disposable DB state and recovery plan | With enabled staging workflow, test missing and read-only/unavailable volume one at a time, then restore. | No implicit DB creation; request/operator operations fail closed and external access is denied. | HTTP statuses, file-existence check, recovery result. | BLOCKER | NOT RUN |

### D. SQLite durability and recovery

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| D-01 | Backend restart | STG; disposable requests and approval | Restart only the isolated backend service; query operator list. | Requests, decisions, and invitation IDs persist. | Before/after redacted records and volume name. | BLOCKER | NOT RUN |
| D-02 | Container replacement | STG; approved and bound test identity | Replace only the backend container without deleting volumes; repeat login and protected request. | Binding and Data Protection continuity remain intact. | Image/container IDs, redacted auth results. | BLOCKER | NOT RUN |
| D-03 | Revocation durability | STG; bound disposable identity | Revoke, replace backend, then retry protected access. | Revocation persists; old cookie and new login cannot regain access. | Revocation result, before/after 401 statuses. | BLOCKER | NOT RUN |
| D-04 | Consistent backup | STG; disposable DB and restricted backup target | Stop all writers or use a SQLite-aware snapshot; back up DB, then run integrity and foreign-key checks on the copy. | Verified, consistent backup including committed WAL state; no live raw-file copy. | Backup manifest, checksum, check results. | BLOCKER | NOT RUN |
| D-05 | Isolated restore and rollback | STG; verified disposable backup and separate target volume | Keep target backend stopped, restore into isolated target, verify schema and representative requests, bindings, revocations; rehearse rollback without `down -v`. | Restored state matches snapshot; both named volumes are preserved; abort on mismatch or stale revocation state. | Restore checklist, aggregate counts, gate decision. | BLOCKER | NOT RUN |

### E. Anonymous invitation requests

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| E-01 | Disabled route | AUT and STG; workflow flag false | POST the route with otherwise valid request. | Route absent; no request or invitation saved. | HTTP status and DB counts. | HIGH | NOT RUN |
| E-02 | Valid and duplicate request | STG; workflow on, valid CSRF and Origin | Submit one address twice, varying case; inspect pending list as operator. | Both responses are indistinguishable empty 202; one pending request, no invitation. | Redacted status/body comparison, counts. | HIGH | NOT RUN |
| E-03 | Shape and size bounds | AUT; TestHost instrumented body stream | Send non-JSON, malformed JSON, extra/duplicate fields, >1024-byte Content-Length and unknown-length body. | 400; rejected oversized/form bodies are not consumed beyond the defined boundary; no persistence. | Test output, read-byte count, DB counts. | HIGH | NOT RUN |
| E-04 | CSRF and trusted Origin | STG; browser and disposable data | Submit valid JSON with missing/invalid CSRF, then untrusted Origin, then valid pair. | Invalid requests fail; valid request gets 202; only valid one persists. | Redacted HTTP statuses and Origin classification. | BLOCKER | NOT RUN |
| E-05 | Rate limiting | AUT and STG; isolated source IP | Exceed 10 requests within a 10-minute window; compare resolved proxy client IP. | Excess returns 429; one client's limit is not accidentally shared or bypassed by forwarding. | Status sequence and sanitized IP partition evidence. | HIGH | NOT RUN |
| E-06 | Storage failure neutrality | STG; disposable missing DB | Submit eligible request while DB is unavailable. | Generic 503, no invitation or implicit DB creation, no identity disclosure. | Status/body shape and file check. | BLOCKER | NOT RUN |

### F. Operator authorization and decisions

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| F-01 | Exact private Keycloak identity | EXT; private operator account and trusted issuer/subject confirmed | Log in through Keycloak and list requests; compare ticket identity to reviewed allowlist via trusted operator process. | Only exact issuer/raw subject is accepted; no email/role substitution. | Redacted authorization result and allowlist sign-off. | BLOCKER | NOT RUN |
| F-02 | Missing or incomplete allowlist | AUT and STG; controlled configuration variants | Try absent issuer, absent subject, and malformed pair with a valid Keycloak session. | Every variant denies operator access. | 403 statuses and sanitized config shape. | BLOCKER | NOT RUN |
| F-03 | Wrong identity and social login | EXT plus AUT; alternate Keycloak and Google/GitHub accounts | Try wrong issuer, wrong subject, public demo identity, and social session on list and mutations. | No operator access; anonymous receives authentication challenge/401 as configured. | Redacted route/status matrix. | BLOCKER | NOT RUN |
| F-04 | Protected list and decisions | STG; authorized operator, pending test requests | List pending/approved/rejected; approve one, reject one, retry both and invalid IDs. | Correct state and 204; repeat/conflict 409, missing 404, malformed 400; list is no-store and bounded. | Redacted status/record history. | HIGH | NOT RUN |
| F-05 | Mutation CSRF, Origin, body | STG; operator cookie | Try missing/invalid CSRF, untrusted Origin, and non-empty body on approve/reject/revoke. | No mutation; expected 400/403; valid bodyless request succeeds. | Statuses and unchanged DB counts. | BLOCKER | NOT RUN |
| F-06 | Concurrent decisions | AUT; disposable DB | Race approvals and approve/reject for one request, including an active redeemable-email conflict. | One winner, atomic request/invitation link, no orphaned invitation; other attempts conflict. | Test output and post-race counts. | HIGH | NOT RUN |

### G. External invitation redemption

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| G-01 | Google first binding | EXT; approved disposable email and Google test account with verified email | Sign in via Google callback and direct `/api/auth/login/google`; inspect protected access. | Verified `sub` plus matching email binds once and issues a local BFF session. | Redacted callback/status and binding count. | BLOCKER | NOT RUN |
| G-02 | GitHub first binding | EXT; approved email and GitHub account with verified primary email | Sign in via GitHub, including the direct login route; verify email API topology and result. | Numeric GitHub ID binds only to one verified primary email; no public-email fallback or persisted provider token. | Redacted callback/status, binding count, endpoint origin. | BLOCKER | NOT RUN |
| G-03 | Wrong or unverified email | AUT and EXT where accounts permit | Try uninvited address, unverified Google email, GitHub non-primary/unverified email, and provider API failure. | Generic denial, no local BFF cookie or binding. | Status/redirect and DB count, no token capture. | BLOCKER | NOT RUN |
| G-04 | Wrong subject or provider collision | AUT; fake provider endpoints | Try missing/ambiguous subject, principal mismatch, same subject across providers, and cross-provider reuse of one invitation. | Invalid evidence denies; provider identities remain separate; one invitation cannot link providers automatically. | Focused test results and binding counts. | BLOCKER | NOT RUN |
| G-05 | Concurrent first binding and repeat login | AUT plus EXT returning account | Race two identities for one approval; then repeat login with the winning stable provider identity after email change. | One atomic winner; returning bound identity succeeds without a new binding or GitHub email API call. | Race results, binding count, redacted repeat status. | HIGH | NOT RUN |

### H. Seven-day first-redemption policy

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| H-01 | Approval timestamp contract | AUT; controlled TimeProvider | Approve request and read UTC tick fields. | `created_at = approved_at`; `expires_at = approved_at + 7 days`. | Focused test result and tick deltas. | HIGH | NOT RUN |
| H-02 | Before boundary | AUT; fresh unbound approval and controlled clock | First bind at `expires_at - 1 tick`. | New binding allowed. | Decision reason and binding count. | HIGH | NOT RUN |
| H-03 | At and after boundary | AUT; separate fresh unbound approvals | First bind exactly at expiry and one tick after. | Both deny as Expired; no binding. | Decision reasons and counts. | BLOCKER | NOT RUN |
| H-04 | Bound identity after expiry | AUT; bind before boundary | Advance clock past seven days and check same provider/subject. | Existing binding remains allowed. | BoundIdentity decision. | HIGH | NOT RUN |
| H-05 | Revocation after binding | AUT and STG; bound test identity | Revoke after expiry, retry protected access. | Revoked decision and 401; expiration does not override revocation. | Decision and HTTP status. | BLOCKER | NOT RUN |
| H-06 | Reissue and history | AUT; expired unredeemed operator approval | Submit and approve a second request for same canonical email; inspect both records. | New invitation can bind; prior request, invitation ID, decision and revocation history remain. | Opaque IDs and aggregate record checks. | HIGH | NOT RUN |
| H-07 | Historical NULL expiry | AUT; disposable legacy row | Reopen/reprovision a pre-policy invitation with NULL expiry and test binding. | Row remains unchanged; historical behavior persists. | Null-field and decision checks. | MEDIUM | NOT RUN |

### I. Existing sessions and logout

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| I-01 | Cookie revalidation and revocation | STG; active invited Google/GitHub sessions | Revoke while cookies exist; call `/api/auth/me` and a protected API. | Next access is 401 and local cookie is deleted; no grace period or new binding. | Redacted statuses and Set-Cookie attributes. | BLOCKER | NOT RUN |
| I-02 | Provider-aware logout | STG; revoked external cookies, valid CSRF/Origin | POST exact `/api/auth/logout`; compare GET and other paths. | External logout is local and expires cookie; no Keycloak remote logout; exception grants no API access. | Redirect target and cookie attributes, no cookie value. | HIGH | NOT RUN |
| I-03 | Storage outage with active cookie | STG; reversible isolated storage fault | Remove DB access, request protected endpoint, then restore storage. | Enforced external cookie fails closed; no HTTP 500 or automatic rebind. | Status, cookie deletion, recovery result. | BLOCKER | NOT RUN |
| I-04 | Keycloak demo path | EXT; isolated Keycloak test realm | Log in and out through OIDC, including public demo identity where configured. | OIDC login/logout still works; invitation storage does not gate it; demo identity is not operator. | Redacted callback and status sequence. | BLOCKER | NOT RUN |

### J. Public activation decision

| ID | Objective | Preconditions | Proposed verification procedure | Expected result | Evidence to capture | Severity if failed | Status |
| --- | --- | --- | --- | --- | --- | --- | --- |
| J-01 | Safe public default | AUT; tracked example and normal frontend build | Inspect resolved Compose configuration and public-demo UI; check all three backend switches. | Workflow/enforcement and both UI flags off; acceptance image is not selected. | Sanitized config/build summary. | BLOCKER | NOT RUN |
| J-02 | Acceptance isolation and rollback | STG; inventory and verified backups | Compare deployed image/project/volumes against public; rehearse isolated rollback preserving both volumes. | Acceptance build never reaches public project; rollback is executable without losing revocation history. | Signed isolation and restore/rollback checklist. | BLOCKER | NOT RUN |
| J-03 | Explicit release decision | STG; complete redacted evidence package | Review all cases, unresolved risks, backup/restore proof, and operator activation approval. | GO only after every blocker/security gate passes; otherwise retain NO-GO and flags off. | Reviewed matrix, risk register, named decision owner. | BLOCKER | NOT RUN |

**Matrix count:** A 4, B 5, C 5, D 5, E 6, F 6, G 5, H 7, I 4, J 3; **50 total**, all NOT RUN.

## Execution phases and dependencies

**P5-A8b-02 — isolated Docker/staging verification:** first run the AUT checks against this commit; inspect the resolved configuration with an untracked staging-only environment; prove resource isolation before creating Docker resources; then exercise LDO and STG provisioning, persistence, permission, proxy, and backup/restore cases with disposable data. Obtain a separate authorization for any host action. Stop on a production-resource match, failed schema check, missing backup tool, inaccessible volume, or other BLOCKER. Preserve the original public project and both of its volumes.

Proposed local AUT commands from the repository root (record output and SHA):

```sh
dotnet test src/backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~InvitationDatabaseProvisioningTests|FullyQualifiedName~InvitationRequestWorkflowTests|FullyQualifiedName~OperatorWorkflowTests|FullyQualifiedName~SqliteInvitationStoreTests|FullyQualifiedName~GoogleInvitationGateTests|FullyQualifiedName~GitHubInvitationGateTests|FullyQualifiedName~InvitationSessionValidationTests"
cd src/frontend
npm run test:public-demo-build-config
npm run build:public-demo
npm run build:public-demo-acceptance
npx ng test frontend --watch=false --browsers=ChromeHeadless
```

Before any isolated Docker action, resolve the proposed staging configuration read-only from `deploy/public-demo` with `docker compose --project-name <stage-project> --env-file <stage-env> config --quiet`; substitute operator-reviewed staging-only values and apply the same project override to every later Compose command. This is a future procedure, not a command run for this document.

**P5-A8b-03 — end-to-end security acceptance and decision:** after P5-A8b-02 evidence is reviewed, use disposable Keycloak, Google, and GitHub accounts for the EXT cases. Confirm registered HTTPS callbacks, trusted operator issuer/raw subject, verified provider email behavior, browser CSRF/Origin, cookie attributes, revocation, and OIDC isolation. Review every matrix row and the rollback rehearsal. External provider credentials, test accounts, and Keycloak access are prerequisites, not assumed available.

Prior focused tests exist in `src/backend.Tests` and `src/frontend`, including real-handler callbacks with fake OAuth endpoints and controlled-clock boundary tests. They should be rerun in P5-A8b-02 with recorded commit, command, counts, and logs. No P5-A8b tests were run for this document.

## Evidence checklist, abort conditions, and decision gate

- Record baseline SHA, frontend/backend image digests, resolved staging project/host/ports/network/volume names, and an explicit comparison against production resources.
- Capture sanitized build, Compose, provisioning, schema/integrity, permission, request/response, and focused-test evidence by stable test ID. Never retain tokens, cookie values, real secrets, or unrestricted personal data in the package.
- Record approved invitation, binding, request-decision, and revocation continuity across restart, replacement, backup, and isolated restore using disposable data and opaque identifiers.
- Require a consistent SQLite snapshot with stopped writers or a SQLite-aware backup, verified `quick_check` and foreign-key checks, a successful restore to an isolated target, and a rollback owner. A stale restore that would erase revocations is an abort condition.
- Abort on any production-volume/project overlap, untrusted proxy/callback path, unauthorized operator access, CSRF/Origin bypass, incorrect provider binding, uninvited or revoked access, missing/unwritable DB allowing access, failed backup/restore, or an acceptance UI reaching the public project.
- **GO requires:** all BLOCKER rows PASS, every security-critical negative case verified, no unresolved HIGH risk accepted without an explicit reviewed decision, complete backup/restore and isolation evidence, and explicit operator approval. Unrun security-critical cases, missing rollback prerequisites, or any BLOCKER failure keep **NO-GO**. Public flags and the acceptance frontend must remain off until that review is complete.

## Open questions

The operator must identify the isolated host or consistently overridden project, confirm available ports/subnet and effective image UID/GID, provide disposable test identities and registered callbacks, select a verified SQLite backup tool and restore target, establish the trusted private Keycloak issuer/raw subject, and name the evidence reviewer and rollback owner. None of these answers is inferred from the tracked example. **Current release decision: NO-GO.**
