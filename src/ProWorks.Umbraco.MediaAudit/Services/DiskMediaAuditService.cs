using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IDiskMediaAuditService" />
public sealed class DiskMediaAuditService : IDiskMediaAuditService
{
    private const int PageSize = 500;

    private readonly IDiskMediaScanner _scanner;
    private readonly IMediaService _mediaService;
    private readonly IMediaFileSystemProvider _fileSystemProvider;

    private readonly object _lock = new();
    private DiskAuditRun _currentRun = new() { Status = DiskAuditRunStatus.Complete, RunAt = null };
    private IReadOnlyList<DiskMediaItem> _items = Array.Empty<DiskMediaItem>();

    public DiskMediaAuditService(IDiskMediaScanner scanner, IMediaService mediaService, IMediaFileSystemProvider fileSystemProvider)
    {
        _scanner = scanner;
        _mediaService = mediaService;
        _fileSystemProvider = fileSystemProvider;
    }

    public DiskAuditRun GetCurrentAudit()
    {
        lock (_lock)
        {
            return Clone(_currentRun);
        }
    }

    public Task<DiskAuditRun> RunAuditAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentRun.Status == DiskAuditRunStatus.Running)
            {
                return Task.FromResult(Clone(_currentRun));
            }

            _currentRun = new DiskAuditRun { Status = DiskAuditRunStatus.Running };
        }

        _ = Task.Run(() => ExecuteAuditAsync(cancellationToken), cancellationToken);

        return Task.FromResult(GetCurrentAudit());
    }

    public DiskMediaItemsResult GetItems()
    {
        lock (_lock)
        {
            return new DiskMediaItemsResult { Items = _items };
        }
    }

    public bool IsDiskOnly(string relativePath) => !GetOwnedRelativePaths().Contains(Normalize(relativePath));

    private async Task ExecuteAuditAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var diskFiles = await _scanner.ScanAsync(cancellationToken);
            var ownedPaths = GetOwnedRelativePaths();

            var diskOnlyItems = diskFiles
                .Where(f => !ownedPaths.Contains(f.RelativePath))
                .Select(f => new DiskMediaItem
                {
                    RelativePath = f.RelativePath,
                    Url = f.Url,
                    SizeBytes = f.SizeBytes,
                    LastModifiedUtc = f.LastModifiedUtc,
                })
                .OrderBy(i => i.RelativePath)
                .ToList();

            lock (_lock)
            {
                _items = diskOnlyItems;
                _currentRun = new DiskAuditRun
                {
                    RunAt = DateTime.UtcNow,
                    TotalFilesOnDisk = diskFiles.Count,
                    DiskOnlyCount = diskOnlyItems.Count,
                    DiskOnlySizeBytes = diskOnlyItems.Sum(i => i.SizeBytes),
                    Status = DiskAuditRunStatus.Complete,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            lock (_lock)
            {
                _currentRun = new DiskAuditRun
                {
                    RunAt = DateTime.UtcNow,
                    Status = DiskAuditRunStatus.Failed,
                    ErrorMessage = ex.Message,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }
        }
    }

    /// <summary>
    /// Every relative path currently claimed as a media item's stored file, active or trashed
    /// (FR-002, research.md R2) - trashed items still own their file until purged, so their paths
    /// must never be reported as disk-only.
    /// </summary>
    private HashSet<string> GetOwnedRelativePaths()
    {
        var owned = new HashSet<string>();
        long pageIndex = 0;
        long total;
        do
        {
            // Deliberately no ".Where(m => !m.Trashed)" here, unlike MediaAuditService's
            // classification query - a trashed item still owns its file until purged (FR-002).
            var page = _mediaService
                .GetPagedDescendants(CoreConstants.System.Root, pageIndex, PageSize, out total)
                .Where(m => m.ContentType.Alias != CoreConstants.Conventions.MediaTypes.Folder)
                .ToList();

            foreach (var media in page)
            {
                var filePath = MediaFilePathResolver.GetFilePath(media);
                if (!string.IsNullOrWhiteSpace(filePath))
                {
                    owned.Add(Normalize(_fileSystemProvider.FileSystem.GetRelativePath(filePath)));
                }
            }

            pageIndex++;
        } while (pageIndex * PageSize < total);

        return owned;
    }

    private static string Normalize(string path) => path.Replace('\\', '/').TrimStart('/');

    private static DiskAuditRun Clone(DiskAuditRun run) => new()
    {
        RunAt = run.RunAt,
        TotalFilesOnDisk = run.TotalFilesOnDisk,
        DiskOnlyCount = run.DiskOnlyCount,
        DiskOnlySizeBytes = run.DiskOnlySizeBytes,
        Status = run.Status,
        DurationMs = run.DurationMs,
        ErrorMessage = run.ErrorMessage,
    };
}
