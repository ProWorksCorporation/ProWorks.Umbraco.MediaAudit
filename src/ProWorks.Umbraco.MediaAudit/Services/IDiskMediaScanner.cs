using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Walks every file physically present in Umbraco's configured media file system (research.md R1) -
/// whatever <c>IFileSystem</c> implementation the site uses, local disk by default, not a guessed
/// physical path. Deliberately unaware of which files an Umbraco media item owns; that's
/// <see cref="IDiskMediaAuditService"/>'s job.
/// </summary>
public interface IDiskMediaScanner
{
    /// <summary>Every file under the media file system's root, recursively - directories are walked but never returned themselves.</summary>
    Task<IReadOnlyList<MediaFileEntry>> ScanAsync(CancellationToken cancellationToken = default);
}
