# Feature Specification: Media Usage Audit Dashboard

**Feature Branch**: `001-media-usage-audit`

**Created**: 2026-08-17

**Status**: Draft

**Input**: User description: "We want to build an Umbraco v17 backoffice dashboard that audits unused and used media files."

## Clarifications

### Session 2026-08-17

- Q: When an admin deletes unused media from the dashboard, should it be permanently removed immediately, or moved to Umbraco's Recycle Bin (soft delete, reversible)? → A: Move to Recycle Bin (soft delete, reversible), matching Umbraco's native media-deletion behavior.
- Q: Should usage detection also scan Umbraco Member data (e.g. a member's profile photo via a Media Picker property), or only page/document content? → A: Documents only for this version (MVP); scanning Member data is explicitly out of scope for now but flagged as a candidate future enhancement.
- Q: On a multi-language (variant) Umbraco site, must usage detection check media references across every configured language, or is checking just the default/invariant values enough? → A: All configured languages/cultures must be checked — a media item used in any language variant of any content item counts as "Used."
- Q: Should the system keep a record of what was deleted through the dashboard, by whom, and when? → A: Yes, but as one log entry per delete *action/batch* (timestamp, admin, item count, total size, compact item list) rather than one entry per file, so the log doesn't balloon on large bulk deletes.
- Q: Since moving items to the Recycle Bin doesn't free space until purged, should the dashboard also offer an explicit "empty the recycle bin now" action? → A: Yes — two-step: the default action remains the safe move-to-Recycle-Bin; a separate, more strongly-confirmed admin action can immediately purge (permanently remove) already-soft-deleted items to actually reclaim space on demand.

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See which media files are unused (Priority: P1)

An editor or administrator opens the Media Audit dashboard in the Umbraco backoffice and sees a clear list of media items (images, documents, files) that are not currently referenced anywhere in the site's content, so they can identify candidates for cleanup and, ultimately, reclaim storage space (via the delete and purge actions in FR-014/FR-018).

**Why this priority**: This is the core value proposition of the feature — without a reliable "unused media" view, the dashboard has no reason to exist. It is the minimum viable slice that already delivers value on its own.

**Independent Test**: Can be fully tested by opening the dashboard on a site with a mix of referenced and unreferenced media, and confirming that only truly unreferenced items appear in the "unused" list.

**Acceptance Scenarios**:

1. **Given** a media library containing files that are referenced by published or draft content and files that are not referenced anywhere, **When** the editor opens the Media Audit dashboard, **Then** the dashboard displays a distinct "Unused" list containing only the unreferenced files.
2. **Given** the dashboard is displaying results, **When** the editor selects a media item in the "Unused" list, **Then** the editor can see identifying details (name, type, size, last modified date, folder location) for that item.
3. **Given** a large media library, **When** the audit runs, **Then** the editor sees a loading/progress indicator until results are ready rather than a frozen or blank screen.

---

### User Story 2 - See where a used media file is referenced (Priority: P2)

An editor or administrator selects a media item marked as "Used" and sees the list of content items (pages, elements) that reference it, so they can confirm it is safe to leave in place or track down every place it needs to be replaced before editing it.

**Why this priority**: Knowing *that* something is used is good, but knowing *where* it's used is what turns the audit into an actionable tool rather than just a count. This builds directly on Story 1's data.

**Independent Test**: Can be fully tested by selecting a "Used" media item and confirming the dashboard lists every content item that actually references it, with working links to open each one.

**Acceptance Scenarios**:

1. **Given** a media item referenced by two different pages, **When** the editor views that item's usage details, **Then** both referencing content items are listed, each with a link that navigates to that content item.
2. **Given** a media item referenced only inside a nested content/block-based property, **When** the editor views that item's usage details, **Then** the referencing content item is still listed as a usage location.

---

### User Story 3 - Filter, sort, and export the audit results (Priority: P3)

An administrator narrows the audit results by criteria such as file type, size, folder, or usage status, sorts the list to prioritize the biggest opportunities (e.g., largest unused files first), and exports the results for reporting or sharing with others outside the backoffice.

**Why this priority**: This is a productivity/reporting enhancement on top of the core audit — valuable for larger sites and recurring cleanup workflows, but the feature is already useful without it.

**Independent Test**: Can be fully tested by applying a filter/sort combination and confirming the displayed list updates correctly, and by exporting results and confirming the exported file matches the currently filtered view.

**Acceptance Scenarios**:

1. **Given** a mixed set of audit results, **When** the administrator filters by "Unused" and sorts by file size descending, **Then** the list shows only unused items ordered from largest to smallest.
2. **Given** a filtered/sorted result set, **When** the administrator exports the results, **Then** the exported file contains exactly the rows currently displayed, with their key columns (name, status, size, type, folder).

---

### Edge Cases

- What happens when the media library is very large (tens of thousands of items)? The dashboard must remain responsive and communicate progress rather than timing out silently.
- How does the system treat media referenced only in unpublished/draft content, or only in a previous (non-current) version of a content item?
- How does the system treat media referenced by recently trashed/recycled content, or by content awaiting permanent deletion?
- How does the system treat a media *folder* that contains only unused children — is the folder itself flagged, or only leaf files?
- What happens when a media item is referenced by something the audit cannot detect (e.g., a hard-coded URL in free text, or a third-party/custom integration outside standard property editors)? The system should avoid false confidence and, where feasible, indicate that results reflect detectable references only.
- What happens when the audit is run while content editors are actively making changes? Results should reflect a consistent point-in-time snapshot rather than partially-updated data.
- What happens when a non-administrator with Media section access opens the dashboard? They see full audit results but no delete controls.
- What happens when an administrator attempts to bulk-delete unused media that has become referenced by content since the audit was last run? The system should protect against deleting items that turn out to be in use (e.g., by re-checking status before deletion, or requiring a fresh audit before delete is enabled).
- What happens when a user without sufficient permissions tries to access the dashboard at all (no Media section access)? They should not see the dashboard/menu entry.
- What happens on a multi-language site when a media item is referenced only by a non-default language variant of a content item (e.g. only the Spanish translation, not the English default)? It MUST still be classified "Used" (FR-017), not "Unused."
- What happens if an administrator tries to purge (FR-018) an item that was already restored out of the Recycle Bin by someone else (e.g. via the standard Media section) since it was soft-deleted? The purge action MUST skip and report items no longer in the Recycle Bin rather than erroring the whole batch or purging an unrelated restored item.
- What happens if an audit run fails partway through (e.g., an unexpected error while scanning)? The system MUST mark the run `Failed`, surface a clear error state to the user instead of silently showing stale or partial results, and allow the user to retry.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: The system MUST provide a dashboard, accessible from within the Umbraco v17 backoffice, that displays an audit of media library items.
- **FR-002**: The system MUST classify each media item as either "Used" or "Unused" based on whether it is referenced by any page/document content in the site. Member data (e.g. a Media Picker property on a Member) is explicitly out of scope for this version (see Assumptions) — a media item referenced only by Member data will be classified "Unused."
- **FR-003**: The system MUST detect media references stored in standard built-in Umbraco property editors (e.g., Media Picker, Rich Text Editor, Image Cropper) across all page/document content, including references nested inside block-based and nested-content property values.
- **FR-004**: The system MUST treat a media item as "Used" if it is referenced by at least one piece of content, regardless of whether that content is currently published or only saved as a draft.
- **FR-005**: For each "Used" media item, the system MUST allow the editor to view the specific content item(s) that reference it, with a way to navigate to each referencing content item.
- **FR-006**: For each media item, the system MUST display identifying metadata including name, file type, file size, folder/location, and last modified date.
- **FR-007**: The system MUST allow filtering of audit results by usage status (Used/Unused), and by common attributes such as file type and folder.
- **FR-008**: The system MUST allow sorting of audit results by attributes including name, file size, and last modified date.
- **FR-009**: The system MUST allow exporting the currently filtered/sorted audit results to a shareable file format.
- **FR-010**: The system MUST display a summary of total media items, count/size of used items, and count/size of unused items.
- **FR-011**: The system MUST indicate to the user when an audit is in progress and when results were last refreshed, and MUST allow the user to manually trigger a re-audit.
- **FR-012**: The system MUST remain usable while auditing large media libraries — specifically, the backoffice UI MUST stay interactive (the user can navigate away or continue other work) for the full duration of an audit run, consistent with the audit completing within the SC-002 performance target rather than blocking the request pipeline synchronously.
- **FR-013**: The system MUST restrict access to the dashboard (viewing audit results) to authenticated backoffice users who have permission to access the Media section, relying on Umbraco's existing permission system with no new dedicated permission introduced.
- **FR-014**: The system MUST allow an administrator to select one or more "Unused" media items and delete them directly from the dashboard, with a confirmation step before deletion. Deletion MUST move the selected item(s) to Umbraco's Recycle Bin (soft delete, reversible via Umbraco's standard recycle bin restore), matching Umbraco's native media-deletion behavior, rather than permanently removing them immediately.
- **FR-015**: The system MUST restrict the bulk-delete action (FR-014) and the purge action (FR-018) specifically to users with administrator-level privileges; backoffice users who can view the dashboard but are not administrators MUST be able to see audit results but MUST NOT see or be able to trigger either action.
- **FR-016**: The system MUST clearly communicate that usage detection covers standard, trackable Umbraco content references, and MUST flag results as based on detectable references only, since references embedded in free text, external systems, or non-standard custom property editors may not be detected.
- **FR-017**: On a multi-language (variant) site, the system MUST check media references across every configured language/culture on a content item, not only the default/invariant value; a media item referenced in any language variant of any content item MUST be classified "Used."
- **FR-018**: The system MUST offer a separate administrator-only action to immediately and permanently remove (purge) specific items previously deleted via FR-014 — scoped only to the selected items, not the entire Recycle Bin — for admins who want to reclaim disk space right away rather than waiting for Umbraco's normal recycle-bin cleanup. This purge action MUST require its own, more strongly-worded confirmation step (distinct from the FR-014 confirmation) since it is irreversible.
- **FR-019**: The system MUST record a log entry for every delete action (FR-014) and every purge action (FR-018), each entry capturing: timestamp, the acting administrator, the number of items affected, the total file size affected, and a reference to which items were included. The system MUST record one log entry per action/batch, not one entry per individual item, so the log does not grow unmanageably on large bulk operations.

