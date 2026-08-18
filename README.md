# Umbraco Media Audit

A backoffice dashboard for Umbraco v17 that audits every item in the media library, tells you
exactly which pages reference each "Used" item, and lets an administrator safely clean up the rest —
with a batch-level accountability log for every delete and purge.

## Why

Media libraries accumulate cruft: old campaign images, superseded PDFs, test uploads nobody remembered
to remove. Finding out what's actually still referenced usually means grepping a database export or
just guessing. This package answers that question from inside the backoffice, using Umbraco's own
reference-tracking as the source of truth, and turns the answer into an actionable, auditable cleanup
workflow.

## Features

- **Used / Unused classification** for every media item — images, documents, files — based on
  Umbraco's own content-relation tracking, checked across every configured language.
- **Usage drill-down**: for any "Used" item, see exactly which content items reference it, with a
  direct link to open each one (and to open the media item itself).
- **Filter, sort, and export**: narrow by status, media type, or folder; sort by name, size, or last
  modified; export the current view to CSV, with the referencing page names included for "Used" rows.
- **Safe delete and purge**: move confirmed-unused items to the Recycle Bin, or permanently purge
  already-trashed items to reclaim storage — each gated by a fresh, immediate re-check so nothing
  that's become referenced (or already been restored) since the last audit gets touched.
- **Deletion log**: one accountability entry per delete/purge *batch* (who, when, how many items, how
  much space), not one entry per file, so it stays useful even after a large cleanup.

## Requirements

- Umbraco CMS 17.x
- .NET 10

## Installation

```powershell
dotnet add package ProWorks.Umbraco.MediaAudit
```

Build your site as usual. The package is a self-contained Razor Class Library — installing it is
enough to register the dashboard, its API, and its backoffice assets; no manual file copying or
`App_Plugins` setup required.

## Getting started

1. Log into the Umbraco backoffice and open the **Media** section.
2. Select the **Media Audit** dashboard.
3. Click **Run Audit**. Results appear once the scan completes; a progress indicator is shown for
   larger libraries.
4. Select any item to see its details. For a "Used" item, this includes every page that references
   it, each linking directly to that content item.
5. Administrators additionally see delete/purge controls on "Unused" items, and can review the
   deletion log for a history of past cleanup actions.

Anyone with Media section access can view the audit. Deleting and purging are restricted to
administrators.

## How it works

Classification runs primarily against Umbraco's own relation-tracking (`IRelationService` /
`IDataValueReference`) — the same mechanism Umbraco itself uses to know what a piece of content
references, so it's fast enough to audit an entire library in one pass and inherently correct for
every native property editor (Media Picker, Rich Text, Block List/Grid, and anything else that
implements the standard reference-tracking interface).

Before any delete or purge actually executes, that item gets an additional, editor-agnostic scan pass
as a safety net — because a delete is destructive, it deserves more certainty than the fast primary
signal alone. A media item that lives inside a folder which is itself referenced (e.g. a
gallery/slideshow block that picks the folder rather than each file) is treated as used too.

Member data (profile photos, etc.) is intentionally out of scope for this version — only document
content is scanned.

## Local development

This repo's own local Umbraco sample site, spec-kit documents, and manual test fixtures live under
`src/` and `specs/001-media-usage-audit/`. See
[specs/001-media-usage-audit/quickstart.md](specs/001-media-usage-audit/quickstart.md) for the full
local setup and validation walkthrough.

```powershell
# Sample site
dotnet run --project src/ProWorks.Umbraco.MediaAudit.Web

# Backoffice client assets (separate terminal)
cd src/ProWorks.Umbraco.MediaAudit/Client
npm install
npm run dev
```

## License

MIT — see [LICENSE](LICENSE).
