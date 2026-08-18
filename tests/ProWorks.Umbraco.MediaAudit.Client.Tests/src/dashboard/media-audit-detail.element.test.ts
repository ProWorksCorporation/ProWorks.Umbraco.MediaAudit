import "../test-support/uui-setup.js";
import { html, fixture, expect, aTimeout } from "@open-wc/testing";
import "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-detail.element.js";
import type { MediaAuditDetailElement } from "../../../../src/ProWorks.Umbraco.MediaAudit/Client/src/dashboard/media-audit-detail.element.js";
import { stubFetch, jsonResponse } from "../test-support/fetch-stub.js";
import { makeItem, makeUsage } from "../test-support/fixtures.js";

describe("media-audit-detail", () => {
  let restoreFetch: () => void;

  afterEach(() => {
    restoreFetch?.();
  });

  it("renders nothing until an item is set", async () => {
    const el = await fixture<MediaAuditDetailElement>(html`<media-audit-detail></media-audit-detail>`);
    expect(el.shadowRoot!.textContent!.trim()).to.equal("");
  });

  it("fetches usages for the item's key and renders one row per usage", async () => {
    const item = makeItem({ usageStatus: "Used" });
    let requestedUrl: string | undefined;
    restoreFetch = stubFetch((url) => {
      requestedUrl = url;
      return jsonResponse({
        mediaKey: item.key,
        usages: [
          makeUsage({ contentName: "Home", culture: null, publishState: "Published" }),
          makeUsage({ contentName: "About (draft)", culture: "en-US", publishState: "Draft" }),
        ],
      });
    });

    const el = await fixture<MediaAuditDetailElement>(html`<media-audit-detail .item=${item}></media-audit-detail>`);
    await aTimeout(20);

    expect(requestedUrl).to.equal(`/umbraco/media-audit/api/v1/items/${item.key}/usages`);
    const rows = [...el.shadowRoot!.querySelectorAll("uui-table-row")];
    expect(rows).to.have.length(2);
    expect(rows[0].textContent).to.contain("Home");
    expect(rows[1].textContent).to.contain("About (draft)");
    expect(rows[1].textContent).to.contain("en-US");
  });

  it("shows the stale-relation warning when a Used item resolves zero usages", async () => {
    const item = makeItem({ usageStatus: "Used" });
    restoreFetch = stubFetch(() => jsonResponse({ mediaKey: item.key, usages: [] }));

    const el = await fixture<MediaAuditDetailElement>(html`<media-audit-detail .item=${item}></media-audit-detail>`);
    await aTimeout(20);

    expect(el.shadowRoot!.textContent).to.contain("no active references could be found");
  });

  it("shows an error tag when the request fails", async () => {
    const item = makeItem({ usageStatus: "Used" });
    restoreFetch = stubFetch(() => jsonResponse({ message: "boom" }, 500));

    const el = await fixture<MediaAuditDetailElement>(html`<media-audit-detail .item=${item}></media-audit-detail>`);
    await aTimeout(20);

    const errorTag = el.shadowRoot!.querySelector('uui-tag[color="danger"]');
    expect(errorTag).to.exist;
    expect(errorTag!.textContent).to.contain("Could not load usages");
  });

  it("re-fetches when the item changes to a different key", async () => {
    const firstItem = makeItem({ usageStatus: "Used" });
    const secondItem = makeItem({ usageStatus: "Used" });
    const requestedUrls: string[] = [];
    restoreFetch = stubFetch((url) => {
      requestedUrls.push(url);
      return jsonResponse({ mediaKey: "x", usages: [] });
    });

    const el = await fixture<MediaAuditDetailElement>(html`<media-audit-detail .item=${firstItem}></media-audit-detail>`);
    await aTimeout(20);

    el.item = secondItem;
    await aTimeout(20);

    expect(requestedUrls).to.deep.equal([
      `/umbraco/media-audit/api/v1/items/${firstItem.key}/usages`,
      `/umbraco/media-audit/api/v1/items/${secondItem.key}/usages`,
    ]);
  });
});
