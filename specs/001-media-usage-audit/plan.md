# Implementation Plan: Media Usage Audit Dashboard

**Branch**: `001-media-usage-audit` | **Date**: 2026-08-17 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `/specs/001-media-usage-audit/spec.md`

**Note**: This template is filled in by the `/speckit-plan` command; its definition describes the execution workflow.

## Summary

A marketplace-deployable Umbraco v17 package that adds a backoffice dashboard auditing every media
library item as "Used" or "Unused," lets an editor drill into exactly which content references a
given "Used" item, and lets an administrator safely bulk-delete confirmed-"Unused" items. Usage
detection is a hybrid of Umbraco's native relation-tracking (fast, primary signal, confirmed to cover
Media Picker/RTE/Block List/Block Grid including nested references, across every configured
language/culture on variant sites) and a scan-based safety net adapted from a proven Python reference
implementation (used for deep verification and as a mandatory pre-delete/pre-purge re-check), so a
stale or untracked relation can never cause a still-used file to be deleted. Deletion is two-step and
admin-only: a safe default (move to Recycle Bin) plus a separate, more strongly-confirmed purge action
for admins who want disk space reclaimed immediately; every delete/purge action is recorded as one
batch-level log entry for accountability. Member data and multi-site auditing are explicitly out of
scope for this version. Full technical rationale is in [research.md](./research.md), updated after a
`/speckit-clarify` session that refined the delete/purge/logging/scope decisions below.

## Technical Context

**Language/Version**: C# / .NET 10 (server package code); TypeScript (strict) for backoffice client code — .NET 10 is fixed by the Umbraco v17 LTS target (research.md §1)

**Primary Dependencies**: `Umbraco.Cms` v17.x (IMediaService, IContentService, IRelationService, IDataValueReference infrastructure); `@umbraco-cms/backoffice` package + Lit for backoffice UI; Umbraco UI Library (UUI) components; Vite as build tool (constitution Principles III & IV; research.md §2–3)

**Storage**: Almost entirely schema-less — reads through Umbraco's existing persistence APIs (`IMediaService`, `IContentService`, `IRelationService`) against whatever database the host Umbraco v17 site already uses (SQLite in dev, SQL Server or another supported RDBMS in production); audit results are computed on demand and not persisted (spec Assumptions). **One exception**: a single new package-owned table for `DeletionLogEntry` (research.md §10), introduced via a Package Migration (Umbraco's own documented mechanism) — one row per delete/purge batch, not per item

**Testing**: xUnit for server-side unit + integration tests (integration tests run against a seeded local Umbraco v17 SQLite instance); Web Test Runner for Lit component tests (research.md §7)

**Target Platform**: Umbraco v17 backoffice — ASP.NET Core / .NET 10 host, modern evergreen browsers supporting the Umbraco 17 backoffice SPA

**Project Type**: Umbraco CMS package — Razor Class Library (server) + Vite/Lit/TypeScript backoffice extension (client), distributed as a NuGet package tagged for the Umbraco Marketplace (research.md §2)

**Performance Goals**: Full audit of up to 10,000 media items completes and displays results within 60 seconds (spec SC-002); relation-based classification is the default fast path specifically to hit this target, with the more expensive scan-based check reserved for smaller candidate sets (research.md §4)

**Constraints**: No new persistent database schema beyond the single deletion-log table noted above; MUST NOT introduce a UI approach outside UUI/Lit/TypeScript or a bundler other than Vite without a constitution amendment; NuGet package MUST carry the `umbraco-marketplace` tag and required metadata (research.md §2); bulk delete MUST re-verify each item immediately before deleting, and purge MUST re-verify each item is still trashed immediately before permanently removing it (spec edge cases; data-model.md validation rules); purge MUST be scoped to selected items via per-item `Delete()`, never the untargeted `EmptyRecycleBin()` (research.md §5); every delete/purge action MUST write exactly one log entry, never one per item (FR-019)

