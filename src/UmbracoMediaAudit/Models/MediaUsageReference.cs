using System.Text.Json.Serialization;

namespace UmbracoMediaAudit.Models;

/// <summary>
/// One place a <see cref="MediaAuditItem"/> is referenced. Maps to spec.md's "Usage Reference" entity.
/// Populated on demand for a given media item (FR-005), not eagerly for every item in the list.
/// </summary>
public sealed class MediaUsageReference
{
    public required int ContentId { get; init; }

    /// <summary>Used to build the backoffice edit link.</summary>
    public required Guid ContentKey { get; init; }

    public required string ContentName { get; init; }

    public required string ContentTypeAlias { get; init; }

    /// <summary>
    /// Culture/language code the reference was found in. Null for invariant properties/sites.
    /// Required for multi-language coverage (FR-017, research.md §8).
    /// </summary>
    public string? Culture { get; init; }

    /// <summary>Property alias holding the reference. Null if only found by the scan layer without per-property attribution.</summary>
    public string? PropertyAlias { get; init; }

    /// <summary>Confirms FR-004 - both published and draft-only references are surfaced.</summary>
    public required ContentPublishState PublishState { get; init; }

    public required MediaUsageDetectionSource DetectionSource { get; init; }

    /// <summary>Constructed backoffice deep link, used to navigate to the referencing content item (User Story 2).</summary>
    public required string EditUrl { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum ContentPublishState
{
    Draft,
    Published,
}

/// <summary>
/// Which mechanism found this specific usage reference. Unlike <see cref="MediaDetectionSource"/> on
/// MediaAuditItem, a resolved UsageReference is always attributable to exactly one mechanism.
/// </summary>
[JsonConverter(typeof(JsonStringEnumConverter))]
public enum MediaUsageDetectionSource
{
    Relation,
    Scan,
}
