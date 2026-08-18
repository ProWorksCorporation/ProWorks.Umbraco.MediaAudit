using System.Text;
using Asp.Versioning;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Umbraco.Cms.Core.Security;
using Umbraco.Cms.Web.Common.Authorization;
using ProWorks.Umbraco.MediaAudit.Models;
using ProWorks.Umbraco.MediaAudit.Services;

namespace ProWorks.Umbraco.MediaAudit.Controllers
{
    /// <summary>Implements contracts/media-audit-api.md.</summary>
    [ApiVersion("1.0")]
    [ApiExplorerSettings(GroupName = "ProWorks.Umbraco.MediaAudit")]
    public class UmbracoMediaAuditApiController : UmbracoMediaAuditApiControllerBase
    {
        private readonly IMediaAuditService _auditService;
        private readonly IMediaDeleteService _deleteService;
        private readonly IMediaPurgeService _purgeService;
        private readonly IDeletionLogService _deletionLogService;
        private readonly IBackOfficeSecurityAccessor _backOfficeSecurityAccessor;

        public UmbracoMediaAuditApiController(
            IMediaAuditService auditService,
            IMediaDeleteService deleteService,
            IMediaPurgeService purgeService,
            IDeletionLogService deletionLogService,
            IBackOfficeSecurityAccessor backOfficeSecurityAccessor)
        {
            _auditService = auditService;
            _deleteService = deleteService;
            _purgeService = purgeService;
            _deletionLogService = deletionLogService;
            _backOfficeSecurityAccessor = backOfficeSecurityAccessor;
        }

        /// <summary>
        /// The acting backoffice user's id, for DeletionLogEntry.PerformedByUserId - always an
        /// administrator here, since every caller of this reaches it through an admin-only action.
        /// </summary>
        private int CurrentUserId =>
            _backOfficeSecurityAccessor.BackOfficeSecurity?.CurrentUser?.Id
            ?? throw new InvalidOperationException("No authenticated backoffice user for this request.");

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
        /// GET /items - FR-002, FR-006, FR-007, FR-008: status/mediaTypeAlias/folderId filters,
        /// name/sizeBytes/updateDate sort, and paging (contracts §GET /items).
        /// </summary>
        [HttpGet("items")]
        [ProducesResponseType<MediaAuditItemsResponse>(StatusCodes.Status200OK)]
        public ActionResult<MediaAuditItemsResponse> GetItems([FromQuery] MediaAuditItemsQuery query)
        {
            var result = _auditService.GetItems(query);
            return Ok(new MediaAuditItemsResponse
            {
                Page = Math.Max(query.Page, 1),
                PageSize = Math.Clamp(query.PageSize, 1, 200),
                TotalItems = result.TotalItems,
                Items = result.Items,
            });
        }

        /// <summary>
        /// GET /export - FR-009. Same filter/sort as GET /items (contracts §GET /export), no paging,
        /// as a downloadable CSV.
        /// </summary>
        [HttpGet("export")]
        public IActionResult Export([FromQuery] MediaAuditItemsQuery query)
        {
            var items = _auditService.GetExportItems(query);
            var csv = BuildCsv(items);
            return File(Encoding.UTF8.GetBytes(csv), "text/csv", "media-audit-export.csv");
        }

        /// <summary>GET /folders - FR-007, for the folder filter dropdown (data-model.md MediaFolder).</summary>
        [HttpGet("folders")]
        [ProducesResponseType<MediaFoldersResponse>(StatusCodes.Status200OK)]
        public ActionResult<MediaFoldersResponse> GetFolders() =>
            Ok(new MediaFoldersResponse { Folders = _auditService.GetFolders() });

        /// <summary>GET /media-types - FR-007, for the type filter dropdown. Not in the original contract - added alongside GET /folders for the same reason.</summary>
        [HttpGet("media-types")]
        [ProducesResponseType<MediaTypeOptionsResponse>(StatusCodes.Status200OK)]
        public ActionResult<MediaTypeOptionsResponse> GetMediaTypeOptions() =>
            Ok(new MediaTypeOptionsResponse { MediaTypes = _auditService.GetMediaTypeOptions() });

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

