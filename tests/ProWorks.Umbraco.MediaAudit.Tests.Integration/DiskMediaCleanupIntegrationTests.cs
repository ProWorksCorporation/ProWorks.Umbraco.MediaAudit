using System.Text;
using Umbraco.Cms.Core.IO;
using NUnit.Framework;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using ProWorks.Umbraco.MediaAudit.Services;
using ProWorks.Umbraco.MediaAudit.Tests.Integration.TestSupport;
using CoreConstants = Umbraco.Cms.Core.Constants;

namespace ProWorks.Umbraco.MediaAudit.Tests.Integration;

/// <summary>
/// Exercises the disk-only audit/delete pipeline end-to-end: a real file written directly into the
/// media file system (bypassing Umbraco, the way a leftover/migrated file would arrive) alongside a
/// real Umbraco media item, against the real MediaFileManager-backed IFileSystem rather than a mock.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class DiskMediaCleanupIntegrationTests : MediaAuditIntegrationTestBase
{
    private IMediaService MediaService => GetRequiredService<IMediaService>();
    private MediaFileManager MediaFileManager => GetRequiredService<MediaFileManager>();
    private IDiskMediaAuditService DiskAuditService => GetRequiredService<IDiskMediaAuditService>();
    private IDiskMediaDeleteService DiskDeleteService => GetRequiredService<IDiskMediaDeleteService>();
    private IDeletionLogService DeletionLogService => GetRequiredService<IDeletionLogService>();

    private static async Task<Models.DiskAuditRun> RunAndWaitAsync(IDiskMediaAuditService service)
    {
        await service.RunAuditAsync();
        var deadline = DateTime.UtcNow.AddSeconds(10);
        Models.DiskAuditRun run;
        do
        {
            run = service.GetCurrentAudit();
            if (run.Status != Models.DiskAuditRunStatus.Running) return run;
            await Task.Delay(20);
        } while (DateTime.UtcNow < deadline);

        throw new TimeoutException("Disk audit did not complete in time.");
    }

    private void WriteOrphanFile(string relativePath, string content = "orphan")
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        MediaFileManager.FileSystem.AddFile(relativePath, stream, overrideIfExists: true);
    }

    [Test]
    public async Task RunAuditAsync_finds_a_real_orphan_file_but_not_a_real_media_items_file()
    {
        var media = MediaService.CreateMediaWithIdentity("kept.jpg", -1, CoreConstants.Conventions.MediaTypes.Image);
        media.SetValue(CoreConstants.Conventions.Media.File, "kept-files/kept.jpg");
        MediaService.Save(media);
        WriteOrphanFile("kept-files/kept.jpg", "kept");
        WriteOrphanFile("stray/orphan.txt");

        var run = await RunAndWaitAsync(DiskAuditService);

        Assert.That(run.Status, Is.EqualTo(Models.DiskAuditRunStatus.Complete));
        var items = DiskAuditService.GetItems().Items;
        Assert.That(items.Select(i => i.RelativePath), Does.Contain("stray/orphan.txt"));
        Assert.That(items.Select(i => i.RelativePath), Does.Not.Contain("kept-files/kept.jpg"));
    }

    [Test]
    public async Task DeleteAsync_removes_the_file_from_disk_and_logs_a_DiskDelete_entry()
    {
        WriteOrphanFile("stray/to-delete.txt");
        await RunAndWaitAsync(DiskAuditService);

        var result = await DiskDeleteService.DeleteAsync(new[] { "stray/to-delete.txt" }, performingUserId: -1);

        Assert.That(result.Deleted, Is.EquivalentTo(new[] { "stray/to-delete.txt" }));
        Assert.That(result.Skipped, Is.Empty);
        Assert.That(MediaFileManager.FileSystem.FileExists("stray/to-delete.txt"), Is.False);

        var (entries, _) = DeletionLogService.GetPagedHistory(1, 10);
        var entry = entries.Single(e => e.Id == result.LogEntryId);
        Assert.That(entry.ActionType, Is.EqualTo(Models.DeletionLogActionType.DiskDelete));
        Assert.That(entry.ItemCount, Is.EqualTo(1));
    }
}
