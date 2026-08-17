// Hand-written fetch wrapper for contracts/media-audit-api.md - deliberately not the generated
// openapi-ts client (research.md keeps this simple/self-contained rather than depending on
// regenerating a client against a live swagger doc during development).

// Confirmed against the running site's actual swagger.json - matches contracts/media-audit-api.md
// exactly. Derived from `[BackOfficeRoute("media-audit/api/v{version:apiVersion}")]` on
// UmbracoMediaAuditApiControllerBase.
const API_BASE = "/umbraco/media-audit/api/v1";

export type MediaUsageStatus = "Used" | "Unused";
export type MediaDetectionSource = "None" | "Relation" | "Scan" | "Both";
export type AuditRunStatus = "Running" | "Complete" | "Failed";
export type ContentPublishState = "Draft" | "Published";
export type MediaUsageDetectionSource = "Relation" | "Scan";

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
  mediaTypeAlias: string;
  extension: string | null;
  sizeBytes: number | null;
  path: string;
  folderId: number | null;
  createDate: string;
  updateDate: string;
  usageStatus: MediaUsageStatus;
  usageCount: number;
  detectionSource: MediaDetectionSource;
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

async function request<T>(path: string, init?: RequestInit): Promise<T> {
  const response = await fetch(`${API_BASE}${path}`, {
    credentials: "include",
    headers: { "Content-Type": "application/json" },
    ...init,
  });

  if (!response.ok) {
    throw new Error(`Media Audit API request failed: ${init?.method ?? "GET"} ${path} -> ${response.status}`);
  }

  // 202 Accepted / 204 No Content responses may have no body.
  const text = await response.text();
  return text ? (JSON.parse(text) as T) : (undefined as T);
}

export const MediaAuditRepository = {
  getSummary: () => request<AuditRun>("/summary"),

  runAudit: () => request<AuditRun>("/run", { method: "POST" }),

  getItems: (status?: MediaUsageStatus) => {
    const query = status ? `?status=${encodeURIComponent(status)}` : "";
    return request<MediaAuditItemsResponse>(`/items${query}`);
  },

  getUsages: (mediaKey: string) =>
    request<{ mediaKey: string; usages: MediaUsageReference[] }>(`/items/${mediaKey}/usages`),
};
