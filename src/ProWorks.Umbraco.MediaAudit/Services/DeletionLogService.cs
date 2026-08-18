using System.Text.Json;
using Umbraco.Cms.Infrastructure.Scoping;
using ProWorks.Umbraco.MediaAudit.Migrations;
using ProWorks.Umbraco.MediaAudit.Models;

namespace ProWorks.Umbraco.MediaAudit.Services;

/// <inheritdoc cref="IDeletionLogService" />
public sealed class DeletionLogService : IDeletionLogService
{
    private const string TableName = AddDeletionLogTablePlan.TableName;

    private readonly IScopeProvider _scopeProvider;

    public DeletionLogService(IScopeProvider scopeProvider)
    {
        _scopeProvider = scopeProvider;
    }

    public int LogAction(
        DeletionLogActionType actionType,
        int performedByUserId,
        IReadOnlyList<DeletionLogItem> items,
        long totalSizeBytes,
        int skippedCount)
    {
        using var scope = _scopeProvider.CreateScope();

        var row = new DeletionLogRow
        {
            OccurredAt = DateTime.UtcNow,
            ActionType = actionType.ToString(),
            PerformedByUserId = performedByUserId,
            ItemCount = items.Count,
            TotalSizeBytes = totalSizeBytes,
            Items = JsonSerializer.Serialize(items),
            SkippedCount = skippedCount,
        };

        // Table/PK column supplied explicitly (rather than via attributes on DeletionLogRow) so this
        // stays a plain, attribute-free POCO matched by the SQL's own column aliases in GetPagedHistory.
        var id = scope.Database.Insert(TableName, "id", true, row);
        scope.Complete();
        return Convert.ToInt32(id);
    }

    public (IReadOnlyList<DeletionLogEntry> Entries, int TotalItems) GetPagedHistory(int page, int pageSize)
    {
        using var scope = _scopeProvider.CreateScope();

        var totalItems = scope.Database.ExecuteScalar<int>($"SELECT COUNT(*) FROM {TableName}");

        // NPoco's paged Fetch overload handles provider-specific paging syntax (SQLite vs. SQL
        // Server) itself, rather than hand-writing OFFSET/FETCH or LIMIT/OFFSET here.
        var rows = scope.Database.Fetch<DeletionLogRow>(
            Math.Max(page, 1),
            pageSize,
            $"SELECT id AS Id, occurredAt AS OccurredAt, actionType AS ActionType, " +
            $"performedByUserId AS PerformedByUserId, itemCount AS ItemCount, " +
            $"totalSizeBytes AS TotalSizeBytes, items AS Items, skippedCount AS SkippedCount " +
            $"FROM {TableName} ORDER BY occurredAt DESC");

        scope.Complete();

        var entries = rows.Select(r => new DeletionLogEntry
        {
            Id = r.Id,
            OccurredAt = r.OccurredAt,
            ActionType = Enum.Parse<DeletionLogActionType>(r.ActionType),
            PerformedByUserId = r.PerformedByUserId,
            ItemCount = r.ItemCount,
            TotalSizeBytes = r.TotalSizeBytes,
            Items = JsonSerializer.Deserialize<List<DeletionLogItem>>(r.Items) ?? new List<DeletionLogItem>(),
            SkippedCount = r.SkippedCount,
        }).ToList();

        return (entries, totalItems);
    }

    /// <summary>
    /// Plain, attribute-free row shape for NPoco - matched to <see cref="AddDeletionLogTablePlan"/>'s
    /// columns via explicit SQL aliases (GetPagedHistory) or the explicit table/PK-name overload of
    /// Insert (LogAction), rather than [TableName]/[Column] attributes. Internal rather than private
    /// so unit tests can set up Moq expectations against IUmbracoDatabase's generic Insert&lt;T&gt;/
    /// Fetch&lt;T&gt; calls (see InternalsVisibleTo in AssemblyInfo.cs) - otherwise this type isn't
    /// nameable outside this class at all, and those generic methods can't be mocked without it.
    /// </summary>
    internal sealed class DeletionLogRow
    {
        public int Id { get; set; }
        public DateTime OccurredAt { get; set; }
        public string ActionType { get; set; } = "";
        public int PerformedByUserId { get; set; }
        public int ItemCount { get; set; }
        public long TotalSizeBytes { get; set; }
        public string Items { get; set; } = "";
        public int SkippedCount { get; set; }
    }
}
