# Current Status

## Status Note

This file captures current working assumptions for Codex.

Update it after meaningful milestone changes, not after every small task.

Last reviewed: 2026-07-15, after the `OidcStarter.AspNetCore.Bff` `v1.0.1` hardening validation patch release.

## Repository Stage

`oidc-starter` is no longer an early prototype.

Current state:

- the reusable backend NuGet package `OidcStarter.AspNetCore.Bff` is at the verified `v1.0.1` baseline,
- the maintained reusable frontend package source is `@mordalabs/oidc-starter-auth` at version `0.2.0` and is not yet published to npm,
- the repository remains a public reference implementation and sample app,
- the backend package is the primary product focus at this stage, while the frontend package remains part of the reusable starter contract,
- the `v1.0.1` backend release was a hardening validation patch with no breaking changes, no public API changes, and no runtime behavior changes.

## Current Primary Focus

Current work in this repository is focused on:

- controlled, incremental feature development,
- package-safe compatibility, security, and maintenance improvements,
- focused test additions for behavior-changing or security-sensitive work,
- and minimal documentation updates when consumer-visible behavior changes.

The current goal is not broad redesign. The current goal is to evolve the starter incrementally while preserving package stability and the security guarantees validated in the `v1.0.1` hardening checkpoint.

### Angular Package Compatibility Closeout

The historical `@flying-bee/oidc-starter-auth` `0.1.1` Angular 20.3+/21/22 compatibility release was published to npm, and its Angular compatibility was verified. No further compatibility implementation is required.

### Angular package scope migration

- Core package identity migration to `@mordalabs/oidc-starter-auth` is merged.
- The maintained source package remains at version `0.2.0` and has not yet been published to npm.
- `@flying-bee/oidc-starter-auth@0.2.0` remains published and must not yet be deprecated.

## Stable Assumptions

Treat the following as established project assumptions:

- BFF is the primary production-oriented path.
- SPA remains a supported reference/demo mode and must not be removed or weakened unless explicitly requested.
- The reusable backend package should remain provider-agnostic by default.
- Provider-specific behavior should stay in sample/integration code unless a task explicitly asks for a generic reusable abstraction.
- Local Keycloak setup is development-only infrastructure.
- Findings from `oidc-starter-agent` must be classified before fixing.
- Not every audit finding is a real starter bug.
- Prefer grouped follow-up releases over publishing a patch release after every single finding.

## Recent Hardening Checkpoint

After `v1.0.0`, the repository completed an audit-driven hardening validation checkpoint for the backend package.

The `OidcStarter.AspNetCore.Bff` `v1.0.1` checkpoint is closed and recorded as:

- hardening validation patch release,
- BFF test project passed,
- audit smoke confirmed 0 findings at the checkpoint,
- breaking changes: none,
- public API changes: none,
- runtime behavior changes: none.

Going forward:

- findings from `oidc-starter-agent` remain structured input for small implementation tasks,
- findings classified as stale or false positives are valid outcomes of the audit process and do not require starter changes,
- real issues should be fixed incrementally in the starter after classification,
- future security-sensitive work should retain targeted regression tests and an audit smoke check at an appropriate final validation point.

## Things Already Intentionally Solved

The following areas are already intentionally present in the repository and should not be rediscovered from scratch during normal implementation tasks.

Do not reopen these areas unless the current task explicitly targets them or new evidence shows they are incomplete:

- explicit SameSite configuration exists for relevant BFF cookies and has focused test coverage,
- antiforgery flow exists end-to-end for browser-to-BFF requests,
- `/api/auth/csrf` exists and is part of the documented frontend contract,
- package-provided logout is protected by antiforgery validation, with origin validation retained as defense in depth,
- the reusable backend package exposes a generic role-mapping extension point,
- the sample backend demonstrates Keycloak-specific role mapping,
- the local Keycloak import provisions sample roles for `testuser`,
- the root README positions the repository as a public reference implementation and reusable starter,
- package release documentation exists in `docs/package-release.md`.

## Licensing

- Licensing clarified: current public core packages remain MIT.
- Root MIT `LICENSE` added.
- Next phase: Public Demo Readiness.

## Current Release Discipline

Do not assume every feature, compatibility, security, or maintenance change should immediately trigger a new package release.

Default approach:

- accumulate coherent feature, compatibility, security, or maintenance changes,
- classify future audit findings before changing behavior,
- preserve public package APIs and documented BFF contracts unless a breaking release is explicitly approved,
- keep release notes in `docs/package-release.md` when release planning is part of the task,
- and release only when the resulting patch/minor version is worth publishing as a coherent update.