**Scale/Scope**: Single Umbraco site/installation (spec Assumptions); media libraries up to at least 10,000 items (SC-002); page/document content only — Member data explicitly out of scope (FR-002, research.md §9); all configured languages/cultures must be scanned on variant sites (FR-017, research.md §8); 3 prioritized user stories (unused list, usage detail, filter/sort/export) plus admin-only delete + purge (FR-014/015/018) and batch-level deletion logging (FR-019)

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

| Principle | Status | Evidence |
|---|---|---|
| I. Umbraco Package & Platform Standards | **PASS** | RCL + `umbraco-package.json` manifest + App_Plugins convention + NuGet marketplace tag/metadata (research.md §2); usage detection, delete, and purge all route through Umbraco's own service APIs rather than bypassing them (research.md §4–5); the one new table (deletion log) is introduced via Umbraco's own documented Package Migration mechanism, not an ad hoc schema change (research.md §10) |
| II. Documentation-Driven, Verified Assumptions (NON-NEGOTIABLE) | **PASS** | .NET version, package structure, relation-tracking coverage (incl. source-level confirmation for Block List/Block Grid), draft-save behavior, and the `Delete()` vs. `EmptyRecycleBin()` distinction (research.md §5) were all verified against official docs/source rather than assumed; the one item that could not be confirmed (Nested Content coverage) is explicitly flagged as a residual risk with a documented mitigation, not silently assumed away |
| III. Backoffice UI Consistency (UUI, Lit, TypeScript) | **PASS** | Dashboard/detail/delete-confirmation/purge-confirmation/deletion-log UI specified as Lit web components using UUI, in TypeScript (research.md §3) |
| IV. Standardized Build Tooling (Vite) | **PASS** | Vite is the sole build tool for the client extension, per the official Vite Package Setup pattern (research.md §2) |

No violations — Complexity Tracking is not required for this plan.

**Re-check after Phase 1 design (updated post-clarify)**: Confirmed — the hybrid relation+scan
detection design (research.md §4), the two-step delete/purge design (research.md §5), the
per-culture scan requirement (research.md §8), the Member-data exclusion (research.md §9), and the
new deletion-log table (research.md §10) — together with the data-model/contracts derived from them —
do not introduce any new principle violation. The deletion-log table is the only new schema in this
feature; it is justified by FR-019 (an accountability record must outlive a session) and delivered
through Umbraco's own package-migration mechanism, so it is documented as a research decision rather
than listed as a Complexity Tracking violation.

## Project Structure

### Documentation (this feature)

```text
specs/001-media-usage-audit/
├── plan.md              # This file (/speckit-plan command output)
├── research.md          # Phase 0 output (/speckit-plan command)
├── data-model.md        # Phase 1 output (/speckit-plan command)
├── quickstart.md        # Phase 1 output (/speckit-plan command)
├── contracts/           # Phase 1 output (/speckit-plan command)
│   └── media-audit-api.md
├── reference/           # Reference material — not part of the shipped package
│   └── media_audit.py   # User-supplied Python CLI this feature's detection logic is ported/validated from
├── checklists/
│   └── requirements.md
└── tasks.md             # Phase 2 output (/speckit-tasks command - NOT created by /speckit-plan)
```

### Source Code (repository root)

> **Note**: `UmbracoMediaAudit` below is a Razor Class Library (RCL), not a C#-vs-JavaScript choice —
> the dashboard UI itself is still TypeScript/Lit built by Vite (see `Client/` below). The RCL is the
> .NET project type that lets `dotnet pack` embed those built JS/CSS files (from `wwwroot`) together
> with this feature's C# server code into one NuGet package, so a client can install the whole thing
> via NuGet with nothing to copy by hand. See research.md §2 for the full rationale.

