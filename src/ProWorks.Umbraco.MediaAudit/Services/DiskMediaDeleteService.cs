using Umbraco.Cms.Core.IO;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IDiskMediaDeleteService" />
public sealed class DiskMediaDeleteService : IDiskMediaDeleteService
{
    private readonly IMediaFileSystemProvider _fileSystemProvider;
    private readonly IDiskMediaAuditService _auditService;
    private readonly IDeletionLogService _deletionLogService;

    public DiskMediaDeleteService(
        IMediaFileSystemProvider fileSystemProvider,
        IDiskMediaAuditService auditService,
        IDeletionLogService deletionLogService)
    {
        _fileSystemProvider = fileSystemProvider;
        _auditService = auditService;
        _deletionLogService = deletionLogService;
    }

    public Task<DiskMediaDeleteResult> DeleteAsync(IReadOnlyList<string> relativePaths, int performingUserId, CancellationToken cancellationToken = default)
    {
        var deleted = new List<string>();
        var loggedItems = new List<DeletionLogItem>();
        var skipped = new List<DiskMediaActionSkip>();
        long totalSizeBytes = 0;

        var fileSystem = _fileSystemProvider.FileSystem;

        foreach (var relativePath in relativePaths)
        {
            cancellationToken.ThrowIfCancellationRequested();

            if (!fileSystem.FileExists(relativePath))
            {
                skipped.Add(new DiskMediaActionSkip { RelativePath = relativePath, Reason = "NotFound" });
                continue;
            }

            if (!_auditService.IsDiskOnly(relativePath))
            {
                skipped.Add(new DiskMediaActionSkip { RelativePath = relativePath, Reason = "NowOwned" });
                continue;
            }

            var sizeBytes = fileSystem.GetSize(relativePath);
            deleted.Add(relativePath);
            loggedItems.Add(new DeletionLogItem { RelativePath = relativePath, Name = relativePath });
            totalSizeBytes += sizeBytes;

            fileSystem.DeleteFile(relativePath);
        }

        RemoveEmptiedFolders(fileSystem, deleted);

        var logEntryId = _deletionLogService.LogAction(
            DeletionLogActionType.DiskDelete,
            performingUserId,
            loggedItems,
            totalSizeBytes,
            skipped.Count);

        return Task.FromResult(new DiskMediaDeleteResult { Deleted = deleted, Skipped = skipped, LogEntryId = logEntryId });
    }

    /// <summary>
    /// Removes each folder a deleted file lived in once it's completely empty, then walks up through
    /// parents that became empty as a result - never the media root, and never a folder holding
    /// anything else. Emptiness is checked here rather than relying on a non-recursive delete
    /// failing, since Azure blob storage always deletes recursively.
    /// </summary>
    private static void RemoveEmptiedFolders(IFileSystem fileSystem, IEnumerable<string> deletedPaths)
    {
        var folders = deletedPaths
            .Select(GetParentFolder)
            .Where(folder => folder.Length > 0)
            .Distinct()
            .OrderByDescending(folder => folder.Count(c => c == '/'));

        foreach (var startFolder in folders)
        {
            var folder = startFolder;
            while (folder.Length > 0
                && fileSystem.DirectoryExists(folder)
                && !fileSystem.GetFiles(folder).Any()
                && !fileSystem.GetDirectories(folder).Any())
            {
                fileSystem.DeleteDirectory(folder, false);
                folder = GetParentFolder(folder);
            }
        }
    }

    private static string GetParentFolder(string relativePath)
    {
        var normalized = relativePath.Replace('\\', '/').TrimEnd('/');
        var lastSlash = normalized.LastIndexOf('/');
        return lastSlash < 0 ? string.Empty : normalized[..lastSlash];
    }
}
