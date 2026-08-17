using UmbracoMediaAudit.Models;

namespace UmbracoMediaAudit.Services;

/// <summary>
/// Orchestrates the media audit: relation-based Used/Unused classification (the fast primary signal,
/// research.md §4) over the whole media library, held in memory for the duration of a backoffice
/// session (spec.md Assumptions - audit results are computed on demand, not persisted).
/// </summary>
public interface IMediaAuditService
{
    /// <summary>Current <see cref="AuditRun"/> summary (FR-010, FR-011). Never null - a "no run yet" state has <c>RunAt: null</c>.</summary>
    AuditRun GetCurrentAudit();

    /// <summary>
    /// Triggers a new audit run in the background and returns immediately with the new "Running"
    /// status (FR-011). A run already in progress is not restarted - its current status is returned
    /// instead (contracts §POST /run).
    /// </summary>
    Task<AuditRun> RunAuditAsync(CancellationToken cancellationToken = default);

    /// <summary>Base item listing, filtered by usage status only (FR-002, FR-006). Full filter/sort/paging is added in User Story 3.</summary>
    IReadOnlyList<MediaAuditItem> GetItems(MediaUsageStatus? status = null);
}
