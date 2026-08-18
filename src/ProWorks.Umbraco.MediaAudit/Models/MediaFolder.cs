namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// A media library container, used for the folder filter (FR-007) and location display (FR-006).
/// Maps to spec.md's "Media Folder" entity.
/// </summary>
public sealed class MediaFolder
{
    public required int Id { get; init; }

    public required string Name { get; init; }

    /// <summary>Full folder path for breadcrumb-style display.</summary>
    public required string Path { get; init; }

    /// <summary>Null at the Media library root.</summary>
    public int? ParentId { get; init; }
}
