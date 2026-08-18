using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using UmbracoMediaAudit.Models;
using UmbracoMediaAudit.Services;
using UmbracoMediaAudit.Tests.Unit.TestSupport;

namespace UmbracoMediaAudit.Tests.Unit;

public class MediaDeleteServiceTests
{
    private static (MediaDeleteService Service, Mock<IMediaService> MediaService, Mock<IMediaAuditService> AuditService, Mock<IDeletionLogService> DeletionLogService)
        CreateService()
    {
        var mediaService = new Mock<IMediaService>();
        var auditService = new Mock<IMediaAuditService>();
        var deletionLogService = new Mock<IDeletionLogService>();
        deletionLogService
            .Setup(d => d.LogAction(It.IsAny<DeletionLogActionType>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<DeletionLogItem>>(), It.IsAny<long>(), It.IsAny<int>()))
            .Returns(42);

        var service = new MediaDeleteService(mediaService.Object, auditService.Object, deletionLogService.Object);
        return (service, mediaService, auditService, deletionLogService);
    }

    [Fact]
    public async Task DeleteAsync_moves_a_still_unused_item_to_the_recycle_bin()
    {
        var (service, mediaService, auditService, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("orphan.jpg", mediaType, id: 1, sizeBytes: 500);

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);
        auditService.Setup(a => a.GetUsagesAsync(media.Key, It.IsAny<CancellationToken>())).ReturnsAsync(Array.Empty<MediaUsageReference>());

        var result = await service.DeleteAsync(new[] { media.Key }, performingUserId: 7);

        Assert.Equal(new[] { media.Key }, result.Deleted);
        Assert.Empty(result.Skipped);
        mediaService.Verify(m => m.MoveToRecycleBin(media), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_skips_an_item_that_has_become_referenced_since_the_last_audit()
    {
        // spec.md edge case: protect against deleting items that turn out to be in use. This is
        // also where the ancestor-folder fix (research.md §4) is enforced, since DeleteAsync calls
        // the same GetUsagesAsync the usage-detail view and classification both rely on.
        var (service, mediaService, auditService, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("now-used.jpg", mediaType, id: 2);
        var contentType = ModelFactory.CreateContentType();
        var referencingContent = ModelFactory.CreateContent("Homepage", contentType, id: 500);

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);
        auditService.Setup(a => a.GetUsagesAsync(media.Key, It.IsAny<CancellationToken>())).ReturnsAsync(new[]
        {
            new MediaUsageReference
            {
                ContentId = referencingContent.Id,
                ContentKey = referencingContent.Key,
                ContentName = "Homepage",
                ContentTypeAlias = "page",
                Culture = null,
                PropertyAlias = "bodyText",
                PublishState = ContentPublishState.Published,
                DetectionSource = MediaUsageDetectionSource.Relation,
                EditUrl = "/edit/500",
            },
        });

        var result = await service.DeleteAsync(new[] { media.Key }, performingUserId: 7);

        Assert.Empty(result.Deleted);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NowReferenced", skip.Reason);
        mediaService.Verify(m => m.MoveToRecycleBin(It.IsAny<IMedia>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_skips_a_key_that_no_longer_resolves_to_any_media()
    {
        var (service, mediaService, _, _) = CreateService();
        var missingKey = Guid.NewGuid();
        mediaService.Setup(m => m.GetById(missingKey)).Returns((IMedia?)null);

        var result = await service.DeleteAsync(new[] { missingKey }, performingUserId: 7);

        Assert.Empty(result.Deleted);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NotFound", skip.Reason);
    }

    [Fact]
    public async Task DeleteAsync_always_writes_exactly_one_log_entry_even_when_everything_is_skipped()
    {
        var (service, mediaService, _, deletionLogService) = CreateService();
        var missingKey = Guid.NewGuid();
        mediaService.Setup(m => m.GetById(missingKey)).Returns((IMedia?)null);

        var result = await service.DeleteAsync(new[] { missingKey }, performingUserId: 7);

        Assert.Equal(42, result.LogEntryId);
        deletionLogService.Verify(
            d => d.LogAction(DeletionLogActionType.Delete, 7, It.Is<IReadOnlyList<DeletionLogItem>>(items => items.Count == 0), 0, 1),
            Times.Once);
    }
}
