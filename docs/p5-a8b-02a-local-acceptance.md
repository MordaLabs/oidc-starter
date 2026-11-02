# P5-A8b-02A local automated acceptance evidence

**Scope:** local automated and tracked-source checks only. No staging, provisioning, Docker build/container operation, public activation, or production inspection occurred. The canonical 50-case matrix in `docs/p5-a8b-acceptance-plan.md` is unchanged. **Release decision: NO-GO.**

## Baseline and tools

| Item | Measured value |
| --- | --- |
| Repository, branch, baseline | `MordaLabs/oidc-starter`; `test/p5-a8b-02a-local-acceptance`; `6e506794f98506221dc11bc925cdd33465fbf0cc` |
| Execution date | 2026-11-02 UTC; preflight recorded at 16:36:44 UTC |
| Initial worktree | Clean |
| OS | Microsoft Windows 10.0.26100 |
| .NET SDK | 10.0.401 |
| Node.js / npm | v22.23.2 / 10.9.8 |
| Docker Compose | v5.4.0 (version command only; no running resources accessed) |

## Executed commands and results

Commands below ran at the baseline SHA. `dotnet` used the repository root; `npm`/`npx` used `src/frontend`; Compose used `deploy/public-demo`. Normal ignored build/test outputs were permitted. No package installation or source change was made.

| Command | Exit | Measured result |
| --- | ---: | --- |
| `dotnet test src/backend.Tests/Backend.Tests.csproj --filter "FullyQualifiedName~InvitationDatabaseProvisioningTests\|FullyQualifiedName~InvitationRequestWorkflowTests\|FullyQualifiedName~OperatorWorkflowTests\|FullyQualifiedName~SqliteInvitationStoreTests\|FullyQualifiedName~GoogleInvitationGateTests\|FullyQualifiedName~GitHubInvitationGateTests\|FullyQualifiedName~InvitationSessionValidationTests"` | 1 | Initial restricted run stopped after restore; no test count or diagnostic error was emitted. This is **not** a test PASS. |
| Same filtered command with `--no-restore -v normal` | 1 | Diagnostic restricted run stopped at the VSTest build target before test discovery; reported zero warnings and zero errors but no test count. |
| Original filtered backend command with permission for normal ignored output writes | 0 | **209 passed, 0 failed, 0 skipped** on net10.0. No warnings were reported in the successful output. |
| `npm run test:public-demo-build-config` | 0 | **3 passed, 0 failed, 0 skipped**: Angular mapping, exact Dockerfile build-argument allowlist/default, and Compose frontend default. |
| `npm run build:public-demo` | 0 | Angular selected `public-demo`; bundle generation completed. No warning/error reported. |
| `npm run build:public-demo-acceptance` | 0 | Angular selected `public-demo-acceptance`; bundle generation completed independently. No warning/error reported. |
| `npx ng test frontend --watch=false --browsers=ChromeHeadless` | 1 | Initial restricted run built the test bundle but failed before browser execution: `EPERM` creating an ignored `dist/test-out` directory. No test count. |
| Same ChromeHeadless command with permission for normal ignored output writes | 0 | **85 passed, 0 failed** in Chrome Headless 156.0.0.0. Jasmine warned that several `App` specs have no expectations; this did not fail the suite. |
| `docker compose --project-name oidc-starter-a8b-audit --env-file .env.example -f compose.yaml config --quiet` | 0 | Static Compose syntax/configuration accepted with no output. Only the tracked example env file was used. No containers, images, networks, or volumes were queried or changed. |

The two restricted backend attempts did not yield a source-level error. The successful permission-adjusted run establishes the focused test result but does not prove the precise cause of those earlier exits. The frontend `EPERM` was an explicit local output-directory permission failure. Both original failures remain part of this evidence. The Angular builds establish bundle compilation, not Docker image construction or served UI behavior.

## Static configuration observations

**Inferred from tracked source and the three passing configuration tests:** `src/frontend/angular.json` maps the two exact configurations to separate environment files. `public-demo` uses BFF with relative same-origin API and both invitation UI flags false; `public-demo-acceptance` keeps BFF and relative API but sets both flags true. `deploy/public-demo/frontend.Dockerfile` defaults to `public-demo` and allows exactly `public-demo` or `public-demo-acceptance`; other build arguments exit 2 before selecting an Angular build. The example Compose frontend value is `public-demo`.

