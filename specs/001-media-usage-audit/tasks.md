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

- [ ] T027 [US2] Implement `GET /items/{key}/usages` resolving the combined relation+scan `UsageReference` list (`culture`, `publishState`, `detectionSource`, `editUrl`) in `MediaAuditApiController` + `MediaAuditService` per FR-004, FR-005, FR-017 and contracts §GET /items/{key}/usages
- [ ] T028 [US2] Implement the stale-relation data-integrity signal (a `Used` item that resolves zero usages) in `MediaAuditService` per the data-model.md validation rules
- [ ] T029 [P] [US2] Create `src/UmbracoMediaAudit/Client/src/dashboard/media-audit-detail.element.ts` rendering the usage list (content name/type/culture/publish-state badges, navigate links) per FR-005 and User Story 2 Acceptance Scenarios 1–2
- [ ] T030 [US2] Wire dashboard row-click to open the detail element (UUI modal/workspace) in `media-audit-dashboard.element.ts` (depends on T023, T029)
- [ ] T031 [US2] Implement backoffice deep-link construction (`editUrl`) for referencing content items in `MediaAuditService` per contracts §GET /items/{key}/usages

**Checkpoint**: User Stories 1 and 2 both work independently.

---

## Phase 5: User Story 3 - Filter, sort, and export the audit results (Priority: P3)

**Goal**: An administrator filters/sorts the results and exports the current view.

**Independent Test**: quickstart.md scenario 3 — filter to "Unused" sorted by size descending, confirm correctness, then export and confirm the file matches what's on screen.

### Implementation for User Story 3

- [ ] T032 [US3] Extend `GET /items` with `mediaTypeAlias`/`folderId` filters, `sort`/`sortDirection` (`name`/`sizeBytes`/`updateDate`), and paging in `MediaAuditApiController` + `MediaAuditService` per FR-007, FR-008 and contracts §GET /items
- [ ] T033 [US3] Implement `GET /export` producing a CSV of the current filtered/sorted set in `MediaAuditApiController` per FR-009 and contracts §GET /export (shares `MediaAuditApiController.cs` with T032 — sequence after it, not parallel)
- [ ] T034 [US3] Implement the `MediaFolder` listing service (for the folder filter dropdown) in `MediaAuditService` per data-model.md (shares `MediaAuditService.cs` with T032 — sequence after it, not parallel)
- [ ] T035 [US3] Implement filter controls (status/type/folder) in `media-audit-dashboard.element.ts` per FR-007 (depends on T034)
- [ ] T036 [US3] Implement sortable column headers in the results table in `media-audit-dashboard.element.ts` per FR-008
- [ ] T037 [US3] Implement the Export action (triggers `GET /export` download) in `media-audit-dashboard.element.ts` per FR-009

**Checkpoint**: User Stories 1–3 all work independently.

---

## Phase 6: User Story 4 - Admin cleanup: delete, purge & deletion log (Priority: P4)

**Goal**: An administrator can safely soft-delete confirmed-unused items, optionally purge specific already-deleted items to reclaim space immediately, and review a batch-level log of every such action. Non-admins never see these controls.

**Independent Test**: quickstart.md scenarios 4–7 — non-admin sees no delete/purge controls; admin can delete (moves to Recycle Bin, race-condition re-check skips newly-referenced items); admin can purge specific trashed items with a separate stronger confirmation (skips items already restored by someone else); the deletion log shows exactly one entry per action, never one per item.

### Implementation for User Story 4

