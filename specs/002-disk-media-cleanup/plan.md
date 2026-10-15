# Implementation Plan: Disk-Only Media Cleanup

**Branch**: `002-disk-media-cleanup` | **Date**: 2026-09-18 | **Spec**: [spec.md](./spec.md)

**Input**: Feature specification from `specs/002-disk-media-cleanup/spec.md`

## Summary

Add a second, independent audit mode to the existing Media Audit dashboard: a disk scan that
enumerates every file in Umbraco's configured media file system and reports every one that isn't
the current `umbracoFile` value of any Umbraco media item (active or trashed). Administrators can
delete selected files, or all of them, directly from disk in one confirmed, logged action. This is
additive to the existing Used/Unused relation audit, not a replacement - it answers a different
question ("what's on disk Umbraco doesn't know about" vs. "what does Umbraco know about that isn't
referenced by content").

## Technical Context

**Language/Version**: C# / .NET 10 (server), TypeScript (backoffice client) - same as the rest of
this package.

**Primary Dependencies**: Umbraco CMS v17 core services (`IMediaService`, `MediaFileManager` /
`IFileSystem`), matching the existing package's dependency set. No new third-party packages.

**Storage**: Whatever `IFileSystem` the site's media file system is configured with (local disk by
default). The existing package-owned Deletion Log table is reused as-is (no schema change - see
Data Model).

**Testing**: NUnit unit tests (Moq-based, matching `MediaAuditServiceTests.cs`'s style) plus an
`UmbracoIntegrationTest`-based integration test that writes a real file into the media file system
via `MediaFileManager.FileSystem.AddFile` and asserts it round-trips through scan → list → delete.

**Target Platform**: Umbraco v17 backoffice (server-side package + backoffice extension), same as
the rest of this package.

**Project Type**: Umbraco CMS package (single project, matching the existing structure).

**Performance Goals**: Same qualitative bar as the existing usage audit - runs as a background
operation with a polled "Running" status rather than blocking the dashboard, acceptable for
media libraries in the tens of thousands of files.

**Constraints**: Must never delete a file that is the current `umbracoFile` (or image-cropper
`src`) of any existing media item, active or trashed (FR-002, FR-011, SC-003). Must re-verify each
file immediately before deleting it (FR-008).

**Scale/Scope**: One new server-side scan/delete pipeline, one new controller surface (folded into
the existing `UmbracoMediaAuditApiController`), and one new backoffice panel added to the existing
dashboard (not a separate dashboard).

## Constitution Check

*GATE: Must pass before Phase 0 research. Re-check after Phase 1 design.*

- **I. Umbraco Package & Platform Standards**: Reuses the existing composer/controller/package
  structure; no new manifest or section registration needed since this lives inside the existing
  Media Audit dashboard entry. PASS.
- **II. Documentation-Driven, Verified Assumptions**: `IFileSystem` (`GetDirectories`, `GetFiles`,
  `FileExists`, `DeleteFile`, `GetUrl`, `GetLastModified`, `GetSize`) and `MediaFileManager`
  (`.FileSystem` property, DI-resolvable as a concrete singleton registered by Umbraco core)
  verified against the official Umbraco C# API docs during planning (see Research). `GetFiles`/
  `GetDirectories` are documented as operating on the given path only (not implicitly recursive),
  so the scanner walks the tree itself rather than assuming either call recurses - this specific
  behavior is re-verified by the integration test (Phase 1), not just documentation, per this
  principle's "verified via test" requirement. PASS.
- **III. Backoffice UI Consistency**: New panel is a Lit/TypeScript custom element using UUI
  components, registered the same way the existing dashboard/detail/confirm elements are (manifest
  + element API), not a new UI framework. PASS.
- **IV. Standardized Build Tooling**: No build tooling changes - same Vite config, same `Client`
  project. PASS.
- **V. Minimal, Self-Documenting Code**: New services/models follow the existing file-per-type,
  `///`-doc-commented-public-surface convention; no inline "why" comments beyond what's needed for
  non-obvious tree-walking logic. PASS.

No violations - Complexity Tracking is not needed.

## Project Structure

### Documentation (this feature)

```text
specs/002-disk-media-cleanup/
├── plan.md              # This file
├── research.md          # Phase 0 output
├── data-model.md         # Phase 1 output
├── contracts/
│   └── disk-media-api.md
└── tasks.md              # Phase 2 output (/speckit-tasks)
```

### Source Code (repository root)

```text
src/ProWorks.Umbraco.MediaAudit/
├── Services/
│   ├── IDiskMediaScanner.cs          # walks IFileSystem, returns every file's relative path + metadata
│   ├── DiskMediaScanner.cs
│   ├── IDiskMediaAuditService.cs     # orchestrates scan, diffs against known media file paths, holds results
│   ├── DiskMediaAuditService.cs
│   ├── IDiskMediaDeleteService.cs    # deletes selected/all disk-only files, re-verifying, logging
│   └── DiskMediaDeleteService.cs
├── Models/
│   ├── DiskMediaItem.cs
│   ├── DiskAuditRun.cs
│   ├── DiskMediaItemsResult.cs
│   └── DiskMediaDeleteResult.cs
├── Controllers/UmbracoMediaAuditApiController.cs   # + disk-scan/disk-items/disk-delete actions
└── Composers/UmbracoMediaAuditApiComposer.cs       # + new service registrations

src/ProWorks.Umbraco.MediaAudit/Client/src/
├── api/media-audit.repository.ts                   # + disk scan/list/delete calls & types
└── dashboard/
    ├── media-audit-disk-panel.element.ts            # new: toggled panel with list + selection + toolbar
    └── media-audit-disk-delete-confirm.element.ts   # new: permanent-delete confirmation

tests/ProWorks.Umbraco.MediaAudit.Tests.Unit/
├── DiskMediaScannerTests.cs
├── DiskMediaAuditServiceTests.cs
└── DiskMediaDeleteServiceTests.cs

tests/ProWorks.Umbraco.MediaAudit.Tests.Integration/
└── DiskMediaCleanupIntegrationTests.cs

tests/ProWorks.Umbraco.MediaAudit.Client.Tests/src/dashboard/
├── media-audit-disk-panel.element.test.ts
└── media-audit-disk-delete-confirm.element.test.ts
```

**Structure Decision**: Extends the existing single-project package structure exactly - no new
projects, dashboards, or sections. The disk-audit feature is additive files inside the same
`Services`/`Models`/`Controllers`/`Client` folders the existing usage audit already uses, plus one
new toggled panel in the existing dashboard element (same pattern as the existing Deletion Log
toggle).

## Complexity Tracking

No constitution violations - not applicable.
