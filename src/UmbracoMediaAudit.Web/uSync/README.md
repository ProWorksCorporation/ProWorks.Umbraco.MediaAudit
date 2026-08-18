# uSync — sample site schema-as-code

This folder is [uSync](https://github.com/KevinJump/uSync)'s export of the sample site's schema
(Document Types, Data Types, Member Types, Languages, Media Types, Relation Types). It re-exports
automatically on every app startup when there are local changes, and — this is the useful part —
**imports automatically on startup for anyone else** who clones the repo and runs the site against
a blank database: uSync detects the DB is missing this schema and creates it from these files,
no manual clicking required.

## Why this exists

[`TestSchemaSeeder.cs`](../TestSchema/TestSchemaSeeder.cs) creates the doctypes/datatypes needed
to manually test the Media Audit dashboard against
[`specs/001-media-usage-audit/test-media-seed/`](../../../specs/001-media-usage-audit/test-media-seed/)
in-process, via Umbraco's own C# services, the first time the site boots against a blank DB. uSync
then exports whatever that (and anything you add by hand afterward) produces to these files, so the
schema is committed and reproducible — not just reconstructable by re-running the seeder against a
matching code version.

## What's tracked vs. not

- `DataTypes/`, `ContentTypes/`, `MemberTypes/`, `Languages/`, `MediaTypes/`, `RelationTypes/` —
  committed. This is real schema-as-code.
- `Media/` — **not** committed (see `.gitignore`). uSync's Media handler only exports node
  metadata (name, path, dates) for whatever's currently uploaded to `wwwroot/media/`, which is
  itself gitignored as generated local dev data. Committing one without the other would leave
  dangling references on a fresh clone.

## The test schema, concretely

- **Audit Test Page** (`auditTestPage`, allowed at root, varies by culture)
  - Title (plain text)
  - Body Text (Rich Text Editor — media embed via the toolbar's default image/media button)
  - Featured Media (Media Picker, varies by culture)
  - Content Blocks (Block List → one block: **Audit Test - Testimonial Block**)
- **Audit Test - Testimonial Block** (`auditTestTestimonialBlock`, element type)
  - Avatar (Media Picker)
- **Member** (built-in member type) gained a **Profile Photo** (Media Picker) property
- **French (France)** (`fr-FR`) added as a second, non-default language
