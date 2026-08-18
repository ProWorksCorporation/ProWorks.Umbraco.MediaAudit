# Test media seed set

10 neutral placeholder files (9 images + 1 PDF), sized and typed for variety (0.4 KB up to
217 KB; jpg/png/pdf). Names and on-image captions are deliberately generic — nothing here hints
at what each file is "supposed" to be, so you can wire them up to content however you like and
judge the audit's output cold, rather than confirming what you already expect to see.

## Upload

Drag the whole `test-media-seed` folder onto the Media section's list view (Umbraco recreates
the folder from a dropped directory), or drag the files in individually.

## Schema

The sample site (`src/ProWorks.Umbraco.MediaAudit.Web`) seeds this automatically on first boot against a
blank DB — see [`TestSchemaSeeder.cs`](../../../src/ProWorks.Umbraco.MediaAudit.Web/TestSchema/TestSchemaSeeder.cs)
and [`uSync/README.md`](../../../src/ProWorks.Umbraco.MediaAudit.Web/uSync/README.md) for how. Nothing to
create by hand:

- **Audit Test Page** doctype (allowed at root, varies by culture) — properties: Title, Body Text
  (Rich Text Editor), **Featured Media** (Media Picker, varies by culture), Content Blocks (Block
  List).
- **Audit Test - Testimonial Block** — the one block type available inside Content Blocks, with
  its own **Avatar** Media Picker property.
- **Member** (Members section) gained a **Profile Photo** Media Picker property.
- **French (France)** (`fr-FR`) is available as a second language under Settings → Languages.

## Suggested wiring (pick your own subset/assignment — this is just a menu)

To get good coverage per [quickstart.md](../quickstart.md), across the 10 files try to end up
with a mix of, all using the schema above:

- Several left **completely unreferenced**.
- One picked in an **Audit Test Page**'s Featured Media property, published.
- One embedded **inline in Body Text** (Rich Text Editor's image/media toolbar button), published.
- One picked in a Content Blocks block's **Avatar** property, published.
- One picked in Featured Media but the page left **as Draft** (never published).
- One picked only via the **Member**'s Profile Photo property (Members section, not Content).
- One picked only in Featured Media on the **fr-FR variant** of a page (Settings → Languages has
  fr-FR already), with the default-language (en-US) variant's Featured Media left empty.

Assign these however you want, in whatever order — don't look at `answer-key.md` first.

## After you've wired things up and run the audit

Open [`answer-key.md`](./answer-key.md) to see what each file was built to test and what the
dashboard *should* report for it, and check your actual results against it.

Not covered here: the ~10,000-item volume test for SC-002 — this set is sized for functional
correctness, not load testing.
