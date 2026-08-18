using CoreConstants = Umbraco.Cms.Core.Constants;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IMediaDeleteService" />
public sealed class MediaDeleteService : IMediaDeleteService
{
    private readonly IMediaService _mediaService;
    private readonly IMediaAuditService _auditService;
    private readonly IDeletionLogService _deletionLogService;

    public MediaDeleteService(IMediaService mediaService, IMediaAuditService auditService, IDeletionLogService deletionLogService)
    {
        _mediaService = mediaService;
        _auditService = auditService;
        _deletionLogService = deletionLogService;
    }

    public async Task<MediaDeleteResult> DeleteAsync(IReadOnlyList<Guid> mediaKeys, int performingUserId, CancellationToken cancellationToken = default)
    {
        var deleted = new List<Guid>();
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

            var usages = await _auditService.GetUsagesAsync(key, cancellationToken);
            if (usages is null)
            {
                skipped.Add(new MediaActionSkip { MediaKey = key, Reason = "NotFound" });
                continue;
            }

            if (usages.Count > 0)
            {
                skipped.Add(new MediaActionSkip { MediaKey = key, Reason = "NowReferenced" });
                continue;
            }

            var sizeBytes = media.GetValue<int?>(CoreConstants.Conventions.Media.Bytes) ?? 0;
            deleted.Add(key);
            loggedItems.Add(new DeletionLogItem { Key = media.Key, Name = media.Name ?? key.ToString() });
            totalSizeBytes += sizeBytes;

            _mediaService.MoveToRecycleBin(media);
        }

        var logEntryId = _deletionLogService.LogAction(
            DeletionLogActionType.Delete,
            performingUserId,
            loggedItems,
            totalSizeBytes,
            skipped.Count);

        return new MediaDeleteResult { Deleted = deleted, Skipped = skipped, LogEntryId = logEntryId };
    }
}
