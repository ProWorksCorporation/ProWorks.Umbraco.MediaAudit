using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IDiskMediaScanner" />
public sealed class DiskMediaScanner : IDiskMediaScanner
{
    private readonly IMediaFileSystemProvider _fileSystemProvider;

    public DiskMediaScanner(IMediaFileSystemProvider fileSystemProvider)
    {
        _fileSystemProvider = fileSystemProvider;
    }

    public Task<IReadOnlyList<MediaFileEntry>> ScanAsync(CancellationToken cancellationToken = default)
    {
        var results = new List<MediaFileEntry>();
        WalkDirectory(string.Empty, results, cancellationToken);
        return Task.FromResult<IReadOnlyList<MediaFileEntry>>(results);
    }

    /// <summary>
    /// <c>IFileSystem.GetFiles</c>/<c>GetDirectories</c> each operate on exactly the given path, not
    /// recursively (research.md R1) - this walks the tree itself, one directory at a time.
    /// </summary>
    private void WalkDirectory(string path, List<MediaFileEntry> results, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();

        var fileSystem = _fileSystemProvider.FileSystem;

        foreach (var filePath in fileSystem.GetFiles(path))
        {
            cancellationToken.ThrowIfCancellationRequested();

            results.Add(new MediaFileEntry
            {
                RelativePath = NormalizePath(filePath),
                Url = fileSystem.GetUrl(filePath),
                SizeBytes = fileSystem.GetSize(filePath),
                LastModifiedUtc = fileSystem.GetLastModified(filePath).UtcDateTime,
            });
        }

        foreach (var directoryPath in fileSystem.GetDirectories(path))
        {
            WalkDirectory(directoryPath, results, cancellationToken);
        }
    }

    /// <summary>Normalizes to a forward-slash, no-leading-slash relative path so it compares reliably against media items' stored <c>umbracoFile</c> values (research.md R2).</summary>
    private static string NormalizePath(string path) =>
        path.Replace('\\', '/').TrimStart('/');
}
