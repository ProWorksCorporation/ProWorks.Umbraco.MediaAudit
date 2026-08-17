# Phase 1 Data Model: Media Usage Audit Dashboard

**Feature**: [spec.md](./spec.md) | **Research**: [research.md](./research.md)

Audit results themselves introduce no new schema — they're computed on demand from Umbraco's existing
data (media nodes, content nodes, relations), per the spec's Assumptions. `MediaAuditItem`,
`UsageReference`, `AuditRun`, and `MediaFolder` below describe API response shapes (see
[contracts/media-audit-api.md](./contracts/media-audit-api.md)), not new database tables.

**One exception**: `DeletionLogEntry` (added after the clarification session, resolves FR-019) *is*
persisted in one new package-owned table, created via a Package Migration (research.md §10), because
a deletion/purge record must outlive a single backoffice session to be useful for accountability.

**Scope note carried from research.md §9**: all entities below that represent "content" mean
Umbraco page/document content (`IContentService`/`IContent`) only. Member data is explicitly out of
scope for this version (FR-002) — see Assumptions in spec.md.

**Variant note carried from research.md §8**: `UsageReference` rows are per culture/segment on
multi-language sites — a content item referencing a media item in 2 languages produces 2
`UsageReference` rows (one per culture), not 1, so the UI can show which language(s) hold the
reference.

## MediaAuditItem

Represents one media library file's audit status. Maps to spec's **Media Item** entity.

| Field | Type | Source | Notes |
|---|---|---|---|
| `id` | int | `umbracoNode.id` (via `IMedia.Id`) | Umbraco content/media node id |
| `key` | guid | `IMedia.Key` | Stable identifier, used in delete/detail requests instead of `id` |
| `name` | string | `IMedia.Name` | Display name |
| `mediaTypeAlias` | string | `IMedia.ContentType.Alias` | e.g. `image`, `file` |
| `extension` | string? | Property `umbracoExtension` | Null for container/folder media types |
| `sizeBytes` | long? | Property `umbracoBytes` | Null for container/folder media types |
| `path` | string | `IMedia.Path` resolved to a human folder path | Used for folder filter/display (FR-006, FR-007) |
| `folderId` | int? | Parent node id | Null if item is at Media library root |
| `createDate` | datetime | `IMedia.CreateDate` | |
| `updateDate` | datetime | `IMedia.UpdateDate` | Reflects latest save |
| `usageStatus` | enum: `Used` \| `Unused` | Derived (see below) | FR-002 |
| `usageCount` | int | Derived — count of distinct referencing content items | Drives sort/summary (FR-008, FR-010) |
| `detectionSource` | enum: `Relation` \| `Scan` \| `Both` \| `None` | Derived | Which mechanism(s) found a reference; `None` when `usageStatus = Unused`. Surfaced for transparency (FR-016), not required in the primary UI. **In v1 only `Relation` or `None` are ever produced here** — `Scan`/`Both` are reserved for the deferred deep-scan mode (research.md §4) and not currently reachable. |

**Derivation rule (usageStatus)**: `Used` if the relation-based check (research.md §4) finds at least
one reference **or** the on-demand scan (when run) finds one; otherwise `Unused`. A plain audit run
uses the relation check only, for performance (SC-002); the scan is applied per-item as described in
`UsageReference` below and mandatorily immediately before delete.

## UsageReference

Represents one place a `MediaAuditItem` is referenced. Maps to spec's **Usage Reference** entity.
Populated on demand for a given media item (FR-005), not eagerly for every item in the list.

| Field | Type | Source | Notes |
|---|---|---|---|
| `contentId` | int | Referencing `IContent.Id` | |
| `contentKey` | guid | Referencing `IContent.Key` | Used to build the backoffice edit link |
| `contentName` | string | Referencing `IContent.Name` | |
| `contentTypeAlias` | string | Referencing `IContent.ContentType.Alias` | |
| `culture` | string? | Culture/language code the reference was found in | Null for invariant properties/sites; required for FR-017 — see research.md §8 |
| `propertyAlias` | string? | Property holding the reference | Null if only found by the scan layer without per-property attribution |
| `publishState` | enum: `Published` \| `Draft` | `IContent.Published` | Confirms FR-004 — both states are surfaced, not just published |
| `detectionSource` | enum: `Relation` \| `Scan` | | Which mechanism found this specific reference |
| `editUrl` | string | Constructed backoffice deep link | Used for "navigate to each referencing content item" (Acceptance Scenario, User Story 2) |

