import "../test-support/uui-setup.js";
import { html, fixture, expect, aTimeout } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-deletion-log.element.js";
import type { MediaAuditDeletionLogElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-deletion-log.element.js";
import { stubFetch, jsonResponse } from "../test-support/fetch-stub.js";
import { makeDeletionLogEntry } from "../test-support/fixtures.js";
import { clickUuiButton, clickUuiCheckbox } from "../test-support/uui-interactions.js";

describe("media-audit-deletion-log", () => {
  let restoreFetch: () => void;

  afterEach(() => {
    restoreFetch?.();
  });

  it("fetches the log on connect and shows an empty message when there are no entries", async () => {
    restoreFetch = stubFetch(() => jsonResponse({ page: 1, pageSize: 50, totalItems: 0, entries: [] }));

    const el = await fixture<MediaAuditDeletionLogElement>(html`<media-audit-deletion-log></media-audit-deletion-log>`);
    await aTimeout(20);

    expect(el.shadowRoot!.textContent).to.contain("No delete or purge actions recorded yet.");
  });

  it("renders one row per entry with the action type, item count, and formatted size", async () => {
    restoreFetch = stubFetch(() =>
      jsonResponse({
        page: 1,
        pageSize: 50,
        totalItems: 2,
        entries: [
          makeDeletionLogEntry({ id: 1, actionType: "Delete", itemCount: 2, totalSizeBytes: 2048, skippedCount: 1 }),
          makeDeletionLogEntry({ id: 2, actionType: "Purge", itemCount: 1, totalSizeBytes: 512, skippedCount: 0 }),
        ],
      })
    );

    const el = await fixture<MediaAuditDeletionLogElement>(html`<media-audit-deletion-log></media-audit-deletion-log>`);
    await aTimeout(20);

    const rows = [...el.shadowRoot!.querySelectorAll("uui-table-row")];
    expect(rows).to.have.length(2);
    expect(rows[0].textContent).to.contain("Delete");
    expect(rows[0].textContent).to.contain("2.0 KB");
    expect(rows[0].textContent).to.contain("1");
    expect(rows[1].textContent).to.contain("Purge");
  });

  it("only shows a Purge button for Delete entries with items actually affected", async () => {
    restoreFetch = stubFetch(() =>
      jsonResponse({
        page: 1,
        pageSize: 50,
        totalItems: 3,
        entries: [
          makeDeletionLogEntry({ id: 1, actionType: "Delete", itemCount: 2 }),
          makeDeletionLogEntry({ id: 2, actionType: "Purge", itemCount: 1 }),
          makeDeletionLogEntry({ id: 3, actionType: "Delete", itemCount: 0 }),
        ],
      })
    );

    const el = await fixture<MediaAuditDeletionLogElement>(html`<media-audit-deletion-log></media-audit-deletion-log>`);
    await aTimeout(20);

    const rows = [...el.shadowRoot!.querySelectorAll("uui-table-row")];
    const purgeButtonsPerRow = rows.map((row) => row.querySelectorAll("uui-button").length);
    expect(purgeButtonsPerRow).to.deep.equal([1, 0, 0]);
  });

  it("opens the purge confirmation with that entry's items when Purge is clicked", async () => {
    const entry = makeDeletionLogEntry({ id: 1, actionType: "Delete", itemCount: 2 });
    restoreFetch = stubFetch(() => jsonResponse({ page: 1, pageSize: 50, totalItems: 1, entries: [entry] }));

    const el = await fixture<MediaAuditDeletionLogElement>(html`<media-audit-deletion-log></media-audit-deletion-log>`);
    await aTimeout(20);

    expect(el.shadowRoot!.querySelector("media-audit-purge-confirm")).to.not.exist;

    const purgeButton = el.shadowRoot!.querySelector("uui-table-row uui-button")!;
    await clickUuiButton(purgeButton);
    await el.updateComplete;

    const confirm = el.shadowRoot!.querySelector("media-audit-purge-confirm") as unknown as { items: unknown };
    expect(confirm).to.exist;
    expect(confirm.items).to.deep.equal(entry.items);
  });

  it("purges and reloads the log when the purge confirmation is confirmed", async () => {
    const entry = makeDeletionLogEntry({ id: 1, actionType: "Delete", itemCount: 2 });
    let getCallCount = 0;
    let purgeRequestBody: unknown;
    restoreFetch = stubFetch((url, init) => {
      if (init?.method === "POST" && url.endsWith("/purge")) {
        purgeRequestBody = init.body ? JSON.parse(init.body as string) : undefined;
        return jsonResponse({ purged: entry.items.map((i) => i.key), skipped: [], logEntryId: 99 });
      }
      getCallCount++;
      return jsonResponse({ page: 1, pageSize: 50, totalItems: 1, entries: [entry] });
    });

    const el = await fixture<MediaAuditDeletionLogElement>(html`<media-audit-deletion-log></media-audit-deletion-log>`);
    await aTimeout(20);

    await clickUuiButton(el.shadowRoot!.querySelector("uui-table-row uui-button")!);
    await el.updateComplete;

    const purgeConfirm = el.shadowRoot!.querySelector("media-audit-purge-confirm")! as unknown as { updateComplete: Promise<unknown>; shadowRoot: ShadowRoot };
    const checkbox = purgeConfirm.shadowRoot.querySelector("uui-checkbox")!;
    await clickUuiCheckbox(checkbox);
    await purgeConfirm.updateComplete;

    const confirmButton = purgeConfirm.shadowRoot.querySelectorAll("uui-button")[1];
    await clickUuiButton(confirmButton);
    await aTimeout(20);

    expect(purgeRequestBody).to.deep.equal({ mediaKeys: entry.items.map((i) => i.key) });
    expect(getCallCount).to.equal(2);
    expect(el.shadowRoot!.querySelector("media-audit-purge-confirm")).to.not.exist;
  });
});
