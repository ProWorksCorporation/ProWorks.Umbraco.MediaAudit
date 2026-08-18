using System.Data;
using Moq;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Infrastructure.Persistence;
using Umbraco.Cms.Infrastructure.Scoping;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Tests.Unit;

public class DeletionLogServiceTests
{
    private static (DeletionLogService Service, Mock<IUmbracoDatabase> Database) CreateService()
    {
        var database = new Mock<IUmbracoDatabase>();
        var scope = new Mock<IScope>();
        scope.SetupGet(s => s.Database).Returns(database.Object);

        var scopeProvider = new Mock<IScopeProvider>();
        scopeProvider
            .Setup(p => p.CreateScope(
                // global:: is required here (not just before the rename) - this file's namespace
                // now nests under ProWorks.Umbraco.*, and C# searches enclosing namespaces for a
                // matching child before falling back to the true global "Umbraco" root, so a bare
                // "Umbraco.Cms...." reference written inside the namespace body resolves to
                // "ProWorks.Umbraco.Cms" (which doesn't exist) instead of the real Umbraco SDK.
                It.IsAny<IsolationLevel>(), It.IsAny<global::Umbraco.Cms.Core.Scoping.RepositoryCacheMode>(), It.IsAny<IEventDispatcher>(),
                It.IsAny<IScopedNotificationPublisher>(), It.IsAny<bool?>(), It.IsAny<bool>(), It.IsAny<bool>()))
            .Returns(scope.Object);

        return (new DeletionLogService(scopeProvider.Object), database);
    }

    [Fact]
    public void LogAction_inserts_one_row_with_the_items_JSON_serialized()
    {
        var (service, database) = CreateService();
        DeletionLogService.DeletionLogRow? capturedRow = null;
        database
            .Setup(d => d.Insert(
                "UmbracoMediaAuditDeletionLog", "id", true,
                It.IsAny<DeletionLogService.DeletionLogRow>()))
            .Callback<string, string, bool, DeletionLogService.DeletionLogRow>((_, _, _, row) => capturedRow = row)
            .Returns((object)99);

        var items = new[] { new DeletionLogItem { Key = Guid.NewGuid(), Name = "hero.jpg" } };
        var logEntryId = service.LogAction(DeletionLogActionType.Delete, performedByUserId: 7, items, totalSizeBytes: 500, skippedCount: 1);

        Assert.Equal(99, logEntryId);
        Assert.NotNull(capturedRow);
        Assert.Equal("Delete", capturedRow!.ActionType);
        Assert.Equal(7, capturedRow.PerformedByUserId);
        Assert.Equal(1, capturedRow.ItemCount);
        Assert.Equal(500, capturedRow.TotalSizeBytes);
        Assert.Equal(1, capturedRow.SkippedCount);
        Assert.Contains("hero.jpg", capturedRow.Items);
        database.Verify(d => d.Insert("UmbracoMediaAuditDeletionLog", "id", true, It.IsAny<DeletionLogService.DeletionLogRow>()), Times.Once);
    }

    [Fact]
    public void LogAction_writes_exactly_one_row_even_for_a_fully_skipped_batch()
    {
        var (service, database) = CreateService();
        database
            .Setup(d => d.Insert("UmbracoMediaAuditDeletionLog", "id", true, It.IsAny<DeletionLogService.DeletionLogRow>()))
            .Returns((object)100);

        service.LogAction(DeletionLogActionType.Purge, performedByUserId: 7, Array.Empty<DeletionLogItem>(), totalSizeBytes: 0, skippedCount: 3);

        database.Verify(
            d => d.Insert("UmbracoMediaAuditDeletionLog", "id", true, It.Is<DeletionLogService.DeletionLogRow>(r => r.ItemCount == 0 && r.SkippedCount == 3)),
            Times.Once);
    }

    [Fact]
    public void GetPagedHistory_maps_rows_back_to_entries_and_deserializes_items()
    {
        var (service, database) = CreateService();
        var itemKey = Guid.NewGuid();
        var row = new DeletionLogService.DeletionLogRow
        {
            Id = 5,
            OccurredAt = new DateTime(2026, 8, 18, 12, 0, 0, DateTimeKind.Utc),
            ActionType = "Purge",
            PerformedByUserId = 7,
            ItemCount = 1,
            TotalSizeBytes = 1234,
            // Matches what LogAction's plain System.Text.Json.Serialize(items) actually produces -
            // PascalCase, no custom naming policy - not the "key"/"name" a hand-guess might use.
            Items = $"[{{\"Key\":\"{itemKey}\",\"Name\":\"hero.jpg\"}}]",
            SkippedCount = 0,
        };

        database
            .Setup(d => d.Fetch<DeletionLogService.DeletionLogRow>(It.IsAny<long>(), It.IsAny<long>(), It.IsAny<string>()))
            .Returns(new List<DeletionLogService.DeletionLogRow> { row });
        database.Setup(d => d.ExecuteScalar<int>(It.IsAny<string>())).Returns(1);

        var (entries, totalItems) = service.GetPagedHistory(page: 1, pageSize: 50);

        Assert.Equal(1, totalItems);
        var entry = Assert.Single(entries);
        Assert.Equal(5, entry.Id);
        Assert.Equal(DeletionLogActionType.Purge, entry.ActionType);
        var item = Assert.Single(entry.Items);
        Assert.Equal(itemKey, item.Key);
        Assert.Equal("hero.jpg", item.Name);
    }
}
