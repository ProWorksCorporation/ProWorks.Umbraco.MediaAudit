using Moq;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Strings;

namespace UmbracoMediaAudit.Tests.Unit.TestSupport;

/// <summary>
/// Builds real (not mocked) Umbraco content model objects - `Content`/`Media`/`MediaType`/
/// `ContentType`/`PropertyType` are Umbraco's own concrete POCO classes and don't need a live
/// database to construct, only an `IShortStringHelper` (real, via `DefaultShortStringHelper`) and,
/// for property types, an `IDataType` (stubbed via Moq - PropertyType only reads a couple of its
/// members at construction, so a loose stub is enough; no property editor plumbing needed).
///
/// Verified against the real Umbraco.Cms.Core 17.2.2 assembly via reflection before writing this -
/// see [[media-audit-local-dev]] memory for the technique.
/// </summary>
internal static class ModelFactory
{
    public static readonly IShortStringHelper ShortStringHelper = new DefaultShortStringHelper(new DefaultShortStringHelperConfig());

    /// <summary>A minimal IDataType stub - just enough for PropertyType's constructor, not a real property editor.</summary>
    private static IDataType StubDataType()
    {
        var mock = new Mock<IDataType>();
        mock.SetupAllProperties();
        mock.Object.Id = 1;
        return mock.Object;
    }

    public static IMediaType CreateMediaType(string alias = "image", string name = "Image", bool withFileProperties = true)
    {
        var mediaType = new MediaType(ShortStringHelper, -1) { Alias = alias, Name = name };

        if (withFileProperties)
        {
            var propertyTypes = new PropertyTypeCollection(true);
            propertyTypes.Add(new PropertyType(ShortStringHelper, StubDataType()) { Alias = "umbracoFile", Name = "File" });
            propertyTypes.Add(new PropertyType(ShortStringHelper, StubDataType()) { Alias = "umbracoBytes", Name = "Bytes" });
            propertyTypes.Add(new PropertyType(ShortStringHelper, StubDataType()) { Alias = "umbracoExtension", Name = "Extension" });
            mediaType.PropertyGroups = new PropertyGroupCollection(new[] { new PropertyGroup(propertyTypes) { Name = "Content", Alias = "content" } });
        }

        return mediaType;
    }

    public static IMediaType CreateFolderMediaType() => new MediaType(ShortStringHelper, -1) { Alias = "Folder", Name = "Folder" };

    public static IMedia CreateMedia(string name, IMediaType mediaType, int id, int parentId = -1, long? sizeBytes = null, string? extension = null)
    {
        var media = new Media(name, parentId, mediaType) { Id = id };
        if (sizeBytes is not null) media.SetValue("umbracoBytes", sizeBytes.Value);
        if (extension is not null) media.SetValue("umbracoExtension", extension);
        return media;
    }

    /// <summary>A content type with one invariant text property ("bodyText") and, if varying, one culture-variant property ("variantText") - enough for scanner/classification tests.</summary>
    public static IContentType CreateContentType(string alias = "page", string name = "Page", bool variesByCulture = false)
    {
        var contentType = new ContentType(ShortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Variations = variesByCulture ? ContentVariation.Culture : ContentVariation.Nothing,
        };

        var propertyTypes = new PropertyTypeCollection(true);
        propertyTypes.Add(new PropertyType(ShortStringHelper, StubDataType()) { Alias = "bodyText", Name = "Body Text" });
        if (variesByCulture)
        {
            propertyTypes.Add(new PropertyType(ShortStringHelper, StubDataType())
            {
                Alias = "variantText",
                Name = "Variant Text",
                Variations = ContentVariation.Culture,
            });
        }

        contentType.PropertyGroups = new PropertyGroupCollection(new[] { new PropertyGroup(propertyTypes) { Name = "Content", Alias = "content" } });
        return contentType;
    }

    public static IContent CreateContent(string name, IContentType contentType, int id, bool published = true, int parentId = -1)
    {
        var content = new Content(name, parentId, contentType) { Id = id, Published = published };
        return content;
    }
}
