# Current Status

## Status Note

This file captures current working assumptions for Codex.

Update it after meaningful milestone changes, not after every small task.

Last reviewed: after `v1.0.0` release and during audit-driven hardening.

## Repository Stage

`oidc-starter` is no longer an early prototype.

Current state:

- the reusable backend NuGet package `OidcStarter.AspNetCore.Bff` has been released at `v1.0.0`,
- the reusable frontend package `@flying-bee/oidc-starter-auth` exists and is published,
- the repository remains a public reference implementation and sample app,
- the backend package is the primary product focus at this stage, while the frontend package remains part of the reusable starter contract.

## Current Primary Focus

Current work in this repository is focused on:

- small hardening fixes,
- package-safe improvements,
- fixes driven by `oidc-starter-agent` audit findings,
- focused test additions,
- and minimal documentation updates when consumer-visible behavior changes.

The current goal is not broad redesign. The current goal is to strengthen the starter incrementally while preserving package stability.

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

## Recent Post-Release Direction

After `v1.0.0`, work has shifted toward audit-driven hardening.

This means:

- findings from `oidc-starter-agent` are used as input for small implementation tasks,
- stale and false-negative findings are treated as valid outcomes of the audit process,
- real issues are fixed incrementally in the starter,
- release decisions should be made after several meaningful fixes, not after every isolated change.

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

## Current Release Discipline

Do not assume every post-release hardening fix should immediately trigger a new package release.

Default approach:

- accumulate several meaningful hardening or compatibility fixes,
- keep release notes in `docs/package-release.md` when release planning is part of the task,
- and release only when the resulting patch/minor version is worth publishing as a coherent update.
