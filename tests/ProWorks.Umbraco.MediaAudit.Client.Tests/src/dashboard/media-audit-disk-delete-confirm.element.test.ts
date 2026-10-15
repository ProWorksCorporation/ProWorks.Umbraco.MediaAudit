import "../test-support/uui-setup.js";
import { html, fixture, expect } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-disk-delete-confirm.element.js";
import type { MediaAuditDiskDeleteConfirmElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-disk-delete-confirm.element.js";
import { makeDiskItem } from "../test-support/fixtures.js";
import { clickUuiButton, clickUuiCheckbox } from "../test-support/uui-interactions.js";

describe("media-audit-disk-delete-confirm", () => {
  it("shows the file count and lists each file's relative path", async () => {
    const items = [makeDiskItem({ relativePath: "stray/a.txt" }), makeDiskItem({ relativePath: "stray/b.txt" })];
    const el = await fixture<MediaAuditDiskDeleteConfirmElement>(
      html`<media-audit-disk-delete-confirm .items=${items}></media-audit-disk-delete-confirm>`
    );

    const box = el.shadowRoot!.querySelector("uui-box")!;
    expect(box.getAttribute("headline")).to.equal("Permanently delete 2 file(s) from disk?");
    const paths = [...el.shadowRoot!.querySelectorAll("li")].map((li) => li.textContent);
    expect(paths).to.deep.equal(["stray/a.txt", "stray/b.txt"]);
  });

  it("warns this is permanent with no Recycle Bin", async () => {
    const el = await fixture<MediaAuditDiskDeleteConfirmElement>(
      html`<media-audit-disk-delete-confirm .items=${[makeDiskItem()]}></media-audit-disk-delete-confirm>`
    );

    const text = el.shadowRoot!.textContent!.toLowerCase();
    expect(text).to.contain("permanently");
    expect(text).to.contain("cannot be undone");
  });

  it("keeps the confirm button disabled until the acknowledgment checkbox is ticked", async () => {
    const el = await fixture<MediaAuditDiskDeleteConfirmElement>(
      html`<media-audit-disk-delete-confirm .items=${[makeDiskItem()]}></media-audit-disk-delete-confirm>`
    );

    const confirmButton = el.shadowRoot!.querySelectorAll("uui-button")[1];
    expect(confirmButton.hasAttribute("disabled")).to.be.true;

    const checkbox = el.shadowRoot!.querySelector("uui-checkbox")!;
    await clickUuiCheckbox(checkbox);
    await el.updateComplete;

    expect(confirmButton.hasAttribute("disabled")).to.be.false;
  });

  it("calls onConfirm only after acknowledging", async () => {
    let confirmed = false;
    const el = await fixture<MediaAuditDiskDeleteConfirmElement>(html`
      <media-audit-disk-delete-confirm .items=${[makeDiskItem()]} .onConfirm=${() => (confirmed = true)}></media-audit-disk-delete-confirm>
    `);

    const checkbox = el.shadowRoot!.querySelector("uui-checkbox")!;
    await clickUuiCheckbox(checkbox);
    await el.updateComplete;

    const confirmButton = el.shadowRoot!.querySelectorAll("uui-button")[1];
    await clickUuiButton(confirmButton);

    expect(confirmed).to.be.true;
  });

  it("calls onCancel when Cancel is clicked", async () => {
    let cancelled = false;
    const el = await fixture<MediaAuditDiskDeleteConfirmElement>(html`
      <media-audit-disk-delete-confirm .items=${[makeDiskItem()]} .onCancel=${() => (cancelled = true)}></media-audit-disk-delete-confirm>
    `);

    const cancelButton = el.shadowRoot!.querySelectorAll("uui-button")[0];
    await clickUuiButton(cancelButton);

    expect(cancelled).to.be.true;
  });
});
