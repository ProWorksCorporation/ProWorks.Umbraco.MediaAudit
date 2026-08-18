using NUnit.Framework;
using Umbraco.Cms.Tests.Common.Testing;
using UmbracoMediaAudit.Models;
using UmbracoMediaAudit.Services;
using UmbracoMediaAudit.Tests.Integration.TestSupport;

namespace UmbracoMediaAudit.Tests.Integration;

/// <summary>
/// Round-trips DeletionLogService against a real, freshly-migrated SQLite database - this is what
/// DeletionLogServiceTests (unit, IUmbracoDatabase mocked) can't prove: that AddDeletionLogTablePlan's
/// package migration actually creates the table, and that LogAction/GetPagedHistory's raw SQL and NPoco
/// mapping genuinely round-trip through it (research.md §10, FR-019).
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class DeletionLogServiceIntegrationTests : MediaAuditIntegrationTestBase
{
    private IDeletionLogService DeletionLogService => GetRequiredService<IDeletionLogService>();

    [Test]
    public void LogAction_persists_a_row_that_GetPagedHistory_reads_back_correctly()
    {
        var items = new[]
        {
            new DeletionLogItem { Key = Guid.NewGuid(), Name = "one.jpg" },
            new DeletionLogItem { Key = Guid.NewGuid(), Name = "two.jpg" },
        };

        var logEntryId = DeletionLogService.LogAction(
            DeletionLogActionType.Delete, performedByUserId: -1, items, totalSizeBytes: 2048, skippedCount: 1);

        Assert.That(logEntryId, Is.GreaterThan(0));

        var (entries, totalItems) = DeletionLogService.GetPagedHistory(page: 1, pageSize: 50);

        Assert.That(totalItems, Is.EqualTo(1));
        var entry = entries.Single();
        Assert.That(entry.Id, Is.EqualTo(logEntryId));
        Assert.That(entry.ActionType, Is.EqualTo(DeletionLogActionType.Delete));
        Assert.That(entry.PerformedByUserId, Is.EqualTo(-1));
        Assert.That(entry.ItemCount, Is.EqualTo(2));
        Assert.That(entry.TotalSizeBytes, Is.EqualTo(2048));
        Assert.That(entry.SkippedCount, Is.EqualTo(1));
        Assert.That(entry.Items.Select(i => i.Name), Is.EquivalentTo(new[] { "one.jpg", "two.jpg" }));
    }

    [Test]
    public void LogAction_writes_exactly_one_row_even_for_a_fully_skipped_batch()
    {
        DeletionLogService.LogAction(DeletionLogActionType.Purge, -1, Array.Empty<DeletionLogItem>(), totalSizeBytes: 0, skippedCount: 3);

        var (entries, totalItems) = DeletionLogService.GetPagedHistory(1, 50);

        Assert.That(totalItems, Is.EqualTo(1));
        var entry = entries.Single();
        Assert.That(entry.ItemCount, Is.EqualTo(0));
        Assert.That(entry.SkippedCount, Is.EqualTo(3));
        Assert.That(entry.Items, Is.Empty);
    }

    [Test]
    public void GetPagedHistory_orders_newest_first()
    {
        DeletionLogService.LogAction(DeletionLogActionType.Delete, -1, Array.Empty<DeletionLogItem>(), 0, 0);
        DeletionLogService.LogAction(DeletionLogActionType.Purge, -1, Array.Empty<DeletionLogItem>(), 0, 0);

        var (entries, totalItems) = DeletionLogService.GetPagedHistory(1, 50);

        Assert.That(totalItems, Is.EqualTo(2));
        Assert.That(entries[0].ActionType, Is.EqualTo(DeletionLogActionType.Purge));
        Assert.That(entries[1].ActionType, Is.EqualTo(DeletionLogActionType.Delete));
    }
}
