using Moq;
using Umbraco.Cms.Core.IO;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class DiskMediaScannerTests
{
    private static (DiskMediaScanner Scanner, Mock<IFileSystem> FileSystem) CreateScanner()
    {
        var fileSystem = new Mock<IFileSystem>();
        var provider = new Mock<IMediaFileSystemProvider>();
        provider.Setup(p => p.FileSystem).Returns(fileSystem.Object);

        return (new DiskMediaScanner(provider.Object), fileSystem);
    }

    private static void SetupEmpty(Mock<IFileSystem> fileSystem, string path)
    {
        fileSystem.Setup(f => f.GetFiles(path)).Returns(Array.Empty<string>());
        fileSystem.Setup(f => f.GetDirectories(path)).Returns(Array.Empty<string>());
    }

    [Fact]
    public async Task ScanAsync_returns_every_file_at_the_root()
    {
        var (scanner, fileSystem) = CreateScanner();
        fileSystem.Setup(f => f.GetFiles(string.Empty)).Returns(new[] { "readme.txt" });
        fileSystem.Setup(f => f.GetDirectories(string.Empty)).Returns(Array.Empty<string>());
        fileSystem.Setup(f => f.GetUrl("readme.txt")).Returns("/media/readme.txt");
        fileSystem.Setup(f => f.GetSize("readme.txt")).Returns(123L);
        fileSystem.Setup(f => f.GetLastModified("readme.txt")).Returns(new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero));

        var items = await scanner.ScanAsync();

        var item = Assert.Single(items);
        Assert.Equal("readme.txt", item.RelativePath);
        Assert.Equal("/media/readme.txt", item.Url);
        Assert.Equal(123L, item.SizeBytes);
    }

    [Fact]
    public async Task ScanAsync_recurses_into_nested_directories()
    {
        var (scanner, fileSystem) = CreateScanner();
        fileSystem.Setup(f => f.GetFiles(string.Empty)).Returns(Array.Empty<string>());
        fileSystem.Setup(f => f.GetDirectories(string.Empty)).Returns(new[] { "1234" });
        fileSystem.Setup(f => f.GetFiles("1234")).Returns(new[] { "1234/photo.jpg" });
        fileSystem.Setup(f => f.GetDirectories("1234")).Returns(Array.Empty<string>());
        fileSystem.Setup(f => f.GetUrl("1234/photo.jpg")).Returns("/media/1234/photo.jpg");
        fileSystem.Setup(f => f.GetSize("1234/photo.jpg")).Returns(9000L);
        fileSystem.Setup(f => f.GetLastModified("1234/photo.jpg")).Returns(DateTimeOffset.UnixEpoch);

        var items = await scanner.ScanAsync();

        var item = Assert.Single(items);
        Assert.Equal("1234/photo.jpg", item.RelativePath);
    }

    [Fact]
    public async Task ScanAsync_normalizes_backslashes_and_leading_slashes()
    {
        var (scanner, fileSystem) = CreateScanner();
        fileSystem.Setup(f => f.GetFiles(string.Empty)).Returns(new[] { @"\1234\photo.jpg" });
        fileSystem.Setup(f => f.GetDirectories(string.Empty)).Returns(Array.Empty<string>());
        fileSystem.Setup(f => f.GetUrl(@"\1234\photo.jpg")).Returns("/media/1234/photo.jpg");
        fileSystem.Setup(f => f.GetSize(@"\1234\photo.jpg")).Returns(1L);
        fileSystem.Setup(f => f.GetLastModified(@"\1234\photo.jpg")).Returns(DateTimeOffset.UnixEpoch);

        var items = await scanner.ScanAsync();

        Assert.Equal("1234/photo.jpg", Assert.Single(items).RelativePath);
    }

    [Fact]
    public async Task ScanAsync_returns_empty_for_an_empty_file_system()
    {
        var (scanner, fileSystem) = CreateScanner();
        SetupEmpty(fileSystem, string.Empty);

        var items = await scanner.ScanAsync();

        Assert.Empty(items);
    }
}
