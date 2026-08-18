using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using CoreConstants = Umbraco.Cms.Core.Constants;

namespace UmbracoMediaAudit.Tests.Integration.TestSupport;

/// <summary>
/// Builds a minimal real content type with a single Media Picker 3 property against a live
/// (SQLite, per-fixture) Umbraco instance - shared by the integration test fixtures below so each
/// doesn't re-derive the property editor/data type/content type wiring already established in
/// src/UmbracoMediaAudit.Web/TestSchema/TestSchemaSeeder.cs for the manual-QA sample site.
///
/// Deliberately builds real IDataType/IContentType/IContent instances via the actual Umbraco
/// services rather than mocking anything - the entire point of the integration suite is proving
/// Umbraco's own IDataValueReference/IRelationService pipeline agrees with this package's
/// classification, which a mock can't demonstrate.
/// </summary>
internal static class MediaPickerTestSchema
{
    private const string PageTypeAlias = "mediaAuditIntegrationTestPage";
    private const string MediaPropertyAlias = "featuredMedia";

    /// <summary>Value format confirmed via reflection against MediaPicker3PropertyValueEditor+MediaWithCropsDto
    /// (Umbraco.Cms.Infrastructure 17.2.2): key/mediaKey/mediaTypeAlias/crops/focalPoint, camelCase,
    /// no explicit JsonPropertyName attributes (relies on Umbraco's default camelCase serialization).
    /// Deliberately omits crops/focalPoint (rather than sending them as [] / null) - GetReferences()
    /// produced zero relations with those included, and omitting an optional JSON member leaves a
    /// struct-typed property at its default value on deserialization instead of risking a
    /// null-into-non-nullable-struct failure that a caching layer could swallow silently.</summary>
    public static string MediaPickerValue(Guid mediaKey, string mediaTypeAlias = "Image") =>
        $$"""[{"key":"{{Guid.NewGuid()}}","mediaKey":"{{mediaKey}}","mediaTypeAlias":"{{mediaTypeAlias}}"}]""";

    public static IContentType GetOrCreatePageType(
        IContentTypeService contentTypeService,
        IDataTypeService dataTypeService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configJsonSerializer,
        IShortStringHelper shortStringHelper)
    {
        var existing = contentTypeService.Get(PageTypeAlias);
        if (existing is not null)
        {
            return existing;
        }

        if (!propertyEditors.TryGet(CoreConstants.PropertyEditors.Aliases.MediaPicker3, out var editor) || editor is null)
        {
            throw new InvalidOperationException($"Property editor '{CoreConstants.PropertyEditors.Aliases.MediaPicker3}' is not registered.");
        }

        var dataType = new DataType(editor, configJsonSerializer, -1) { Name = "Media Audit Integration Test - Media Picker" };
        dataTypeService.Save(dataType);

        var propertyType = new PropertyType(shortStringHelper, dataType)
        {
            Alias = MediaPropertyAlias,
            Name = "Featured Media",
            Variations = ContentVariation.Nothing,
        };
        var propertyTypes = new PropertyTypeCollection(true);
        propertyTypes.Add(propertyType);
        var propertyGroup = new PropertyGroup(propertyTypes)
        {
            Name = "Content",
            Alias = "content",
        };

        var contentType = new ContentType(shortStringHelper, -1)
        {
            Alias = PageTypeAlias,
            Name = "Media Audit Integration Test Page",
            Icon = "icon-document",
            AllowedAsRoot = true,
            PropertyGroups = new PropertyGroupCollection(new[] { propertyGroup }),
        };

        contentTypeService.Save(contentType);
        return contentType;
    }

    public static IContent CreatePublishedPageReferencing(
        IContentService contentService,
        IContentType pageType,
        string pageName,
        Guid referencedMediaKey,
        string mediaTypeAlias = "Image")
    {
        var page = contentService.Create(pageName, -1, pageType, -1);
        page.SetValue(MediaPropertyAlias, MediaPickerValue(referencedMediaKey, mediaTypeAlias));
        contentService.Save(page, -1);

        var publishResult = contentService.Publish(page, new[] { "*" }, -1);
        if (!publishResult.Success)
        {
            throw new InvalidOperationException($"Failed to publish test page '{pageName}': {publishResult.Result}.");
        }

        return page;
    }
}
