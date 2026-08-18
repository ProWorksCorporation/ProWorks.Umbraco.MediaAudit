using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Admin-only delete: moves confirmed-unused media to the Recycle Bin (FR-014, research.md §5).
/// Reversible - not a permanent hard delete (that's <see cref="IMediaPurgeService"/>'s job).
/// </summary>
public interface IMediaDeleteService
{
    /// <summary>
    /// Deletes the given items, after re-verifying each is still unused immediately before deleting
    /// it (spec.md edge case: protect against deleting items that became referenced since the last
    /// audit). Always writes exactly one <see cref="Models.DeletionLogEntry"/>, even if every item
    /// was skipped.
    /// </summary>
    Task<MediaDeleteResult> DeleteAsync(IReadOnlyList<Guid> mediaKeys, int performingUserId, CancellationToken cancellationToken = default);
}
