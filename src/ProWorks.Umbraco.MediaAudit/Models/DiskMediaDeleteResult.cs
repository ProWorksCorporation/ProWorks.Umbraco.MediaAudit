namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>Result of a POST /disk-delete batch (FR-005, FR-006, contracts/disk-media-api.md).</summary>
public sealed class DiskMediaDeleteResult
{
    public required IReadOnlyList<string> Deleted { get; init; }

    public required IReadOnlyList<DiskMediaActionSkip> Skipped { get; init; }

    public required int LogEntryId { get; init; }
}
