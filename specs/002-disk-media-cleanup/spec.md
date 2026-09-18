# Feature Specification: Disk-Only Media Cleanup

**Feature Branch**: `002-disk-media-cleanup`

**Created**: 2026-09-18

**Status**: Draft

**Input**: User description: "Add a feature to the audit section with a button to audit media on
disk that isn't in Umbraco. List all the URLs of the media items on disk, and allow people to
delete them off disk by selecting one/multiple or deleting all of them in one swoop."

## User Scenarios & Testing *(mandatory)*

### User Story 1 - See what's on disk but not in Umbraco (Priority: P1)

An administrator suspects the media folder on the server has accumulated files that were never
properly imported into Umbraco (leftover uploads, files copied in during a migration, orphaned
temp files, etc.). From the Media Audit dashboard they click a "Scan Disk Media" button and see a
list of every file physically present in the media storage location that has no corresponding
Umbraco media item, each with its URL/relative path, size, and last-modified date.

**Why this priority**: Without this, disk clutter is invisible - the existing audit only reasons
about media items Umbraco already knows about (Used/Unused). This closes that gap and is useless
without a listing, so it is the MVP.

**Independent Test**: Can be fully tested by placing a file directly in the media storage location
(bypassing Umbraco), clicking "Scan Disk Media", and confirming that file appears in the resulting
list with a correct URL, size, and last-modified date, while files that do belong to real Umbraco
media items do not appear.

**Acceptance Scenarios**:

1. **Given** the media storage location contains a file that was never uploaded through Umbraco,
   **When** an administrator runs a disk scan, **Then** that file is listed with its relative
   URL, size, and last-modified date.
2. **Given** every file in the media storage location corresponds to a known Umbraco media item,
   **When** an administrator runs a disk scan, **Then** the resulting list is empty.
3. **Given** a disk scan is currently running, **When** the administrator looks at the dashboard,
   **Then** a progress indicator is shown and the button is disabled until the scan completes.

---

### User Story 2 - Delete selected disk-only files (Priority: P2)

Having seen the list, an administrator selects one or more specific files they recognize as junk
and removes them from disk in a single confirmed action.

**Why this priority**: Listing alone still leaves manual cleanup (FTP/RDP access) as the only way
to act on the findings; this is what makes the audit actionable for the common case of removing a
handful of known-junk files.

**Independent Test**: Can be fully tested by selecting a subset of listed files, confirming the
delete, and verifying only the selected files are removed from disk and disappear from the list on
the next scan, while unselected files remain untouched.

**Acceptance Scenarios**:

1. **Given** one or more disk-only files are selected, **When** the administrator clicks
   "Delete Selected" and confirms, **Then** exactly those files are removed from disk and no longer
   appear in the list.
2. **Given** no files are selected, **When** the administrator looks at the toolbar, **Then**
   "Delete Selected" is disabled.
3. **Given** a selected file was already removed by some other process since the scan ran,
   **When** the administrator confirms the delete, **Then** that file is reported as skipped
   rather than causing the whole action to fail.

---

### User Story 3 - Delete all disk-only files in one action (Priority: P3)

An administrator who trusts the scan wants to clear out every disk-only file at once, without
selecting each one individually.

**Why this priority**: A convenience on top of User Story 2 for the common "just clean it all up"
case; not required for the feature to deliver value, so it is lower priority than being able to
review and select first.

**Independent Test**: Can be fully tested by running a scan that finds several disk-only files,
clicking "Delete All", confirming, and verifying every one of them is removed from disk and a
re-scan returns an empty list.

**Acceptance Scenarios**:

1. **Given** a completed disk scan lists one or more files, **When** the administrator clicks
   "Delete All" and confirms, **Then** every listed file is removed from disk.
2. **Given** the disk scan list is empty, **When** the administrator looks at the toolbar,
   **Then** "Delete All" is disabled.

### Edge Cases

- What happens when the configured media storage location cannot be reached (misconfigured,
  permissions error, missing directory)? The scan MUST fail visibly with an error message rather
  than silently reporting zero disk-only files.
