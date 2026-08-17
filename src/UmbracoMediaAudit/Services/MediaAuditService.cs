using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using UmbracoMediaAudit.Models;

namespace UmbracoMediaAudit.Services;

/// <inheritdoc cref="IMediaAuditService" />
public sealed class MediaAuditService : IMediaAuditService
{
    private const int PageSize = 500;

    private readonly IMediaService _mediaService;
    private readonly IRelationService _relationService;

    private readonly object _lock = new();
    private AuditRun _currentRun = new() { Status = AuditRunStatus.Complete, RunAt = null };
    private IReadOnlyList<MediaAuditItem> _items = Array.Empty<MediaAuditItem>();

    public MediaAuditService(IMediaService mediaService, IRelationService relationService)
    {
        _mediaService = mediaService;
        _relationService = relationService;
    }

    public AuditRun GetCurrentAudit()
    {
        lock (_lock)
        {
            return Clone(_currentRun);
        }
    }

    public Task<AuditRun> RunAuditAsync(CancellationToken cancellationToken = default)
    {
        lock (_lock)
        {
            if (_currentRun.Status == AuditRunStatus.Running)
            {
                return Task.FromResult(Clone(_currentRun));
            }

            _currentRun = new AuditRun { Status = AuditRunStatus.Running };
        }

        // Fire-and-forget on a background thread so the API request returns immediately (FR-012) -
        // the client polls GET /summary for completion (contracts §POST /run).
        _ = Task.Run(() => ExecuteAuditAsync(cancellationToken), cancellationToken);

        return Task.FromResult(GetCurrentAudit());
    }

    public IReadOnlyList<MediaAuditItem> GetItems(MediaUsageStatus? status = null)
    {
        lock (_lock)
        {
            return status is null
                ? _items.ToList()
                : _items.Where(i => i.UsageStatus == status).ToList();
        }
    }

    private void ExecuteAuditAsync(CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();
        try
        {
            var items = new List<MediaAuditItem>();
            long pageIndex = 0;
            long total;
            do
            {
                cancellationToken.ThrowIfCancellationRequested();

                var page = _mediaService
                    .GetPagedDescendants(CoreConstants.System.Root, pageIndex, PageSize, out total)
                    .ToList();

                items.AddRange(page.Select(ClassifyMedia));

                pageIndex++;
            } while (pageIndex * PageSize < total);

            var usedItems = items.Where(i => i.UsageStatus == MediaUsageStatus.Used).ToList();
            var unusedItems = items.Where(i => i.UsageStatus == MediaUsageStatus.Unused).ToList();

            lock (_lock)
            {
                _items = items;
                _currentRun = new AuditRun
                {
                    RunAt = DateTime.UtcNow,
                    TotalScanned = items.Count,
                    UsedCount = usedItems.Count,
                    UsedSizeBytes = usedItems.Sum(i => i.SizeBytes ?? 0),
                    UnusedCount = unusedItems.Count,
                    UnusedSizeBytes = unusedItems.Sum(i => i.SizeBytes ?? 0),
                    Status = AuditRunStatus.Complete,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            // T060 - Failed audit-run state handling (spec.md edge case): surface the error instead
            // of silently leaving stale results or an indefinite "Running" state.
            lock (_lock)
            {
                _currentRun = new AuditRun
                {
                    RunAt = DateTime.UtcNow,
                    Status = AuditRunStatus.Failed,
                    ErrorMessage = ex.Message,
                    DurationMs = (int)stopwatch.ElapsedMilliseconds,
                };
            }
        }
    }

    /// <summary>
    /// Relation-based classification (research.md §4): a media item is "Used" if Umbraco's tracked
    /// references (IRelationService, populated via IDataValueReference at save time) record at least
    /// one "relatedMedia" relation pointing at it. This is the fast primary signal used for a full
    /// audit run; the scan-based safety net (IMediaReferenceScanner) supplements this per-item on
    /// demand (usage detail, pre-delete/pre-purge re-check) rather than on every item in bulk, to hit
    /// the SC-002 performance target.
    /// </summary>
    private MediaAuditItem ClassifyMedia(IMedia media)
    {
        var relations = _relationService.GetByChildId(media.Id)
            .Where(r => r.RelationType.Alias == CoreConstants.Conventions.RelationTypes.RelatedMediaAlias)
            .ToList();

        var isUsed = relations.Count > 0;

        return new MediaAuditItem
        {
            Id = media.Id,
            Key = media.Key,
            Name = media.Name ?? $"(media {media.Id})",
            MediaTypeAlias = media.ContentType.Alias,
            Extension = media.GetValue<string>(CoreConstants.Conventions.Media.Extension),
            SizeBytes = GetSizeBytes(media),
            Path = ResolveFolderPath(media),
            FolderId = media.ParentId == CoreConstants.System.Root ? null : media.ParentId,
            CreateDate = media.CreateDate,
            UpdateDate = media.UpdateDate,
            UsageStatus = isUsed ? MediaUsageStatus.Used : MediaUsageStatus.Unused,
            UsageCount = relations.Select(r => r.ParentId).Distinct().Count(),
            DetectionSource = isUsed ? MediaDetectionSource.Relation : MediaDetectionSource.None,
        };
    }

    /// <summary>umbracoBytes (research.md §6) - null for container/folder media types that don't have it.</summary>
    private static long? GetSizeBytes(IMedia media)
    {
        var value = media.GetValue<int?>(CoreConstants.Conventions.Media.Bytes);
        return value.HasValue ? value.Value : null;
    }

    /// <summary>
    /// Resolves IMedia.Path's comma-separated id list (e.g. "-1,1234,5678") into a human-readable
    /// folder path (FR-006, FR-007) - resolves the G2 gap flagged in /speckit-analyze.
    /// </summary>
    private string ResolveFolderPath(IMedia media)
    {
        var ancestorIds = media.Path
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .Where(id => id != CoreConstants.System.Root && id != media.Id)
            .ToList();

        if (ancestorIds.Count == 0)
        {
            return "/";
        }

        var names = ancestorIds.Select(id => _mediaService.GetById(id)?.Name ?? $"#{id}");
        return "/" + string.Join("/", names);
    }

    private static AuditRun Clone(AuditRun run) => new()
    {
        RunAt = run.RunAt,
        TotalScanned = run.TotalScanned,
        UsedCount = run.UsedCount,
        UsedSizeBytes = run.UsedSizeBytes,
        UnusedCount = run.UnusedCount,
        UnusedSizeBytes = run.UnusedSizeBytes,
        Status = run.Status,
        DurationMs = run.DurationMs,
        ErrorMessage = run.ErrorMessage,
    };
}