```text
src/
├── UmbracoMediaAudit/                        # Shippable RCL — the NuGet/Marketplace package
│   ├── UmbracoMediaAudit.csproj              # Marketplace metadata: Title/Description/Version/Authors/
│   │                                          # PackageProjectUrl/PackageLicenseExpression, umbraco-marketplace tag
│   ├── Composers/
│   │   └── MediaAuditComposer.cs             # Registers services, API controllers, package migration
│   ├── Constants.cs
│   ├── Controllers/
│   │   └── MediaAuditApiController.cs        # Implements contracts/media-audit-api.md (incl. /purge, /deletion-log)
│   ├── Migrations/
│   │   └── AddDeletionLogTablePlan.cs        # PackageMigrationPlan creating the DeletionLogEntry table (research.md §10)
│   ├── Services/
│   │   ├── IMediaAuditService.cs             # Orchestrates relation-based classification (data-model.md), per-culture (§8)
│   │   ├── MediaAuditService.cs
│   │   ├── IMediaReferenceScanner.cs         # Scan-based safety net (research.md §4), ported from reference/media_audit.py
│   │   ├── MediaReferenceScanner.cs          # Iterates Property.GetValue(culture, segment) per configured language (§8)
│   │   ├── MediaDeleteService.cs             # Admin-only delete (move to Recycle Bin) with pre-delete re-check
│   │   ├── MediaPurgeService.cs              # Admin-only purge: per-item Delete(), not EmptyRecycleBin() (research.md §5)
│   │   └── IDeletionLogService.cs / DeletionLogService.cs  # Writes/reads DeletionLogEntry (one row per batch, FR-019)
│   ├── Models/
│   │   ├── MediaAuditItem.cs
│   │   ├── MediaUsageReference.cs            # Includes Culture (§8)
│   │   ├── AuditRun.cs
│   │   ├── MediaFolder.cs
│   │   └── DeletionLogEntry.cs
│   ├── Client/                               # Vite/Lit/TypeScript source — NOT shipped as-is, built into wwwroot
│   │   ├── src/
│   │   │   ├── dashboard/
│   │   │   │   ├── media-audit-dashboard.element.ts
│   │   │   │   ├── media-audit-detail.element.ts
│   │   │   │   ├── media-audit-delete-confirm.element.ts
│   │   │   │   ├── media-audit-purge-confirm.element.ts   # Separate, more strongly-worded confirmation (FR-018)
│   │   │   │   └── media-audit-deletion-log.element.ts     # Admin-only log view (FR-019, GET /deletion-log)
│   │   │   ├── api/
│   │   │   │   └── media-audit.repository.ts
│   │   │   ├── manifests.ts
│   │   │   └── index.ts
│   │   ├── public/
│   │   │   └── umbraco-package.json          # Extension manifest (research.md §2)
│   │   ├── package.json
│   │   ├── tsconfig.json
│   │   └── vite.config.ts
│   └── wwwroot/                              # Vite build output — embedded as static web assets on `dotnet pack`
│       └── App_Plugins/UmbracoMediaAudit/
│
└── UmbracoMediaAudit.Web/                    # Sample Umbraco v17 site for local dev + quickstart.md validation

tests/
├── UmbracoMediaAudit.Tests.Unit/             # xUnit — classification, scanner, delete-safety logic
├── UmbracoMediaAudit.Tests.Integration/      # xUnit — against seeded local Umbraco v17 SQLite instance
└── UmbracoMediaAudit.Client.Tests/           # Web Test Runner — Lit element tests
```

**Structure Decision**: Single Umbraco package project (RCL) following the Vite-Package-Setup pattern
from research.md §2 — this is a self-contained backoffice add-on, not a frontend/backend split web
application, so the "web application" structure option doesn't apply. The RCL's `Client/` folder holds
the Vite/Lit/TypeScript source; its build output goes straight into the same project's `wwwroot` so
`dotnet pack` embeds it automatically for Marketplace distribution. `UmbracoMediaAudit.Web` is a
throwaway sample site (standard Umbraco template) used only for local development and the
quickstart.md validation scenarios — it is not part of what ships. The reference Python script lives
under the spec's own `reference/` folder as research material/cross-validation tool (quickstart.md §8),
not as a runtime dependency of the shipped package.

## Complexity Tracking

> **Fill ONLY if Constitution Check has violations that must be justified**

*No entries — the Constitution Check above found no violations requiring justification.*
