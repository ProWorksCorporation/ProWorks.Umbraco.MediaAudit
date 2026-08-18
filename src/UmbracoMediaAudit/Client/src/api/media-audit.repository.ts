// Hand-written fetch wrapper for contracts/media-audit-api.md - deliberately not the generated
// openapi-ts client (research.md keeps this simple/self-contained rather than depending on
// regenerating a client against a live swagger doc during development).

import type { UmbAuthContext } from "@umbraco-cms/backoffice/auth";

// Confirmed against the running site's actual swagger.json - matches contracts/media-audit-api.md
// exactly. Derived from `[BackOfficeRoute("media-audit/api/v{version:apiVersion}")]` on
// UmbracoMediaAuditApiControllerBase.
const API_BASE = "/umbraco/media-audit/api/v1";

// The backoffice Management API requires a Bearer token per request (cookie-based auth alone
// 401s with "missing_token") - the dashboard element wires this up via setAuthContext() once it
// has consumed UMB_AUTH_CONTEXT, per UmbAuthContext.getOpenApiConfiguration()'s own doc example
// for manual fetch calls.
let authContext: UmbAuthContext | undefined;

export function setAuthContext(context: UmbAuthContext): void {
  authContext = context;
}

/** Thrown by request()/exportCsv() on a non-OK response - carries the HTTP status so callers can distinguish e.g. 403 (not an admin) from other failures. */
export class MediaAuditApiError extends Error {
  constructor(public readonly status: number, message: string) {
    super(message);
    this.name = "MediaAuditApiError";
  }
}

export type MediaUsageStatus = "Used" | "Unused";
export type MediaDetectionSource = "None" | "Relation" | "Scan" | "Both";
export type AuditRunStatus = "Running" | "Complete" | "Failed";
export type ContentPublishState = "Draft" | "Published";
export type MediaUsageDetectionSource = "Relation" | "Scan";
export type MediaAuditSortField = "name" | "sizeBytes" | "updateDate";
export type MediaAuditSortDirection = "asc" | "desc";

export interface AuditRun {
  runAt: string | null;
  totalScanned: number;
  usedCount: number;
  usedSizeBytes: number;
  unusedCount: number;
  unusedSizeBytes: number;
  status: AuditRunStatus;
  durationMs: number | null;
  errorMessage: string | null;
}

export interface MediaAuditItem {
  id: number;
  key: string;
  name: string;
  /** Used for filtering; see mediaTypeName for the human-readable label to display. */
  mediaTypeAlias: string;
  mediaTypeName: string;
  extension: string | null;
  sizeBytes: number | null;
  path: string;
  folderId: number | null;
  createDate: string;
  updateDate: string;
  usageStatus: MediaUsageStatus;
  usageCount: number;
  detectionSource: MediaDetectionSource;
  mediaEditUrl: string;
}

export interface MediaAuditItemsResponse {
  page: number;
  pageSize: number;
  totalItems: number;
  items: MediaAuditItem[];
}

export interface MediaUsageReference {
  contentId: number;
  contentKey: string;
  contentName: string;
  contentTypeAlias: string;
  culture: string | null;
  propertyAlias: string | null;
  publishState: ContentPublishState;
  detectionSource: MediaUsageDetectionSource;
  editUrl: string;
}

export interface MediaFolder {
  id: number;
  name: string;
  path: string;
  parentId: number | null;
}

/** One selectable option for the type filter dropdown - alias for filtering, name for display. */
export interface MediaTypeOption {
  alias: string;
  name: string;
}

export type DeletionLogActionType = "Delete" | "Purge";

/** One item requested for delete/purge but skipped, and why (contracts §POST /delete, §POST /purge). */
export interface MediaActionSkip {
  mediaKey: string;
  /** "NowReferenced" (delete), "NotTrashed" (purge), or "NotFound" (either). */
  reason: string;
}

export interface MediaDeleteResult {
  deleted: string[];
  skipped: MediaActionSkip[];
  logEntryId: number;
}

export interface MediaPurgeResult {
  purged: string[];
  skipped: MediaActionSkip[];
  logEntryId: number;
}

export interface DeletionLogItem {
  key: string;
  name: string;
}

export interface DeletionLogEntry {
  id: number;
  occurredAt: string;
  actionType: DeletionLogActionType;
  performedByUserId: number;
  itemCount: number;
  totalSizeBytes: number;
  items: DeletionLogItem[];
  skippedCount: number;
}

export interface DeletionLogResponse {
  page: number;
  pageSize: number;
  totalItems: number;
  entries: DeletionLogEntry[];
}

