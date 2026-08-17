using System.Text.Json.Serialization;

namespace UmbracoMediaAudit.Models;

/// <summary>
/// One media library file's audit status. Maps to spec.md's "Media Item" entity
/// and data-model.md's MediaAuditItem. FR-002, FR-006.
/// </summary>
public sealed class MediaAuditItem
{
    /// <summary>Umbraco media node id.</summary>
    public required int Id { get; init; }

    /// <summary>Stable identifier used in detail/delete/purge requests instead of <see cref="Id"/>.</summary>
    public required Guid Key { get; init; }

    public required string Name { get; init; }

    /// <summary>e.g. "image", "file" - <c>IMedia.ContentType.Alias</c>.</summary>
    public required string MediaTypeAlias { get; init; }

    /// <summary>From the built-in <c>umbracoExtension</c> property. Null for container/folder media types (research.md §6).</summary>
    public string? Extension { get; init; }

    /// <summary>From the built-in <c>umbracoBytes</c> property. Null for container/folder media types (research.md §6).</summary>
    public long? SizeBytes { get; init; }

    /// <summary>Human-readable folder path, resolved from <c>IMedia.Path</c>'s id list (FR-006, FR-007).</summary>
    public required string Path { get; init; }

    /// <summary>Immediate parent node id. Null if the item is at the Media library root.</summary>
    public int? FolderId { get; init; }

    public required DateTime CreateDate { get; init; }

    public required DateTime UpdateDate { get; init; }

    public required MediaUsageStatus UsageStatus { get; init; }

    /// <summary>Count of distinct referencing content items. Drives sort/summary (FR-008, FR-010).</summary>
    public required int UsageCount { get; init; }

    /// <summary>
    /// Which mechanism(s) found a reference. In v1 only <see cref="MediaDetectionSource.Relation"/> or
    /// <see cref="MediaDetectionSource.None"/> are ever produced here - Scan/Both are reserved for the
    /// deferred deep-scan mode (research.md §4) and not currently reachable.
    /// </summary>
    public required MediaDetectionSource DetectionSource { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaUsageStatus
{
    Used,
    Unused,
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaDetectionSource
{
    None,
    Relation,
    Scan,
    Both,
}
