import { playwrightLauncher } from "@web/test-runner-playwright";
import { esbuildPlugin } from "@web/dev-server-esbuild";

// research.md §7 — Web Test Runner is the tooling Umbraco's own backoffice extension examples
// standardize on for Lit component tests, matching this feature's client stack.
//
// esbuildPlugin({ ts: true }) is required, not optional - @web/test-runner serves files to a real
// browser as-is with no built-in TS transform, and these elements use legacy/experimental
// decorators (@customElement, @property, @state) that only esbuild's own tsconfig-aware decorator
// handling understands; without it every *.element.ts import fails to parse in the browser.
// rootDir is widened to the repo root (not this package's own folder) because the test files import
// the real element sources from ../../../../src/UmbracoMediaAudit/Client/src/... - the dev server
// can't serve a path that escapes its own rootDir, so without this every such import 404s.
export default {
  // `files` (test-file discovery) is resolved relative to CWD, unlike `rootDir` (dev-server URL
  // mapping, resolved relative to this config file) - keep this CWD-relative even though rootDir
  // below points elsewhere, or file discovery finds nothing.
  files: "src/**/*.test.ts",
  rootDir: "../..",
  nodeResolve: true,
  plugins: [esbuildPlugin({ ts: true, tsconfig: "./tsconfig.json" })],
  browsers: [playwrightLauncher({ product: "chromium" })],
};
