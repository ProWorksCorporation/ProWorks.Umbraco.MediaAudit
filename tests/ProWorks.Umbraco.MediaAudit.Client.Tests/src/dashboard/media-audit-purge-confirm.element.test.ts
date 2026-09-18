import "../test-support/uui-setup.js";
import { html, fixture, expect } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-purge-confirm.element.js";
import type { MediaAuditPurgeConfirmElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-purge-confirm.element.js";
import type { DeletionLogItem } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/api/media-audit.repository.js";
import { clickUuiCheckbox, clickUuiButton } from "../test-support/uui-interactions.js";

function makeLogItem(overrides: Partial<DeletionLogItem> = {}): DeletionLogItem {
  return { key: "11111111-1111-1111-1111-111111111111", name: "one.jpg", ...overrides };
}

describe("media-audit-purge-confirm", () => {
  it("shows the item count, lists names, and warns this is permanent", async () => {
    const items = [makeLogItem({ name: "a.jpg" }), makeLogItem({ name: "b.jpg" })];
    const el = await fixture<MediaAuditPurgeConfirmElement>(
      html`<media-audit-purge-confirm .items=${items}></media-audit-purge-confirm>`
    );

    const box = el.shadowRoot!.querySelector("uui-box")!;
    expect(box.getAttribute("headline")).to.equal("Permanently purge 2 item(s)?");
    expect(el.shadowRoot!.textContent).to.contain("cannot be undone");
    const names = [...el.shadowRoot!.querySelectorAll("li")].map((li) => li.textContent);
    expect(names).to.deep.equal(["a.jpg", "b.jpg"]);
  });

  it("keeps the purge button disabled until the acknowledgment checkbox is checked", async () => {
    const el = await fixture<MediaAuditPurgeConfirmElement>(
      html`<media-audit-purge-confirm .items=${[makeLogItem()]}></media-audit-purge-confirm>`
    );

    const purgeButton = el.shadowRoot!.querySelectorAll("uui-button")[1] as HTMLElement;
    const checkbox = el.shadowRoot!.querySelector("uui-checkbox")!;

    expect(purgeButton.hasAttribute("disabled")).to.be.true;

    await clickUuiCheckbox(checkbox);
    await el.updateComplete;

    expect(purgeButton.hasAttribute("disabled")).to.be.false;
  });

  it("does not call onConfirm from a click while still unacknowledged (button stays disabled)", async () => {
    let confirmed = false;
    const el = await fixture<MediaAuditPurgeConfirmElement>(html`
      <media-audit-purge-confirm .items=${[makeLogItem()]} .onConfirm=${() => (confirmed = true)}></media-audit-purge-confirm>
    `);

    const purgeButton = el.shadowRoot!.querySelectorAll("uui-button")[1];
    await clickUuiButton(purgeButton);

    expect(confirmed).to.be.false;
  });

  it("calls onConfirm once acknowledged and the purge button is clicked", async () => {
    let confirmed = false;
    const el = await fixture<MediaAuditPurgeConfirmElement>(html`
      <media-audit-purge-confirm .items=${[makeLogItem()]} .onConfirm=${() => (confirmed = true)}></media-audit-purge-confirm>
    `);

    const checkbox = el.shadowRoot!.querySelector("uui-checkbox")!;
    await clickUuiCheckbox(checkbox);
    await el.updateComplete;

    const purgeButton = el.shadowRoot!.querySelectorAll("uui-button")[1];
    await clickUuiButton(purgeButton);

    expect(confirmed).to.be.true;
  });

  it("calls onCancel regardless of acknowledgment state", async () => {
    let cancelled = false;
    const el = await fixture<MediaAuditPurgeConfirmElement>(html`
      <media-audit-purge-confirm .items=${[makeLogItem()]} .onCancel=${() => (cancelled = true)}></media-audit-purge-confirm>
    `);

    const cancelButton = el.shadowRoot!.querySelectorAll("uui-button")[0];
    await clickUuiButton(cancelButton);

    expect(cancelled).to.be.true;
  });
});
