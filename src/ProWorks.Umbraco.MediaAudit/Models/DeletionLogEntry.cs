using System.Text.Json.Serialization;

namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One delete or purge action taken from the dashboard (FR-019). Maps to spec.md's "Deletion Log
/// Entry" entity and data-model.md. Persisted - the one exception to audit results otherwise being
/// computed on demand - in a package-owned table created via Package Migration (research.md §10).
/// One row per action/batch, never one row per item.
/// </summary>
public sealed class DeletionLogEntry
{
    /// <summary>Package-owned table's own identity column.</summary>
    public int Id { get; init; }

    public required DateTime OccurredAt { get; init; }

    public required DeletionLogActionType ActionType { get; init; }

    /// <summary>Umbraco backoffice user id - always an administrator (FR-015).</summary>
    public required int PerformedByUserId { get; init; }

    /// <summary>Number of media items actually affected by this action (excludes skipped items).</summary>
    public required int ItemCount { get; init; }

    /// <summary>Sum of sizeBytes across affected items, at time of action.</summary>
    public required long TotalSizeBytes { get; init; }

    /// <summary>Affected items' key + name, for display without a join back to (now possibly gone) media nodes.</summary>
    public required IReadOnlyList<DeletionLogItem> Items { get; init; }

    /// <summary>Items requested but skipped this action (e.g. NowReferenced/NotTrashed) - 0 if none.</summary>
    public required int SkippedCount { get; init; }
}

/// <summary>
/// One affected item within a <see cref="DeletionLogEntry"/> - stored as compact JSON, not a join.
/// <see cref="Key"/> identifies an Umbraco media item (Delete/Purge actions); <see cref="RelativePath"/>
/// identifies a disk-only file with no Umbraco media item (DiskDelete actions). Exactly one of the
/// two is set, depending on <see cref="DeletionLogEntry.ActionType"/>.
/// </summary>
public sealed class DeletionLogItem
{
    public Guid? Key { get; init; }

    /// <summary>Set instead of <see cref="Key"/> for a DiskDelete action's items.</summary>
    public string? RelativePath { get; init; }

    public required string Name { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeletionLogActionType
{
    Delete,
    Purge,
    DiskDelete,
}