### Key Entities

- **Media Item**: A file stored in the Umbraco media library (image, document, or other file type). Key attributes: name, file type/extension, file size, folder/location, last modified date, usage status (Used/Unused).
- **Usage Reference**: A link between a Media Item and a piece of content that references it. Key attributes: referencing content item (name, type, id), the property on that content item holding the reference, and the content item's publish state (published/draft).
- **Audit Run**: A point-in-time execution of the media audit. Key attributes: run timestamp, total media items scanned, counts/sizes of used vs. unused items, completion status.
- **Media Folder**: A container for organizing media items within the library, used for filtering and location display.
- **Deletion Log Entry**: A record of one delete or purge action taken from the dashboard (FR-019). Key attributes: timestamp, acting administrator, action type (delete-to-recycle-bin or purge), item count, total size affected, reference to the items included. One entry per action/batch, not per item.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An editor can determine whether a specific media file is used or unused within 10 seconds of opening the dashboard.
- **SC-002**: For a media library of up to 10,000 items, a full audit completes and displays results within 60 seconds.
- **SC-003**: 100% of media references created through standard, supported property editors are correctly identified as "Used" in audit results (zero false "Unused" classifications for detectable references).
- **SC-004**: Administrators report at least a 50% reduction in time spent manually identifying unused media files compared to prior manual review methods.
- **SC-005**: Users can locate the content item(s) referencing a given "Used" media file in 2 or fewer interactions from the dashboard.
- **SC-006**: At least 90% of pilot users rate the dashboard's audit results as trustworthy and actionable in post-use feedback.

