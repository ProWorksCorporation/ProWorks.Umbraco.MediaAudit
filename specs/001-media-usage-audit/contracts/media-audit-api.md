# Contract: Media Audit Management API

**Feature**: [spec.md](../spec.md) | **Data model**: [../data-model.md](../data-model.md)

This package exposes its backoffice-facing interface as Umbraco Management API endpoints (the
backoffice client calls these; they are not intended as a public integration surface). All endpoints
require authenticated backoffice access with permission to the Media section (FR-013); endpoints
marked **Admin-only** additionally require administrator-level privileges (FR-015) and MUST return
`403 Forbidden` for a non-admin caller rather than silently omitting the action.

Base path: `/umbraco/media-audit/api/v1`

## GET /summary

Returns the current `AuditRun` summary (FR-010, FR-011).

**Response 200**:
```json
{
  "runAt": "2026-08-17T14:32:00Z",
  "status": "Complete",
  "totalScanned": 4213,
  "usedCount": 3480, "usedSizeBytes": 812345678,
  "unusedCount": 733, "unusedSizeBytes": 94112233,
  "durationMs": 18422
}
```
If no audit has run yet this session, `status: "Complete"` fields are omitted and `runAt: null`.

## POST /run

Triggers a new audit run (FR-011). Idempotent-in-intent: a run already `Running` returns its current
status rather than starting a second concurrent run.

**Response 202**: `{ "status": "Running" }` — client polls `GET /summary` until `status: "Complete"`.

## GET /items

Returns the paged, filtered, sorted list of `MediaAuditItem` (FR-006, FR-007, FR-008).

**Query params**: `status` (`Used`\|`Unused`, optional), `mediaTypeAlias` (optional),
`folderId` (optional), `sort` (`name`\|`sizeBytes`\|`updateDate`, default `name`),
`sortDirection` (`asc`\|`desc`, default `asc`), `page` (default 1), `pageSize` (default 50, max 200).

**Response 200**:
```json
{
  "page": 1, "pageSize": 50, "totalItems": 733,
  "items": [
    {
      "id": 1234, "key": "b1a2...", "name": "hero-banner-old.jpg",
      "mediaTypeAlias": "image", "extension": "jpg", "sizeBytes": 482113,
      "path": "/Campaign Assets/2023", "folderId": 987,
      "createDate": "2023-04-01T09:00:00Z", "updateDate": "2023-04-01T09:00:00Z",
      "usageStatus": "Unused", "usageCount": 0, "detectionSource": "None"
    }
  ]
}
```

## GET /items/{key}/usages

Returns the list of `UsageReference` for one media item (FR-005). Runs lazily — not precomputed for
every row in `GET /items` — since only the items an editor actually opens need this detail.

**Response 200**:
```json
{
  "mediaKey": "c9f1...",
  "usages": [
    {
      "contentId": 5551, "contentKey": "a001...", "contentName": "Homepage",
      "contentTypeAlias": "landingPage", "culture": "en-US", "propertyAlias": "heroImage",
      "publishState": "Published", "detectionSource": "Relation",
      "editUrl": "/umbraco/section/content/workspace/document/edit/a001..."
    }
  ]
}
```
**Response 200, empty usages on a `Used` item**: signals the data-integrity condition noted in
data-model.md (stale relation) — the client MUST surface this distinctly rather than rendering an
empty, unexplained list.

## GET /export

Returns the currently filtered/sorted result set (same query params as `GET /items`, no paging) as a
downloadable file (FR-009). Response `Content-Type` is a spreadsheet-compatible format
(e.g. `text/csv`); columns match the `MediaAuditItem` fields used for display (name, status, size,
type, folder, dates).

## POST /delete — **Admin-only**

Deletes (moves to Recycle Bin — research.md §5) the given `Unused` media items (FR-014, FR-015),
after a fresh per-item safety re-check (data-model.md validation rules). Always writes exactly one
`DeletionLogEntry` (`actionType: "Delete"`, FR-019), even if every item was skipped.

**Request**:
```json
{ "mediaKeys": ["b1a2...", "c9f1..."] }
```

**Response 200**:
```json
{
  "deleted": ["b1a2..."],
  "skipped": [
    { "mediaKey": "c9f1...", "reason": "NowReferenced" }
  ],
  "logEntryId": 42
}
```
A `skipped` entry with `reason: "NowReferenced"` corresponds to the edge case where the item became
referenced after the last audit — it is never deleted, and the response tells the admin exactly which
items were protected and why, rather than failing the whole batch silently.

**Response 403**: caller is not an administrator. Returned instead of performing any deletion.

## POST /purge — **Admin-only**

Permanently removes specific, already-trashed media items (FR-018), by calling `Delete()` per item
rather than `EmptyRecycleBin()` (research.md §5) — scoped to exactly what's requested, not the whole
Recycle Bin. Requires its own confirmation on the client, distinct from `/delete`'s, since this step
is irreversible. Always writes exactly one `DeletionLogEntry` (`actionType: "Purge"`, FR-019).

**Request**:
```json
{ "mediaKeys": ["b1a2..."] }
```

**Response 200**:
```json
{
  "purged": ["b1a2..."],
  "skipped": [
    { "mediaKey": "c9f1...", "reason": "NotTrashed" }
  ],
  "logEntryId": 43
}
```
A `skipped` entry with `reason: "NotTrashed"` corresponds to the edge case where the item was already
restored out of the Recycle Bin by someone else since being soft-deleted — it is never purged.

**Response 403**: caller is not an administrator. Returned instead of performing any purge.

## GET /deletion-log — **Admin-only**

Returns the paged history of `DeletionLogEntry` records (FR-019), newest first, for accountability
review. Not exposed to non-admin viewers, consistent with delete/purge themselves being admin-only.

**Query params**: `page` (default 1), `pageSize` (default 50, max 200).

**Response 200**:
```json
{
  "page": 1, "pageSize": 50, "totalItems": 12,
  "entries": [
    {
      "id": 43, "occurredAt": "2026-08-17T15:10:00Z", "actionType": "Purge",
      "performedByUserId": 7, "itemCount": 2, "totalSizeBytes": 964226,
      "items": [{ "key": "b1a2...", "name": "hero-banner-old.jpg" }],
      "skippedCount": 1
    }
  ]
}
```
