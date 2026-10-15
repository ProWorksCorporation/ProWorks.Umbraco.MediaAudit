namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One disk-only file requested for delete but skipped, and why (contracts/disk-media-api.md
/// §POST /disk-delete). Never causes the whole batch to fail.
/// </summary>
public sealed class DiskMediaActionSkip
{
    public required string RelativePath { get; init; }

    /// <summary>"NotFound" (no longer on disk) or "NowOwned" (an Umbraco media item now claims this path).</summary>
    public required string Reason { get; init; }
}
