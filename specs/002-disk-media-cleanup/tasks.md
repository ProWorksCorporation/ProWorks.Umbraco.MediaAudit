# Tasks: Disk-Only Media Cleanup

**Input**: plan.md, data-model.md, contracts/disk-media-api.md, spec.md (User Stories 1-3)

## Phase 1: Models & Scanner (foundation, blocks everything else)

- [ ] T001 Add `DiskMediaItem`, `DiskAuditRun` (+ `DiskAuditRunStatus`), `DiskMediaItemsResult`,
      `DiskMediaDeleteResult`, `DiskMediaActionSkip` to `src/ProWorks.Umbraco.MediaAudit/Models/`.
- [ ] T002 Extend `DeletionLogActionType` with `DiskDelete`; make `DeletionLogItem.Key` nullable
      (`Guid?`) and add optional `RelativePath` (`string?`) - `src/.../Models/DeletionLogEntry.cs`.
      Update `MediaDeleteService`/`MediaPurgeService` call sites for the now-nullable `Key` (no
      behavior change - they always set it).
- [ ] T003 [P] `IDiskMediaScanner`/`DiskMediaScanner` (`src/.../Services/`): recursive walk of
      `MediaFileManager.FileSystem` returning every file's normalized relative path + size +
      last-modified (research.md R1).
- [ ] T004 [P] Unit tests `DiskMediaScannerTests.cs` against a mocked `IFileSystem` covering: flat
      root files, nested directories, an empty file system, and path normalization.

## Phase 2: User Story 1 - Scan & list (P1, MVP)

- [ ] T005 `IDiskMediaAuditService`/`DiskMediaAuditService`: `GetCurrentAudit()`,
      `RunAuditAsync()` (background, Running/Complete/Failed lifecycle mirroring
      `MediaAuditService`), `GetItems()`. Builds the "owned paths" set from every non-folder
      `IMedia` (including trashed) via the existing `umbracoFile`/image-cropper resolution
      (research.md R2), diffs against `DiskMediaScanner`'s output.
- [ ] T006 [P] Unit tests `DiskMediaAuditServiceTests.cs`: a disk file with no matching media is
      listed; a disk file matching an active media item is not; a disk file matching a *trashed*
      media item is not (Edge Cases); a failed scan reports `Failed` with an error message.
- [ ] T007 Register `IDiskMediaScanner`/`IDiskMediaAuditService` as singletons in
      `UmbracoMediaAuditApiComposer`.
- [ ] T008 Controller actions `GET /disk-summary`, `POST /disk-scan`, `GET /disk-items` on
      `UmbracoMediaAuditApiController` (contracts/disk-media-api.md).
- [ ] T009 [P] Integration test `DiskMediaCleanupIntegrationTests.cs`: write a real file via
      `MediaFileManager.FileSystem.AddFile`, create an unrelated real media item, run the audit
      service, assert only the orphan file is listed.
- [ ] T010 Client: `MediaAuditRepository` additions (`getDiskSummary`, `runDiskScan`,
      `getDiskItems`, types) in `media-audit.repository.ts`.
- [ ] T011 Client: new `media-audit-disk-panel.element.ts` (toggled from the dashboard toolbar next
      to "View Deletion Log", same pattern) - scan button, progress indicator, results list with
      URL/size/last-modified, following `media-audit-dashboard.element.ts`'s grid/CSS conventions.
- [ ] T012 Wire the panel's toggle button into `media-audit-dashboard.element.ts`.
- [ ] T013 [P] Client test `media-audit-disk-panel.element.test.ts` covering scan trigger, empty
      state, and results rendering.

**Checkpoint**: User Story 1 is independently testable/demoable here.

## Phase 3: User Story 2 - Delete selected (P2)

- [ ] T014 `IDiskMediaDeleteService`/`DiskMediaDeleteService.DeleteAsync(paths, userId)`:
      re-verifies each path is still present and still unowned immediately before deleting
      (FR-008), deletes via `MediaFileManager.FileSystem.DeleteFile`, logs one `DiskDelete` entry
      via `IDeletionLogService` (always, even if every item skipped).
- [ ] T015 [P] Unit tests `DiskMediaDeleteServiceTests.cs`: happy path; a path that's since been
      claimed by a new media item is skipped as "NowOwned"; a path already gone is skipped as
      "NotFound"; a fully-skipped batch still logs one entry.
- [ ] T016 Register `IDiskMediaDeleteService`; controller action `POST /disk-delete` (admin-only,
      same `[Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]` pattern as `/delete`).
- [ ] T017 [P] Integration test extending T009's fixture: delete the orphan file, assert it's gone
      from disk and from a re-scan, and appears in `GetPagedHistory` as `DiskDelete`.
- [ ] T018 Client: selection checkboxes (per-item) in the disk panel + `deleteDiskItems` repository
      call.
- [ ] T019 New `media-audit-disk-delete-confirm.element.ts` (permanent-delete wording +
      acknowledgment checkbox, mirroring `media-audit-purge-confirm.element.ts` since disk deletes
      have no Recycle Bin) wired into the disk panel.
- [ ] T020 [P] Client test `media-audit-disk-delete-confirm.element.test.ts`.

**Checkpoint**: User Stories 1+2 independently testable/demoable here.

## Phase 4: User Story 3 - Delete all (P3)

- [ ] T021 Client: "Delete All" button in the disk panel that selects every currently-listed item
      and reuses the same confirm/delete flow as T018-T019 (no new server endpoint - see
      contracts/disk-media-api.md).
- [ ] T022 [P] Client test covering "Delete All" selecting the full list and disabling when the
      list is empty.

**Checkpoint**: All user stories independently testable/demoable here.

## Phase 5: Polish

- [ ] T023 [P] Update `README.md`/`CONTRIBUTING.md` screenshots-list section if it enumerates
      dashboard features (only if such a list already exists there).
- [ ] T024 Run `/speckit-analyze` for cross-artifact consistency before opening the PR.

## Dependencies

- Phase 1 blocks Phases 2-4.
- Within Phase 2: T005 blocks T006-T012; T010 blocks T011-T013.
- Phase 3 depends on Phase 2 (needs the panel and item list to select from).
- Phase 4 depends on Phase 3 (reuses its confirm/delete flow).
- Tasks marked [P] within a phase touch different files and can run in parallel with each other.
