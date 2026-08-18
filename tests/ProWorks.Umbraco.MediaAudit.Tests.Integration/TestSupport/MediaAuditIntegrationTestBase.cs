using Microsoft.Extensions.Configuration;
using NUnit.Framework;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Infrastructure.Install;
using Umbraco.Cms.Tests.Common.Testing;
using Umbraco.Cms.Tests.Integration.Testing;
using ProWorks.Umbraco.MediaAudit.Composers;

namespace ProWorks.Umbraco.MediaAudit.Tests.Integration.TestSupport;

/// <summary>
/// Shared base for this suite's UmbracoIntegrationTest fixtures. Umbraco.Cms.Tests.Integration ships
/// its own appsettings.Tests.json as a NuGet contentFiles item declaring the SQLite test-database
/// provider (Tests:Database:DatabaseType) - copying that file into this project (see the .csproj)
/// still left TestDatabaseFactory.Create throwing "Unsupported test database provider", so the value
/// isn't reliably reaching IConfiguration via the file-based route in this setup (likely a working-
/// directory/host-timing difference under `dotnet test`, not investigated further). Setting it
/// in-memory here - a later-added, always-authoritative config source - sidesteps that entirely
/// rather than depending on exactly where/when the JSON file gets discovered.
/// </summary>
public abstract class MediaAuditIntegrationTestBase : UmbracoIntegrationTest
{
    protected override void SetUpTestConfiguration(IConfigurationBuilder configBuilder)
    {
        base.SetUpTestConfiguration(configBuilder);

        configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["Tests:Database:DatabaseType"] = "Sqlite",
            // Diagnostic: the default (PrepareThreadCount: 4, SchemaDatabaseCount: 4) pool warm-up
            // hangs indefinitely on this machine before any test code runs - 0% disk I/O, ~0% CPU,
            // so it's blocked, not just slow. Forcing single-threaded/single-database prep to rule out
            // a Windows-specific threading/locking issue in the pool warm-up itself.
            ["Tests:Database:PrepareThreadCount"] = "1",
            ["Tests:Database:SchemaDatabaseCount"] = "1",
            ["Tests:Database:EmptyDatabasesCount"] = "0",
        });
    }

    /// <summary>
    /// The test host's TypeLoader doesn't auto-discover IComposer implementations from this
    /// package's assembly the way a real site boot does (confirmed: every service/migration this
    /// package registers was missing from DI until this ran) - invoke the package's own composer
    /// directly rather than re-declaring its registrations by hand a second time here, so the two
    /// can't drift out of sync.
    /// </summary>
    protected override void CustomTestSetup(IUmbracoBuilder builder)
    {
        base.CustomTestSetup(builder);
        new UmbracoMediaAuditApiComposer().Compose(builder);
    }

    /// <summary>
    /// Package migrations (AddDeletionLogTablePlan) don't run automatically at this boot level the
    /// way they do on a real site's application-started lifecycle (confirmed: every DeletionLogService
    /// call failed with "no such table: UmbracoMediaAuditDeletionLog" without this) - run them
    /// explicitly. NUnit runs every [SetUp]-attributed method up the class hierarchy, base-first, so
    /// this always runs after UmbracoIntegrationTest's own Setup() has finished creating the database.
    /// </summary>
    [SetUp]
    public async Task RunPendingPackageMigrationsForThisPackage()
    {
        var runner = GetRequiredService<PackageMigrationRunner>();
        await runner.RunPendingPackageMigrations("ProWorks.Umbraco.MediaAudit");
    }
}
