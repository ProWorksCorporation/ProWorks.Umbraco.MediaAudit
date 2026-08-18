# Quickstart: Validating the Media Usage Audit Dashboard

**Feature**: [spec.md](./spec.md) | **Contracts**: [contracts/media-audit-api.md](./contracts/media-audit-api.md) | **Data model**: [data-model.md](./data-model.md)

This is a runnable validation guide, not implementation documentation — it proves the feature works
end-to-end against the acceptance scenarios in spec.md. Implementation steps belong in `tasks.md`.

## Prerequisites

- .NET 10 SDK installed (research.md §1)
- A local Umbraco v17 site (the sample project under `src/`) with the media-audit package project
  referenced
- Node.js + the package's `Client/` dependencies installed, for building the Vite/Lit backoffice
  assets during development
- A seeded local Umbraco SQLite database containing:
  - A mix of media items referenced via Media Picker, Rich Text Editor, and inside Block List blocks
  - At least one media item referenced only by draft (unpublished) content
  - At least one media item referenced only by a Member property (to validate FR-002's Member-data
    exclusion — it MUST show as "Unused")
  - A second configured language, with at least one media item referenced only in the non-default
    language's variant of a content item (to validate FR-017)
  - At least one genuinely unreferenced media item
  - Enough content/media volume to sanity-check performance at scale (SC-002 targets 10,000 items;
    a smaller seeded set is fine for functional validation, with a separate load-test pass for SC-002)
- Two backoffice user accounts: one Administrator, one non-admin user with Media section access

## Setup

```powershell
# From the sample Umbraco site project
dotnet run --project src/ProWorks.Umbraco.MediaAudit.Web

# In a separate terminal, for backoffice asset development
cd src/ProWorks.Umbraco.MediaAudit/Client
npm install
npm run dev   # or: npm run build, for a production-equivalent bundle
```

Log into the backoffice, open the **Media** section, and select the **Media Audit** dashboard.

## Validation scenarios

Each scenario references its source acceptance criteria so a failure points back to the exact
requirement.

### 1. Unused list (User Story 1 / FR-002, FR-006, FR-011, SC-001)

1. Click **Run Audit**. Confirm a progress indicator is shown until results are ready.
2. Confirm the "Unused" list shows only the genuinely unreferenced seeded item(s) — none of the
   referenced items appear.
3. Select an unused item; confirm name, type, size, folder, and last-modified date are all shown.
4. Time from dashboard load to being able to answer "is item X used?" — should be under 10 seconds
   (SC-001) for the seeded dataset.

### 2. Usage detail (User Story 2 / FR-004, FR-005)

1. Select the media item referenced only by **draft** content. Confirm it is classified "Used" (not
   "Unused") — this validates the FR-004 resolution in research.md §4.
2. Open its usage detail. Confirm the draft content item is listed, marked as `Draft`, with a working
   link that navigates to it.
3. Select the media item referenced inside a Block List block. Confirm the referencing content item
   is listed (validates the Block List/Block Grid native-relation coverage in research.md §4).
4. Select the media item referenced only in the non-default language variant. Confirm it is
   classified "Used," and its usage detail shows the correct `culture` (FR-017, research.md §8).
5. Select the media item referenced only by a Member property. Confirm it is classified "Unused" —
   this is correct, expected behavior (FR-002, research.md §9), not a bug.

### 3. Filter, sort, export (User Story 3 / FR-007, FR-008, FR-009)

1. Filter to "Unused", sort by size descending. Confirm ordering and filtering are both correct.
2. Export the current filtered/sorted view. Open the exported file and confirm its rows and column
   values match what was on screen, in the same order.

### 4. Access control (FR-013, FR-015)

1. Log in as the non-admin user. Confirm the dashboard and its audit results are visible, but no
   delete control is shown or reachable.
2. Log in as the Administrator. Confirm the delete control is visible for "Unused" items.

### 5. Safe delete (FR-014, data-model.md validation rules, contracts §POST /delete)

1. As Administrator, select one or more "Unused" items and delete, confirming the confirmation step
   appears first.
2. Confirm deleted items move to the Umbraco Recycle Bin (research.md §5), not a permanent hard
   delete.
3. Reproduce the race-condition edge case: reference a currently-"Unused" item from new content
   *without* re-running the audit, then attempt to delete it. Confirm the API's fresh per-item
   re-check (contracts §POST /delete) skips it with `reason: "NowReferenced"` instead of deleting it.

### 6. Purge / reclaim space (FR-018, contracts §POST /purge)

1. As Administrator, after step 5's delete, open the purge action for one of the just-deleted items.
   Confirm it requires its own, distinctly-worded confirmation (separate from the delete confirmation
   in scenario 5).
2. Confirm the purge permanently removes the item (verify it is gone from the Recycle Bin, not just
   restorable).
3. As non-admin, confirm the purge control is not visible/reachable (same access rule as delete,
   FR-015).
4. Reproduce the edge case: restore a just-deleted item from the Recycle Bin via the standard Media
   section, then attempt to purge it from this dashboard. Confirm the API skips it with
   `reason: "NotTrashed"` rather than purging it or erroring the whole batch.

### 7. Deletion log (FR-019, contracts §GET /deletion-log)

1. After performing the delete in scenario 5 and the purge in scenario 6, open the deletion log view.
2. Confirm there are exactly **two** entries (one `Delete`, one `Purge`) — not one entry per file —
   each showing the acting administrator, timestamp, item count, and total size.
3. Confirm the entry from scenario 5's skipped item shows `skippedCount: 1` rather than omitting the
   skip silently.

### 8. Cross-validation against the reference script (optional but recommended)

Run the original Python reference implementation against the same seeded SQLite database:

```bash
python specs/001-media-usage-audit/reference/media_audit.py --db "path/to/seeded/Umbraco.sqlite.db"
```

Compare its unreferenced-file count/list to the dashboard's "Unused" list. Differences are expected
only where research.md predicts them:
- §4 — an item referenced solely via a property editor the script's substring scan catches but native
  relations might miss, or vice versa.
- §9 — an item referenced only by Member data: the dashboard classifies it "Unused" (Member data is
  out of scope); the script also never scanned Members, so both should agree here.
- Any item referenced only via a non-default-language variant: the script's raw text-blob search
  doesn't distinguish cultures at all, so it may happen to still find the reference; this is not proof
  the dashboard's variant-aware scan (§8) is correct or incorrect on its own — verify that case
  directly per quickstart scenario 2, step 4, not via this comparison.

Any *other, unexplained* discrepancy is a bug, not a design difference.