- [ ] T038 [P] [US4] Create `AddDeletionLogTablePlan` (`PackageMigrationPlan`) in `src/UmbracoMediaAudit/Migrations/AddDeletionLogTablePlan.cs` creating the `DeletionLogEntry` table per research.md §10 and data-model.md
- [ ] T039 [P] [US4] Create the `DeletionLogEntry` model in `src/UmbracoMediaAudit/Models/DeletionLogEntry.cs` per data-model.md
- [ ] T040 [US4] Register the migration plan's execution in `MediaAuditComposer.cs` (depends on T038)
- [ ] T041 [US4] Implement `IDeletionLogService`/`DeletionLogService` in `src/UmbracoMediaAudit/Services/DeletionLogService.cs` — write exactly one entry per delete/purge batch (never per item, never zero even if fully skipped) and read paged history, per FR-019 (depends on T038, T039)
- [ ] T042 [US4] Implement `MediaDeleteService` in `src/UmbracoMediaAudit/Services/MediaDeleteService.cs` — `IMediaService.MoveToRecycleBin` per selected item, with a fresh `MediaReferenceScanner` re-check immediately before each deletion and `NowReferenced` skip-reporting, per FR-014 and research.md §4–5
- [ ] T043 [US4] Implement `MediaPurgeService` in `src/UmbracoMediaAudit/Services/MediaPurgeService.cs` — `IMediaService.Delete()` per selected item (never `EmptyRecycleBin()`), with a fresh `Trashed`-state re-check immediately before each purge and `NotTrashed` skip-reporting, per FR-018 and research.md §5
- [ ] T044 [US4] Implement the shared admin-only permission-check helper in `MediaAuditApiController.cs` per FR-015
- [ ] T045 [US4] Implement `POST /delete` wired to `MediaDeleteService` + `DeletionLogService`, returning `logEntryId` in `MediaAuditApiController.cs` per FR-014, FR-015, FR-019 and contracts §POST /delete (depends on T041, T042, T044)
- [ ] T046 [US4] Implement `POST /purge` wired to `MediaPurgeService` + `DeletionLogService`, returning `logEntryId` in `MediaAuditApiController.cs` per FR-018, FR-015, FR-019 and contracts §POST /purge (depends on T041, T043, T044)
- [ ] T047 [US4] Implement `GET /deletion-log` (admin-only, paged) in `MediaAuditApiController.cs` per FR-019 and contracts §GET /deletion-log (depends on T041, T044)
- [ ] T048 [P] [US4] Create `media-audit-delete-confirm.element.ts` in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-014
- [ ] T049 [P] [US4] Create `media-audit-purge-confirm.element.ts` — its own, more strongly-worded confirmation, distinct from delete's — in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-018
- [ ] T050 [P] [US4] Create `media-audit-deletion-log.element.ts` (admin-only log view) in `src/UmbracoMediaAudit/Client/src/dashboard/` per FR-019
- [ ] T051 [US4] Wire delete/purge/deletion-log controls to render only for administrators (hidden entirely, not just disabled, for non-admins) in `media-audit-dashboard.element.ts` per FR-015 (depends on T023, T048–T050)
- [ ] T052 [US4] Implement `NowReferenced`/`NotTrashed` skip-reporting feedback in the delete/purge confirmation elements per the spec's race-condition edge cases (depends on T045, T046, T048, T049)

**Checkpoint**: All four user stories are independently functional. Feature-complete per spec.md.

---

## Phase 7: Polish & Cross-Cutting Concerns

**Purpose**: Validation and hardening across all stories.

- [ ] T053 [P] Run quickstart.md validation scenarios 1–8 end-to-end against a seeded local Umbraco v17 site, explicitly recording pass/fail against SC-001 (≤10s to determine status), SC-003 (100% detection accuracy across every standard property editor named in FR-003), and SC-005 (≤2 interactions to find usage) — these three have no other dedicated validation task
- [ ] T054 [P] Seed a ~10,000-item media library and confirm the audit completes within 60 seconds per SC-002
- [ ] T055 [P] Cross-validate audit results against `reference/media_audit.py` per quickstart.md scenario 8; confirm any discrepancies are the documented, expected ones (research.md §4, §9) and not unexplained bugs
- [ ] T056 [P] Add xUnit unit tests for `MediaAuditService`, `MediaReferenceScanner`, `MediaDeleteService`, `MediaPurgeService`, and `DeletionLogService` in `tests/UmbracoMediaAudit.Tests.Unit/`
- [ ] T057 [P] Add xUnit integration tests (against the seeded local Umbraco v17 SQLite instance) covering the full `GET`/`POST` surface in `tests/UmbracoMediaAudit.Tests.Integration/`
- [ ] T058 [P] Add Web Test Runner component tests for the dashboard, detail, delete-confirm, purge-confirm, and deletion-log elements in `tests/UmbracoMediaAudit.Client.Tests/`
- [ ] T059 [P] Write the package README and Marketplace listing copy (description, docs URL, category) per research.md §2
- [ ] T060 [P] Implement `Failed` audit-run state handling (surfaced error message, retry action) in `MediaAuditService` + `media-audit-dashboard.element.ts`, per the spec.md edge case for audit failures

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
