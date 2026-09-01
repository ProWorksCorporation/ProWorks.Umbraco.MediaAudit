using NUnit.Framework;
using Umbraco.Cms.Core.PropertyEditors;
using Umbraco.Cms.Core.Serialization;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;
using ProWorks.Umbraco.MediaAudit.Tests.Integration.TestSupport;
using CoreConstants = Umbraco.Cms.Core.Constants;

namespace ProWorks.Umbraco.MediaAudit.Tests.Integration;

/// <summary>
/// Exercises MediaDeleteService/MediaPurgeService end-to-end against a real database: real
/// MoveToRecycleBin/Delete side effects, and (critically) MediaDeleteService's mandatory
/// re-check (research.md §4-5) actually consulting a real IRelationService-backed
/// MediaAuditService.GetUsagesAsync rather than a mock told what to return.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class MediaDeleteAndPurgeIntegrationTests : MediaAuditIntegrationTestBase
{
    private IMediaService MediaService => GetRequiredService<IMediaService>();
    private IContentService ContentService => GetRequiredService<IContentService>();
    private IContentTypeService ContentTypeService => GetRequiredService<IContentTypeService>();
    private IDataTypeService DataTypeService => GetRequiredService<IDataTypeService>();
    private PropertyEditorCollection PropertyEditors => GetRequiredService<PropertyEditorCollection>();
    private IConfigurationEditorJsonSerializer ConfigJsonSerializer => GetRequiredService<IConfigurationEditorJsonSerializer>();
    private IMediaDeleteService DeleteService => GetRequiredService<IMediaDeleteService>();
    private IMediaPurgeService PurgeService => GetRequiredService<IMediaPurgeService>();
    private IDeletionLogService DeletionLogService => GetRequiredService<IDeletionLogService>();

    [Test]
    public async Task DeleteAsync_moves_an_unused_item_to_the_recycle_bin_and_logs_one_entry()
    {
        var media = MediaService.CreateMediaWithIdentity("deletable.jpg", -1, CoreConstants.Conventions.MediaTypes.Image);

        var result = await DeleteService.DeleteAsync(new[] { media.Key }, performingUserId: -1);

        Assert.That(result.Deleted, Is.EquivalentTo(new[] { media.Key }));
        Assert.That(result.Skipped, Is.Empty);

        var reloaded = MediaService.GetById(media.Key);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.Trashed, Is.True);

        var (entries, _) = DeletionLogService.GetPagedHistory(1, 10);
        var entry = entries.Single(e => e.Id == result.LogEntryId);
        Assert.That(entry.ActionType, Is.EqualTo(DeletionLogActionType.Delete));
        Assert.That(entry.ItemCount, Is.EqualTo(1));
        Assert.That(entry.SkippedCount, Is.EqualTo(0));
    }

    [Test]
    public async Task DeleteAsync_skips_an_item_that_is_actually_referenced_by_a_published_page()
    {
        var image = MediaService.CreateMediaWithIdentity("actually-used.jpg", -1, CoreConstants.Conventions.MediaTypes.Image);
        var pageType = await MediaPickerTestSchema.GetOrCreatePageType(
            ContentTypeService, DataTypeService, PropertyEditors, ConfigJsonSerializer, ShortStringHelper);
        MediaPickerTestSchema.CreatePublishedPageReferencing(ContentService, pageType, "Landing Page", image.Key);

        var result = await DeleteService.DeleteAsync(new[] { image.Key }, performingUserId: -1);

        Assert.That(result.Deleted, Is.Empty);
        var skip = result.Skipped.Single();
        Assert.That(skip.MediaKey, Is.EqualTo(image.Key));
        Assert.That(skip.Reason, Is.EqualTo("NowReferenced"));

        var reloaded = MediaService.GetById(image.Key);
        Assert.That(reloaded, Is.Not.Null);
        Assert.That(reloaded!.Trashed, Is.False);
    }

    [Test]
    public void Purge_only_purges_trashed_items_and_skips_a_non_trashed_one_in_the_same_batch()
    {
        var trashed = MediaService.CreateMediaWithIdentity("already-trashed.jpg", -1, CoreConstants.Conventions.MediaTypes.Image);
        MediaService.MoveToRecycleBin(trashed);
        var stillLive = MediaService.CreateMediaWithIdentity("still-live.jpg", -1, CoreConstants.Conventions.MediaTypes.Image);

        var result = PurgeService.Purge(new[] { trashed.Key, stillLive.Key }, performingUserId: -1);

        Assert.That(result.Purged, Is.EquivalentTo(new[] { trashed.Key }));
        var skip = result.Skipped.Single();
        Assert.That(skip.MediaKey, Is.EqualTo(stillLive.Key));
        Assert.That(skip.Reason, Is.EqualTo("NotTrashed"));

        Assert.That(MediaService.GetById(trashed.Key), Is.Null);
        Assert.That(MediaService.GetById(stillLive.Key), Is.Not.Null);

        var (entries, _) = DeletionLogService.GetPagedHistory(1, 10);
        var entry = entries.Single(e => e.Id == result.LogEntryId);
        Assert.That(entry.ActionType, Is.EqualTo(DeletionLogActionType.Purge));
        Assert.That(entry.ItemCount, Is.EqualTo(1));
        Assert.That(entry.SkippedCount, Is.EqualTo(1));
    }
}
