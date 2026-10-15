using Umbraco.Cms.Core.IO;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IMediaFileSystemProvider" />
public sealed class MediaFileSystemProvider : IMediaFileSystemProvider
{
    public MediaFileSystemProvider(MediaFileManager mediaFileManager)
    {
        FileSystem = mediaFileManager.FileSystem;
    }

    public IFileSystem FileSystem { get; }
}
