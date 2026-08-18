using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using UmbracoMediaAudit.Models;
using UmbracoMediaAudit.Services;
using UmbracoMediaAudit.Tests.Unit.TestSupport;

namespace UmbracoMediaAudit.Tests.Unit;

public class MediaPurgeServiceTests
{
    private static (MediaPurgeService Service, Mock<IMediaService> MediaService, Mock<IDeletionLogService> DeletionLogService) CreateService()
    {
        var mediaService = new Mock<IMediaService>();
        var deletionLogService = new Mock<IDeletionLogService>();
        deletionLogService
            .Setup(d => d.LogAction(It.IsAny<DeletionLogActionType>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<DeletionLogItem>>(), It.IsAny<long>(), It.IsAny<int>()))
            .Returns(43);

        var service = new MediaPurgeService(mediaService.Object, deletionLogService.Object);
        return (service, mediaService, deletionLogService);
    }

    [Fact]
    public void Purge_permanently_deletes_a_trashed_item()
    {
        var (service, mediaService, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("deleted.jpg", mediaType, id: 1, sizeBytes: 200);
        ((Media)media).Trashed = true;

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);

        var result = service.Purge(new[] { media.Key }, performingUserId: 7);

        Assert.Equal(new[] { media.Key }, result.Purged);
        Assert.Empty(result.Skipped);
        // Scoped per-item Delete(), never the untargeted EmptyRecycleBin() (research.md §5).
        mediaService.Verify(m => m.Delete(media, It.IsAny<int>()), Times.Once);
        mediaService.Verify(m => m.EmptyRecycleBin(It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Purge_skips_an_item_already_restored_out_of_the_recycle_bin()
    {
        // spec.md edge case: an item restored by someone else since being soft-deleted must be
        // skipped, not purged or errored as a whole batch.
        var (service, mediaService, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("restored.jpg", mediaType, id: 2);
        // Trashed defaults to false - i.e. already restored.

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);

        var result = service.Purge(new[] { media.Key }, performingUserId: 7);

        Assert.Empty(result.Purged);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NotTrashed", skip.Reason);
        mediaService.Verify(m => m.Delete(It.IsAny<IMedia>(), It.IsAny<int>()), Times.Never);
    }

    [Fact]
    public void Purge_skips_a_key_that_no_longer_resolves_to_any_media()
    {
        var (service, mediaService, _) = CreateService();
        var missingKey = Guid.NewGuid();
        mediaService.Setup(m => m.GetById(missingKey)).Returns((IMedia?)null);

        var result = service.Purge(new[] { missingKey }, performingUserId: 7);

        Assert.Empty(result.Purged);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NotFound", skip.Reason);
    }

    [Fact]
    public void Purge_always_writes_exactly_one_log_entry_even_when_everything_is_skipped()
    {
        var (service, mediaService, deletionLogService) = CreateService();
        var missingKey = Guid.NewGuid();
        mediaService.Setup(m => m.GetById(missingKey)).Returns((IMedia?)null);

        var result = service.Purge(new[] { missingKey }, performingUserId: 7);

        Assert.Equal(43, result.LogEntryId);
        deletionLogService.Verify(
            d => d.LogAction(DeletionLogActionType.Purge, 7, It.Is<IReadOnlyList<DeletionLogItem>>(items => items.Count == 0), 0, 1),
            Times.Once);
    }
}
