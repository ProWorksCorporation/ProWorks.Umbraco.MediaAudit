using Asp.Versioning;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using UmbracoMediaAudit.Models;
using UmbracoMediaAudit.Services;

namespace UmbracoMediaAudit.Controllers
{
    /// <summary>Implements contracts/media-audit-api.md.</summary>
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "UmbracoMediaAudit")]
    public class UmbracoMediaAuditApiController : UmbracoMediaAuditApiControllerBase
    {
        private readonly IMediaAuditService _auditService;

        public UmbracoMediaAuditApiController(IMediaAuditService auditService)
        {
            _auditService = auditService;
        }

        /// <summary>GET /summary - FR-010, FR-011.</summary>
        [HttpGet("summary")]
        [ProducesResponseType<AuditRun>(StatusCodes.Status200OK)]
        public ActionResult<AuditRun> GetSummary() => Ok(_auditService.GetCurrentAudit());

        /// <summary>POST /run - FR-011. Triggers a background audit run; idempotent-in-intent while one is already Running.</summary>
        [HttpPost("run")]
        [ProducesResponseType<AuditRun>(StatusCodes.Status202Accepted)]
        public async Task<ActionResult<AuditRun>> RunAudit(CancellationToken cancellationToken)
        {
            var run = await _auditService.RunAuditAsync(cancellationToken);
            return Accepted(run);
        }

        /// <summary>
        /// GET /items - FR-002, FR-006, FR-007 (status filter only for now; mediaTypeAlias/folderId
        /// filters, sort, and paging are added in User Story 3).
        /// </summary>
        [HttpGet("items")]
        [ProducesResponseType<MediaAuditItemsResponse>(StatusCodes.Status200OK)]
        public ActionResult<MediaAuditItemsResponse> GetItems([FromQuery] MediaUsageStatus? status = null)
        {
            var items = _auditService.GetItems(status);
            return Ok(new MediaAuditItemsResponse
            {
                Page = 1,
                PageSize = items.Count,
                TotalItems = items.Count,
                Items = items,
            });
        }

        /// <summary>
        /// GET /items/{key}/usages - FR-004, FR-005, FR-017. Runs lazily - only for the item an
        /// editor actually opens, not precomputed for every row in GET /items.
        /// </summary>
        [HttpGet("items/{key:guid}/usages")]
        [ProducesResponseType<MediaUsagesResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status404NotFound)]
        public async Task<ActionResult<MediaUsagesResponse>> GetUsages(Guid key, CancellationToken cancellationToken)
        {
            var usages = await _auditService.GetUsagesAsync(key, cancellationToken);
            if (usages is null)
            {
                return NotFound();
            }

            return Ok(new MediaUsagesResponse { MediaKey = key, Usages = usages });
        }
    }

    /// <summary>Response envelope for GET /items (contracts §GET /items).</summary>
    public sealed class MediaAuditItemsResponse
    {
        public required int Page { get; init; }
        public required int PageSize { get; init; }
        public required int TotalItems { get; init; }
        public required IReadOnlyList<MediaAuditItem> Items { get; init; }
    }

    /// <summary>Response envelope for GET /items/{key}/usages (contracts §GET /items/{key}/usages).</summary>
    public sealed class MediaUsagesResponse
    {
        public required Guid MediaKey { get; init; }
        public required IReadOnlyList<MediaUsageReference> Usages { get; init; }
    }
}
