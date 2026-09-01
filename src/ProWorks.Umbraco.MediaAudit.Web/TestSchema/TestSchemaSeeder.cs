using Microsoft.Extensions.Logging;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Notifications;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Core.Strings;
using UmbracoConstants = Umbraco.Cms.Core.Constants;

namespace ProWorks.Umbraco.MediaAudit.Web.TestSchema;

/// <summary>
/// One-time, idempotent seeding of the doctypes/datatypes needed to manually test the Media Audit
/// dashboard against specs/001-media-usage-audit/test-media-seed/ (see that folder's README.md).
///
/// Sample-site-only dev convenience - NOT part of the shipped ProWorks.Umbraco.MediaAudit package. Safe to
/// run on every startup: every step checks for an existing entity by name/alias first, so it never
/// duplicates or overwrites anything a developer created by hand afterward.
/// </summary>
public class TestSchemaSeeder : INotificationAsyncHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDataTypeService _dataTypeService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly ILanguageService _languageService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _configJsonSerializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILogger<TestSchemaSeeder> _logger;

    public TestSchemaSeeder(
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IMemberTypeService memberTypeService,
        ILanguageService languageService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configJsonSerializer,
        IShortStringHelper shortStringHelper,
        ILogger<TestSchemaSeeder> logger)
    {
        _dataTypeService = dataTypeService;
        _contentTypeService = contentTypeService;
        _memberTypeService = memberTypeService;
        _languageService = languageService;
        _propertyEditors = propertyEditors;
        _configJsonSerializer = configJsonSerializer;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
    }

    public async Task HandleAsync(UmbracoApplicationStartedNotification notification, CancellationToken cancellationToken)
    {
        try
        {
            await SeedAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TestSchemaSeeder] Failed to seed media-audit test schema.");
        }
    }

    public async Task HandleAsync(IEnumerable<UmbracoApplicationStartedNotification> notifications, CancellationToken cancellationToken)
    {
        foreach (var notification in notifications)
        {
            await HandleAsync(notification, cancellationToken);
        }
    }

    private async Task SeedAsync()
    {
        var mediaPickerType = await GetOrCreateDataTypeAsync(
            "Media Audit Test - Media Picker",
            UmbracoConstants.PropertyEditors.Aliases.MediaPicker3,
            "Umb.PropertyEditorUi.MediaPicker");

        var richTextType = await GetOrCreateDataTypeAsync(
            "Media Audit Test - Rich Text",
            UmbracoConstants.PropertyEditors.Aliases.RichText,
            "Umb.PropertyEditorUi.Tiptap");

        var textType = (await _dataTypeService.GetByEditorAliasAsync(UmbracoConstants.PropertyEditors.Aliases.TextBox)).FirstOrDefault()
            ?? await GetOrCreateDataTypeAsync(
                "Media Audit Test - Textstring",
                UmbracoConstants.PropertyEditors.Aliases.TextBox,
                "Umb.PropertyEditorUi.TextBox");

        var testimonialBlock = await GetOrCreateContentTypeAsync(
            alias: "auditTestTestimonialBlock",
            name: "Audit Test - Testimonial Block",
            isElement: true,
            properties: new[]
            {
                ("avatar", "Avatar", mediaPickerType, ContentVariation.Nothing),
            });

        var blockListType = await GetOrCreateDataTypeAsync(
            "Media Audit Test - Block List (Testimonial)",
            UmbracoConstants.PropertyEditors.Aliases.BlockList,
            "Umb.PropertyEditorUi.BlockList",
            editor =>
            {
                var configEditor = editor.GetConfigurationEditor();
                var config = new BlockListConfiguration
                {
                    Blocks = new[]
                    {
                        new BlockListConfiguration.BlockConfiguration
                        {
                            ContentElementTypeKey = testimonialBlock.Key,
                        },
                    },
                };
                return configEditor.FromConfigurationObject(config, _configJsonSerializer);
            });

        var testPage = await GetOrCreateContentTypeAsync(
            alias: "auditTestPage",
            name: "Audit Test Page",
            isElement: false,
            allowedAsRoot: true,
            variesByCulture: true,
            properties: new[]
            {
                ("title", "Title", textType, ContentVariation.Nothing),
                ("bodyText", "Body Text", richTextType, ContentVariation.Nothing),
                ("featuredMedia", "Featured Media", mediaPickerType, ContentVariation.Culture),
                ("contentBlocks", "Content Blocks", blockListType, ContentVariation.Nothing),
            });

        await SeedMemberTypePropertyAsync(mediaPickerType);
        await SeedSecondLanguageAsync();

        _logger.LogInformation(
            "[TestSchemaSeeder] Media-audit test schema ready: doctype '{Alias}' (Title/Body Text/Featured Media [varies by culture]/Content Blocks), block '{BlockAlias}' (Avatar).",
            testPage.Alias,
            testimonialBlock.Alias);
    }

    private async Task<IDataType> GetOrCreateDataTypeAsync(
        string name,
        string editorAlias,
        string editorUiAlias,
        Func<IDataEditor, IDictionary<string, object>>? configure = null)
    {
        var existing = await _dataTypeService.GetAsync(name);
        if (existing is not null)
        {
            return existing;
        }

        if (!_propertyEditors.TryGet(editorAlias, out var editor) || editor is null)
        {
            throw new InvalidOperationException($"Property editor '{editorAlias}' is not registered.");
        }

        var dataType = new DataType(editor, _configJsonSerializer, -1) { Name = name, EditorUiAlias = editorUiAlias };
        if (configure is not null)
        {
            dataType.ConfigurationData = configure(editor);
        }

        var result = await _dataTypeService.CreateAsync(dataType, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create data type '{name}': {result.Status}.");
        }

        _logger.LogInformation("[TestSchemaSeeder] Created data type '{Name}'.", name);
        return result.Result!;
    }

    private async Task<IContentType> GetOrCreateContentTypeAsync(
        string alias,
        string name,
        bool isElement,
        (string Alias, string Name, IDataType DataType, ContentVariation Variation)[] properties,
        bool allowedAsRoot = false,
        bool variesByCulture = false)
    {
        var existing = _contentTypeService.Get(alias);
        if (existing is not null)
        {
            return existing;
        }

        var propertyTypes = new PropertyTypeCollection(true);
        foreach (var (propAlias, propName, dataType, variation) in properties)
        {
            propertyTypes.Add(new PropertyType(_shortStringHelper, dataType)
            {
                Alias = propAlias,
                Name = propName,
                Variations = variation,
            });
        }

        var propertyGroup = new PropertyGroup(propertyTypes) { Name = "Content", Alias = "content" };

        var contentType = new ContentType(_shortStringHelper, -1)
        {
            Alias = alias,
            Name = name,
            Icon = isElement ? "icon-plugin" : "icon-document",
            IsElement = isElement,
            AllowedAsRoot = allowedAsRoot,
            Variations = variesByCulture ? ContentVariation.Culture : ContentVariation.Nothing,
            PropertyGroups = new PropertyGroupCollection(new[] { propertyGroup }),
        };

        var result = await _contentTypeService.CreateAsync(contentType, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to create content type '{alias}': {result.Result}.");
        }

        _logger.LogInformation("[TestSchemaSeeder] Created content type '{Alias}'.", alias);
        return contentType;
    }

    private async Task SeedMemberTypePropertyAsync(IDataType mediaPickerType)
    {
        var memberTypeAlias = _memberTypeService.GetDefault();
        var memberType = _memberTypeService.Get(memberTypeAlias);
        if (memberType is null)
        {
            _logger.LogWarning(
                "[TestSchemaSeeder] Default member type '{Alias}' not found, skipping Member Type seeding.",
                memberTypeAlias);
            return;
        }

        if (memberType.PropertyTypes.Any(p => p.Alias == "profilePhoto"))
        {
            return;
        }

        var group = memberType.PropertyGroups.FirstOrDefault();
        if (group is null)
        {
            group = new PropertyGroup(new PropertyTypeCollection(true)) { Name = "Membership", Alias = "membership" };
            memberType.PropertyGroups.Add(group);
        }

        group!.PropertyTypes!.Add(new PropertyType(_shortStringHelper, mediaPickerType)
        {
            Alias = "profilePhoto",
            Name = "Profile Photo",
            Variations = ContentVariation.Nothing,
        });

        var result = await _memberTypeService.UpdateAsync(memberType, UmbracoConstants.Security.SuperUserKey);
        if (!result.Success)
        {
            throw new InvalidOperationException($"Failed to update member type '{memberTypeAlias}': {result.Result}.");
        }

        _logger.LogInformation(
            "[TestSchemaSeeder] Added 'profilePhoto' property to member type '{Alias}'.",
            memberTypeAlias);
    }

    private async Task SeedSecondLanguageAsync()
    {
        const string isoCode = "fr-FR";
        if (await _languageService.GetAsync(isoCode) is not null)
        {
            return;
        }

        var language = new Language(isoCode, "French (France)") { IsDefault = false };
        await _languageService.CreateAsync(language, UmbracoConstants.Security.SuperUserKey);
        _logger.LogInformation("[TestSchemaSeeder] Created language '{IsoCode}'.", isoCode);
    }
}
