# AGENTS.md

## Repository Identity

`oidc-starter` is a public OpenID Connect reference implementation and reusable starter. It contains:

- a reusable ASP.NET Core BFF package,
- a reusable Angular authentication package,
- backend and frontend sample applications,
- local Keycloak development infrastructure.

BFF is the primary production-oriented path. SPA remains a supported reference/demo path. Reusable package behavior should remain provider-agnostic by default.

## Instruction Sources

Every Codex task in this repository must read:

- `/ai/agent-rules.md`
- `/ai/token-budget.md`

Additionally read `/ai/project-context.md` for architecture, repository responsibilities, package/sample boundaries, BFF or SPA behavior, provider-specific behavior, public APIs, compatibility, or security-sensitive behavior.

Additionally read `/ai/current-status.md` for the current project phase, task ordering, recent milestones, hardening or audit status, release state, release preparation, or project-status documentation.

Repository-scoped skills live under `/.agents/skills` and define repeatable task procedures. Load a skill when the invoking prompt explicitly names its `$skill-name`. The current explicitly invoked workflows are:

- `$oidc-starter-implementation-task`: one focused implementation change; file modifications are allowed within the invoking scope.
- `$oidc-starter-review-task`: one explicitly identified review target; review only, with no file modifications.
- `$oidc-starter-docs-update`: one explicitly identified documentation scope; documentation files only, with no runtime behavior changes.

Concrete requirements in the invoking prompt remain the source of truth for the current task. If the prompt, an applicable skill, this file, or an `/ai` contract materially conflicts, stop and report the conflict instead of guessing.

## Repository Map

- `src/OidcStarter.AspNetCore.Bff`: reusable backend BFF package.
- `src/OidcStarter.AspNetCore.Bff.Tests`: backend package tests.
- `src/backend`: sample backend host.
- `src/frontend/projects/oidc-starter-auth`: reusable Angular package.
- `src/frontend`: sample frontend application.
- `infra/keycloak`: local Keycloak infrastructure.
- `docs`: architecture and package-release documentation.
- `ai`: AI working contracts.
- `.agents/skills`: repository-scoped skills.

## Universal Working Defaults

- Work on one narrowly scoped task at a time.
- Verify that the active repository is `oidc-starter` before editing; do not work in `PersonalRAG` or `oidc-starter-agent`.
- Inspect files explicitly named by the prompt first, then expand only to directly related dependencies, configuration, and tests.
- Do not scan the whole repository by default.
- Prefer the smallest coherent and reversible diff.
- Do not perform unrelated refactoring, renaming, cleanup, formatting, modernization, or redesign.
- Do not fix unrelated findings; report them as follow-up items.
- Preserve public package APIs and documented BFF contracts unless a breaking change is explicitly approved.
- Keep reusable packages provider-agnostic by default and provider-specific behavior in sample or integration code unless a reusable abstraction is explicitly requested.
- Preserve BFF as the primary production-oriented path and SPA as a supported reference/demo path.
- Treat audit findings as structured input requiring classification, not automatic proof that starter behavior is wrong.
- Do not modify the separate `oidc-starter-agent` repository or add unapproved production dependencies.
- Do not create commits, tags, pushes, package publications, GitHub releases, or other release actions unless explicitly requested.

## Validation Defaults

`/ai/token-budget.md` controls validation scope. Prefer targeted tests, builds, linting, or package checks for the changed area. Do not run the full solution build, full test suite, broad audit, or release validation by default.

The operator normally runs validation manually unless the invoking prompt explicitly authorizes Codex to execute commands. When Codex does not run validation, provide exact targeted operator commands drawn from the README, project files, package scripts, or existing repository documentation. Security-sensitive changes should include focused regression coverage and may require an audit smoke check at an appropriate final-validation stage.

## Documentation and Release Boundaries

Do not update `/ai/current-status.md`, release notes, versions, package metadata, changelogs, tags, or release artifacts unless explicitly included in the task. Documentation-only tasks must not change runtime behavior. Implementation tasks must not silently become release-preparation tasks; release preparation and release execution are separate workflows.

## Final Response

Final Codex responses should be concise and include status, changed files, behavior or documentation changed, tests/builds/audits run or not run, exact targeted operator validation commands, and material limitations or follow-up only when important.

Do not include full diffs, long logs, repeated repository context, large code excerpts, or unrelated recommendations.
