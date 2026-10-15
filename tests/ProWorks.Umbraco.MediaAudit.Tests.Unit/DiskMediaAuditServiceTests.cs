using Moq;
using Umbraco.Cms.Core.IO;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;
using ProWorks.Umbraco.MediaAudit.Tests.Unit.TestSupport;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class DiskMediaAuditServiceTests
{
    private static void SetUpPagedDescendants(Mock<IMediaService> mock, IEnumerable<IMedia> items)
    {
        var list = items.ToList();
        long total = list.Count;
        mock.Setup(s => s.GetPagedDescendants(
                It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total,
                It.IsAny<IQuery<IMedia>>(), It.IsAny<Ordering>()))
            .Returns(list);
    }

    /// <summary>Mirrors MediaAuditServiceTests.RunAndWaitAsync - RunAuditAsync's audit itself runs fire-and-forget on a background Task.Run, so awaiting the call alone doesn't wait for it to finish.</summary>
    private static async Task<DiskAuditRun> RunAndWaitAsync(IDiskMediaAuditService service)
    {
        await service.RunAuditAsync();
        var deadline = DateTime.UtcNow.AddSeconds(5);
        DiskAuditRun run;
        do
        {
            run = service.GetCurrentAudit();
            if (run.Status != DiskAuditRunStatus.Running) return run;
            await Task.Delay(10);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException("Disk audit did not complete in time.");
    }

    private static (DiskMediaAuditService Service, Mock<IDiskMediaScanner> Scanner)
        CreateService(IEnumerable<IMedia> mediaItems, IEnumerable<MediaFileEntry> diskFiles)
    {
        var scanner = new Mock<IDiskMediaScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>())).ReturnsAsync(diskFiles.ToList());

        var mediaService = new Mock<IMediaService>();
        SetUpPagedDescendants(mediaService, mediaItems);

        var fileSystem = new Mock<IFileSystem>();
        fileSystem.Setup(f => f.GetRelativePath(It.IsAny<string>())).Returns((string p) => p);

        var provider = new Mock<IMediaFileSystemProvider>();
        provider.Setup(p => p.FileSystem).Returns(fileSystem.Object);

        var service = new DiskMediaAuditService(scanner.Object, mediaService.Object, provider.Object);
        return (service, scanner);
    }

    private static MediaFileEntry DiskFile(string relativePath, long sizeBytes = 100) => new()
    {
        RelativePath = relativePath,
        Url = $"/media/{relativePath}",
        SizeBytes = sizeBytes,
        LastModifiedUtc = DateTime.UnixEpoch,
    };

    [Fact]
    public async Task RunAuditAsync_lists_a_file_with_no_matching_media_item()
    {
        var (service, _) = CreateService(Array.Empty<IMedia>(), new[] { DiskFile("1234/orphan.jpg") });

        var run = await RunAndWaitAsync(service);

        Assert.Equal(DiskAuditRunStatus.Complete, run.Status);
        var item = Assert.Single(service.GetItems().Items);
        Assert.Equal("1234/orphan.jpg", item.RelativePath);
    }

    [Fact]
    public async Task RunAuditAsync_excludes_a_file_owned_by_an_active_media_item()
    {
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("owned.jpg", mediaType, id: 1);
        media.SetValue("umbracoFile", "1234/owned.jpg");

        var (service, _) = CreateService(new[] { media }, new[] { DiskFile("1234/owned.jpg") });

        await RunAndWaitAsync(service);

        Assert.Empty(service.GetItems().Items);
    }

    [Fact]
    public async Task RunAuditAsync_excludes_a_file_owned_by_a_trashed_media_item()
    {
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("trashed.jpg", mediaType, id: 1);
        media.SetValue("umbracoFile", "1234/trashed.jpg");
        ((Media)media).Trashed = true;

        var (service, _) = CreateService(new[] { media }, new[] { DiskFile("1234/trashed.jpg") });

        await RunAndWaitAsync(service);

        Assert.Empty(service.GetItems().Items);
    }

    [Fact]
    public async Task RunAuditAsync_records_total_and_disk_only_counts()
    {
        var mediaType = ModelFactory.CreateMediaType();
        var owned = ModelFactory.CreateMedia("owned.jpg", mediaType, id: 1);
        owned.SetValue("umbracoFile", "owned.jpg");

        var (service, _) = CreateService(new[] { owned }, new[] { DiskFile("owned.jpg"), DiskFile("orphan.jpg", 250) });

        var run = await RunAndWaitAsync(service);

        Assert.Equal(2, run.TotalFilesOnDisk);
        Assert.Equal(1, run.DiskOnlyCount);
        Assert.Equal(250, run.DiskOnlySizeBytes);
    }

    [Fact]
    public async Task RunAuditAsync_reports_a_failed_scan_with_an_error_message()
    {
        var scanner = new Mock<IDiskMediaScanner>();
        scanner.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>())).ThrowsAsync(new InvalidOperationException("media folder unreachable"));

        var mediaService = new Mock<IMediaService>();
        SetUpPagedDescendants(mediaService, Array.Empty<IMedia>());

        var fileSystem = new Mock<IFileSystem>();
        var provider = new Mock<IMediaFileSystemProvider>();
        provider.Setup(p => p.FileSystem).Returns(fileSystem.Object);

        var service = new DiskMediaAuditService(scanner.Object, mediaService.Object, provider.Object);

        var run = await RunAndWaitAsync(service);

        Assert.Equal(DiskAuditRunStatus.Failed, run.Status);
        Assert.Equal("media folder unreachable", run.ErrorMessage);
    }

    [Fact]
    public async Task IsDiskOnly_reflects_the_last_completed_scan()
    {
        var mediaType = ModelFactory.CreateMediaType();
        var owned = ModelFactory.CreateMedia("owned.jpg", mediaType, id: 1);
        owned.SetValue("umbracoFile", "owned.jpg");

        var (service, _) = CreateService(new[] { owned }, Array.Empty<MediaFileEntry>());

        Assert.False(service.IsDiskOnly("owned.jpg"));
        Assert.True(service.IsDiskOnly("not-owned.jpg"));
    }

    [Fact]
    public async Task RunAuditAsync_completes_even_when_the_triggering_request_token_is_cancelled()
    {
        var (service, scanner) = CreateService(Array.Empty<IMedia>(), Array.Empty<MediaFileEntry>());
        scanner.Setup(s => s.ScanAsync(It.IsAny<CancellationToken>()))
            .Returns(async (CancellationToken token) =>
            {
                await Task.Delay(50);
                token.ThrowIfCancellationRequested();
                return (IReadOnlyList<MediaFileEntry>)new List<MediaFileEntry>();
            });

        using var requestAborted = new CancellationTokenSource();
        await service.RunAuditAsync(requestAborted.Token);
        requestAborted.Cancel();

        var deadline = DateTime.UtcNow.AddSeconds(5);
        while (service.GetCurrentAudit().Status == DiskAuditRunStatus.Running && DateTime.UtcNow < deadline)
        {
            await Task.Delay(10);
        }

        Assert.Equal(DiskAuditRunStatus.Complete, service.GetCurrentAudit().Status);
    }
}
