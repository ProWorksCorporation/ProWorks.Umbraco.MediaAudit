#!/usr/bin/env node
// T055 (quickstart.md scenario 8): cross-validate the Media Audit dashboard's classification
// against reference/media_audit.py's independent implementation, over the exact same live
// database, and report any discrepancy against what research.md §4/§9 predicts.
//
// This drives the REAL dashboard in a real browser rather than reconstructing Umbraco's Bearer-
// token auth flow by hand (a plain `credentials: "include"` fetch 401s - see
// media-audit-local-dev.md's known gotcha) - it logs into the already-running local sample site,
// clicks Run Audit, and reads the dashboard's own /summary and /items responses as they happen.
//
// Prerequisites:
//   - The sample site is already running: dotnet run --project src/UmbracoMediaAudit.Web
//     (defaults to https://localhost:44318 - override with SITE_URL if different)
//   - Run reference/media_audit.py against the SAME site's Umbraco.sqlite.db first, with
//     --output pointed at a CSV path, then pass that path as this script's one argument.
//   - `playwright` + its chromium browser must be installed - this repo already has both under
//     tests/UmbracoMediaAudit.Client.Tests/node_modules (installed for T058), resolved below via
//     createRequire rather than a plain `import "playwright"` (bare-specifier ESM resolution is
//     based on THIS file's own location, not CWD, and NODE_PATH does not affect it either - this
//     script deliberately lives alongside media_audit.py, not inside that test project).
//     Run from anywhere:
//       node specs/001-media-usage-audit/reference/cross-validate.mjs <path-to-python-csv>
//
// Admin credentials match src/UmbracoMediaAudit.Web/appsettings.Development.json's unattended
// install (local dev only, not a secret worth guarding - see media-audit-local-dev.md).
import { readFileSync } from "node:fs";
import { createRequire } from "node:module";
import { pathToFileURL, fileURLToPath } from "node:url";
import path from "node:path";

const here = path.dirname(fileURLToPath(import.meta.url));
const clientTestsPkgJson = path.resolve(here, "../../../tests/UmbracoMediaAudit.Client.Tests/package.json");
const requireFromClientTests = createRequire(clientTestsPkgJson);
const playwrightModule = await import(pathToFileURL(requireFromClientTests.resolve("playwright")).href);
// playwright is published as CJS - ESM interop sometimes surfaces its exports under .default
// instead of as named exports, depending on how the loader's static analysis reads it.
const { chromium } = playwrightModule.default ?? playwrightModule;

const SITE_URL = process.env.SITE_URL ?? "https://localhost:44318";
const EMAIL = "admin@example.com";
const PASSWORD = "1234567890!";
const csvPath = process.argv[2];

if (!csvPath) {
  console.error("Usage: node cross-validate.mjs <path-to-media_audit.py's --output CSV>");
  process.exit(1);
}

// --- 1. Parse the Python script's CSV (id, name, ..., page_count, referenced_on) -----------------
function parseReferenceCsv(path) {
  const lines = readFileSync(path, "utf-8").trim().split(/\r?\n/);
  const header = lines[0].split(",");
  const idIdx = header.indexOf("id");
  const nameIdx = header.indexOf("name");
  const pageCountIdx = header.indexOf("page_count");

  const byId = new Map();
  for (const line of lines.slice(1)) {
    // Naive CSV split is fine here - none of this script's own fields contain commas.
    const cols = line.split(",");
    byId.set(Number(cols[idIdx]), {
      name: cols[nameIdx],
      pageCount: Number(cols[pageCountIdx]),
    });
  }
  return byId;
}

// --- 2. Drive the real dashboard and capture what it actually classifies -------------------------
async function runDashboardAudit() {
  const browser = await chromium.launch({ headless: true, ignoreHTTPSErrors: true });
  const context = await browser.newContext({ ignoreHTTPSErrors: true });
  const page = await context.newPage();

  const captured = { summary: null, unused: null, used: null };
  page.on("response", async (res) => {
    const url = res.url();
    if (!url.includes("/media-audit/api/v1/") || res.request().method() !== "GET") return;
    try {
      if (url.includes("/summary")) captured.summary = await res.json();
      if (url.includes("/items")) {
        const body = await res.json();
        // The dashboard's own filter state at request time tells us which bucket this is.
        if (url.includes("status=Used")) captured.used = body;
        else if (url.includes("status=Unused") || !url.includes("status=")) captured.unused = body;
      }
    } catch {
      /* ignore non-JSON bodies, e.g. an interim 401 */
    }
  });

  await page.goto(`${SITE_URL}/umbraco`, { waitUntil: "load" });
  await page.waitForSelector("#username-input", { timeout: 30000 });
  await page.fill("#username-input", EMAIL);
  await page.fill("#password-input", PASSWORD);
  await page.keyboard.press("Enter");
  await page.waitForURL(/\/umbraco(\/|$)/, { timeout: 30000 });
  await page.waitForTimeout(3000);

  await page.goto(`${SITE_URL}/umbraco/section/media`, { waitUntil: "load" });
  await page.waitForTimeout(2000);

  await page.getByText("Media Audit", { exact: false }).first().click();
  await page.waitForTimeout(1500);

  await page.getByRole("button", { name: /run audit/i }).first().click();

  for (let i = 0; i < 60; i++) {
    await page.waitForTimeout(1000);
    if (captured.summary && captured.summary.status !== "Running") break;
  }

  // Dashboard defaults to the "Unused" filter pill - click "Used" too so we see both buckets.
  // (Not anchored to string-start: Lit's template whitespace means the pill's actual text content
  // has leading whitespace before "Used:", which a `^`-anchored regex won't match.)
  await page.getByText(/Used:\s*\d/).first().click();
  await page.waitForTimeout(1000);

  await browser.close();
  return captured;
}

// --- 3. Compare and report ------------------------------------------------------------------------
const reference = parseReferenceCsv(csvPath);
const dashboard = await runDashboardAudit();

if (!dashboard.summary) {
  console.error("Could not capture a completed /summary response - is the dashboard reachable?");
  process.exit(1);
}

const dashboardStatusById = new Map();
for (const item of dashboard.unused?.items ?? []) dashboardStatusById.set(item.id, "Unused");
for (const item of dashboard.used?.items ?? []) dashboardStatusById.set(item.id, "Used");

console.log(`Dashboard : totalScanned=${dashboard.summary.totalScanned} used=${dashboard.summary.usedCount} unused=${dashboard.summary.unusedCount}`);
console.log(`Reference : totalFiles=${reference.size} referenced=${[...reference.values()].filter((r) => r.pageCount > 0).length} unreferenced=${[...reference.values()].filter((r) => r.pageCount === 0).length}`);
console.log();

let mismatches = 0;
for (const [id, ref] of reference) {
  const dashboardStatus = dashboardStatusById.get(id);
  const referenceStatus = ref.pageCount > 0 ? "Used" : "Unused";
  if (dashboardStatus === undefined) {
    console.log(`? id=${id} (${ref.name}) - reference says ${referenceStatus}, not present in either dashboard bucket (folder-excluded item, or a paging gap)`);
    continue;
  }
  if (dashboardStatus !== referenceStatus) {
    mismatches++;
    console.log(`✗ id=${id} (${ref.name}) - dashboard=${dashboardStatus}, reference=${referenceStatus}`);
  }
}

if (mismatches === 0) {
  console.log(`✓ All ${reference.size} items agree between the dashboard and the reference script.`);
} else {
  console.log(`\n${mismatches} unexplained discrepancy/discrepancies found - see research.md §4 (scan-vs-relation gap) and §9 (Member data) before treating any of these as a bug.`);
}