- What happens when a file that was disk-only at scan time gets a matching Umbraco media item
  created for it (e.g. someone uploads a same-named file through the backoffice) before the
  administrator deletes it? That file MUST be re-checked immediately before deletion and skipped,
  not deleted, if it is no longer disk-only.
- What happens to media that is currently in Umbraco's Recycle Bin? Its underlying file is still a
  legitimate Umbraco-owned file (recoverable) and MUST NOT be listed as disk-only.
- What happens to empty subfolders left behind after their files are deleted? Out of scope for
  this feature (see Assumptions) - only files are removed, never directories.
- What happens with a very large media library (tens of thousands of files)? The scan runs as a
  background operation with the same "Running" progress pattern as the existing usage audit,
  rather than blocking the dashboard.

## Requirements *(mandatory)*

### Functional Requirements

- **FR-001**: System MUST provide a way, from the Media Audit dashboard, for an administrator to
  trigger a scan of the physical media storage location.
- **FR-002**: System MUST identify every file present in the media storage location that is not
  currently owned by any existing Umbraco media item - whether that item is active or sitting in
  the Recycle Bin.
- **FR-003**: System MUST list each such disk-only file with, at minimum, its relative path/URL,
  file size, and last-modified date.
- **FR-004**: System MUST show a progress indicator while a scan is running and prevent starting a
  second scan concurrently, matching the existing usage-audit run's behavior.
- **FR-005**: Users MUST be able to select one or more listed disk-only files and delete just those
  from disk.
- **FR-006**: Users MUST be able to delete every currently-listed disk-only file in one action
  without selecting each individually.
- **FR-007**: System MUST require an explicit confirmation step before any disk-only delete action
  executes, and MUST make clear that this deletion is permanent (no Recycle Bin exists for files
  that were never Umbraco media items).
- **FR-008**: System MUST re-verify, immediately before deleting each file, that it is still
  present on disk and still has no corresponding Umbraco media item; a file that no longer meets
  both conditions MUST be skipped and reported rather than causing the batch to fail.
- **FR-009**: Disk-only delete actions MUST be restricted to administrators, consistent with the
  existing Delete/Purge actions in this package.
- **FR-010**: Every disk-only delete action (selected or all) MUST be recorded in the existing
  Deletion Log, distinguishable from the existing Delete/Purge action types.
- **FR-011**: System MUST NOT delete directories - only individual files - and MUST NOT modify or
  delete any file that does correspond to an existing Umbraco media item.

### Key Entities

- **Disk Media Item**: One file found in the media storage location during a scan that has no
  matching Umbraco media item. Attributes: relative path, URL, size in bytes, last-modified date.
- **Disk Audit Run**: One execution of the disk scan. Attributes: status (running/complete/failed),
  when it ran, how many files were scanned in total, how many were found disk-only, their combined
  size, how long it took, and an error message if it failed. Mirrors the existing Audit Run entity
  for the usage audit.

## Success Criteria *(mandatory)*

### Measurable Outcomes

- **SC-001**: An administrator can discover every file on disk that Umbraco doesn't know about in
  a single scan action, with no need for direct server/file-system access.
- **SC-002**: An administrator can remove one, several, or all discovered disk-only files in a
  single confirmed action, without touching files that belong to real Umbraco media items.
- **SC-003**: Zero files that correspond to an existing Umbraco media item (active or trashed) are
  ever deleted by this feature.
- **SC-004**: 100% of disk-only delete actions appear in the Deletion Log afterward.

## Assumptions

- The "media storage location" is whatever Umbraco's own configured media file system points at
  for this installation (local disk by default) - this feature audits that file system through
  Umbraco's own file-system abstraction rather than assuming a specific physical folder path, so it
  keeps working if a site is later configured to use a different storage provider.
- Only files are audited/deleted; empty directories left behind are not cleaned up automatically in
  this version.
- A file's "ownership" by an Umbraco media item is determined the same way the existing package
  already resolves a media item's stored file path (its `umbracoFile` value, including the
  image-cropper JSON shape), so any file path a media item currently claims - trashed or not - is
  never considered disk-only.
- Non-admin users can trigger a scan and view the list (read-only, consistent with viewing the
  existing Used/Unused audit), but only administrators can delete disk-only files, consistent with
  the existing Delete/Purge actions.
