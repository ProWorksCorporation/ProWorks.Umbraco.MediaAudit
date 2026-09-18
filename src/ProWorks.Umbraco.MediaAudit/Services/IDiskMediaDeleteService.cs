using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Admin-only permanent delete of disk-only files (FR-005, FR-006, FR-007). There is no Recycle
/// Bin for these - they were never Umbraco media items - so this is irreversible, unlike
/// <see cref="IMediaDeleteService"/>.
/// </summary>
public interface IDiskMediaDeleteService
{
    /// <summary>
    /// Deletes the given files, after re-verifying each is still present on disk and still
    /// disk-only immediately before deleting it (FR-008: protects against a file that's since been
    /// claimed by a newly created media item, or already removed by something else). Always writes
    /// exactly one <see cref="Models.DeletionLogEntry"/> (<c>DiskDelete</c>), even if every file was
    /// skipped.
    /// </summary>
    Task<DiskMediaDeleteResult> DeleteAsync(IReadOnlyList<string> relativePaths, int performingUserId, CancellationToken cancellationToken = default);
}