/** Shared by GET /items and GET /export (contracts §GET /items, §GET /export). */
export interface MediaAuditItemsQuery {
  status?: MediaUsageStatus;
  mediaTypeAlias?: string;
  folderId?: number;
  sort?: MediaAuditSortField;
  sortDirection?: MediaAuditSortDirection;
  /** Ignored by GET /export. */
  page?: number;
  /** Ignored by GET /export. */
  pageSize?: number;
}

function buildQueryString(query: MediaAuditItemsQuery, includePaging: boolean): string {
  const params = new URLSearchParams();
  if (query.status) params.set("status", query.status);
  if (query.mediaTypeAlias) params.set("mediaTypeAlias", query.mediaTypeAlias);
  if (query.folderId !== undefined) params.set("folderId", String(query.folderId));
  if (query.sort) params.set("sort", query.sort);
  if (query.sortDirection) params.set("sortDirection", query.sortDirection);
  if (includePaging) {
    if (query.page !== undefined) params.set("page", String(query.page));
    if (query.pageSize !== undefined) params.set("pageSize", String(query.pageSize));
  }

  const queryString = params.toString();
  return queryString ? `?${queryString}` : "";
}

async function authHeaders(): Promise<{ headers: Record<string, string>; credentials: RequestCredentials }> {
  const config = authContext?.getOpenApiConfiguration();
  const headers: Record<string, string> = {};
  if (config) {
    const token = await config.token();
    if (token) headers.Authorization = `Bearer ${token}`;
  }
  return { headers, credentials: config?.credentials ?? "include" };
}

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const { headers, credentials } = await authHeaders();
  const response = await fetch(`${API_BASE}${path}`, {
    credentials,
    ...init,
    headers: { "Content-Type": "application/json", ...headers, ...(init?.headers as Record<string, string> | undefined) },
  });

  if (!response.ok) {
    throw new MediaAuditApiError(
      response.status,
      `Media Audit API request failed: ${init?.method ?? "GET"} ${path} -> ${response.status}`
    );
  }

  // 202 Accepted / 204 No Content responses may have no body.
  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}

export const MediaAuditRepository = {
  getSummary: () => request<AuditRun>("/summary"),

  runAudit: () => request<AuditRun>("/run", { method: "POST" }),

  getItems: (query: MediaAuditItemsQuery = {}) =>
    request<MediaAuditItemsResponse>(`/items${buildQueryString(query, true)}`),

  getUsages: (mediaKey: string) =>
    request<{ mediaKey: string; usages: MediaUsageReference[] }>(`/items/${mediaKey}/usages`),

  getFolders: () => request<{ folders: MediaFolder[] }>("/folders"),

  getMediaTypeOptions: () => request<{ mediaTypes: MediaTypeOption[] }>("/media-types"),

  /** Admin-only (403 for non-admins) - FR-014, FR-015. */
  deleteItems: (mediaKeys: string[]) =>
    request<MediaDeleteResult>("/delete", { method: "POST", body: JSON.stringify({ mediaKeys }) }),

  /** Admin-only (403 for non-admins) - FR-018, FR-015. */
  purgeItems: (mediaKeys: string[]) =>
    request<MediaPurgeResult>("/purge", { method: "POST", body: JSON.stringify({ mediaKeys }) }),

  /**
   * Admin-only (403 for non-admins) - FR-019. Also doubles as this backoffice user's admin-status
   * check (see media-audit-dashboard.element.ts) - a successful call here means the caller is an
   * administrator, since the server enforces that already; a MediaAuditApiError with status 403
   * means they aren't. Reuses the server's own authorization rather than re-deriving "is admin"
   * from Umbraco's client-side user context.
   */
  getDeletionLog: (page = 1, pageSize = 50) =>
    request<DeletionLogResponse>(`/deletion-log?page=${page}&pageSize=${pageSize}`),

  /**
   * GET /export returns a CSV file, not JSON - a plain `<a href>` can't carry the Bearer token
   * required (see module doc comment), so this fetches it as a Blob and triggers the download via
   * a synthetic, immediately-discarded anchor element instead.
   */
  async exportCsv(query: MediaAuditItemsQuery = {}): Promise<void> {
    const { headers, credentials } = await authHeaders();
    const response = await fetch(`${API_BASE}/export${buildQueryString(query, false)}`, { credentials, headers });

    if (!response.ok) {
      throw new Error(`Media Audit API request failed: GET /export -> ${response.status}`);
    }

    const blob = await response.blob();
    const url = URL.createObjectURL(blob);
    try {
      const link = document.createElement("a");
      link.href = url;
      link.download = "media-audit-export.csv";
      link.click();
    } finally {
      URL.revokeObjectURL(url);
    }
  },
};
