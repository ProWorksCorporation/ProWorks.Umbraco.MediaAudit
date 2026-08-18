# Phase 0 Research: Media Usage Audit Dashboard

**Feature**: [spec.md](./spec.md) | **Constitution**: v1.0.0

This document resolves every `NEEDS CLARIFICATION` in the plan's Technical Context and the two
"pending technical verification" items the spec flagged in its Assumptions section, per the
constitution's Principle II (Documentation-Driven, Verified Assumptions). Each decision below cites
the official documentation and/or Umbraco source consulted, not guesswork.

A reference implementation was supplied by the user: a working Python CLI (`media_audit.py`) that
already audits an Umbraco v10 SQLite/SQL Server database and produces a CSV. It has been copied into
[reference/media_audit.py](./reference/media_audit.py) for traceability. Its proven logic (GUID +
file-path substring matching, checking the "current" content version so drafts are covered) is the
starting point for this feature's detection strategy — the decisions below explain what carries over
directly, what changes for a marketplace-distributed C# package, and why.

---

## 1. Target runtime: .NET version for Umbraco v17

**Decision**: .NET 10 (LTS).

**Rationale**: Umbraco 17 is an LTS release built on .NET 10, itself an LTS release; both are
supported into Q4 2028. This is a hard platform requirement, not a preference.

**Alternatives considered**: None — this is fixed by the target platform (Umbraco v17), not a
project choice.

**Source**: [Umbraco 17 LTS](https://umbraco.com/products/umbraco-cms/umbraco-17/), [Umbraco 17 LTS: Final Release](https://umbraco.com/blog/umbraco-17-lts-release/), [CMS Requirements](https://docs.umbraco.com/umbraco-cms/get-started/installation/requirements)

---

## 2. Package project structure & distribution (marketplace deployable)

**Decision**: A Razor Class Library (RCL) is the shippable package project. Backoffice client code
(TypeScript/Lit) lives in a `Client/` source folder built by Vite with `outDir` pointed at the RCL's
`wwwroot`, so the compiled assets are automatically embedded as static web assets when the RCL is
packed. The extension manifest (`umbraco-package.json`) is authored in the client's `public/` folder
so Vite copies it, unmodified, to `wwwroot/App_Plugins/{PackageName}/umbraco-package.json` where
Umbraco's backoffice auto-discovers it. The NuGet package carries the `umbraco-marketplace` tag and
standard metadata (`Title`, `Description`, `Version`, `Authors`, `PackageProjectUrl`,
`PackageLicenseExpression`) in the `.csproj`, per Marketplace listing requirements.

> **Clarification — the RCL is a delivery container, not an alternative to JS/Lit.** The dashboard,
> detail panel, and delete-confirmation UI are still ordinary Lit web components written in
> TypeScript and built by Vite (§3) — that doesn't change here. What the RCL provides is purely the
> .NET project *type* that carries those built JS/CSS files (in `wwwroot`) alongside this feature's
> C# server code (API controllers, services) as a single, installable unit. The alternative — hand
> -placing built JS files into an `App_Plugins/` folder inside an actual Umbraco *website* project —
> is how you'd customize one specific site directly; it has no `.csproj` to run `dotnet pack` against,
> so there is nothing to publish to NuGet and nothing for a client's site to install. An RCL's
> `wwwroot` is specifically designed to be embedded as ASP.NET Core static web assets on `dotnet pack`,
> so when a client installs this package via NuGet, their site automatically serves this feature's JS
> at `/App_Plugins/UmbracoMediaAudit/...` with no manual file copying. RCL vs. plain `App_Plugins` is a
> "distributable package vs. single-site customization" choice, not a "C#/.NET vs. JavaScript" choice —
> it was fixed by the "marketplace deployable" requirement, not a UI-technology decision.

**Rationale**: This is Umbraco's own documented pattern for a *distributable* package (as opposed to
an in-project-only `App_Plugins` extension), and it's the only structure that survives `dotnet pack`
/ NuGet distribution cleanly. It also satisfies constitution Principle IV (Vite as the sole build
tool) and Principle I (package conventions) directly.

**Alternatives considered**: Plain `App_Plugins` folder with no RCL — rejected; it's the
in-project/dev-only pattern and doesn't package into a distributable NuGet artifact. A separate,
un-embedded static asset host — rejected as unnecessary complexity for a self-contained backoffice
dashboard.

