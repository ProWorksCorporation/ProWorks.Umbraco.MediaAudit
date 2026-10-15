# ProWorks Umbraco Media Audit

This package audits your Umbraco CMS (v18+) media library right from the backoffice — classifying
every item as Used or Unused, showing exactly which pages reference it, and giving administrators
safe, auditable tools to clean up the rest.

## Key Features

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
- **Disk-only media cleanup**: find files sitting in the media folder that no Umbraco media item owns
  (left over from migrations, failed uploads, or manual copies), export them to CSV, and delete
  selected files or all of them. Folders left empty by a delete are removed too.

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
6. Click **Scan Disk Media** to list files on disk that Umbraco doesn't know about. Administrators
   can delete them from the same panel.

Anyone with Media section access can view the audit and the disk scan. Deleting and purging are
restricted to administrators.

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

The disk scan walks Umbraco's configured media file system (so it works with any storage provider)
and compares every file against the stored file path of every media item, including items in the
Recycle Bin, which still own their file until purged. Each delete re-checks that the file is still
present and still unowned, is permanent (these files were never in Umbraco, so there's no Recycle
Bin), and is recorded in the deletion log.

Member data (profile photos, etc.) is intentionally out of scope for this version — only document
content is scanned.

## Contributing

Working on the package itself (running the sample site, tests, or spec-kit docs)? See
[CONTRIBUTING.md](CONTRIBUTING.md).

## License

MIT — see [LICENSE](LICENSE).
