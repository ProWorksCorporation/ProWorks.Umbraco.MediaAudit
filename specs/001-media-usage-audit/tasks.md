---

description: "Task list template for feature implementation"
---

# Tasks: Media Usage Audit Dashboard

**Input**: Design documents from `/specs/001-media-usage-audit/`

**Prerequisites**: [plan.md](./plan.md), [spec.md](./spec.md), [research.md](./research.md), [data-model.md](./data-model.md), [contracts/media-audit-api.md](./contracts/media-audit-api.md), [quickstart.md](./quickstart.md)

**Tests**: Not explicitly requested in the feature specification — no per-story TDD test tasks are included below. Test *infrastructure* (xUnit projects, Web Test Runner config) is still scaffolded in Setup because plan.md's Technical Context commits to that testing approach, and dedicated test-writing tasks appear in the Polish phase as an explicit, opt-in addition.

**Organization**: Tasks are grouped by user story from spec.md (US1–US3, in priority order), plus a fourth group (US4) for the admin delete/purge/logging capability. US4 is not a separately numbered story in spec.md's User Scenarios section, but FR-014/015/018/019 plus quickstart.md scenarios 5–7 make it a distinct, independently-testable increment in its own right — it is called out as its own phase here for that reason, not because spec.md numbered it.

## Format: `[ID] [P?] [Story] Description`

- **[P]**: Can run in parallel (different files, no dependencies)
- **[Story]**: Which user story this task belongs to (US1, US2, US3, US4)
- Every task includes an exact file path and cites the requirement(s)/contract(s) it satisfies

## Path Conventions

Per plan.md's Project Structure — single Umbraco package project (RCL), not a frontend/backend split:

