using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IMediaPurgeService" />
public sealed class MediaPurgeService : IMediaPurgeService
{
    private readonly IMediaService _mediaService;
    private readonly IDeletionLogService _deletionLogService;

    public MediaPurgeService(IMediaService mediaService, IDeletionLogService deletionLogService)
    {
        _mediaService = mediaService;
        _deletionLogService = deletionLogService;
    }

    public MediaPurgeResult Purge(IReadOnlyList<Guid> mediaKeys, int performingUserId)
    {
        var purged = new List<Guid>();
        var loggedItems = new List<DeletionLogItem>();
        var skipped = new List<MediaActionSkip>();
        long totalSizeBytes = 0;

        foreach (var key in mediaKeys)
        {
            var media = _mediaService.GetById(key);
            if (media is null)
            {
                skipped.Add(new MediaActionSkip { MediaKey = key, Reason = "NotFound" });
                continue;
            }

            // Fresh Trashed-state re-check immediately before purging (research.md §5; spec.md edge
            // case: an item restored out of the Recycle Bin by someone else since being soft-deleted
            // must be skipped, not purged or errored as a whole batch).
            if (!media.Trashed)
            {
                skipped.Add(new MediaActionSkip { MediaKey = key, Reason = "NotTrashed" });
                continue;
            }

            var sizeBytes = media.GetValue<int?>(CoreConstants.Conventions.Media.Bytes) ?? 0;
            purged.Add(key);
            loggedItems.Add(new DeletionLogItem { Key = media.Key, Name = media.Name ?? key.ToString() });
            totalSizeBytes += sizeBytes;

            // Per-item Delete(), never EmptyRecycleBin() - scoped to exactly what was requested.
            _mediaService.Delete(media);
        }

        var logEntryId = _deletionLogService.LogAction(
            DeletionLogActionType.Purge,
            performingUserId,
            loggedItems,
            totalSizeBytes,
            skipped.Count);

        return new MediaPurgeResult { Purged = purged, Skipped = skipped, LogEntryId = logEntryId };
    }
}
