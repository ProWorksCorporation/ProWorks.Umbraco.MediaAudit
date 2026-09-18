using Moq;
using Umbraco.Cms.Core.IO;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class DiskMediaDeleteServiceTests
{
    private static (DiskMediaDeleteService Service, Mock<IFileSystem> FileSystem, Mock<IDiskMediaAuditService> AuditService, Mock<IDeletionLogService> DeletionLogService)
        CreateService()
    {
        var fileSystem = new Mock<IFileSystem>();
        var provider = new Mock<IMediaFileSystemProvider>();
        provider.Setup(p => p.FileSystem).Returns(fileSystem.Object);

        var auditService = new Mock<IDiskMediaAuditService>();
        var deletionLogService = new Mock<IDeletionLogService>();
        deletionLogService
            .Setup(d => d.LogAction(It.IsAny<DeletionLogActionType>(), It.IsAny<int>(), It.IsAny<IReadOnlyList<DeletionLogItem>>(), It.IsAny<long>(), It.IsAny<int>()))
            .Returns(99);

        var service = new DiskMediaDeleteService(provider.Object, auditService.Object, deletionLogService.Object);
        return (service, fileSystem, auditService, deletionLogService);
    }

    [Fact]
    public async Task DeleteAsync_deletes_a_file_that_is_still_present_and_still_disk_only()
    {
        var (service, fileSystem, auditService, _) = CreateService();
        fileSystem.Setup(f => f.FileExists("1234/orphan.jpg")).Returns(true);
        fileSystem.Setup(f => f.GetSize("1234/orphan.jpg")).Returns(500L);
        auditService.Setup(a => a.IsDiskOnly("1234/orphan.jpg")).Returns(true);

        var result = await service.DeleteAsync(new[] { "1234/orphan.jpg" }, performingUserId: 7);

        Assert.Equal(new[] { "1234/orphan.jpg" }, result.Deleted);
        Assert.Empty(result.Skipped);
        fileSystem.Verify(f => f.DeleteFile("1234/orphan.jpg"), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_skips_a_path_that_is_no_longer_on_disk()
    {
        var (service, fileSystem, _, _) = CreateService();
        fileSystem.Setup(f => f.FileExists("gone.jpg")).Returns(false);

        var result = await service.DeleteAsync(new[] { "gone.jpg" }, performingUserId: 7);

        Assert.Empty(result.Deleted);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NotFound", skip.Reason);
        fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_skips_a_path_that_has_since_been_claimed_by_a_new_media_item()
    {
        var (service, fileSystem, auditService, _) = CreateService();
        fileSystem.Setup(f => f.FileExists("now-owned.jpg")).Returns(true);
        auditService.Setup(a => a.IsDiskOnly("now-owned.jpg")).Returns(false);

        var result = await service.DeleteAsync(new[] { "now-owned.jpg" }, performingUserId: 7);

        Assert.Empty(result.Deleted);
        var skip = Assert.Single(result.Skipped);
        Assert.Equal("NowOwned", skip.Reason);
        fileSystem.Verify(f => f.DeleteFile(It.IsAny<string>()), Times.Never);
    }

    [Fact]
    public async Task DeleteAsync_always_writes_exactly_one_log_entry_even_when_everything_is_skipped()
    {
        var (service, fileSystem, _, deletionLogService) = CreateService();
        fileSystem.Setup(f => f.FileExists("gone.jpg")).Returns(false);

        var result = await service.DeleteAsync(new[] { "gone.jpg" }, performingUserId: 7);

        Assert.Equal(99, result.LogEntryId);
        deletionLogService.Verify(
            d => d.LogAction(DeletionLogActionType.DiskDelete, 7, It.Is<IReadOnlyList<DeletionLogItem>>(items => items.Count == 0), 0, 1),
            Times.Once);
    }
}
