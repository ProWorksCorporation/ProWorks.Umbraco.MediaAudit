using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Api.Common.Attributes;
using Umbraco.Cms.Web.Common.Authorization;
using Umbraco.Cms.Web.Common.Routing;

namespace ProWorks.Umbraco.MediaAudit.Controllers
{
    /// <summary>
    /// Base route for every Media Audit Management API endpoint (contracts/media-audit-api.md).
    /// FR-013: every endpoint requires authenticated backoffice access with permission to the Media
    /// section - endpoints that additionally require administrator privileges (FR-015) layer their own
    /// stricter check on top (see AdminOnlyAttribute).
    /// </summary>
    [ApiController]
    [BackOfficeRoute("media-audit/api/v{version:apiVersion}")]
    [Authorize(Policy = AuthorizationPolicies.SectionAccessMedia)]
    [MapToApi(Constants.ApiName)]
    public class UmbracoMediaAuditApiControllerBase : ControllerBase
    {
    }
}
