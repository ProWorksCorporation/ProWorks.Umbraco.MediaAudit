using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Reads/writes the <see cref="DeletionLogEntry"/> table (FR-019, research.md §10). Every delete
/// and every purge action writes exactly one entry - never one per item, and never zero even if
/// every requested item was skipped (a fully-skipped action is still a complete, loggable record).
/// </summary>
public interface IDeletionLogService
{
    /// <summary>Writes one log entry for a completed delete/purge batch and returns its new id.</summary>
    int LogAction(
        DeletionLogActionType actionType,
        int performedByUserId,
        IReadOnlyList<DeletionLogItem> items,
        long totalSizeBytes,
        int skippedCount);

    /// <summary>Paged history, newest first (contracts §GET /deletion-log).</summary>
    (IReadOnlyList<DeletionLogEntry> Entries, int TotalItems) GetPagedHistory(int page, int pageSize);
}
