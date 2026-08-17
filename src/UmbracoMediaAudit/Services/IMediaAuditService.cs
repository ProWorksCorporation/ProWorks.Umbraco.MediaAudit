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

    /// <summary>
    /// Resolves the combined relation+scan <see cref="MediaUsageReference"/> list for one media item
    /// (FR-004, FR-005, FR-017; contracts §GET /items/{key}/usages). Runs lazily, on demand, not
    /// precomputed for every row in <see cref="GetItems"/>.
    ///
    /// Returns null if no media item exists for <paramref name="mediaKey"/> (controller maps to 404).
    /// Returns an empty (but non-null) list for a "Used" item whose relation(s) point at content that
    /// no longer resolves (e.g. deleted) and that the scan-based layer also can't find text evidence
    /// for - this is the stale-relation data-integrity condition data-model.md calls out; the client
    /// MUST surface it distinctly rather than rendering it as an unexplained empty list.
    /// </summary>
    Task<IReadOnlyList<MediaUsageReference>?> GetUsagesAsync(Guid mediaKey, CancellationToken cancellationToken = default);
}
