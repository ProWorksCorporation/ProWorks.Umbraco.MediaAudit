using System.Text.Json.Serialization;

namespace UmbracoMediaAudit.Models;

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

/// <summary>One affected item within a <see cref="DeletionLogEntry"/> - stored as compact JSON, not a join.</summary>
public sealed class DeletionLogItem
{
    public required Guid Key { get; init; }

    public required string Name { get; init; }
}

[JsonConverter(typeof(JsonStringEnumConverter))]
public enum DeletionLogActionType
{
    Delete,
    Purge,
}
