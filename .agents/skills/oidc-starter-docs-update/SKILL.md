---
name: oidc-starter-docs-update
description: Update one explicitly identified documentation scope in oidc-starter using locally verifiable repository evidence, minimal scope, and documentation-appropriate validation. Never change runtime behavior, tests, versions, or package metadata. Use only for documentation updates; not for implementation, review-only, release execution, audit-rule development, or oidc-starter-agent work.
---

# OIDC Starter Docs Update

Use this skill for one focused documentation-only task in `oidc-starter`. The invoking prompt must identify the concrete documentation target and intended outcome. This skill defines the working method, not feature- or release-specific content. Keep every claim grounded in locally verifiable evidence and never change runtime behavior.

## Repository Guard and Contracts

Verify that the active Git repository is `oidc-starter`. Do not work in `PersonalRAG` or modify the separate `oidc-starter-agent` repository. If the active repository or documentation target is ambiguous, stop before editing.

Before editing, read these working contracts:

- `/AGENTS.md`
- `/ai/agent-rules.md`
- `/ai/token-budget.md`
- `/ai/project-context.md`
- `/ai/current-status.md`

If the invoking prompt, this skill, `/AGENTS.md`, or an `/ai` contract materially conflicts, stop and report the conflict instead of guessing.

## Establish the Documentation Target

Resolve the exact documentation scope before editing. Valid targets include the root README, package or consumer documentation, architecture documentation, sample or local-Keycloak setup instructions, `/ai/current-status.md`, another explicitly named `/ai` contract, package release documentation, or a named Markdown file or tightly related documentation set.

Prefer files explicitly named by the invoking prompt. Inspect only directly relevant source files, project metadata, tests, release documentation, or narrowly scoped Git history needed to verify claims. Do not scan the whole repository, assume related documents need synchronized changes, or update a document merely because it exists. If the target cannot be established without guessing, stop and request a precise scope.

## Classify the Task

Classify the task before editing as one of:

- consumer or package documentation,
- sample or local-development documentation,
- architecture or repository guidance,
- AI working-contract update,
- current-status or milestone update,
- release-documentation update,
- focused correction or cleanup.

Use the classification to choose the smallest relevant evidence and validation scope. Do not combine unrelated documentation categories unless the invoking prompt explicitly requires synchronization.

## Source-of-Truth Discipline

Verify factual statements against the smallest relevant local evidence, such as runtime or package code, public API declarations, project files, package metadata, tests, README files, architecture documentation, `/ai/current-status.md`, release documentation, local tags or narrowly scoped history, configuration files, or package scripts.

- Do not invent behavior, commands, versions, release facts, paths, compatibility guarantees, or test results.
- Do not describe planned behavior as implemented or historical behavior as current.
- Distinguish verified facts from recommendations and future direction.
- If evidence conflicts, stop and report the conflict.
- Prefer repository code and package metadata over stale prose for implementation facts.
- Treat `/ai/current-status.md` as current-phase source of truth when consistent with repository evidence.
- Treat release notes and release metadata as the source of truth for completed release claims.

## Editing Discipline

- Make the smallest coherent change satisfying the invoking task.
- Preserve useful existing wording and terminology.
- Prefer a minimal diff; do not reformat unrelated sections.
- Do not rename headings, files, or directories without explicit need.
- Do not modernize unrelated prose, add speculative future features, or duplicate information owned by another document.
- Link or route to the appropriate source instead of copying large sections when practical.
- Report separate documentation problems as follow-up rather than expanding scope.

## Current Status and AI Contracts

When updating `/ai/current-status.md`, do so only for a meaningful milestone, release, phase change, or explicitly requested status correction. Preserve stable assumptions, distinguish completed checkpoints from active work, avoid claims that no active findings exist unless verified, avoid unapproved future-feature promises, and record dates or versions only when locally supported.

When updating another `/ai` contract, preserve its existing responsibility. Do not move current status into permanent rules, detailed repository context into task procedures, or skill procedures into `/ai` files. Do not change workflow semantics unless explicitly requested.

## Consumer and Release Boundaries

For package-facing documentation, verify public API names, endpoints, configuration keys, package names, and example commands. Preserve the reusable-package versus sample distinction, BFF as the primary production-oriented path, SPA as a supported reference/demo path, provider-agnostic reusable behavior, and clearly identified Keycloak-specific sample behavior.

Release documentation may be updated only when explicitly requested. It may record verified changes, compatibility impact, migration notes, or targeted validation. Never change package versions or metadata, create tags or releases, publish packages, or claim validation ran when only commands were recommended. Release preparation and release execution are separate workflows.

## Runtime and Repository Boundaries

While this skill is active, do not modify runtime code, tests, project files, package metadata, versions, lock files, infrastructure configuration, existing skills unless explicitly targeted, or `oidc-starter-agent`. Do not stage files or create commits, branches, tags, pushes, releases, or package publications. Reading directly relevant non-documentation files is allowed only to verify claims.

## Validation

Use documentation-appropriate validation. Always inspect the final diff, confirm only intended documentation files changed, check for formatting damage, verify paths, package names, API names, commands, versions, and links against local evidence, and check that historical and current-state language are not mixed.

Do not run builds, tests, audits, restores, package commands, or release commands unless the invoking prompt explicitly authorizes execution. Documentation-only tasks normally do not justify runtime validation. If documentation describes a command or behavior requiring verification, provide the smallest targeted operator command drawn from README files, project files, package scripts, or existing documentation. Do not invent commands.

Always provide a scoped `git diff --check`, a scoped `git diff`, and `git status --short` in the final handoff.

## Stop Conditions

Stop and report instead of editing when the repository is not `oidc-starter`, the documentation target is missing or ambiguous, requested facts cannot be verified locally, evidence conflicts, runtime/test/version/package-metadata/release-execution changes are required, the task targets `oidc-starter-agent`, broad scanning is required, unrelated documentation scopes are combined without justification, or the task becomes materially larger than the prompt suggests.

Do not silently broaden the task.

## Final Response

Return one concise, copyable Markdown block with no nested fenced code blocks. Include status, changed documentation files, sections or topics updated, evidence used for factual claims, commands/tests/builds/audits/releases run or not run, exact targeted operator verification commands, and material ambiguity or follow-up only when important.

Do not include complete file contents, full diffs, long logs, repeated repository background, large source excerpts, unrelated recommendations, or speculative future work.