**Source**: [Vite Package Setup (17.latest)](https://docs.umbraco.com/umbraco-cms/17.latest/extend-your-project/backoffice-extensions/development-flow/vite-package-setup.md), [Creating a Package (17.latest)](https://docs.umbraco.com/umbraco-cms/17.latest/extend-your-project/packages/creating-a-package), [Listing a Package on the Umbraco Marketplace](https://docs.umbraco.com/umbraco-cms/extending/packages/listing-on-marketplace)

---

## 3. Backoffice UI implementation

**Decision**: Dashboard, detail panel, and delete-confirmation UI are Lit-based Web Components
registered via `umbraco-package.json` extension entries (`type: "dashboard"` for the main entry,
plus supporting element/modal extensions), built in TypeScript, styled with Umbraco UI Library (UUI)
components and design tokens, bundled by Vite in library mode with `@umbraco-cms/*` externalized.

**Rationale**: Directly mandated by constitution Principle III; also the documented, supported path
for Umbraco 17's backoffice (which is itself built this way), giving native look-and-feel and
compatibility with the extension registry, permission-gating APIs, and routing.

**Alternatives considered**: None evaluated — precluded by the constitution.

**Source**: [Umbraco 17 Backoffice Extensions: TypeScript + Lit + Vite](https://medium.com/@girish.sasikumar.kl33/umbraco-17-backoffice-extensions-for-beginners-typescript-lit-vite-and-the-easy-alternatives-4dd6e2f5c429), [Creating your First Extension](https://docs.umbraco.com/umbraco-cms/tutorials/creating-your-first-extension)

---

## 4. Media usage detection strategy (resolves spec Assumptions: FR-003, FR-004 pending verification)

**Decision — hybrid, native-first with a scan-based safety net**:

- **Primary signal**: Umbraco's built-in Tracked References system (`IRelationService` /
  `IDataValueReference`). Relations are created **when content is saved** (not only on publish), and
  — confirmed directly from source — `BlockValuePropertyValueEditorBase.GetBlockValueReferences()`
  iterates every block's `ContentData` **and** `SettingsData` property values, groups them by their
  own property-editor alias, and calls that nested editor's `GetReferences()` (e.g. a Media Picker
  living inside a Block List/Block Grid block) to collect `UmbracoEntityReference`s. This means Block
  List and Block Grid nested media references are natively tracked out of the box, and relations exist
  for draft-only saves, not just published content.
