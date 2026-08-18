// Both UUIBooleanInputElement (<uui-checkbox>/<uui-toggle>) and UUIButtonElement (<uui-button>)
// override the inherited HTMLElement.click() as an ASYNC method that clicks their own internal
// native <input>/<button> - that's what actually toggles `checked` (firing the real "change" event)
// or fires the click that bubbles out to a listener bound on the host tag in our own templates
// (media-audit-*.element.ts's `@click=${...}` bindings). Calling `.click()` without awaiting it
// leaves that internal click - and everything it triggers - still pending when the next assertion
// runs, a real race that intermittently (not always) fails depending on exact microtask timing.
// Always await both through these helpers rather than calling `.click()` directly.
export async function clickUuiCheckbox(checkbox: Element): Promise<void> {
  await (checkbox as unknown as { click(): Promise<void> }).click();
}

export async function clickUuiButton(button: Element): Promise<void> {
  await (button as unknown as { click(): Promise<void> }).click();
}
