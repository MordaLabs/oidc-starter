# Token Budget

## Purpose

This file defines default limits for repository scanning, test execution, documentation updates, and output size.

The goal is to keep Codex work in this repository:

- focused,
- low-cost in token usage,
- minimal in scope,
- and easy to review.

## Default Reading Budget

Always follow `/ai/agent-rules.md` when this file is referenced.

Start with the smallest relevant file set.

For most implementation tasks, first read:

- 1 to 3 directly relevant code files,
- 0 to 2 directly relevant test files,
- 0 to 1 documentation files if the task explicitly involves docs or consumer-visible behavior.

Do not read the whole repository by default.

Do not open all `/ai/*.md` files by default.
Open additional `/ai/*.md` files only when needed for the current task.

Always use the current prompt to narrow the file set first.

If more files are needed beyond the initial budget, explain why and keep the expanded scope explicit and minimal. Ask for guidance before expanding broadly.

## Audit Finding Workflow Budget

For findings coming from `oidc-starter-agent`, use this workflow:

1. Read the finding carefully.
2. Inspect only the directly related implementation files first.
3. Classify the finding as:
   - valid,
   - partially valid,
   - stale / false positive.
4. Only after classification decide whether the right action is:
   - code fix,
   - test update,
   - documentation clarification,
   - or no change in `oidc-starter`.

Do not start with a fix before classification.

Prefer one finding = one small task.

## Implementation Budget

Prefer:

- one small fix,
- one focused test update,
- one short explanation.

Avoid:

- broad refactors,
- unrelated cleanup,
- package-wide redesigns,
- touching multiple areas unless the task clearly requires it.

If the smallest correct fix affects both package and sample, keep changes minimal in each place.

## Testing Budget

Prefer the smallest relevant verification step.

Rules:

- If only documentation changed, do not run builds or tests by default.
- If a focused backend package behavior changed, run only the directly relevant .NET test project or targeted tests.
- If a focused frontend package behavior changed, run only the smallest relevant frontend/package validation available.
- If a sample-only behavior changed, run only the smallest relevant sample verification.
- Do not run the full solution test suite by default.
- Do not run full solution build/test unless:
  - explicitly requested,
  - shared package contracts changed,
  - shared infrastructure changed,
  - or final validation for a larger implementation task requires it.

If the user says they will run tests manually, do not run tests.
Provide the exact recommended command instead.

When tests are not run, say so explicitly.

## Documentation Budget

Do not update documentation by default.
Do not proactively update package versions or release notes during implementation tasks unless explicitly requested.

Only update docs when:

- the task explicitly requests it,
- the change introduces a new consumer-visible integration contract,
- the change introduces or documents a breaking behavior,
- the change affects security behavior consumers must know,
- or the task is specifically a documentation task.

Prefer updating the smallest relevant doc:

- package README for package contract changes,
- `docs/package-release.md` for post-release hardening notes,
- root README only when repository-level positioning or usage changed.

## Output Budget

Keep the final response concise.

For implementation tasks, the default final response should include only:

- status,
- changed files,
- tests run or not run,
- finding classification if relevant,
- short notes.

Do not include:

- full diffs,
- long logs,
- repeated explanations,
- large code excerpts unless explicitly requested.

## Escalation Rules

Before exceeding the normal token budget, explicitly note why.

Escalate only when:

- the task may require a breaking change,
- the task may affect public package API,
- the task cannot be solved without scanning a much larger area,
- the task requires both package and sample changes across multiple boundaries,
- the task reveals a broader architectural issue,
- or the user explicitly asks for a larger review/refactor.

When escalation is needed:

- say what broader area must be inspected,
- keep the scope bounded,
- and do not expand silently.

## Success Criteria

A good Codex task outcome in this repository is usually:

- one clearly classified finding or issue,
- one minimal fix or explicit no-fix conclusion,
- focused tests where needed,
- minimal docs only when justified,
- concise reporting.
