using CoreConstants = Umbraco.Cms.Core.Constants;
using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;
using ProWorks.Umbraco.MediaAudit.Tests.Unit.TestSupport;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class MediaAuditServiceTests
{
    private static void SetUpPagedDescendants<T>(Mock<T> mock, IEnumerable<IMedia> items) where T : class, IMediaService
    {
        var list = items.ToList();
        long total = list.Count;
        mock.Setup(s => s.GetPagedDescendants(
                It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total,
                It.IsAny<IQuery<IMedia>>(), It.IsAny<Ordering>()))
            .Returns(list);
    }

    private static void SetUpPagedDescendants(Mock<IContentService> mock, IEnumerable<IContent> items)
    {
        var list = items.ToList();
        long total = list.Count;
        mock.Setup(s => s.GetPagedDescendants(
                It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total,
                It.IsAny<IQuery<IContent>>(), It.IsAny<Ordering>()))
            .Returns(list);
    }

    /// <summary>A relation of the type the audit's relation-based signal actually looks for (research.md §4).</summary>
    private static IRelation RelatedMediaRelation(int contentId, int mediaId)
    {
        var relationType = new RelationType(CoreConstants.Conventions.RelationTypes.RelatedMediaAlias, "Related Media");
        return new Relation(contentId, mediaId, relationType);
    }

    private static async Task<AuditRun> RunAndWaitAsync(IMediaAuditService service)
    {
        await service.RunAuditAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        AuditRun run;
        do
        {
            run = service.GetCurrentAudit();
            if (run.Status != AuditRunStatus.Running) return run;
            await Task.Delay(10);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException("Audit did not complete in time.");
    }

    private static (MediaAuditService Service, Mock<IMediaService> MediaService, Mock<IRelationService> RelationService, Mock<IContentService> ContentService, Mock<IMediaReferenceScanner> Scanner)
        CreateService()
    {
        var mediaService = new Mock<IMediaService>();
        var relationService = new Mock<IRelationService>();
        var contentService = new Mock<IContentService>();
        var scanner = new Mock<IMediaReferenceScanner>();
        scanner.Setup(s => s.FindReferencesAsync(It.IsAny<IMedia>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MediaUsageReference>());

        var service = new MediaAuditService(mediaService.Object, relationService.Object, contentService.Object, scanner.Object);
        return (service, mediaService, relationService, contentService, scanner);
    }

    [Fact]
    public async Task Media_with_a_relation_is_classified_Used()
    {
        var (service, mediaService, relationService, _, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("hero.jpg", mediaType, id: 1);

        SetUpPagedDescendants(mediaService, new[] { media });
        relationService.Setup(r => r.GetByChildId(1)).Returns(new[] { RelatedMediaRelation(contentId: 100, mediaId: 1) });

        await RunAndWaitAsync(service);
        var item = Assert.Single(service.GetItems(new MediaAuditItemsQuery()).Items);

        Assert.Equal(MediaUsageStatus.Used, item.UsageStatus);
        Assert.Equal(MediaDetectionSource.Relation, item.DetectionSource);
        Assert.Equal(1, item.UsageCount);
    }

    [Fact]
    public async Task Media_with_no_relation_is_classified_Unused()
    {
        var (service, mediaService, relationService, _, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("orphan.jpg", mediaType, id: 2);

        SetUpPagedDescendants(mediaService, new[] { media });
        relationService.Setup(r => r.GetByChildId(2)).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);
        var item = Assert.Single(service.GetItems(new MediaAuditItemsQuery()).Items);

        Assert.Equal(MediaUsageStatus.Unused, item.UsageStatus);
        Assert.Equal(MediaDetectionSource.None, item.DetectionSource);
    }

    [Fact]
    public async Task Folder_type_media_is_excluded_from_results()
    {
        // spec.md edge case: a folder is an organizational container, not an audited item -
        // resolved by excluding it entirely rather than always showing it as "Unused" clutter.
        var (service, mediaService, relationService, _, _) = CreateService();
        var folderType = ModelFactory.CreateFolderMediaType();
        var folder = ModelFactory.CreateMedia("Campaign Assets", folderType, id: 3);

        SetUpPagedDescendants(mediaService, new[] { folder });
        relationService.Setup(r => r.GetByChildId(It.IsAny<int>())).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);

        Assert.Empty(service.GetItems(new MediaAuditItemsQuery()).Items);
    }

    [Fact]
    public async Task Trashed_media_is_excluded_from_results()
    {
        // GetPagedDescendants(Root, ...) turns out to include already-trashed items (found during
        // manual US4 testing) - must be excluded explicitly, same as folders.
        var (service, mediaService, relationService, _, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("deleted.jpg", mediaType, id: 4);
        ((Media)media).Trashed = true;

        SetUpPagedDescendants(mediaService, new[] { media });
        relationService.Setup(r => r.GetByChildId(It.IsAny<int>())).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);

        Assert.Empty(service.GetItems(new MediaAuditItemsQuery()).Items);
    }

    [Fact]
    public async Task Media_is_Used_when_only_an_ancestor_folder_is_referenced()
    {
        // research.md §4 addendum: a gallery/slideshow block can pick the *folder* itself - Umbraco
        // then records the relation on the folder node, never the child file.
        var (service, mediaService, relationService, _, _) = CreateService();
        var folderType = ModelFactory.CreateFolderMediaType();
        var folder = ModelFactory.CreateMedia("Gallery", folderType, id: 10);
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("slide1.jpg", mediaType, id: 11, parentId: 10);
        media.Path = "-1,10,11";

        SetUpPagedDescendants(mediaService, new[] { media });
        mediaService.Setup(m => m.GetById(10)).Returns(folder);
        relationService.Setup(r => r.GetByChildId(11)).Returns(Array.Empty<IRelation>());
        relationService.Setup(r => r.GetByChildId(10)).Returns(new[] { RelatedMediaRelation(contentId: 200, mediaId: 10) });

        await RunAndWaitAsync(service);
        var item = Assert.Single(service.GetItems(new MediaAuditItemsQuery()).Items);

        Assert.Equal(MediaUsageStatus.Used, item.UsageStatus);
    }

    [Fact]
    public async Task GetItems_filters_by_status_type_and_folder()
    {
        var (service, mediaService, relationService, _, _) = CreateService();
        var imageType = ModelFactory.CreateMediaType("image", "Image");
        var fileType = ModelFactory.CreateMediaType("file", "File");

        var used = ModelFactory.CreateMedia("used.jpg", imageType, id: 20, parentId: 5);
        var unusedImage = ModelFactory.CreateMedia("unused.jpg", imageType, id: 21, parentId: 5);
        var unusedFile = ModelFactory.CreateMedia("unused.pdf", fileType, id: 22, parentId: 6);

        SetUpPagedDescendants(mediaService, new[] { used, unusedImage, unusedFile });
        relationService.Setup(r => r.GetByChildId(20)).Returns(new[] { RelatedMediaRelation(300, 20) });
        relationService.Setup(r => r.GetByChildId(It.Is<int>(id => id != 20))).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);

        var unusedOnly = service.GetItems(new MediaAuditItemsQuery { Status = MediaUsageStatus.Unused }).Items;
        Assert.Equal(2, unusedOnly.Count);

        var unusedImagesInFolder5 = service.GetItems(new MediaAuditItemsQuery
        {
            Status = MediaUsageStatus.Unused,
            MediaTypeAlias = "image",
            FolderId = 5,
        }).Items;
        var only = Assert.Single(unusedImagesInFolder5);
        Assert.Equal(21, only.Id);
    }

    [Fact]
    public async Task GetItems_sorts_by_size_descending()
    {
        var (service, mediaService, relationService, _, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var small = ModelFactory.CreateMedia("small.jpg", mediaType, id: 30, sizeBytes: 100);
        var large = ModelFactory.CreateMedia("large.jpg", mediaType, id: 31, sizeBytes: 900);

        SetUpPagedDescendants(mediaService, new[] { small, large });
        relationService.Setup(r => r.GetByChildId(It.IsAny<int>())).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);
        var items = service.GetItems(new MediaAuditItemsQuery { Sort = MediaAuditSortField.SizeBytes, SortDirection = MediaAuditSortDirection.Desc }).Items;

        Assert.Equal(new[] { 31, 30 }, items.Select(i => i.Id));
    }

    [Fact]
    public async Task GetItems_paginates()
    {
        var (service, mediaService, relationService, _, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var items = Enumerable.Range(1, 5).Select(i => ModelFactory.CreateMedia($"item{i}.jpg", mediaType, id: 100 + i)).ToArray();

        SetUpPagedDescendants(mediaService, items);
        relationService.Setup(r => r.GetByChildId(It.IsAny<int>())).Returns(Array.Empty<IRelation>());

        await RunAndWaitAsync(service);
        var result = service.GetItems(new MediaAuditItemsQuery { Page = 2, PageSize = 2 });

        Assert.Equal(5, result.TotalItems);
        Assert.Equal(2, result.Items.Count);
    }

    [Fact]
    public async Task GetUsagesAsync_returns_null_for_an_unknown_media_key()
    {
        var (service, mediaService, _, _, _) = CreateService();
        mediaService.Setup(m => m.GetById(It.IsAny<Guid>())).Returns((IMedia?)null);

        var result = await service.GetUsagesAsync(Guid.NewGuid());

        Assert.Null(result);
    }

    [Fact]
    public async Task GetUsagesAsync_upgrades_a_scan_hit_to_Relation_when_the_relation_table_agrees()
    {
        var (service, mediaService, relationService, _, scanner) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("hero.jpg", mediaType, id: 40);
        var contentType = ModelFactory.CreateContentType();
        var content = ModelFactory.CreateContent("Homepage", contentType, id: 400);

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);
        relationService.Setup(r => r.GetByChildId(40)).Returns(new[] { RelatedMediaRelation(400, 40) });
        scanner.Setup(s => s.FindReferencesAsync(media, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[]
            {
                new MediaUsageReference
                {
                    ContentId = 400,
                    ContentKey = content.Key,
                    ContentName = "Homepage",
                    ContentTypeAlias = "page",
                    Culture = null,
                    PropertyAlias = "bodyText",
                    PublishState = ContentPublishState.Published,
                    DetectionSource = MediaUsageDetectionSource.Scan,
                    EditUrl = "/edit/400",
                },
            });

        var usages = await service.GetUsagesAsync(media.Key);

        var usage = Assert.Single(usages!);
        Assert.Equal(MediaUsageDetectionSource.Relation, usage.DetectionSource);
    }

    [Fact]
    public async Task GetUsagesAsync_adds_an_unattributed_placeholder_for_a_relation_the_scan_missed()
    {
        var (service, mediaService, relationService, contentService, scanner) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("hero.jpg", mediaType, id: 41);
        var contentType = ModelFactory.CreateContentType();
        var content = ModelFactory.CreateContent("Homepage", contentType, id: 401);

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);
        relationService.Setup(r => r.GetByChildId(41)).Returns(new[] { RelatedMediaRelation(401, 41) });
        contentService.Setup(c => c.GetById(401)).Returns(content);
        scanner.Setup(s => s.FindReferencesAsync(media, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MediaUsageReference>());

        var usages = await service.GetUsagesAsync(media.Key);

        var usage = Assert.Single(usages!);
        Assert.Equal(MediaUsageDetectionSource.Relation, usage.DetectionSource);
        Assert.Null(usage.Culture);
        Assert.Null(usage.PropertyAlias);
    }

    [Fact]
    public async Task GetUsagesAsync_returns_empty_for_a_stale_relation_pointing_at_deleted_content()
    {
        // data-model.md validation rule: a "Used" item that resolves zero usages is the
        // stale-relation data-integrity condition, not an error - the client surfaces it distinctly.
        var (service, mediaService, relationService, contentService, scanner) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("hero.jpg", mediaType, id: 42);

        mediaService.Setup(m => m.GetById(media.Key)).Returns(media);
        relationService.Setup(r => r.GetByChildId(42)).Returns(new[] { RelatedMediaRelation(402, 42) });
        contentService.Setup(c => c.GetById(402)).Returns((IContent?)null); // content since deleted
        scanner.Setup(s => s.FindReferencesAsync(media, It.IsAny<CancellationToken>()))
            .ReturnsAsync(Array.Empty<MediaUsageReference>());

        var usages = await service.GetUsagesAsync(media.Key);

        Assert.NotNull(usages);
        Assert.Empty(usages!);
    }

    [Fact]
    public async Task GetFolders_returns_only_non_trashed_folders()
    {
        var (service, mediaService, _, _, _) = CreateService();
        var folderType = ModelFactory.CreateFolderMediaType();
        var liveFolder = ModelFactory.CreateMedia("2023", folderType, id: 50);
        var trashedFolder = ModelFactory.CreateMedia("Old", folderType, id: 51);
        ((Media)trashedFolder).Trashed = true;
        var imageType = ModelFactory.CreateMediaType();
        var notAFolder = ModelFactory.CreateMedia("photo.jpg", imageType, id: 52);

        SetUpPagedDescendants(mediaService, new[] { liveFolder, trashedFolder, notAFolder });

        var folders = service.GetFolders();

        var only = Assert.Single(folders);
        Assert.Equal(50, only.Id);
    }

    [Fact]
    public async Task GetUsedOnPageNames_returns_names_of_referencing_content()
    {
        var (service, mediaService, relationService, contentService, _) = CreateService();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("hero.jpg", mediaType, id: 60);
        var contentType = ModelFactory.CreateContentType();
        var content = ModelFactory.CreateContent("Homepage", contentType, id: 600);

        mediaService.Setup(m => m.GetById(60)).Returns(media);
        relationService.Setup(r => r.GetByChildId(60)).Returns(new[] { RelatedMediaRelation(600, 60) });
        contentService.Setup(c => c.GetById(600)).Returns(content);

        var names = service.GetUsedOnPageNames(60);

        Assert.Equal(new[] { "Homepage" }, names);
    }
}
