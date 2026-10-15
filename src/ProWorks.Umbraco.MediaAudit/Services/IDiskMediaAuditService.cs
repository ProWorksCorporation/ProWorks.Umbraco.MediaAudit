using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Orchestrates the disk-only audit: walks Umbraco's media file system (<see cref="IDiskMediaScanner"/>)
/// and reports every file that isn't the current stored file of any Umbraco media item, active or
/// trashed (FR-002, research.md R2). Held in memory for the duration of a backoffice session,
/// mirroring <see cref="IMediaAuditService"/>'s pattern for the existing usage audit.
/// </summary>
public interface IDiskMediaAuditService
{
    /// <summary>Current <see cref="DiskAuditRun"/> summary. Never null - a "no run yet" state has <c>RunAt: null</c>.</summary>
    DiskAuditRun GetCurrentAudit();

    /// <summary>
    /// Triggers a new disk scan in the background and returns immediately with the new "Running"
    /// status. A scan already in progress is not restarted - its current status is returned instead.
    /// </summary>
    Task<DiskAuditRun> RunAuditAsync(CancellationToken cancellationToken = default);

    /// <summary>Every <see cref="DiskMediaItem"/> found by the last completed scan (contracts §GET /disk-items).</summary>
    DiskMediaItemsResult GetItems();

    /// <summary>
    /// True if <paramref name="relativePath"/> is not the current stored file of any Umbraco media
    /// item, active or trashed - the same "is this file disk-only" check the scan itself uses,
    /// exposed for <see cref="IDiskMediaDeleteService"/>'s mandatory pre-delete re-check (FR-008).
    /// </summary>
    bool IsDiskOnly(string relativePath);
}
