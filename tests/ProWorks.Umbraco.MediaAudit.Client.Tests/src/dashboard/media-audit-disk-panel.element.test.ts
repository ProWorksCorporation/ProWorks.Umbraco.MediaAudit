import "../test-support/uui-setup.js";
import { html, fixture, expect, aTimeout } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-disk-panel.element.js";
import type { MediaAuditDiskPanelElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-disk-panel.element.js";
import { stubFetch, jsonResponse } from "../test-support/fetch-stub.js";
import { makeDiskAuditRun, makeDiskItem } from "../test-support/fixtures.js";
import { clickUuiButton, clickUuiCheckbox } from "../test-support/uui-interactions.js";

describe("media-audit-disk-panel", () => {
  let restoreFetch: () => void;

  afterEach(() => {
    restoreFetch?.();
  });

  it("shows the empty state when no disk-only files are found", async () => {
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun({ runAt: null, diskOnlyCount: 0, status: "Complete" }));
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [] });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    expect(el.shadowRoot!.textContent).to.contain("No disk-only files found.");
    expect(el.shadowRoot!.textContent).to.contain("No disk scan has run yet this session.");
  });

  it("lists disk-only files with their relative path, size, and last-modified date", async () => {
    const items = [makeDiskItem({ relativePath: "stray/orphan.jpg", sizeBytes: 2048 })];
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun({ diskOnlyCount: 1 }));
      if (url.endsWith("/disk-items")) return jsonResponse({ items });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    expect(el.shadowRoot!.textContent).to.contain("stray/orphan.jpg");
    expect(el.shadowRoot!.textContent).to.contain("2.0 KB");
  });

  it("hides selection controls and delete buttons for a non-admin", async () => {
    const items = [makeDiskItem()];
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun());
      if (url.endsWith("/disk-items")) return jsonResponse({ items });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel .isAdmin=${false}></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    expect(el.shadowRoot!.querySelector("uui-checkbox")).to.be.null;
    const buttonLabels = [...el.shadowRoot!.querySelectorAll("uui-button")].map((b) => b.textContent!.trim());
    expect(buttonLabels.some((label) => label.includes("Delete"))).to.be.false;
  });

  it("selecting all files and clicking Delete Selected opens the confirmation with every file", async () => {
    const items = [makeDiskItem({ relativePath: "a.jpg" }), makeDiskItem({ relativePath: "b.jpg" })];
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun());
      if (url.endsWith("/disk-items")) return jsonResponse({ items });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel .isAdmin=${true}></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    const selectAll = el.shadowRoot!.querySelector("uui-checkbox")!;
    await clickUuiCheckbox(selectAll);
    await el.updateComplete;

    const deleteSelectedButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) =>
      b.textContent!.includes("Delete Selected")
    )!;
    await clickUuiButton(deleteSelectedButton);
    await el.updateComplete;

    const confirm = el.shadowRoot!.querySelector("media-audit-disk-delete-confirm")!;
    expect((confirm as unknown as { items: unknown[] }).items).to.have.length(2);
  });

  it("Delete All opens the confirmation with every currently-listed file, with none pre-selected", async () => {
    const items = [makeDiskItem({ relativePath: "a.jpg" }), makeDiskItem({ relativePath: "b.jpg" })];
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun());
      if (url.endsWith("/disk-items")) return jsonResponse({ items });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel .isAdmin=${true}></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    const deleteAllButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.trim() === "Delete All")!;
    await clickUuiButton(deleteAllButton);
    await el.updateComplete;

    const confirm = el.shadowRoot!.querySelector("media-audit-disk-delete-confirm")!;
    expect((confirm as unknown as { items: unknown[] }).items).to.have.length(2);
  });

  it("Delete All is disabled when the disk-only list is empty", async () => {
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun({ diskOnlyCount: 0 }));
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [] });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel .isAdmin=${true}></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    const deleteAllButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.trim() === "Delete All")!;
    expect(deleteAllButton.hasAttribute("disabled")).to.be.true;
  });
  it("scan() starts a disk scan and reports scanning progress to the dashboard", async () => {
    const requests: string[] = [];
    restoreFetch = stubFetch((url, init) => {
      requests.push(`${init?.method ?? "GET"} ${url}`);
      if (url.endsWith("/disk-scan")) return jsonResponse(makeDiskAuditRun({ status: "Running" }));
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun({ runAt: null }));
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [] });
      throw new Error(`unexpected request: ${url}`);
    });

    const states: boolean[] = [];
    const el = await fixture<MediaAuditDiskPanelElement>(
      html`<media-audit-disk-panel @disk-scan-state=${(e: CustomEvent<{ scanning: boolean }>) => states.push(e.detail.scanning)}></media-audit-disk-panel>`
    );
    await aTimeout(0);

    await el.scan();
    await el.updateComplete;

    expect(requests.some((r) => r.startsWith("POST") && r.endsWith("/disk-scan"))).to.be.true;
    expect(states.at(-1)).to.be.true;
  });

  it("resumes polling when shown while a scan is still running server-side", async () => {
    let summaryCalls = 0;
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) {
        summaryCalls++;
        return jsonResponse(makeDiskAuditRun(summaryCalls === 1 ? { status: "Running" } : { status: "Complete", diskOnlyCount: 1 }));
      }
      if (url.endsWith("/disk-items")) return jsonResponse({ items: summaryCalls > 1 ? [makeDiskItem({ relativePath: "late.jpg" })] : [] });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel></media-audit-disk-panel>`);
    await aTimeout(1800);
    await el.updateComplete;

    expect(summaryCalls).to.be.greaterThan(1);
    expect(el.shadowRoot!.textContent).to.contain("late.jpg");
  });

  it("Export CSV is disabled when the disk-only list is empty", async () => {
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun({ diskOnlyCount: 0 }));
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [] });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    const exportButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.trim() === "Export CSV")!;
    expect(exportButton.hasAttribute("disabled")).to.be.true;
  });

  it("Export CSV downloads from /disk-export", async () => {
    const requests: string[] = [];
    restoreFetch = stubFetch((url) => {
      requests.push(url);
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun());
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [makeDiskItem()] });
      if (url.endsWith("/disk-export")) return new Response("RelativePath,Url,SizeBytes,LastModifiedUtc", { headers: { "Content-Type": "text/csv" } });
      throw new Error(`unexpected request: ${url}`);
    });

    const el = await fixture<MediaAuditDiskPanelElement>(html`<media-audit-disk-panel></media-audit-disk-panel>`);
    await aTimeout(0);
    await el.updateComplete;

    const exportButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.trim() === "Export CSV")!;
    await clickUuiButton(exportButton);
    await aTimeout(0);

    expect(requests.some((url) => url.endsWith("/disk-export"))).to.be.true;
  });

  it("Hide asks the dashboard to close the panel", async () => {
    restoreFetch = stubFetch((url) => {
      if (url.endsWith("/disk-summary")) return jsonResponse(makeDiskAuditRun());
      if (url.endsWith("/disk-items")) return jsonResponse({ items: [] });
      throw new Error(`unexpected request: ${url}`);
    });

    let closed = false;
    const el = await fixture<MediaAuditDiskPanelElement>(
      html`<media-audit-disk-panel @disk-panel-close=${() => (closed = true)}></media-audit-disk-panel>`
    );
    await aTimeout(0);
    await el.updateComplete;

    const hideButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.trim() === "Hide")!;
    await clickUuiButton(hideButton);

    expect(closed).to.be.true;
  });
});
