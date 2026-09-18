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

        var logEntryId = _deletionLogService.LogAction(
            DeletionLogActionType.DiskDelete,
            performingUserId,
            loggedItems,
            totalSizeBytes,
            skipped.Count);

        return Task.FromResult(new DiskMediaDeleteResult { Deleted = deleted, Skipped = skipped, LogEntryId = logEntryId });
    }
}
