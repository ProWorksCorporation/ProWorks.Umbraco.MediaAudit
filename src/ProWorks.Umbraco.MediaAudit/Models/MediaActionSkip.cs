namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One item requested for delete/purge but skipped, and why (contracts §POST /delete, §POST /purge).
/// Never causes the whole batch to fail - the admin is told exactly which items were protected and
/// why, per the spec's race-condition edge cases.
/// </summary>
public sealed class MediaActionSkip
{
    public required Guid MediaKey { get; init; }

    /// <summary>"NowReferenced" (delete), "NotTrashed" (purge), or "NotFound" (either).</summary>
    public required string Reason { get; init; }
}
