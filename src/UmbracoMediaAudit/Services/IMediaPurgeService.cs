using UmbracoMediaAudit.Models;

namespace UmbracoMediaAudit.Services;

/// <summary>
/// Admin-only purge: permanently removes specific, already-trashed media items on demand (FR-018,
/// research.md §5). Scoped to exactly the requested items via per-item <c>IMediaService.Delete()</c>
/// - never <c>IMediaService.EmptyRecycleBin()</c>, which is untargeted and would risk destroying
/// unrelated trashed content.
/// </summary>
public interface IMediaPurgeService
{
    /// <summary>
    /// Purges the given items, after re-verifying each is still Trashed immediately before purging
    /// it (spec.md edge case: an item restored out of the Recycle Bin since being soft-deleted must
    /// be skipped, not purged). Always writes exactly one <see cref="Models.DeletionLogEntry"/>, even
    /// if every item was skipped.
    /// </summary>
    MediaPurgeResult Purge(IReadOnlyList<Guid> mediaKeys, int performingUserId);
}
