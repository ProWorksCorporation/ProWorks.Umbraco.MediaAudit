namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One physical file found while walking Umbraco's media file system (<see cref="Services.IDiskMediaScanner"/>),
/// before it's known whether an Umbraco media item owns it. <see cref="Services.IDiskMediaAuditService"/>
/// diffs these against known media file paths to produce the disk-only <see cref="DiskMediaItem"/> list.
/// </summary>
public sealed class MediaFileEntry
{
    /// <summary>Normalized, forward-slash relative path within the media file system.</summary>
    public required string RelativePath { get; init; }

    public required string Url { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTime LastModifiedUtc { get; init; }
}
