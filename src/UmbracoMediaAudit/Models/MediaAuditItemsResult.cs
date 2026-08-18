namespace UmbracoMediaAudit.Models;

/// <summary>Service-layer result for a filtered/sorted/paged GET /items request.</summary>
public sealed class MediaAuditItemsResult
{
    /// <summary>The current page's items.</summary>
    public required IReadOnlyList<MediaAuditItem> Items { get; init; }

    /// <summary>Total items matching the filter, before paging - drives the response envelope's <c>totalItems</c>.</summary>
    public required int TotalItems { get; init; }
}
