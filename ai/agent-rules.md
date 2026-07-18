# Agent Rules

These rules apply to Codex work in this repository.

## Role

Act as a focused implementation agent for this repository.

Your job is to:

- implement narrowly scoped changes,
- add or update focused tests for behavior-changing implementation work,
- preserve package compatibility,
- preserve the sample app’s ability to demonstrate the packages,
- report concise results.

## Context Rules

Use `/ai/token-budget.md` for context, scanning, test execution, and output limits.

Read additional `/ai/*.md` files only when the task references them or when needed for the current scope.

Do not scan the whole repository unless explicitly requested or approved after explaining the need.

## Scope Discipline

Work only within the scope of the current task.

Do not:

- scan the whole repository unless the task truly requires it,
- solve unrelated problems "while you are here",
- broaden the task into a larger refactor,
- redesign architecture unless explicitly requested.

Prefer small, local, reversible changes.

## Auth Mode Rules

BFF is the primary production-oriented path for hardening and package evolution.

SPA mode is retained as a supported reference/demo mode.

Unless the task explicitly targets SPA mode:

- do not modify SPA-specific behavior,
- do not remove or weaken SPA support,
- reason about hardening primarily through the BFF architecture.

## Package Compatibility Rules

Protect compatibility for:

- the NuGet package public API,
- the Angular package public API,
- documented BFF endpoints,
- sample app startup flow,
- local Keycloak demo setup.

Do not introduce breaking changes unless the task explicitly requests them or the task is specifically about a documented breaking hardening change.

If a change affects consumers, call it out explicitly.

## Package vs Sample Rules

Prefer package-level fixes when the issue belongs to reusable behavior.

Prefer sample-level fixes when the issue is:

- provider-specific,
- demo-specific,
- local-development-specific,
- or intentionally outside the reusable package contract.

Do not move provider-specific behavior into reusable packages unless explicitly requested.

Example:

- Keycloak-specific role mapping belongs in the sample integration layer unless the task explicitly asks for a reusable provider recipe.

## Testing Rules

Prefer focused tests over broad test runs.

When behavior changes:

- add or update the smallest relevant test coverage,
- run only the directly relevant test project or targeted tests when possible.

Do not run the full solution test suite by default.
Run it only when explicitly requested, before final handoff for implementation tasks, or when shared infrastructure/package contracts changed.

Do not:

- add heavy end-to-end coverage unless explicitly requested,
- add tests for unrelated sample/demo behavior when the task is package-focused.

## Documentation Rules

Do not update README, release notes, package docs, changelogs, package version numbers, or release artifacts unless:

- the task explicitly requires documentation changes,
- the change introduces a new integration contract,
- the change introduces or documents a breaking behavior,
- the change affects security behavior that consumers must know.

Keep documentation changes minimal and directly tied to the task.

## Audit-Finding Workflow

Treat findings from `oidc-starter-agent` as structured input for small implementation tasks.

For each finding, first classify it as:

- valid,
- partially valid,
- stale / false positive.

Only then decide whether the correct response is:

- a code fix,
- a test addition/update,
- a documentation clarification,
- or no change in `oidc-starter`.

Do not assume an auditor finding automatically means the starter is wrong.

## Security Change Discipline

For security-related tasks:

- prefer minimal, correct hardening fixes,
- preserve current package architecture unless the task explicitly requires a broader redesign,
- prefer defense-in-depth when it fits the current design,
- document consumer-visible security contract changes when needed.

Do not perform broad security redesigns unless explicitly requested.

## Forbidden Changes

Do not add:

- LLM, OpenAI, Azure OpenAI, Semantic Kernel, LangChain, or MCP integrations,
- custom identity provider implementations,
- new authentication modes unrelated to the current starter,
- broad framework abstractions,
- broad architecture rewrites,
- unrelated package splits or repository reorganizations.

Do not remove, weaken, or break SPA mode unless the task explicitly targets SPA cleanup or deprecation.

## Final Response Format

For implementation tasks, respond with:

```md
Done.

Changed:
- ...

Tests:
- ...

Audit/Finding:
- ...

Notes:
- ...
```

Keep the final response concise.
Do not include full diffs or long logs unless requested.

