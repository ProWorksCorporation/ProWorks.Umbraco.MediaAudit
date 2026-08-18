using System.Diagnostics;
using NUnit.Framework;
using Umbraco.Cms.Core.Models;
using Umbraco.Cms.Core.Services;
using Umbraco.Cms.Tests.Common.Testing;
using UmbracoMediaAudit.Models;
using UmbracoMediaAudit.Services;
using UmbracoMediaAudit.Tests.Integration.TestSupport;
using CoreConstants = Umbraco.Cms.Core.Constants;

namespace UmbracoMediaAudit.Tests.Integration;

/// <summary>
/// T054 (SC-002): confirm a ~10,000-item media library audits in well under 60 seconds, against a
/// real Umbraco + SQLite instance (the same harness T057's functional integration tests use), not
/// a mocked estimate. The 60s bound applies to the audit itself, not the one-time seeding step
/// below - seeding uses IMediaService.Save's bulk overload (one transaction for all 10,000 items)
/// rather than 10,000 individual CreateMediaWithIdentity calls, which would each open/commit their
/// own scope and dominate the test's wall-clock for no reason relevant to what's being measured.
/// </summary>
[TestFixture]
[UmbracoTest(Database = UmbracoTestOptions.Database.NewSchemaPerTest)]
public class PerformanceTests : MediaAuditIntegrationTestBase
{
    private const int ItemCount = 10_000;
    private const int SC002LimitSeconds = 60;

    private IMediaService MediaService => GetRequiredService<IMediaService>();
    private IMediaAuditService AuditService => GetRequiredService<IMediaAuditService>();

    [Test]
    public async Task Audit_of_10000_media_items_completes_within_the_SC002_60_second_budget()
    {
        var seedStopwatch = Stopwatch.StartNew();
        var batch = new List<IMedia>(ItemCount);
        for (var i = 0; i < ItemCount; i++)
        {
            batch.Add(MediaService.CreateMedia($"perf-item-{i}.jpg", -1, CoreConstants.Conventions.MediaTypes.Image));
        }

        MediaService.Save(batch, -1);
        seedStopwatch.Stop();
        TestContext.Out.WriteLine($"Seeded {ItemCount} media items in {seedStopwatch.Elapsed.TotalSeconds:F1}s (not part of the SC-002 budget).");

        var auditStopwatch = Stopwatch.StartNew();
        await AuditService.RunAuditAsync();

        AuditRun run;
        var attempts = 0;
        do
        {
            run = AuditService.GetCurrentAudit();
            if (run.Status != AuditRunStatus.Running)
            {
                break;
            }

            await Task.Delay(200);
        } while (++attempts < 600); // up to 120s of polling headroom before the test gives up waiting
        auditStopwatch.Stop();

        TestContext.Out.WriteLine($"Audit of {run.TotalScanned} items completed in {auditStopwatch.Elapsed.TotalSeconds:F1}s (SC-002 budget: {SC002LimitSeconds}s). Status: {run.Status}.");

        Assert.That(run.Status, Is.EqualTo(AuditRunStatus.Complete), $"Audit did not complete cleanly: {run.ErrorMessage}");
        Assert.That(run.TotalScanned, Is.EqualTo(ItemCount));
        Assert.That(
            auditStopwatch.Elapsed.TotalSeconds,
            Is.LessThanOrEqualTo(SC002LimitSeconds),
            $"SC-002 requires a {ItemCount}-item audit to complete within {SC002LimitSeconds}s; took {auditStopwatch.Elapsed.TotalSeconds:F1}s.");
    }
}
