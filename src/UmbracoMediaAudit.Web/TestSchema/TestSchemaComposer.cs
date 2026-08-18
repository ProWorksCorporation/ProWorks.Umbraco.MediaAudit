using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Notifications;

namespace UmbracoMediaAudit.Web.TestSchema;

/// <summary>Registers <see cref="TestSchemaSeeder"/> - sample-site-only, see its own doc comment.</summary>
public class TestSchemaComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        builder.AddNotificationHandler<UmbracoApplicationStartedNotification, TestSchemaSeeder>();
    }
}
