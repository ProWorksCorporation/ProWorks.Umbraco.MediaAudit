// Side-effect import - registers <uui-button>, <uui-box>, <uui-checkbox>, <uui-table>, <uui-tag>,
// <uui-loader>, etc. as real custom elements. Without this, the elements under test still render
// (Lit doesn't care whether a custom element is defined), but the UUI tags stay undefined/inert:
// no shadow DOM, no visible "headline" attribute text, native attribute-only behavior - so anything
// asserting on rendered text/structure inside them would silently fail. Imported once per test file
// via `import "../test-support/uui-setup.js";` at the top, before any element/fixture usage.
import "@umbraco-cms/backoffice/external/uui";