## AuditRun

Represents one execution of the audit. Maps to spec's **Audit Run** entity. Not persisted between
requests beyond what's needed to show "last refreshed" (FR-011) — held in memory / short-lived cache
for the duration of a backoffice session; re-running the audit replaces it.

| Field | Type | Notes |
|---|---|---|
| `runAt` | datetime | Timestamp the audit completed |
| `totalScanned` | int | Total media items considered |
| `usedCount` / `usedSizeBytes` | int / long | Summary counts (FR-010) |
| `unusedCount` / `unusedSizeBytes` | int / long | Summary counts (FR-010) |
| `status` | enum: `Running` \| `Complete` \| `Failed` | Drives progress indicator (FR-011, Acceptance Scenario 3 of User Story 1) |
| `durationMs` | int? | Set once `Complete` |

## MediaFolder

Represents a media library container, used for the folder filter (FR-007) and location display
(FR-006). Maps to spec's **Media Folder** entity.

| Field | Type | Notes |
|---|---|---|
| `id` | int | |
| `name` | string | |
| `path` | string | Full folder path for breadcrumb-style display |
| `parentId` | int? | Null at Media library root |

## DeletionLogEntry

Represents one delete or purge action taken from the dashboard. Maps to spec's **Deletion Log Entry**
entity (added during clarification). **Persisted** — the one exception noted above — in a new table
created via Package Migration (research.md §10). One row per action/batch, never one row per item.

| Field | Type | Notes |
|---|---|---|
| `id` | int | Package-owned table's own identity column |
| `occurredAt` | datetime | When the action completed |
| `actionType` | enum: `Delete` \| `Purge` | FR-014 vs. FR-018 |
| `performedByUserId` | int | Umbraco backoffice user id (always an administrator — FR-015) |
| `itemCount` | int | Number of media items affected by this action |
| `totalSizeBytes` | long | Sum of `sizeBytes` across affected items, at time of action |
| `items` | string (JSON array) | Compact list of affected items' `key` + `name`, for display without a join back to (now possibly gone) media nodes |
| `skippedCount` | int | Items requested but skipped this action (e.g. `NowReferenced` on delete, or "already restored" on purge) — 0 if none |

## Validation / business rules carried from the spec

- An item with no detectable references from *either* mechanism is `Unused` (FR-002).
- `Used` items MUST always be able to resolve at least one `UsageReference` (FR-005) — if the relation
  check reports `Used` but zero references can be listed, this is a data-integrity condition to
  surface (e.g., a stale relation pointing at deleted content) rather than silently showing an empty
  list.
- Bulk delete (FR-014) MUST re-run the scan-based check (research.md §4) per selected item
  immediately before deleting; any item that check finds to now be referenced MUST be excluded from
  the delete and reported back to the admin rather than deleted anyway (edge case in spec.md).
- Purge (FR-018) MUST re-check each selected item's `Trashed` state immediately before calling
  `Delete()`; an item no longer trashed (e.g. restored since being soft-deleted) MUST be skipped and
  reported back, never purged (edge case in spec.md).
- Only administrators may see or invoke delete or purge; all users with Media section access may see
  audit results (FR-013, FR-015).
- Every delete and every purge action MUST produce exactly one `DeletionLogEntry` (FR-019) — never
  zero (even a fully-skipped action should still log 0 affected + N skipped, for a complete record)
  and never one-per-item.
- A media item referenced only via Member data, or only in a language/culture not covered by scanning
  (should not occur per FR-017, but see residual risk in research.md §4 for Nested Content), is
  classified "Unused" — this is accepted, documented behavior (FR-002, FR-016), not a bug.
