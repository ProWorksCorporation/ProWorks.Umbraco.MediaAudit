# Data Model: Disk-Only Media Cleanup

## DiskMediaItem

One file found on disk with no owning Umbraco media item (spec.md "Disk Media Item").

| Field | Type | Notes |
|---|---|---|
| RelativePath | string | Normalized (`/`-separated) path within the media file system, e.g. `1234/photo.jpg`. Stable identifier for select/delete requests. |
| Url | string | `IFileSystem.GetUrl(RelativePath)` - what spec.md's "list all the URLs" asks for. |
| SizeBytes | long | `IFileSystem.GetSize(RelativePath)`. |
| LastModifiedUtc | DateTime | `IFileSystem.GetLastModified(RelativePath)`, converted to UTC. |

## DiskAuditRun

One execution of the disk scan (spec.md "Disk Audit Run"). Mirrors the existing `AuditRun` shape
and lifecycle (Running/Complete/Failed), held in memory for the session, replaced by each new run.

| Field | Type | Notes |
|---|---|---|
| RunAt | DateTime? | Null while Running with no prior run. |
| TotalFilesOnDisk | int | Every file the scan walked, owned or not. |
| DiskOnlyCount | int | Count of `DiskMediaItem`s found. |
| DiskOnlySizeBytes | long | Sum of their sizes. |
| Status | enum: Running / Complete / Failed | |
| DurationMs | int? | |
| ErrorMessage | string? | Set when Status is Failed. |

## DiskMediaDeleteResult

Result of a delete (selected or all) batch.

| Field | Type | Notes |
|---|---|---|
| Deleted | IReadOnlyList\<string\> | Relative paths actually removed. |
| Skipped | IReadOnlyList\<DiskMediaActionSkip\> | Relative path + reason ("NotFound" or "NowOwned"). |
| LogEntryId | int | The `DeletionLogEntry` this batch was recorded under. |

## Existing entities extended (no schema change)

- `DeletionLogActionType` gains a `DiskDelete` member alongside `Delete`/`Purge` (string column,
  no migration).
- `DeletionLogItem.Key` becomes `Guid?` (nullable) and gains an optional `RelativePath` (string?),
  so one deletion-log-item shape covers both Umbraco-media deletes (`Key` set) and disk-only
  deletes (`RelativePath` set) - the `Items` column is a JSON blob, so this needs no migration.