## Assumptions

- The dashboard operates on the current state of content and media within a single Umbraco site/installation; multi-site or cross-installation auditing is out of scope for this feature.
- Usage detection scans page/document content only. Umbraco Member data (e.g. a profile-photo Media Picker property on a Member) is explicitly out of scope for this version — a media item referenced only by Member data is classified "Unused." This is a candidate for a future enhancement, not part of this feature's MVP.
- "Usage" is determined by references trackable through Umbraco's standard content relation/tracking mechanisms; references embedded in free-form text, external systems, or fully custom property editors that don't register relations are not guaranteed to be detected.
- Audit results are computed on demand (or on a refresh trigger) rather than continuously live-updated in real time; a manual "re-run audit" action is sufficient to refresh results.
- Users accessing the dashboard already have standard Umbraco backoffice authentication and at least Media section access; the feature relies on Umbraco's existing user/permission system rather than introducing a separate access model.
- Export of audit results produces a common, widely-supported file format (e.g., spreadsheet-compatible) rather than a specialized proprietary format.
- Trashed/recycled content is treated as not currently referencing media (i.e., media referenced only by trashed content is treated as unused), consistent with how Umbraco treats trashed content as removed from the live site.
- **Resolved during planning (see [research.md](./research.md) §4)**: FR-003's nested block-based reference detection is confirmed for Block List/Block Grid — Umbraco's built-in relation-tracking recursively inspects each block's nested property values via source-confirmed behavior. Nested Content (the legacy editor) coverage could not be independently confirmed and is treated as a residual risk, mitigated by a scan-based safety-net layer described in research.md that does not depend on which property editor stored the reference.
- **Resolved during planning (see [research.md](./research.md) §4)**: FR-004's draft-state detection is confirmed — Umbraco records relations when content is saved, not only when published, per official documentation.
