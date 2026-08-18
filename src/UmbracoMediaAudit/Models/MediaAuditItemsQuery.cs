using System.Text.Json.Serialization;

namespace UmbracoMediaAudit.Models;

/// <summary>
/// Filter/sort/paging options shared by GET /items and GET /export (contracts §GET /items,
/// §GET /export) - User Story 3, FR-007, FR-008.
/// </summary>
public sealed class MediaAuditItemsQuery
{
    public MediaUsageStatus? Status { get; init; }

    public string? MediaTypeAlias { get; init; }

    public int? FolderId { get; init; }

    public MediaAuditSortField Sort { get; init; } = MediaAuditSortField.Name;

    public MediaAuditSortDirection SortDirection { get; init; } = MediaAuditSortDirection.Asc;

    /// <summary>Ignored by GET /export (contracts: "same query params as GET /items, no paging").</summary>
    public int Page { get; init; } = 1;

    /// <summary>Ignored by GET /export. Clamped to [1, 200] by the service.</summary>
    public int PageSize { get; init; } = 50;
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaAuditSortField
{
    Name,
    SizeBytes,
    UpdateDate,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaAuditSortDirection
{
    Asc,
    Desc,
}
