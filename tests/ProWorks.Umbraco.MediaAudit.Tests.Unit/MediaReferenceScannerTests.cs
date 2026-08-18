using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Persistence.Querying;
using Umbraco.Cms.Core.Services;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;
using ProWorks.Umbraco.MediaAudit.Tests.Unit.TestSupport;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

/// <summary>
/// research.md §4: the scan-based safety net is editor-agnostic GUID/path substring matching -
/// exactly what the reference Python script (reference/media_audit.py) already validated in
/// practice, ported here without a live database.
/// </summary>
public class MediaReferenceScannerTests
{
    private static Mock<IContentService> SetUpContentService(params IContent[] contentItems)
    {
        var mock = new Mock<IContentService>();
        long total = contentItems.Length;
        mock.Setup(s => s.GetPagedDescendants(
                It.IsAny<int>(), It.IsAny<long>(), It.IsAny<int>(), out total,
                It.IsAny<IQuery<IContent>>(), It.IsAny<Ordering>()))
            .Returns(contentItems);
        return mock;
    }

    [Fact]
    public async Task FindReferencesAsync_matches_guid_with_hyphens_in_a_property_value()
    {
        var contentType = ModelFactory.CreateContentType();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);

        var content = ModelFactory.CreateContent("Homepage", contentType, id: 100);
        content.SetValue("bodyText", $"<p>see {media.Key}</p>");

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        var usage = Assert.Single(results);
        Assert.Equal(100, usage.ContentId);
        Assert.Equal(MediaUsageDetectionSource.Scan, usage.DetectionSource);
        Assert.Equal(ContentPublishState.Published, usage.PublishState);
    }

    [Fact]
    public async Task FindReferencesAsync_matches_guid_without_hyphens_too()
    {
        var contentType = ModelFactory.CreateContentType();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);

        var content = ModelFactory.CreateContent("Homepage", contentType, id: 100);
        content.SetValue("bodyText", $"{{\"udi\":\"umb://media/{media.Key:N}\"}}");

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        Assert.Single(results);
    }

    [Fact]
    public async Task FindReferencesAsync_returns_empty_when_nothing_references_the_media()
    {
        var contentType = ModelFactory.CreateContentType();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);

        var content = ModelFactory.CreateContent("Homepage", contentType, id: 100);
        content.SetValue("bodyText", "nothing relevant here");

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        Assert.Empty(results);
    }

    [Fact]
    public async Task FindReferencesAsync_reports_draft_only_content_as_Draft_not_Published()
    {
        var contentType = ModelFactory.CreateContentType();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);

        // FR-004: a reference from unpublished/draft-only content still counts as "Used".
        var content = ModelFactory.CreateContent("Draft Page", contentType, id: 100, published: false);
        content.SetValue("bodyText", media.Key.ToString());

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        var usage = Assert.Single(results);
        Assert.Equal(ContentPublishState.Draft, usage.PublishState);
    }

    [Fact]
    public async Task FindReferencesAsync_attributes_the_correct_culture_for_a_variant_property()
    {
        // research.md §8 (FR-017): a reference in only one language variant must still be found,
        // and reported against that specific culture - not silently merged/ignored.
        var contentType = ModelFactory.CreateContentType(variesByCulture: true);
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);

        var content = ModelFactory.CreateContent("Homepage", contentType, id: 100);
        content.SetCultureName("Homepage (FR)", "fr-FR");
        content.SetCultureName("Homepage (EN)", "en-US");
        content.SetValue("variantText", media.Key.ToString(), culture: "fr-FR");
        // en-US variant deliberately left without the reference.

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        var usage = Assert.Single(results);
        Assert.Equal("fr-FR", usage.Culture);
    }

    [Fact]
    public async Task FindReferencesAsync_matches_the_media_file_path_not_just_the_guid()
    {
        var contentType = ModelFactory.CreateContentType();
        var mediaType = ModelFactory.CreateMediaType();
        var media = ModelFactory.CreateMedia("photo.jpg", mediaType, id: 1);
        media.SetValue("umbracoFile", "/media/1001/photo.jpg");

        var content = ModelFactory.CreateContent("Homepage", contentType, id: 100);
        content.SetValue("bodyText", "<img src=\"/media/1001/photo.jpg\">");

        var scanner = new MediaReferenceScanner(SetUpContentService(content).Object);
        var results = await scanner.FindReferencesAsync(media);

        Assert.Single(results);
    }
}