**Inferred from tracked Compose and `.env.example`, with static `config --quiet` success:** Compose declares the fixed default name `oidc-starter-public-demo`, two separate backend named volumes (invitation database and Data Protection keys), loopback-only published frontend/backend ports, and false defaults for workflow, Google enforcement, and GitHub enforcement. The acceptance frontend is an explicit build selection. These checks do not prove resolved staging resource isolation, host port availability, or the state of a running deployment. No expanded environment map or secret value was captured.

## Acceptance case mapping

Statuses describe **local evidence for this slice**, not changes to the canonical matrix. `AUT PASS` means the full automated procedure was run. `AUT PARTIAL` means local supporting checks passed while a required Docker, staging, external-provider, or served-UI step remains. `NOT RUN` preserves cases with no execution of their required environment procedure.

| Case | Evidence status | Basis and remaining boundary |
| --- | --- | --- |
| A-01 | AUT PASS | `public-demo` build plus environment/configuration tests passed. |
| A-02 | AUT PASS | Independent acceptance build plus focused Angular UI specs within the 85/85 run passed. |
| A-03 | AUT PARTIAL | Source-level two-value allowlist/default test passed; real Docker builds, including invalid arguments, were not run. |
| E-01 | AUT PARTIAL | Backend disabled-route test passed; staging route and database state were not checked. |
| E-03 | AUT PASS | TestHost cases covered JSON shape, malformed/duplicate fields, pre-read Content-Length rejection, form/non-JSON no-read rejection, unknown-length 1025-byte bound, and no persistence. |
| E-05 | AUT PARTIAL | Backend fixed-window rate-limit test passed; staging proxy/client-IP partitioning was not checked. |
| F-02 | AUT PARTIAL | Backend empty/malformed operator allowlist tests passed; staging configuration variants were not checked. |
| F-06 | AUT PASS | Backend concurrent approve/reject and active-invitation conflict tests passed. |
| G-03 | AUT PARTIAL | Fake-provider tests covered uninvited/unverified evidence and API failures; real external accounts were not used. |
| G-04 | AUT PASS | Google/GitHub gate and store tests covered malformed/ambiguous subjects, principal mismatch, provider-scoped IDs, and invitation reuse. |
| G-05 | AUT PARTIAL | Backend race and repeat-identity tests passed, including no repeat GitHub email call; external return login was not exercised. |
| H-01 | AUT PASS | Controlled-clock approval test checked one UTC snapshot and seven-day expiry. |
| H-02 | AUT PASS | Controlled-clock first binding immediately before expiry passed. |
| H-03 | AUT PASS | Exact-expiry and post-expiry first binding denied in controlled-clock tests. |
| H-04 | AUT PASS | Previously bound identity remained valid after expiry in store/operator tests. |
| H-05 | AUT PARTIAL | Revocation-after-binding tests passed; staging protected-request proof remains. |
| H-06 | AUT PASS | Expired unredeemed approval reissue and history tests passed. |
| H-07 | AUT PASS | Historical NULL-expiry preservation test passed. |
| J-01 | AUT PARTIAL | Safe tracked defaults, static Compose check, and normal frontend build passed; resolved deployed configuration and served public UI were not verified. |
| C-01 | NOT RUN | Process-level provisioning unit coverage passed, but the required Docker one-shot procedure was not run. |

**Additional supporting unit coverage, without upgrading environment cases:** provisioning tests exercised exact/malformed command exits, schema verification, repeatability, and unexpected trigger/index rejection; SQLite store tests exercised explicit creation, missing-file fail-closed behavior, persistence, constraints, and atomic binding. Session validation tests exercised revoked-cookie denial, local external logout with valid CSRF/Origin, provider isolation, and missing-storage denial. These results do not complete C-02 through C-05, D-01 through D-05, or I-01 through I-04, whose Docker/staging/external procedures remain **NOT RUN**.

## Gaps and next checkpoint

No Docker image was built. No container, volume, network, one-shot provisioner, HTTPS staging route, live provider, real browser served from an image, backup, restore, restart, or rollback was exercised. The static Compose success cannot establish isolation or deployment safety. The backend and browser tests used local fake/test infrastructure; they do not establish real provider or proxy behavior. The Jasmine no-expectation warnings merit separate test-quality follow-up but did not invalidate the 85-test result.

**Next checkpoint:** P5-A8b-02 isolated Docker/staging verification, beginning with an operator-reviewed staging identity and resolved resource comparison before any Docker resource creation. Keep the public workflow and enforcement switches off and retain **NO-GO** until the remaining blocker/security cases, backup/restore, and explicit release review pass.
