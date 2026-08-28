using Asp.Versioning;
using Microsoft.AspNetCore.Mvc.ApiExplorer;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi;
using Swashbuckle.AspNetCore.SwaggerGen;
using Umbraco.Cms.Core.Composing;
using Umbraco.Cms.Core.DependencyInjection;
using Umbraco.Cms.Api.Management.OpenApi;
using Umbraco.Cms.Api.Common.OpenApi;
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

            builder.Services.AddSingleton<IOperationIdHandler, CustomOperationHandler>();

            builder.Services.Configure<SwaggerGenOptions>(opt =>
            {
                opt.SwaggerDoc(Constants.ApiName, new OpenApiInfo
                {
                    Title = "ProWorks Umbraco Media Audit Backoffice API",
                    Version = "1.0",
                });

                opt.OperationFilter<UmbracoMediaAuditOperationSecurityFilter>();
            });
        }

        public class UmbracoMediaAuditOperationSecurityFilter : BackOfficeSecurityRequirementsOperationFilterBase
        {
            protected override string ApiName => Constants.ApiName;
        }

        public class CustomOperationHandler : OperationIdHandler
        {
            public CustomOperationHandler(IOptions<ApiVersioningOptions> apiVersioningOptions) : base(apiVersioningOptions)
            {
            }

            protected override bool CanHandle(ApiDescription apiDescription, ControllerActionDescriptor controllerActionDescriptor)
            {
                return controllerActionDescriptor.ControllerTypeInfo.Namespace?.StartsWith("ProWorks.Umbraco.MediaAudit.Controllers", comparisonType: StringComparison.InvariantCultureIgnoreCase) is true;
            }

            public override string Handle(ApiDescription apiDescription) => $"{apiDescription.ActionDescriptor.RouteValues["action"]}";
        }
    }
}
