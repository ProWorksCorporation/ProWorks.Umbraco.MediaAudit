import type {
  AuditRun,
  DeletionLogEntry,
  MediaAuditItem,
  MediaUsageReference,
} from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/api/media-audit.repository.js";

let nextId = 1;

export function makeItem(overrides: Partial<MediaAuditItem> = {}): MediaAuditItem {
  const id = nextId++;
  return {
    id,
    key: `11111111-1111-1111-1111-${String(id).padStart(12, "0")}`,
    name: `item-${id}.jpg`,
    mediaTypeAlias: "image",
    mediaTypeName: "Image",
    extension: "jpg",
    sizeBytes: 2048,
    path: "/",
    folderId: null,
    createDate: "2026-01-01T00:00:00Z",
    updateDate: "2026-01-02T00:00:00Z",
    usageStatus: "Unused",
    usageCount: 0,
    detectionSource: "None",
    mediaEditUrl: "/umbraco/media/edit",
    ...overrides,
  };
}

export function makeUsage(overrides: Partial<MediaUsageReference> = {}): MediaUsageReference {
  return {
    contentId: 1,
    contentKey: "22222222-2222-2222-2222-222222222222",
    contentName: "Home",
    contentTypeAlias: "page",
    culture: null,
    propertyAlias: "featuredMedia",
    publishState: "Published",
    detectionSource: "Relation",
    editUrl: "/umbraco/content/edit",
    ...overrides,
  };
}

export function makeAuditRun(overrides: Partial<AuditRun> = {}): AuditRun {
  return {
    runAt: "2026-01-02T00:00:00Z",
    totalScanned: 10,
    usedCount: 6,
    usedSizeBytes: 6144,
    unusedCount: 4,
    unusedSizeBytes: 4096,
    status: "Complete",
    durationMs: 123,
    errorMessage: null,
    ...overrides,
  };
}

export function makeDeletionLogEntry(overrides: Partial<DeletionLogEntry> = {}): DeletionLogEntry {
  return {
    id: 1,
    occurredAt: "2026-01-02T00:00:00Z",
    actionType: "Delete",
    performedByUserId: 7,
    itemCount: 2,
    totalSizeBytes: 4096,
    items: [
      { key: "33333333-3333-3333-3333-333333333333", name: "one.jpg" },
      { key: "44444444-4444-4444-4444-444444444444", name: "two.jpg" },
    ],
    skippedCount: 0,
    ...overrides,
  };
}
