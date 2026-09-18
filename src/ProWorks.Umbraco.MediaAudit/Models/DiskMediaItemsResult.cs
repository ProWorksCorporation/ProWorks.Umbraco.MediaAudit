namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>Result of GET /disk-items (contracts/disk-media-api.md).</summary>
public sealed class DiskMediaItemsResult
{
    public required IReadOnlyList<DiskMediaItem> Items { get; init; }
}
