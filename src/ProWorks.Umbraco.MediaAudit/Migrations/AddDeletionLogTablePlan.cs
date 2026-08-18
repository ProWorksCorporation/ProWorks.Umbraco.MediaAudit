using Umbraco.Cms.Core.Packaging;
using Umbraco.Cms.Infrastructure.Migrations;

namespace ProWorks.Umbraco.MediaAudit.Migrations;

/// <summary>
/// Creates the <see cref="TableName"/> table (FR-019, research.md §10, data-model.md
/// DeletionLogEntry) via Umbraco's Package Migration mechanism - the one deliberate exception to
/// the "no new persistent schema" posture elsewhere in this feature, since a deletion/purge record
/// must outlive a single backoffice session. One row per delete/purge batch, never per item.
///
/// Registered in UmbracoMediaAuditApiComposer.cs via <c>builder.PackageMigrationPlans().Add&lt;&gt;()</c>.
/// </summary>
public sealed class AddDeletionLogTablePlan : PackageMigrationPlan
{
    public const string TableName = "UmbracoMediaAuditDeletionLog";

    public AddDeletionLogTablePlan() : base("ProWorks.Umbraco.MediaAudit")
    {
    }

    protected override void DefinePlan()
    {
        To<AddDeletionLogTableMigration>("umbraco-media-audit-deletion-log-v1");
    }
}

/// <summary>The actual schema change for <see cref="AddDeletionLogTablePlan"/>'s one migration step.</summary>
internal sealed class AddDeletionLogTableMigration : AsyncMigrationBase
{
    public AddDeletionLogTableMigration(IMigrationContext context) : base(context)
    {
    }

    // MigrationBase (sync Migrate()) is obsolete, scheduled for removal in Umbraco 18 - this table
    // creation has no genuinely async work, so it's just wrapped in Task.CompletedTask.
    protected override Task MigrateAsync()
    {
        if (TableExists(AddDeletionLogTablePlan.TableName))
        {
            return Task.CompletedTask;
        }

        Create.Table(AddDeletionLogTablePlan.TableName)
            .WithColumn("id").AsInt32().NotNullable().Identity().PrimaryKey()
            .WithColumn("occurredAt").AsDateTime().NotNullable()
            .WithColumn("actionType").AsString(20).NotNullable()
            .WithColumn("performedByUserId").AsInt32().NotNullable()
            .WithColumn("itemCount").AsInt32().NotNullable()
            .WithColumn("totalSizeBytes").AsInt64().NotNullable()
            .WithColumn("items").AsString().Nullable()
            .WithColumn("skippedCount").AsInt32().NotNullable()
            .Do();

        return Task.CompletedTask;
    }
}
