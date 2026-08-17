# Test media seed set

10 neutral placeholder files (9 images + 1 PDF), sized and typed for variety (0.4 KB up to
217 KB; jpg/png/pdf). Names and on-image captions are deliberately generic — nothing here hints
at what each file is "supposed" to be, so you can wire them up to content however you like and
judge the audit's output cold, rather than confirming what you already expect to see.

## Upload

Drag the whole `test-media-seed` folder onto the Media section's list view (Umbraco recreates
the folder from a dropped directory), or drag the files in individually.

## Suggested wiring (pick your own subset/assignment — this is just a menu)

To get good coverage per [quickstart.md](../quickstart.md), across the 10 files try to end up
with a mix of:

- Several left **completely unreferenced**.
- One picked in a **Media Picker** property on a published content item.
- One embedded **inline in a Rich Text Editor** property, published.
- One picked inside a **Block List** block's own Media Picker property, published.
- One picked in a Media Picker property but the content item left **as Draft** (never published).
- One picked only via a **Member Type**'s Media Picker property (Members section, not Content).
- One picked only in the **non-default language variant** of a content item (requires a second
  language configured under Settings → Languages), with the default-language variant's picker
  left empty.

Assign these however you want, in whatever order — don't look at `answer-key.md` first.

## After you've wired things up and run the audit

Open [`answer-key.md`](./answer-key.md) to see what each file was built to test and what the
dashboard *should* report for it, and check your actual results against it.

Not covered here: the ~10,000-item volume test for SC-002 — this set is sized for functional
correctness, not load testing.
