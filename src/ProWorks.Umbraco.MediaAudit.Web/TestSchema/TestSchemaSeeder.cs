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
public class TestSchemaSeeder : INotificationHandler<UmbracoApplicationStartedNotification>
{
    private readonly IDataTypeService _dataTypeService;
    private readonly IContentTypeService _contentTypeService;
    private readonly IMemberTypeService _memberTypeService;
    private readonly ILocalizationService _localizationService;
    private readonly PropertyEditorCollection _propertyEditors;
    private readonly IConfigurationEditorJsonSerializer _configJsonSerializer;
    private readonly IShortStringHelper _shortStringHelper;
    private readonly ILogger<TestSchemaSeeder> _logger;

    public TestSchemaSeeder(
        IDataTypeService dataTypeService,
        IContentTypeService contentTypeService,
        IMemberTypeService memberTypeService,
        ILocalizationService localizationService,
        PropertyEditorCollection propertyEditors,
        IConfigurationEditorJsonSerializer configJsonSerializer,
        IShortStringHelper shortStringHelper,
        ILogger<TestSchemaSeeder> logger)
    {
        _dataTypeService = dataTypeService;
        _contentTypeService = contentTypeService;
        _memberTypeService = memberTypeService;
        _localizationService = localizationService;
        _propertyEditors = propertyEditors;
        _configJsonSerializer = configJsonSerializer;
        _shortStringHelper = shortStringHelper;
        _logger = logger;
    }

    public void Handle(UmbracoApplicationStartedNotification notification)
    {
        try
        {
            Seed();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[TestSchemaSeeder] Failed to seed media-audit test schema.");
        }
    }

    private void Seed()
    {
        var mediaPickerType = GetOrCreateDataType(
            "Media Audit Test - Media Picker",
            UmbracoConstants.PropertyEditors.Aliases.MediaPicker3,
            "Umb.PropertyEditorUi.MediaPicker");

        var richTextType = GetOrCreateDataType(
            "Media Audit Test - Rich Text",
            UmbracoConstants.PropertyEditors.Aliases.RichText,
            "Umb.PropertyEditorUi.Tiptap");

        var textType = _dataTypeService.GetByEditorAlias(UmbracoConstants.PropertyEditors.Aliases.TextBox).FirstOrDefault()
            ?? GetOrCreateDataType(
                "Media Audit Test - Textstring",
                UmbracoConstants.PropertyEditors.Aliases.TextBox,
                "Umb.PropertyEditorUi.TextBox");

        // Element type used inside the Block List - must exist before the Block List data type
        // below can reference its key.
        var testimonialBlock = GetOrCreateContentType(
            alias: "auditTestTestimonialBlock",
            name: "Audit Test - Testimonial Block",
            isElement: true,
            properties: new[]
            {
                ("avatar", "Avatar", mediaPickerType, ContentVariation.Nothing),
            });

        var blockListType = GetOrCreateDataType(
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

        var testPage = GetOrCreateContentType(
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

        SeedMemberTypeProperty(mediaPickerType);
        SeedSecondLanguage();

        _logger.LogInformation(
            "[TestSchemaSeeder] Media-audit test schema ready: doctype '{Alias}' (Title/Body Text/Featured Media [varies by culture]/Content Blocks), block '{BlockAlias}' (Avatar).",
            testPage.Alias,
            testimonialBlock.Alias);
    }

    private IDataType GetOrCreateDataType(
        string name,
        string editorAlias,
        string editorUiAlias,
        Func<IDataEditor, IDictionary<string, object>>? configure = null)
    {
        var existing = _dataTypeService.GetDataType(name);
        if (existing is not null)
        {
            return existing;
        }

        if (!_propertyEditors.TryGet(editorAlias, out var editor) || editor is null)
        {
            throw new InvalidOperationException($"Property editor '{editorAlias}' is not registered.");
        }

        // EditorUiAlias is the modern (v14+) backoffice's key for which UI component renders this
        // property - without it the property shows "The configured property editor UI could not be
        // found" even though the server-side Editor alias is perfectly valid.
        var dataType = new DataType(editor, _configJsonSerializer, -1) { Name = name, EditorUiAlias = editorUiAlias };
        if (configure is not null)
        {
            dataType.ConfigurationData = configure(editor);
        }

        _dataTypeService.Save(dataType);
        _logger.LogInformation("[TestSchemaSeeder] Created data type '{Name}'.", name);
        return dataType;
    }

    private IContentType GetOrCreateContentType(
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

        _contentTypeService.Save(contentType);
        _logger.LogInformation("[TestSchemaSeeder] Created content type '{Alias}'.", alias);
        return contentType;
    }

    private void SeedMemberTypeProperty(IDataType mediaPickerType)
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

        group.PropertyTypes.Add(new PropertyType(_shortStringHelper, mediaPickerType)
        {
            Alias = "profilePhoto",
            Name = "Profile Photo",
            Variations = ContentVariation.Nothing,
        });

        _memberTypeService.Save(memberType);
        _logger.LogInformation(
            "[TestSchemaSeeder] Added 'profilePhoto' property to member type '{Alias}'.",
            memberTypeAlias);
    }

    private void SeedSecondLanguage()
    {
        const string isoCode = "fr-FR";
        if (_localizationService.GetLanguageByIsoCode(isoCode) is not null)
        {
            return;
        }

        var language = new Language(isoCode, "French (France)") { IsDefault = false };
        _localizationService.Save(language);
        _logger.LogInformation("[TestSchemaSeeder] Created language '{IsoCode}'.", isoCode);
    }
}
