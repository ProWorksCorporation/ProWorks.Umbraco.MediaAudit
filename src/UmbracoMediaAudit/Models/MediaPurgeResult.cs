namespace UmbracoMediaAudit.Models;

/// <summary>Result of a POST /purge batch (FR-018, contracts §POST /purge).</summary>
public sealed class MediaPurgeResult
{
    public required IReadOnlyList<Guid> Purged { get; init; }

    public required IReadOnlyList<MediaActionSkip> Skipped { get; init; }

    public required int LogEntryId { get; init; }
}
