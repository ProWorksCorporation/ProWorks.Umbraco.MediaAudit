# Answer key — spoilers

Don't open this until you've wired up the files per README.md and run the audit. This is what
each file was *built* to test — it's a suggestion for how to assign them, not a record of how
you actually did. Match it against your own wiring, not blindly against the "intended" column.

| File | Intended test | Expected classification |
|---|---|---|
| `conference-room-wide.jpg` | left unreferenced | **Unused** |
| `summer-picnic.png` | left unreferenced | **Unused** |
| `small-badge.png` | left unreferenced | **Unused** |
| `style-guide.pdf` | left unreferenced | **Unused** |
| `banner-wide-blue.jpg` | Media Picker property, published | **Used**, detection source Relation |
| `architecture-diagram.png` | inline Rich Text Editor embed, published | **Used**, likely detection source Scan (RTE references often aren't native relations) |
| `headshot-square.jpg` | inside a Block List block's Media Picker, published | **Used**, detection source Relation (validates native Block List relation coverage) |
| `roadmap-preview.png` | Media Picker property, left as **Draft** (never published) | **Used** — relations don't require publish state; usage detail should show `Draft` |
| `staff-portrait.jpg` | Member Type's Media Picker property only | **Unused** — Member data is out of scope by design (FR-002); this is correct, not a bug |
| `ribbon-banner-teal.png` | Media Picker property, **non-default language variant only** | **Used**; usage detail's `culture` should report the non-default language, not the default one |

If your actual result disagrees with the "expected" column *for the wiring you actually did*,
that's worth investigating. If you assigned files differently than the suggestions above, judge
each by its own wiring, not by this table's file-to-purpose mapping.