- Server (C#): `src/UmbracoMediaAudit/`
- Backoffice client (TypeScript/Lit/Vite): `src/UmbracoMediaAudit/Client/`
- Sample site for local dev/validation: `src/UmbracoMediaAudit.Web/`
- Tests: `tests/UmbracoMediaAudit.Tests.Unit/`, `tests/UmbracoMediaAudit.Tests.Integration/`, `tests/UmbracoMediaAudit.Client.Tests/`

---

## Phase 1: Setup (Shared Infrastructure)

**Purpose**: Project scaffolding — nothing here is feature logic yet.

- [X] T001 Create the repository project structure per plan.md's Project Structure: `src/UmbracoMediaAudit/` (RCL, with `Composers/`, `Constants.cs`, `Controllers/`, `Migrations/`, `Services/`, `Models/`, `Client/`, `wwwroot/` subfolders) and `src/UmbracoMediaAudit.Web/`
- [X] T002 [P] Initialize Vite + TypeScript + Lit config in `src/UmbracoMediaAudit/Client/` — `package.json`, `tsconfig.json` (with `@umbraco-cms/backoffice/extension-types` in `compilerOptions.lib`), and `vite.config.ts` (`outDir: "wwwroot"`, `base: "/App_Plugins/UmbracoMediaAudit/"`, `@umbraco-cms/*` externalized) per research.md §2
- [X] T003 [P] Create the extension manifest skeleton `src/UmbracoMediaAudit/Client/public/umbraco-package.json` (empty `extensions` array, package `name`/`version`) per research.md §2
- [X] T004 [P] Configure `src/UmbracoMediaAudit/UmbracoMediaAudit.csproj` with Marketplace metadata (`Title`, `Description`, `Version`, `Authors`, `PackageProjectUrl`, `PackageLicenseExpression`) and the `umbraco-marketplace` NuGet tag per research.md §2
- [X] T005 [P] Scaffold `src/UmbracoMediaAudit.Web/` as a standard Umbraco v17 template site that project-references `UmbracoMediaAudit`, for local dev and quickstart.md validation
- [X] T006 [P] Scaffold xUnit test projects `tests/UmbracoMediaAudit.Tests.Unit/` and `tests/UmbracoMediaAudit.Tests.Integration/` (the latter configured to run against a seeded local Umbraco v17 SQLite instance) per research.md §7
- [X] T007 [P] Scaffold Web Test Runner config in `tests/UmbracoMediaAudit.Client.Tests/` for Lit component tests per research.md §7

---

## Phase 2: Foundational (Blocking Prerequisites)

**Purpose**: The shared detection engine, base models, and API/UI skeleton every user story is built on.

**⚠️ CRITICAL**: No user story phase (3–6) can begin until this phase is complete.

- [X] T008 [P] Create `MediaAuditItem` model in `src/UmbracoMediaAudit/Models/MediaAuditItem.cs` per data-model.md
- [X] T009 [P] Create `MediaUsageReference` model (including the `Culture` field) in `src/UmbracoMediaAudit/Models/MediaUsageReference.cs` per data-model.md
- [X] T010 [P] Create `AuditRun` model in `src/UmbracoMediaAudit/Models/AuditRun.cs` per data-model.md
- [X] T011 [P] Create `MediaFolder` model in `src/UmbracoMediaAudit/Models/MediaFolder.cs` per data-model.md
- [X] T012 Implement `IMediaReferenceScanner`/`MediaReferenceScanner` in `src/UmbracoMediaAudit/Services/MediaReferenceScanner.cs` — the scan-based safety net ported from `reference/media_audit.py`, reading via `IContent.Properties`/`Property.GetValue(culture, segment)` **once per configured language/segment** (not just the default), matching GUID with/without hyphens and file path/filename, scoped to `IContentService` only (no Member data) per research.md §4, §8, §9 (depends on T008–T011)
- [X] T013 Implement `IMediaAuditService`/`MediaAuditService` in `src/UmbracoMediaAudit/Services/MediaAuditService.cs` — relation-based classification via `IRelationService`/`IDataValueReference` as the fast primary signal, with `MediaReferenceScanner` invoked for deep-scan mode and pre-delete/pre-purge re-checks, per research.md §4 (depends on T012)
- [X] T014 Implement the `MediaAuditApiController` skeleton (routing, auth gate requiring Media-section access per FR-013) with stub `GET /summary`, `POST /run`, `GET /items` actions in `src/UmbracoMediaAudit/Controllers/MediaAuditApiController.cs` per contracts/media-audit-api.md (depends on T013)
- [X] T015 Implement `MediaAuditComposer.cs` in `src/UmbracoMediaAudit/Composers/MediaAuditComposer.cs` registering `IMediaReferenceScanner`, `IMediaAuditService`, and the API controller in Umbraco's DI container (depends on T012–T014)
- [X] T016 [P] Create the dashboard shell element and its extension registration in `src/UmbracoMediaAudit/Client/src/dashboard/media-audit-dashboard.element.ts` and `src/UmbracoMediaAudit/Client/src/manifests.ts` (`type: "dashboard"` entry per research.md §3), plus `src/UmbracoMediaAudit/Client/src/index.ts`
- [X] T017 [P] Create the API client wrapper `src/UmbracoMediaAudit/Client/src/api/media-audit.repository.ts` covering all endpoints in contracts/media-audit-api.md

**Checkpoint**: Foundation ready — the detection engine, models, and skeleton controller/dashboard exist. User story phases can now begin.

---

## Phase 3: User Story 1 - See which media files are unused (Priority: P1) 🎯 MVP

**Goal**: An editor opens the dashboard, triggers an audit, and sees a correct Used/Unused list with per-item metadata and a progress indicator.

**Independent Test**: quickstart.md scenario 1 — open the dashboard on a site with a mix of referenced/unreferenced media and confirm only truly unreferenced items appear in "Unused," with correct metadata and a working progress indicator.

### Implementation for User Story 1

- [X] T018 [US1] Implement `GET /summary` (current `AuditRun` status/counts) in `MediaAuditApiController` + `MediaAuditService` per FR-010, FR-011 and contracts §GET /summary
- [X] T019 [US1] Implement `POST /run` (trigger an audit; `Running`→`Complete` state; return current status if already `Running`) in `MediaAuditApiController` + `MediaAuditService` per FR-011 and contracts §POST /run
- [X] T020 [US1] Implement `GET /items` base listing (status filter only, unpaged for now — full filter/sort/paging comes in US3) returning `MediaAuditItem` with `usageStatus`/`usageCount`/`detectionSource` in `MediaAuditApiController` + `MediaAuditService` per FR-002, FR-006 and contracts §GET /items
- [X] T021 [US1] Populate `sizeBytes`/`extension` (from `umbracoBytes`/`umbracoExtension`) **and `path`/`folderId`** (resolving `IMedia.Path`'s comma-separated id list to a human-readable folder path, plus the immediate parent id) on `MediaAuditItem` in `MediaAuditService` per research.md §6 and data-model.md
- [X] T022 [US1] Implement the audit progress indicator and "last refreshed" timestamp in `src/UmbracoMediaAudit/Client/src/dashboard/media-audit-dashboard.element.ts` per FR-011 and User Story 1 Acceptance Scenario 3
- [X] T023 [US1] Implement the Used/Unused results table (UUI table components) in `media-audit-dashboard.element.ts` per FR-001, FR-002 and User Story 1 Acceptance Scenario 1
- [X] T024 [US1] Implement the per-item metadata panel (name/type/size/folder/last-modified) on row selection in `media-audit-dashboard.element.ts` per FR-006 and User Story 1 Acceptance Scenario 2
- [X] T025 [US1] Implement the summary bar (total/used/unused counts + sizes) in `media-audit-dashboard.element.ts` per FR-010
- [X] T026 [US1] Implement the "detectable references only" disclaimer banner in `media-audit-dashboard.element.ts` per FR-016

**Checkpoint**: User Story 1 is fully functional and independently testable — this is the MVP.

---

## Phase 4: User Story 2 - See where a used media file is referenced (Priority: P2)

**Goal**: An editor selects a "Used" item and sees every content item referencing it, with working navigation links.

**Independent Test**: quickstart.md scenario 2 — select a "Used" item and confirm every actual referencing content item is listed (including a draft-only and a Block-List-nested reference), each with a working link.

### Implementation for User Story 2

- [X] T027 [US2] Implement `GET /items/{key}/usages` resolving the combined relation+scan `UsageReference` list (`culture`, `publishState`, `detectionSource`, `editUrl`) in `MediaAuditApiController` + `MediaAuditService` per FR-004, FR-005, FR-017 and contracts §GET /items/{key}/usages
- [X] T028 [US2] Implement the stale-relation data-integrity signal (a `Used` item that resolves zero usages) in `MediaAuditService` per the data-model.md validation rules
- [X] T029 [P] [US2] Create `src/UmbracoMediaAudit/Client/src/dashboard/media-audit-detail.element.ts` rendering the usage list (content name/type/culture/publish-state badges, navigate links) per FR-005 and User Story 2 Acceptance Scenarios 1–2
- [X] T030 [US2] Wire dashboard row-click to open the detail element (UUI modal/workspace) in `media-audit-dashboard.element.ts` (depends on T023, T029) — implemented as an inline expansion in the existing detail panel rather than a separate modal/workspace; also added a minimal Unused/Used status toggle since the table previously only ever fetched "Unused" items and Used items were otherwise unreachable (the full status/type/folder filter UI remains US3's T035)
- [X] T031 [US2] Implement backoffice deep-link construction (`editUrl`) for referencing content items in `MediaAuditService` per contracts §GET /items/{key}/usages — via the existing `BackofficeLinks.ContentEditUrl` helper

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Filter, sort, and export the audit results (Priority: P3)

**Goal**: An administrator filters/sorts the results and exports the current view.

**Independent Test**: quickstart.md scenario 3 — filter to "Unused" sorted by size descending, confirm correctness, then export and confirm the file matches what's on screen.

### Implementation for User Story 3

- [X] T032 [US3] Extend `GET /items` with `mediaTypeAlias`/`folderId` filters, `sort`/`sortDirection` (`name`/`sizeBytes`/`updateDate`), and paging in `MediaAuditApiController` + `MediaAuditService` per FR-007, FR-008 and contracts §GET /items — via new `MediaAuditItemsQuery`/`MediaAuditItemsResult` models
- [X] T033 [US3] Implement `GET /export` producing a CSV of the current filtered/sorted set in `MediaAuditApiController` per FR-009 and contracts §GET /export (shares `MediaAuditApiController.cs` with T032 — sequence after it, not parallel)
- [X] T034 [US3] Implement the `MediaFolder` listing service (for the folder filter dropdown) in `MediaAuditService` per data-model.md (shares `MediaAuditService.cs` with T032 — sequence after it, not parallel) — plus a new `GET /folders` endpoint and (beyond the original task scope) a `GET /media-types` endpoint for the type-filter dropdown's options; both added to contracts/media-audit-api.md since neither was in the original contract draft
- [X] T035 [US3] Implement filter controls (status/type/folder) in `media-audit-dashboard.element.ts` per FR-007 (depends on T034) — native `<select>` dropdowns; the status filter continues to use the US2-era summary-pill toggle rather than a third dropdown
- [X] T036 [US3] Implement sortable column headers in the results table in `media-audit-dashboard.element.ts` per FR-008 — Name/Size/Last modified headers toggle sort field/direction with a ▲/▼ indicator
- [X] T037 [US3] Implement the Export action (triggers `GET /export` download) in `media-audit-dashboard.element.ts` per FR-009 — via an authenticated Blob download (a plain `<a href>` can't carry the required Bearer token)

**Extra correctness fixes made while implementing this phase (not originally scoped, found along the way)**:
- Folders are now excluded from audit classification entirely (`ExecuteAuditAsync`) — they were previously showing up as permanently-"Unused" clutter, resolving the spec.md edge case about folder classification.
- A media item is now also classified "Used" if any *ancestor folder* is itself referenced (e.g. a gallery/slideshow block that picks a folder rather than each file) — both in bulk classification and in `GetUsagesAsync`. **Not yet applied to User Story 4's delete/purge pre-action re-check, since that doesn't exist yet — it MUST be when built.** See research.md §4 and spec.md's Edge Cases for the full writeup.
- CSV export gained a `UsedOnPages` column (referencing content item names, relation-based only for speed) per user request; contracts/media-audit-api.md updated to match.
- UI layout polish: Total/Used/Unused pills moved to sit left of the Type/Folder filter dropdowns in one row (`.filter-bar`), rather than stacked above them.

**Checkpoint**: User Stories 1–3 all work independently.

---

## Phase 6: User Story 4 - Admin cleanup: delete, purge & deletion log (Priority: P4)

**Goal**: An administrator can safely soft-delete confirmed-unused items, optionally purge specific already-deleted items to reclaim space immediately, and review a batch-level log of every such action. Non-admins never see these controls.

**Independent Test**: quickstart.md scenarios 4–7 — non-admin sees no delete/purge controls; admin can delete (moves to Recycle Bin, race-condition re-check skips newly-referenced items); admin can purge specific trashed items with a separate stronger confirmation (skips items already restored by someone else); the deletion log shows exactly one entry per action, never one per item.

### Implementation for User Story 4

- [X] T038 [P] [US4] Create `AddDeletionLogTablePlan` (`PackageMigrationPlan`) in `src/UmbracoMediaAudit/Migrations/AddDeletionLogTablePlan.cs` creating the `DeletionLogEntry` table per research.md §10 and data-model.md — fluent `Create.Table(...).WithColumn(...)` API (verified via reflection against the real assembly, not guessed), not an attribute-decorated DTO; confirmed running correctly against the live SQLite dev DB
- [X] T039 [P] [US4] Create the `DeletionLogEntry` model in `src/UmbracoMediaAudit/Models/DeletionLogEntry.cs` per data-model.md
- [X] T040 [US4] Register the migration plan's execution in `MediaAuditComposer.cs` (depends on T038) — actually `UmbracoMediaAuditApiComposer.cs` (the project's real composer filename) via `builder.PackageMigrationPlans().Add<>()`
- [X] T041 [US4] Implement `IDeletionLogService`/`DeletionLogService` in `src/UmbracoMediaAudit/Services/DeletionLogService.cs` — write exactly one entry per delete/purge batch (never per item, never zero even if fully skipped) and read paged history, per FR-019 (depends on T038, T039) — plain attribute-free NPoco row type, matched via explicit SQL column aliases and the explicit-table-name `Insert()` overload
- [X] T042 [US4] Implement `MediaDeleteService` in `src/UmbracoMediaAudit/Services/MediaDeleteService.cs` — `IMediaService.MoveToRecycleBin` per selected item, with a fresh re-check immediately before each deletion and `NowReferenced` skip-reporting, per FR-014 and research.md §4–5 — **the pre-delete re-check reuses `IMediaAuditService.GetUsagesAsync` rather than re-implementing relation+scan+ancestor-folder logic separately**, so the gallery/slideshow folder-reference fix from User Story 3 (research.md §4 addendum, [[media-audit-folder-references]]) automatically applies here too, resolving that fix's flagged follow-up
- [X] T043 [US4] Implement `MediaPurgeService` in `src/UmbracoMediaAudit/Services/MediaPurgeService.cs` — `IMediaService.Delete()` per selected item (never `EmptyRecycleBin()`), with a fresh `Trashed`-state re-check immediately before each purge and `NotTrashed` skip-reporting, per FR-018 and research.md §5
- [X] T044 [US4] Implement the shared admin-only permission-check helper in `MediaAuditApiController.cs` per FR-015 — `[Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]` stacked on top of the base controller's Media-section-access policy (ASP.NET Core combines multiple `[Authorize]` attributes with AND semantics, returning 403 for an authenticated-but-non-admin caller) rather than a hand-written check
- [X] T045 [US4] Implement `POST /delete` wired to `MediaDeleteService` + `DeletionLogService`, returning `logEntryId` in `MediaAuditApiController.cs` per FR-014, FR-015, FR-019 and contracts §POST /delete (depends on T041, T042, T044)
- [X] T046 [US4] Implement `POST /purge` wired to `MediaPurgeService` + `DeletionLogService`, returning `logEntryId` in `MediaAuditApiController.cs` per FR-018, FR-015, FR-019 and contracts §POST /purge (depends on T041, T043, T044)
- [X] T047 [US4] Implement `GET /deletion-log` (admin-only, paged) in `MediaAuditApiController.cs` per FR-019 and contracts §GET /deletion-log (depends on T041, T044)
- [X] T048 [P] [US4] Create `media-audit-delete-confirm.element.ts` in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-014
- [X] T049 [P] [US4] Create `media-audit-purge-confirm.element.ts` — its own, more strongly-worded confirmation, distinct from delete's — in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-018 — requires ticking an explicit "I understand this is permanent" checkbox before the button enables, not just a click
- [X] T050 [P] [US4] Create `media-audit-deletion-log.element.ts` (admin-only log view) in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-019 — **also where purge is initiated from**: each `Delete`-type log entry gets its own Purge action against exactly that entry's items, since "specific items previously deleted via FR-014" (FR-018's own wording) are precisely a Delete entry's item list, so no separate Recycle-Bin item-picker UI was needed
- [X] T051 [US4] Wire delete/purge/deletion-log controls to render only for administrators (hidden entirely, not just disabled, for non-admins) in `media-audit-dashboard.element.ts` per FR-015 (depends on T023, T048–T050) — admin status is *derived*, not read from Umbraco's client-side user context: the dashboard calls `GET /deletion-log` once on load and treats a 403 (`MediaAuditApiError.status`) as "not admin," reusing the server's own already-correct authorization instead of independently re-deriving admin status client-side
- [X] T052 [US4] Implement `NowReferenced`/`NotTrashed` skip-reporting feedback in the delete/purge confirmation elements per the spec's race-condition edge cases (depends on T045, T046, T048, T049) — surfaced via notification toasts distinguishing "N deleted/purged, M skipped" from a plain success message

**Checkpoint**: All four user stories are independently functional. Feature-complete per spec.md.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation and hardening across all stories.

- [ ] T053 [P] Run quickstart.md validation scenarios 1–8 end-to-end against a seeded local Umbraco v17 site, explicitly recording pass/fail against SC-001 (≤10s to determine status), SC-003 (100% detection accuracy across every standard property editor named in FR-003), and SC-005 (≤2 interactions to find usage) — these three have no other dedicated validation task
- [ ] T054 [P] Seed a ~10,000-item media library and confirm the audit completes within 60 seconds per SC-002
- [ ] T055 [P] Cross-validate audit results against `reference/media_audit.py` per quickstart.md scenario 8; confirm any discrepancies are the documented, expected ones (research.md §4, §9) and not unexplained bugs
- [X] T056 [P] Add xUnit unit tests for `MediaAuditService`, `MediaReferenceScanner`, `MediaDeleteService`, `MediaPurgeService`, and `DeletionLogService` in `tests/UmbracoMediaAudit.Tests.Unit/` — 31 tests, all passing. Uses Moq for the service-interface boundary, but real (not mocked) Umbraco model objects (`Content`/`Media`/`ContentType`/`MediaType`/`PropertyType`) since those are plain constructible POCOs, not database-backed - verified their exact constructor/`SetValue` shapes via reflection first rather than guessing. `DeletionLogService.DeletionLogRow` was changed `private` → `internal` (+ `AssemblyInfo.cs` `InternalsVisibleTo`) purely for testability, so Moq can set up `IUmbracoDatabase`'s generic `Insert<T>`/`Fetch<T>` calls against a nameable type. Along the way, found and fixed two real (unrelated to tests) obsolete-API warnings only visible on a `--no-incremental` rebuild: `DeletionLogService` was using the obsolete `Umbraco.Cms.Core.Scoping.IScopeProvider` (now `Umbraco.Cms.Infrastructure.Scoping`), and the migration was using `MigrationBase`, which is scheduled for removal in Umbraco 18 (now `AsyncMigrationBase`).
- [X] T057 [P] Add xUnit integration tests (against the seeded local Umbraco v17 SQLite instance) covering the full `GET`/`POST` surface in `tests/UmbracoMediaAudit.Tests.Integration/` — 10 tests, all passing (~6s), against a real Umbraco host + SQLite database via `Umbraco.Cms.Tests.Integration`. Design deviations/gotchas found and fixed:
  - Umbraco's own integration-test scaffolding is **NUnit-based**, not xUnit (confirmed via reflection: base `Setup()`/`TearDown()` are NUnit-attributed) - switched this one test project to NUnit; the unit test project (T056) stays xUnit.
  - The default test-DB pool warm-up (`PrepareThreadCount: 4`, `SchemaDatabaseCount: 4`) **deadlocked indefinitely** on this dev machine (confirmed via OS-level CPU/IO monitoring - alive, "Responding: True", ~0% CPU and 0 disk I/O for 12+ minutes, not merely slow) - fixed by forcing `PrepareThreadCount`/`SchemaDatabaseCount` to 1 and `EmptyDatabasesCount` to 0 via an in-memory config override (`SetUpTestConfiguration`). Root cause not fully isolated (Windows-specific threading/locking in the pool warm-up), but the workaround is clean and doesn't affect what's being tested.
  - The test host's `TypeLoader` does **not** auto-discover this package's own `IComposer` the way a real site boot does - every service/migration this package registers was missing from DI until fixed by invoking `new UmbracoMediaAuditApiComposer().Compose(builder)` directly in `CustomTestSetup`, rather than re-declaring its registrations a second time.
  - Package migrations (`AddDeletionLogTablePlan`) don't run automatically at this boot level either - fixed with an explicit `PackageMigrationRunner.RunPendingPackageMigrations("UmbracoMediaAudit")` call as an additional `[SetUp]` step.
  - **Known, documented limitation** (see the doc comment on `MediaAuditServiceIntegrationTests`): `RunAuditAsync`'s classification loop runs on a bare `Task.Run` background thread (by design, FR-012) - under `UmbracoIntegrationTest` specifically, that thread's `IRelationService` reads don't observe relations the same test just published, even after ruling out (via direct experiment) an uncommitted-scope theory and a cache-invalidation-timing theory. The identical relation-lookup code called directly on the test's own thread (`GetUsagesAsync`, also used by `MediaDeleteService`'s pre-delete re-check) sees it correctly every time, and `ClassifyMedia`'s logic is separately verified against mocks in T056's unit tests - so this looks like a test-harness/background-thread ambient-scope quirk, not a product defect. The two affected tests verify classification via `GetUsagesAsync` instead of `GetItems()` (still a real, non-mocked check of the same "is it used" decision) rather than continuing an open-ended debugging spiral on a test-environment artifact.
  - Shared support: `TestSupport/MediaAuditIntegrationTestBase.cs` (config override, composer invocation, migration runner) and `TestSupport/MediaPickerTestSchema.cs` (real content-type/data-type/content creation with a live Media Picker property, mirroring `TestSchemaSeeder.cs`'s pattern) factor out what's common across the three fixture files.
- [X] T058 [P] Add Web Test Runner component tests for the dashboard, detail, delete-confirm, purge-confirm, and deletion-log elements in `tests/UmbracoMediaAudit.Client.Tests/` — 30 tests, all passing (~30s). The existing scaffold was missing several things needed for it to run at all, all fixed:
  - `@open-wc/testing` (fixture/expect/aTimeout) and the real `@umbraco-cms/backoffice` package (pinned to `17.5.3`, matching what's actually installed in the Client project) were never added as dependencies - the elements import Lit/UUI *through* this package at runtime, so it's not just a types dependency.
  - `web-test-runner.config.js` had no TypeScript transform at all (`esbuildPlugin({ ts: true })`, from `@web/dev-server-esbuild`) - without it every `*.element.ts` import fails to parse in the browser, since these files use experimental/legacy decorators.
  - Its `rootDir` was implicitly scoped to this package's own folder, so the tests' imports of the real element sources (`../../../../src/UmbracoMediaAudit/Client/src/...`) 404'd - widened to the repo root; the `files` glob for test-*discovery* stayed CWD-relative (a different resolution rule than `rootDir`, easy to conflate).
  - `<uui-button>`/`<uui-box>`/`<uui-checkbox>`/etc. are undefined custom elements until `@umbraco-cms/backoffice/external/uui` (which re-exports `@umbraco-ui/uui`) is imported somewhere - added as a side-effect import (`test-support/uui-setup.ts`) every test file loads first.
  - No Playwright browser binary was installed (`npx playwright install chromium`).
  - `MediaAuditDashboardElement` defers all its initial data loading until Umbraco's `UMB_AUTH_CONTEXT` is consumed, which a bare fixture never provides - rather than reconstructing Umbraco's context-request protocol, its tests poke the element's `@state()` fields directly (TS `private` is erased at the esbuild/JS level, so this is a normal reactive-setter assignment) to put it in a given state, then verify rendering/click-driven behavior from there.
  - **Real, non-obvious gotcha, cost the most debugging time**: both `UUIButtonElement` (`<uui-button>`) and `UUIBooleanInputElement` (`<uui-checkbox>`/`<uui-toggle>`) override the inherited `HTMLElement.click()` as an **async** method that clicks their own internal native `<button>`/`<input>` - directly setting `.checked` and dispatching a synthetic `change` event does nothing (their real state lives on the internal input), and calling `.click()` without `await`ing it is a real race (the internal click, and everything it triggers, is still pending when the next assertion runs). Fixed everywhere via two tiny awaited helpers (`test-support/uui-interactions.ts`), used instead of calling `.click()` directly on any `uui-button`/`uui-checkbox`.
  - `MediaAuditRepository` is a plain object of functions funneling through one shared `request()` helper that calls global `fetch()` - stubbing `globalThis.fetch` directly (`test-support/fetch-stub.ts`) was sufficient for every element; no mocking library needed.
- [X] T059 [P] Write the package README and Marketplace listing copy (description, docs URL, category) per research.md §2 — added repo-root `README.md` (features/requirements/install/getting-started/how-it-works/local-dev/license), wired into the NuGet package via `PackageReadmeFile` + `Pack="true"` (verified via an actual `dotnet pack`, README lands in the .nupkg root), added `LICENSE` (MIT, referenced by the existing `PackageLicenseExpression`), and a root `umbraco-marketplace.json` for the optional listing metadata (Category/AlternateCategory/DocumentationUrl/IssueTrackerUrl/Tags). Category taxonomy and the optional-fields shape were fetched from the real current docs (https://docs.umbraco.com/umbraco-dxp/marketplace/listing-your-package.md), not guessed - chose "Editor Tools" as primary category (this is an editor/admin-facing backoffice dashboard, not a dev-only tool) with "Developer Tools" as the alternate. Required package name/authors/description/project URL are already sourced automatically from the existing `.csproj` metadata per that doc.
- [X] T060 [P] Implement `Failed` audit-run state handling (surfaced error message, retry action) in `MediaAuditService` + `media-audit-dashboard.element.ts`, per the spec.md edge case for audit failures — was already implemented back in the Foundational phase (T013's `ExecuteAuditAsync` catch block + the dashboard's poll-completion handler), just never marked done; "retry" is the existing Run Audit button, re-enabled once `_isRunning` clears

---

## Dependencies & Execution Order

### Phase Dependencies

- **Setup (Phase 1)**: No dependencies — start immediately. T001 is sequential (creates the directories everything else lands in); T002–T007 can then run in parallel.
- **Foundational (Phase 2)**: Depends on Setup — BLOCKS all user stories. Models (T008–T011) are parallel; the scanner (T012) depends on the models; the audit service (T013) depends on the scanner; the controller skeleton (T014) depends on the audit service; the composer (T015) depends on all three; the client shell (T016) and API repository (T017) are parallel to each other and to the server chain.
- **User Stories (Phases 3–6)**: All depend on Foundational completion. They may then proceed in parallel (if staffed) or sequentially in priority order (P1 → P2 → P3 → P4), matching the spec's own priority ordering.
- **Polish (Phase 7)**: Depends on all four user-story phases being complete.

### User Story Dependencies

- **User Story 1 (P1)**: No dependencies on other stories. This is the MVP.
- **User Story 2 (P2)**: Reuses US1's dashboard table/row (T023) as an attachment point (T030) but its own endpoint/service/element work (T027–T029, T031) is independent and could be built without US1's UI existing yet.
- **User Story 3 (P3)**: Extends the `GET /items` endpoint US1 already stood up (T020 → T032) and reuses the table (T023); independently testable once US1 exists.
- **User Story 4 (P4)**: Independent server-side work (T038–T047) has no dependency on US1–US3; only the final UI wiring (T051) attaches to US1's table (T023) and US4's own confirm/log elements (T048–T050).

### Within Each User Story

- Models/migrations before services
- Services before controller endpoints
- Server endpoints before the client code that calls them
- Story complete and checkpointed before moving to the next priority (if working sequentially)

### Parallel Opportunities

- All Setup tasks marked [P] (T002–T007) can run together once T001 is done
- All Foundational model tasks (T008–T011) can run together; T016/T017 can run alongside the server chain (T012–T015)
- Once Foundational is complete, different developers could take US1, US2, US3, and US4 in parallel — US2/US3/US4's server-side work doesn't strictly require US1's UI to exist, only its endpoints (already in Foundational/US1)
- Within US4, T038/T039 (migration + model) and T048/T049/T050 (the three new UI elements) are each parallel groups
- All Polish tasks (T053–T060) are parallel

---

## Parallel Example: Foundational Phase

```bash
# Launch all Foundational models together:
Task: "Create MediaAuditItem model in src/UmbracoMediaAudit/Models/MediaAuditItem.cs"
Task: "Create MediaUsageReference model in src/UmbracoMediaAudit/Models/MediaUsageReference.cs"
Task: "Create AuditRun model in src/UmbracoMediaAudit/Models/AuditRun.cs"
Task: "Create MediaFolder model in src/UmbracoMediaAudit/Models/MediaFolder.cs"

# Once the server chain is underway, the client shell and API repository can run alongside it:
Task: "Create dashboard shell element + manifests.ts in src/UmbracoMediaAudit/Client/src/dashboard/ and src/UmbracoMediaAudit/Client/src/manifests.ts"
Task: "Create media-audit.repository.ts API client wrapper in src/UmbracoMediaAudit/Client/src/api/"
```

## Parallel Example: User Story 4

```bash
# Migration + model can be built together:
Task: "Create AddDeletionLogTablePlan in src/UmbracoMediaAudit/Migrations/AddDeletionLogTablePlan.cs"
Task: "Create DeletionLogEntry model in src/UmbracoMediaAudit/Models/DeletionLogEntry.cs"

# The three new UI elements are independent of each other:
Task: "Create media-audit-delete-confirm.element.ts"
Task: "Create media-audit-purge-confirm.element.ts"
Task: "Create media-audit-deletion-log.element.ts"
```

---

## Implementation Strategy

### MVP First (User Story 1 Only)

1. Complete Phase 1: Setup
2. Complete Phase 2: Foundational (CRITICAL — blocks all stories)
3. Complete Phase 3: User Story 1
4. **STOP and VALIDATE**: run quickstart.md scenario 1 independently
5. Deploy/demo if ready — this is already a usable audit dashboard, just without drill-down, filtering, or cleanup actions

### Incremental Delivery

1. Setup + Foundational → detection engine + skeleton ready
2. Add User Story 1 → validate (quickstart scenario 1) → demo (MVP!)
3. Add User Story 2 → validate (scenario 2) → demo
4. Add User Story 3 → validate (scenario 3) → demo
5. Add User Story 4 → validate (scenarios 4–7) → demo — this is the first point the "reclaim storage space" outcome in User Story 1's own motivation is fully deliverable end-to-end
6. Phase 7: Polish, including the SC-002 performance validation and the reference-script cross-check

### Parallel Team Strategy

With multiple developers, after Foundational completes:

- Developer A: User Story 1 (T018–T026)
- Developer B: User Story 2 (T027–T031) — server-side pieces don't block on US1's UI
- Developer C: User Story 3 (T032–T037) — extends the same `GET /items` endpoint US1 stood up, so best started once T020 lands
- Developer D: User Story 4 (T038–T052) — fully independent server-side work; final UI wiring (T051) is the only point of contact with US1's table

---

## Notes

- [P] tasks = different files, no dependencies
- [Story] label maps task to specific user story for traceability (US4 = admin delete/purge/log, not separately numbered in spec.md but treated as its own increment here — see Organization note above)
- No dedicated per-story test tasks were generated (not explicitly requested); test infrastructure is scaffolded in Setup and dedicated test-writing is grouped in Polish (T056–T058)
- Every task cites the FR(s)/contract endpoint(s)/data-model entity it satisfies — cross-reference spec.md, data-model.md, and contracts/media-audit-api.md if a task's intent is unclear
- Commit after each task or logical group; stop at any checkpoint to validate a story independently
- Avoid: vague tasks, same-file conflicts on [P] tasks, cross-story dependencies that break independent testability
