namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>Result of a POST /delete batch (FR-014, contracts §POST /delete).</summary>
public sealed class MediaDeleteResult
{
    public required IReadOnlyList<Guid> Deleted { get; init; }

    public required IReadOnlyList<MediaActionSkip> Skipped { get; init; }

    public required int LogEntryId { get; init; }
}
