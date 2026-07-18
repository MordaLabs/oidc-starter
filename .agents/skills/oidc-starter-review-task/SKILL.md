---
name: oidc-starter-review-task
description: Review one explicitly identified change, diff, commit, implementation result, or selected file scope in oidc-starter without modifying files. Assess correctness, security, compatibility, package/sample boundaries, tests, and commit readiness. Use only for review; not for implementation, docs updates, release execution, audit-rule development, or oidc-starter-agent work.
---

# OIDC Starter Review Task

Use this skill for one explicitly identified review target in `oidc-starter`. The invoking prompt defines the concrete scope; this skill defines the review method. Never modify files while using it.

## Repository Guard and Contracts

Verify that the active Git repository is `oidc-starter`. Do not work in `PersonalRAG` or review the separate `oidc-starter-agent` repository. If the active repository or target is ambiguous, stop before reviewing.

Before reviewing, read these working contracts:

- `/AGENTS.md`
- `/ai/agent-rules.md`
- `/ai/token-budget.md`
- `/ai/project-context.md`
- `/ai/current-status.md`

If the invoking prompt, this skill, `/AGENTS.md`, or an `/ai` contract materially conflicts, stop and report the conflict instead of guessing.

## Establish the Target

Resolve the exact target before analysis. It may be a current working-tree diff, staged diff, commit hash, commit range, named files, or a pasted implementation summary paired with locally inspectable changes.

Use narrow, read-only Git inspection where appropriate: status, diff, staged diff, show, or narrowly scoped log inspection. Do not assume every uncommitted file belongs to the task. Exclude unrelated pre-existing changes. If the target cannot be established without guessing, stop and request a precise scope. Do not review the whole repository by default.

## Review Scope

Review only the identified change and directly related contracts, dependencies, and tests. Assess where relevant:

- behavioral correctness and failure handling,
- security, authentication, authorization, and antiforgery implications,
- public API, package, and documented BFF compatibility,
- reusable package versus sample responsibility,
- provider-agnostic versus provider-specific placement,
- BFF versus SPA boundaries,
- regression risk and test adequacy,
- missing negative or edge-case coverage,
- consumer-visible documentation impact,
- and consistency with the current repository stage.

Do not broaden the review into general architecture assessment, repository-wide cleanup, unrelated security auditing, modernization, cosmetic style review, release preparation, or implementation of fixes.

## Stable Boundaries

Review against these project boundaries:

- BFF remains the primary production-oriented path.
- SPA remains a supported reference/demo mode.
- Reusable package behavior should remain provider-agnostic by default.
- Provider-specific behavior should remain in sample or integration code unless a reusable abstraction was explicitly approved.
- Public package APIs and documented BFF contracts should remain compatible unless a breaking change was explicitly approved.
- An `oidc-starter-agent` finding is structured input requiring classification, not automatic proof that starter behavior is wrong.
- Do not reopen intentionally solved hardening areas without task-specific evidence.

## Audit-Finding Classification

When the review is driven by an `oidc-starter-agent` finding, classify it before judging the implementation as valid, partially valid, stale or false positive, or unclear and requiring more evidence. Determine whether the appropriate response belongs to starter runtime code, starter tests, starter documentation, the detector/rule in `oidc-starter-agent`, or no code change.

Do not recommend modifying both repositories unless explicitly requested. Never implement either change while using this skill.

## No Modifications

While this skill is active, do not edit, create, delete, move, rename, or reformat files. Do not apply fixes, update tests or documentation, update `/ai/current-status.md`, update skills or `/AGENTS.md`, stage files, or create commits, tags, branches, pushes, releases, or package publications. Read-only inspection is allowed within the identified scope.

## Validation Assessment

Determine what validation the implementation author claims. Distinguish validation confirmed by available output, validation claimed but not independently evidenced, and validation not performed. Inspect directly relevant tests when needed; passing tests do not prove correctness, and missing execution does not automatically make a change wrong.

Identify the smallest targeted validation still needed before commit. Use commands supported by README files, project files, package scripts, or repository documentation; do not invent commands.

Do not run builds, tests, audits, restores, package commands, or broad validation unless the invoking prompt explicitly authorizes execution. The operator normally performs validation manually. Do not recommend the full solution build or full test suite unless shared infrastructure changed, package-wide compatibility requires it, the task is a final handoff, or the prompt explicitly requests it.

## Findings and Verdict

Report only actionable findings supported by the reviewed change. Use these severities:

- Critical: immediate security compromise, destructive behavior, severe data exposure, or unusable public package.
- High: likely runtime failure, security regression, breaking public contract, or major behavioral defect.
- Medium: meaningful correctness, compatibility, maintainability, or test-gap issue needing attention before or shortly after commit.
- Low: limited-risk issue worth correcting but not normally blocking alone.

For each finding include severity, concise title, affected file and line or smallest useful location, what is wrong, why it matters, the smallest suggested correction, and missing validation when applicable. Order findings highest to lowest severity. Do not inflate severity, manufacture findings, report cosmetic preferences without material risk, or duplicate the same root cause. Separate blocking findings from non-blocking observations. If no actionable findings exist, say so directly.

Finish with exactly one verdict:

- `Ready to commit`
- `Ready to commit with non-blocking notes`
- `Not ready to commit`
- `Insufficient evidence for a verdict`

Use `Ready to commit` when no actionable issues or material validation gaps remain. Use `Ready to commit with non-blocking notes` only for clearly non-blocking observations. Use `Not ready to commit` for blocking correctness, security, compatibility, scope, or test issues. Use `Insufficient evidence for a verdict` when the target, required context, or validation evidence is materially incomplete.

## Stop Conditions

Stop and report without completing a normal review when the repository is not `oidc-starter`, the target is missing or ambiguous, required contracts cannot be read, instructions conflict, the task asks for implementation or file modification, the task targets `oidc-starter-agent`, broad scanning is required, or the review is materially larger than the prompt suggests.

## Final Response

Return one concise, copyable Markdown block with no nested fenced code blocks. Use this structure:

- `Review Verdict`
- `Review Target`
- `Findings`
- `Validation Assessment`
- `Residual Risks or Non-Blocking Notes`

Include the reviewed target, files or range inspected, precise findings, validation run or not run, exact targeted operator commands, and excluded unrelated changes. When there are no findings, explicitly state: `No actionable findings identified in the reviewed scope.`

Do not include full diffs, long logs, repeated repository background, large code excerpts, generic best-practice lists, speculative architecture changes, or unrelated recommendations.
