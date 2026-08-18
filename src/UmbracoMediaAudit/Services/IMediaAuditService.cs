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

    /// <summary>
    /// Filtered/sorted/paged item listing (FR-002, FR-006, FR-007, FR-008; contracts §GET /items).
    /// </summary>
    MediaAuditItemsResult GetItems(MediaAuditItemsQuery query);

    /// <summary>
    /// The same filter/sort as <see cref="GetItems"/>, but the full matching set with no paging
    /// applied - <paramref name="query"/>'s <c>Page</c>/<c>PageSize</c> are ignored (contracts
    /// §GET /export: "same query params as GET /items, no paging").
    /// </summary>
    IReadOnlyList<MediaAuditItem> GetExportItems(MediaAuditItemsQuery query);

    /// <summary>Media library folders, for the folder filter dropdown (FR-007, data-model.md MediaFolder).</summary>
    IReadOnlyList<MediaFolder> GetFolders();

    /// <summary>Distinct media types (alias + display name) across the last audit run, for the type filter dropdown (FR-007).</summary>
    IReadOnlyList<MediaTypeOption> GetMediaTypeOptions();

    /// <summary>
    /// Content item names referencing this media item, for GET /export's "Used On Pages" column
    /// (FR-009). Relation-based only (research.md §4's fast primary signal, including the
    /// ancestor-folder fallback) rather than the full combined relation+scan lookup
    /// <see cref="GetUsagesAsync"/> does - export can cover a large filtered set at once, and the
    /// scan-based safety net's cost isn't worth paying per row just to name pages in a CSV.
    /// </summary>
    IReadOnlyList<string> GetUsedOnPageNames(int mediaId);

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
