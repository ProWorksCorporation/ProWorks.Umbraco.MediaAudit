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
    private readonly IContentService _contentService;
    private readonly IMediaReferenceScanner _scanner;

    private readonly object _lock = new();
    private AuditRun _currentRun = new() { Status = AuditRunStatus.Complete, RunAt = null };
    private IReadOnlyList<MediaAuditItem> _items = Array.Empty<MediaAuditItem>();

    public MediaAuditService(
        IMediaService mediaService,
        IRelationService relationService,
        IContentService contentService,
        IMediaReferenceScanner scanner)
    {
        _mediaService = mediaService;
        _relationService = relationService;
        _contentService = contentService;
        _scanner = scanner;
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

    /// <inheritdoc />
    public MediaAuditItemsResult GetItems(MediaAuditItemsQuery query)
    {
        var filtered = FilterAndSort(query);
        var pageSize = Math.Clamp(query.PageSize, 1, 200);
        var page = Math.Max(query.Page, 1);

        return new MediaAuditItemsResult
        {
            Items = filtered.Skip((page - 1) * pageSize).Take(pageSize).ToList(),
            TotalItems = filtered.Count,
        };
    }

    /// <inheritdoc />
    public IReadOnlyList<MediaAuditItem> GetExportItems(MediaAuditItemsQuery query) => FilterAndSort(query);

    /// <inheritdoc />
    public IReadOnlyList<MediaFolder> GetFolders()
    {
        var folders = new List<MediaFolder>();
        long pageIndex = 0;
        long total;
        do
        {
            var page = _mediaService
                .GetPagedDescendants(CoreConstants.System.Root, pageIndex, PageSize, out total)
                .Where(m => m.ContentType.Alias == CoreConstants.Conventions.MediaTypes.Folder)
                .ToList();

            folders.AddRange(page.Select(f => new MediaFolder
            {
                Id = f.Id,
                Name = f.Name ?? $"(folder {f.Id})",
                Path = ResolveFullPath(f),
                ParentId = f.ParentId == CoreConstants.System.Root ? null : f.ParentId,
            }));

            pageIndex++;
        } while (pageIndex * PageSize < total);

        return folders;
    }

    /// <inheritdoc />
    public IReadOnlyList<string> GetUsedOnPageNames(int mediaId)
    {
        var media = _mediaService.GetById(mediaId);
        if (media is null)
        {
            return Array.Empty<string>();
        }

        var contentIds = GetRelatedContentIds(mediaId);
        if (contentIds.Count == 0)
        {
            contentIds = GetAncestorFolderRelatedContentIds(media);
        }

        return contentIds
            .Select(id => _contentService.GetById(id)?.Name)
            .Where(name => !string.IsNullOrEmpty(name))
            .Select(name => name!)
            .OrderBy(name => name)
            .ToList();
    }

    /// <inheritdoc />
    public IReadOnlyList<MediaTypeOption> GetMediaTypeOptions()
    {
        lock (_lock)
        {
            return _items
                .GroupBy(i => i.MediaTypeAlias)
                .Select(g => new MediaTypeOption { Alias = g.Key, Name = g.First().MediaTypeName })
                .OrderBy(o => o.Name)
                .ToList();
        }
    }

    /// <summary>Applies GET /items'/GET /export's shared status/type/folder filter and sort (FR-007, FR-008).</summary>
    private List<MediaAuditItem> FilterAndSort(MediaAuditItemsQuery query)
    {
        IEnumerable<MediaAuditItem> items;
        lock (_lock)
        {
            items = _items.ToList();
        }

        if (query.Status is not null)
        {
            items = items.Where(i => i.UsageStatus == query.Status);
        }

        if (!string.IsNullOrWhiteSpace(query.MediaTypeAlias))
        {
            items = items.Where(i => i.MediaTypeAlias == query.MediaTypeAlias);
        }

        if (query.FolderId is not null)
        {
            items = items.Where(i => i.FolderId == query.FolderId);
        }

        items = query.Sort switch
        {
            MediaAuditSortField.SizeBytes => OrderBy(items, i => i.SizeBytes ?? 0, query.SortDirection),
            MediaAuditSortField.UpdateDate => OrderBy(items, i => i.UpdateDate, query.SortDirection),
            _ => OrderBy(items, i => i.Name, query.SortDirection),
        };

        return items.ToList();
    }

    private static IOrderedEnumerable<MediaAuditItem> OrderBy<TKey>(
        IEnumerable<MediaAuditItem> items,
        Func<MediaAuditItem, TKey> keySelector,
        MediaAuditSortDirection direction) =>
        direction == MediaAuditSortDirection.Desc
            ? items.OrderByDescending(keySelector)
            : items.OrderBy(keySelector);

    /// <inheritdoc />
    public async Task<IReadOnlyList<MediaUsageReference>?> GetUsagesAsync(Guid mediaKey, CancellationToken cancellationToken = default)
    {
        var media = _mediaService.GetById(mediaKey);
        if (media is null)
        {
            return null;
        }

        // Authoritative "is it used, and by which content items" signal (research.md §4).
        var relationContentIds = GetRelatedContentIds(media.Id);

        // Editor-agnostic per-property/culture scan (research.md §4, §8) - this is what actually
        // attributes *which* culture/property holds the reference, since IRelation itself carries no
        // culture/property information. It naturally covers every content item, not just
        // relation-linked ones, so it also catches anything the relation layer missed.
        var scanResults = (await _scanner.FindReferencesAsync(media, cancellationToken)).ToList();

        // Gallery/slideshow pattern (see GetAncestorFolderRelatedContentIds's doc comment): nothing
        // found directly on the item itself - check whether a folder it lives in is what's actually
        // referenced, for both signals, before concluding this "Used" item resolves to zero usages.
        if (relationContentIds.Count == 0 && scanResults.Count == 0)
        {
            relationContentIds = GetAncestorFolderRelatedContentIds(media);

            foreach (var ancestorId in GetAncestorIds(media))
            {
                var ancestor = _mediaService.GetById(ancestorId);
                if (ancestor is null)
                {
                    continue;
                }

                scanResults.AddRange(await _scanner.FindReferencesAsync(ancestor, cancellationToken));
            }
        }

        var results = new List<MediaUsageReference>();
        var attributedContentIds = new HashSet<int>();

        foreach (var usage in scanResults)
        {
            var isRelationConfirmed = relationContentIds.Contains(usage.ContentId);
            if (isRelationConfirmed)
            {
                attributedContentIds.Add(usage.ContentId);
            }

            // A relation is the authoritative "used" signal even when the scan also independently
            // matched the same content item - report it as Relation rather than Scan in that case.
            results.Add(isRelationConfirmed ? WithDetectionSource(usage, MediaUsageDetectionSource.Relation) : usage);
        }

        // A relation exists for a content item the scan found no textual match in (e.g. a
        // property/editor whose stored value doesn't literally contain the media's GUID/path). Still
        // surface it - without per-property/culture attribution - rather than silently dropping it;
        // if the content itself no longer resolves (stale relation to deleted content), it's skipped,
        // which is exactly the data-integrity condition callers must handle (see interface doc).
        foreach (var contentId in relationContentIds.Except(attributedContentIds))
        {
            var content = _contentService.GetById(contentId);
            if (content is null)
            {
                continue;
            }

            results.Add(new MediaUsageReference
            {
                ContentId = content.Id,
                ContentKey = content.Key,
                ContentName = content.Name ?? $"(content {content.Id})",
                ContentTypeAlias = content.ContentType.Alias,
                Culture = null,
                PropertyAlias = null,
                PublishState = content.Published ? ContentPublishState.Published : ContentPublishState.Draft,
                DetectionSource = MediaUsageDetectionSource.Relation,
                EditUrl = BackofficeLinks.ContentEditUrl(content.Key),
            });
        }

        return results;
    }

    private HashSet<int> GetRelatedContentIds(int mediaId) =>
        _relationService.GetByChildId(mediaId)
            .Where(r => r.RelationType.Alias == CoreConstants.Conventions.RelationTypes.RelatedMediaAlias)
            .Select(r => r.ParentId)
            .ToHashSet();

    /// <summary>
    /// A content property can reference a media *folder* itself (e.g. a gallery/slideshow block
    /// that picks a folder and renders whatever's inside it) rather than each child file
    /// individually - Umbraco then records the relation on the folder node, never on its children.
    /// Without this, every file in such a folder would be misclassified "Unused" (and eligible for
    /// delete) despite being genuinely rendered on the site.
    ///
    /// IMPORTANT: this same ancestor-folder check must be applied everywhere else "is this media
    /// used" gets decided - it's applied here (bulk classification) and in GetUsagesAsync (usage
    /// detail), but when User Story 4's delete/purge pre-action re-check is built, it MUST also call
    /// this (or reuse it), or a folder-referenced file could still be deleted despite showing "Used"
    /// everywhere else in the UI.
    /// </summary>
    private HashSet<int> GetAncestorFolderRelatedContentIds(IMedia media)
    {
        var result = new HashSet<int>();
        foreach (var ancestorId in GetAncestorIds(media))
        {
            result.UnionWith(GetRelatedContentIds(ancestorId));
        }

        return result;
    }

    private static MediaUsageReference WithDetectionSource(MediaUsageReference usage, MediaUsageDetectionSource source) => new()
    {
        ContentId = usage.ContentId,
        ContentKey = usage.ContentKey,
        ContentName = usage.ContentName,
        ContentTypeAlias = usage.ContentTypeAlias,
        Culture = usage.Culture,
        PropertyAlias = usage.PropertyAlias,
        PublishState = usage.PublishState,
        DetectionSource = source,
        EditUrl = usage.EditUrl,
    };

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
                    // Folders are organizational containers, not audited files - per spec.md's edge
                    // case ("is the folder itself flagged, or only leaf files?"), only leaf files are
                    // classified. Without this, every folder shows up as permanently "Unused" clutter,
                    // since content references individual media items, never the containing folder.
                    .Where(m => m.ContentType.Alias != CoreConstants.Conventions.MediaTypes.Folder)
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
    /// one "relatedMedia" relation pointing at it, OR (see <see cref="GetAncestorFolderRelatedContentIds"/>)
    /// pointing at a folder it lives in. This is the fast primary signal used for a full audit run;
    /// the scan-based safety net (IMediaReferenceScanner) supplements this per-item on demand (usage
    /// detail, pre-delete/pre-purge re-check) rather than on every item in bulk, to hit the SC-002
    /// performance target.
    /// </summary>
    private MediaAuditItem ClassifyMedia(IMedia media)
    {
        var relatedContentIds = GetRelatedContentIds(media.Id);
        if (relatedContentIds.Count == 0)
        {
            relatedContentIds = GetAncestorFolderRelatedContentIds(media);
        }

        var isUsed = relatedContentIds.Count > 0;

        return new MediaAuditItem
        {
            Id = media.Id,
            Key = media.Key,
            Name = media.Name ?? $"(media {media.Id})",
            MediaTypeAlias = media.ContentType.Alias,
            MediaTypeName = media.ContentType.Name ?? media.ContentType.Alias,
            Extension = media.GetValue<string>(CoreConstants.Conventions.Media.Extension),
            SizeBytes = GetSizeBytes(media),
            Path = ResolveFolderPath(media),
            FolderId = media.ParentId == CoreConstants.System.Root ? null : media.ParentId,
            CreateDate = media.CreateDate,
            UpdateDate = media.UpdateDate,
            UsageStatus = isUsed ? MediaUsageStatus.Used : MediaUsageStatus.Unused,
            UsageCount = relatedContentIds.Count,
            DetectionSource = isUsed ? MediaDetectionSource.Relation : MediaDetectionSource.None,
            MediaEditUrl = BackofficeLinks.MediaEditUrl(media.Key),
        };
    }

    /// <summary>umbracoBytes (research.md §6) - null for container/folder media types that don't have it.</summary>
    private static long? GetSizeBytes(IMedia media)
    {
        var value = media.GetValue<int?>(CoreConstants.Conventions.Media.Bytes);
        return value.HasValue ? value.Value : null;
    }

    /// <summary>Resolves IMedia.Path's comma-separated id list (e.g. "-1,1234,5678") into just the ancestor folder ids, nearest-parent-last excluded, root excluded.</summary>
    private static List<int> GetAncestorIds(IMedia media) =>
        media.Path
            .Split(',', StringSplitOptions.RemoveEmptyEntries)
            .Select(int.Parse)
            .Where(id => id != CoreConstants.System.Root && id != media.Id)
            .ToList();

    /// <summary>
    /// Resolves IMedia.Path's comma-separated id list into a human-readable folder path (FR-006,
    /// FR-007) - resolves the G2 gap flagged in /speckit-analyze.
    /// </summary>
    private string ResolveFolderPath(IMedia media)
    {
        var ancestorIds = GetAncestorIds(media);
        if (ancestorIds.Count == 0)
        {
            return "/";
        }

        var names = ancestorIds.Select(id => _mediaService.GetById(id)?.Name ?? $"#{id}");
        return "/" + string.Join("/", names);
    }

    /// <summary>Like <see cref="ResolveFolderPath"/>, but includes the item itself - used for a folder's own breadcrumb path (GetFolders), not an item's containing-folder path.</summary>
    private string ResolveFullPath(IMedia media)
    {
        var parentPath = ResolveFolderPath(media);
        return parentPath == "/" ? $"/{media.Name}" : $"{parentPath}/{media.Name}";
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
