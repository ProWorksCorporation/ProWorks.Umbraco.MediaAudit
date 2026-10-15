namespace ProWorks.Umbraco.MediaAudit.Models;

/// <summary>
/// One file found in Umbraco's media file system during a disk scan that has no corresponding
/// Umbraco media item (spec.md "Disk Media Item", FR-002, FR-003).
/// </summary>
public sealed class DiskMediaItem
{
    /// <summary>Normalized, forward-slash relative path within the media file system - e.g. "1234/photo.jpg". Stable identifier for select/delete requests.</summary>
    public required string RelativePath { get; init; }

    /// <summary><c>IFileSystem.GetUrl(RelativePath)</c> - what spec.md's "list all the URLs" asks for.</summary>
    public required string Url { get; init; }

    public required long SizeBytes { get; init; }

    public required DateTime LastModifiedUtc { get; init; }
}
