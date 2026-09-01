using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.OpenApi;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Extensions;
using ProWorks.Umbraco.MediaAudit.Migrations;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Composers
{
    public class UmbracoMediaAuditApiComposer : IComposer
    {
        public void Compose(IUmbracoBuilder builder)
        {
            builder.Services.AddSingleton<IMediaReferenceScanner, MediaReferenceScanner>();
            builder.Services.AddSingleton<IMediaAuditService, MediaAuditService>();

            builder.Services.AddSingleton<IDeletionLogService, DeletionLogService>();
            builder.Services.AddSingleton<IMediaDeleteService, MediaDeleteService>();
            builder.Services.AddSingleton<IMediaPurgeService, MediaPurgeService>();
            builder.PackageMigrationPlans().Add<AddDeletionLogTablePlan>();

            builder.AddBackOfficeOpenApiDocument(Constants.ApiName, document => document
                .WithTitle("ProWorks Umbraco Media Audit Backoffice API")
                .WithBackOfficeAuthentication()
                .ConfigureOpenApiOptions(opt => opt.AddOperationTransformer(new ShortOperationIdTransformer())));
        }

        /// <summary>
        /// Replaces the framework's path-derived operation ID (e.g. "GetItemsByKeyUsages") with the bare
        /// controller action name (e.g. "GetUsages") - this document contains only our own controllers, so
        /// there's nothing to disambiguate against. Keeps the generated TypeScript client's method names
        /// stable/short across changes to a route's shape.
        /// </summary>
        private sealed class ShortOperationIdTransformer : IOpenApiOperationTransformer
        {
            public Task TransformAsync(OpenApiOperation operation, OpenApiOperationTransformerContext context, CancellationToken cancellationToken)
            {
                if (context.Description.ActionDescriptor is ControllerActionDescriptor controllerActionDescriptor)
                {
                    operation.OperationId = controllerActionDescriptor.ActionName;
                }

                return Task.CompletedTask;
            }
        }
    }
}
