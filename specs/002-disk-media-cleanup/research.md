# Research: Disk-Only Media Cleanup

## R1: How to enumerate every file in Umbraco's media storage without assuming a physical path

**Decision**: Inject `Umbraco.Cms.Core.IO.MediaFileManager` (a concrete singleton Umbraco core
already registers) and walk `MediaFileManager.FileSystem` (`IFileSystem`) recursively: start at the
root path `""`, call `GetDirectories(path)` for immediate subdirectories and `GetFiles(path)` for
files directly in `path`, and recurse into every subdirectory returned. This works against
whatever `IFileSystem` implementation the site is configured with (local disk by default, but also
e.g. an Azure Blob-backed one), matching the assumption in spec.md.

**Rationale**: `IFileSystem`'s documented contract (Umbraco C# API docs, `IFileSystem` interface)
is that `GetFiles`/`GetDirectories` operate on the given path only - callers walk the tree
themselves for a full recursive listing. This is the same abstraction Umbraco itself uses
internally for the media file system, so it is the only approach that stays correct regardless of
storage provider (Principle II: documentation-driven, not a guessed physical path).

**Alternatives considered**: Reading `IOptions<GlobalSettings>`/hosting `wwwroot/media` directly
with `System.IO` - rejected because it silently breaks for any non-local-disk media file system and
duplicates a path Umbraco already owns; the constitution's Principle II specifically calls out
guessing platform behavior as a defect, not a shortcut.

## R2: How to determine which disk files are "owned" by an Umbraco media item

**Decision**: Reuse the existing `MediaReferenceScanner.GetMediaFilePath`-equivalent resolution
(the `umbracoFile` property, either a plain string or an image-cropper JSON blob's `src`) across
every non-folder media item returned by `IMediaService.GetPagedDescendants`, **without** filtering
out trashed items (unlike the existing Used/Unused audit, which only classifies non-trashed items -
FR-002/Edge Cases require Recycle Bin files to still count as owned). Build a `HashSet<string>` of
normalized (lower-cased, `/`-separated, no leading slash) relative paths from this, then treat any
disk file whose normalized path isn't in that set as disk-only.

**Rationale**: This is the same signal already trusted elsewhere in the package for "what file does
this media item actually use," so a disk-only file determined this way is consistent with how the
rest of the package reasons about media file identity, and correctly protects Recycle Bin items
per the spec's edge cases.

**Alternatives considered**: Comparing by file name only - rejected: collisions across folders
would misclassify genuinely-owned files in a different folder as disk-only.

## R3: Recording disk deletions in the existing Deletion Log without a schema change

**Decision**: Add a `DiskDelete` member to the existing `DeletionLogActionType` enum (stored as a
string column already - no migration needed) and make `DeletionLogItem.Key` nullable
(`Guid? Key`) with a new optional `RelativePath` field, populated for disk-only items (which have
no Umbraco media key) instead of `Key`. The `Items` column is already a serialized JSON blob, so
adding an optional property to the shape needs no migration - the existing
`AddDeletionLogTablePlan` table (and its already-`nvarchar(max)`-fixed `Items` column, see the
003/004 fix branches) is reused as-is.

**Rationale**: Keeps disk deletes in the same audit trail admins already check (FR-010) without a
second logging mechanism or a migration for a one-off feature.

**Alternatives considered**: A separate disk-deletion log table - rejected as needless duplication
for what the spec calls out as the same auditability need (SC-004) the existing log already serves.
