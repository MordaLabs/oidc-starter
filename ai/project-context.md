# Project Context

## Repository Purpose

`oidc-starter` is a public reference implementation and reusable starter for OpenID Connect with ASP.NET Core, Angular, and a local Keycloak setup.

The primary production-oriented direction in this repository is the backend-for-frontend (BFF) model. The SPA mode is retained as a supported reference/demo mode, but BFF is the default direction for hardening and package evolution.

Do not remove, weaken, or break SPA mode unless the task explicitly targets SPA cleanup or deprecation.

## Main Deliverables

This repository contains:

- `src/OidcStarter.AspNetCore.Bff`  
  Source of the reusable ASP.NET Core backend package `OidcStarter.AspNetCore.Bff`.

- `src/frontend/projects/oidc-starter-auth`  
  Source of the reusable Angular auth package `@mordalabs/oidc-starter-auth`.

- `src/backend`  
  Sample ASP.NET Core backend host that consumes the reusable backend package.

- `src/frontend`  
  Sample Angular app that consumes the reusable frontend package.

- `src/OidcStarter.AspNetCore.Bff.Tests`  
  Automated tests for the reusable backend package.

- `infra/keycloak`  
  Local development Keycloak setup with automated realm import.

## Package vs Sample Boundaries

Treat the reusable package sources as the primary source of truth:

- backend package source of truth: `src/OidcStarter.AspNetCore.Bff`
- frontend package source of truth: `src/frontend/projects/oidc-starter-auth`

Treat the following as sample/demo consumers:

- `src/backend`
- `src/frontend`

Use the sample apps to demonstrate package behavior, local development setup, and provider-specific examples.

Do not move provider-specific behavior into reusable packages unless explicitly requested.

Example:

- a Keycloak-specific role mapper belongs in the sample/backend integration layer unless the task explicitly requires a generic provider recipe in the reusable package.

## Supported Auth Modes

- `bff` = primary path for production-oriented usage and hardening
- `spa` = supported reference/demo mode

Unless a task explicitly targets SPA mode, prefer to reason about the repository through the BFF architecture.

## Compatibility Priorities

Preserve compatibility for:

- the NuGet package public API,
- the Angular package public API,
- documented BFF endpoints,
- local sample startup flow,
- Keycloak demo setup.

Breaking changes require explicit approval.

## Local Infrastructure

`infra/keycloak` is local/dev-only infrastructure used to support the sample apps and package verification workflow.

It includes:

- local realm import
- sample clients
- sample user
- sample roles

Do not treat local Keycloak setup as production identity guidance.

## Relation to oidc-starter-agent

`oidc-starter-agent` is a separate repository that audits this starter.

Treat findings from `oidc-starter-agent` as structured input for small implementation tasks, not as automatic proof that the current starter is wrong.

For each finding, first classify it as one of:

- valid
- partially valid
- stale / false positive

Only then decide whether to:

- fix code
- add/update tests
- clarify documentation
- or leave starter behavior unchanged.

## Change Philosophy

When working in this repository:

- prefer small, safe, local fixes
- preserve public package compatibility unless explicitly told otherwise
- preserve stable package contracts
- keep reusable packages generic where possible
- keep sample apps focused on demonstrating package use
- prefer package-level fixes over sample-only fixes when the issue belongs to reusable behavior
- prefer sample-only fixes when the issue is provider-specific or demo-specific

## Non-goals

This repository is not an AI/LLM integration project.

Do not add:

- LLM, OpenAI, Azure OpenAI, Semantic Kernel, LangChain, or MCP integrations,
- custom identity provider implementations,
- production Keycloak deployment guidance,
- broad framework abstractions,
- unrelated authentication modes,
- broad security redesigns unless explicitly requested.
