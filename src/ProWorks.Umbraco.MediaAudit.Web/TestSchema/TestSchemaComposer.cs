using Microsoft.Extensions.DependencyInjection;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Core.Events;
using Umbraco.Cms.Core.Notifications;

namespace ProWorks.Umbraco.MediaAudit.Web.TestSchema;

/// <summary>Registers <see cref="TestSchemaSeeder"/> - sample-site-only, see its own doc comment.</summary>
public class TestSchemaComposer : IComposer
{
    public void Compose(IUmbracoBuilder builder)
    {
        // AddNotificationHandler<>() only accepts the sync INotificationHandler<> - TestSchemaSeeder needs
        // async service calls, so its INotificationAsyncHandler<> is registered directly instead.
        builder.Services.AddTransient<INotificationAsyncHandler<UmbracoApplicationStartedNotification>, TestSchemaSeeder>();
    }
}
