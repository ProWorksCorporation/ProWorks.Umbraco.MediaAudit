import "../test-support/uui-setup.js";
import { html, fixture, expect } from "@open-wc/testing";
import "../../../../src/UmbracoMediaAudit/Client/src/dashboard/media-audit-delete-confirm.element.js";
import type { MediaAuditDeleteConfirmElement } from "../../../../src/UmbracoMediaAudit/Client/src/dashboard/media-audit-delete-confirm.element.js";
import { makeItem } from "../test-support/fixtures.js";
import { clickUuiButton } from "../test-support/uui-interactions.js";

describe("media-audit-delete-confirm", () => {
  it("shows the item count and lists each item's name", async () => {
    const items = [makeItem({ name: "a.jpg" }), makeItem({ name: "b.jpg" })];
    const el = await fixture<MediaAuditDeleteConfirmElement>(
      html`<media-audit-delete-confirm .items=${items}></media-audit-delete-confirm>`
    );

    const box = el.shadowRoot!.querySelector("uui-box")!;
    expect(box.getAttribute("headline")).to.equal("Delete 2 item(s)?");
    const names = [...el.shadowRoot!.querySelectorAll("li")].map((li) => li.textContent);
    expect(names).to.deep.equal(["a.jpg", "b.jpg"]);
  });

  it("says items move to the Recycle Bin and is reversible, unlike the purge confirmation's wording", async () => {
    const el = await fixture<MediaAuditDeleteConfirmElement>(
      html`<media-audit-delete-confirm .items=${[makeItem()]}></media-audit-delete-confirm>`
    );

    const text = el.shadowRoot!.textContent!.toLowerCase();
    expect(text).to.contain("recycle bin");
    expect(text).to.contain("not permanently removed");
    // Distinct from media-audit-purge-confirm's deliberately stronger, irreversible-action wording.
    expect(text).to.not.contain("cannot be undone");
  });

  it("calls onCancel when Cancel is clicked", async () => {
    let cancelled = false;
    const el = await fixture<MediaAuditDeleteConfirmElement>(html`
      <media-audit-delete-confirm .items=${[makeItem()]} .onCancel=${() => (cancelled = true)}></media-audit-delete-confirm>
    `);

    const cancelButton = el.shadowRoot!.querySelectorAll("uui-button")[0];
    await clickUuiButton(cancelButton);

    expect(cancelled).to.be.true;
  });

  it("calls onConfirm when the delete button is clicked", async () => {
    let confirmed = false;
    const el = await fixture<MediaAuditDeleteConfirmElement>(html`
      <media-audit-delete-confirm .items=${[makeItem()]} .onConfirm=${() => (confirmed = true)}></media-audit-delete-confirm>
    `);

    const deleteButton = el.shadowRoot!.querySelectorAll("uui-button")[1];
    await clickUuiButton(deleteButton);

    expect(confirmed).to.be.true;
  });

  it("disables both buttons while confirming, so a second click can't fire mid-request", async () => {
    const el = await fixture<MediaAuditDeleteConfirmElement>(
      html`<media-audit-delete-confirm .items=${[makeItem()]} .confirming=${true}></media-audit-delete-confirm>`
    );

    const buttons = [...el.shadowRoot!.querySelectorAll("uui-button")];
    expect(buttons).to.have.length(2);
    for (const button of buttons) {
      expect(button.hasAttribute("disabled")).to.be.true;
    }
  });
});
