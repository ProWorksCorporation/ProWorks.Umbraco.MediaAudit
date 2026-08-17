import { playwrightLauncher } from "@web/test-runner-playwright";

// research.md §7 — Web Test Runner is the tooling Umbraco's own backoffice extension examples
// standardize on for Lit component tests, matching this feature's client stack.
export default {
  files: "src/**/*.test.ts",
  nodeResolve: true,
  browsers: [playwrightLauncher({ product: "chromium" })],
};
