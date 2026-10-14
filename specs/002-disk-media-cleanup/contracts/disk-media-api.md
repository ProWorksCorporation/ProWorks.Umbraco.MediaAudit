# API Contract: Disk-Only Media Cleanup

All routes are additions to the existing `UmbracoMediaAuditApiController`
(`/umbraco/media-audit/api/v1`), same auth model as the existing endpoints.

## GET /disk-summary

Current `DiskAuditRun`. Never null - "no run yet" has `runAt: null`, `status: "Complete"`.

## POST /disk-scan

Triggers a background disk scan. Returns the new `DiskAuditRun` with `status: "Running"`
immediately (202 Accepted). A scan already running is not restarted - its current status is
returned instead, matching POST /run's existing behavior.

## GET /disk-items

Returns every `DiskMediaItem` from the last completed scan. No filtering/sorting/paging in v1
(scope is bounded by spec.md; the existing usage-audit list already provides that pattern if a
future version needs it here too).

```json
{ "items": [ { "relativePath": "1234/old-upload.jpg", "url": "/media/1234/old-upload.jpg", "sizeBytes": 48213, "lastModifiedUtc": "2025-01-01T00:00:00Z" } ] }
```

## GET /disk-export

Same rows as GET /disk-items, as a downloadable `text/csv` file (`disk-media-export.csv`)
with the columns `RelativePath,Url,SizeBytes,LastModifiedUtc`. Not admin-only - mirrors GET /export
for the usage audit, and anyone who can view the list can export it.

## POST /disk-delete

Admin-only (403 for non-admins, same policy as POST /delete). Body: `{ "relativePaths": ["1234/old-upload.jpg"] }`.
Deletes exactly the given files after re-verifying each is still present and still disk-only
(FR-008); skips and reports otherwise. Folders the delete leaves completely empty are then removed,
along with parents that become empty, never the media root (FR-011). Always writes exactly one `DeletionLogEntry`
(`actionType: "DiskDelete"`), even if every item was skipped.

```json
{ "deleted": ["1234/old-upload.jpg"], "skipped": [], "logEntryId": 42 }
```

"Delete All" (User Story 3) is the same endpoint called with every `relativePath` currently listed
by GET /disk-items - no separate endpoint, matching how "select all" + "Delete Selected" already
works for the existing Used/Unused audit.
