import "../test-support/uui-setup.js";
import { html, fixture, expect, aTimeout } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-dashboard.element.js";
import type { MediaAuditDashboardElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-dashboard.element.js";
import { stubFetch, jsonResponse } from "../test-support/fetch-stub.js";
import { makeAuditRun, makeItem } from "../test-support/fixtures.js";
import { clickUuiButton, clickUuiCheckbox } from "../test-support/uui-interactions.js";

/**
 * MediaAuditDashboardElement defers ALL of its initial data loading until Umbraco's UMB_AUTH_CONTEXT
 * is consumed (see the constructor) - a bare fixture in this test environment never provides that
 * context, so #loadSummary/#loadItems/#checkAdmin never fire on their own. Rather than hand-rolling
 * a fake context provider for Umbraco's context-request protocol, these tests poke the element's
 * @state() fields directly (TS `private` is erased at the esbuild/JS level, so this is a normal
 * property assignment that still goes through Lit's reactive setter) to put the component into a
 * given state, then verify rendering and click-driven behavior from there - including calls the
 * click handlers make back into MediaAuditRepository, verified via a real fetch stub. This is a
 * pragmatic, common pattern for testing Lit elements whose loading is gated behind app-level
 * context/auth that a component test shouldn't need to fully reconstruct.
 */
type DashboardInternals = MediaAuditDashboardElement & {
  _run?: ReturnType<typeof makeAuditRun>;
  _items: ReturnType<typeof makeItem>[];
  _totalItems: number;
  _statusFilter?: "Used" | "Unused";
  _isAdmin: boolean;
  _selectedForDelete: Set<string>;
  _selectedItem?: ReturnType<typeof makeItem>;
  _sort: string;
  _sortDirection: string;
};

async function dashboardFixture(state: Partial<DashboardInternals> = {}): Promise<DashboardInternals> {
  const el = (await fixture(html`<media-audit-dashboard></media-audit-dashboard>`)) as DashboardInternals;
  Object.assign(el, state);
  await el.updateComplete;
  return el;
}

describe("media-audit-dashboard", () => {
  let restoreFetch: () => void;

  afterEach(() => {
    restoreFetch?.();
  });

  it("renders the Total/Used/Unused summary pills with formatted sizes", async () => {
    const el = await dashboardFixture({
      _run: makeAuditRun({ totalScanned: 10, usedCount: 6, usedSizeBytes: 6144, unusedCount: 4, unusedSizeBytes: 2048 }),
    });

    const pills = [...el.shadowRoot!.querySelectorAll(".filter-pill")];
    expect(pills.map((p) => p.textContent!.trim())).to.deep.equal([
      "Total: 10",
      "Used: 6 (6.0 KB)",
      "Unused: 4 (2.0 KB)",
    ]);
  });

  it("marks the active status filter pill", async () => {
    const el = await dashboardFixture({ _run: makeAuditRun(), _statusFilter: "Used" });
    const pills = [...el.shadowRoot!.querySelectorAll(".filter-pill")];
    const activePill = pills.find((p) => p.hasAttribute("active"));
    expect(activePill!.textContent).to.contain("Used:");
  });

  it("re-fetches items with the matching status when a pill is clicked", async () => {
    let requestedUrl: string | undefined;
    restoreFetch = stubFetch((url) => {
      requestedUrl = url;
      return jsonResponse({ page: 1, pageSize: 50, totalItems: 0, items: [] });
    });

    const el = await dashboardFixture({ _run: makeAuditRun(), _statusFilter: "Unused" });
    const usedPill = [...el.shadowRoot!.querySelectorAll(".filter-pill")].find((p) => p.textContent!.includes("Used:"));
    (usedPill as HTMLElement).click();
    await aTimeout(20);

    expect(requestedUrl).to.contain("status=Used");
  });

  it("renders one row per item with formatted size and last-modified date", async () => {
    const el = await dashboardFixture({
      _items: [makeItem({ name: "hero.jpg", sizeBytes: 2048 }), makeItem({ name: "old.pdf", sizeBytes: null })],
      _totalItems: 2,
    });

    const rows = [...el.shadowRoot!.querySelectorAll(".grid-row:not(.grid-header)")];
    expect(rows).to.have.length(2);
    expect(rows[0].textContent).to.contain("hero.jpg");
    expect(rows[0].textContent).to.contain("2.0 KB");
    expect(rows[1].textContent).to.contain("old.pdf");
    expect(rows[1].textContent).to.contain("—");
  });

  it("shows a message instead of a table when there are no matching items", async () => {
    const el = await dashboardFixture({ _items: [], _totalItems: 0, _statusFilter: "Unused" });
    expect(el.shadowRoot!.textContent).to.contain("No unused media found.");
  });

  it("re-fetches with the new sort field/direction when a sortable header is clicked", async () => {
    const requestedUrls: string[] = [];
    restoreFetch = stubFetch((url) => {
      requestedUrls.push(url);
      return jsonResponse({ page: 1, pageSize: 50, totalItems: 0, items: [] });
    });

    const el = await dashboardFixture({ _items: [makeItem()], _totalItems: 1 });
    const sizeHeader = [...el.shadowRoot!.querySelectorAll(".sort-header")].find((b) => b.textContent!.includes("Size"));
    (sizeHeader as HTMLElement).click();
    await aTimeout(20);

    expect(requestedUrls[0]).to.contain("sort=sizeBytes");
    expect(requestedUrls[0]).to.contain("sortDirection=asc");
  });

  it("expands an accordion detail row on click, showing <media-audit-detail> only for Used items", async () => {
    const usedItem = makeItem({ name: "used.jpg", usageStatus: "Used" });
    const unusedItem = makeItem({ name: "unused.jpg", usageStatus: "Unused" });
    restoreFetch = stubFetch(() => jsonResponse({ mediaKey: "x", usages: [] }));

    const el = await dashboardFixture({ _items: [usedItem, unusedItem], _totalItems: 2 });
    const rows = [...el.shadowRoot!.querySelectorAll(".grid-row:not(.grid-header)")];

    (rows[0] as HTMLElement).click();
    await el.updateComplete;
    expect(el.shadowRoot!.querySelector("media-audit-detail")).to.exist;

    (rows[0] as HTMLElement).click();
    await el.updateComplete;
    (rows.find((r) => r.textContent?.includes("unused.jpg")) as HTMLElement).click();
    await el.updateComplete;
    expect(el.shadowRoot!.querySelector("media-audit-detail")).to.not.exist;
  });

  it("hides admin-only controls (Delete Selected, Deletion Log) for a non-admin", async () => {
    const el = await dashboardFixture({ _isAdmin: false, _statusFilter: "Unused", _items: [makeItem()], _totalItems: 1 });
    const buttonLabels = [...el.shadowRoot!.querySelectorAll("uui-button")].map((b) => b.textContent!.trim());

    expect(buttonLabels.some((t) => t.includes("Delete Selected"))).to.be.false;
    expect(buttonLabels.some((t) => t.includes("Deletion Log"))).to.be.false;
    expect(el.shadowRoot!.querySelectorAll(".grid.has-checkbox")).to.have.length(0);
  });

  it("shows checkboxes and a Delete Selected button for an admin viewing Unused items", async () => {
    const item = makeItem({ usageStatus: "Unused" });
    const el = await dashboardFixture({ _isAdmin: true, _statusFilter: "Unused", _items: [item], _totalItems: 1 });

    expect(el.shadowRoot!.querySelector(".grid.has-checkbox")).to.exist;
    const deleteButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.includes("Delete Selected"));
    expect(deleteButton).to.exist;
    expect(deleteButton!.hasAttribute("disabled")).to.be.true;

    const rowCheckbox = el.shadowRoot!.querySelector(".grid-row:not(.grid-header) uui-checkbox")!;
    await clickUuiCheckbox(rowCheckbox);
    await el.updateComplete;

    expect(deleteButton!.hasAttribute("disabled")).to.be.false;
    expect(deleteButton!.textContent).to.contain("Delete Selected (1)");
  });

  it("posts the selected keys to /delete and reloads when the delete confirmation is confirmed", async () => {
    const item = makeItem({ usageStatus: "Unused" });
    let deleteBody: unknown;
    let getItemsCallCount = 0;
    restoreFetch = stubFetch((url, init) => {
      if (init?.method === "POST" && url.endsWith("/delete")) {
        deleteBody = init.body ? JSON.parse(init.body as string) : undefined;
        return jsonResponse({ deleted: [item.key], skipped: [], logEntryId: 1 });
      }
      if (url.includes("/summary")) return jsonResponse(makeAuditRun());
      getItemsCallCount++;
      return jsonResponse({ page: 1, pageSize: 50, totalItems: 0, items: [] });
    });

    const el = await dashboardFixture({
      _isAdmin: true,
      _statusFilter: "Unused",
      _items: [item],
      _totalItems: 1,
      _selectedForDelete: new Set([item.key]),
    });

    const deleteButton = [...el.shadowRoot!.querySelectorAll("uui-button")].find((b) => b.textContent!.includes("Delete Selected"))!;
    await clickUuiButton(deleteButton);
    await el.updateComplete;

    const confirmEl = el.shadowRoot!.querySelector("media-audit-delete-confirm")!;
    const confirmButton = confirmEl.shadowRoot!.querySelectorAll("uui-button")[1];
    await clickUuiButton(confirmButton);
    await aTimeout(20);

    expect(deleteBody).to.deep.equal({ mediaKeys: [item.key] });
    expect(el._selectedForDelete.size).to.equal(0);
    expect(el.shadowRoot!.querySelector("media-audit-delete-confirm")).to.not.exist;
    expect(getItemsCallCount).to.equal(1);
  });
});
