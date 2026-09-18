using Umbraco.Cms.Core.IO;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <summary>
/// Thin seam around <c>MediaFileManager.FileSystem</c> (research.md R1) so the disk-audit services
/// depend on the mockable <c>IFileSystem</c> interface directly, rather than the concrete
/// <c>MediaFileManager</c> - keeping <see cref="IDiskMediaScanner"/>/<see cref="IDiskMediaAuditService"/>/
/// <see cref="IDiskMediaDeleteService"/> unit-testable against a fake file system.
/// </summary>
public interface IMediaFileSystemProvider
{
    IFileSystem FileSystem { get; }
}
