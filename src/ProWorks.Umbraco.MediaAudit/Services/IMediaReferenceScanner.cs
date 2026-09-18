using Umbraco.Cms.Core.Models;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Scan-based safety net (research.md §4), ported from the reference Python implementation
/// (reference/media_audit.py): searches every content item's currently-saved property values -
/// across every configured language/segment (research.md §8) - for the target media's GUID
/// (with and without hyphens) or file path/filename, case-insensitively.
///
/// This is editor-agnostic: it doesn't matter which property editor stored the reference or
/// whether that editor implements Umbraco's native IDataValueReference correctly. It exists
/// specifically to catch what the fast, relation-based check (see IMediaAuditService) might miss -
/// e.g. content imported or migrated without triggering Umbraco's normal relation-building save
/// pipeline.
///
/// Deliberately scoped to page/document content (IContentService) only - Member data is out of
/// scope for this version (FR-002, research.md §9).
/// </summary>
public interface IMediaReferenceScanner
{
    /// <summary>
    /// Scans all page/document content for references to <paramref name="media"/>. Used as (a) the
    /// mandatory per-item re-check immediately before a delete or purge executes, and (b) the
    /// on-demand supplement to the relation-based lookup when an editor opens a "Used" item's usage
    /// detail (GET /items/{key}/usages).
    /// </summary>
    Task<IReadOnlyList<MediaUsageReference>> FindReferencesAsync(IMedia media, CancellationToken cancellationToken = default);
}