- **Secondary signal (fallback / pre-delete safety net)**: a scan-based check ported from the proven
  Python script — search each content item's currently-saved property values (read via Umbraco's
  `IContent.Properties` / `Property.GetValue(published: false)` API, **not** raw SQL) for the media
  item's GUID (with and without hyphens) and file path/filename, case-insensitively. This is
  editor-agnostic: it doesn't matter which property editor stored the reference or whether that
  editor implements `IDataValueReference` correctly.

  The scan runs in two places: (a) optionally, as a "deep scan" the auditor can request for extra
  confidence on the full list, and (b) **mandatorily**, as an immediate per-item re-check the moment
  before any bulk-delete actually executes (see spec edge case: "protect against deleting items that
  turn out to be in use"). A delete only proceeds for an item that both the relation table *and* the
  fresh scan agree is unreferenced.

  **Scope note (added post-`/speckit-analyze`)**: the optional, user-triggered "deep scan" mode
  described in (a) is explicitly **deferred to a future enhancement** — it is not exposed via any v1
  endpoint or UI control. Only the mandatory per-item pre-delete/pre-purge re-check in (b) is in scope
  for this version. If deep-scan is added later, it needs its own contract parameter (e.g. `deepScan`
  on `POST /run`) and a UI control; until then, `MediaAuditItem.detectionSource` never produces `Scan`
  or `Both` (see data-model.md).

**Rationale**: Relying on relations alone is the "correct", performant, Umbraco-native mechanism
(Principle I) and resolves FR-004 (drafts are covered — confirmed, relations are written on save).
But it has one documented gap this feature cannot silently assume away per Principle II: relations
are only (re)computed when content is saved through Umbraco's normal save pipeline. Content that was
bulk-imported, migrated from an older Umbraco version, or written directly to the database can have
stale or missing relations despite genuinely containing a reference. Since this feature adds a
**destructive** action (bulk delete), shipping on relations alone would let a legitimate but
untracked reference be silently deleted. The scan-based check is exactly what the supplied Python
script already validates in practice against real client data, so reusing that logic as a safety net
— rather than trusting relations unconditionally — is the direct application of "validate assumptions
rather than make them."

Nested Content (the older, largely superseded block-style editor) could not be confirmed via source
in this research pass to implement `IDataValueReference` with the same certainty as Block List/Block
Grid. This is a known residual risk, explicitly mitigated by the scan-based layer above, which does
not depend on which editor stored the value.

**Folder-level references (found during User Story 3 implementation)**: a property can pick a media
*folder* itself (e.g. a gallery/slideshow block that picks a folder and renders whatever's inside it
at request time) rather than each child file individually. Umbraco then records the relation on the
folder node, never on its children — so without an explicit fix, every file in such a folder would be
misclassified "Unused" (and eligible for delete) despite being genuinely rendered on the site. Fix:
both classification (`MediaAuditService.ClassifyMedia`) and usage-detail lookup (`GetUsagesAsync`)
fall back to checking whether *any ancestor folder* is itself referenced (relation-based for
classification, relation- and scan-based for usage-detail) before concluding a file is unused.

**Resolved for User Story 4's pre-delete re-check**: rather than re-implementing this logic a third
time in `MediaDeleteService`, its mandatory pre-delete re-check calls `GetUsagesAsync` directly (an
item is still-unused only if it resolves zero usages) — so the ancestor-folder fix applies to the
one place it matters most (actually preventing deletion) automatically, with no separately-maintained
copy of the check to keep in sync.

**Alternatives considered**:
- *Relations only*: rejected as the sole mechanism — see gap above; unacceptable given the delete
  feature.
- *Scan only (i.e., port the Python script's raw-SQL approach as-is)*: rejected as the **primary**
  mechanism — it requires loading every content item's full property data into memory on every audit
  run, ignoring the indexed relation data Umbraco already maintains, which risks the SC-002 (60s /
  10k items) performance target on larger sites. Retained as the secondary/verification layer instead,
  where its cost is paid only for the (typically much smaller) "unused" candidate set about to be
  deleted, or on-demand for a deep scan.
- *Raw SQL against `umbracoPropertyData` (as the Python script does)*: rejected for the C# port —
  the Python script's schema comment says "same across both backends for Umbraco v10"; that schema
  detail was not re-verified against Umbraco v17 and must not be assumed stable across major versions
  per Principle II. Reading property values through `IContent.Properties` instead uses Umbraco's own
  supported API, which is guaranteed to work regardless of underlying schema or database provider
  (SQLite, SQL Server, or other Umbraco-supported databases), and eliminates the driver-detection
  complexity (`pyodbc`, ODBC driver version probing) the Python script needed to handle manually.

**Source**: [Tracking References (17.latest)](https://docs.umbraco.com/umbraco-cms/17.latest/extend-your-project/backoffice-extensions/property-editors/tracking.md), `BlockValuePropertyValueEditorBase.GetBlockValueReferences` (Umbraco-CMS source, `contrib` branch, `src/Umbraco.Infrastructure/PropertyEditors/`), [IDataValueReference API docs](https://apidocs.umbraco.com/v10/csharp/api/Umbraco.Cms.Core.PropertyEditors.IDataValueReference.html), [reference/media_audit.py](./reference/media_audit.py)

---

## 5. Delete behavior (FR-014/FR-015/FR-018) — updated after clarification session

**Decision**: Two-step, both admin-only:

1. **Delete (FR-014)** — moves the confirmed-unused, selected media item(s) to the Umbraco Recycle
   Bin via `IMediaService.MoveToRecycleBin(IMedia)`, the same outcome as manually deleting media from
   the Media section tree. Reversible; not an irreversible hard delete.
2. **Purge (FR-018)** — a separate, more strongly-confirmed action that permanently removes
   specific, already-trashed items on demand, by calling `IMediaService.Delete(IMedia)` individually
   for each selected item that is still `Trashed`. **This is not** `IMediaService.EmptyRecycleBin()`
   — that method is all-or-nothing (purges *everything* currently in the Recycle Bin, confirmed via
   API docs/community sources) and would risk deleting unrelated trashed items a different editor put
   there deliberately. Calling `Delete()` per selected item achieves the same permanent-removal
   outcome while staying scoped to exactly what the admin picked. Before purging, each item's
   `Trashed` state is re-checked; an item no longer trashed (e.g., restored by someone else since
   being soft-deleted) is skipped and reported back, per the spec's edge case for this scenario.

**Rationale**: Matches native Umbraco UX/expectations (Principle I) for step 1, and gives a safety net
for a destructive action by default. The clarification session surfaced that soft-delete alone doesn't
actually reclaim disk space until the Recycle Bin is emptied — since "reclaim storage space" is
explicitly part of User Story 1's motivation, an on-demand purge step is needed for the feature to
fully deliver that outcome, not just defer it silently to whatever recycle-bin housekeeping the site
happens to have. Using `Delete()` per item instead of `EmptyRecycleBin()` keeps the purge scoped and
auditable (each purged item can be attributed in the deletion log, §10) rather than an untargeted bulk
operation with side effects outside this feature's control.

**Alternatives considered**:
- *Immediate permanent delete only, no Recycle Bin step* — rejected; unnecessarily risky as the only
  option for a first version.
- *`EmptyRecycleBin()` as the purge mechanism* — rejected; it is untargeted (empties the entire bin,
  not just this feature's items), so it can't honor "purge only these specific items" and risks
  destroying other trashed content unrelated to this audit.
- *Rely entirely on Umbraco's own scheduled recycle-bin cleanup, no purge action at all* — rejected
  per the clarification: it doesn't satisfy "reclaim storage space" on demand, only eventually.

**Source**: [IMediaService API docs](https://apidocs.umbraco.com/v12/csharp/api/Umbraco.Cms.Core.Services.IMediaService.html), community confirmation of `EmptyRecycleBin()` being all-or-nothing vs. `Delete()` being selective/by-id (see references above); if implementation reveals different current-version semantics for the installed Umbraco v17 package version, this decision must be re-verified against that version's `IMediaService` API docs before merging.

---

## 6. Media metadata (file size, type) — resolves FR-006 data gap

**Decision**: File size and extension are read from Umbraco's own built-in Media property aliases
(`umbracoBytes`, `umbracoExtension`) rather than re-derived from disk or added as new custom fields.
(The Python script's CSV did not include a size column at all — this is new for the dashboard, per
FR-006.)

**Rationale**: These are already the values Umbraco itself displays elsewhere in the backoffice for
built-in media types (Image, File, etc.); reusing them keeps the audit consistent with what an editor
already sees on the Media item's own edit view (Principle I — use Umbraco's own data, don't
reinvent it).

**Alternatives considered**: Querying the physical file from disk — rejected; unnecessary I/O per
item and inconsistent if the configured file system provider is remote/cloud storage rather than
local disk.

---

## 7. Testing approach

**Decision**: xUnit for the C# package (unit tests for classification/scanning/delete-safety logic;
integration tests run against a local, seeded Umbraco v17 SQLite instance using Umbraco's own test
scaffolding conventions). Lit components are tested with Web Test Runner, matching the tooling
Umbraco's own backoffice codebase uses for the same component model.

**Rationale**: Keeps the test stack aligned with what Umbraco itself uses for the same layers,
minimizing unfamiliar tooling for future contributors (Principle I/III spirit).

**Alternatives considered**: Vitest for the client tests — plausible alternative, not chosen only
because Web Test Runner is what Umbraco's own extension examples standardize on; revisit in `tasks.md`
if it proves friction during implementation (non-blocking choice, not a constitutional matter).

---

## 8. Multi-language (variant) reference scanning (resolves FR-017)

**Decision**: Both detection layers from §4 must account for every configured language/culture, not
just the default/invariant value:

- **Relation-based signal**: no extra work needed. Umbraco persists property values per
  culture/segment for a variant property, and relation-building (`IDataValueReference.GetReferences`
  → `PersistRelations`) runs against whatever specific value was just saved — including a single
  culture's edit. So a media reference added only in, say, the Spanish variant of a property already
  produces a relation when that Spanish value is saved; nothing about the relation-based layer changes.
- **Scan-based signal**: this *does* require an explicit change from a naive port of the Python
  script. `Property.GetValue()` with no culture argument only returns the invariant/default value —
  for a variant property, the scanner MUST call `Property.GetValue(culture: cultureCode, segment: ...)`
  once per configured language (and segment, if segments are used) on the content item, not just once
  per property. Missing this would silently under-scan non-default-language content, exactly the kind
  of unverified-assumption gap Principle II exists to catch.

**Rationale**: The clarification session determined that *any* language variant containing a
reference must count as "Used" (FR-017) — a translated page still counts as real usage of an image
even if the default-language version doesn't reference it.

**Alternatives considered**: Scanning only the default language — rejected per the clarification
answer; would misclassify variant-only-referenced media as "Unused," which is a direct correctness
bug for any multi-language site given delete is in scope.

**Source**: Confirmed against the same `BlockValuePropertyValueEditorBase`/`IDataValueReference`
mechanism examined in §4 (relation-building operates per saved property value, which is already
per-culture for variant properties); `Property.GetValue(culture, segment)` is Umbraco's documented
API shape for reading a specific variant's value. If a specific Umbraco v17 nuance to this is found
during implementation, re-verify against that version's `IContent`/`IProperty` API docs before relying
on it further.

---

## 9. Member-data scope exclusion (resolves FR-002 clarification)

**Decision**: Usage detection scans page/document content only, via `IContentService`/`IContent`.
Umbraco Member data (`IMemberService`/`IMember`) is explicitly not scanned in this version. A media
item referenced only by a Member property (e.g. a profile photo) is classified "Unused."

**Rationale**: Explicit user decision during clarification, made to keep the MVP scope focused; the
reference Python script this feature is based on also never scanned Member data. Recorded here so the
`MediaReferenceScanner`/relation-check services are deliberately scoped to `IContentService` only —
not an oversight, an intentional boundary — and so future work adding Member coverage has a clear
single addition point (an `IMemberService`-based pass alongside the existing `IContentService` one)
rather than a re-architecture.

**Alternatives considered**: Scanning Members too — deferred to a future enhancement per the
clarification answer, not rejected outright; the data-model and service interfaces (§ data-model.md)
should avoid choices that would make adding it later structurally awkward (e.g., keep "content item"
reference lookups behind an interface rather than hard-coding `IContentService` calls throughout).

---

## 10. Deletion/purge log storage (resolves FR-019)

**Decision**: A new, minimal Umbraco package table — created via a `PackageMigrationPlan` (Umbraco's
documented package-migration mechanism, cited in §2's package-creation research) — stores one row per
delete or purge **action/batch**, not per item: timestamp, acting user id, action type
(`Delete`/`Purge`), item count, total bytes affected, and a compact reference to the affected items
(e.g. a JSON array of media keys/names in a single column). This is the one deliberate exception to
the "no new persistent schema" posture stated elsewhere in this feature's Assumptions/Constraints —
that posture was about *audit results* (recomputed on demand, per spec Assumptions), not the
*deletion log*, which by definition must outlive a single browser session to be useful for
accountability (FR-019).

**Rationale**: The clarification session explicitly rejected one-row-per-item logging as unbounded
growth risk on large bulk operations, and explicitly wanted this scoped as its own record (not purely
delegated to Umbraco's generic audit trail) so the dashboard can show "who deleted/purged what, when"
directly. A package migration is the constitution-compliant (Principle I), officially documented way
for a package to introduce its own schema, keeping it isolated from Umbraco's own core tables.

**Alternatives considered**:
- *One row per item deleted* — rejected per the clarification answer; grows unboundedly with batch
  size.
- *Rely solely on Umbraco's native per-item audit trail, no package-level log* — considered and
  offered to the user as an option; rejected in favor of the batch-summary log because the user
  wanted an explicit, package-owned accountability record rather than only Umbraco's generic trail.
- *In-memory/session-only log* — rejected; would not survive a browser session or backoffice restart,
  defeating the purpose of "for later accountability."

**Source**: Package migration mechanism per [Creating a Package (17.latest)](https://docs.umbraco.com/umbraco-cms/17.latest/extend-your-project/packages/creating-a-package) (§2); no Umbraco-native equivalent found for a package-scoped, batch-level deletion log, hence the new table.

---

## Summary: resolution of spec's pending verification items

| Spec item | Status | Resolution |
|---|---|---|
| FR-003 — nested block-based/Nested Content reference detection | **Resolved for Block List/Block Grid** (confirmed via source); **residual risk for Nested Content** (unconfirmed) | Hybrid detection (§4) — scan-based layer catches gaps regardless of editor |
| FR-004 — draft-state detection | **Resolved** — relations are written on save, not only on publish | Confirmed via official docs (§4) |
| FR-002 — Member-data scope | **Resolved** — explicitly out of scope for this version | §9 |
| FR-017 — multi-language/variant detection | **Resolved** — relation layer needs no change; scan layer must iterate every configured culture/segment | §8 |
| FR-018 — selective purge mechanism | **Resolved** — per-item `Delete()`, not `EmptyRecycleBin()` | §5 |
| FR-019 — deletion log storage | **Resolved** — new package-migration table, one row per batch | §10 |
