---
name: oidc-starter-implementation-task
description: Implement one focused feature, fix, or behavior change in oidc-starter while preserving package/sample boundaries and compatibility through minimal scope and targeted validation. Use only for implementation work; do not use for review-only, docs-only, release, audit-rule, or oidc-starter-agent tasks.
---

# OIDC Starter Implementation Task

Use this skill for one focused implementation task in `oidc-starter`. Treat the invoking prompt as the source of the concrete product requirements; this skill defines the working method, not the feature.

## Repository Contracts

Before implementation, read these working contracts:

- `/ai/agent-rules.md`
- `/ai/token-budget.md`
- `/ai/project-context.md`
- `/ai/current-status.md`

If the invoking prompt conflicts with them, stop and report the conflict. Do not silently choose an interpretation.

## Scope the Task

Identify the smallest relevant area:

- reusable backend BFF package,
- backend package tests,
- sample backend,
- reusable frontend package,
- sample frontend,
- local infrastructure,
- or tightly related documentation when explicitly required.

Inspect named files first, then only directly related dependencies and tests. Do not scan unrelated areas, modify `oidc-starter-agent`, or fix unrelated findings.

## Preserve Boundaries

- Keep BFF as the primary production-oriented path.
- Keep SPA as a supported reference/demo mode.
- Keep reusable backend behavior provider-agnostic by default.
- Keep provider-specific behavior in sample or integration code unless a reusable abstraction is explicitly requested.
- Preserve public package APIs and documented BFF contracts unless an API change is explicitly approved.
- Treat audit findings as input to classify, not automatic proof that the starter is wrong.

## Implement

- Make the smallest coherent change that satisfies the invoking task.
- Prefer a minimal diff and established repository patterns.
- Do not refactor, rename, reformat, modernize, or redesign unrelated code.
- Do not broaden the task when separate issues appear; report them as follow-up items.
- Add or update the smallest relevant regression coverage when behavior changes, especially for security-sensitive behavior.

## Validate

The operator normally runs builds and tests manually. Do not run tests, builds, audits, package publishing, or broad validation unless the invoking prompt explicitly authorizes execution.

Always provide exact targeted validation commands for the changed area. Do not recommend a full solution build or full test suite unless shared infrastructure changed or the invoking task explicitly requires it. For security-sensitive changes, include targeted regression tests and mention an audit smoke check only as a later final-validation step when appropriate.

## Release and Documentation Boundaries

Do not update `/ai/current-status.md`, release notes, versions, package metadata, tags, changelogs, or release artifacts unless explicitly included in the invoking task. Do not create commits, tags, pushes, GitHub releases, NuGet publications, or npm publications. Keep release preparation separate from implementation.

## Stop Conditions

Stop and report instead of implementing when:

- the task requires an unapproved public API or breaking change,
- the required repository area is ambiguous,
- the behavior conflicts with repository contracts,
- implementation requires changing both `oidc-starter` and `oidc-starter-agent`,
- a new production dependency appears necessary but was not approved,
- broader repository scanning is required,
- or the task becomes materially larger than the invoking prompt suggests.

Do not silently broaden the task.

## Final Response

Return one concise, copyable Markdown block with no nested fenced code blocks. Include:

- status,
- changed files,
- behavior implemented,
- tests, builds, and audits run or not run,
- exact targeted operator validation commands,
- important limitations or follow-up only when material.

Do not include full diffs, long logs, repeated project background, large code excerpts, or unrelated recommendations.