        /// <summary>
        /// POST /delete - FR-014, FR-015, FR-019. Admin-only (T044: layered on top of the base
        /// controller's Media-section-access policy via a second [Authorize] - ASP.NET Core combines
        /// multiple [Authorize] attributes with AND semantics, and returns 403 - not silently omitting
        /// the action - for an authenticated-but-non-admin caller, exactly per FR-015).
        /// </summary>
        [HttpPost("delete")]
        [Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]
        [ProducesResponseType<MediaDeleteResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public async Task<ActionResult<MediaDeleteResult>> Delete([FromBody] MediaKeysRequest request, CancellationToken cancellationToken)
        {
            var result = await _deleteService.DeleteAsync(request.MediaKeys, CurrentUserId, cancellationToken);
            return Ok(result);
        }

        /// <summary>POST /purge - FR-018, FR-015, FR-019. Admin-only (see Delete's doc comment for the authorization approach).</summary>
        [HttpPost("purge")]
        [Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]
        [ProducesResponseType<MediaPurgeResult>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public ActionResult<MediaPurgeResult> Purge([FromBody] MediaKeysRequest request)
        {
            var result = _purgeService.Purge(request.MediaKeys, CurrentUserId);
            return Ok(result);
        }

        /// <summary>GET /deletion-log - FR-019. Admin-only, paged, newest first.</summary>
        [HttpGet("deletion-log")]
        [Authorize(Policy = AuthorizationPolicies.RequireAdminAccess)]
        [ProducesResponseType<DeletionLogResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status403Forbidden)]
        public ActionResult<DeletionLogResponse> GetDeletionLog([FromQuery] int page = 1, [FromQuery] int pageSize = 50)
        {
            var clampedPageSize = Math.Clamp(pageSize, 1, 200);
            var (entries, totalItems) = _deletionLogService.GetPagedHistory(Math.Max(page, 1), clampedPageSize);
            return Ok(new DeletionLogResponse
            {
                Page = Math.Max(page, 1),
                PageSize = clampedPageSize,
                TotalItems = totalItems,
                Entries = entries,
            });
        }

        /// <summary>
        /// Columns match the MediaAuditItem fields used for display (contracts §GET /export), plus
        /// "Used On Pages" - the names of content items referencing a "Used" item, blank for
        /// "Unused" ones (<see cref="IMediaAuditService.GetUsedOnPageNames"/>).
        /// </summary>
        private string BuildCsv(IReadOnlyList<MediaAuditItem> items)
        {
            var sb = new StringBuilder();
            sb.AppendLine("Name,Status,SizeBytes,Type,Folder,CreateDate,UpdateDate,UsedOnPages");
            foreach (var item in items)
            {
                var usedOnPages = item.UsageStatus == MediaUsageStatus.Used
                    ? string.Join("; ", _auditService.GetUsedOnPageNames(item.Id))
                    : "";

                sb.AppendLine(string.Join(",", new[]
                {
                    CsvField(item.Name),
                    CsvField(item.UsageStatus.ToString()),
                    CsvField(item.SizeBytes?.ToString() ?? ""),
                    CsvField(item.MediaTypeName),
                    CsvField(item.Path),
                    CsvField(item.CreateDate.ToString("O")),
                    CsvField(item.UpdateDate.ToString("O")),
                    CsvField(usedOnPages),
                }));
            }

            return sb.ToString();
        }

        private static string CsvField(string value) =>
            value.Contains(',') || value.Contains('"') || value.Contains('\n')
                ? "\"" + value.Replace("\"", "\"\"") + "\""
                : value;
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

    /// <summary>Response envelope for GET /folders.</summary>
    public sealed class MediaFoldersResponse
    {
        public required IReadOnlyList<MediaFolder> Folders { get; init; }
    }

    /// <summary>Response envelope for GET /media-types.</summary>
    public sealed class MediaTypeOptionsResponse
    {
        public required IReadOnlyList<MediaTypeOption> MediaTypes { get; init; }
    }

    /// <summary>Request body shared by POST /delete and POST /purge (contracts §POST /delete, §POST /purge).</summary>
    public sealed class MediaKeysRequest
    {
        public required IReadOnlyList<Guid> MediaKeys { get; init; }
    }

    /// <summary>Response envelope for GET /deletion-log.</summary>
    public sealed class DeletionLogResponse
    {
        public required int Page { get; init; }
        public required int PageSize { get; init; }
        public required int TotalItems { get; init; }
        public required IReadOnlyList<DeletionLogEntry> Entries { get; init; }
    }
}
