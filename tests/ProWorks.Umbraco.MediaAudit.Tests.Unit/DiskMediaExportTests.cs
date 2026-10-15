using System.Text;
using Microsoft.AspNetCore.Mvc;
using Moq;
using Umbraco.Cms.Core.Security;
using ProWorks.Umbraco.MediaAudit.Controllers;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class DiskMediaExportTests
{
    private static string Export(params DiskMediaItem[] items)
    {
        var diskAuditService = new Mock<IDiskMediaAuditService>();
        diskAuditService.Setup(s => s.GetItems()).Returns(new DiskMediaItemsResult { Items = items });

        var controller = new UmbracoMediaAuditApiController(
            Mock.Of<IMediaAuditService>(),
            Mock.Of<IMediaDeleteService>(),
            Mock.Of<IMediaPurgeService>(),
            Mock.Of<IDeletionLogService>(),
            diskAuditService.Object,
            Mock.Of<IDiskMediaDeleteService>(),
            Mock.Of<IBackOfficeSecurityAccessor>());

        var file = Assert.IsType<FileContentResult>(controller.DiskExport());
        Assert.Equal("text/csv", file.ContentType);
        Assert.Equal("disk-media-export.csv", file.FileDownloadName);
        return Encoding.UTF8.GetString(file.FileContents);
    }

    [Fact]
    public void DiskExport_writes_one_row_per_disk_only_file_and_quotes_fields_containing_commas()
    {
        var csv = Export(new DiskMediaItem
        {
            RelativePath = "zz-test/orphan, copy.png",
            Url = "/media/zz-test/orphan, copy.png",
            SizeBytes = 2048,
            LastModifiedUtc = new DateTime(2026, 10, 14, 12, 0, 0, DateTimeKind.Utc),
        });

        var lines = csv.Split(Environment.NewLine, StringSplitOptions.RemoveEmptyEntries);
        Assert.Equal("RelativePath,Url,SizeBytes,LastModifiedUtc", lines[0]);
        Assert.Equal("\"zz-test/orphan, copy.png\",\"/media/zz-test/orphan, copy.png\",2048,2026-10-14T12:00:00.0000000Z", lines[1]);
        Assert.Equal(2, lines.Length);
    }

    [Fact]
    public void DiskExport_is_just_the_header_when_nothing_is_disk_only()
    {
        var csv = Export();

        Assert.Equal("RelativePath,Url,SizeBytes,LastModifiedUtc", csv.Trim());
    }
}
