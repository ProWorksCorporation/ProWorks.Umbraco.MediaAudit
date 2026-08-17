<!--
Sync Impact Report
- Version change: (template, unratified) → 1.0.0
- Modified principles: n/a (initial ratification)
- Added sections:
  - Core Principles: I. Umbraco Package & Platform Standards
  - Core Principles: II. Documentation-Driven, Verified Assumptions (NON-NEGOTIABLE)
  - Core Principles: III. Backoffice UI Consistency (UUI, Web Components, Lit, TypeScript)
  - Core Principles: IV. Standardized Build Tooling (Vite)
  - Technology Stack Requirements
  - Development Workflow & Quality Gates
  - Governance
- Removed sections: none (first concrete ratification of a previously placeholder-only file)
- Templates requiring updates:
  - .specify/templates/plan-template.md ⚠ pending manual review (verify its Constitution Check section references these 4 principles by name)
  - .specify/templates/spec-template.md ✅ no principle-specific references, no changes required
  - .specify/templates/tasks-template.md ⚠ pending manual review (verify task categorization allows for "docs/research" and "UUI/Lit component" task types)
- Follow-up TODOs:
  - TODO(RATIFICATION_DATE): Confirmed as the date this constitution was first adopted (2026-08-17). Update only if an earlier, unrecorded adoption date is later identified.
-->

# Umbraco.MediaAudit Constitution

## Core Principles

### I. Umbraco Package & Platform Standards

Every part of this package MUST conform to official Umbraco package conventions and to Umbraco's
general platform standards for the version being targeted (Umbraco v17). This includes, at minimum:
package/plugin manifest structure, composer/dependency registration, backoffice section/tree/menu
registration patterns, notification handler conventions, and versioning/compatibility declarations
as documented by Umbraco HQ. Deviating from a documented Umbraco convention requires an explicit,
recorded rationale (see Governance) rather than silent divergence. Rationale: consistency with
platform conventions is what keeps the package installable, upgradeable, and maintainable across
Umbraco releases, and keeps it recognizable to Umbraco developers who did not write it.

### II. Documentation-Driven, Verified Assumptions (NON-NEGOTIABLE)

Before implementing against any Umbraco API, backoffice extension point, or UI Library component,
the current official Umbraco documentation (and, where documentation is silent or ambiguous, the
Umbraco source/reference implementation) MUST be consulted. When behavior, an API contract, or a
requirement is uncertain and cannot be confidently resolved from documentation or source, the
correct action is to ask a clarifying question rather than proceed on an assumption. Assumptions
that are unavoidable (e.g., no documentation exists for an edge case) MUST be explicitly recorded
and validated against real behavior (e.g., via a spike, test, or manual verification in a running
Umbraco v17 instance) before being relied upon in shipped functionality. Undocumented, unverified
guesses MUST NOT be treated as facts in specs, plans, or code comments. Rationale: Umbraco's APIs
and backoffice extension model evolve across versions; guessing produces subtly broken integrations
that only surface after release, while verified, documented decisions are traceable and defensible.

### III. Backoffice UI Consistency (Umbraco UI Library, Web Components, Lit, TypeScript)

All backoffice-facing UI MUST be built using the Umbraco UI Library (UUI) component set as the
default building blocks, implemented as native Web Components using Lit, and authored in
TypeScript. Custom elements MUST follow Umbraco's backoffice extension registration patterns
(manifests, element APIs) rather than ad hoc DOM manipulation or non-standard frameworks. Introducing
a UI approach outside UUI/Lit/TypeScript (e.g., a different component framework, plain JavaScript,
or bespoke styling that bypasses UUI design tokens) requires explicit justification recorded in the
relevant plan's Complexity Tracking section. Rationale: matching the host backoffice's component
system and language keeps the package visually and behaviorally consistent with core Umbraco and
other packages, reduces bundle duplication, and keeps contributions approachable to Umbraco
backoffice developers.

### IV. Standardized Build Tooling (Vite)

Vite MUST be the build tool for all client-side/backoffice package assets (bundling, dev server,
TypeScript/Lit compilation, and production output). Alternative or additional bundlers MUST NOT be
introduced for the same asset pipeline without an explicit, documented reason and constitution
amendment. Rationale: a single, standardized build tool keeps local development, CI builds, and
package distribution reproducible and reduces the maintenance burden of supporting multiple
toolchains for the same output.

## Technology Stack Requirements

- **Target platform**: Umbraco CMS v17 backoffice (server-side package + backoffice extension).
- **Backoffice UI**: Umbraco UI Library (UUI) components, implemented as Lit-based Web Components.
- **Language**: TypeScript for all backoffice/client code; C#/.NET conventions matching the Umbraco
  version's supported runtime for any server-side package code.
- **Build tool**: Vite for all backoffice asset bundling and development workflows.
- **Documentation of record**: The official Umbraco documentation (and Umbraco source where
  documentation is incomplete) for the specific Umbraco v17 release in use is the source of truth
  for API contracts, extension points, and UI Library usage.

## Development Workflow & Quality Gates

- Before starting implementation work on any feature, the relevant Umbraco v17 documentation for the
  APIs/extension points involved MUST be identified and referenced in the plan or task notes.
- Any point where documentation is unclear, contradictory, or silent MUST be surfaced as a question
  (to the user, or as a `[NEEDS CLARIFICATION]`/TODO marker in the relevant spec/plan) rather than
  resolved by silent assumption.
- Code reviews (self- or peer-performed) MUST check for: adherence to Umbraco package standards,
  correct use of UUI/Lit/TypeScript for backoffice UI, Vite as the sole build tool for client assets,
  and that any deviation from these principles is explicitly justified in writing.
- Assumptions that were validated during implementation (e.g., "confirmed via local Umbraco v17
  instance that X behaves as Y") SHOULD be captured in the plan or PR description so future
  contributors do not have to re-verify the same behavior from scratch.

## Governance

This constitution supersedes ad hoc conventions for this repository. Amendments require:

1. A documented rationale for the change (what is changing and why).
2. An explicit version bump following semantic versioning:
   - **MAJOR**: Backward-incompatible governance changes, or removal/redefinition of a principle.
   - **MINOR**: A new principle or materially expanded guidance is added.
   - **PATCH**: Clarifications, wording fixes, or non-semantic refinements.
3. Updating the Sync Impact Report at the top of this file and, where applicable, updating dependent
   templates (`plan-template.md`, `spec-template.md`, `tasks-template.md`) to stay consistent.

All plans and PRs MUST verify compliance with this constitution; unresolved deviations MUST be
recorded in the plan's Complexity Tracking section with a justification rather than silently ignored.
Complexity or deviation from these principles must be justified by a concrete constraint (e.g., a
genuine Umbraco platform limitation), not convenience.

**Version**: 1.0.0 | **Ratified**: 2026-08-17 | **Last Amended**: 2026-08-17
